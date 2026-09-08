# BOTC Icon Filter

Applies a filter to `.png` files similar to the one used in the original *Blood on the Clocktower* character icons by The Pandemonium Institute.  
For each processed file, two versions are generated: one marked **Good** (blue) and one marked **Evil** (red), with the corresponding label appended to the filename.

---

## User Manual

### Terminal
> Usage: <INPUT DIRECTORY> <OUTPUT DIRECTORY> [numberToProcess]


- `<INPUT DIRECTORY>` and `<OUTPUT DIRECTORY>` can be absolute or relative folder paths.
- `[numberToProcess]` is an optional integer. If omitted, all `.png` files in the input directory will be processed.
- The input directory **must** contain a file named `filter.png`. This file is used as the noise mask and is not processed itself.

### Standalone

The program will ask for each value one at a time. If any input is invalid, it exits without modifying any files.

---

## Warning

Force-closing the program while it's running may result in data loss.

---

This project is an unofficial, fan-made tool and is not affiliated with, endorsed by, or sponsored by The Pandemonium Institute or Steven Medway. It is intended for community use only.
– Made by Patosalvaje