# Form to BMP Conversion Tools

**Date**: 2025-11-22

## Overview

Created utilities to convert Smalltalk-80 Form files to standard Windows BMP format, enabling easy viewing of extracted cursor/arrow bitmaps with any image viewer.

## Tools Created

### 1. Python Script (Cross-platform)

**form_to_bmp.py** - Converts Form files to Windows BMP format

**Features:**
- Reads Smalltalk-80 Form files (big-endian, column-major format)
- Converts to standard Windows BMP (monochrome, 1-bit)
- Single file or batch directory conversion
- Handles proper format conversion:
  - Column-major → Row-major layout
  - Bottom-up bitmap orientation (BMP standard)
  - 4-byte row padding alignment
  - Color inversion (Form: 1=black, 0=white; BMP: 1=white, 0=black)

**Usage:**
```bash
# Single file
python3 form_to_bmp.py arrow1.form arrow1.bmp

# Batch conversion
python3 form_to_bmp.py --dir ./arrow_output ./bmp_output
```

### 2. PowerShell Script (Windows/PowerShell Core)

**Convert-FormToBmp.ps1** - PowerShell version with identical functionality

**Usage:**
```powershell
# Single file
pwsh ./Convert-FormToBmp.ps1 -FormFile arrow1.form -BmpFile arrow1.bmp

# Batch conversion
pwsh ./Convert-FormToBmp.ps1 -InputDirectory ./arrow_output -OutputDirectory ./bmp_output
```

## BMP Format Specification

The tools generate standard **Windows 3.x BMP files** with the following structure:

### File Structure

```
Offset  Size  Field                   Value
------  ----  -----                   -----
0       2     Signature               'BM' (0x42 0x4D)
2       4     File size               Total file size in bytes
6       2     Reserved 1              0
8       2     Reserved 2              0
10      4     Pixel data offset       62 (0x3E) - after headers and color table

-- DIB Header (BITMAPINFOHEADER, 40 bytes) --
14      4     DIB header size         40 (0x28)
18      4     Width                   Image width in pixels (signed)
22      4     Height                  Image height in pixels (signed, positive = bottom-up)
26      2     Planes                  1
28      2     Bits per pixel          1 (monochrome)
30      4     Compression             0 (BI_RGB, uncompressed)
34      4     Image size              Size of pixel data in bytes
38      4     X pixels per meter      2835 (72 DPI)
42      4     Y pixels per meter      2835 (72 DPI)
46      4     Colors in palette       2
50      4     Important colors        2

-- Color Table (8 bytes) --
54      4     Color 0 (RGBQUAD)       0x00000000 (Black: B=0, G=0, R=0, Reserved=0)
58      4     Color 1 (RGBQUAD)       0x00FFFFFF (White: B=255, G=255, R=255, Reserved=0)

-- Pixel Data (variable size) --
62      N     Bitmap pixels           1 bit per pixel, row-major, bottom-to-top,
                                      padded to 4-byte boundaries
```

### Format Details

**Byte Order:** Little-endian (Intel format)

**Color Mapping:**
- Palette index 0 = Black (RGB 0,0,0)
- Palette index 1 = White (RGB 255,255,255)

**Pixel Layout:**
- **Row order:** Bottom-to-top (first row in file = bottom row of image)
- **Bit order:** MSB first within each byte
- **Padding:** Each row padded to 4-byte boundary

**Example for 32-pixel wide image:**
```
Bytes per row (unpadded) = (32 + 7) / 8 = 4 bytes
Bytes per row (padded)   = ((4 + 3) / 4) * 4 = 4 bytes (already aligned)
```

## Test Results

Successfully converted 6 Form files to BMP format:

| File         | Form Size | BMP Size | BMP Dimensions | Status |
|--------------|-----------|----------|----------------|--------|
| arrow1.bmp   | 126 bytes | 178 bytes| 32×29         | ✓ Valid|
| arrow2.bmp   | 514 bytes | 566 bytes| 63×63         | ✓ Valid|
| arrow3.bmp   | 160 bytes | 262 bytes| 48×25         | ✓ Valid|
| arrow4.bmp   | 490 bytes | 542 bytes| 60×60         | ✓ Valid|
| arrow5.bmp   | 226 bytes | 278 bytes| 56×27         | ✓ Valid|
| arrow6.bmp   | 458 bytes | 510 bytes| 56×56         | ✓ Valid|

**Validation:** All BMP files correctly identified by `file` command:
```
arrow1.bmp: PC bitmap, Windows 3.x format, 32 x 29 x 1
```

## Format Conversion Details

### Column-Major to Row-Major

**Form format** stores pixels in column-major order:
```
For each word-column (0 to word_width-1):
    For each row (0 to height-1):
        16-bit word containing 16 horizontal pixels
```

**BMP format** requires row-major order:
```
For each row (height-1 down to 0):  # Bottom-to-top!
    For each byte in row:
        8 pixels packed, MSB first
    Padding bytes to align to 4-byte boundary
```

### Color Inversion

Form and BMP use opposite conventions for monochrome pixels:
- **Form:** 1 = black pixel, 0 = white pixel
- **BMP:** 1 = white pixel (palette index 1), 0 = black pixel (palette index 0)

The conversion tools automatically invert pixels during conversion.

### Row Padding

BMP requires each row to be aligned to 4-byte boundaries:

```python
bytes_per_row_unpacked = (width + 7) // 8  # Actual bytes needed
bytes_per_row_padded = ((bytes_per_row_unpacked + 3) // 4) * 4  # With padding
```

**Examples:**
- Width 32: 4 bytes → 4 bytes (already aligned)
- Width 60: 8 bytes → 8 bytes (already aligned)
- Width 63: 8 bytes → 8 bytes (already aligned)
- Width 48: 6 bytes → 8 bytes (2 padding bytes added)

## Integration Workflow

Complete workflow from Alto bitmap file to viewable BMP:

```bash
# Step 1: Convert octal dump to binary
python3 convert_octal_dump.py arrows_dump.txt ARROWS10.AL

# Step 2: Extract individual cursor bitmaps to Form files
python3 extract_arrow_bitmaps.py ARROWS10.AL ./arrow_output

# Step 3: Convert Form files to BMP for viewing
python3 form_to_bmp.py --dir ./arrow_output ./bmp_output

# Step 4: View BMP files with any image viewer
# Windows: start arrow1.bmp
# Linux:   xdg-open arrow1.bmp
# macOS:   open arrow1.bmp
```

## Technical Implementation

### Key Algorithms

**1. Read Form File:**
```python
- Parse big-endian header (fileCode, width, height, offsets)
- Read column-major bitmap data (16-bit words, big-endian)
- Store as array of words
```

**2. Convert to Pixel Array:**
```python
for row in range(height):
    for col in range(word_width):
        word = bitmap_data[col * height + row]  # Column-major indexing
        for bit in range(15, -1, -1):  # MSB first
            pixel = (word >> bit) & 1
            pixels[row].append(pixel)
```

**3. Write BMP:**
```python
for row in reversed(pixels):  # Bottom-to-top
    byte_row = pack_bits_to_bytes(row, invert=True)
    pad_to_4_bytes(byte_row)
    write_to_bmp(byte_row)
```

### Error Handling

Both tools validate:
- Form file signature (fileCode == 1)
- Sufficient data for bitmap dimensions
- File existence and permissions
- Output directory creation

## Verification

### BMP Header Verification

Sample BMP header (arrow1.bmp):
```
00000000: 42 4d b2 00 00 00 00 00  00 00 3e 00 00 00 28 00  BM........>...(.
00000010: 00 00 20 00 00 00 1d 00  00 00 01 00 01 00 00 00  .. .............
00000020: 00 00 74 00 00 00 13 0b  00 00 13 0b 00 00 02 00  ..t.............
00000030: 00 00 02 00 00 00 00 00  00 00 ff ff ff 00        ..............
```

**Analysis:**
- ✓ Signature: 0x42 0x4D ('BM')
- ✓ File size: 0xB2 (178 bytes)
- ✓ Pixel offset: 0x3E (62 bytes)
- ✓ DIB size: 0x28 (40 bytes)
- ✓ Width: 0x20 (32 pixels)
- ✓ Height: 0x1D (29 pixels)
- ✓ Bits per pixel: 0x01 (monochrome)
- ✓ Color table: Black (00 00 00 00), White (FF FF FF 00)

## Future Enhancements

Possible improvements:
1. **Anti-aliasing:** Option to convert to 8-bit grayscale for smoother appearance
2. **Scaling:** Option to scale bitmaps 2x, 4x, etc. for better visibility
3. **PNG output:** Alternative to BMP for smaller file sizes
4. **Batch preview:** Generate HTML gallery of all converted bitmaps
5. **Color mapping:** Support for color BMPs with custom palette

## Status

✅ **Complete and Tested**

Both Python and PowerShell tools are fully functional and produce valid, standards-compliant Windows BMP files viewable in any image viewer.

## File Locations

- **Tools**: `/home/user/Smalltalk-80/tools/`
- **Python**: `form_to_bmp.py`
- **PowerShell**: `Convert-FormToBmp.ps1`
- **Test output**: `/home/user/Smalltalk-80/tools/bmp_output/`
- **Documentation**: This file

## References

- **BMP Format:** Microsoft Windows Bitmap File Format Specification
- **Form Format:** `/home/user/Smalltalk-80/docs/Form-File-Format.md`
- **BITMAPINFOHEADER:** Windows 3.x DIB header (40 bytes)
- **Color Table:** RGBQUAD format (4 bytes per color)
