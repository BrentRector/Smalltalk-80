# Native 32bpp Smalltalk Implementation with GPU Acceleration

**Date**: 2026-01-01
**Branch**: `claude/32bpp-display-graphics-RLW1k`
**Status**: Detailed Planning Complete

---

## Executive Summary

This document provides a comprehensive plan for migrating Smalltalk-80 from 1bpp monochrome graphics to native 32bpp ARGB with pluggable GPU-accelerated rendering backends.

### Current State
- Smalltalk Forms: 1bpp packed bits (16 pixels per word)
- Halftones: 16x16 dithering patterns simulating gray
- Display: C# intercepts primitives, renders to 32bpp buffer
- Limitation: Intermediate Forms (window backing stores) remain dithered

### Target State
- Smalltalk Forms: Selectable depth (1bpp or 32bpp)
- Colors: True ARGB colors via Color class
- Display: Native depth throughout graphics stack
- Backend: Pluggable GPU acceleration (OpenGL/Vulkan via Silk.NET)

---

## Part 1: Complete Audit of 1bpp Assumptions in Smalltalk Source

### Overview Statistics

| Class | Methods Affected | Severity |
|-------|------------------|----------|
| Form | 17 methods + 6 constants | CRITICAL |
| BitBlt | 3 methods | CRITICAL |
| DisplayScreen | 2 methods | CRITICAL |
| Cursor | 2 methods + init | HIGH |
| DisplayMedium | 6 methods | MEDIUM |
| StrikeFont | 3 methods | MEDIUM |
| CharacterScanner | 3 methods | MEDIUM |
| Pen | 4 methods | LOW |
| **TOTAL** | **47+ locations** | |

---

## Part 2: Detailed Method-by-Method Audit

### 2.1 CLASS: Form (Lines 19546-20400)

The Form class is the **most critical** for 32bpp migration. It stores all bitmap data.

#### Instance Variable Layout
```smalltalk
Form instanceVariableNames: 'bits width height offset'
```

**Required Change**: Add `depth` instance variable:
```smalltalk
Form instanceVariableNames: 'bits width height offset depth'
```

#### CRITICAL: Bitmap Size Calculations

| Method | Line | Current Code | 1bpp Assumption | 32bpp Change |
|--------|------|--------------|-----------------|--------------|
| `extent:` | 19580 | `extentSize _ nonZeroExtent y * (nonZeroExtent x + 15 // 16)` | 16 pixels per word | `depth = 32 ifTrue: [extentSize _ height * width * 2] ifFalse: [extentSize _ height * (width + 15 // 16)]` |
| `extent:offset:` | 19594 | `bits: (WordArray new: normalizedPoint y * (normalizedPoint x + 15 // 16))` | 16 pixels per word | Same as above |
| `setExtent:fromArray:setOffset:` | 20206 | `bits _ WordArray new: width + 15 // 16 * height` | 16 pixels per word | Depth-aware allocation |
| `setExtent:fromCompactArray:setOffset:` | 20214 | `bits _ WordArray new: width + 15 // 16 * height` | 16 pixels per word | Depth-aware allocation |

**New Method Required**:
```smalltalk
Form>>wordsPerLine
    "Answer number of 16-bit words per scanline based on depth"
    depth = 32 ifTrue: [^width * 2].  "2 words per 32-bit pixel"
    depth = 8 ifTrue: [^(width + 1) // 2].
    ^(width + 15) // 16  "1bpp default"

Form>>extent: aPoint depth: bitsPerPixel
    | wordsPerLine |
    depth _ bitsPerPixel.
    wordsPerLine _ self wordsPerLine.
    bits _ WordArray new: aPoint y * wordsPerLine.
    width _ aPoint x.
    height _ aPoint y.
```

#### File I/O Methods

| Method | Line | Current Code | Change Required |
|--------|------|--------------|-----------------|
| `writeOn:` | 20145 | `1 to: width + 15 // 16 do:` | `1 to: self wordsPerLine do:` |
| `readFormFile:` | 20329 | `theBits _ WordArray new: newWidth + 15 // 16 * newHeight` | Add depth parameter to file format |
| `readFormFile:` | 20331 | `1 to: newWidth + 15 // 16 do:` | `1 to: wordsPerLine do:` |
| `stringScanLineOfWidth:` | 20349 | `width+15//16*2` | `self wordsPerLine * 2` |

#### Pixel Access Methods

| Method | Line | Current Code | Change Required |
|--------|------|--------------|-----------------|
| `valueAt:` | 19696-19708 | `(OneBitForm bits at: 1) = 0` | Depth-aware pixel read |
| `valueAt:put:` | 19709-19722 | `maskCode = 0` / `maskCode ~= 0` | Depth-aware pixel write |
| `isAllWhite` | 20195-20201 | `count _ bits countBits` | Check all pixels = 0xFFFFFFFF |

**New Pixel Access for 32bpp**:
```smalltalk
Form>>pixelAt: aPoint
    "Answer the 32-bit ARGB value at aPoint"
    | wordIndex |
    depth = 32 ifFalse: [^self valueAt: aPoint].
    wordIndex _ (aPoint y * width + aPoint x) * 2 + 1.
    ^(bits at: wordIndex) << 16 + (bits at: wordIndex + 1)

Form>>pixelAt: aPoint put: argbValue
    "Store 32-bit ARGB value at aPoint"
    | wordIndex |
    depth = 32 ifFalse: [^self valueAt: aPoint put: argbValue].
    wordIndex _ (aPoint y * width + aPoint x) * 2 + 1.
    bits at: wordIndex put: (argbValue >> 16) bitAnd: 16rFFFF.
    bits at: wordIndex + 1 put: argbValue bitAnd: 16rFFFF.
```

#### Halftone Mask Constants (Class Side)

| Method | Line | Current Pattern | 32bpp Equivalent |
|--------|------|-----------------|------------------|
| `black` | 20372 | 16x16 all-ones pattern (65535) | Solid 0xFF000000 |
| `white` | 20389 | 16x16 all-zeros pattern | Solid 0xFFFFFFFF |
| `gray` | 20379 | 21845/43690 alternating (50% dither) | Solid 0xFF808080 |
| `darkGray` | 20375 | 30583/56797 pattern (~75% black) | Solid 0xFF404040 |
| `lightGray` | 20381 | Light dither pattern (~25% black) | Solid 0xFFC0C0C0 |
| `veryLightGray` | 20385 | Very light pattern (~12% black) | Solid 0xFFE0E0E0 |

**Implementation**:
```smalltalk
Form class>>grayColor: intensity
    "Answer a 1x1 32bpp Form with the given gray intensity (0.0-1.0)"
    | argb form |
    argb _ (255 * (1.0 - intensity)) asInteger.
    argb _ 16rFF000000 bitOr: (argb << 16) bitOr: (argb << 8) bitOr: argb.
    form _ Form extent: 1@1 depth: 32.
    form pixelAt: 0@0 put: argb.
    ^form

Form class>>gray32
    ^self grayColor: 0.5

Form class>>black32
    ^self grayColor: 1.0
```

#### Scaling Methods

| Method | Line | Current Behavior | 32bpp Change |
|--------|------|------------------|--------------|
| `magnifyBy:` | 19815-19835 | Bit replication | Pixel replication (works similarly) |
| `shrinkBy:` | 20016-20042 | Sample single bit | Average RGB values for anti-aliasing |
| `spread:from:by:spacing:direction:` | 20043-20061 | Bit spreading | Pixel spreading |

---

### 2.2 CLASS: BitBlt (Lines 5412-5650)

BitBlt is the core graphics operation. The primitive (96) handles actual bit manipulation.

#### Instance Variables
```smalltalk
BitBlt instanceVariableNames: 'destForm sourceForm halftoneForm combinationRule
    destX destY width height sourceX sourceY clipX clipY clipWidth clipHeight'
```

**No change needed** - BitBlt operates on Forms; depth awareness comes from Form.

#### Methods Requiring Changes

| Method | Line | Current Behavior | 32bpp Change |
|--------|------|------------------|--------------|
| `copyBits` | 5521-5538 | `<primitive: 96>` - 1bpp operations | Primitive must detect Form depth |
| `paintBits` | 5600-5614 | Halftone masking with AND/OR | Color blending for 32bpp |
| `drawLoopX:Y:` | 5555-5592 | `<primitive: 104>` - Bresenham at 1bpp | Pixel writes become 32-bit |

#### Combination Rules

The 16 combination rules have different semantics in 32bpp:

| Rule | 1bpp Meaning | 32bpp Meaning |
|------|--------------|---------------|
| 0 (Clear) | dest _ 0 | dest _ 0xFF000000 (black) |
| 3 (Copy) | dest _ src | dest _ src (ARGB copy) |
| 6 (Xor) | dest _ src XOR dest | Per-component XOR (for selection) |
| 7 (Or) | dest _ src OR dest | Per-component OR (paint) |
| 10 (Invert) | dest _ NOT dest | dest _ 0xFFFFFFFF - dest (invert RGB) |
| 15 (Set) | dest _ 1 | dest _ 0xFFFFFFFF (white) |

**Primitive 96 Changes** (C# side):
```csharp
// Read depth from Form instance variable
int sourceDepth = GetFormDepth(sourceForm);
int destDepth = GetFormDepth(destForm);

// Dispatch based on depth combination
(sourceDepth, destDepth) switch
{
    (1, 1) => BitBltEngine.CopyBits(...),      // Current 1bpp engine
    (32, 32) => BitBltEngine32.CopyBits32(...), // 32bpp pixel ops
    (1, 32) => BitBltEngine32.CopyBits1To32(...), // Expand bits to pixels
    (32, 1) => BitBltEngine32.CopyBits32To1(...), // Threshold/dither
};
```

---

### 2.3 CLASS: DisplayScreen (Lines 16844-16920)

DisplayScreen is the visible display surface.

#### Methods Requiring Changes

| Method | Line | Current Code | 32bpp Change |
|--------|------|--------------|--------------|
| `displayExtent:` | 16889 | `w _ extent x truncateTo: 32` | Keep for alignment |
| `displayExtent:` | 16899 | `bits: (DisplayBitmap new: w // 16 * 16)` | `bits: (DisplayBitmap new: w * h * 2)` for 32bpp |
| `displayExtent:` | 16905 | `bits: (DisplayBitmap new: w // 16 * h)` | Add depth parameter |
| `beDisplay` | 16859 | `<primitive: 102>` | Primitive reads depth from Form |

**New Method**:
```smalltalk
DisplayScreen class>>displayExtent: extent depth: bitsPerPixel
    | w h newDisplay |
    w _ extent x truncateTo: 32.
    h _ extent y truncateTo: 2.
    newDisplay _ self new.
    newDisplay setExtent: w@h depth: bitsPerPixel.
    Display _ newDisplay.
    Display beDisplay.
    ^Display

DisplayScreen class>>preferredDepth
    "Answer the preferred display depth. Override for 32bpp."
    ^32  "or ^1 for traditional"
```

---

### 2.4 CLASS: Cursor (Lines 14105-14500)

Cursors are 16x16 bitmaps with a hotspot.

#### Current Representation
- 16x16 = 256 pixels = 16 words (1bpp)
- Each cursor defined with 16 binary values

#### 32bpp Change
- 16x16 = 256 pixels = 512 words (32bpp, 2 words per pixel)
- OR keep cursors 1bpp and convert at display time (simpler)

| Method | Line | Current | 32bpp Change |
|--------|------|---------|--------------|
| `extent:fromArray:offset:` | 14445 | `extentPoint = (16 @ 16)` check | Keep 16x16 check |
| `initialize` | 14155-14438 | 16 binary values per cursor | Keep 1bpp, convert in primitive 101 |

**Recommendation**: Keep cursors 1bpp internally. Primitive 101 (`beCursor`) converts to 32bpp for display.

---

### 2.5 CLASS: DisplayMedium (Lines 16299-16400)

DisplayMedium provides convenience methods for filling with colors.

#### Methods Using 1bpp Masks

| Method | Line | Current Code | 32bpp Change |
|--------|------|--------------|--------------|
| `black` | 16313 | `mask: Form black` | `mask: Form black32` when dest is 32bpp |
| `darkGray` | 16320 | `mask: Form darkGray` | `mask: Form darkGray32` |
| `gray` | 16338 | `mask: Form gray` | `mask: Form gray32` |
| `lightGray` | 16345 | `mask: Form lightGray` | `mask: Form lightGray32` |
| `veryLightGray` | 16368 | `mask: Form veryLightGray` | `mask: Form veryLightGray32` |
| `reverse` | 16353 | `rule: Form reverse mask: Form black` | XOR with 0xFFFFFF for 32bpp |

**Depth-Aware Implementation**:
```smalltalk
DisplayMedium>>gray
    "Fill receiver with gray"
    self depth = 32
        ifTrue: [self fill: self boundingBox rule: Form over mask: Form gray32]
        ifFalse: [self fill: self boundingBox rule: Form over mask: Form gray]
```

---

### 2.6 CLASS: StrikeFont (Lines 36346-36600)

StrikeFont stores character glyphs as a single large 1bpp Form.

#### Current Structure
```smalltalk
StrikeFont instanceVariableNames: 'glyphs xTable characterWidth height ascent descent name'
```

- `glyphs`: Form containing all characters side-by-side (1bpp)
- `xTable`: Array of x-offsets for each character

#### 32bpp Options

**Option A: Keep fonts 1bpp, convert on render** (Recommended)
- Font glyphs remain 1bpp
- CharacterScanner/BitBlt converts to text color when drawing
- Simple, preserves existing fonts

**Option B: 32bpp antialiased fonts**
- Store glyphs with alpha channel for antialiasing
- Requires new font format/loader
- Better quality, more complex

| Method | Line | Current | Change for Option A |
|--------|------|---------|---------------------|
| `characterForm:` | 36387-36403 | Copies from 1bpp glyphs | No change needed |
| `characterForm:` | 36401 | `mask: Form black` | Text color applied by BitBlt 1→32 conversion |

---

### 2.7 CLASS: CharacterScanner (Lines 9445-9600)

CharacterScanner renders text using BitBlt.

#### Methods

| Method | Line | Current | 32bpp Change |
|--------|------|---------|--------------|
| `setStopConditions` | 9551 | `sourceForm _ font glyphs` | Font glyphs stay 1bpp |
| `scanCharactersFrom:to:...` | 9531 | `<primitive: 103>` | Primitive handles 1→32 conversion |
| `initializeFromParagraph:` | 9573 | `halftoneForm _ aParagraph mask` | Mask becomes text color |

**Primitive 103 Change**: When destination is 32bpp, convert source glyph bits to text color.

---

### 2.8 CLASS: Pen (Lines 31371-31600)

Pen draws lines and curves using BitBlt.

#### Methods

| Method | Line | Current | 32bpp Change |
|--------|------|---------|--------------|
| `mask: Form black` | 31402-31404 | Uses 1bpp mask | Use Color for pen color |
| `mask: Form white` | 31407-31409 | Uses 1bpp mask | Use Color for pen color |
| `new` | 31594 | `quill mask: Form black` | `quill color: Color black` |

**New Protocol**:
```smalltalk
Pen>>color: aColor
    "Set pen drawing color"
    destForm depth = 32
        ifTrue: [self halftoneForm: aColor asForm32]
        ifFalse: [self halftoneForm: aColor asHalftone]
```

---

## Part 3: New Color Class

### Class Definition

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'red green blue alpha'
    classVariableNames: 'Black White Gray DarkGray LightGray Transparent'
    poolDictionaries: ''
    category: 'Graphics-Primitives'

"Instance creation"
Color class>>r: r g: g b: b
    ^self new setRed: r green: g blue: b alpha: 255

Color class>>r: r g: g b: b a: a
    ^self new setRed: r green: g blue: b alpha: a

Color class>>gray: brightness
    "0.0 = black, 1.0 = white"
    | v |
    v _ (brightness * 255) asInteger.
    ^self r: v g: v b: v

Color class>>fromARGB: argbInteger
    ^self new
        setRed: (argbInteger >> 16) bitAnd: 255
        green: (argbInteger >> 8) bitAnd: 255
        blue: argbInteger bitAnd: 255
        alpha: (argbInteger >> 24) bitAnd: 255

"Conversion"
Color>>asARGB
    "Answer 32-bit ARGB integer"
    ^(alpha << 24) + (red << 16) + (green << 8) + blue

Color>>asForm32
    "Answer 1x1 32bpp Form with this color"
    | form |
    form _ Form extent: 1@1 depth: 32.
    form pixelAt: 0@0 put: self asARGB.
    ^form

Color>>asHalftone
    "Answer 16x16 1bpp halftone approximating this color"
    | brightness |
    brightness _ (red + green + blue) / 3.0 / 255.0.
    brightness < 0.125 ifTrue: [^Form black].
    brightness < 0.375 ifTrue: [^Form darkGray].
    brightness < 0.625 ifTrue: [^Form gray].
    brightness < 0.875 ifTrue: [^Form lightGray].
    ^Form white

"Standard colors"
Color class>>black
    ^Black ifNil: [Black _ self r: 0 g: 0 b: 0]

Color class>>white
    ^White ifNil: [White _ self r: 255 g: 255 b: 255]

Color class>>gray
    ^Gray ifNil: [Gray _ self r: 128 g: 128 b: 128]

Color class>>red
    ^self r: 255 g: 0 b: 0

Color class>>green
    ^self r: 0 g: 255 b: 0

Color class>>blue
    ^self r: 0 g: 0 b: 255

Color class>>transparent
    ^Transparent ifNil: [Transparent _ self r: 0 g: 0 b: 0 a: 0]
```

---

## Part 4: C# Primitive Changes

### Primitive 96 (copyBits)

```csharp
// GraphicsPrimitives.cs - Depth-aware BitBlt

private SmalltalkOOP Primitive96_CopyBits(PrimitiveContext context)
{
    // Extract BitBlt parameters...

    // Get Form depths
    int sourceDepth = GetFormDepth(context.Heap, sourceForm);
    int destDepth = GetFormDepth(context.Heap, destForm);
    int halftoneDepth = GetFormDepth(context.Heap, halftoneForm);

    // Dispatch based on depth combination
    switch ((sourceDepth, destDepth))
    {
        case (1, 1):
            BitBltEngine.CopyBits(...);  // Current implementation
            break;

        case (32, 32):
            BitBltEngine32.CopyBits32To32(...);
            break;

        case (1, 32):
            // Expand 1bpp source to 32bpp dest
            // Use halftone color or black/white
            uint foreground = halftoneDepth == 32
                ? GetPixel32(halftoneForm, 0, 0)
                : 0xFF000000;
            uint background = 0xFFFFFFFF;
            BitBltEngine32.CopyBits1To32(
                ..., foreground, background, combinationRule);
            break;

        case (32, 1):
            // Threshold 32bpp to 1bpp (for compatibility)
            BitBltEngine32.CopyBits32To1(..., threshold: 128);
            break;
    }

    return context.Metadata.KnownObjects.Nil;
}

private int GetFormDepth(ISmalltalkHeap heap, SmalltalkOOP formOOP)
{
    if (formOOP.IsNil) return 0;

    // Depth is ivar index 4 (bits, width, height, offset, depth)
    var depthOOP = heap.GetInstanceVariable(formOOP, 4);
    if (depthOOP.IsNil || !depthOOP.IsSmallInteger)
        return 1;  // Default 1bpp for backward compatibility

    return depthOOP.IsSmallInteger ? depthOOP.AsSmallInteger() : 1;
}
```

### Primitive 102 (beDisplay)

```csharp
private SmalltalkOOP Primitive102_BeDisplay(PrimitiveContext context)
{
    var displayForm = context.Receiver;

    // Get dimensions
    int width = GetFormWidth(displayForm);
    int height = GetFormHeight(displayForm);
    int depth = GetFormDepth(context.Heap, displayForm);

    // Configure display for this depth
    context.Display?.ConfigureDepth(depth);
    context.Display?.Resize(width, height);

    // Store Display OOP for later reference
    context.Metadata.KnownObjects.Display = displayForm;

    return displayForm;
}
```

### Primitive 103 (scanCharacters)

```csharp
// When scanning to 32bpp destination with 1bpp font glyphs:
// Convert font bits to text color

private void RenderGlyph1To32(
    Span<byte> dest32, int destWidth,
    ReadOnlySpan<byte> glyph1, int glyphWidth, int glyphHeight,
    int destX, int destY,
    uint textColor)
{
    // For each set bit in glyph, write textColor to dest
    for (int y = 0; y < glyphHeight; y++)
    {
        for (int x = 0; x < glyphWidth; x++)
        {
            if (GetBit1(glyph1, glyphWidth, x, y))
            {
                SetPixel32(dest32, destWidth, destX + x, destY + y, textColor);
            }
        }
    }
}
```

---

## Part 5: GPU Acceleration Architecture

### IDisplay Interface for GPU Backends

```csharp
public interface IDisplay : IDisposable
{
    // Existing members...

    // GPU-specific additions

    /// <summary>
    /// Create a GPU texture from Smalltalk Form data.
    /// </summary>
    IGpuTexture CreateTexture(int width, int height, int depth, ReadOnlySpan<byte> data);

    /// <summary>
    /// Perform BitBlt on GPU.
    /// </summary>
    void GpuBitBlt(IGpuTexture dest, IGpuTexture source, IGpuTexture halftone,
        int destX, int destY, int width, int height,
        int sourceX, int sourceY, CombinationRule rule);

    /// <summary>
    /// Upload modified Form bits to GPU texture.
    /// </summary>
    void UpdateTexture(IGpuTexture texture, ReadOnlySpan<byte> data);

    /// <summary>
    /// Download GPU texture to Smalltalk Form bits.
    /// </summary>
    void DownloadTexture(IGpuTexture texture, Span<byte> data);
}

public interface IGpuTexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    int Depth { get; }  // 1, 8, or 32
}
```

### Silk.NET Implementation Sketch

```csharp
public class SilkNetDisplay : IDisplay
{
    private IWindow _window;
    private GL _gl;
    private Dictionary<SmalltalkOOP, GpuTexture> _textureCache;

    public void GpuBitBlt(IGpuTexture dest, IGpuTexture source, ...)
    {
        // Use compute shader for BitBlt combination rules
        _gl.UseProgram(_bitBltShader);
        _gl.BindImageTexture(0, dest.Handle, ...);
        _gl.BindImageTexture(1, source.Handle, ...);
        _gl.Uniform1i(_ruleLoc, (int)rule);
        _gl.DispatchCompute(width / 16, height / 16, 1);
    }
}
```

### BitBlt Compute Shader

```glsl
#version 450

layout(local_size_x = 16, local_size_y = 16) in;

layout(rgba8, binding = 0) uniform image2D destImage;
layout(rgba8, binding = 1) uniform readonly image2D sourceImage;
uniform int combinationRule;
uniform ivec4 destRect;  // x, y, width, height
uniform ivec2 sourceOffset;

void main() {
    ivec2 gid = ivec2(gl_GlobalInvocationID.xy);
    if (gid.x >= destRect.z || gid.y >= destRect.w) return;

    ivec2 destPos = destRect.xy + gid;
    ivec2 srcPos = sourceOffset + gid;

    vec4 src = imageLoad(sourceImage, srcPos);
    vec4 dst = imageLoad(destImage, destPos);
    vec4 result;

    switch (combinationRule) {
        case 0:  result = vec4(0, 0, 0, 1); break;  // Clear (black)
        case 3:  result = src; break;               // Copy
        case 6:  result = vec4(abs(src.rgb - dst.rgb), 1); break;  // XOR approximation
        case 7:  result = max(src, dst); break;     // Or
        case 10: result = vec4(1.0 - dst.rgb, 1); break;  // Invert
        case 15: result = vec4(1, 1, 1, 1); break;  // Set (white)
        default: result = dst; break;
    }

    imageStore(destImage, destPos, result);
}
```

---

## Part 6: Migration Strategy

### Phase 1: Form Depth Infrastructure (Low Risk)

**Smalltalk Changes:**
1. Add `depth` instance variable to Form
2. Add `Form>>depth`, `Form>>depth:` accessors
3. Add `Form class>>extent:depth:` creation method
4. Default depth = 1 for backward compatibility

**C# Changes:**
1. Primitive 96 reads depth from Form ivar
2. Dispatch to appropriate BitBlt engine

**Test Criteria:**
- All existing code continues to work (depth defaults to 1)
- Can create 32bpp Form programmatically

### Phase 2: DisplayScreen at 32bpp (Medium Risk)

**Smalltalk Changes:**
1. Add `DisplayScreen class>>displayExtent:depth:`
2. System startup uses 32bpp Display

**C# Changes:**
1. Primitive 102 configures display for depth
2. WinFormsDisplay handles 32bpp buffer natively

**Test Criteria:**
- System Browser displays correctly
- No dithering artifacts
- Selection highlighting works

### Phase 3: Color Class (Low Risk)

**Smalltalk Changes:**
1. Implement Color class as specified above
2. Add `Color>>asForm32`, `Color>>asHalftone`
3. Backward compatible with existing halftone usage

**Test Criteria:**
- `Color gray asForm32` creates valid 32bpp Form
- `Color red asHalftone` returns approximation

### Phase 4: Widget Integration (Medium Risk)

**Smalltalk Changes:**
1. Update DisplayMedium convenience methods
2. Update Pen to use Color
3. Update ScrollBar, Button, etc.

**Test Criteria:**
- UI widgets render correctly in 32bpp
- No visual regressions

### Phase 5: GPU Backend (High Risk, Optional)

**C# Changes:**
1. Implement SilkNetDisplay : IDisplay
2. BitBlt compute shaders
3. Texture caching

**Test Criteria:**
- Performance improvement over software
- Visual parity with software renderer

---

## Part 7: File Change Summary

### Smalltalk Files to Modify

| File | Changes |
|------|---------|
| `Smalltalk-80.st` | Form class (add depth), Color class (new), DisplayScreen, DisplayMedium, Pen |

### C# Files to Modify

| File | Changes |
|------|---------|
| `GraphicsPrimitives.cs` | Depth-aware primitives 96, 102, 103 |
| `BitBltEngine.cs` | No change (1bpp engine) |
| `BitBltEngine32.cs` | Add 32→32, 1→32, 32→1 operations |
| `VMConstants.cs` | Form ivar indices (add depth at index 4) |
| `ColdStartPhases.cs` | Form class with 5 ivars |
| `IDisplay.cs` | GPU texture interface |
| `WinFormsDisplay.cs` | Native 32bpp support |

### New C# Files

| File | Purpose |
|------|---------|
| `SilkNetDisplay.cs` | GPU-accelerated display backend |
| `GpuTexture.cs` | GPU texture wrapper |
| `BitBltShader.glsl` | Compute shader for GPU BitBlt |

---

## Part 8: Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking existing 1bpp code | Medium | High | Default depth=1, extensive testing |
| Performance regression | Low | Medium | Profile, optimize hot paths |
| GPU driver issues | Medium | Medium | Software fallback always available |
| Font rendering issues | Low | Medium | Keep fonts 1bpp, convert on render |
| File format incompatibility | Low | Low | Version file format, converters |

---

## Appendix A: Complete Method Index

### Form Class Methods Requiring Changes

1. `Form>>extent:` (Line 19570)
2. `Form>>extent:offset:` (Line 19584)
3. `Form>>setExtent:fromArray:setOffset:` (Line 20202)
4. `Form>>setExtent:fromCompactArray:setOffset:` (Line 20209)
5. `Form>>writeOn:` (Line 20130)
6. `Form>>readFormFile:` (Line 20315)
7. `Form>>stringScanLineOfWidth:` (Line 20348)
8. `Form>>isAllWhite` (Line 20195)
9. `Form>>valueAt:` (Line 19696)
10. `Form>>valueAt:put:` (Line 19709)
11. `Form>>magnifyBy:` (Line 19815)
12. `Form>>shrinkBy:` (Line 20016)
13. `Form>>spread:from:by:spacing:direction:` (Line 20043)
14. `Form class>>initializeMasks` (Line 20233)
15. `Form class>>black` (Line 20372)
16. `Form class>>white` (Line 20389)
17. `Form class>>gray` (Line 20379)
18. `Form class>>darkGray` (Line 20375)
19. `Form class>>lightGray` (Line 20381)
20. `Form class>>veryLightGray` (Line 20385)

### BitBlt Class Methods Requiring Changes

1. `BitBlt>>copyBits` (Line 5521)
2. `BitBlt>>paintBits` (Line 5600)
3. `BitBlt>>drawLoopX:Y:` (Line 5555)

### DisplayScreen Class Methods Requiring Changes

1. `DisplayScreen class>>displayExtent:` (Line 16889)
2. `DisplayScreen>>beDisplay` (Line 16854)

### Other Classes

1. `Cursor>>extent:fromArray:offset:` (Line 14441)
2. `DisplayMedium>>black` (Line 16309)
3. `DisplayMedium>>darkGray` (Line 16317)
4. `DisplayMedium>>gray` (Line 16335)
5. `DisplayMedium>>lightGray` (Line 16342)
6. `DisplayMedium>>veryLightGray` (Line 16365)
7. `DisplayMedium>>reverse` (Line 16349)
8. `StrikeFont>>characterForm:` (Line 36387)
9. `CharacterScanner>>setStopConditions` (Line 9547)
10. `CharacterScanner>>scanCharactersFrom:to:...` (Line 9526)
11. `CharacterScanner>>initializeFromParagraph:` (Line 9568)
12. `Pen>>mask: Form black` (Line 31402)
13. `Pen>>mask: Form white` (Line 31407)
14. `Pen class>>new` (Line 31588)

---

## Part 9: Why Skip 8-bit and Go Direct to 32-bit

### Analysis: 8-bit vs 32-bit Migration Path

**8-bit Adds Complexity That 32-bit Doesn't Need:**

| Feature | 8-bit (Indexed) | 32-bit (Direct RGB) |
|---------|-----------------|---------------------|
| Color storage | Palette index | Direct ARGB value |
| Palette management | Required | Not needed |
| Color mapping tables | Required for depth conversion | Not needed |
| ColorForm subclass | Required | Not needed |
| Pixel alignment | Awkward (8 pixels ≠ word) | Perfect (1 pixel = 2 words) |
| Alpha blending | Complex | Native |

**32-bit Is Actually Simpler:**
```
8-bit pixel:  [palette index 0-255] → lookup → actual color
32-bit pixel: [A][R][G][B] → direct color value
```

**Memory Comparison:**

| Resolution | 1-bit | 8-bit | 32-bit | Modern RAM |
|------------|-------|-------|--------|------------|
| 800×600 | 59 KB | 469 KB | 1.8 MB | Trivial |
| 1920×1080 | 253 KB | 2.0 MB | 7.9 MB | Trivial |
| 4K (3840×2160) | 1.0 MB | 7.9 MB | 31.6 MB | Acceptable |

**Recommendation**: Skip 8-bit entirely. Modern Squeak/Pharo treat 8-bit as legacy - their default is 32-bit.

---

## Part 10: GPU Acceleration Architecture

### Current Architecture vs. GPU-Accelerated

| Current (Software) | GPU-Accelerated |
|--------------------|-----------------|
| CPU processes each pixel | GPU processes millions in parallel |
| BitBlt in C# loops | Hardware texture blit |
| Sequential combination rules | Shader-based blend modes |
| System RAM bandwidth limited | GPU VRAM + dedicated memory bus |

### GPU Library Options for .NET/Windows

#### Option 1: SkiaSharp (Recommended for Cross-Platform)

**Pros:**
- Cross-platform (Windows, macOS, Linux)
- GPU acceleration via SKGLControl
- Excellent 2D performance
- Well-maintained, large community
- Simple .NET integration

```csharp
// Replace BitBltEngine with SkiaSharp
using SkiaSharp;

public class SkiaDisplayProvider : IDisplayProvider
{
    private SKSurface _surface;
    private GRContext _grContext;  // GPU context

    public void Initialize(int width, int height)
    {
        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        _surface = SKSurface.Create(_grContext, false,
            new SKImageInfo(width, height, SKColorType.Bgra8888));
    }

    public void BitBlt(SKImage source, SKRect srcRect,
                       SKRect destRect, SKBlendMode blendMode)
    {
        var canvas = _surface.Canvas;
        using var paint = new SKPaint { BlendMode = blendMode };
        canvas.DrawImage(source, srcRect, destRect, paint);
    }
}
```

#### Option 2: Direct2D (Maximum Windows Performance)

**Pros:**
- Best Windows performance
- Native GPU acceleration
- Full alpha blending support
- d2dlib provides .NET wrapper

**Cons:**
- Windows-only
- More complex API

```csharp
using unvell.D2DLib;

public class Direct2DDisplayProvider : IDisplayProvider
{
    private D2DDevice _device;
    private D2DBitmap _displayBitmap;

    public void BitBlt(D2DBitmap source, D2DRect srcRect,
                       D2DRect destRect, D2DBitmapInterpolationMode mode)
    {
        _device.BeginRender();
        _device.DrawBitmap(source, destRect, 1.0f, mode, srcRect);
        _device.EndRender();
    }
}
```

#### Option 3: OpenGL via OpenTK (Cross-Platform, Maximum Control)

**Pros:**
- Cross-platform
- Direct GPU control
- Can implement custom shaders for combination rules

```csharp
// Forms become GPU textures
int textureId = GL.GenTexture();
GL.BindTexture(TextureTarget.Texture2D, textureId);
GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
    width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

// BitBlt becomes texture blit with blend mode
GL.Enable(EnableCap.Blend);
GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
DrawTexturedQuad(sourceTexture, destRect);
```

### Mapping Smalltalk Concepts to GPU

| Smalltalk | GPU Equivalent |
|-----------|----------------|
| Form | Texture / Render Target |
| Form bits | Texture pixel data |
| DisplayScreen | Framebuffer / Backbuffer |
| BitBlt copy | Texture blit |
| Combination rules 0-15 | Blend modes |
| Rule 24 (alphaBlend) | SrcAlpha/OneMinusSrcAlpha |
| Halftone pattern | Pattern texture / shader |
| Clipping rectangle | Scissor rect / stencil |

### Combination Rules → GPU Blend Modes

| Rule | Smalltalk | SkiaSharp | Direct2D | OpenGL |
|------|-----------|-----------|----------|--------|
| 0 | Clear | Clear | Clear | GL_ZERO |
| 3 | Copy | Src | SourceCopy | GL_ONE, GL_ZERO |
| 6 | XOR | Xor | Xor | Custom shader |
| 7 | OR | Lighten | — | GL_MAX |
| 24 | alphaBlend | SrcOver | SourceOver | SrcAlpha, 1-SrcAlpha |

### Performance Comparison (Estimated)

| Operation | Software (Current) | SkiaSharp GPU | Direct2D |
|-----------|-------------------|---------------|----------|
| Full screen BitBlt (1920×1080×32) | ~15ms | ~0.5ms | ~0.3ms |
| 1000 small blits | ~50ms | ~2ms | ~1ms |
| Alpha blend | ~25ms | ~0.5ms | ~0.3ms |
| Text rendering | ~10ms | ~1ms | ~0.8ms |

GPU acceleration provides **10-50× speedup** for graphics operations.

---

## Part 11: Graphics Abstraction Layer Design

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Smalltalk Layer                          │
│              Form, BitBlt, DisplayScreen                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   IGraphicsBackend                           │
│  BitBlt(), CreateTexture(), FillRect(), Present()           │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│   Software    │    │  SkiaSharp    │    │   Direct2D    │
│  (Complete)   │    │   (Future)    │    │   (Future)    │
└───────────────┘    └───────────────┘    └───────────────┘
```

### Core Interfaces

#### ITexture Interface

```csharp
public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    PixelDepth Depth { get; }

    // CPU data access
    ReadOnlySpan<byte> GetPixelData();
    void SetPixelData(ReadOnlySpan<byte> data);

    // Pixel operations
    uint GetPixel(int x, int y);
    void SetPixel(int x, int y, uint argb);

    // Dirty region tracking
    bool IsDirty { get; }
    void MarkDirty(GraphicsRect region);
    void ClearDirty();
}
```

#### IGraphicsBackend Interface

```csharp
public interface IGraphicsBackend : IDisposable
{
    string Name { get; }
    BackendCapabilities Capabilities { get; }

    // Texture management
    ITexture CreateTexture(int width, int height, PixelDepth depth);
    ITexture CreateTextureFromData(int w, int h, PixelDepth depth, ReadOnlySpan<byte> data);

    // Core operations
    void BitBlt(in BitBltParams p);
    void FillRect(ITexture dest, GraphicsRect rect, uint argb, BlendMode mode);
    void DrawLine(ITexture dest, int x1, int y1, int x2, int y2, uint color);

    // Display
    ITexture DisplayTexture { get; }
    void Present();
    void InvalidateRegion(GraphicsRect rect);

    // Stats
    BackendStats GetStats();
    void ResetStats();
}
```

#### BlendMode Enum

```csharp
public enum BlendMode : byte
{
    // Classic Smalltalk-80 combination rules (0-15)
    Clear = 0,          // Rule 0: Always 0
    And = 1,            // Rule 1: src AND dst
    AndReverse = 2,     // Rule 2: src AND (NOT dst)
    Copy = 3,           // Rule 3: src (normal copy)
    AndInverted = 4,    // Rule 4: (NOT src) AND dst
    Noop = 5,           // Rule 5: dst (no change)
    Xor = 6,            // Rule 6: src XOR dst
    Or = 7,             // Rule 7: src OR dst
    Nor = 8,            // Rule 8: NOT (src OR dst)
    Equiv = 9,          // Rule 9: NOT (src XOR dst)
    Invert = 10,        // Rule 10: NOT dst
    OrReverse = 11,     // Rule 11: src OR (NOT dst)
    CopyInverted = 12,  // Rule 12: NOT src
    OrInverted = 13,    // Rule 13: (NOT src) OR dst
    Nand = 14,          // Rule 14: NOT (src AND dst)
    Set = 15,           // Rule 15: Always 1

    // Modern extensions (16+)
    AlphaBlend = 24,    // Porter-Duff source-over
    PixelPaint = 25,    // Paint source where not transparent
    RgbAdd = 34,        // Additive blending
    RgbSubtract = 37,   // Subtractive blending
}
```

#### GraphicsBackendFactory

```csharp
public static class GraphicsBackendFactory
{
    public static IGraphicsBackend Create(GraphicsBackendOptions options)
    {
        return options.PreferredBackend switch
        {
            GraphicsBackendType.Auto => CreateBestAvailable(options),
            GraphicsBackendType.Software => new SoftwareGraphicsBackend(options),
            GraphicsBackendType.SkiaSharp => CreateSkiaSharp(options),
            GraphicsBackendType.Direct2D => CreateDirect2D(options),
            GraphicsBackendType.Null => new NullGraphicsBackend(),
            _ => throw new ArgumentException($"Unknown backend: {options.PreferredBackend}")
        };
    }

    private static IGraphicsBackend CreateBestAvailable(GraphicsBackendOptions options)
    {
        // Try GPU backends first, fall back to software
        if (TryCreateSkiaSharp(options, out var skia)) return skia;
        if (TryCreateDirect2D(options, out var d2d)) return d2d;
        return new SoftwareGraphicsBackend(options);
    }
}
```

### BitBlt Compute Shader (For GPU Backends)

```glsl
#version 450

layout(local_size_x = 16, local_size_y = 16) in;

layout(rgba8, binding = 0) uniform image2D destImage;
layout(rgba8, binding = 1) uniform readonly image2D sourceImage;
uniform int combinationRule;
uniform ivec4 destRect;  // x, y, width, height
uniform ivec2 sourceOffset;

void main() {
    ivec2 gid = ivec2(gl_GlobalInvocationID.xy);
    if (gid.x >= destRect.z || gid.y >= destRect.w) return;

    ivec2 destPos = destRect.xy + gid;
    ivec2 srcPos = sourceOffset + gid;

    vec4 src = imageLoad(sourceImage, srcPos);
    vec4 dst = imageLoad(destImage, destPos);
    vec4 result;

    switch (combinationRule) {
        case 0:  result = vec4(0, 0, 0, 1); break;  // Clear (black)
        case 3:  result = src; break;               // Copy
        case 6:  result = vec4(abs(src.rgb - dst.rgb), 1); break;  // XOR approximation
        case 7:  result = max(src, dst); break;     // Or
        case 10: result = vec4(1.0 - dst.rgb, 1); break;  // Invert
        case 15: result = vec4(1, 1, 1, 1); break;  // Set (white)
        case 24: // Alpha blend
            float srcA = src.a;
            float invA = 1.0 - srcA;
            result = vec4(src.rgb * srcA + dst.rgb * invA, 1.0);
            break;
        default: result = dst; break;
    }

    imageStore(destImage, destPos, result);
}
```

### Files Structure

```
Smalltalk80.Interpreter/Core/VM/Graphics/
├── Abstraction/
│   ├── BlendMode.cs              # Extended blend modes (0-15 classic + modern)
│   ├── ITexture.cs               # Texture/surface abstraction
│   ├── IGraphicsBackend.cs       # Core graphics operations interface
│   ├── SoftwareGraphicsBackend.cs # CPU-based implementation
│   ├── GraphicsBackendFactory.cs  # Runtime backend selection
│   └── BackendCapabilities.cs     # Feature flags
├── Backends/
│   ├── SkiaSharpBackend.cs        # GPU via SkiaSharp (future)
│   └── Direct2DBackend.cs         # GPU via Direct2D (future)
├── BitBltEngine.cs                # Existing 1bpp engine
└── BitBltEngine32.cs              # 32bpp engine (current implementation)
```

---

## Part 12: Complete Implementation Roadmap

### Phase 1: Form Depth Infrastructure (Week 1)

**Smalltalk Changes:**
1. Add `depth` instance variable to Form
2. Add `Form>>depth`, `Form>>depth:` accessors
3. Add `Form class>>extent:depth:` creation method
4. Update `Form>>extent:` to use depth-aware allocation
5. Default depth = 1 for backward compatibility

**C# Changes:**
1. Update VMConstants.cs with Form depth ivar index
2. Update ColdStartPhases.cs for 5-ivar Form class
3. Primitive 96 reads depth from Form ivar
4. Dispatch to appropriate BitBlt engine based on depth

### Phase 2: DisplayScreen at 32bpp (Week 1-2)

**Smalltalk Changes:**
1. Add `DisplayScreen class>>displayExtent:depth:`
2. Add `DisplayScreen class>>preferredDepth` (returns 32)
3. Update system startup to use 32bpp Display

**C# Changes:**
1. Remove current display buffer hack in WinFormsDisplay
2. Primitive 102 configures display for Form's depth
3. WinFormsDisplay handles 32bpp buffer natively

### Phase 3: Color Class (Week 2)

**Smalltalk Changes:**
1. Implement Color class with ARGB storage
2. Add `Color>>asForm32`, `Color>>asHalftone`
3. Add standard colors (black, white, gray, red, green, blue)
4. Backward compatible with existing halftone usage

### Phase 4: Graphics Abstraction Layer (Week 2-3)

**C# Changes:**
1. Create ITexture interface
2. Create IGraphicsBackend interface
3. Implement SoftwareGraphicsBackend (wrapping existing engines)
4. Create GraphicsBackendFactory
5. Create NullGraphicsBackend for testing

### Phase 5: Widget Integration (Week 3)

**Smalltalk Changes:**
1. Update DisplayMedium convenience methods (gray, black, etc.)
2. Update Pen to use Color
3. Update ScrollBar, Button, Menu rendering

### Phase 6: GPU Backend - SkiaSharp (Week 4)

**C# Changes:**
1. Add SkiaSharp NuGet package
2. Implement SkiaSharpGraphicsBackend
3. GPU texture management
4. Blend mode mapping
5. Performance testing

### Phase 7: GPU Backend - Direct2D (Optional, Week 5)

**C# Changes:**
1. Add d2dlib or direct COM interop
2. Implement Direct2DGraphicsBackend
3. Windows-specific optimizations

---

## Part 13: Testing Strategy

### Unit Tests

```csharp
[TestClass]
public class GraphicsBackendTests
{
    [TestMethod]
    [DataRow(GraphicsBackendType.Software)]
    [DataRow(GraphicsBackendType.SkiaSharp)]
    public void BitBlt_Copy_CopiesPixelsCorrectly(GraphicsBackendType type)
    {
        var backend = GraphicsBackendFactory.Create(new() { PreferredBackend = type });
        var src = backend.CreateTexture(10, 10, PixelDepth.ThirtyTwoBit);
        var dst = backend.CreateTexture(10, 10, PixelDepth.ThirtyTwoBit);

        src.SetPixel(5, 5, 0xFFFF0000); // Red

        backend.BitBlt(new BitBltParams {
            Source = src, Destination = dst,
            Width = 10, Height = 10,
            BlendMode = BlendMode.Copy
        });

        Assert.AreEqual(0xFFFF0000, dst.GetPixel(5, 5));
    }

    [TestMethod]
    public void AlphaBlend_BlendsSemiTransparentCorrectly()
    {
        // 50% transparent red over blue should give purple
        // ...
    }
}
```

### Visual Regression Tests

1. Render reference images with software backend
2. Render same scenes with GPU backends
3. Compare pixel-by-pixel with tolerance

### Performance Benchmarks

```csharp
[Benchmark]
public void FullScreenBitBlt_Software() => RunBitBlt(GraphicsBackendType.Software);

[Benchmark]
public void FullScreenBitBlt_SkiaSharp() => RunBitBlt(GraphicsBackendType.SkiaSharp);

[Benchmark]
public void FullScreenBitBlt_Direct2D() => RunBitBlt(GraphicsBackendType.Direct2D);
```

---

---

## Part 14: Critical Architecture Insight - Why IDisplay Must Be Unified

### The Problem with Separating BitBlt from Display/Input

The initial graphics abstraction was **fundamentally flawed** because it separated BitBlt operations from display/input handling:

```
FLAWED Design (DO NOT USE):
┌─────────────────────────────────────┐
│     WinFormsDisplayProvider         │ ← Owns window, input, display rendering
└─────────────────────────────────────┘
              ╳ Separate (WRONG)
┌─────────────────────────────────────┐
│     IGraphicsBackend                │ ← Only handles BitBlt operations
└─────────────────────────────────────┘
```

**This doesn't work for GPU backends** because GPU graphics libraries want to own:
1. **Window/surface creation** - they create the GPU context tied to the window
2. **Rendering operations** - they manage GPU resources and command buffers
3. **Input events** - they receive events from their window

You **cannot** use WinForms for the window and OpenGL for rendering without awkward bridging that defeats GPU acceleration.

### The Correct Architecture: Unified IDisplay

```
CORRECT Design:
┌─────────────────────────────────────────────────────────────────────┐
│                           IDisplay                                   │
│  ┌──────────────┬────────────────┬─────────────────────────────┐    │
│  │   Window     │    Renderer    │         Input               │    │
│  │  Management  │    Context     │        Events               │    │
│  ├──────────────┼────────────────┼─────────────────────────────┤    │
│  │ Width/Height │ CreateTexture  │ MousePosition               │    │
│  │ Title        │ BitBlt         │ MouseButtons                │    │
│  │ Show/Hide    │ FillRect       │ KeyboardState               │    │
│  │ Resize       │ Clear          │ PollEvents/WaitEvents       │    │
│  │ Close        │ Present        │ EventQueue                  │    │
│  └──────────────┴────────────────┴─────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        ▼                           ▼                           ▼
┌───────────────────┐   ┌───────────────────┐   ┌───────────────────┐
│  WinFormsDisplay  │   │  SilkNetDisplay   │   │    SDL2Display    │
│  (Software/GDI)   │   │  (OpenGL/Vulkan)  │   │  (SDL_Renderer)   │
└───────────────────┘   └───────────────────┘   └───────────────────┘
```

### IDisplay Interface Design

```csharp
/// <summary>
/// Unified display surface with integrated rendering and input.
/// Window, graphics, and input are inseparable in GPU-accelerated systems.
/// </summary>
public interface IDisplay : IDisposable
{
    // === Window Management ===
    int Width { get; }
    int Height { get; }
    string Title { get; set; }
    bool IsVisible { get; }
    bool IsFocused { get; }

    void Show();
    void Hide();
    void Close();
    void Resize(int width, int height);

    // === Rendering Context ===
    ITexture CreateTexture(int width, int height, PixelDepth depth);
    void BitBlt(in BitBltParams p);
    void FillRect(int x, int y, int width, int height, Color32 color);
    void Clear(Color32 color);
    void BeginFrame();
    void EndFrame();
    void Present();  // Swap buffers / flip

    /// <summary>
    /// The display's backing texture (Smalltalk's Display Form).
    /// BitBlt operations targeting this texture appear on screen after Present().
    /// </summary>
    ITexture DisplayTexture { get; }
    void InvalidateRegion(int x, int y, int width, int height);

    // === Input (Unified with display - NOT separable) ===
    (int X, int Y) MousePosition { get; }
    MouseButtons MouseButtons { get; }
    IKeyboardState KeyboardState { get; }
    bool PollEvent(out DisplayEvent evt);

    /// <summary>
    /// WaitHandle that signals when events are available.
    /// Use with WaitHandle.WaitAny() for efficient multi-source waiting.
    /// </summary>
    WaitHandle EventAvailableHandle { get; }

    // === Event loop integration ===
    void ProcessEvents();  // Must be called on display's thread
}
```

### Threading Model Integration

Our two-thread architecture integrates as follows:

```
┌────────────────────────────────────────────────────────────────────┐
│                    UI/Display Thread                                │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  IDisplay Implementation                      │   │
│  │                                                               │   │
│  │  ProcessEvents() / RunEventLoop()                            │   │
│  │    - Pumps window system messages                            │   │
│  │    - Enqueues DisplayEvents to thread-safe queue             │   │
│  │    - Updates atomic MousePosition/MouseButtons               │   │
│  │    - Signals EventAvailableHandle                            │   │
│  │                                                               │   │
│  │  EndFrame() / Present()                                      │   │
│  │    - Swaps GPU buffers or blits to screen                    │   │
│  │                                                               │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                      │
│                              │ EventAvailableHandle (WaitHandle)    │
│                              │ + Atomic state accessors             │
│                              ▼                                      │
└────────────────────────────────────────────────────────────────────┘
                               │
                               │  WaitHandle.WaitAny([EventHandle, TimerHandle])
                               ▼
┌────────────────────────────────────────────────────────────────────┐
│                       Smalltalk Thread                              │
│                                                                     │
│  InterpreterLoop:                                                   │
│    - WaitAny([display.EventAvailableHandle, timer.Handle])         │
│    - Read events via display.PollEvent()                           │
│    - Poll state via display.MousePosition, display.MouseButtons    │
│    - Execute BitBlt primitives:                                    │
│        - For software backend: direct execution                    │
│        - For GPU backend: may queue commands for UI thread         │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

**Thread Safety Requirements:**
1. **Atomic state reads**: `MousePosition` and `MouseButtons` must be atomic for polling
2. **Event queue**: Thread-safe queue with `EventAvailableHandle` signaling
3. **BitBlt operations**: Software backend executes directly; GPU backend may marshal to UI thread

### IDisplaySystem Factory Interface

```csharp
/// <summary>
/// Factory for creating display systems. Entry point for the graphics subsystem.
/// </summary>
public interface IDisplaySystem : IDisposable
{
    /// <summary>Display system name (e.g., "WinForms/GDI", "Silk.NET/OpenGL")</summary>
    string Name { get; }

    /// <summary>Capabilities supported by this display system</summary>
    DisplayCapabilities Capabilities { get; }

    /// <summary>Create a new display with the specified options</summary>
    IDisplay CreateDisplay(DisplayOptions options);

    /// <summary>Enumerate available monitors</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();
}

public record DisplayOptions
{
    public int Width { get; init; } = 800;
    public int Height { get; init; } = 600;
    public string Title { get; init; } = "Smalltalk-80";
    public bool Fullscreen { get; init; } = false;
    public bool VSync { get; init; } = true;
    public PixelDepth PreferredDepth { get; init; } = PixelDepth.OneBit;
}
```

### DisplayEvent Structure

```csharp
/// <summary>
/// Unified event type for all display input events.
/// Follows Blue Book input word format for compatibility.
/// </summary>
public readonly struct DisplayEvent
{
    public DisplayEventType Type { get; init; }

    // Mouse events
    public int MouseX { get; init; }
    public int MouseY { get; init; }
    public MouseButtons Button { get; init; }

    // Keyboard events
    public KeyCode Key { get; init; }
    public char Character { get; init; }
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Convert to Blue Book input word format (type << 12 | param)</summary>
    public int ToBlueBookWord() => Type switch
    {
        DisplayEventType.MouseMove => (1 << 12) | MouseX,
        DisplayEventType.MouseButtonDown => (5 << 12) | (int)Button,
        DisplayEventType.MouseButtonUp => (6 << 12) | (int)Button,
        DisplayEventType.KeyDown => (3 << 12) | (int)Key,
        DisplayEventType.KeyUp => (4 << 12) | (int)Key,
        _ => 0
    };
}

public enum DisplayEventType
{
    None = 0,
    MouseMove,
    MouseButtonDown,
    MouseButtonUp,
    KeyDown,
    KeyUp,
    TextInput,
    Resize,
    Focus,
    Blur,
    CloseRequested,
}
```

---

## Part 15: Phase 1 Implementation (Completed in Another Branch)

Phase 1 was implemented in branch `claude/standardize-assignment-operator-mLcex`.

### Files Created

| File | Purpose |
|------|---------|
| `Platform/IDisplay.cs` | Unified interface for window, rendering, and input |
| `Platform/IDisplaySystem.cs` | Factory interface and types |
| `Platform/DisplaySystemFactory.cs` | Factory + NullDisplay for headless testing |
| `Platform/Windows/WinFormsDisplay.cs` | WinForms implementation of IDisplay |
| `Platform/Windows/WinFormsDisplaySystem.cs` | WinForms factory |

### Files Removed

- `Core/VM/Graphics/Abstraction/*` - All 6 files (IGraphicsBackend, SoftwareGraphicsBackend, etc.)
- `Platform/Windows/WinFormsDisplayProvider.cs` - Replaced by WinFormsDisplay

### Key Implementation Details

1. **WinFormsDisplay** implements both `IDisplay` (new unified interface) and `IPlatformBridge` (backward compatibility for primitives)

2. **GraphicsPrimitives** now uses `BitBltEngine` directly - no abstraction layer overhead

3. **Program.cs** updated to use `WinFormsDisplay` instead of the old provider

### Note on Merging

This branch (`claude/32bpp-display-graphics-RLW1k`) will need to be rebased onto or merged with `claude/standardize-assignment-operator-mLcex` to incorporate the unified IDisplay implementation.

---

## Part 16: Complete File Structure After All Phases

```
Smalltalk80.Interpreter/
├── Core/VM/Graphics/
│   ├── BitBltEngine.cs           # 1-bit BitBlt operations
│   └── BitBltEngine32.cs         # 32-bit BitBlt operations
│
├── Platform/
│   ├── IDisplay.cs               # Unified display interface
│   ├── IDisplaySystem.cs         # Factory interface & types
│   ├── DisplaySystemFactory.cs   # Factory implementation
│   │
│   ├── Windows/
│   │   ├── WinFormsDisplay.cs    # Software rendering backend
│   │   └── WinFormsDisplaySystem.cs
│   │
│   └── SilkNet/                  # Future GPU backend
│       ├── SilkNetDisplay.cs
│       └── SilkNetDisplaySystem.cs
│
├── Core/VM/Primitives/
│   ├── GraphicsPrimitives.cs     # Uses BitBltEngine directly
│   └── InputPrimitives.cs        # Uses IDisplay for input
│
└── Program.cs                    # Creates display via factory
```

---

**Document Version**: 4.0
**Last Updated**: 2026-01-01
**Author**: Claude Code Session
**Prior Work Incorporated**:
- 8-bit vs 32-bit analysis
- GPU acceleration options research
- Graphics abstraction layer design (both flawed and corrected)
- SkiaSharp/Direct2D/OpenGL/Silk.NET evaluation
- Unified IDisplay interface design
- Threading model integration
- Phase 1 implementation details from `claude/standardize-assignment-operator-mLcex`
