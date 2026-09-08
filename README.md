# BOTC Icon Filter

Applies a filter to `.png` files similar to the one used in the original *Blood on the Clocktower* character icons by The Pandemonium Institute.  
For each processed file, two versions are generated: one marked **Good** (blue) and one marked **Evil** (red), with the corresponding label appended to the filename.

There is a `filter.png` file in the repository. This is an example filter ( the one I personaly use ) but can be switched by placing any other file in its place and renaming it to `filter.png`.

## User Manual

### Terminal
> Usage: `<INPUT DIRECTORY> <OUTPUT DIRECTORY> [numberToProcess]`


- `<INPUT DIRECTORY>` and `<OUTPUT DIRECTORY>` can be absolute or relative folder paths.
- `[numberToProcess]` is an optional integer. If omitted, all `.png` files in the input directory will be processed.
- The input directory **must** contain a file named `filter.png`. This file is used as the noise mask and is not processed itself.

### Standalone

The program will ask for each value one at a time. If any input is invalid, it exits without modifying any files.
- `INPUT FOLDER` -> The directory with the images to process. Must contain a image called `filter.png`
- `OUTPUT FOLDER` -> The directory where the program outputs the processed images. 
- `Images to process` -> The program processes the given number of images, starting with the one with more recent changes. If no one is given, all images are processed.
---

## Warning

Force-closing the program while it's running may result in data loss.

---


This project is an unofficial, fan-made tool and is not affiliated with, endorsed by, or sponsored by The Pandemonium Institute or Steven Medway. It is intended for community use only.