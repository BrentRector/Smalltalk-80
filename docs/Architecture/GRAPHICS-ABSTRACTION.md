# Graphics Abstraction Layer

**Last Updated**: 2025-12-31

## Overview

The graphics abstraction layer provides a pluggable interface for BitBlt raster operations. This allows swapping between different rendering backends (CPU software, SkiaSharp GPU, Direct2D, OpenGL) without changing the Smalltalk primitives.

## Architecture

### Separation of Concerns

The graphics system has two distinct responsibilities:

1. **IGraphicsBackend** (Abstraction Layer): Performs BitBlt operations on in-memory textures/bitmaps
2. **WinFormsDisplayProvider** (Platform Layer): Window creation, input events, final display rendering

```
┌─────────────────────────────────────────────────────────────────┐
│                      Smalltalk Code                              │
│                  (Form, BitBlt, DisplayScreen)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    GraphicsPrimitives.cs                         │
│              (Primitive 96: copyBits, etc.)                      │
│                              │                                   │
│                              ▼                                   │
│              GraphicsBackendFactory.Current                      │
│                     (IGraphicsBackend)                           │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   Software      │ │   SkiaSharp     │ │   Direct2D      │
│   Backend       │ │   Backend       │ │   Backend       │
│   (CPU)         │ │   (GPU)         │ │   (GPU/Win)     │
└─────────────────┘ └─────────────────┘ └─────────────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│               WinFormsDisplayProvider                            │
│  - Reads Display bitmap from Smalltalk heap                      │
│  - Converts 1-bit to 32-bit for screen rendering                 │
│  - Handles mouse/keyboard input events                           │
│  - Thread-safe signaling to Smalltalk thread                     │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

1. **Backend abstraction is for computation, not display**: The platform-specific window and input handling remain in WinFormsDisplayProvider (or future platform providers).

2. **No backward compatibility layer**: GraphicsPrimitives.cs uses IGraphicsBackend exclusively - there's no fallback to direct BitBltEngine calls.

3. **SoftwareGraphicsBackend wraps BitBltEngine**: For 1-bit operations, the existing optimized BitBltEngine is called. For 32-bit operations, pure C# implementations are used.

4. **Thread-safe**: The graphics backend can be called from the Smalltalk thread while WinFormsDisplayProvider runs on the UI thread.

## File Structure

```
Smalltalk80.Interpreter/Core/VM/Graphics/
├── BitBltEngine.cs              # Original 1-bit BitBlt engine
└── Abstraction/
    ├── BlendMode.cs             # Extended blend modes (0-15 classic + alpha)
    ├── ITexture.cs              # Texture interface + CpuTexture
    ├── IGraphicsBackend.cs      # Core backend interface
    ├── SoftwareGraphicsBackend.cs  # CPU implementation
    ├── GraphicsBackendFactory.cs   # Factory + NullGraphicsBackend
    └── GraphicsIntegration.cs      # Helper for Form ↔ Texture conversion
```

## Interfaces

### IGraphicsBackend

Core interface for all graphics operations:

```csharp
public interface IGraphicsBackend : IDisposable
{
    string Name { get; }
    BackendCapabilities Capabilities { get; }
    ref BackendStats Stats { get; }

    // Texture creation
    ITexture CreateTexture(int width, int height, PixelDepth depth, bool isRenderTarget = true);
    ITexture CreateTextureFromData(int width, int height, PixelDepth depth, ReadOnlySpan<byte> data, bool isRenderTarget = true);

    // Core operations
    void BitBlt(in BitBltParams @params);
    void FillRect(ITexture dest, GraphicsRect rect, Color32 color, BlendMode mode = BlendMode.Copy);
    void CopyTexture(ITexture dest, int destX, int destY, ITexture source, int srcX, int srcY, int width, int height);
    void Clear(ITexture dest, Color32 color);

    // Frame management
    void BeginFrame();
    void EndFrame();
    void Flush();

    // Display integration
    void SetDisplayTexture(ITexture display);
    void Present();
    void InvalidateDisplay(GraphicsRect rect);

    // Depth conversion
    void ConvertPixels(ReadOnlySpan<byte> source, PixelDepth sourceDepth, Span<byte> dest, PixelDepth destDepth, int width, int height);
}
```

### ITexture

Abstraction for bitmap/surface storage:

```csharp
public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    PixelDepth Depth { get; }
    int Stride { get; }
    bool IsRenderTarget { get; }

    ITextureLock Lock(TextureAccess access);
    ReadOnlyMemory<byte>? TryGetPixelData();
    void Invalidate(int x, int y, int width, int height);
    void SetPixels(ReadOnlySpan<byte> data);
    void SetPixels(int x, int y, int width, int height, ReadOnlySpan<byte> data);
}
```

### BlendMode

Extended from classic BitBlt rules (0-15) to include modern operations:

```csharp
public enum BlendMode : byte
{
    // Classic BitBlt rules 0-15
    Clear = 0,           // 0000 - All zeros
    And = 1,             // 0001 - Source AND Dest
    AndReverse = 2,      // 0010 - Source AND (NOT Dest)
    Copy = 3,            // 0011 - Source only
    AndInverted = 4,     // 0100 - (NOT Source) AND Dest
    Noop = 5,            // 0101 - Dest unchanged
    Xor = 6,             // 0110 - Source XOR Dest
    Or = 7,              // 0111 - Source OR Dest
    Nor = 8,             // 1000 - NOT (Source OR Dest)
    Equiv = 9,           // 1001 - NOT (Source XOR Dest)
    Invert = 10,         // 1010 - NOT Dest
    OrReverse = 11,      // 1011 - Source OR (NOT Dest)
    CopyInverted = 12,   // 1100 - NOT Source
    OrInverted = 13,     // 1101 - (NOT Source) OR Dest
    Nand = 14,           // 1110 - NOT (Source AND Dest)
    Set = 15,            // 1111 - All ones

    // Extended operations (16+)
    AlphaBlend = 24,         // Per-pixel alpha blending
    AlphaBlendConstant = 25, // Constant alpha blending
    RgbAdd = 34,             // RGB additive (saturated)
    RgbSubtract = 35,        // RGB subtractive
    RgbMultiply = 36         // RGB multiply
}
```

## Initialization

The graphics backend is initialized early in Program.cs:

```csharp
// Initialize graphics backend (required before any BitBlt operations)
GraphicsBackendFactory.CreateAndSetCurrent(new GraphicsBackendOptions
{
    PreferredBackend = GraphicsBackendType.Software,
    AllowFallback = true,
    EnableStats = true
});
```

## Pixel Depths

Three depths are supported:

- **OneBit (1)**: Original Smalltalk-80 monochrome - 1 bit per pixel, MSB first
- **EightBit (8)**: Grayscale or indexed color (not prioritized)
- **ThirtyTwoBit (32)**: ARGB format - target for modern rendering

## Implementation Status

| Component | Status |
|-----------|--------|
| IGraphicsBackend interface | Complete |
| ITexture interface | Complete |
| BlendMode enum | Complete |
| SoftwareGraphicsBackend | Complete (1-bit via BitBltEngine, 32-bit native) |
| GraphicsBackendFactory | Complete |
| GraphicsPrimitives integration | Complete |
| SkiaSharp backend | Not implemented |
| Direct2D backend | Not implemented |
| OpenGL backend | Not implemented |

## Backend Selection

The factory supports automatic and manual backend selection:

```csharp
// Automatic - chooses best available
GraphicsBackendFactory.CreateAndSetCurrent(new GraphicsBackendOptions
{
    PreferredBackend = GraphicsBackendType.Auto
});

// Explicit software backend
GraphicsBackendFactory.CreateAndSetCurrent(new GraphicsBackendOptions
{
    PreferredBackend = GraphicsBackendType.Software
});
```

Priority for Auto selection:
1. Direct2D (Windows only, best performance)
2. SkiaSharp (cross-platform, good performance)
3. OpenGL (cross-platform)
4. Software (always available)

## Future GPU Backend Implementation

To implement a GPU backend (e.g., SkiaSharp):

1. Create `SkiaSharpGraphicsBackend : IGraphicsBackend`
2. Implement texture creation using SkiaSharp `SKBitmap` or `SKImage`
3. Implement BitBlt using SkiaSharp canvas drawing with appropriate paint modes
4. Handle 1-bit ↔ 32-bit conversion for Smalltalk compatibility
5. Register in `GraphicsBackendFactory.CreateSkiaSharpBackend()`

The 32-bit path will be used when migrating from monochrome to color displays.

## Thread Safety

- `GraphicsBackendFactory.Current` is accessed via lock
- SoftwareGraphicsBackend operations are thread-safe (no shared mutable state)
- WinFormsDisplayProvider handles UI thread marshaling separately

## Testing

The NullGraphicsBackend can be used for headless testing:

```csharp
GraphicsBackendFactory.CreateAndSetCurrent(new GraphicsBackendOptions
{
    PreferredBackend = GraphicsBackendType.Null
});
```
