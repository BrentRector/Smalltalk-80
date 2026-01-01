# Migration to 32-bit Words

**Created**: 2026-01-01
**Status**: Planning
**Branch**: `claude/review-graphics-architecture-FssRQ`

---

## Executive Summary

This document tracks the migration from 16-bit words (Blue Book original) to 32-bit words for the internal representation of WordIndexed objects. This change aligns the word size with the interpreter's OOP and SmallInteger design while maintaining backward compatibility with existing file formats.

### Design Principles

| Principle | Description |
|-----------|-------------|
| **Internal = 32-bit** | Runtime WordIndexed storage uses 32-bit words |
| **External = 16-bit** | File formats remain 16-bit for backward compatibility |
| **Conversion at I/O** | Transform between representations at file boundaries |
| **Blue Book Encapsulation** | Higher-level code shouldn't know about word size |

---

## Current State (16-bit Words)

### HeapManager

```csharp
// SmalltalkHeapManager.cs:155
ObjectFormat.WordIndexed => new ushort[indexedSize]

// ISmalltalkHeap.cs:133,139
ushort GetIndexedWord(SmalltalkOOP objectOOP, int index);
void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value);
```

### Float Storage

- 8 bytes = 4 × 16-bit words
- Allocated with `AllocateInstanceOf(FloatClassOOP, 4)`

### Form Bitmap Calculation

```smalltalk
"Smalltalk-80.st - multiple locations"
bits := WordArray new: height * (width + 15 // 16)
```

### BitBlt Stride

```csharp
// BitBltEngine.cs:78-80
int destStride = ((destWidth + 15) / 16) * 2;   // 2 bytes per 16-bit word
int bitIndex = 15 - (x % 16);                    // MSB first in 16-bit word
```

### File I/O (Stays 16-bit)

```smalltalk
"WindowsFileStream - reads/writes 2 bytes as one 'word'"
nextWord
    | high low |
    high := self next.
    low := self next.
    ^(high asInteger bitShift: 8) + low asInteger

nextWordPut: aWord
    self nextPut: (Character value: ((aWord bitShift: -8) bitAnd: 255)).
    self nextPut: (Character value: (aWord bitAnd: 255)).
```

---

## Target State (32-bit Words)

### HeapManager

```csharp
// SmalltalkHeapManager.cs
ObjectFormat.WordIndexed => new uint[indexedSize]

// ISmalltalkHeap.cs
uint GetIndexedWord(SmalltalkOOP objectOOP, int index);
void SetIndexedWord(SmalltalkOOP objectOOP, int index, uint value);
```

### Float Storage

- 8 bytes = 2 × 32-bit words
- Allocated with `AllocateInstanceOf(FloatClassOOP, 2)`

### Form Bitmap Calculation

```smalltalk
"32 pixels per 32-bit word for 1bpp"
bits := WordArray new: height * (width + 31 // 32)

"For 32bpp: 1 word per pixel (natural fit)"
bits := WordArray new: height * width
```

### BitBlt Stride

```csharp
// BitBltEngine.cs
int destStride = ((destWidth + 31) / 32) * 4;   // 4 bytes per 32-bit word
int bitIndex = 31 - (x % 32);                    // MSB first in 32-bit word
```

### File I/O (Unchanged - stays 16-bit)

The `nextWord` and `nextWordPut:` methods remain unchanged. They define the external file format, not internal representation.

---

## Migration Phases

### Phase 1: HeapManager Foundation

**Files to modify:**
- `Smalltalk80.Interpreter.HeapManager/SmalltalkHeapManager.cs`
- `Smalltalk80.Interpreter.HeapManager/ISmalltalkHeap.cs`
- `Smalltalk80.Interpreter.HeapManager/ObjectTableEntry.cs`

**Changes:**

| Location | Before | After |
|----------|--------|-------|
| `SmalltalkHeapManager.cs:155` | `new ushort[indexedSize]` | `new uint[indexedSize]` |
| `ISmalltalkHeap.cs:133` | `ushort GetIndexedWord(...)` | `uint GetIndexedWord(...)` |
| `ISmalltalkHeap.cs:139` | `void SetIndexedWord(..., ushort)` | `void SetIndexedWord(..., uint)` |
| `ObjectTableEntry.cs:64` | `AsWordArray() => ... as ushort[]` | `AsWordArray() => ... as uint[]` |
| `SmalltalkHeapManager.cs:435` | `public ushort GetIndexedWord(...)` | `public uint GetIndexedWord(...)` |
| `SmalltalkHeapManager.cs:448` | `public void SetIndexedWord(..., ushort)` | `public void SetIndexedWord(..., uint)` |

**Validation:**
- [ ] All existing tests pass
- [ ] WordArray operations work correctly

---

### Phase 2: Float Primitives

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Primitives/FloatPrimitives.cs`

**Changes:**

```csharp
// BEFORE: 4 words for 8-byte double
var floatOop = ctx.HeapManager.AllocateInstanceOf(ctx.Metadata!.FloatClassOOP, 4);

// AFTER: 2 words for 8-byte double
var floatOop = ctx.HeapManager.AllocateInstanceOf(ctx.Metadata!.FloatClassOOP, 2);
```

**Also update:**
- `GetFloatAsDouble` - read 2 words instead of 4
- `SetFloatFromDouble` - write 2 words instead of 4

**Validation:**
- [ ] Float arithmetic tests pass
- [ ] Float comparison tests pass
- [ ] Float transcendental function tests pass

---

### Phase 3: BitBltEngine

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Graphics/BitBltEngine.cs`

**Changes:**

| Location | Before | After |
|----------|--------|-------|
| Stride calculation | `((width + 15) / 16) * 2` | `((width + 31) / 32) * 4` |
| Bit index (MSB first) | `15 - (x % 16)` | `31 - (x % 32)` |
| Word selection | `x / 16` | `x / 32` |
| Bits per word constant | 16 | 32 |
| Bytes per word constant | 2 | 4 |

**Validation:**
- [ ] All 16 combination rules work
- [ ] Clipping works correctly
- [ ] Halftone patterns work
- [ ] Display updates correctly

---

### Phase 4: Font Bridges

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Graphics/WinFormsFontBridge.cs`
- `Smalltalk80.Interpreter/Core/VM/Graphics/GdiBitmapFontBridge.cs`
- `Smalltalk80.Interpreter/Core/VM/Graphics/FontBridgeFactory.cs`
- `Smalltalk80.Interpreter/Core/VM/Graphics/NullFontBridge.cs`

**Changes (apply to all):**

| Pattern | Before | After |
|---------|--------|-------|
| Words per row | `(width + 15) / 16` | `(width + 31) / 32` |
| Array type | `ushort[]` | `uint[]` |
| Bit index | `15 - (x % 16)` | `31 - (x % 32)` |
| Word index | `y * wordsPerRow + x / 16` | `y * wordsPerRow + x / 32` |
| Bit set | `(ushort)(1 << bitIndex)` | `(uint)(1 << bitIndex)` |

**Validation:**
- [ ] Text renders correctly
- [ ] All characters display properly
- [ ] Underscore/left-arrow conversion works

---

### Phase 5: Display Bitmap Creation

**Files to modify:**
- `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/RuntimeObjectCreator.cs`

**Changes:**

```csharp
// BEFORE
int bytesPerRow = displayWidth / 8;
int totalBytes = bytesPerRow * displayHeight;
int totalWords = totalBytes / 2;  // 2 bytes per 16-bit word

// AFTER
int bitsPerRow = displayWidth;
int totalBits = bitsPerRow * displayHeight;
int totalWords = (totalBits + 31) / 32;  // 32 bits per 32-bit word
```

**Validation:**
- [ ] Display initializes correctly
- [ ] Correct dimensions

---

### Phase 6: Smalltalk Form Code

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**Locations to change (search for `+ 15 // 16`):**

| Line | Context | Before | After |
|------|---------|--------|-------|
| 19612 | `Form>>extent:` | `width + 15 // 16` | `width + 31 // 32` |
| 19626 | `Form>>extent:offset:` | `width + 15 // 16` | `width + 31 // 32` |
| 20177 | `Form>>writeOnFileNamed:` | `width + 15 // 16` | See file I/O conversion |
| 20238 | `Form>>setExtent:fromArray:` | `width + 15 // 16` | `width + 31 // 32` |
| 20246 | `Form>>setExtent:fromCompactArray:` | `width + 15 // 16` | `width + 31 // 32` |
| 20361 | `Form class>>readFormFile:` | `width + 15 // 16` | See file I/O conversion |
| 20363 | `Form class>>readFormFile:` | `width + 15 // 16` | See file I/O conversion |

**Validation:**
- [ ] Form creation works
- [ ] Form copying works
- [ ] BitBlt operations work

---

### Phase 7: File I/O Compatibility Layer

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**New/Modified Methods:**

#### Form Writing (32-bit internal → 16-bit file)

```smalltalk
Form>>writeOnFileNamed: fileName
    "Write Form to file in 16-bit word format for compatibility"
    | file |
    file := FileStream fileNamed: fileName.
    file binary.
    file nextWordPut: 1.  "fileCode"
    file nextWordPut: width.
    file nextWordPut: height.
    file nextWordPut: offset x.
    file nextWordPut: offset y.
    self write16BitWordsTo: file.
    file close.

Form>>write16BitWordsTo: aStream
    "Convert internal 32-bit words to 16-bit file format (column-major)"
    | internalWordsPerRow fileWordsPerRow |
    internalWordsPerRow := width + 31 // 32.
    fileWordsPerRow := width + 15 // 16.
    1 to: fileWordsPerRow do: [:col |
        1 to: height do: [:row |
            | internalWordIndex internalBitOffset word16 |
            "Map file column to internal word and half"
            internalWordIndex := (row - 1) * internalWordsPerRow + ((col - 1) // 2) + 1.
            internalBitOffset := ((col - 1) \\ 2) * 16.
            "Extract 16-bit portion (high half first for even cols, low for odd)"
            internalBitOffset = 0
                ifTrue: [word16 := (bits at: internalWordIndex) bitShift: -16]
                ifFalse: [word16 := (bits at: internalWordIndex) bitAnd: 16rFFFF].
            aStream nextWordPut: word16]].
```

#### Form Reading (16-bit file → 32-bit internal)

```smalltalk
Form class>>readFormFile: file
    "Read Form from 16-bit word format file into 32-bit internal storage"
    | newForm newWidth newHeight theBits offsetX offsetY
      fileWordsPerRow internalWordsPerRow |
    newForm := self new.
    newWidth := file nextWord.
    newHeight := file nextWord.
    newForm extent: newWidth @ newHeight.
    offsetX := file nextWord.
    offsetY := file nextWord.
    offsetX > 32767 ifTrue: [offsetX := offsetX - 65536].
    offsetY > 32767 ifTrue: [offsetY := offsetY - 65536].
    newForm offset: offsetX @ offsetY.

    "Allocate 32-bit internal storage"
    internalWordsPerRow := newWidth + 31 // 32.
    theBits := WordArray new: internalWordsPerRow * newHeight.

    "Read 16-bit file words and pack into 32-bit internal words"
    fileWordsPerRow := newWidth + 15 // 16.
    1 to: fileWordsPerRow do: [:col |
        1 to: newHeight do: [:row |
            | word16 internalWordIndex internalBitOffset |
            word16 := file nextWord.
            internalWordIndex := (row - 1) * internalWordsPerRow + ((col - 1) // 2) + 1.
            internalBitOffset := ((col - 1) \\ 2) * 16.
            "Pack into high or low half of 32-bit word"
            internalBitOffset = 0
                ifTrue: [theBits at: internalWordIndex put:
                    ((theBits at: internalWordIndex) bitOr: (word16 bitShift: 16))]
                ifFalse: [theBits at: internalWordIndex put:
                    ((theBits at: internalWordIndex) bitOr: word16)]]].

    newForm bits: theBits.
    file close.
    ^newForm
```

#### WordArray Byte Stream Conversion

```smalltalk
WordArray>>fromByteStream: aStream
    "Read 16-bit words from stream, pack into 32-bit internal words"
    | word16High word16Low |
    1 to: self size do: [:i |
        "Read two 16-bit words, pack into one 32-bit word"
        word16High := (aStream next asInteger bitShift: 8) + aStream next asInteger.
        word16Low := (aStream next asInteger bitShift: 8) + aStream next asInteger.
        self at: i put: (word16High bitShift: 16) + word16Low].
```

**Validation:**
- [ ] Can read existing Form files
- [ ] Written files can be read back
- [ ] Round-trip preserves image data

---

## 32bpp Integration

With 32-bit words, the 32bpp depth becomes natural:

```smalltalk
Form>>wordsPerRow
    "Words needed per row based on depth"
    depth = 32 ifTrue: [^width].        "1 word per pixel (32-bit ARGB)"
    ^(width + 31) // 32                  "1bpp: 32 pixels per word"

Form>>bytesPerRow
    ^self wordsPerRow * 4               "4 bytes per 32-bit word"
```

---

## Form File Formats

### Design Decision: Use Standard Image Formats for 32bpp

Rather than creating a custom 32bpp Form file format, we use **standard image formats** (PNG, BMP) for color images:

| Use Case | Format | Rationale |
|----------|--------|-----------|
| **1bpp legacy** | fileCode=1 (.form) | Backward compatibility with Smalltalk archives |
| **32bpp color** | PNG (.png) | Industry standard, alpha support, lossless compression |
| **32bpp simple** | BMP (.bmp) | Uncompressed, simple, Windows native |

**Benefits of standard formats:**
- External tools (Photoshop, GIMP, Paint.NET) can create/edit images
- Well-documented specifications with no maintenance burden
- Existing C# libraries (`System.Drawing.Common`, `ImageSharp`, `SkiaSharp`)
- Industry-proven compression (PNG achieves ~70% size reduction)
- Future-proof and universally supported

### Format 1: 1bpp Monochrome (Existing Smalltalk Format)

Keep the original Blue Book format for backward compatibility with existing `.form` files:

```
Offset  Size     Field      Description
------  ----     -----      -----------
0       2 bytes  fileCode   = 1 (identifies 1bpp Form)
2       2 bytes  width      Width in pixels (unsigned 16-bit)
4       2 bytes  height     Height in pixels (unsigned 16-bit)
6       2 bytes  offsetX    X offset (signed, two's complement)
8       2 bytes  offsetY    Y offset (signed, two's complement)
10      N×2      bits       Bitmap data (16-bit words, column-major)
                            N = (width + 15) // 16 * height words
```

**Byte order**: Big-endian (MSB first) - matches original Xerox Alto

### Format 2: PNG for 32bpp Color Forms

Use standard PNG format via C# primitives:

```csharp
// In GraphicsPrimitives.cs or new ImagePrimitives.cs

// Primitive 2xx: Read PNG file into 32bpp Form
public static void ReadPngIntoForm(string filePath, Form targetForm)
{
    using var image = Image.Load<Rgba32>(filePath);
    // Copy pixels to Form's WordArray...
}

// Primitive 2xx: Write 32bpp Form to PNG file
public static void WriteFormToPng(Form sourceForm, string filePath)
{
    using var image = new Image<Rgba32>(width, height);
    // Copy Form's pixels to image...
    image.SaveAsPng(filePath);
}
```

### Smalltalk Interface

```smalltalk
Form class>>readFrom: fileName
    "Read Form from file, dispatching based on extension"
    | extension |
    extension := (fileName asPath) extension asLowercase.
    extension = 'form' ifTrue: [^self readFormFile: fileName].
    extension = 'png' ifTrue: [^self readPngFile: fileName].
    extension = 'bmp' ifTrue: [^self readBmpFile: fileName].
    self error: 'Unknown image format: ', extension.
    ^nil

Form class>>readPngFile: fileName
    "Read PNG file into 32bpp Form (uses C# primitive)"
    <primitive: 2xx>
    ^self primitiveFailed

Form>>writeOnFileNamed: fileName
    "Write Form to file based on extension"
    | extension |
    extension := (fileName asPath) extension asLowercase.
    extension = 'form' ifTrue: [^self writeFormFile: fileName].
    extension = 'png' ifTrue: [^self writePngFile: fileName].
    extension = 'bmp' ifTrue: [^self writeBmpFile: fileName].
    "Default: use depth-appropriate format"
    self depth = 32
        ifTrue: [self writePngFile: fileName, '.png']
        ifFalse: [self writeFormFile: fileName, '.form']

Form>>writePngFile: fileName
    "Write 32bpp Form to PNG file (uses C# primitive)"
    <primitive: 2xx>
    ^self primitiveFailed
```

### C# Library Options

| Library | License | Features | Recommendation |
|---------|---------|----------|----------------|
| **System.Drawing.Common** | MIT | Windows only, GDI+ | Simple, but Windows-only |
| **ImageSharp** | Apache 2.0 | Cross-platform, pure C# | Best for portability |
| **SkiaSharp** | MIT | Cross-platform, Google Skia | Good if using Skia elsewhere |

**Recommended**: `ImageSharp` for cross-platform support without native dependencies.

### Depth Conversion

```smalltalk
Form>>as32bpp
    "Convert 1bpp Form to 32bpp"
    | newForm |
    newForm := Form extent: self extent depth: 32.
    "Black pixels (1) become 0xFF000000, white (0) becomes 0xFFFFFFFF"
    0 to: height - 1 do: [:y |
        0 to: width - 1 do: [:x |
            | pixel |
            pixel := self pixelAt: x@y.
            newForm pixelAt: x@y put:
                (pixel = 1 ifTrue: [16rFF000000] ifFalse: [16rFFFFFFFF])]].
    ^newForm

Form>>as1bpp
    "Convert 32bpp Form to 1bpp using luminance threshold"
    | newForm |
    newForm := Form extent: self extent depth: 1.
    0 to: height - 1 do: [:y |
        0 to: width - 1 do: [:x |
            | pixel r g b luminance |
            pixel := self pixelAt: x@y.
            r := (pixel bitShift: -16) bitAnd: 255.
            g := (pixel bitShift: -8) bitAnd: 255.
            b := pixel bitAnd: 255.
            luminance := (r * 0.299) + (g * 0.587) + (b * 0.114).
            newForm pixelAt: x@y put: (luminance < 128 ifTrue: [1] ifFalse: [0])]].
    ^newForm
```

### Implementation Phases

1. **Add ImageSharp NuGet package** to Interpreter project
2. **Implement PNG read primitive** for loading 32bpp images
3. **Implement PNG write primitive** for saving 32bpp images
4. **Update Form class** with file extension dispatch
5. **Add BMP support** (optional, for uncompressed option)

---

## Testing Strategy

### Unit Tests

- [ ] HeapManager word operations
- [ ] Float arithmetic and storage
- [ ] BitBlt all 16 rules
- [ ] Form creation and manipulation

### Integration Tests

- [ ] Cold start completes successfully
- [ ] All 286 init expressions pass
- [ ] BlockContext tests pass (returns 1191)
- [ ] System Browser opens and displays correctly
- [ ] Text rendering works

### File Compatibility Tests

- [ ] Read existing 1bpp Form files (.form, fileCode=1)
- [ ] Write 1bpp Form files readable by original format
- [ ] Round-trip 1bpp Form files without data loss
- [ ] Read PNG files into 32bpp Forms
- [ ] Write 32bpp Forms to PNG files
- [ ] Round-trip PNG files without data loss
- [ ] PNG files readable by external tools (GIMP, etc.)
- [ ] Convert 1bpp Form to 32bpp (as32bpp)
- [ ] Convert 32bpp Form to 1bpp (as1bpp with luminance threshold)
- [ ] Handle unknown file extensions gracefully

---

## Rollback Plan

If migration causes issues:

1. Revert HeapManager to `ushort[]`
2. Revert all formula changes
3. Remove file conversion layer

The changes are localized enough that rollback is straightforward.

---

## Open Questions

### Word Size Migration

1. **WordArray>>at:put: range checking**: Should we validate values fit in 32 bits?
2. **Primitive 60/61 (at:/at:put:)**: Do these need updating for 32-bit words?
3. **Snapshot/image format**: Does the image save/restore need conversion?
4. **Large integers**: Any impact on LargePositiveInteger word storage?

### Image I/O

5. **Library choice**: ImageSharp vs SkiaSharp vs System.Drawing.Common?
6. **Primitive numbers**: Which primitive range for PNG/BMP read/write?
7. **Error handling**: How to report image load failures to Smalltalk?
8. **Metadata**: Should we preserve/restore Form offset in PNG metadata?

### Graphics Architecture

9. **Premultiplied alpha**: Should we use premultiplied alpha for better blending?
10. **Halftone forms**: Should halftone Forms be limited to 1bpp, or allow 32bpp patterns?
11. **StrikeFont rendering**: Always render 1bpp fonts to 32bpp at display time?

### BitBlt Considerations

12. **Mixed-depth BitBlt**: What combination rules apply when copying 1bpp to 32bpp or vice versa?
13. **Alpha blending rules**: Should rule 24 (alphaBlend) be implemented in C# primitive or Smalltalk?
14. **Cursor depth**: Should cursors support 32bpp, or stay 1bpp with color expansion?

---

## References

- Blue Book Chapter 18: Graphics Kernel
- Blue Book Chapter 27: Object Memory
- `docs/Graphics-Architecture.md`: 32bpp implementation plan
- `docs/Form-File-Format.md`: Original 1bpp Form file format
- `docs/Architecture/HEAP-MANAGER-ARCHITECTURE.md`: HeapManager design
