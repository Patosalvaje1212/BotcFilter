using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

public partial class ImageFilterProgram
{
    static readonly Rgba32 outColorGood = new(24, 148, 243);
    static readonly Hsv outColorGoodHSL = ColorSpaceConverter.ToHsv(outColorGood);
    static readonly Rgba32 outColorBad = new(195, 19, 25);
    static readonly Hsv outColorBadHSL = ColorSpaceConverter.ToHsv(outColorBad);
    static readonly Rgba32 outColorWhite = new(250, 250, 245);
    static readonly Hsv outColorWhiteHSL = ColorSpaceConverter.ToHsv(outColorWhite);

    static readonly Rgba32 outColorYellow = new(222, 205, 0);
    static readonly Hsv outColorYellowHSL = ColorSpaceConverter.ToHsv(outColorYellow);

    static readonly Rgba32 outColorGreen = new(158, 196, 54);
    static readonly Hsv outColorGreenHSL = ColorSpaceConverter.ToHsv(outColorGreen);

    private static string outputPath;

    public static bool UseMultithreading { get; set; } = true;
    private static bool? _threadingAvailable;

    /// <summary>
    /// True when the current runtime can actually run managed threads in parallel.
    /// Desktop: always true.
    /// Browser (WASM): true only if WasmEnableThreads was set AND the page
    /// is cross-origin isolated with SharedArrayBuffer available.
    /// </summary>
    public static bool IsMultithreadingSupported
    {
        get
        {
            if (_threadingAvailable.HasValue)
                return _threadingAvailable.Value;

            _threadingAvailable = DetectThreadingSupport();
            return _threadingAvailable.Value;
        }
    }

    private static bool DetectThreadingSupport()
    {
        // 1. Desktop / non-WASM: threading is always available.
        if (!OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi())
            return true;

        // 2. Try the official API (available in .NET 11+).
        try
        {
            var prop = typeof(RuntimeFeature).GetProperty(
                "IsMultithreadingSupported",
                BindingFlags.Public | BindingFlags.Static);

            if (prop != null)
                return (bool)prop.GetValue(null)!;
        }
        catch
        {
            // Reflection failed; fall through to JS check.

        }

        return false;
    }

    /// <summary>
    /// Whether the app should actually run image processing in parallel.
    /// Combines the user toggle with the runtime capability check.
    /// </summary>
    public static bool CanParallelize =>
        UseMultithreading && IsMultithreadingSupported;

    // =====================================================================
    //  CLI ENTRY POINT
    // =====================================================================
    static void Main(string[] args)
    {
        // When hosted in the browser, never run the CLI loop.
        if (OperatingSystem.IsBrowser())
            return;

        string inputPath;
        int toProcess = int.MaxValue;

        if (args.Length < 2)
        {
            if (args.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Usage: <INPUT DIRECTORY> <OUTPUT DIRECTORY> [numberToProcess(default=all)]");

                Console.ForegroundColor = ConsoleColor.Green;

                Console.WriteLine();
                Console.WriteLine("     There must be a file called \"filter.png\" on the INPUT DIRECTORY for the filter to work.");
                Console.WriteLine();
                Console.WriteLine("     If not \"numberToProcess\" is give, the program will output all .png images in the INPUT DIRECTORY");
                Console.WriteLine("     Otherwise it will process the given number of images (starting with the ones with most recent changes)");

                Console.ResetColor();

                Console.WriteLine();
                Console.WriteLine("Press Enter to exit.");

                Console.ReadLine();
                return;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Input Folder: ");
                Console.ResetColor();

                inputPath = Console.ReadLine()?.Trim() ?? throw new Exception("Wrong text input");

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Output Folder: ");
                Console.ResetColor();

                outputPath = Console.ReadLine()?.Trim() ?? throw new Exception("Wrong text input");

                int nToProcess = -1;

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Images to process (default = all): ");
                Console.ResetColor();

                if (int.TryParse(Console.ReadLine()?.Trim(), out int res))
                    nToProcess = res - 1;

                if (nToProcess >= 0)
                    toProcess = nToProcess;
            }
        }
        else
        {
            inputPath = args[0];
            outputPath = args[1];

            if (args.Length >= 3)
            {
                string amountToProcess = args[2];
                int nToProcess = int.Parse(amountToProcess) - 1;

                if (nToProcess >= 0)
                    toProcess = nToProcess;
            }
        }

        if (args.Contains("--no-threads"))
            UseMultithreading = false;


        if (!Directory.Exists(inputPath))
        {
            Console.WriteLine($"Error: [INPUT DIRECTORY] '{inputPath}' not found.");
            return;
        }

        if (!Directory.Exists(outputPath))
        {
            Console.WriteLine($"Error: [OUTPUT DIRECTORY] '{inputPath}' not found.");
            return;
        }

        if (!File.Exists(inputPath + "filter.png"))
        {
            Console.WriteLine($"Error: Filter png file '{inputPath}' not found. There must be a file called filter.png with the filter image inside the [INPUT DIRECTORY]");
            return;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        using Image<Rgba32> filter = Image.Load<Rgba32>(inputPath + "filter.png");

        int i = 0;
        IEnumerable<string> files = Directory.EnumerateFiles(inputPath, "*.png");

        var filesToProcess = files
            .OrderByDescending(File.GetLastWriteTime)
            .Where(f => Path.GetFileNameWithoutExtension(f) != "filter")
            .Take(toProcess == int.MaxValue ? int.MaxValue : toProcess + 1)
            .ToList();

        if (CanParallelize)
        {
            // Desktop: true OS threads. WASM: runtime-managed worker threads.
            Parallel.ForEach(filesToProcess, file =>
            {
                ProcessFile(file, filter);
            });
        }
        else
        {
            // Sequential fallback (single-threaded WASM or user disabled)
            foreach (var file in filesToProcess)
                ProcessFile(file, filter);
        }

        stopwatch.Stop();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"{i} images rendered in {stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds} elapsed");
    }

    // =====================================================================
    //  FILE-BASED PROCESSING (CLI PATH ONLY)
    // =====================================================================
    private static void ProcessFile(string path, Image<Rgba32> filter)
    {
        bool alt = Path.GetFileNameWithoutExtension(path).EndsWith("-alt");

        using Image<Rgba32> sourceImage = Image.Load<Rgba32>(path);

        string originName = Path.GetFileNameWithoutExtension(path);
        Console.Out.WriteLine("Processing " + outputPath + originName + "...");

        var (goodBytes, badBytes) = ProcessImage(sourceImage, filter, alt);

        File.WriteAllBytes(outputPath + originName + (alt ? "-Loric.png"  : "-Good.png"), goodBytes);
        File.WriteAllBytes(outputPath + originName + (alt ? "-Fabled.png" : "-Evil.png"), badBytes);

        Console.Out.WriteLine("Done " + originName + "!");
    }


    // =====================================================================
    //  JS EXPORT
    // =====================================================================
    /// <summary>
    /// Called from JavaScript. Returns a single packed byte[]:
    ///   bytes [0..3]  = int32 little-endian length of the first (Good/Loric) PNG
    ///   bytes [4..]   = first PNG
    ///   remaining     = second (Evil/Fabled) PNG
    /// </summary>
    [JSExport]
    public static byte[] ProcessImageFromBytes(byte[] sourceBytes, byte[] filterBytes, bool alt)
    {
        using var sourceImage = Image.Load<Rgba32>(sourceBytes);
        using var filter      = Image.Load<Rgba32>(filterBytes);

        var (good, bad) = ProcessImage(sourceImage, filter, alt);

        var result = new byte[4 + good.Length + bad.Length];
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), good.Length);
        good.CopyTo(result, 4);
        bad.CopyTo(result, 4 + good.Length);
        return result;
    }

    /// <summary>
    /// Called from JavaScript. Returns an array of byte[]:
    ///   bytes [0..3]  = int32 little-endian length of the first (Good/Loric) PNG
    ///   bytes [4..]   = first PNG
    ///   remaining     = second (Evil/Fabled) PNG
    /// </summary>
    [JSExport]
    public static byte[] ProcessBulkImageFromBytes(byte[] packedSources, byte[] filterBytes, bool alt)
    {
        // 1. Unpack the flat input into N source images.
        byte[][] sourceBytes = UnpackByteArrays(packedSources);

        // 2. Process (parallel or sequential).
        var results = new (byte[] good, byte[] bad)[sourceBytes.Length];

        if (CanParallelize)
        {
            Parallel.For(0, sourceBytes.Length, i =>
            {
                using var sourceImage = Image.Load<Rgba32>(sourceBytes[i]);
                using var filter      = Image.Load<Rgba32>(filterBytes);
                results[i] = ProcessImage(sourceImage, filter, alt);
            });
        }
        else
        {
            using var filter = Image.Load<Rgba32>(filterBytes);
            for (int i = 0; i < sourceBytes.Length; i++)
            {
                using var sourceImage = Image.Load<Rgba32>(sourceBytes[i]);
                results[i] = ProcessImage(sourceImage, filter, alt);
            }
        }

        // 3. Pack all results into one flat byte[].
        return PackResults(results.ToList());
    }

    // =====================================================================
    //  CORE PROCESSING (SHARED BETWEEN CLI AND WASM)
    //  Takes a loaded source image + filter image, returns two PNG blobs.
    // =====================================================================
    private static (byte[] good, byte[] bad) ProcessImage(Image<Rgba32> sourceImage, Image<Rgba32> filter, bool alt)
    {
        // ---------- Resize / pad ----------
        if (sourceImage.Height > filter.Height)
        {
            sourceImage.Mutate(x =>
            {
                x.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(filter.Width, filter.Height),
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.RobidouxSharp,
                    CenterCoordinates = new PointF(0.5f, 0.5f)
                });
                x.Pad(filter.Width, filter.Height, Color.Transparent);
            });
        }
        else if (sourceImage.Width > filter.Width)
        {
            sourceImage.Mutate(x =>
            {
                x.Resize(new ResizeOptions()
                {
                    Size = new Size(filter.Width, filter.Height),
                    Mode = ResizeMode.Max,
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.Lanczos3
                });
                x.Pad(filter.Width, filter.Height, Color.Transparent);
            });
        }

        using Image<Rgba32> destG = new Image<Rgba32>(Configuration.Default, sourceImage.Width, sourceImage.Height);
        using Image<Rgba32> destB = new Image<Rgba32>(Configuration.Default, sourceImage.Width, sourceImage.Height);

        int height = sourceImage.Height;

        // ---------- Per-pixel filter ----------
        sourceImage.ProcessPixelRows(destG, destB, (sourceAccessor, targetGoodAccessor, targetBadAccessor) =>
        {
            for (int i = 0; i < height; i++)
            {
                Span<Rgba32> sourceRow = sourceAccessor.GetRowSpan(i);
                Span<Rgba32> targetGoodRow = targetGoodAccessor.GetRowSpan(i);
                Span<Rgba32> targetBadRow = targetBadAccessor.GetRowSpan(i);

                for (int x = 0; x < sourceRow.Length; x++)
                {
                    Hsv hsvFilter = ColorSpaceConverter.ToHsv(filter[x, i]);

                    Hsv goodColor = ColorSpaceConverter.ToHsv(Color.Transparent.ToPixel<Rgba32>());
                    Hsv badColor  = ColorSpaceConverter.ToHsv(Color.Transparent.ToPixel<Rgba32>());

                    Rgba32 pixel = sourceRow[x];
                    float alpha = pixel.A / 255f;

                    if (alpha > 0.8)
                    {
                        Hsv hsvPixel = ColorSpaceConverter.ToHsv(pixel);

                        if (hsvPixel.V < 0.85)
                        {
                            var filterPow  = MathF.Pow(hsvFilter.V, 1.75f);
                            var filterPow2 = MathF.Pow(hsvFilter.V, 1.25f);
                            var pixelPow   = MathF.Pow(hsvPixel.V, 1.25f);

                            if (alt)
                            {
                                float darkness = 1f - hsvFilter.V;
                                float orangeAmount = MathF.Pow(darkness, 2f);

                                float baseH = Mix(outColorYellowHSL.H, outColorWhiteHSL.H, pixelPow);
                                float baseS = Mix(outColorYellowHSL.S, outColorWhiteHSL.S, pixelPow);
                                float baseV = Mix(outColorYellowHSL.V, outColorWhiteHSL.V, pixelPow);

                                float orangeH = 0f;
                                float hue = Mix(baseH, orangeH, orangeAmount);

                                float orangeS = MathF.Min(1f, baseS * 1.2f + 0.1f);
                                float sat = Mix(baseS, orangeS, orangeAmount * 0.7f);
                                float val = baseV * hsvFilter.V * (1f - 0.1f * orangeAmount);

                                goodColor = new Hsv(hue, sat, val);

                                badColor = new Hsv(
                                    Mix(outColorGreenHSL.H, outColorWhiteHSL.H, pixelPow) * 0.2f + outColorGreenHSL.H * 0.8f,
                                    Mix(outColorGreenHSL.S, outColorWhiteHSL.S, pixelPow) * 0.4f + filterPow * 0.6f,
                                    Mix(outColorGreenHSL.V, outColorWhiteHSL.V, pixelPow) * filterPow2 * 0.85f
                                );
                            }
                            else
                            {
                                goodColor = new Hsv(
                                    Mix(outColorGoodHSL.H, outColorWhiteHSL.H, pixelPow) * 0.2f + outColorGoodHSL.H * 0.8f,
                                    Mix(outColorGoodHSL.S, outColorWhiteHSL.S, pixelPow) * 0.80f + filterPow * 0.2f,
                                    Mix(outColorGoodHSL.V, outColorWhiteHSL.V, pixelPow) * hsvFilter.V
                                );

                                badColor = new Hsv(
                                    Mix(outColorBadHSL.H, outColorWhiteHSL.H, pixelPow) * 0.2f + outColorBadHSL.H * 0.8f,
                                    Mix(outColorBadHSL.S, outColorWhiteHSL.S, pixelPow) * 0.85f + filterPow * 0.2f,
                                    Mix(outColorBadHSL.V, outColorWhiteHSL.V, pixelPow) * filterPow2 * 0.85f
                                );
                            }
                        }
                        else
                        {
                            goodColor = GetWhiteColor(hsvFilter);
                            badColor  = GetWhiteColor(hsvFilter);
                        }
                    }
                    else
                    {
                        int amount = 20;

                        float oD = ApplyShadow(
                            sourceAccessor: sourceAccessor,
                            i: i,
                            xPos: x,
                            amountX: 2,
                            amountY: amount,
                            sourceHeight: sourceImage.Height,
                            sourceWidth: sourceImage.Width
                        );

                        if (oD > 0)
                        {
                            goodColor = ColorSpaceConverter.ToHsv(Color.Black.ToPixel<Rgba32>());
                            badColor  = ColorSpaceConverter.ToHsv(Color.Black.ToPixel<Rgba32>());
                            alpha = MathF.Pow(oD, 1.25f) * 0.50f;
                        }

                        amount = 5;

                        float nD = ApplyOutline(sourceAccessor, i, x, amount, sourceImage.Height, sourceImage.Width, true);

                        if (nD > 0)
                        {
                            goodColor = GetWhiteColor(hsvFilter);
                            badColor  = GetWhiteColor(hsvFilter);
                            alpha = MathF.Min(MathF.Pow(1 - nD, 0.2f) + 0.25f, 1f);
                        }
                    }

                    Rgba32 gCol = new Rgba32(new Vector4(ColorSpaceConverter.ToRgb(goodColor).ToVector3(), alpha));
                    Rgba32 bCol = new Rgba32(new Vector4(ColorSpaceConverter.ToRgb(badColor).ToVector3(), alpha));

                    targetGoodRow[x] = gCol;
                    targetBadRow[x]  = bCol;
                }
            }
        });

        // ---------- Encode ----------
        var pngEncoder = new PngEncoder()
        {
            TransparentColorMode = PngTransparentColorMode.Clear,
            Threshold = 150,
            FilterMethod = PngFilterMethod.Average,
            CompressionLevel = PngCompressionLevel.BestCompression,
            ColorType = PngColorType.RgbWithAlpha,
            PixelSamplingStrategy = new ExtensivePixelSamplingStrategy(),
            BitDepth = PngBitDepth.Bit4,
            SkipMetadata = true,
        };

        destB.Mutate(res => res.Resize(sourceImage.Width / 2, sourceImage.Height / 2));
        destG.Mutate(res => res.Resize(sourceImage.Width / 2, sourceImage.Height / 2));

        using var msG = new MemoryStream();
        using var msB = new MemoryStream();
        destG.SaveAsPng(msG, pngEncoder);
        destB.SaveAsPng(msB, pngEncoder);

        return (msG.ToArray(), msB.ToArray());
    }

    // =====================================================================
    //  Helpers 
    // =====================================================================

    [JSExport]
    public static void SetMultithreading(bool enabled)
    {
        UseMultithreading = enabled;
    }

    /// <summary>
    /// Unpacks a flat byte[] produced by JS into N separate byte arrays.
    /// Format: [N][len1][len2]...[lenN][data1][data2]...[dataN]
    /// </summary>
    private static byte[][] UnpackByteArrays(byte[] packed)
    {
        int n = BitConverter.ToInt32(packed, 0);

        var lengths = new int[n];
        int headerSize = 4 + 4 * n;
        for (int i = 0; i < n; i++)
            lengths[i] = BitConverter.ToInt32(packed, 4 + 4 * i);

        var result = new byte[n][];
        int offset = headerSize;
        for (int i = 0; i < n; i++)
        {
            result[i] = new byte[lengths[i]];
            Buffer.BlockCopy(packed, offset, result[i], 0, lengths[i]);
            offset += lengths[i];
        }
        return result;
    }

    /// <summary>
    /// Packs N (good, bad) PNG pairs into a single self-describing byte[].
    /// Format: [N][lenGood1][lenBad1][good1][bad1][lenGood2][lenBad2][good2][bad2]...
    /// </summary>
    private static byte[] PackResults(List<(byte[] good, byte[] bad)> results)
    {
        int total = 4; // N
        foreach (var (g, b) in results)
            total += 8 + g.Length + b.Length; // 2 lengths + payloads

        var packed = new byte[total];
        int offset = 0;

        BitConverter.TryWriteBytes(packed.AsSpan(offset, 4), results.Count);
        offset += 4;

        foreach (var (good, bad) in results)
        {
            BitConverter.TryWriteBytes(packed.AsSpan(offset, 4), good.Length);
            offset += 4;
            BitConverter.TryWriteBytes(packed.AsSpan(offset, 4), bad.Length);
            offset += 4;

            good.CopyTo(packed, offset); offset += good.Length;
            bad.CopyTo(packed, offset);  offset += bad.Length;
        }
        return packed;
    }

    static float Mix(float to, float source, float amount)
        => source * amount + to * (1 - amount);

    static Hsv GetWhiteColor(Hsv hsvFilter)
    {
        return new Hsv(
            outColorWhiteHSL.H,
            (outColorWhiteHSL.S * 0.92f + hsvFilter.V * 0.08f) * 0.8f,
            outColorWhiteHSL.V * hsvFilter.V * .2f + 0.8f
        );
    }

    static float ApplyOutline(PixelAccessor<Rgba32> sourceAccessor, int i, int x, int amount, int sourceHeight, int sourceWidth, bool applyOnWhite = false)
    {
        float nearest = float.MaxValue;
        bool f = false;

        for (int k = -amount; k < amount; k++)
        {
            var search = sourceAccessor.GetRowSpan(Math.Clamp(i + k, 0, sourceHeight - 1));

            for (int j = -amount; j < amount; j++)
            {
                int d = k * k + j * j;
                if (d > amount * amount) continue;

                var pixel = search[Math.Clamp(x + j, 0, sourceWidth - 1)];

                if (pixel.A < 20 || (!applyOnWhite && pixel.R == 255 && pixel.G == 255 && pixel.B == 255))
                    continue;

                if (nearest > d)
                {
                    nearest = d;
                    if (nearest == 0)
                    {
                        nearest = 0.1f;
                        f = true;
                        break;
                    }
                }
            }

            if (f) break;
        }

        if (nearest == float.MaxValue) nearest = 0;
        return nearest / (amount * amount);
    }

    static float ApplyShadow(PixelAccessor<Rgba32> sourceAccessor, int i, int xPos, int amountX, int amountY, int sourceHeight, int sourceWidth)
    {
        if (sourceAccessor.GetRowSpan(i)[xPos].A > 40)
            return 0;

        float res = 0;
        for (int k = 0; k < amountY; k++)
        {
            int y = i - k;
            if (y < 0) break;

            int b = int.MaxValue;
            for (int j = -amountX - k; j < amountX + k / 2; j++)
            {
                int x = xPos + j;
                if (x < 0 || x >= sourceWidth) break;
                if (sourceAccessor.GetRowSpan(y)[x].A < 40) break;

                int val = k * k + j * j;
                if (val < b) b = val;
            }

            if (b != int.MaxValue)
            {
                float maxSqd = amountX * 2 * amountX * 2 + amountY * amountY;
                float val = 1 - (b / maxSqd);
                if (res < val) res = val;
            }
        }

        return res;
    }
}