# Migration to 32-bit Words

**Created**: 2026-01-01
**Status**: Planning
**Branch**: `claude/review-graphics-architecture-FssRQ`
**Master Plan**: See `docs/32-BIT-IMPLEMENTATION-PLAN.md` for consolidated phased plan

> **Note**: This document provides detailed technical reference for the 32-bit word migration.
> For the implementation roadmap with milestones, see the Master Plan.

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
- `Smalltalk80.Interpreter/Core/VM/Graphics/FontBridgeFactory.cs` (contains NullFontBridge)

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

### Design Decision: Pure Smalltalk PNG Implementation

Implement PNG reading/writing entirely in Smalltalk, with no C# library dependencies:

| Depth | PNG Type | Notes |
|-------|----------|-------|
| **1bpp** | 1-bit indexed (2-color palette) | Excellent compression for line art |
| **32bpp** | RGBA (8 bits per channel) | Full color with alpha |

**Benefits:**
- No external library dependencies
- Runs on any Smalltalk VM implementation
- Full control over implementation
- Educational value - demonstrates Smalltalk's capability
- External tools (GIMP, Photoshop) work with all images

**Design Principle: Primitive-Ready Interfaces**

Design all PNG classes with clean method boundaries so that performance-critical methods can be replaced with C# primitives later if needed:

```smalltalk
DeflateStream>>inflate
    "Decompress DEFLATE stream. Could be primitiveInflate for speed."
    <primitive: 'primitiveDeflateInflate'>
    ^self inflateInSmalltalk  "Fallback to pure Smalltalk"

CRC32>>updateWith: aByteArray
    "Update CRC with bytes. Could be primitiveCRC32Update for speed."
    <primitive: 'primitiveCRC32Update'>
    ^self updateWithInSmalltalk: aByteArray  "Fallback"
```

This approach:
- Delivers working code immediately (pure Smalltalk)
- Identifies optimization targets through profiling
- Allows incremental optimization without architectural changes
- Maintains testability (Smalltalk fallback always available)

### Legacy .form Format (Deprecated)

The original Blue Book `.form` format is supported for **reading only** to allow migration:

```
Offset  Size     Field      Description
------  ----     -----      -----------
0       2 bytes  fileCode   = 1 (identifies 1bpp Form)
2       2 bytes  width      Width in pixels (unsigned 16-bit)
4       2 bytes  height     Height in pixels (unsigned 16-bit)
6       2 bytes  offsetX    X offset (signed, two's complement)
8       2 bytes  offsetY    Y offset (signed, two's complement)
10      N×2      bits       Bitmap data (16-bit words, column-major)
```

### PNG Implementation in Smalltalk

PNG format requires implementing these components:

#### Required Classes

```smalltalk
Object subclass: #PNGReader
    instanceVariableNames: 'stream width height bitDepth colorType chunks'

Object subclass: #PNGWriter
    instanceVariableNames: 'stream form compressionLevel'

Object subclass: #DeflateStream
    instanceVariableNames: 'stream window position'
    "Implements RFC 1951 DEFLATE decompression/compression"

Object subclass: #CRC32
    instanceVariableNames: 'table value'
    "Implements CRC-32 as used by PNG"
```

#### PNG File Structure

```
PNG Signature: 89 50 4E 47 0D 0A 1A 0A (8 bytes)

Chunks (each chunk):
  Length:     4 bytes (big-endian, data length only)
  Type:       4 bytes (ASCII: IHDR, IDAT, IEND, PLTE, etc.)
  Data:       [Length] bytes
  CRC:        4 bytes (CRC-32 of Type + Data)

Required chunks:
  IHDR - Image header (width, height, bit depth, color type)
  IDAT - Image data (deflate-compressed, filtered scanlines)
  IEND - Image end (empty)

Optional chunks:
  PLTE - Palette (for indexed color)
  tRNS - Transparency
  ... (many others we can ignore)
```

#### PNG Color Types

| Type | Name | Channels | Our Support |
|------|------|----------|-------------|
| 0 | Grayscale | 1 | Read (convert to 1bpp or 32bpp) |
| 2 | RGB | 3 | Read (convert to 32bpp, alpha=255) |
| 3 | Indexed | 1 | Read/Write for 1bpp |
| 4 | Grayscale+Alpha | 2 | Read (convert to 32bpp) |
| 6 | RGBA | 4 | Read/Write for 32bpp |

#### PNG Filtering (per scanline)

| Filter | Algorithm |
|--------|-----------|
| 0 None | Raw bytes |
| 1 Sub | pixel[x] - pixel[x-1] |
| 2 Up | pixel[x] - pixel[x, y-1] |
| 3 Average | pixel[x] - (left + up) / 2 |
| 4 Paeth | pixel[x] - PaethPredictor(left, up, upperLeft) |

#### Implementation Phases

**Phase 1: Foundation Classes**
1. `CRC32` - Table-based CRC-32 implementation
2. `DeflateStream` - RFC 1951 decompression (reading)
3. `PNGChunk` - Chunk parsing/writing

**Phase 2: PNG Reading**
1. `PNGReader>>readFrom:` - Main entry point
2. Parse IHDR for dimensions and color type
3. Collect and decompress IDAT chunks
4. Apply reverse filtering to scanlines
5. Convert to Form based on color type

**Phase 3: PNG Writing**
1. `PNGWriter>>write:to:` - Main entry point
2. Write PNG signature
3. Write IHDR chunk
4. For indexed: write PLTE chunk
5. Filter scanlines and compress with Deflate
6. Write IDAT chunk(s)
7. Write IEND chunk

**Phase 4: Deflate Compression (for writing)**
1. Implement RFC 1951 compression
2. Can start with simple "stored" blocks (no compression)
3. Add LZ77 + Huffman later for better compression

### Smalltalk Interface

```smalltalk
Form class>>readFrom: fileName
    "Read Form from file, dispatching based on extension"
    | extension |
    extension := (fileName asPath) extension asLowercase.
    extension = 'form' ifTrue: [^self readLegacyFormFile: fileName].
    extension = 'png' ifTrue: [^PNGReader readFrom: fileName].
    self error: 'Unknown image format: ', extension

Form>>writeOnFileNamed: fileName
    "Write Form to PNG file"
    | baseName |
    baseName := (fileName asPath) withoutExtension asString.
    PNGWriter write: self to: baseName, '.png'

Form>>writePngFile: fileName
    "Write Form to PNG file"
    PNGWriter write: self to: fileName
```

### Deflate Implementation Notes

Deflate (RFC 1951) is the core compression. For reading:
- Parse block headers (BFINAL, BTYPE)
- Handle stored blocks (BTYPE=00): copy literal bytes
- Handle fixed Huffman (BTYPE=01): use predefined codes
- Handle dynamic Huffman (BTYPE=10): decode code tables first
- Maintain 32KB sliding window for back-references

For writing (simplified start):
- Use stored blocks only (BTYPE=00) - no compression
- Later add fixed Huffman for better compression
- Dynamic Huffman optional (best compression, most complex)

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

### Estimated Complexity

| Component | Lines of Code | Difficulty |
|-----------|---------------|------------|
| CRC32 | ~50 | Low |
| Deflate Decompression | ~300-500 | High |
| Deflate Compression (stored) | ~50 | Low |
| Deflate Compression (Huffman) | ~400 | High |
| PNG Chunk I/O | ~100 | Medium |
| PNG Reader | ~200 | Medium |
| PNG Writer | ~150 | Medium |
| **Total (basic)** | ~900 | |
| **Total (full compression)** | ~1300 | |

### References

- [PNG Specification](http://www.libpng.org/pub/png/spec/1.2/PNG-Contents.html)
- [RFC 1951 - DEFLATE](https://datatracker.ietf.org/doc/html/rfc1951)
- [RFC 1950 - ZLIB](https://datatracker.ietf.org/doc/html/rfc1950) (wrapper around DEFLATE)

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

- [ ] Read legacy .form files (deprecated, for migration)
- [ ] Write 1bpp Forms to PNG (1-bit indexed)
- [ ] Write 32bpp Forms to PNG (RGBA)
- [ ] Round-trip 1bpp PNG files without data loss
- [ ] Round-trip 32bpp PNG files without data loss
- [ ] PNG files readable by external tools (GIMP, etc.)
- [ ] External PNG files loadable as Forms
- [ ] Convert 1bpp Form to 32bpp (as32bpp)
- [ ] Convert 32bpp Form to 1bpp (as1bpp with luminance threshold)
- [ ] Migration script converts .form to .png correctly

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

### PNG Implementation

5. **Deflate priority**: Implement full Huffman compression, or start with stored blocks?
6. **Interlacing**: Support Adam7 interlacing, or non-interlaced only?
7. **Ancillary chunks**: Which optional chunks to support (tEXt, tIME, etc.)?
8. **Form offset**: Store in tEXt chunk, or ignore on export?

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
