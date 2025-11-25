# FormMenuView File Requirements - Complete Analysis

**Date**: 2025-11-25
**Purpose**: Reverse-engineering specification for creating the 23 Form files required by FormMenuView initialize

## Executive Summary

The Smalltalk-80 FormMenuView class requires 23 external bitmap files (.form files) to initialize the Form Editor menu system. These files contain small icon bitmaps (typically 32x32 or 32x48 pixels) representing tools, modes, colors, and operations available in the Form Editor. This document provides complete specifications for recreating these files.

## File Format Specification

### Binary Structure

All .form files use the following binary format (16-bit words, big-endian byte order):

```
Offset  Size    Field       Description
------  ----    -----       -----------
0       1 word  fileCode    Must be 1 (identifies file as Form)
2       1 word  width       Width in pixels (unsigned)
4       1 word  height      Height in pixels (unsigned)
6       1 word  offsetX     X offset (signed two's complement)
8       1 word  offsetY     Y offset (signed two's complement)
10      N words bits        Bitmap data (column-major order)
```

### Key Format Details

1. **Word Size**: 16 bits (2 bytes)
2. **Byte Order**: Big-endian (MSB first) - Xerox Alto convention
3. **File Code**: Must be exactly 1
4. **Offsets**: Signed integers using two's complement
   - Values > 32767 represent negative numbers (subtract 65536)
   - Typical offset: 0@0 or -16@-16 (center hotspot)
5. **Bitmap Size**: `(width + 15) // 16 * height` words
6. **Bit Packing**: MSB first (leftmost pixel = bit 7 of byte 0)
7. **Layout**: Column-major (iterate word-columns, then rows within each column)

### Reading Algorithm

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

## Required Files - Complete Specification

### Source Reference

From `FormMenuView class>>initialize` (Smalltalk-80.st:21347-21380):

```smalltalk
names _
    #('select.form.' 'singlecopy.form.' 'repeatcopy.form.' 'line.form.' 'curve.form.'
    'block.form' 'over.form.' 'under.form.' 'reverse.form.' 'erase.form.' 'in.form.'
    'magnify.form.' 'white.form' 'lightgray.form' 'gray.form' 'darkgray.form' 'black.form'
    'xgrid.form.' 'ygrid.form.' 'togglegrids.form.' 'out.form.' ).
```

Plus two border forms:
```smalltalk
SpecialBorderForm  _ Form readFrom: 'specialborderform.form'.
BorderForm _ Form readFrom: 'borderform.form'
```

### Menu Layout

The FormMenuView arranges buttons in a 2-row grid:

**Row 1** (Y offset: 0): 11 buttons at X offsets 0, 64, 96, 128, 160, 192, 256, 288, 320, 352, 420
**Row 2** (Y offset: 48): 10 buttons at X offsets 0, 64, 96, 128, 160, 192, 256, 304, 352, 420

Button dimensions: Approximately 32x48 pixels (based on spacing)

## File Specifications by Category

### Category 1: Drawing Tools (Row 1, Buttons 1-6)

#### 1. select.form. (Key: 'a')
- **Position**: Row 1, offset 0@0
- **Purpose**: Select/define new source form for copying
- **Method**: `FormEditor>>newSourceForm` (line 20777)
- **Behavior**: Allows user to define a rectangular area from display as the source form for copy operations
- **Icon Design**: Selection cursor or crosshair with rectangle outline
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 2. singlecopy.form. (Key: 's')
- **Position**: Row 1, offset 64@0
- **Purpose**: Single copy of source form
- **Method**: `FormEditor>>singleCopy` (line 20805)
- **Behavior**: Copy source form once at cursor position
- **Icon Design**: Single stamp/brush icon or form outline with "1x"
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 3. repeatcopy.form. (Key: 'd')
- **Position**: Row 1, offset 96@0
- **Purpose**: Continuous copying while red button pressed
- **Method**: `FormEditor>>repeatCopy` (line 20789)
- **Behavior**: Repeatedly copies source form while mouse button held
- **Icon Design**: Multiple overlapping form outlines or "nx" repeat symbol
- **Suggested Size**: 32x48 pixels
- **Initial State**: ON (default tool)

#### 4. line.form. (Key: 'f')
- **Position**: Row 1, offset 128@0
- **Purpose**: Rubber-band line drawing
- **Method**: `FormEditor>>line` (line 20744)
- **Behavior**: Draw straight line from first point to rubber-banded endpoint
- **Icon Design**: Diagonal line with endpoints marked
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 5. curve.form. (Key: 'g')
- **Position**: Row 1, offset 160@0
- **Purpose**: Conic section curve drawing
- **Method**: `FormEditor>>curve` (line 20680)
- **Behavior**: Draw conic section specified by three points
- **Icon Design**: Smooth curved line through control points
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 6. block.form (Key: 'h')
- **Position**: Row 1, offset 192@0
- **Purpose**: Fill rectangular area
- **Method**: `FormEditor>>block` (line 20608)
- **Behavior**: Fill user-selected rectangle with current color and mode
- **Icon Design**: Filled rectangle
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

### Category 2: Display Modes (Row 1, Buttons 7-10)

#### 7. over.form. (Key: 'j')
- **Position**: Row 1, offset 256@0
- **Purpose**: Set display mode to "over" (rule 3)
- **Method**: `FormEditor>>overMode` (line 20783)
- **Behavior**: Sets `mode _ Form over` (BitBlt rule 3: source OR destination)
- **Icon Design**: Two overlapping shapes with top one opaque
- **Suggested Size**: 32x48 pixels
- **Initial State**: ON (default mode)

#### 8. under.form. (Key: 'k')
- **Position**: Row 1, offset 288@0
- **Purpose**: Set display mode to "under" (rule 7)
- **Method**: `FormEditor>>underMode` (line 20837)
- **Behavior**: Sets `mode _ Form under` (BitBlt rule 7: paint behind existing)
- **Icon Design**: Two overlapping shapes with bottom one visible through top
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 9. reverse.form. (Key: 'l')
- **Position**: Row 1, offset 320@0
- **Purpose**: Set display mode to "reverse" (rule 6)
- **Method**: `FormEditor>>reverseMode` (line 20799)
- **Behavior**: Sets `mode _ Form reverse` (BitBlt rule 6: XOR/invert)
- **Icon Design**: Two overlapping shapes showing XOR effect (inverted intersection)
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 10. erase.form. (Key: ';')
- **Position**: Row 1, offset 352@0
- **Purpose**: Set display mode to "erase" (rule 4)
- **Method**: `FormEditor>>eraseMode` (line 20702)
- **Behavior**: Sets `mode _ 4` (BitBlt rule 4: erase)
- **Icon Design**: Eraser icon or shape with "subtract" symbol
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

### Category 3: File Operations (Row 1, Button 11 & Row 2, Button 21)

#### 11. in.form. (Key: ''')
- **Position**: Row 1, offset 420@0
- **Purpose**: Read form from file (file in)
- **Method**: `FormEditor>>fileInForm` (line 20708)
- **Behavior**: Prompts for filename and loads Form from .form file
- **Icon Design**: Folder with arrow pointing IN or document with left arrow
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 21. out.form. (Key: '/')
- **Position**: Row 2, offset 420@48
- **Purpose**: Write form to file (file out)
- **Method**: `FormEditor>>fileOutForm` (line 20725)
- **Behavior**: Prompts for filename and saves source Form to .form file
- **Icon Design**: Folder with arrow pointing OUT or document with right arrow
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

### Category 4: Editing Tools (Row 2, Button 12)

#### 12. magnify.form. (Key: 'z')
- **Position**: Row 2, offset 0@48
- **Purpose**: Bit-level editing (magnified view)
- **Method**: `FormEditor>>magnify` (line 20757)
- **Behavior**: Opens BitEditor with 8x magnification for pixel-level editing
- **Icon Design**: Magnifying glass or zoomed grid of pixels
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

### Category 5: Color/Pattern Selectors (Row 2, Buttons 13-17)

These buttons set the halftone mask used for drawing operations.

#### 13. white.form (Key: 'x')
- **Position**: Row 2, offset 64@48
- **Purpose**: Set color to white (clear)
- **Method**: `FormEditor>>colorWhite` (line 20675)
- **Behavior**: Sets `color _ Form white` (all-zero halftone)
- **Icon Design**: Solid white square/circle
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 14. lightgray.form (Key: 'c')
- **Position**: Row 2, offset 96@48
- **Purpose**: Set color to light gray pattern
- **Method**: `FormEditor>>colorLightGray` (line 20670)
- **Behavior**: Sets `color _ Form lightGray` (25% black halftone)
- **Icon Design**: Light gray halftone pattern square
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 15. gray.form (Key: 'v')
- **Position**: Row 2, offset 128@48
- **Purpose**: Set color to medium gray pattern
- **Method**: `FormEditor>>colorGray` (line 20665)
- **Behavior**: Sets `color _ Form gray` (50% checkerboard halftone)
- **Icon Design**: Medium gray checkerboard pattern square
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 16. darkgray.form (Key: 'b')
- **Position**: Row 2, offset 160@48
- **Purpose**: Set color to dark gray pattern
- **Method**: `FormEditor>>colorDarkGray` (line 20660)
- **Behavior**: Sets `color _ Form darkGray` (75% black halftone)
- **Icon Design**: Dark gray halftone pattern square
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 17. black.form (Key: 'n')
- **Position**: Row 2, offset 192@48
- **Purpose**: Set color to black (solid)
- **Method**: `FormEditor>>colorBlack` (line 20655)
- **Behavior**: Sets `color _ Form black` (all-one halftone)
- **Icon Design**: Solid black square/circle
- **Suggested Size**: 32x48 pixels
- **Initial State**: ON (default color)

### Category 6: Grid Controls (Row 2, Buttons 18-20)

#### 18. xgrid.form. (Key: 'm')
- **Position**: Row 2, offset 256@48
- **Purpose**: Toggle horizontal (X) grid snapping
- **Method**: `FormEditor>>togglexGridding` (line 20813)
- **Behavior**: Toggles X-axis grid constraint on/off
- **Icon Design**: Vertical grid lines or "X" with grid symbol
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 19. ygrid.form. (Key: ',')
- **Position**: Row 2, offset 304@48
- **Purpose**: Toggle vertical (Y) grid snapping
- **Method**: `FormEditor>>toggleyGridding` (line 20825)
- **Behavior**: Toggles Y-axis grid constraint on/off
- **Icon Design**: Horizontal grid lines or "Y" with grid symbol
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

#### 20. togglegrids.form. (Key: '.')
- **Position**: Row 2, offset 352@48
- **Purpose**: Change grid spacing settings
- **Method**: `FormEditor>>changeGridding` (line 20619)
- **Behavior**: Prompts user to enter new horizontal/vertical grid values
- **Icon Design**: Grid with settings icon or adjustable grid spacing indicator
- **Suggested Size**: 32x48 pixels
- **Initial State**: OFF

### Category 7: Border Forms (Not in menu, used for window decoration)

#### 22. borderform.form
- **Purpose**: Standard window border decoration
- **Usage**: `BorderForm _ Form readFrom: 'borderform.form'`
- **Behavior**: Used for standard view borders in the form editor
- **Icon Design**: Thin border pattern (likely 2-4 pixels wide, tileable)
- **Suggested Size**: Variable (likely small repeating pattern)

#### 23. specialborderform.form
- **Purpose**: Special window border decoration (selected/active state)
- **Usage**: `SpecialBorderForm _ Form readFrom: 'specialborderform.form'`
- **Behavior**: Used for highlighted/selected view borders
- **Icon Design**: Emphasized border pattern (likely complemented/inverted)
- **Suggested Size**: Variable (likely same as borderform.form)

## BitBlt Combination Rules Reference

The mode selectors use these BitBlt combination rules:

| Rule | Name | Operation | Form Method |
|------|------|-----------|-------------|
| 0 | clear | 0 | (none) |
| 1 | and | src AND dst | Form and |
| 2 | andReverse | src AND (NOT dst) | (none) |
| 3 | copy | src | Form over |
| 4 | andInverted | (NOT src) AND dst | Form erase |
| 5 | noop | dst | (none) |
| 6 | xor | src XOR dst | Form reverse |
| 7 | or | src OR dst | Form under |
| 8-15 | ... | (other boolean ops) | (none) |

## Halftone Pattern Reference

From `Form class>>initializeMasks` (line 20291-20308):

```smalltalk
whiteMask:      All zeros (16x16)
blackMask:      All ones (16x16 of 65535)
grayMask:       Checkerboard (alternating 21845/43690)
darkGrayMask:   75% black (alternating 30583/56797)
lightGrayMask:  25% black (darkGray reversed)
```

## Button State Tracking

Initial button states from `FormMenuView class>>initialize`:

```smalltalk
states _
    #(false false true false false false true false false false false false false
    false false false true false false false false ).
```

Mapping:
- Button 3 (repeatcopy): ON - default tool
- Button 7 (over): ON - default mode
- Button 17 (black): ON - default color
- All others: OFF

## Creating Form Files - Step by Step

### 1. Design the Icon Bitmap

For a 32x48 pixel icon:
- Sketch the icon on graph paper or pixel art editor
- Use clear, simple shapes (remember: 1-bit monochrome only)
- Consider visibility at small size
- Test against gray backgrounds

### 2. Convert to Binary Format

```
Example: 32x48 icon
- Word-columns: (32 + 15) // 16 = 2
- Total words: 2 * 48 = 96 words
- File size: 10 bytes (header) + 192 bytes (bitmap) = 202 bytes
```

### 3. Write the File

```python
import struct

def write_form_file(filename, width, height, offset_x, offset_y, bitmap_data):
    """
    Write a Smalltalk-80 .form file.

    bitmap_data: list of 16-bit words in column-major order
                 length must be ((width + 15) // 16) * height
    """
    with open(filename, 'wb') as f:
        # Header (big-endian 16-bit words)
        f.write(struct.pack('>H', 1))           # fileCode
        f.write(struct.pack('>H', width))       # width
        f.write(struct.pack('>H', height))      # height

        # Handle negative offsets (two's complement)
        if offset_x < 0:
            offset_x = 65536 + offset_x
        if offset_y < 0:
            offset_y = 65536 + offset_y

        f.write(struct.pack('>H', offset_x))    # offsetX
        f.write(struct.pack('>H', offset_y))    # offsetY

        # Bitmap data (column-major)
        for word in bitmap_data:
            f.write(struct.pack('>H', word))
```

### 4. Bitmap Data Layout

For column-major layout:
```python
# For width=32, height=48:
word_columns = (32 + 15) // 16  # = 2
bitmap = []

for col in range(word_columns):
    for row in range(height):
        # Each word contains 16 horizontal pixels
        # Bit 15 (MSB) = leftmost pixel of this 16-pixel chunk
        word_value = compute_word_for_column_and_row(col, row)
        bitmap.append(word_value)
```

## Testing Form Files

### Verification Checklist

1. **File Size**: `10 + ((width + 15) // 16 * height * 2)` bytes
2. **File Code**: First 2 bytes = 0x00 0x01 (big-endian 1)
3. **Dimensions**: Reasonable values (< 1024 typically)
4. **Bitmap Size**: Matches expected word count
5. **Visual Test**: Load in Smalltalk-80 and display

### Smalltalk Test Code

```smalltalk
"Test loading a form file"
| testForm |
testForm := Form readFrom: 'select.form.'.
testForm isNil
    ifTrue: [Transcript show: 'FAILED to load form file']
    ifFalse: [
        Transcript show: 'Form loaded: ', testForm extent printString.
        testForm displayAt: 100@100
    ].
```

## Implementation Primitives Required

FormMenuView initialize requires these file I/O primitives:

- **Primitive 98**: File open
- **Primitive 92**: File read (nextWord for binary reads)
- **Primitive 99**: File close
- **Primitive 146**: File operations (readOnly, binary mode)

**Current Status**: These primitives are not yet implemented in our interpreter, which is why FormMenuView initialize is currently skipped.

## Blue Book References

### Relevant Chapters

- **Chapter 18**: Graphics Kernel (BitBlt, Form class)
- **Chapter 17**: Views and Controllers (MVC architecture)
- **Chapter 20**: Editors (Form Editor implementation)

### Related Figures

- **Figure 18.8**: Form object structure (width, height, bitmap)
- **Figure 17.1**: System browser showing graphics classes

## Historical Context

These .form files were part of the original Xerox Smalltalk-80 Release 2 distribution on magnetic tape. They represented the standard tool icons for the Form Editor, which was the primary bitmap/graphics editing tool in Smalltalk-80.

The form editor predates modern pixel art tools but provided sophisticated bitmap editing capabilities including:
- Multiple drawing tools (line, curve, block, copy)
- BitBlt combination modes (over, under, reverse, erase)
- Halftone patterns (simulating grayscale on 1-bit displays)
- Grid snapping for precise alignment
- Bit-level editing with magnification
- File import/export for reusable graphics

## Related Resources

### Source Code Locations

- **FormMenuView class**: Smalltalk-80.st line 21234-21382
- **FormEditor class**: Smalltalk-80.st line 20547-21133
- **Form class**: Smalltalk-80.st line 19242-20479
- **Form readFrom:**: Smalltalk-80.st line 20397
- **Form readFormFile:**: Smalltalk-80.st line 20373

### Documentation

- `/home/user/Smalltalk-80/docs/Form-File-Format.md` - File format specification
- `/home/user/Smalltalk-80/docs/Form-File-Resources.md` - Online resources for obtaining files
- `/home/user/Smalltalk-80/docs/Graphics-Architecture.md` - Graphics system architecture

## Next Steps

To enable FormMenuView initialize:

1. **Option A: Obtain Original Files**
   - Download from Wolczko's ST80 archive
   - Extract from Computer History Museum Alto archive
   - Request from Smalltalk-80 collectors/historians

2. **Option B: Recreate Files**
   - Design icons based on tool descriptions above
   - Use Python/C# script to generate .form files
   - Test in Smalltalk-80 interpreter

3. **Option C: Screenshot Extraction**
   - Run Smalltalk Zoo emulator
   - Capture form editor screenshots
   - Extract and convert icon bitmaps

4. **Implement File I/O Primitives**
   - Primitive 98, 92, 99, 146
   - Enable FormMenuView initialize to run
   - Unlock form editor functionality

## Appendix: Keyboard Reference

Complete keyboard mapping from `FormEditor class>>setKeyboardMap`:

| Key | Tool/Mode | Description |
|-----|-----------|-------------|
| a | Select | Define new source form |
| s | SingleCopy | Single stamp of form |
| d | RepeatCopy | Continuous stamping |
| f | Line | Draw straight line |
| g | Curve | Draw conic curve |
| h | Block | Fill rectangle |
| j | Over | Over mode (opaque) |
| k | Under | Under mode (behind) |
| l | Reverse | Reverse mode (XOR) |
| ; | Erase | Erase mode |
| ' | In | Load form from file |
| z | BitEdit | Magnified editing |
| x | White | White color |
| c | LightGray | Light gray pattern |
| v | Gray | Medium gray pattern |
| b | DarkGray | Dark gray pattern |
| n | Black | Black color |
| m | TogglexGrid | Toggle X grid |
| , | ToggleyGrid | Toggle Y grid |
| . | ChangeGrids | Change grid spacing |
| / | Out | Save form to file |

---

**Document Status**: Complete specification
**Last Updated**: 2025-11-25
**Purpose**: Enable recreation of FormMenuView required .form files for Smalltalk-80 interpreter
