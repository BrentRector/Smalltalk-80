# 32bpp Everywhere Architecture

**Created**: 2026-01-02
**Status**: Planning
**Branch**: `claude/review-graphics-architecture-FssRQ`

---

## Overview

This document describes a redesigned graphics architecture where **all bitmaps are 32bpp internally**, including the Display. This replaces the previous approach of 1bpp internal storage with conversion to 32bpp at display time.

### Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Internal pixel format | 32-bit ARGB | Matches GPU textures, simplifies BitBlt |
| Display buffer | 32bpp | Direct transfer to Windows, GPU-ready |
| Monochrome mode | Black=0xFF000000, White=0xFFFFFFFF | Preserves UI appearance |
| Halftone patterns | 32bpp with B/W pixels | Same visual as 1bpp halftones |
| Single BitBlt engine | Yes | One code path for all operations |

### Benefits

1. **Simpler display pipeline** - No 1bpp→32bpp conversion
2. **Single BitBlt engine** - One code path, easier to maintain
3. **GPU-ready** - 32bpp data maps directly to GPU textures
4. **Future color support** - Just use different pixel values
5. **Simpler bit manipulation** - 1 pixel = 1 word, no bit packing

### Memory Impact

| Item | 1bpp | 32bpp | Increase |
|------|------|-------|----------|
| 800x600 Display | 60 KB | 1.92 MB | 32x |
| 16x16 Halftone | 64 bytes | 1 KB | 16x |
| Typical cursor | 64 bytes | 1 KB | 16x |

Acceptable for modern systems with gigabytes of RAM.

---

## Monochrome Mode

The existing Smalltalk-80 UI operates in monochrome. We preserve this by using:

```
Black pixel: 0xFF000000 (fully opaque black)
White pixel: 0xFFFFFFFF (fully opaque white)
```

All existing UI rendering (text, menus, windows, icons) continues to work identically - visually indistinguishable from 1bpp rendering.

### Halftone Patterns

Classic halftones (gray, lightGray, darkGray) are checkerboard patterns. In 32bpp:

```smalltalk
"Gray halftone: alternating black/white pixels"
0 to: 15 do: [:y |
    0 to: 15 do: [:x |
        bits at: y * 16 + x + 1 put:
            ((x + y) odd ifTrue: [BlackPixel] ifFalse: [WhitePixel])]].
```

This produces visually identical results to 1bpp halftones.

**Future enhancement**: Replace halftone patterns with solid gray colors (e.g., 0xFF808080) for smoother appearance.

---

## Architecture Changes

### 1. Form Allocation (Smalltalk)

**Before (1bpp):**
```smalltalk
"32 pixels packed per word"
bits _ WordArray new: (width + 31 // 32) * height.
```

**After (32bpp):**
```smalltalk
"1 pixel per word"
bits _ WordArray new: width * height.
```

### 2. Form Instance Variables

Add constants or class variables:

```smalltalk
Form class
    classVariableNames: 'BlackPixel WhitePixel OneBitForm'

Form class>>initialize
    BlackPixel := 16rFF000000.
    WhitePixel := 16rFFFFFFFF.
```

### 3. BitBlt Engine (C#)

**Before**: Complex bit-level operations
- Extract bits from words
- Handle word boundaries
- Shift and mask operations

**After**: Simple word operations
```csharp
// Each pixel is one uint - direct array access
uint srcPixel = sourcePixels[srcY * srcWidth + srcX];
uint dstPixel = destPixels[dstY * dstWidth + dstX];
uint result = ApplyRule(rule, srcPixel, dstPixel);
destPixels[dstY * dstWidth + dstX] = result;
```

### 4. Combination Rules

All 16 rules operate on full 32-bit values:

| Rule | Operation | 32bpp Implementation |
|------|-----------|---------------------|
| 0 | Clear | `0` |
| 1 | And | `src & dst` |
| 2 | AndReverse | `src & ~dst` |
| 3 | Copy | `src` |
| 4 | AndInverted | `~src & dst` |
| 5 | Noop | `dst` |
| 6 | Xor | `src ^ dst` |
| 7 | Or | `src \| dst` |
| 8 | Nor | `~(src \| dst)` |
| 9 | Equiv | `~(src ^ dst)` |
| 10 | Invert | `~dst` |
| 11 | OrReverse | `src \| ~dst` |
| 12 | CopyInverted | `~src` |
| 13 | OrInverted | `~src \| dst` |
| 14 | Nand | `~(src & dst)` |
| 15 | Set | `0xFFFFFFFF` |

**Note**: For monochrome mode, these produce correct results because black XOR white = white, etc.

### 5. Display Pipeline

**Before:**
```
Smalltalk BitBlt → 1bpp heap buffer → Convert to 32bpp → Windows
```

**After:**
```
Smalltalk BitBlt → 32bpp heap buffer → Direct copy → Windows
```

### 6. Font Rendering

Strike fonts are stored as 1bpp bitmaps. Options:

1. **Convert on load**: Expand to 32bpp when font is loaded
2. **Convert during scan**: Expand each glyph in CharacterScanner
3. **Hybrid**: Keep 1bpp source, cache 32bpp glyphs

Initial approach: Convert during CharacterScanner (primitive 103).

### 7. Cursors

Cursors become 32bpp with alpha:
- Cursor pixels: Black (0xFF000000) or White (0xFFFFFFFF)
- Mask defines transparency via alpha channel
- Transparent pixels: 0x00000000

---

## Implementation Phases

### Phase 1: Foundation
- [ ] Add BlackPixel/WhitePixel constants to Form class
- [ ] Create BitBlt32Engine.cs with all 16 rules
- [ ] Update Form allocation to `width * height` words

### Phase 2: Core Graphics
- [ ] Update GraphicsPrimitives.cs to use BitBlt32Engine
- [ ] Update ExtractFormData/WriteFormData for 32bpp
- [ ] Update halftone patterns (gray, lightGray, etc.)

### Phase 3: Display
- [ ] Update WinFormsDisplay for direct 32bpp transfer
- [ ] Remove 1bpp-to-32bpp conversion code
- [ ] Update InvalidateDisplayRegion

### Phase 4: Text and Cursors
- [ ] Update CharacterScanner for 32bpp font rendering
- [ ] Update cursor handling for 32bpp with alpha
- [ ] Test with System Browser

### Phase 5: Validation
- [ ] All init expressions pass
- [ ] System Browser works correctly
- [ ] Text renders properly
- [ ] Menus and popups work
- [ ] Mouse cursor displays correctly

---

## Files to Modify

### C# Files

| File | Changes |
|------|---------|
| `BitBltEngine.cs` | Rename/keep for reference, create BitBlt32Engine.cs |
| `GraphicsPrimitives.cs` | Use 32bpp engine, simplify ExtractFormData |
| `WinFormsDisplay.cs` | Direct 32bpp buffer, remove conversion |
| `WinFormsDisplayProvider.cs` | 32bpp back buffer |

### Smalltalk Files

| Location | Changes |
|----------|---------|
| Form class | Allocation, BlackPixel/WhitePixel, wordsPerRow |
| Form class methods | gray, lightGray, darkGray, black, white |
| Cursor class | 32bpp representation |
| DisplayScreen | Verify 32bpp compatibility |

---

## Compatibility Notes

### Legacy .form Files

Legacy 1bpp .form files need conversion on load:
- Each 1-bit becomes a 32-bit pixel
- 0 (white) → 0xFFFFFFFF
- 1 (black) → 0xFF000000

### Smalltalk Source Patterns

Some Form initialization uses literal 16-bit values:
```smalltalk
Form extent: 16@16 fromArray: #(16r5555 16rAAAA ...) offset: 0@0
```

These need conversion to 32bpp pixel arrays or updated initialization.

---

## Testing Strategy

1. **Unit tests**: BitBlt32Engine with all 16 rules
2. **Visual tests**: DisplayScreen initialize (BitBlt grid)
3. **Integration tests**: System Browser, Transcript
4. **Regression tests**: All 286 init expressions

---

## Future Enhancements

Once 32bpp infrastructure is stable:

1. **Solid gray colors**: Replace halftone patterns with true grays
2. **Color UI elements**: Colored selection highlights, icons
3. **Alpha blending**: Translucent windows, shadows
4. **PNG support**: Load/save color images
