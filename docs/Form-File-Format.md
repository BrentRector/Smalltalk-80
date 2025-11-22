# Form File Format Documentation

## Overview

The Smalltalk-80 system stores Form (bitmap) objects in external files with a specific binary format. This document describes the format and the expected Form files used by the system.

## File Format Specification

Based on `Form class>>readFormFile:` (Smalltalk-80.sources:19432) and `Form>>writeOn:` (Smalltalk-80.sources:19247), Form files have the following binary structure:

### File Structure (All fields are 16-bit words)

```
Offset  Size    Field           Description
------  ----    -----           -----------
0       1 word  fileCode        Must be 1 (identifies file as Form)
2       1 word  width           Width of the bitmap in pixels
4       1 word  height          Height of the bitmap in pixels
6       1 word  offsetX         X offset (signed, two's complement)
8       1 word  offsetY         Y offset (signed, two's complement)
10      N words bits            Bitmap data (column-major order)
```

### Important Details

1. **File Code**: Must be exactly 1 to identify the file as a valid Form file
2. **Dimensions**: Width and height are unsigned 16-bit integers
3. **Offset**:
   - Stored as two's complement signed integers
   - If value > 32767, subtract 65536 to get signed value
   - Used to position the Form relative to a reference point (usually 0@0)
4. **Bitmap Data**:
   - Stored as 16-bit words (WordArray)
   - Total size: `(width + 15) // 16 * height` words
   - Layout is **column-major**: iterate columns first, then rows
   - Each word represents 16 horizontal pixels
   - Bits are packed left-to-right within each word

### Reading Algorithm (from readFormFile:)

```smalltalk
file readOnly; binary.
fileCode ← file nextWord.       "Must be 1"
width ← file nextWord.
height ← file nextWord.
offsetX ← file nextWord.
offsetY ← file nextWord.

"Convert offsets from unsigned to signed"
offsetX > 32767 ifTrue: [offsetX ← offsetX - 65536].
offsetY > 32767 ifTrue: [offsetY ← offsetY - 65536].

"Read bitmap data (column-major)"
theBits ← WordArray new: width + 15 // 16 * height.
filePosition ← 0.
1 to: width + 15 // 16 do: [:j |
    1 to: height do: [:i |
        theBits at: (filePosition ← filePosition + 1) put: file nextWord
    ]
].
```

### Writing Algorithm (from writeOn:)

```smalltalk
file binary.
file nextWordPut: 1.              "fileCode"
file nextWordPut: width.
file nextWordPut: height.
file nextWordPut: offset x.       "Automatically stored as two's complement"
file nextWordPut: offset y.

"Write bitmap data (column-major)"
filePosition ← 0.
1 to: width + 15 // 16 do: [:j |
    1 to: height do: [:i |
        file nextWordPut: (bits at: (filePosition ← filePosition + 1))
    ]
].
```

## FormMenuView Required Files

FormMenuView class>>initialize (Smalltalk-80.sources:20407) requires 21 form files for menu buttons, plus 2 border forms:

### Button Forms (21 files)

The filenames are stored in the `names` array and correspond to menu buttons:

| Index | Filename               | Keyboard | Purpose           | Row | Offset  |
|-------|------------------------|----------|-------------------|-----|---------|
| 1     | select.form.           | 'a'      | Form source       | 1   | 0@0     |
| 2     | singlecopy.form.       | 's'      | Single copy tool  | 1   | 64@0    |
| 3     | repeatcopy.form.       | 'd'      | Repeat copy tool  | 1   | 96@0    |
| 4     | line.form.             | 'f'      | Line tool         | 1   | 128@0   |
| 5     | curve.form.            | 'g'      | Curve tool        | 1   | 160@0   |
| 6     | block.form             | 'h'      | Block tool        | 1   | 192@0   |
| 7     | over.form.             | 'j'      | Over mode         | 1   | 256@0   |
| 8     | under.form.            | 'k'      | Under mode        | 1   | 288@0   |
| 9     | reverse.form.          | 'l'      | Reverse mode      | 1   | 320@0   |
| 10    | erase.form.            | ';'      | Erase mode        | 1   | 352@0   |
| 11    | in.form.               | '''      | Filing in         | 1   | 420@0   |
| 12    | magnify.form.          | 'z'      | Bit editing       | 2   | 0@48    |
| 13    | white.form             | 'x'      | White color       | 2   | 64@48   |
| 14    | lightgray.form         | 'c'      | Light gray color  | 2   | 96@48   |
| 15    | gray.form              | 'v'      | Gray color        | 2   | 128@48  |
| 16    | darkgray.form          | 'b'      | Dark gray color   | 2   | 160@48  |
| 17    | black.form             | 'n'      | Black color       | 2   | 192@48  |
| 18    | xgrid.form.            | 'm'      | Toggle x grid     | 2   | 256@48  |
| 19    | ygrid.form.            | ','      | Toggle y grid     | 2   | 304@48  |
| 20    | togglegrids.form.      | '.'      | Setting grid      | 2   | 352@48  |
| 21    | out.form.              | '/'      | Filing out        | 2   | 420@48  |

### Border Forms (2 files)

```smalltalk
SpecialBorderForm ← Form readFrom: 'specialborderform.form'.
BorderForm ← Form readFrom: 'borderform.form'
```

## Relationship to Xerox Distribution

The user-provided table mentions Form1 through Form23 from the Xerox Bluebook Release 2. These appear to be **different** from the FormMenuView files:

- **Form1-Form23**: Xerox distribution files (cursors, UI elements)
- **FormMenuView files**: Menu button bitmaps with descriptive names

The FormMenuView files are the ones actually referenced in the Smalltalk-80 source code we have.

## File Byte Order

The Smalltalk-80 VM uses **big-endian** byte order for 16-bit words (most significant byte first), as was standard on Xerox Alto hardware.

### Example: 16-bit Word Storage

```
Value: 0x1234 (decimal 4660)
Byte 0: 0x12 (high byte)
Byte 1: 0x34 (low byte)
```

## Creating Form Files

To create a Form file from scratch:

1. **Prepare bitmap data**:
   - Determine width and height in pixels
   - Create bitmap array: `(width + 15) // 16 * height` words
   - Fill with pixel data (column-major order)

2. **Write binary file**:
   - Write fileCode: 1 (16-bit word)
   - Write width (16-bit word)
   - Write height (16-bit word)
   - Write offsetX (16-bit signed word, two's complement if negative)
   - Write offsetY (16-bit signed word, two's complement if negative)
   - Write bitmap data (16-bit words, column-major)

3. **Use big-endian byte order** for all 16-bit words

## Column-Major Layout Example

For a 32×16 pixel Form (2 words wide, 16 pixels tall):

```
File word sequence:
Word 0:  Column 0, Row 0-15 (pixels 0-15 of first word-column)
Word 1:  Column 0, Row 0-15 (pixels 0-15 of first word-column) -- Wait, this is wrong
```

Actually, the correct interpretation:

```
WordArray layout for 32×16 Form (2 word-columns × 16 rows = 32 total words):

Word index layout:
1 to: 2 do: [:columnIndex |        "2 word-columns (width=32 → 32+15//16 = 2)"
    1 to: 16 do: [:rowIndex |      "16 rows (height=16)"
        bits at: position put: nextWord
    ]
]

So the sequence is:
Words 0-15:   Column 0 (pixels 0-15 horizontal), rows 0-15
Words 16-31:  Column 1 (pixels 16-31 horizontal), rows 0-15
```

Each word contains 16 horizontal pixels, packed left-to-right.

## Verification

To verify a Form file:
1. Check first word is 1 (fileCode)
2. Verify file size: `10 + ((width + 15) // 16 * height * 2)` bytes
   - 10 bytes header (5 words × 2 bytes)
   - Bitmap data size in bytes
3. All dimension values should be reasonable (< 1024 typically)

## References

- **Form class>>readFrom:**: Smalltalk-80.sources:19456
- **Form class>>readFormFile:**: Smalltalk-80.sources:19432
- **Form>>writeOn:**: Smalltalk-80.sources:19247
- **FormMenuView class>>initialize**: Smalltalk-80.sources:20407

## Status

⚠️ **Missing Files**: None of the required Form files are currently present in the repository. They need to be either:
1. Obtained from a complete Xerox Bluebook Release 2 distribution
2. Reconstructed manually based on descriptions
3. Created as placeholder bitmaps for testing

The FormMenuView initialize method is currently skipped due to missing file I/O primitives, but when implemented, it will require these files to be present.
