# PR: Complete 32bpp Graphics Pipeline with Anti-Aliased Text

## Summary

This PR completes the 32bpp graphics pipeline, enabling full color support and anti-aliased text rendering in the Smalltalk-2026 interpreter. The work includes fixes for scroll artifacts, XOR caret display, font baseline alignment, and implementing proper paint mode blending for anti-aliased content.

## Key Changes

### 1. BitBlt32Engine Overlap Handling
- **Problem**: Scrolling list panes showed duplicate entries
- **Solution**: Detect `destY > sourceY` to determine copy direction (top-to-bottom vs bottom-to-top)
- **Files**: `BitBlt32Engine.cs`

### 2. Paint Mode (Rule 16) - Multiply Blend
- **Problem**: Original paint mode used OR operations designed for 1bpp where black=1. In 32bpp, black=0x000000, so OR produces wrong results and destroys anti-aliasing
- **Solution**: Implement multiply blend (`src * dest / 255` per channel) which correctly handles anti-aliased text on white background
- **Files**: `BitBlt32Engine.cs`, `CombinationRule.cs`, `GraphicsPrimitives.cs`

### 3. XOR Caret Fix
- **Problem**: Caret/cursor was invisible because XOR with black (0x000000) does nothing
- **Solution**: Invert source before XOR (`~source ^ dest`) to preserve 1bpp semantics
- **Files**: `BitBlt32Engine.cs`

### 4. Fixed-Pitch Font Alignment
- **Problem**: Individual character measurements returned varying widths, causing baseline misalignment (especially visible with lowercase 'a')
- **Solution**: Use uniform cell width from representative character ('M'), build linear xTable
- **Files**: `WinFormsFontBridge.cs`

### 5. 32bpp Font Rendering
- Explicit `Format32bppArgb` bitmap creation
- `TextRenderingHint.AntiAlias` for grayscale output
- Color preservation verified with blue text test

### 6. Window Title Update
- Changed from "Smalltalk-80" to "Smalltalk-2026"

## Technical Details

### Multiply Blend Algorithm
```csharp
// Rule 16: Anti-aliased text blending
// White (0xFFFFFF) = transparent, Black (0x000000) = opaque
private static uint BlendPaint(uint source, uint dest)
{
    if (source == WhitePixel) return dest;      // Fast path: transparent
    if (source == BlackPixel) return BlackPixel; // Fast path: opaque

    // Multiply blend: result = src * dst / 255
    int outR = (srcR * dstR) / 255;
    int outG = (srcG * dstG) / 255;
    int outB = (srcB * dstB) / 255;
    return (outR << 16) | (outG << 8) | outB;
}
```

### Why Multiply Blend?
For anti-aliased black text on white background:
- White source (255) → result = dest × 255/255 = dest (transparent)
- Black source (0) → result = dest × 0/255 = 0 (black)
- Gray source (128) → result = dest × 128/255 ≈ dest/2 (darkened)

This correctly applies anti-aliased edges while preserving colored text.

## Files Changed

| File | Lines | Description |
|------|-------|-------------|
| `BitBlt32Engine.cs` | +124/-61 | Overlap handling, multiply blend, XOR fix |
| `CombinationRule.cs` | +9/-1 | Added `Paint = 16` enum |
| `WinFormsFontBridge.cs` | +87/-87 | Uniform cell width, 32bpp format |
| `GraphicsPrimitives.cs` | +5/-1 | Accept rule 16 |
| `Program.cs` | +1/-1 | Window title |
| `CLAUDE.md` | Documentation | Phase 6 section |
| `SESSION-HANDOFF.md` | Documentation | Current status |

## Testing

- ✅ Build: 0 errors, 0 warnings
- ✅ System Browser: Opens and displays source code correctly
- ✅ Scrolling: No duplicate entries in list panes
- ✅ Caret: Visible when editing text
- ✅ Color preservation: Blue text renders correctly (test mode)
- ✅ Font alignment: All characters on same baseline

## Screenshots

The blue text in the screenshot confirms the 32bpp pipeline preserves colors end-to-end:
- Font rendered in blue by GDI+ → stored in 32bpp Form → BitBlt with paint mode → displayed in blue

## Remaining Work

1. Change font color back to black (currently blue for testing)
2. Polish selection highlight appearance (multiply blend with inverted selection looks unusual)

## Related Documentation

- `docs/SESSION-HANDOFF.md` - Detailed technical notes for next session
- `docs/Graphics-Architecture.md` - Graphics system design
- `docs/32-BIT-IMPLEMENTATION-PLAN.md` - Future word migration plan
