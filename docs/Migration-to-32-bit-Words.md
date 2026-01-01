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

The existing Form file format uses **fileCode** to identify the file type. We extend this for 32bpp support:

### File Code Values

| fileCode | Depth | Description |
|----------|-------|-------------|
| 1 | 1bpp | Original Blue Book monochrome format |
| 2 | 32bpp | New color format (ARGB) |

### Format 1: 1bpp Monochrome (Existing)

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

**Pixel layout**:
- 16 pixels per 16-bit word
- MSB = leftmost pixel
- Column-major storage order

### Format 2: 32bpp Color (New)

```
Offset  Size     Field      Description
------  ----     -----      -----------
0       2 bytes  fileCode   = 2 (identifies 32bpp Form)
2       2 bytes  width      Width in pixels (unsigned 16-bit)
4       2 bytes  height     Height in pixels (unsigned 16-bit)
6       2 bytes  offsetX    X offset (signed, two's complement)
8       2 bytes  offsetY    Y offset (signed, two's complement)
10      W×H×4    pixels     Pixel data (32-bit ARGB, row-major)
                            Total = width * height * 4 bytes
```

**Byte order**: Little-endian for pixel data (matches modern systems)

**Pixel format**: ARGB (8 bits each)
```
Byte 0: Blue
Byte 1: Green
Byte 2: Red
Byte 3: Alpha (255 = opaque, 0 = transparent)
```

**Pixel layout**:
- Row-major order (more natural for modern graphics)
- 1 pixel per 32-bit word
- Top-left to bottom-right

### Reading Forms (Dispatch by fileCode)

```smalltalk
Form class>>readFrom: fileName
    "Read Form from file, dispatching based on fileCode"
    | file code |
    file := FileStream oldFileNamed: fileName.
    file readOnly; binary.
    code := file nextWord.
    code = 1 ifTrue: [^self read1bppFormFile: file].
    code = 2 ifTrue: [^self read32bppFormFile: file].
    file close.
    self error: 'Unknown Form file format: ', code printString.
    ^nil

Form class>>read1bppFormFile: file
    "Read 1bpp Form (fileCode already consumed)"
    "... existing readFormFile: logic with 16-bit to 32-bit conversion ..."

Form class>>read32bppFormFile: file
    "Read 32bpp Form (fileCode already consumed)"
    | newForm newWidth newHeight theBits offsetX offsetY |
    newForm := self new.
    newWidth := file nextWord.
    newHeight := file nextWord.
    offsetX := file nextWord.
    offsetY := file nextWord.
    offsetX > 32767 ifTrue: [offsetX := offsetX - 65536].
    offsetY > 32767 ifTrue: [offsetY := offsetY - 65536].

    "Allocate 32bpp storage (1 word per pixel)"
    theBits := WordArray new: newWidth * newHeight.

    "Read pixel data (row-major, little-endian ARGB)"
    1 to: newHeight do: [:y |
        1 to: newWidth do: [:x |
            | b g r a pixel |
            b := file next asInteger.
            g := file next asInteger.
            r := file next asInteger.
            a := file next asInteger.
            pixel := (a bitShift: 24) + (r bitShift: 16) + (g bitShift: 8) + b.
            theBits at: ((y - 1) * newWidth + x) put: pixel]].

    newForm
        setExtent: newWidth @ newHeight
        depth: 32
        offset: offsetX @ offsetY
        bits: theBits.
    file close.
    ^newForm
```

### Writing Forms (Dispatch by depth)

```smalltalk
Form>>writeOnFileNamed: fileName
    "Write Form to file in appropriate format based on depth"
    self depth = 32
        ifTrue: [self write32bppOnFileNamed: fileName]
        ifFalse: [self write1bppOnFileNamed: fileName]

Form>>write1bppOnFileNamed: fileName
    "Write 1bpp Form in original Blue Book format"
    | file |
    file := FileStream fileNamed: fileName.
    file binary.
    file nextWordPut: 1.  "fileCode = 1 for 1bpp"
    file nextWordPut: width.
    file nextWordPut: height.
    file nextWordPut: offset x.
    file nextWordPut: offset y.
    self write16BitWordsTo: file.  "Convert 32-bit internal to 16-bit file"
    file close.

Form>>write32bppOnFileNamed: fileName
    "Write 32bpp Form in new color format"
    | file |
    file := FileStream fileNamed: fileName.
    file binary.
    file nextWordPut: 2.  "fileCode = 2 for 32bpp"
    file nextWordPut: width.
    file nextWordPut: height.
    file nextWordPut: offset x.
    file nextWordPut: offset y.
    "Write pixel data (row-major, little-endian ARGB)"
    1 to: height do: [:y |
        1 to: width do: [:x |
            | pixel |
            pixel := bits at: ((y - 1) * width + x).
            file nextPut: (pixel bitAnd: 16rFF).           "Blue"
            file nextPut: ((pixel bitShift: -8) bitAnd: 16rFF).   "Green"
            file nextPut: ((pixel bitShift: -16) bitAnd: 16rFF).  "Red"
            file nextPut: ((pixel bitShift: -24) bitAnd: 16rFF)]]. "Alpha"
    file close.
```

### Design Rationale

| Decision | Rationale |
|----------|-----------|
| **fileCode dispatch** | Natural extension point, existing code checks for fileCode=1 |
| **Row-major for 32bpp** | Matches modern graphics conventions, simpler pixel addressing |
| **Little-endian pixels** | Matches x86/ARM memory layout, efficient for modern CPUs |
| **BGRA byte order** | Matches Windows DIB and most GPU texture formats |
| **Keep column-major for 1bpp** | Backward compatibility with existing Form files |

### Migration Path

1. **Phase 1**: Add depth instance variable to Form (see Graphics-Architecture.md)
2. **Phase 2**: Implement 32bpp file read/write methods
3. **Phase 3**: Update `readFrom:` to dispatch on fileCode
4. **Phase 4**: Update `writeOnFileNamed:` to dispatch on depth

### Compatibility Matrix

| File Format | 1bpp Form (runtime) | 32bpp Form (runtime) |
|-------------|---------------------|----------------------|
| **fileCode=1 (1bpp file)** | Native read | Read + expand to 32bpp |
| **fileCode=2 (32bpp file)** | Read + threshold to 1bpp | Native read |

For cross-depth file I/O:

```smalltalk
Form class>>read1bppFormFile: file asDepth: targetDepth
    "Read 1bpp file, optionally convert to 32bpp"
    | form |
    form := self read1bppFormFile: file.
    targetDepth = 32 ifTrue: [^form as32bpp].
    ^form

Form>>as32bpp
    "Convert 1bpp Form to 32bpp"
    | newForm |
    newForm := Form extent: self extent depth: 32.
    "Black pixels become 0xFF000000, white becomes 0xFFFFFFFF"
    self do: [:pixel :x :y |
        newForm pixelAt: x@y put:
            (pixel = 1 ifTrue: [16rFF000000] ifFalse: [16rFFFFFFFF])].
    ^newForm

Form>>as1bpp
    "Convert 32bpp Form to 1bpp using luminance threshold"
    | newForm |
    newForm := Form extent: self extent depth: 1.
    self do: [:pixel :x :y |
        | r g b luminance |
        r := (pixel bitShift: -16) bitAnd: 255.
        g := (pixel bitShift: -8) bitAnd: 255.
        b := pixel bitAnd: 255.
        luminance := (r * 0.299) + (g * 0.587) + (b * 0.114).
        newForm pixelAt: x@y put: (luminance < 128 ifTrue: [1] ifFalse: [0])].
    ^newForm
```

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

- [ ] Read existing 1bpp Form files (fileCode=1)
- [ ] Write 1bpp Form files readable by original format
- [ ] Round-trip 1bpp Form files without data loss
- [ ] Write and read 32bpp Form files (fileCode=2)
- [ ] Round-trip 32bpp Form files without data loss
- [ ] Convert 1bpp Form to 32bpp (as32bpp)
- [ ] Convert 32bpp Form to 1bpp (as1bpp with luminance threshold)
- [ ] Handle unknown fileCode gracefully (error message)

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

### 32bpp File Format

5. **Compression**: Should 32bpp files support optional compression (e.g., PNG-style)?
6. **Premultiplied alpha**: Should we use premultiplied alpha for better blending performance?
7. **Color space**: Should we specify sRGB explicitly or leave it implicit?
8. **Halftone forms**: Should halftone Forms be limited to 1bpp, or allow 32bpp patterns?
9. **StrikeFont format**: Do fonts need a 32bpp format, or always render from 1bpp to 32bpp?

### BitBlt Considerations

10. **Mixed-depth BitBlt**: What combination rules apply when copying 1bpp to 32bpp or vice versa?
11. **Alpha blending rules**: Should rule 24 (alphaBlend) be implemented in C# primitive or Smalltalk?
12. **Cursor depth**: Should cursors support 32bpp, or stay 1bpp with color expansion?

---

## References

- Blue Book Chapter 18: Graphics Kernel
- Blue Book Chapter 27: Object Memory
- `docs/Graphics-Architecture.md`: 32bpp implementation plan
- `docs/Form-File-Format.md`: Original 1bpp Form file format
- `docs/Architecture/HEAP-MANAGER-ARCHITECTURE.md`: HeapManager design
