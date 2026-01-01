# 32-bit Word Migration & 32bpp Graphics Implementation Plan

**Created**: 2026-01-01
**Status**: MILESTONE 1 COMPLETE
**Branch**: `claude/review-graphics-architecture-FssRQ`

---

## Executive Summary

This document provides a consolidated, phased implementation plan for:
1. **32-bit Word Migration** - Change internal word storage from 16-bit to 32-bit
2. **32bpp Graphics** - Add full-color Form support with alpha blending
3. **PNG I/O** - Pure Smalltalk image file support

### Dependencies

```
                    ┌─────────────────────┐
                    │  MILESTONE 1        │
                    │  32-bit Words       │
                    │  (Phases 1.1-1.7)   │
                    └─────────┬───────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
    ┌─────────────────┐ ┌───────────┐ ┌─────────────────┐
    │  MILESTONE 2    │ │ PARALLEL  │ │  MILESTONE 3    │
    │  32bpp Graphics │ │           │ │  PNG I/O        │
    │  (Phases 2.1-2.5)│ │           │ │  (Phases 3.1-3.4)│
    └─────────────────┘ └───────────┘ └─────────────────┘
              │                               │
              └───────────────┬───────────────┘
                              ▼
                    ┌─────────────────────┐
                    │  MILESTONE 4        │
                    │  Integration        │
                    │  (Phase 4.1)        │
                    └─────────────────────┘
```

### Design Principles

| Principle | Description |
|-----------|-------------|
| **Internal = 32-bit** | Runtime WordIndexed storage uses 32-bit words |
| **External = 16-bit** | Legacy file formats remain 16-bit for backward compatibility |
| **PNG for new files** | All new image I/O uses industry-standard PNG |
| **Primitive-ready** | Smalltalk code designed for optional C# primitive acceleration |
| **Two depths only** | Only 1bpp and 32bpp supported (no indexed colors) |

---

## MILESTONE 1: 32-bit Word Migration

**Goal**: Change internal word representation from 16-bit to 32-bit while maintaining file compatibility.

**Estimated effort**: Medium
**Risk**: Medium (touches core HeapManager)

### Phase 1.1: HeapManager Foundation

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

**Validation:**
- [x] Build succeeds with 0 errors
- [x] All C# unit tests pass
- [x] Cold start completes

---

### Phase 1.2: Float Primitives

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
- [x] Float arithmetic tests pass
- [x] Float comparison tests pass
- [x] `3.14159 * 2.0` returns correct result

---

### Phase 1.3: BitBltEngine

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Graphics/BitBltEngine.cs`

**Changes:**

| Pattern | Before | After |
|---------|--------|-------|
| Stride calculation | `((width + 15) / 16) * 2` | `((width + 31) / 32) * 4` |
| Bit index (MSB first) | `15 - (x % 16)` | `31 - (x % 32)` |
| Word selection | `x / 16` | `x / 32` |
| Bits per word | 16 | 32 |
| Bytes per word | 2 | 4 |

**Validation:**
- [x] All 16 combination rules work
- [x] Display shows correctly
- [x] Dragon curve renders properly

---

### Phase 1.4: Font Bridges

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Graphics/WinFormsFontBridge.cs`
- `Smalltalk80.Interpreter/Core/VM/Graphics/GdiBitmapFontBridge.cs`
- `Smalltalk80.Interpreter/Core/VM/Graphics/NullFontBridge.cs`

**Changes (apply to all):**

| Pattern | Before | After |
|---------|--------|-------|
| Words per row | `(width + 15) / 16` | `(width + 31) / 32` |
| Array type | `ushort[]` | `uint[]` |
| Bit index | `15 - (x % 16)` | `31 - (x % 32)` |
| Bit set | `(ushort)(1 << bitIndex)` | `(uint)(1 << bitIndex)` |

**Validation:**
- [x] Text renders correctly in System Browser
- [x] All characters display properly

---

### Phase 1.5: Display Bitmap Creation

**Files to modify:**
- `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/RuntimeObjectCreator.cs`

**Changes:**

```csharp
// BEFORE
int totalWords = totalBytes / 2;  // 2 bytes per 16-bit word

// AFTER
int totalWords = (totalBits + 31) / 32;  // 32 bits per 32-bit word
```

**Validation:**
- [x] Display initializes correctly
- [x] Window shows correct dimensions

---

### Phase 1.6: Smalltalk Form Code

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**Search for `+ 15 // 16` and update to `+ 31 // 32`:**

| Method | Change |
|--------|--------|
| `Form>>extent:` | `width + 15 // 16` → `width + 31 // 32` |
| `Form>>extent:offset:` | `width + 15 // 16` → `width + 31 // 32` |
| `Form>>setExtent:fromArray:` | `width + 15 // 16` → `width + 31 // 32` |
| `Form>>setExtent:fromCompactArray:` | `width + 15 // 16` → `width + 31 // 32` |

**Note:** File I/O methods handled separately in Phase 1.7.

**Validation:**
- [x] Form creation works
- [x] Form copying works
- [x] BitBlt operations work

---

### Phase 1.7: Legacy File I/O Compatibility

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**New methods for 16-bit ↔ 32-bit conversion at file boundaries:**

```smalltalk
Form class>>readLegacyFormFile: fileName
    "Read legacy 16-bit .form file into 32-bit internal storage"
    | file fileCode ... |
    file := FileStream readOnlyFileNamed: fileName.
    file binary.
    fileCode := file nextWord.  "Still reads 16-bit words"
    fileCode = 1 ifFalse: [self error: 'Invalid form file'].
    "... pack two 16-bit words into each 32-bit internal word ..."
```

**Validation:**
- [x] Can read existing .form files
- [x] Round-trip preserves image data

---

### MILESTONE 1 CHECKPOINT

**Run full regression test:**

```bash
cd Smalltalk80.Interpreter
dotnet build
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

**Additional validation:**
- [x] All 286 init expressions pass
- [x] System Browser opens and displays source code
- [x] Text rendering works
- [x] Dragon curve renders correctly

---

## MILESTONE 2: 32bpp Graphics

**Goal**: Add full-color Form support with 32-bit ARGB pixels.

**Estimated effort**: High
**Risk**: Medium
**Prerequisite**: MILESTONE 1 complete

### Phase 2.1: Color Class (Already Complete)

**Status**: ✅ Already implemented in `Smalltalk-80.st` (lines 31693-31939)

| Class | Features |
|-------|----------|
| **Color** | RGB (10-bit/component), named colors, `asPixelValue`, `asFormColor` |
| **TranslucentColor** | Alpha support, `r:g:b:alpha:`, `fromInteger:` |

**Validation:**
- [ ] `Color red asPixelValue` returns `16rFFFF0000`
- [ ] `(Color r: 0.5 g: 0.5 b: 0.5) asPixelValue` returns gray

---

### Phase 2.2: Form Depth Instance Variable

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**Changes:**

```smalltalk
DisplayMedium subclass: #Form
    instanceVariableNames: 'bits width height offset depth'
    "Add 'depth' instance variable"

Form>>depth
    "Default 1 for backward compatibility"
    ^depth ifNil: [1]

Form>>wordsPerRow
    "Words needed per row (32-bit internal words)"
    depth = 32 ifTrue: [^width].        "1 word per pixel"
    ^(width + 31) // 32                  "1bpp: 32 pixels per word"

Form class>>extent: extentPoint depth: bitsPerPixel
    "Create Form with specified depth (1 or 32 only)"
    (bitsPerPixel = 1 or: [bitsPerPixel = 32])
        ifFalse: [self error: 'Only depths 1 and 32 supported'].
    ^self new setExtent: extentPoint depth: bitsPerPixel
```

**Validation:**
- [ ] `(Form extent: 100@100) depth` returns 1
- [ ] `(Form extent: 100@100 depth: 32) wordsPerRow` returns 100

---

### Phase 2.3: BitBltEngine32 (C#)

**Files to create:**
- `Smalltalk80.Interpreter/Core/VM/Graphics/BitBltEngine32.cs`

**Implement:**
- All 16 original combination rules for 32bpp
- Alpha blending (rule 24)
- pixPaint (rule 25) - replace where src ≠ 0

**Signature:**

```csharp
public static class BitBltEngine32
{
    public static void CopyBits32(
        uint[] destBits, int destWidth, int destHeight,
        uint[] sourceBits, int sourceWidth, int sourceHeight,
        int destX, int destY, int width, int height,
        int sourceX, int sourceY,
        int clipX, int clipY, int clipWidth, int clipHeight,
        uint[] halftoneBits, int combinationRule);
}
```

**Validation:**
- [ ] Rule 3 (copy) works for 32bpp
- [ ] Rule 24 (alphaBlend) blends correctly
- [ ] Clipping works

---

### Phase 2.4: Primitive 96 Depth Dispatch

**Files to modify:**
- `Smalltalk80.Interpreter/Core/VM/Primitives/GraphicsPrimitives.cs`

**Changes:**

```csharp
private void CopyBitsWithDepth(int srcDepth, int dstDepth, ...)
{
    (srcDepth, dstDepth) switch
    {
        (1, 1)   => BitBltEngine.CopyBits(...),       // Existing
        (32, 32) => BitBltEngine32.CopyBits32(...),   // New
        (1, 32)  => BitBltEngine32.Expand1To32(...),  // New
        (32, 1)  => BitBltEngine32.Reduce32To1(...),  // New
        _ => throw new PrimitiveFailed("Unsupported depth combination")
    };
}
```

**Validation:**
- [ ] 1bpp Forms work as before
- [ ] 32bpp Forms render correctly
- [ ] Mixed-depth BitBlt converts properly

---

### Phase 2.5: Depth Conversion Methods

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**Add to Form:**

```smalltalk
Form>>as32bpp
    "Convert 1bpp Form to 32bpp"
    | newForm |
    self depth = 32 ifTrue: [^self copy].
    newForm := Form extent: self extent depth: 32.
    "Black (1) → 0xFF000000, White (0) → 0xFFFFFFFF"
    ...

Form>>as1bpp
    "Convert 32bpp Form to 1bpp using luminance threshold"
    | newForm |
    self depth = 1 ifTrue: [^self copy].
    newForm := Form extent: self extent depth: 1.
    "Luminance < 50% → 1 (black), >= 50% → 0 (white)"
    ...
```

**Validation:**
- [ ] `aForm as32bpp as1bpp` preserves black/white pattern
- [ ] Color images convert to reasonable monochrome

---

### MILESTONE 2 CHECKPOINT

- [ ] Create 32bpp Form, fill with color, display
- [ ] Alpha blending works (TranslucentColor)
- [ ] Mixed-depth BitBlt converts correctly
- [ ] Existing 1bpp code unaffected

---

## MILESTONE 3: PNG I/O

**Goal**: Pure Smalltalk PNG reader/writer for all Form depths.

**Estimated effort**: High (Deflate is complex)
**Risk**: Low (isolated from core system)
**Can run in parallel with**: MILESTONE 2 (after MILESTONE 1)

### Phase 3.1: Foundation Classes

**Files to create in Smalltalk-80.st:**

```smalltalk
Object subclass: #CRC32
    instanceVariableNames: 'table value'
    "CRC-32 checksum as used by PNG"

Object subclass: #DeflateStream
    instanceVariableNames: 'stream window position bitBuffer bitCount'
    "RFC 1951 DEFLATE decompression"

Object subclass: #PNGChunk
    instanceVariableNames: 'type data'
    "PNG chunk parsing/writing"
```

**Design principle - Primitive-ready interfaces:**

```smalltalk
CRC32>>updateWith: aByteArray
    "Update CRC with bytes. Could be primitive for speed."
    <primitive: 'primitiveCRC32Update'>
    ^self updateWithInSmalltalk: aByteArray

DeflateStream>>inflate
    "Decompress stream. Could be primitive for speed."
    <primitive: 'primitiveDeflateInflate'>
    ^self inflateInSmalltalk
```

**Validation:**
- [ ] CRC32 produces correct checksums (test vectors from spec)
- [ ] DeflateStream decompresses known test data

---

### Phase 3.2: PNG Reader

**Files to add to Smalltalk-80.st:**

```smalltalk
Object subclass: #PNGReader
    instanceVariableNames: 'stream width height bitDepth colorType chunks'

PNGReader class>>readFrom: fileName
    "Read PNG file, return Form"
    ^self new readFrom: fileName

PNGReader>>readFrom: fileName
    "Parse PNG, decompress IDAT, convert to Form"
    ...
```

**Supported color types:**
- Type 0: Grayscale → 1bpp or 32bpp
- Type 2: RGB → 32bpp (alpha=255)
- Type 3: Indexed → 1bpp (2-color) or 32bpp
- Type 6: RGBA → 32bpp

**Validation:**
- [ ] Read 1bpp PNG → 1bpp Form
- [ ] Read RGBA PNG → 32bpp Form
- [ ] External PNG files (GIMP-created) load correctly

---

### Phase 3.3: PNG Writer

**Files to add to Smalltalk-80.st:**

```smalltalk
Object subclass: #PNGWriter
    instanceVariableNames: 'stream form'

PNGWriter class>>write: aForm to: fileName
    "Write Form to PNG file"
    ^self new write: aForm to: fileName
```

**Output format by depth:**
- 1bpp Form → PNG type 3 (indexed, 2-color palette)
- 32bpp Form → PNG type 6 (RGBA)

**Initial compression**: Use stored blocks (BTYPE=00) - no compression but valid PNG.

**Validation:**
- [ ] Written PNGs open in external tools (GIMP, etc.)
- [ ] Round-trip: `Form readFrom: 'test.png'; writeOn: 'test2.png'` preserves data

---

### Phase 3.4: Deflate Compression (Optional Enhancement)

**Files to modify:**
- `DeflateStream` in Smalltalk-80.st

**Add compression methods:**

```smalltalk
DeflateStream>>deflate: aByteArray
    "Compress using DEFLATE. Start with stored blocks."
    <primitive: 'primitiveDeflateDeflate'>
    ^self deflateStoredBlocks: aByteArray  "Fallback: no compression"
```

**Phases:**
1. Stored blocks only (no compression) - immediate
2. Fixed Huffman codes - ~60% compression
3. Dynamic Huffman - best compression (optional)

**Validation:**
- [ ] PNG files with compression open in external tools
- [ ] File sizes reasonable for image content

---

### MILESTONE 3 CHECKPOINT

- [ ] Read any standard PNG file
- [ ] Write PNG files readable by external tools
- [ ] Round-trip 1bpp and 32bpp Forms
- [ ] Legacy .form files can be migrated to PNG

---

## MILESTONE 4: Integration

**Goal**: Tie everything together, update Form API.

### Phase 4.1: Unified Form File API

**Files to modify:**
- `Smalltalk-80 Source/Smalltalk-80.st`

**Update Form class methods:**

```smalltalk
Form class>>readFrom: fileName
    "Read Form from file, dispatch by extension"
    | extension |
    extension := fileName asLowercase.
    (extension endsWith: '.form') ifTrue: [
        ^self readLegacyFormFile: fileName].
    (extension endsWith: '.png') ifTrue: [
        ^PNGReader readFrom: fileName].
    self error: 'Unknown image format'

Form>>writeOnFileNamed: fileName
    "Write Form to PNG (preferred format)"
    | pngName |
    pngName := (fileName endsWith: '.png')
        ifTrue: [fileName]
        ifFalse: [fileName, '.png'].
    PNGWriter write: self to: pngName

Form>>writeLegacyFormFile: fileName
    "Write in legacy 16-bit .form format (for compatibility)"
    "... conversion code ..."
```

**Validation:**
- [ ] `Form readFrom: 'image.png'` works
- [ ] `aForm writeOnFileNamed: 'output'` creates PNG
- [ ] Legacy `.form` files still readable

---

### MILESTONE 4 CHECKPOINT (Final)

**Full system test:**

```bash
cd Smalltalk80.Interpreter
dotnet build
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191
```

**Additional validation:**
- [ ] All 286 init expressions pass
- [ ] System Browser fully functional
- [ ] 32bpp Forms can be created and displayed
- [ ] PNG files can be read and written
- [ ] Legacy .form files still work (read-only)
- [ ] No performance regression in 1bpp mode

---

## Summary: Implementation Order

| Phase | Scope | Est. Lines | Dependencies |
|-------|-------|------------|--------------|
| **1.1** | HeapManager | ~20 | None |
| **1.2** | Float primitives | ~30 | 1.1 |
| **1.3** | BitBltEngine | ~50 | 1.1 |
| **1.4** | Font bridges | ~40 | 1.1 |
| **1.5** | Display creation | ~10 | 1.1 |
| **1.6** | Smalltalk Form | ~20 | 1.1 |
| **1.7** | File I/O compat | ~80 | 1.6 |
| **---** | **MILESTONE 1 COMPLETE** | | |
| **2.1** | Color class | ✅ Done | None |
| **2.2** | Form depth | ~60 | 1.6 |
| **2.3** | BitBltEngine32 | ~400 | 2.2 |
| **2.4** | Depth dispatch | ~50 | 2.3 |
| **2.5** | Depth conversion | ~80 | 2.4 |
| **---** | **MILESTONE 2 COMPLETE** | | |
| **3.1** | CRC32 + Deflate | ~400 | 1.1 |
| **3.2** | PNG Reader | ~200 | 3.1 |
| **3.3** | PNG Writer | ~150 | 3.1 |
| **3.4** | Deflate compression | ~300 | 3.3 (optional) |
| **---** | **MILESTONE 3 COMPLETE** | | |
| **4.1** | Unified API | ~50 | 2.5, 3.3 |
| **---** | **MILESTONE 4 COMPLETE** | | |

**Total estimated new code**: ~1500-2000 lines

---

## Open Questions (Require Decisions)

### Word Migration
1. **Range validation**: Should `WordArray>>at:put:` validate values fit in 32 bits?
2. **LargeInteger**: Any impact on LargePositiveInteger word storage?

### PNG Implementation
3. **Deflate priority**: Start with stored blocks, or implement Huffman immediately?
4. **Interlacing**: Support Adam7 on read, or non-interlaced only?
5. **Form offset**: Store in tEXt chunk, or ignore on PNG export?

### 32bpp Graphics
6. **Premultiplied alpha**: Use for better blending performance?
7. **Cursor depth**: Support 32bpp cursors, or convert at display time?

---

## Rollback Plan

Each milestone can be rolled back independently:

**MILESTONE 1 rollback:**
- Revert HeapManager to `ushort[]`
- Revert all `+ 31 // 32` to `+ 15 // 16`

**MILESTONE 2 rollback:**
- Remove Form depth ivar
- Remove BitBltEngine32

**MILESTONE 3 rollback:**
- Remove PNG classes
- Revert Form file methods

---

## References

- `docs/Migration-to-32-bit-Words.md` - Detailed word migration plan
- `docs/Graphics-Architecture.md` - Graphics system architecture
- `docs/Form-File-Format.md` - Legacy .form format specification
- Blue Book Chapter 18: Graphics Kernel
- Blue Book Chapter 27: Object Memory
- [PNG Specification](http://www.libpng.org/pub/png/spec/1.2/PNG-Contents.html)
- [RFC 1951 - DEFLATE](https://datatracker.ietf.org/doc/html/rfc1951)
