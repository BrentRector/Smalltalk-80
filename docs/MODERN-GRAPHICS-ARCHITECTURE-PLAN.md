# Modern Graphics Architecture Plan

**Document Version**: 1.0
**Date**: 2026-01-05
**Status**: Draft - Awaiting Review

---

## Executive Summary

This document presents a comprehensive plan to modernize the Smalltalk-80 graphics system from its current state (legacy 1bpp concepts with 32bpp bolted on) to a modern, GPU-accelerated graphics architecture while maintaining backward compatibility with existing Smalltalk code.

### Key Findings

| Aspect | Current State | Target State |
|--------|---------------|--------------|
| **Rendering** | CPU-based BitBlt32Engine | GPU-accelerated via SkiaSharp |
| **Text** | Character-by-character rendering | Batched string rendering with glyph atlas |
| **Color** | 32bpp RGB (no alpha) | 32bpp ARGB with Porter-Duff compositing |
| **Coordinates** | Integer pixels | Floating-point for zoom/scale support |
| **Display Updates** | Full invalidation | Dirty rectangle optimization |
| **Platform** | Windows-only (WinForms) | Cross-platform via SkiaSharp |

---

## Part 1: Analysis of Current Implementation

### 1.1 BitBlt Engine Architecture

**Location**: `Smalltalk80.Interpreter/Core/VM/Graphics/BitBlt32Engine.cs`

**Strengths**:
- Complete 16 Blue Book combination rules + Paint mode (rule 16)
- Zero-copy heap access via `GetFormSpanDirect32()`
- Optimized fast paths for rules 3, 7, 1, 6
- Proper overlap handling for scroll operations
- XOR caret fix for 32bpp semantics

**Limitations**:
1. **CPU-only rendering** - No GPU acceleration
2. **No alpha channel** - Only implicit opaque (0xFF)
3. **Per-pixel operations** - No batching
4. **Paint mode is grayscale only** - Not true alpha blending
5. **Integer coordinates** - No sub-pixel positioning

### 1.2 Text Rendering

**Location**: `Smalltalk80.Interpreter/Core/VM/Graphics/WinFormsFontBridge.cs`

**Current Approach**:
- GDI+ font rendering with ClearType anti-aliasing
- Strike fonts built as pre-rendered glyph bitmaps
- Fixed-pitch font alignment via `GetCellWidth()`
- Grayscale anti-aliasing (not subpixel)

**Limitations**:
1. **Character-by-character BitBlt** - Each glyph rendered individually
2. **No string batching** - Massive CPU overhead for text-heavy UIs
3. **No HarfBuzz integration** - Limited international text support
4. **CPU-based compositing** - Paint mode (rule 16) per-pixel blend

### 1.3 Display System

**Location**: `Smalltalk80.Interpreter/Platform/Windows/WinFormsDisplay.cs`

**Current Approach**:
- Double-buffered WinForms with `uint[] _displayBuffer`
- Primitive 96 modifies heap directly, then invalidates region
- WinForms Paint event copies buffer to screen

**Limitations**:
1. **System.Drawing.Common** - Windows-only, deprecated for cross-platform
2. **Full-buffer copy on paint** - No GPU texture caching
3. **No layer compositing** - All morphs rendered to single buffer
4. **Thread synchronization overhead** - UI thread vs Smalltalk thread

### 1.4 Form/Display Classes (Smalltalk-side)

**Location**: `Smalltalk-80 Source/Smalltalk-80.st` (lines 19932-20625)

**Current Structure**:
```
Form
  bits: WordArray (one word per pixel for 32bpp)
  width: SmallInteger
  height: SmallInteger
  offset: Point
```

**Limitations**:
1. **No ColorForm equivalent** - No indexed color support
2. **No alpha in Forms** - Can't represent transparent forms
3. **No depth management** - Hardcoded 32bpp assumption
4. **Legacy halftone patterns** - 1bpp patterns expanded to 32bpp

---

## Part 2: Modern Smalltalk Graphics Comparison

### 2.1 Squeak/Pharo Evolution

| Feature | Blue Book (1983) | Squeak (2000s) | Pharo Bloc (2024) |
|---------|------------------|----------------|-------------------|
| **BitBlt Rules** | 16 rules | 35+ rules | N/A (Canvas API) |
| **Color Depth** | 1bpp | 1-32bpp | 32bpp ARGB |
| **Alpha Blending** | No | Rule 24/34 | Full Porter-Duff |
| **Text** | StrikeFont | FreeType | FreeType+HarfBuzz |
| **Rendering** | CPU BitBlt | CPU BitBlt | Cairo/Skia GPU |
| **UI Framework** | MVC | Morphic | Bloc |
| **Canvas** | N/A | FormCanvas | Athens/Alexandrie |

**Key Innovations from Pharo**:
- **Alexandrie**: FFI bindings to Cairo + FreeType + HarfBuzz (40.3 FPS vs Cairo 12.5 FPS)
- **Bloc**: Next-gen UI with GPU-optimized rendering
- **Layer composition**: Pre-render unchanged elements

### 2.2 Cuis Smalltalk Approach

**Philosophy**: Pure Smalltalk implementation with optional VM acceleration

**Key Innovations**:
1. **Floating-point coordinates** - Enables smooth zooming/scaling
2. **Prefiltering antialiasing** - Superior to pixel coverage (scientifically proven)
3. **VectorGraphics engine** - Path-based drawing with DSP-based AA
4. **TrueType in Smalltalk** - No FreeType dependency
5. **Aggressive simplification** - <600 classes total

**Lessons for Our Implementation**:
- Floating-point coordinates are essential for modern UIs
- Pure Smalltalk is possible but needs VM acceleration for production
- Simplicity should be a design goal

### 2.3 Commercial Implementations

**VisualWorks**:
- MVP pattern with Canvas abstraction
- Cross-platform via CommonGraphics framework
- Pluggable rendering backends

**Dolphin Smalltalk**:
- Direct Windows GDI/GDI+ integration
- Native performance on Windows
- GDI+ for modern features (gradients, antialiasing)

**Lesson**: Platform-specific optimization is valuable, but abstraction layer enables portability.

---

## Part 3: Modern Graphics Technology Assessment

### 3.1 Rendering Backend Options

| Technology | Cross-Platform | GPU Accel | .NET Support | Effort |
|------------|----------------|-----------|--------------|--------|
| **SkiaSharp** | ✅ | ✅ | Excellent | Medium |
| **Direct2D** | ❌ Windows | ✅ | Good | Low |
| **Cairo** | ✅ | Limited | Fair (FFI) | High |
| **Avalonia** | ✅ | ✅ | Native | High |
| **Custom OpenGL** | ✅ | ✅ | Manual | Very High |

**Recommendation**: **SkiaSharp** as primary backend
- Cross-platform (Windows, macOS, Linux)
- GPU-accelerated (OpenGL, Vulkan, Metal, D3D)
- Excellent .NET bindings
- Used by Avalonia, MAUI, Flutter (via Impeller)
- Maps well to BitBlt concepts

### 3.2 Text Rendering Strategy

**Current Issues**:
1. Per-character BitBlt is 10-100x slower than batched rendering
2. No complex script support (Arabic, Thai, Devanagari)
3. No proper kerning or ligatures

**Modern Approach**:
1. **Glyph Atlas**: Pre-render glyphs to GPU texture
2. **Batched Drawing**: Submit entire string as single GPU call
3. **HarfBuzz**: Text shaping for complex scripts
4. **Subpixel Rendering**: 3x horizontal resolution on LCD

**Performance Impact** (from research):
- Microsoft AtlasEngine: 2x raw text throughput, ≥10x colored output
- WebRender optimization: Glyphs in 1-2 textures vs many

### 3.3 Compositing Model

**Current**: Immediate-mode BitBlt to single buffer

**Modern**: Retained-mode layer compositing

**Benefits of Layer Compositing**:
1. **Independent updates**: Only changed layers re-rendered
2. **GPU caching**: Layers as GPU textures
3. **Smooth scrolling**: Compositor thread, not main thread
4. **Transform acceleration**: Scale/rotate without re-rasterization

**Chrome's Model** (for reference):
1. Layerize → Raster → Activate → Aggregate → Draw
2. Compositor frame with render passes and quads
3. Compositor thread handles scrolling independently

---

## Part 4: Recommended Architecture

### 4.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Smalltalk Application Layer                   │
│  (View, Controller, Form, DisplayScreen, existing Morphic/MVC)  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Canvas Abstraction Layer                     │
│    Canvas API (drawRect:, drawString:, drawImage:, paths)       │
│    Maintains backward compatibility via BitBlt facade           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Graphics Engine Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ SkiaSharp    │  │ GlyphAtlas   │  │ LayerCompositor     │  │
│  │ Backend      │  │ Manager      │  │ (Future Phase)       │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Platform Display Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ Windows      │  │ macOS        │  │ Linux               │  │
│  │ (SDL2/Skia)  │  │ (Metal/Skia) │  │ (OpenGL/Vulkan/Skia)│  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Core Components

#### 4.2.1 Canvas API (New)

**Purpose**: Abstract drawing operations from BitBlt details

```csharp
public interface IGraphicsCanvas
{
    // Basic shapes
    void DrawRect(RectF bounds, Paint paint);
    void FillRect(RectF bounds, Paint paint);
    void DrawOval(RectF bounds, Paint paint);
    void FillOval(RectF bounds, Paint paint);

    // Paths (vector graphics)
    void DrawPath(Path path, Paint paint);
    void FillPath(Path path, Paint paint);

    // Text (batched)
    void DrawString(string text, PointF origin, Font font, Paint paint);
    void DrawGlyphs(GlyphRun glyphs, PointF origin, Paint paint);

    // Images/Forms
    void DrawImage(Image image, RectF destRect, RectF? sourceRect = null);
    void DrawForm(FormRef form, PointF dest, CombinationRule rule);

    // Clipping
    void PushClip(RectF clip);
    void PopClip();

    // Transform
    void PushTransform(Matrix3x2 transform);
    void PopTransform();

    // Compositing
    void SetBlendMode(BlendMode mode);
}
```

#### 4.2.2 BitBlt Compatibility Facade

**Purpose**: Maintain existing Smalltalk code compatibility

```csharp
public class BitBltFacade
{
    // Maps BitBlt primitive 96 to Canvas API
    public void CopyBits(BitBltParams params, IGraphicsCanvas canvas)
    {
        // Convert combination rule to blend mode
        var blendMode = MapRuleToBlendMode(params.CombinationRule);
        canvas.SetBlendMode(blendMode);

        if (params.SourceForm == null)
        {
            // Fill operation with halftone
            canvas.FillRect(params.DestRect, CreatePatternPaint(params.Halftone));
        }
        else
        {
            // Image transfer
            canvas.DrawForm(params.SourceForm, params.DestPoint, params.Rule);
        }
    }

    // Map Blue Book rules to Porter-Duff + blend modes
    private BlendMode MapRuleToBlendMode(int rule) => rule switch
    {
        0 => BlendMode.Clear,
        3 => BlendMode.Src,        // Copy
        5 => BlendMode.Dst,        // Noop
        6 => BlendMode.Xor,
        7 => BlendMode.SrcOver,    // Or → SrcOver for alpha
        16 => BlendMode.Multiply,  // Paint mode
        // ... other mappings
    };
}
```

#### 4.2.3 GlyphAtlasManager

**Purpose**: Cache rendered glyphs as GPU textures for batched text rendering

```csharp
public class GlyphAtlasManager
{
    private Dictionary<GlyphKey, GlyphEntry> _cache;
    private SKBitmap _atlasTexture;
    private AtlasPacker _packer;

    public struct GlyphKey
    {
        public int FontId;
        public int GlyphId;
        public int SizePixels;
        public bool Subpixel;
    }

    public struct GlyphEntry
    {
        public SKRect AtlasRect;
        public float AdvanceWidth;
        public SKPoint Bearing;
    }

    public GlyphEntry GetOrRenderGlyph(GlyphKey key, SKFont font)
    {
        if (_cache.TryGetValue(key, out var entry))
            return entry;

        // Render glyph to atlas
        var glyphBitmap = RenderGlyph(key, font);
        var atlasRect = _packer.Pack(glyphBitmap.Width, glyphBitmap.Height);

        // Copy to atlas texture
        CopyToAtlas(glyphBitmap, atlasRect);

        entry = new GlyphEntry
        {
            AtlasRect = atlasRect,
            AdvanceWidth = font.MeasureText(key.GlyphId),
            Bearing = GetBearing(font, key.GlyphId)
        };

        _cache[key] = entry;
        return entry;
    }

    public void DrawStringBatched(SKCanvas canvas, string text, SKPoint origin,
                                   SKFont font, SKPaint paint)
    {
        // Pre-collect all glyphs
        var glyphs = new List<(GlyphEntry, SKPoint)>();
        var x = origin.X;

        foreach (var codepoint in text.EnumerateRunes())
        {
            var glyphId = font.GetGlyph(codepoint);
            var key = new GlyphKey { FontId = font.GetHashCode(),
                                     GlyphId = glyphId,
                                     SizePixels = (int)font.Size };
            var entry = GetOrRenderGlyph(key, font);
            glyphs.Add((entry, new SKPoint(x + entry.Bearing.X,
                                           origin.Y - entry.Bearing.Y)));
            x += entry.AdvanceWidth;
        }

        // Single batched draw call
        canvas.DrawAtlas(_atlasTexture,
                         glyphs.Select(g => g.Item1.AtlasRect).ToArray(),
                         glyphs.Select(g => SKRect.Create(g.Item2,
                                        g.Item1.AtlasRect.Size)).ToArray(),
                         paint);
    }
}
```

#### 4.2.4 Extended Color Model

**Purpose**: Support true alpha blending and modern color operations

```csharp
public struct Color32
{
    public byte A;  // Alpha (0=transparent, 255=opaque)
    public byte R;
    public byte G;
    public byte B;

    public static Color32 FromARGB(uint argb) => new Color32
    {
        A = (byte)(argb >> 24),
        R = (byte)(argb >> 16),
        G = (byte)(argb >> 8),
        B = (byte)argb
    };

    public static Color32 FromSmalltalk10Bit(int rgb, byte alpha = 255)
    {
        // Convert Smalltalk's 10-bit per channel to 8-bit
        return new Color32
        {
            A = alpha,
            R = (byte)(((rgb >> 20) & 0x3FF) * 255 / 1023),
            G = (byte)(((rgb >> 10) & 0x3FF) * 255 / 1023),
            B = (byte)((rgb & 0x3FF) * 255 / 1023)
        };
    }

    // Premultiplied alpha (required for correct compositing)
    public Color32 Premultiply() => new Color32
    {
        A = A,
        R = (byte)(R * A / 255),
        G = (byte)(G * A / 255),
        B = (byte)(B * A / 255)
    };
}
```

### 4.3 Dirty Rectangle Optimization

**Purpose**: Only redraw changed regions

```csharp
public class DamageRegion
{
    private List<SKRectI> _dirtyRects = new();
    private const int MaxDirtyRects = 10; // Coalesce if too many

    public void AddDamage(SKRectI rect)
    {
        // Check for overlap with existing
        for (int i = 0; i < _dirtyRects.Count; i++)
        {
            if (_dirtyRects[i].IntersectsWith(rect))
            {
                // Union overlapping rectangles
                _dirtyRects[i] = SKRectI.Union(_dirtyRects[i], rect);
                CoalesceIfNeeded();
                return;
            }
        }

        _dirtyRects.Add(rect);
        CoalesceIfNeeded();
    }

    private void CoalesceIfNeeded()
    {
        if (_dirtyRects.Count > MaxDirtyRects)
        {
            // Too many rects - union into one
            var bounds = _dirtyRects.Aggregate(SKRectI.Union);
            _dirtyRects.Clear();
            _dirtyRects.Add(bounds);
        }
    }

    public IReadOnlyList<SKRectI> GetDirtyRects() => _dirtyRects;

    public void Clear() => _dirtyRects.Clear();
}
```

---

## Part 5: Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)

**Goal**: Add SkiaSharp backend while maintaining existing WinForms

#### 1.1 Add SkiaSharp Dependencies
- Add SkiaSharp NuGet packages
- Create `IGraphicsBackend` interface
- Implement both `WinFormsBackend` (existing) and `SkiaBackend` (new)
- Configuration switch between backends

#### 1.2 Implement Canvas Abstraction
- Create `IGraphicsCanvas` interface
- Implement `SkiaCanvas` wrapping `SKCanvas`
- Implement `WinFormsCanvas` wrapping existing code
- Verify feature parity

#### 1.3 BitBlt Compatibility Layer
- Create `BitBltFacade` class
- Map all 16 combination rules to Skia blend modes
- Map Paint mode (rule 16) to Multiply blend
- Ensure primitive 96 works through new layer

**Deliverable**: Build passes, existing UI works with either backend

### Phase 2: Text Optimization (Weeks 5-8)

**Goal**: 10x text rendering performance via batched glyph atlas

#### 2.1 Implement GlyphAtlasManager
- Create atlas texture (initially 2048x2048)
- Implement glyph caching with font/size/glyph key
- Rectangle packing algorithm
- Automatic atlas growth

#### 2.2 Integrate with StrikeFont
- Modify `FontObjectBuilder` to populate atlas
- Keep existing Strike font data for backward compatibility
- Add atlas-based rendering path

#### 2.3 Batch Text Rendering
- Implement `DrawStringBatched` in canvas
- Single draw call per string (not per character)
- Benchmark: Target 10x improvement in text-heavy views

**Deliverable**: Browser, Transcript show measurable performance gain

### Phase 3: Alpha Channel Support (Weeks 9-12)

**Goal**: True 32bpp ARGB with alpha compositing

#### 3.1 Extend Form Class
- Add optional alpha channel to Form
- Backward compatible (default alpha = 255)
- New primitive for alpha form creation

#### 3.2 Porter-Duff Compositing
- Implement full Porter-Duff operators
- Extend combination rules beyond 16
- Proper premultiplied alpha handling

#### 3.3 TranslucentColor Class
- Smalltalk-side `TranslucentColor` subclass of `Color`
- `Color blue alpha: 0.5` support
- Integration with Paint mode

**Deliverable**: Semi-transparent morphs render correctly

### Phase 4: Platform Abstraction (Weeks 13-16)

**Goal**: Cross-platform support via SDL2 + SkiaSharp

#### 4.1 SDL2 Window Management
- Replace WinForms with SDL2 for window creation
- Cross-platform input handling
- Maintain WinForms option for Windows-only builds

#### 4.2 GPU Surface Management
- SkiaSharp GPU context creation
- OpenGL/Vulkan/Metal backend selection
- Fallback to CPU if no GPU

#### 4.3 Input Abstraction
- Abstract mouse/keyboard from platform
- Maintain Blue Book input word format
- Add touch/gesture support for future

**Deliverable**: Interpreter runs on macOS/Linux

### Phase 5: Performance Optimization (Weeks 17-20)

**Goal**: GPU-accelerated rendering with dirty rectangles

#### 5.1 Dirty Rectangle Tracking
- Implement `DamageRegion` class
- Track damaged areas from BitBlt operations
- Only repaint dirty regions

#### 5.2 Layer Caching (Optional)
- Cache Views/Windows as GPU textures
- Compositor pattern for unchanged content
- Significant win for scrolling

#### 5.3 Benchmarking & Profiling
- Create graphics benchmark suite
- Profile bottlenecks
- Optimize hot paths

**Deliverable**: 2-5x overall rendering performance

### Phase 6: Advanced Features (Future)

#### 6.1 Vector Graphics Support
- Path-based drawing API
- Anti-aliased shapes without BitBlt
- SVG import/export

#### 6.2 Floating-Point Coordinates
- Support for sub-pixel positioning
- Zoom/scale without pixelation
- Retina/HiDPI support

#### 6.3 Hardware Video Decode
- GPU video playback
- Camera input
- Screen capture

---

## Part 6: Migration Strategy

### 6.1 Backward Compatibility Guarantees

1. **All existing Smalltalk code continues to work**
   - BitBlt primitive 96 behavior unchanged
   - Form class API unchanged
   - DisplayScreen API unchanged

2. **New features are opt-in**
   - Alpha channel forms require explicit creation
   - Vector drawing through new Canvas API
   - GPU backend via configuration

3. **Graceful degradation**
   - Falls back to CPU if no GPU
   - Falls back to WinForms if SDL2 unavailable

### 6.2 Testing Strategy

| Phase | Test Suite |
|-------|------------|
| 1 | BitBlt regression: all 16 rules with/without halftone |
| 2 | Text rendering: character positions, anti-aliasing quality |
| 3 | Alpha: compositing math correctness |
| 4 | Cross-platform: macOS/Linux boot to browser |
| 5 | Performance: FPS benchmarks, memory usage |

### 6.3 Rollback Plan

Each phase produces a separate backend implementation:
- Phase 1: `WinFormsBackend` (existing) vs `SkiaBackend` (new)
- Can switch backends at runtime via configuration
- If issues found, revert to previous backend

---

## Part 7: Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| SkiaSharp API changes | Low | Medium | Pin version, abstract interface |
| GPU driver issues | Medium | Medium | CPU fallback path |
| Text quality regression | Medium | High | Extensive visual testing |
| Performance regression | Low | High | Benchmark before/after each change |
| Cross-platform bugs | Medium | Medium | CI testing on all platforms |

---

## Appendix A: Combination Rule Mapping

| Blue Book Rule | Name | Skia BlendMode | Notes |
|----------------|------|----------------|-------|
| 0 | Clear | Clear | Always black |
| 1 | And | Darken (approx) | src & dest |
| 2 | AndReverse | - | Custom shader |
| 3 | Copy | Src | Direct copy |
| 4 | AndInverted | - | Custom shader |
| 5 | Noop | Dst | No change |
| 6 | Xor | Xor | Need source inversion |
| 7 | Or | SrcOver | For alpha; Plus for RGB |
| 8 | Nor | - | Custom shader |
| 9 | Equiv | - | Custom shader |
| 10 | Invert | - | Custom shader |
| 11 | OrReverse | - | Custom shader |
| 12 | CopyInverted | - | Custom shader |
| 13 | OrInverted | - | Custom shader |
| 14 | Nand | - | Custom shader |
| 15 | Set | - | Always white |
| 16 | Paint | Multiply | Grayscale alpha |

**Note**: Rules 2, 4, 8-15 require custom shaders. Can implement as fallback to CPU BitBlt32Engine if GPU path unavailable.

---

## Appendix B: Performance Targets

| Operation | Current | Target | Improvement |
|-----------|---------|--------|-------------|
| Single character render | ~100μs | ~10μs | 10x |
| Full string (20 chars) | ~2ms | ~50μs | 40x |
| Full screen clear | ~5ms | ~0.5ms | 10x |
| Browser scroll (1 page) | ~50ms | ~16ms | 3x |
| Form BitBlt (100x100) | ~200μs | ~50μs | 4x |

---

## Appendix C: File Structure

```
Smalltalk80.Interpreter/
├── Core/
│   └── VM/
│       └── Graphics/
│           ├── BitBlt32Engine.cs       (existing, unchanged)
│           ├── Canvas/
│           │   ├── IGraphicsCanvas.cs  (new interface)
│           │   ├── SkiaCanvas.cs       (new)
│           │   └── WinFormsCanvas.cs   (adapter)
│           ├── Text/
│           │   ├── GlyphAtlasManager.cs (new)
│           │   └── TextShaper.cs       (future: HarfBuzz)
│           └── Compositing/
│               ├── DamageRegion.cs     (new)
│               └── LayerCompositor.cs  (future)
├── Platform/
│   ├── Windows/
│   │   └── WinFormsDisplay.cs          (existing)
│   ├── CrossPlatform/
│   │   ├── SDL2Display.cs              (new)
│   │   └── SkiaGpuContext.cs           (new)
│   └── IDisplayProvider.cs             (existing interface)
```

---

## Appendix D: References

### Research Sources

1. **Squeak/Pharo**: Alexandrie, Bloc, Athens canvas architectures
2. **Cuis**: Morphic 3, VectorGraphics, Prefiltering Antialiasing paper
3. **VisualWorks**: Canvas abstraction patterns
4. **Chrome**: RenderingNG compositor architecture
5. **Skia**: GPU backend implementation

### Key Papers

- Porter, Duff (1984): "Compositing Digital Images" - Alpha compositing algebra
- Vuletich (2013): "Prefiltering Antialiasing for General Vector Graphics"
- Ingalls (1981): "BitBlt" - Original BitBlt design

### Documentation

- [SkiaSharp Documentation](https://docs.microsoft.com/en-us/dotnet/api/skiasharp)
- [SDL2 Documentation](https://wiki.libsdl.org/)
- [Blue Book Chapter 18](http://www.mirandabanda.org/bluebook/) - BitBlt specification
