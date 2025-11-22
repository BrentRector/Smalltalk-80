# Arrow Bitmap Extractor Tools

This directory contains utilities to extract cursor/arrow bitmaps from the ARROWS10.AL Alto bitmap collection file and convert them to Smalltalk-80 Form format.

**Available in both Python and PowerShell!**

## Files

### Python Scripts (Recommended - Works on Linux/macOS/Windows)
- **convert_octal_dump.py** - Converts octal dump output to binary file
- **extract_arrow_bitmaps.py** - Extracts bitmaps from ARROWS10.AL to individual Form files

### PowerShell Scripts (Windows/PowerShell Core)
- **Extract-ArrowBitmaps.ps1** - Main extraction tool that converts ARROWS10.AL to individual Form files
- **ConvertOctalDump-ToBinary.ps1** - Helper tool to convert octal dump output to binary file

### Data Files
- **arrows_dump.txt** - Sample octal dump of ARROWS10.AL file
- **README-ArrowExtractor.md** - This file

## Quick Start

### Python (Recommended)

**Option 1: Extract from Binary File**

```bash
cd tools
python3 extract_arrow_bitmaps.py ARROWS10.AL ./output/
```

**Option 2: Extract from Octal Dump**

```bash
cd tools

# Step 1: Convert octal dump to binary
python3 convert_octal_dump.py arrows_dump.txt ARROWS10.AL

# Step 2: Extract bitmaps
python3 extract_arrow_bitmaps.py ARROWS10.AL ./output/
```

### PowerShell

**Option 1: Extract from Binary File**

```powershell
cd tools
pwsh ./Extract-ArrowBitmaps.ps1 -InputFile ARROWS10.AL -OutputDirectory ../forms/arrows/
```

**Option 2: Extract from Octal Dump**

```powershell
cd tools

# Step 1: Convert octal dump to binary
pwsh ./ConvertOctalDump-ToBinary.ps1 -InputFile arrows_dump.txt -OutputFile ARROWS10.AL

# Step 2: Extract bitmaps
pwsh ./Extract-ArrowBitmaps.ps1 -InputFile ARROWS10.AL -OutputDirectory ../forms/arrows/
```

## Output

The extraction tool produces:

1. **Individual Form Files**: Each cursor/arrow is saved as `arrow1.form`, `arrow2.form`, etc.
   - Format: Standard Smalltalk-80 Form file format
   - Compatible with `Form class>>readFrom:` method

2. **Visual Text Representation**: `arrows_visual.txt` showing ASCII art of each bitmap
   - `█` = set pixel (1)
   - `·` = clear pixel (0)

## Form File Format

Each output file follows the Smalltalk-80 Form format:

```
Offset  Field       Description
------  -----       -----------
0       fileCode    Word: 1 (identifies as Form file)
2       width       Word: bitmap width in pixels
4       height      Word: bitmap height in pixels
6       offsetX     Word: X offset (signed, two's complement)
8       offsetY     Word: Y offset (signed, two's complement)
10      bits        Words: bitmap data (column-major order)
```

All words are 16-bit big-endian (MSB first).

## Examples

### Test with Included Sample (Python)

```bash
cd tools

# Convert the included octal dump
python3 convert_octal_dump.py arrows_dump.txt test_arrows.bin

# Extract bitmaps
python3 extract_arrow_bitmaps.py test_arrows.bin ./output/

# View the results
cat ./output/arrows_visual.txt
```

### Test with Included Sample (PowerShell)

```powershell
cd tools

# Convert the included octal dump
pwsh ./ConvertOctalDump-ToBinary.ps1 -InputFile arrows_dump.txt -OutputFile test_arrows.bin

# Extract bitmaps
pwsh ./Extract-ArrowBitmaps.ps1 -InputFile test_arrows.bin -OutputDirectory ./output/

# View the results
cat ./output/arrows_visual.txt
```

### Expected Output

```
File size: 3176 bytes

Signature: 0x00 0x1E9E

Scanning for bitmaps...
Bitmap 1 found at offset 0x028E:
  Size: 32x29
  Offset: 1289,96
  Bitmap data: 58 words (116 bytes)
Bitmap 2 found at offset 0x0312:
  Size: 63x63
  Offset: 31,31
  Bitmap data: 252 words (504 bytes)
...

Found 6 bitmaps total

Generating Form files...
  Created: ./output/arrow1.form (32x29)
  Created: ./output/arrow2.form (63x63)
  ...

Visual representation saved to: ./output/arrows_visual.txt
Done! Extracted 6 bitmaps.
```

## Troubleshooting

**Problem**: Script won't run, says "cannot be loaded because running scripts is disabled"

**Solution**: Enable PowerShell script execution:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Problem**: No bitmaps found

**Solution**: The file may have a different format. Check:
- File is not corrupted (verify file size)
- Octal dump conversion was successful
- Try adjusting the starting offset in Extract-ArrowBitmaps.ps1 (line 85)

**Problem**: Bitmaps look wrong

**Solution**: The column-major format may need adjustment. Verify:
- Bitmap width and height are correct
- Word-width calculation: `(width + 15) // 16`

## See Also

- **Form-File-Format.md** - Complete Form file format specification
- **Form-File-Resources.md** - List of required Form files for Smalltalk-80

## License

These tools are provided as utilities for the Smalltalk-80 project.
