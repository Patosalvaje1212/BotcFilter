using System.Diagnostics;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

class ImageFilterProgram
{

    static readonly Rgba32 outColorGood = new(24, 148, 243);
    static readonly Hsv outColorGoodHSL = ColorSpaceConverter.ToHsv(outColorGood);
    static readonly Rgba32 outColorBad = new(195, 19, 25);
    static readonly Hsv outColorBadHSL = ColorSpaceConverter.ToHsv(outColorBad);
    static readonly Rgba32 outColorWhite = new(250, 250, 245);
    static readonly Hsv outColorWhiteHSL = ColorSpaceConverter.ToHsv(outColorWhite);

    private static string outputPath;

    static void Main(string[] args)
    {
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
                {
                    nToProcess = res - 1;
                }

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

        HashSet<Thread> threads = [];


        int i = 0;
        IEnumerable<string> files = Directory.EnumerateFiles(inputPath, "*.png");

        foreach (var file in files.OrderByDescending(File.GetLastWriteTime))
        {
            if (Path.GetFileNameWithoutExtension(file) == "filter")
                continue;

            Thread newThread = new Thread(ProcessFile);
            newThread.Start(new object[] { file, filter });

            threads.Add(newThread);

            i++;

            if (i > toProcess)
                break;
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"{i} images rendered in {stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds} elapsed");
    }

    private static void ProcessFile(object? obj)
    {
        object[] args = (object[])obj;

        string path = (string)args[0];
        Image<Rgba32> filter = (Image<Rgba32>)args[1];

        using Image<Rgba32> sourceImage = Image.Load<Rgba32>(path);

        double aspect = sourceImage.Height / sourceImage.Width;

        if( sourceImage.Height > filter.Height )
        {
            sourceImage.Mutate(x => {
                
                x.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(filter.Width, filter.Height),
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.RobidouxSharp,
                    //TargetRectangle = new Rectangle(new Point(filter.Width / 2, filter.Height / 2), new Size(filter.Width, filter.Height)),
                    CenterCoordinates = new PointF(0.5f, 0.5f)

                } );
                x.Pad(filter.Width, filter.Height, Color.Transparent);
            });
        }
        else
        if( sourceImage.Width > filter.Width )
        {
            sourceImage.Mutate(x => {

                x.Resize(new ResizeOptions()
                {
                    Size = new Size(filter.Width, filter.Height),
                    Mode = ResizeMode.Max,
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.Lanczos3
                } );
                x.Pad(filter.Width, filter.Height, Color.Transparent);
            });

        }

        using Image<Rgba32> destG = new Image<Rgba32>(Configuration.Default, sourceImage.Width, sourceImage.Height);
        using Image<Rgba32> destB = new Image<Rgba32>(Configuration.Default, sourceImage.Width, sourceImage.Height);


        int height = sourceImage.Height;

        string originName = Path.GetFileNameWithoutExtension(path);

        Console.Out.WriteLine("Processing " + outputPath + originName + "...");


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
                    Hsv badColor = ColorSpaceConverter.ToHsv(Color.Transparent.ToPixel<Rgba32>());

                    Rgba32 pixel = sourceRow[x];

                    float alpha = pixel.A / 255f;

                    if (alpha > 0.8)
                    {
                        Hsv hsvPixel = ColorSpaceConverter.ToHsv(pixel);

                        if (hsvPixel.V < 0.85)
                        {
                            var pow = MathF.Pow(hsvFilter.V, 1.75f);
                            var pow2 = MathF.Pow(hsvFilter.V, 1.25f);

                            var invPow = MathF.Pow(1 - hsvFilter.V, 1.25f);

                            goodColor = new Hsv(
                                Mix(outColorGoodHSL.H, outColorWhiteHSL.H, invPow) * 0.2f + outColorGoodHSL.H * 0.8f,
                                Mix(outColorGoodHSL.S, outColorWhiteHSL.S, invPow) * 0.80f + pow * 0.2f,
                                Mix(outColorGoodHSL.V, outColorWhiteHSL.V, invPow) * hsvFilter.V
                            );

                            badColor = new Hsv(
                                Mix(outColorBadHSL.H, outColorWhiteHSL.H, invPow) * 0.2f + outColorBadHSL.H * 0.8f,
                                Mix(outColorBadHSL.S, outColorWhiteHSL.S, invPow) * 0.85f + pow * 0.2f,
                                Mix(outColorBadHSL.V, outColorWhiteHSL.V, invPow) * pow2 * 0.85f
                            );

                        }
                        else
                        {

                            goodColor = GetWhiteColor(hsvFilter);
                            badColor = GetWhiteColor(hsvFilter);
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
                            badColor = ColorSpaceConverter.ToHsv(Color.Black.ToPixel<Rgba32>());
                            alpha = MathF.Pow(oD, 1.25f) * 0.50f;
                        }


                        amount = 5;

                        float nD = ApplyOutline(sourceAccessor, i, x, amount, sourceImage.Height, sourceImage.Width, true);

                        if (nD > 0)
                        {
                            goodColor = GetWhiteColor(hsvFilter);
                            badColor = GetWhiteColor(hsvFilter);

                            alpha = MathF.Min(MathF.Pow(1 - nD, 0.2f) + 0.25f, 1f);
                        }
                    }


                    Rgba32 gCol = new Rgba32(new Vector4(ColorSpaceConverter.ToRgb(goodColor).ToVector3(), alpha));
                    Rgba32 bCol = new Rgba32(new Vector4(ColorSpaceConverter.ToRgb(badColor).ToVector3(), alpha));


                    targetGoodRow[x] = gCol;
                    targetBadRow[x] = bCol;
                }
            }
        });

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


        destG.SaveAsPngAsync(outputPath + originName + "-Good.png", pngEncoder);
        destB.SaveAsPngAsync(outputPath + originName + "-Evil.png", pngEncoder);


        Console.Out.WriteLine("Done " + originName + "!");

    }

    static float Mix(float to, float source, float amount)
    {
        return source * amount + to * (1 - amount);
    }

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

                if (d > amount * amount)
                    continue;

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

        if (nearest == float.MaxValue)
            nearest = 0;

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

               // if (sourceAccessor.GetRowSpan(y)[x + 1].A < 40 || sourceAccessor.GetRowSpan(y)[x - 1].A < 40) val -= 2;

                if (val < b)
                {
                    b = val;
                }
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