// Graphics Abstraction Layer - Backend Interface
// Main abstraction for graphics operations - swap implementations for testing

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Rectangle structure for graphics operations.
/// </summary>
public readonly record struct GraphicsRect(int X, int Y, int Width, int Height)
{
    public static GraphicsRect FromLTRB(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public GraphicsRect Intersect(GraphicsRect other)
    {
        int x = Math.Max(X, other.X);
        int y = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);

        if (right <= x || bottom <= y)
            return new GraphicsRect(0, 0, 0, 0);

        return new GraphicsRect(x, y, right - x, bottom - y);
    }
}

/// <summary>
/// 32-bit ARGB color value.
/// </summary>
public readonly record struct Color32(byte A, byte R, byte G, byte B)
{
    public static readonly Color32 Transparent = new(0, 0, 0, 0);
    public static readonly Color32 Black = new(255, 0, 0, 0);
    public static readonly Color32 White = new(255, 255, 255, 255);

    public uint ToArgb() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    public static Color32 FromArgb(uint argb) => new(
        (byte)(argb >> 24),
        (byte)(argb >> 16),
        (byte)(argb >> 8),
        (byte)argb);

    public static Color32 FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static Color32 Gray(byte level) => new(255, level, level, level);
}

/// <summary>
/// Parameters for a BitBlt operation.
/// </summary>
public readonly struct BitBltParams
{
    public required ITexture Destination { get; init; }
    public required int DestX { get; init; }
    public required int DestY { get; init; }

    public ITexture? Source { get; init; }
    public int SourceX { get; init; }
    public int SourceY { get; init; }

    public required int Width { get; init; }
    public required int Height { get; init; }

    public GraphicsRect ClipRect { get; init; }

    public ITexture? Halftone { get; init; }

    public required BlendMode BlendMode { get; init; }

    /// <summary>
    /// For AlphaBlendConstant mode - alpha value 0-255.
    /// </summary>
    public byte ConstantAlpha { get; init; }

    /// <summary>
    /// Fill color when Source is null.
    /// </summary>
    public Color32 FillColor { get; init; }
}

/// <summary>
/// Graphics backend capabilities.
/// </summary>
[Flags]
public enum BackendCapabilities
{
    None = 0,

    /// <summary>Supports 1-bit monochrome textures</summary>
    OneBitDepth = 1 << 0,

    /// <summary>Supports 8-bit textures</summary>
    EightBitDepth = 1 << 1,

    /// <summary>Supports 32-bit ARGB textures</summary>
    ThirtyTwoBitDepth = 1 << 2,

    /// <summary>Supports hardware-accelerated BitBlt</summary>
    HardwareAccelerated = 1 << 3,

    /// <summary>Supports alpha blending (rules 24+)</summary>
    AlphaBlending = 1 << 4,

    /// <summary>Supports depth conversion during BitBlt</summary>
    DepthConversion = 1 << 5,

    /// <summary>Supports render-to-texture</summary>
    RenderToTexture = 1 << 6,

    /// <summary>Can present to screen</summary>
    ScreenPresent = 1 << 7,

    /// <summary>All capabilities</summary>
    All = OneBitDepth | EightBitDepth | ThirtyTwoBitDepth |
          HardwareAccelerated | AlphaBlending | DepthConversion |
          RenderToTexture | ScreenPresent
}

/// <summary>
/// Graphics backend statistics for performance monitoring.
/// </summary>
public struct BackendStats
{
    public long BitBltCount;
    public long PixelsProcessed;
    public long TextureCreations;
    public long TextureDisposals;
    public long BytesUploaded;
    public long BytesDownloaded;
    public TimeSpan TotalBitBltTime;

    public void Reset()
    {
        BitBltCount = 0;
        PixelsProcessed = 0;
        TextureCreations = 0;
        TextureDisposals = 0;
        BytesUploaded = 0;
        BytesDownloaded = 0;
        TotalBitBltTime = TimeSpan.Zero;
    }
}

/// <summary>
/// Abstract interface for graphics backends.
/// Implementations can be software (CPU), GPU-accelerated (SkiaSharp, Direct2D), etc.
/// </summary>
public interface IGraphicsBackend : IDisposable
{
    /// <summary>
    /// Human-readable name of this backend.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Capabilities supported by this backend.
    /// </summary>
    BackendCapabilities Capabilities { get; }

    /// <summary>
    /// Performance statistics (for debugging/profiling).
    /// </summary>
    ref BackendStats Stats { get; }

    /// <summary>
    /// Create a new texture with the specified dimensions and depth.
    /// </summary>
    /// <param name="width">Width in pixels</param>
    /// <param name="height">Height in pixels</param>
    /// <param name="depth">Pixel depth</param>
    /// <param name="isRenderTarget">Whether texture can be used as destination</param>
    /// <returns>New texture instance</returns>
    ITexture CreateTexture(int width, int height, PixelDepth depth, bool isRenderTarget = true);

    /// <summary>
    /// Create a texture from existing pixel data.
    /// </summary>
    ITexture CreateTextureFromData(int width, int height, PixelDepth depth,
                                    ReadOnlySpan<byte> data, bool isRenderTarget = true);

    /// <summary>
    /// Perform a BitBlt (bit block transfer) operation.
    /// This is the core graphics operation - all other drawing builds on this.
    /// </summary>
    /// <param name="params">BitBlt parameters</param>
    void BitBlt(in BitBltParams @params);

    /// <summary>
    /// Fill a rectangle with a solid color.
    /// Optimized path for simple fills.
    /// </summary>
    void FillRect(ITexture dest, GraphicsRect rect, Color32 color, BlendMode mode = BlendMode.Copy);

    /// <summary>
    /// Copy pixels from source to destination with depth conversion if needed.
    /// </summary>
    void CopyTexture(ITexture dest, int destX, int destY,
                     ITexture source, int srcX, int srcY,
                     int width, int height);

    /// <summary>
    /// Clear a texture to a solid color.
    /// </summary>
    void Clear(ITexture dest, Color32 color);

    /// <summary>
    /// Begin a frame/batch of operations.
    /// For GPU backends, this may acquire resources.
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// End a frame/batch and present to screen if applicable.
    /// </summary>
    void EndFrame();

    /// <summary>
    /// Flush pending operations (for GPU backends).
    /// </summary>
    void Flush();

    /// <summary>
    /// Set the display/screen texture that will be presented.
    /// </summary>
    void SetDisplayTexture(ITexture display);

    /// <summary>
    /// Present the display texture to screen.
    /// </summary>
    void Present();

    /// <summary>
    /// Invalidate a region of the display for refresh.
    /// </summary>
    void InvalidateDisplay(GraphicsRect rect);

    /// <summary>
    /// Convert pixel data between depths.
    /// </summary>
    void ConvertPixels(ReadOnlySpan<byte> source, PixelDepth sourceDepth,
                       Span<byte> dest, PixelDepth destDepth,
                       int width, int height);
}
