# Modern Graphics Architecture Plan

**Document Version**: 2.0
**Date**: 2026-01-05
**Status**: Draft - Clean-Slate Approach (No Backward Compatibility)

---

## Executive Summary

This document presents a clean-slate redesign of the Smalltalk-80 graphics system. We will **replace** (not wrap) the legacy BitBlt-based architecture with a modern GPU-accelerated Canvas API. All Smalltalk source code will be updated to use the new primitives directly.

### Design Principles

1. **No backward compatibility** - Legacy code will be updated, not accommodated
2. **No facades or adapters** - Direct implementation, no translation layers
3. **Delete obsolete code** - Remove BitBlt32Engine, WinForms display, StrikeFont infrastructure
4. **Modern primitives** - Canvas-based drawing, Porter-Duff compositing, batched text
5. **Single implementation** - SkiaSharp only, no fallback paths

### Architecture Summary

| Component | Delete | Replace With |
|-----------|--------|--------------|
| BitBlt class | ✅ | Canvas class |
| Form class | ✅ | Surface class |
| BitBlt32Engine.cs | ✅ | SkiaCanvas.cs |
| WinFormsDisplay.cs | ✅ | SDL2SkiaDisplay.cs |
| StrikeFont | ✅ | TrueTypeFont |
| Primitive 96 (copyBits) | ✅ | Primitive 200+ (canvas ops) |
| 16 combination rules | ✅ | Porter-Duff blend modes |
| Halftone patterns | ✅ | Gradient/Pattern brushes |

---

## Part 1: What We're Removing

### 1.1 BitBlt Infrastructure (DELETE)

**Files to delete**:
- `BitBlt32Engine.cs` (570 lines)
- `BitBltEngine.cs` (418 lines)
- `CombinationRule.cs` (63 lines)

**Smalltalk classes to delete**:
- `BitBlt` - Replace with `Canvas`
- `Form>>copyBits:from:at:clippingBox:rule:mask:` - Replace with `Canvas>>draw:`

**Why delete**: The 16 combination rules are a 1975 artifact. Modern compositing uses Porter-Duff operators (12 modes) plus blend modes (multiply, screen, overlay, etc.). Mapping between them requires complex shaders and loses semantics.

### 1.2 Form Class (DELETE)

**Current Form**:
```smalltalk
Form
  bits: WordArray    "raw pixels in heap"
  width: SmallInteger
  height: SmallInteger
  offset: Point
```

**Problems**:
- Pixels stored in Smalltalk heap (can't GPU accelerate)
- No alpha channel
- Integer coordinates only
- `bits` directly manipulated by BitBlt

**Replace with**: `Surface` class backed by GPU texture

### 1.3 StrikeFont System (DELETE)

**Files to delete**:
- `WinFormsFontBridge.cs` (424 lines)
- `FontObjectBuilder.cs`
- `IFontBridge.cs` (273 lines)

**Smalltalk classes to delete**:
- `StrikeFont` - Replace with `Font`
- Character-by-character rendering methods

**Why delete**: StrikeFont is a bitmap font format from 1980. Each character is BitBlt'd individually. Modern text rendering:
- Uses TrueType/OpenType vector fonts
- Renders entire strings in one GPU call
- Supports kerning, ligatures, complex scripts

### 1.4 WinForms Display (DELETE)

**Files to delete**:
- `WinFormsDisplay.cs` (835 lines)
- `WinFormsDisplayProvider.cs`

**Why delete**:
- System.Drawing.Common is deprecated for cross-platform
- WinForms is Windows-only
- No GPU acceleration
- We're going SDL2 + SkiaSharp from the start

---

## Part 2: New Architecture

### 2.1 High-Level Design

```
┌─────────────────────────────────────────────────────────────────┐
│                    Smalltalk Application Layer                   │
│            View, Controller, Window, DisplayScreen               │
│     (Updated to use Canvas API - no BitBlt references)          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Canvas API                               │
│   Smalltalk: Canvas, Surface, Font, Color, Path, Paint          │
│   Primitives: 200-250 (new graphics primitive range)            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SkiaSharp Engine                            │
│              SKCanvas, SKSurface, SKPaint, SKPath               │
│              GPU-accelerated (OpenGL/Vulkan/Metal)              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SDL2 Platform Layer                          │
│        Window management, input, GPU context creation            │
│           Cross-platform: Windows, macOS, Linux                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 New Smalltalk Class Hierarchy

```
Object
├── Canvas                    "Drawing context (wraps SKCanvas)"
│   └── RecordingCanvas       "For display lists"
├── Surface                   "GPU texture (replaces Form)"
│   └── DisplaySurface        "The screen (replaces DisplayScreen)"
├── Paint                     "Fill/stroke style"
├── Path                      "Vector paths (lines, curves)"
├── Font                      "TrueType font"
├── Color                     "ARGB color with alpha"
├── BlendMode                 "Porter-Duff + blend modes"
└── Transform                 "2D affine transformation matrix"
```

### 2.3 Canvas Class (Smalltalk)

**New API** - Clean, modern, no legacy:

```smalltalk
Canvas class
  "Instance creation"
  onSurface: aSurface         "Create canvas for drawing to surface"
  onDisplay                   "Create canvas for drawing to screen"

Canvas instance
  "State"
  save                        "Push state (clip, transform)"
  restore                     "Pop state"

  "Clipping"
  clipRect: aRectangle
  clipPath: aPath

  "Transform"
  translate: aPoint
  scale: aPoint
  rotate: radians
  transform: aMatrix

  "Drawing - Shapes"
  drawRect: rect paint: paint
  fillRect: rect paint: paint
  drawOval: rect paint: paint
  fillOval: rect paint: paint
  drawLine: p1 to: p2 paint: paint
  drawPath: path paint: paint
  fillPath: path paint: paint

  "Drawing - Images"
  drawSurface: surface at: point
  drawSurface: surface from: srcRect to: destRect
  drawSurface: surface at: point blend: blendMode

  "Drawing - Text"
  drawString: string at: point font: font paint: paint
  drawText: textBlob at: point paint: paint

  "Compositing"
  blendMode: aBlendMode       "Set Porter-Duff/blend mode"

  "Convenience"
  clear: aColor               "Fill entire canvas with color"
```

### 2.4 Surface Class (Smalltalk)

**Replaces Form** - GPU-backed texture:

```smalltalk
Surface class
  "Instance creation"
  extent: aPoint                    "Create ARGB surface"
  extent: aPoint alpha: boolean     "With/without alpha"
  fromFile: path                    "Load PNG/JPEG"

Surface instance
  "Accessing"
  width
  height
  extent
  hasAlpha

  "Pixel access (for compatibility - prefer Canvas drawing)"
  colorAt: aPoint
  colorAt: aPoint put: aColor

  "Canvas"
  canvas                            "Get Canvas for drawing to this surface"

  "I/O"
  saveAsPng: path
  saveAsJpeg: path quality: percent

  "GPU"
  flush                             "Ensure drawing complete"

  "No 'bits' instance variable - pixels live on GPU"
```

### 2.5 Color Class (Updated)

**Full ARGB support**:

```smalltalk
Color class
  "Creation"
  r: r g: g b: b                    "RGB 0.0-1.0, alpha=1.0"
  r: r g: g b: b a: a               "RGBA 0.0-1.0"
  fromARGB: integer                 "0xAARRGGBB"
  transparent                       "Fully transparent"

  "Named colors"
  black, white, red, green, blue, etc.

Color instance
  "Components (0.0-1.0)"
  red, green, blue, alpha

  "Conversion"
  asARGB                            "Integer 0xAARRGGBB"
  withAlpha: newAlpha               "Return copy with different alpha"
  premultiplied                     "For correct compositing"
```

### 2.6 BlendMode Class

**Porter-Duff + Standard Blend Modes**:

```smalltalk
BlendMode class
  "Porter-Duff (compositing)"
  clear           "Result = 0"
  src             "Result = src (ignore dest)"
  dst             "Result = dest (ignore src)"
  srcOver         "Result = src + (1-srcAlpha)*dest  [DEFAULT]"
  dstOver         "Result = (1-dstAlpha)*src + dest"
  srcIn           "Result = src * dstAlpha"
  dstIn           "Result = dest * srcAlpha"
  srcOut          "Result = src * (1-dstAlpha)"
  dstOut          "Result = dest * (1-srcAlpha)"
  srcATop         "Result = src*dstAlpha + dest*(1-srcAlpha)"
  dstATop         "Result = dest*srcAlpha + src*(1-dstAlpha)"
  xor             "Result = src*(1-dstAlpha) + dest*(1-srcAlpha)"

  "Blend modes (color mixing)"
  multiply        "Result = src * dest"
  screen          "Result = src + dest - src*dest"
  overlay         "Multiply or screen based on dest"
  darken          "Result = min(src, dest)"
  lighten         "Result = max(src, dest)"
  colorDodge      "Brighten dest based on src"
  colorBurn       "Darken dest based on src"
  hardLight       "Multiply or screen based on src"
  softLight       "Softer version of hard light"
  difference      "Result = |src - dest|"
  exclusion       "Lower contrast difference"

  "Special"
  plus            "Result = src + dest (additive)"
  modulate        "Result = src * dest (component multiply)"
```

### 2.7 Font Class (New)

**TrueType/OpenType fonts**:

```smalltalk
Font class
  "Creation"
  family: name size: pixels
  family: name size: pixels weight: weight style: style
  fromFile: path size: pixels

Font instance
  "Metrics"
  ascent
  descent
  leading
  lineHeight

  "Measurement"
  measureString: string             "Returns extent"
  measureChar: char                 "Returns width"

  "No xTable, no glyphs Form - it's a handle to Skia font"
```

### 2.8 Path Class (New)

**Vector graphics paths**:

```smalltalk
Path class
  new                               "Empty path"

Path instance
  "Construction"
  moveTo: point
  lineTo: point
  quadTo: control end: end          "Quadratic Bezier"
  cubicTo: c1 c2: c2 end: end       "Cubic Bezier"
  arcTo: rect startAngle: s sweepAngle: sweep
  close                             "Close current contour"

  "Shapes (convenience)"
  addRect: rect
  addOval: rect
  addRoundRect: rect radius: r
  addCircle: center radius: r

  "Operations"
  transform: matrix
  bounds                            "Bounding rectangle"
```

---

## Part 3: New Primitives

### 3.1 Primitive Numbering

Reserve range **200-299** for new graphics primitives. Delete primitives 96-104.

| Primitive | Operation | Parameters |
|-----------|-----------|------------|
| 200 | Canvas: save | canvas |
| 201 | Canvas: restore | canvas |
| 202 | Canvas: clipRect | canvas, rect |
| 203 | Canvas: translate | canvas, point |
| 204 | Canvas: scale | canvas, point |
| 205 | Canvas: rotate | canvas, radians |
| 210 | Canvas: drawRect | canvas, rect, paint |
| 211 | Canvas: fillRect | canvas, rect, paint |
| 212 | Canvas: drawOval | canvas, rect, paint |
| 213 | Canvas: fillOval | canvas, rect, paint |
| 214 | Canvas: drawLine | canvas, p1, p2, paint |
| 215 | Canvas: drawPath | canvas, path, paint |
| 216 | Canvas: fillPath | canvas, path, paint |
| 220 | Canvas: drawSurface | canvas, surface, srcRect, destRect, blend |
| 225 | Canvas: drawString | canvas, string, point, font, paint |
| 226 | Canvas: drawTextBlob | canvas, blob, point, paint |
| 230 | Canvas: clear | canvas, color |
| 231 | Canvas: blendMode | canvas, mode |
| 240 | Surface: create | width, height, hasAlpha → surface |
| 241 | Surface: fromFile | path → surface |
| 242 | Surface: extent | surface → point |
| 243 | Surface: colorAt | surface, point → color |
| 244 | Surface: colorAtPut | surface, point, color |
| 245 | Surface: saveAsPng | surface, path |
| 246 | Surface: canvas | surface → canvas |
| 250 | Font: create | family, size, weight, style → font |
| 251 | Font: metrics | font → array |
| 252 | Font: measureString | font, string → extent |
| 260 | Path: create | → path |
| 261 | Path: moveTo | path, point |
| 262 | Path: lineTo | path, point |
| 263 | Path: quadTo | path, control, end |
| 264 | Path: cubicTo | path, c1, c2, end |
| 265 | Path: close | path |
| 266 | Path: addRect | path, rect |
| 270 | Paint: create | → paint |
| 271 | Paint: color | paint, color |
| 272 | Paint: strokeWidth | paint, width |
| 273 | Paint: style | paint, style (fill/stroke/both) |
| 280 | Display: surface | → displaySurface |
| 281 | Display: present | "Swap buffers" |
| 282 | Display: resize | width, height |

### 3.2 C# Implementation Structure

```
Smalltalk80.Interpreter/
├── Core/VM/
│   ├── Graphics/                    [DELETE old contents]
│   │   ├── SkiaCanvas.cs           [NEW]
│   │   ├── SkiaSurface.cs          [NEW]
│   │   ├── SkiaFont.cs             [NEW]
│   │   ├── SkiaPath.cs             [NEW]
│   │   └── SkiaPaint.cs            [NEW]
│   └── Primitives/
│       ├── GraphicsPrimitives.cs   [REWRITE - new primitive range]
│       └── CanvasPrimitives.cs     [NEW]
├── Platform/
│   ├── Windows/                     [DELETE]
│   └── CrossPlatform/
│       ├── SDL2Display.cs          [NEW]
│       ├── SDL2Input.cs            [NEW]
│       └── SkiaGpuContext.cs       [NEW]
```

---

## Part 4: Smalltalk Source Updates

### 4.1 Files to Modify

**`Smalltalk-80 Source/Smalltalk-80.st`**:

| Section | Action |
|---------|--------|
| BitBlt class | DELETE entirely |
| Form class | DELETE, replace with Surface |
| DisplayScreen class | Rewrite to use DisplaySurface |
| StrikeFont class | DELETE, replace with Font |
| CharacterScanner | DELETE (string rendering is one primitive) |
| Pen class | Rewrite to use Canvas+Path |
| View hierarchy | Update to use Canvas API |

### 4.2 View Class Update Example

**Current** (BitBlt-based):
```smalltalk
View>>displayOn: aDisplayMedium
    | form |
    form := Form extent: self extent.
    self displayView.
    form displayOn: aDisplayMedium at: self origin
        clippingBox: aDisplayMedium boundingBox
        rule: Form over
        mask: Form black
```

**New** (Canvas-based):
```smalltalk
View>>drawOn: canvas
    canvas save.
    canvas clipRect: self bounds.
    canvas translate: self origin.
    self drawContent: canvas.
    self subviews do: [:each | each drawOn: canvas].
    canvas restore
```

### 4.3 DisplayScreen Update

**Current**:
```smalltalk
DisplayScreen>>beDisplay
    <primitive: 102>
```

**New**:
```smalltalk
DisplayScreen class>>current
    "Return the display surface"
    ^DisplaySurface current

DisplayScreen>>drawFrame
    | canvas |
    canvas := DisplaySurface current canvas.
    self rootView drawOn: canvas.
    DisplaySurface current present
```

### 4.4 Text Rendering Update

**Current** (character-by-character):
```smalltalk
CharacterScanner>>displayString: string on: destination
    string do: [:char |
        self displayCharacter: char on: destination.
        destX := destX + (font widthOf: char)]
```

**New** (single call):
```smalltalk
Paragraph>>drawOn: canvas
    canvas drawString: text at: position font: font paint: textPaint
```

---

## Part 5: Implementation Phases

### Phase 1: Platform Foundation (Week 1-2)

**Goal**: SDL2 window with Skia rendering context

**Tasks**:
1. Add NuGet packages: SkiaSharp, SkiaSharp.Views.Desktop, SDL2-CS
2. Create `SDL2Display.cs` - window creation, event loop
3. Create `SkiaGpuContext.cs` - OpenGL context + SKSurface
4. Basic event handling (keyboard, mouse)
5. Delete: `WinFormsDisplay.cs`, `WinFormsDisplayProvider.cs`

**Deliverable**: Window opens, clears to solid color, handles input

### Phase 2: Canvas Primitives (Week 3-4)

**Goal**: Basic Canvas drawing operations working

**Tasks**:
1. Create `SkiaCanvas.cs` wrapping `SKCanvas`
2. Create `SkiaSurface.cs` wrapping `SKSurface`
3. Create `SkiaPaint.cs` wrapping `SKPaint`
4. Implement primitives 200-231 (Canvas operations)
5. Implement primitives 240-246 (Surface operations)
6. Delete: `BitBlt32Engine.cs`, `BitBltEngine.cs`, `GraphicsPrimitives.cs` (old)

**Deliverable**: Can draw rectangles, lines to display from Smalltalk

### Phase 3: Text Rendering (Week 5-6)

**Goal**: Modern text with TrueType fonts

**Tasks**:
1. Create `SkiaFont.cs` wrapping `SKFont`/`SKTypeface`
2. Implement primitives 250-252 (Font operations)
3. Implement primitive 225 (drawString)
4. Delete: `WinFormsFontBridge.cs`, `IFontBridge.cs`, `FontObjectBuilder.cs`
5. Update Smalltalk: Font class, delete StrikeFont

**Deliverable**: Text renders with system fonts, anti-aliased

### Phase 4: Vector Graphics (Week 7-8)

**Goal**: Path-based drawing

**Tasks**:
1. Create `SkiaPath.cs` wrapping `SKPath`
2. Implement primitives 260-266 (Path operations)
3. Add Smalltalk Path class
4. Update Pen class to use Path + Canvas

**Deliverable**: Can draw arbitrary shapes, curves, polygons

### Phase 5: Smalltalk Source Migration (Week 9-12)

**Goal**: Update all Smalltalk graphics code

**Tasks**:
1. Delete BitBlt class
2. Replace Form with Surface throughout
3. Rewrite View hierarchy to use Canvas
4. Rewrite DisplayScreen
5. Delete CharacterScanner, replace with single drawString calls
6. Update all halftone usage to gradient/pattern brushes
7. Run full test suite, fix issues

**Deliverable**: System Browser, Transcript, all tools work with new graphics

### Phase 6: Polish & Performance (Week 13-14)

**Goal**: Production quality

**Tasks**:
1. Dirty rectangle optimization
2. Damage region tracking
3. Performance profiling
4. Memory leak detection
5. Cross-platform testing (Windows, macOS, Linux)

**Deliverable**: Stable, performant graphics system

---

## Part 6: Code Examples

### 6.1 SDL2Display.cs (Sketch)

```csharp
public class SDL2Display : IDisposable
{
    private IntPtr _window;
    private IntPtr _glContext;
    private GRContext _grContext;
    private SKSurface _surface;

    public void Initialize(int width, int height, string title)
    {
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

        // Request OpenGL 3.3 core
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);

        _window = SDL.SDL_CreateWindow(title,
            SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            width, height,
            SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

        _glContext = SDL.SDL_GL_CreateContext(_window);
        SDL.SDL_GL_MakeCurrent(_window, _glContext);

        // Create Skia GPU context
        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        CreateSurface(width, height);
    }

    private void CreateSurface(int width, int height)
    {
        var renderTarget = new GRBackendRenderTarget(
            width, height, 0, 8, new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat()));
        _surface = SKSurface.Create(_grContext, renderTarget,
            GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    public SKCanvas Canvas => _surface.Canvas;

    public void Present()
    {
        _surface.Canvas.Flush();
        SDL.SDL_GL_SwapWindow(_window);
    }
}
```

### 6.2 Canvas Primitive Implementation (Sketch)

```csharp
public static class CanvasPrimitives
{
    // Primitive 211: Canvas fillRect
    public static PrimitiveResult FillRect(PrimitiveContext ctx)
    {
        var canvas = ctx.GetSkiaCanvas(ctx.Receiver);
        var rect = ctx.GetRectangle(ctx.Arguments[0]);
        var paint = ctx.GetSkiaPaint(ctx.Arguments[1]);

        canvas.DrawRect(rect, paint);
        return PrimitiveResult.Success(ctx.Receiver);
    }

    // Primitive 225: Canvas drawString
    public static PrimitiveResult DrawString(PrimitiveContext ctx)
    {
        var canvas = ctx.GetSkiaCanvas(ctx.Receiver);
        var text = ctx.GetString(ctx.Arguments[0]);
        var point = ctx.GetPoint(ctx.Arguments[1]);
        var font = ctx.GetSkiaFont(ctx.Arguments[2]);
        var paint = ctx.GetSkiaPaint(ctx.Arguments[3]);

        canvas.DrawText(text, point.X, point.Y, font, paint);
        return PrimitiveResult.Success(ctx.Receiver);
    }

    // Primitive 220: Canvas drawSurface
    public static PrimitiveResult DrawSurface(PrimitiveContext ctx)
    {
        var canvas = ctx.GetSkiaCanvas(ctx.Receiver);
        var surface = ctx.GetSkiaSurface(ctx.Arguments[0]);
        var srcRect = ctx.GetRectangleOrNull(ctx.Arguments[1]);
        var destRect = ctx.GetRectangle(ctx.Arguments[2]);
        var blend = ctx.GetBlendMode(ctx.Arguments[3]);

        using var paint = new SKPaint { BlendMode = blend };
        canvas.DrawImage(surface.Snapshot(), srcRect, destRect, paint);
        return PrimitiveResult.Success(ctx.Receiver);
    }
}
```

### 6.3 Updated Smalltalk View (Sketch)

```smalltalk
"View class - simplified with Canvas"
View>>drawOn: canvas
    "Draw this view and all subviews"
    canvas save.
    canvas clipRect: self bounds.
    self drawBackground: canvas.
    self drawContent: canvas.
    self drawBorder: canvas.
    subviews do: [:v | v drawOn: canvas].
    canvas restore

View>>drawBackground: canvas
    backgroundColor ifNotNil: [
        canvas fillRect: self bounds paint: (Paint color: backgroundColor)]

View>>drawBorder: canvas
    borderWidth > 0 ifTrue: [
        | paint |
        paint := Paint new
            color: borderColor;
            style: #stroke;
            strokeWidth: borderWidth.
        canvas drawRect: self bounds paint: paint]

"Browser pane drawing"
ListView>>drawOn: canvas
    | y font paint |
    super drawOn: canvas.
    font := Font family: 'Courier New' size: 12.
    paint := Paint color: Color black.
    y := self bounds top + font lineHeight.
    self visibleItems do: [:item |
        item = selection ifTrue: [
            canvas fillRect: (self bounds left @ (y - font ascent)
                              extent: self bounds width @ font lineHeight)
                   paint: (Paint color: Color blue alpha: 0.3)].
        canvas drawString: item asString at: self bounds left @ y font: font paint: paint.
        y := y + font lineHeight]
```

---

## Part 7: Migration Checklist

### Delete List

**C# Files** (~2,500 lines):
- [ ] `BitBlt32Engine.cs`
- [ ] `BitBltEngine.cs`
- [ ] `CombinationRule.cs`
- [ ] `WinFormsFontBridge.cs`
- [ ] `IFontBridge.cs`
- [ ] `FontObjectBuilder.cs`
- [ ] `WinFormsDisplay.cs`
- [ ] `WinFormsDisplayProvider.cs`
- [ ] Old `GraphicsPrimitives.cs` (primitives 96-104)

**Smalltalk Classes**:
- [ ] `BitBlt`
- [ ] `Form` (replace with Surface)
- [ ] `StrikeFont`
- [ ] `CharacterScanner`
- [ ] `Pen` (rewrite)
- [ ] Halftone-related methods

### Create List

**C# Files** (~1,500 lines estimated):
- [ ] `SDL2Display.cs`
- [ ] `SDL2Input.cs`
- [ ] `SkiaGpuContext.cs`
- [ ] `SkiaCanvas.cs`
- [ ] `SkiaSurface.cs`
- [ ] `SkiaFont.cs`
- [ ] `SkiaPath.cs`
- [ ] `SkiaPaint.cs`
- [ ] `CanvasPrimitives.cs`

**Smalltalk Classes**:
- [ ] `Canvas`
- [ ] `Surface`
- [ ] `DisplaySurface`
- [ ] `Font` (new)
- [ ] `Path`
- [ ] `Paint`
- [ ] `BlendMode`
- [ ] `Transform`

### Update List

**Smalltalk Classes** (major updates):
- [ ] `View` and all subclasses
- [ ] `DisplayScreen`
- [ ] `Color` (add alpha)
- [ ] `Paragraph`/text display
- [ ] `Cursor`
- [ ] `Window`
- [ ] All tools (Browser, Inspector, etc.)

---

## Appendix A: Porter-Duff vs BitBlt Rules

**Why Porter-Duff is better**:

| BitBlt Rule | Meaning | Porter-Duff Equivalent |
|-------------|---------|------------------------|
| 3 (copy) | dest = src | `src` |
| 7 (or) | dest = src \| dest | `srcOver` (with alpha) |
| 6 (xor) | dest = src ^ dest | `xor` |
| 16 (paint) | grayscale blend | `multiply` or `srcOver` |

**BitBlt problems**:
1. Assumes 1bpp or no alpha
2. Boolean operations don't compose correctly with alpha
3. "Or" with alpha needs Porter-Duff, not bitwise OR
4. Rules 1,2,4,8,9,10-15 are rarely used and don't map cleanly

**Porter-Duff benefits**:
1. Mathematically correct alpha compositing
2. Industry standard (CSS, SVG, all modern graphics)
3. Skia implements them natively
4. Works correctly at any color depth

---

## Appendix B: Performance Expectations

| Operation | Old (CPU BitBlt) | New (GPU Skia) | Improvement |
|-----------|------------------|----------------|-------------|
| Full screen clear | ~5ms | ~0.1ms | 50x |
| 1000 rectangles | ~20ms | ~0.5ms | 40x |
| 20-char string | ~2ms | ~0.05ms | 40x |
| 100x100 image copy | ~0.2ms | ~0.02ms | 10x |
| Page scroll | ~50ms | ~2ms | 25x |

**Why so much faster**:
1. GPU parallelism (thousands of cores)
2. Batched draw calls
3. Texture caching
4. No CPU-GPU data transfer for cached content

---

## Appendix C: Dependencies

**NuGet Packages**:
```xml
<PackageReference Include="SkiaSharp" Version="2.88.7" />
<PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="2.88.7" />
<PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="2.88.7" />
<PackageReference Include="SDL2-CS" Version="2.28.5" />
```

**System Requirements**:
- OpenGL 3.3+ or Vulkan 1.0+ or Metal (macOS)
- SDL2 runtime library
- .NET 9.0+

---

## Summary

This clean-slate approach results in:

1. **~60% less graphics C# code** (delete 2,500 lines, add 1,500)
2. **Simpler Smalltalk code** (no BitBlt complexity)
3. **25-50x performance improvement** (GPU acceleration)
4. **Cross-platform from day one** (SDL2 + Skia)
5. **Modern graphics features** (alpha, gradients, paths, transforms)
6. **Industry-standard compositing** (Porter-Duff)
7. **High-quality text** (TrueType, anti-aliasing, kerning)

The migration requires updating all Smalltalk graphics code, but the result is dramatically simpler and more powerful.
