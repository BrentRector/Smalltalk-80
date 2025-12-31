// Graphics Abstraction Layer - Software Backend
// CPU-based implementation wrapping the existing BitBltEngine

using System.Diagnostics;

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Software (CPU-based) graphics backend.
/// Wraps the existing BitBltEngine for 1-bit operations.
/// Provides pure C# implementation for all depths.
/// </summary>
public sealed class SoftwareGraphicsBackend : IGraphicsBackend
{
    private ITexture? _displayTexture;
    private BackendStats _stats;
    private readonly Action<GraphicsRect>? _onDisplayInvalidated;

    public string Name => "Software (CPU)";

    public BackendCapabilities Capabilities =>
        BackendCapabilities.OneBitDepth |
        BackendCapabilities.EightBitDepth |
        BackendCapabilities.ThirtyTwoBitDepth |
        BackendCapabilities.AlphaBlending |
        BackendCapabilities.DepthConversion |
        BackendCapabilities.RenderToTexture;

    public ref BackendStats Stats => ref _stats;

    /// <summary>
    /// Create a new software graphics backend.
    /// </summary>
    /// <param name="onDisplayInvalidated">Callback when display region needs refresh</param>
    public SoftwareGraphicsBackend(Action<GraphicsRect>? onDisplayInvalidated = null)
    {
        _onDisplayInvalidated = onDisplayInvalidated;
    }

    public ITexture CreateTexture(int width, int height, PixelDepth depth, bool isRenderTarget = true)
    {
        _stats.TextureCreations++;
        return new CpuTexture(width, height, depth);
    }

    public ITexture CreateTextureFromData(int width, int height, PixelDepth depth,
                                           ReadOnlySpan<byte> data, bool isRenderTarget = true)
    {
        var texture = CreateTexture(width, height, depth, isRenderTarget);
        texture.SetPixels(data);
        _stats.BytesUploaded += data.Length;
        return texture;
    }

    public void BitBlt(in BitBltParams p)
    {
        var sw = Stopwatch.StartNew();
        _stats.BitBltCount++;

        // Handle based on depth combinations
        var destDepth = p.Destination.Depth;
        var sourceDepth = p.Source?.Depth ?? destDepth;

        if (destDepth == PixelDepth.OneBit && (p.Source == null || sourceDepth == PixelDepth.OneBit))
        {
            // Use existing optimized 1-bit engine
            BitBlt1Bit(p);
        }
        else if (destDepth == PixelDepth.ThirtyTwoBit)
        {
            // 32-bit destination
            BitBlt32Bit(p);
        }
        else
        {
            // 8-bit or mixed - use generic path
            BitBltGeneric(p);
        }

        _stats.PixelsProcessed += (long)p.Width * p.Height;
        _stats.TotalBitBltTime += sw.Elapsed;
    }

    private void BitBlt1Bit(in BitBltParams p)
    {
        using var destLock = p.Destination.Lock(TextureAccess.ReadWrite);
        var destBits = destLock.Data;

        ReadOnlySpan<byte> sourceBits = default;
        ITextureLock? sourceLock = null;

        if (p.Source != null)
        {
            sourceLock = p.Source.Lock(TextureAccess.Read);
            sourceBits = sourceLock.Data;
        }

        ReadOnlySpan<byte> halftone = default;
        ITextureLock? halftoneLock = null;
        int halftoneWidth = 0, halftoneHeight = 0;

        if (p.Halftone != null)
        {
            halftoneLock = p.Halftone.Lock(TextureAccess.Read);
            halftone = halftoneLock.Data;
            halftoneWidth = p.Halftone.Width;
            halftoneHeight = p.Halftone.Height;
        }

        try
        {
            // Map BlendMode to CombinationRule
            var rule = (CombinationRule)(byte)p.BlendMode;
            if ((int)p.BlendMode > 15)
                rule = CombinationRule.Copy; // Fallback for unsupported rules

            BitBltEngine.CopyBits(
                destBits,
                p.Destination.Width,
                p.Destination.Height,
                p.DestX,
                p.DestY,
                sourceBits,
                p.Source?.Width ?? 0,
                p.Source?.Height ?? 0,
                p.SourceX,
                p.SourceY,
                p.Width,
                p.Height,
                p.ClipRect.X,
                p.ClipRect.Y,
                p.ClipRect.Width,
                p.ClipRect.Height,
                halftone,
                halftoneWidth,
                halftoneHeight,
                rule);
        }
        finally
        {
            sourceLock?.Dispose();
            halftoneLock?.Dispose();
        }
    }

    private void BitBlt32Bit(in BitBltParams p)
    {
        using var destLock = p.Destination.Lock(TextureAccess.ReadWrite);
        var destPixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(destLock.Data);
        int destStride = p.Destination.Width;

        // Calculate clipping
        var clipRect = p.ClipRect.IsEmpty
            ? new GraphicsRect(0, 0, p.Destination.Width, p.Destination.Height)
            : p.ClipRect;

        var destRect = new GraphicsRect(p.DestX, p.DestY, p.Width, p.Height);
        var clipped = destRect.Intersect(clipRect);

        if (clipped.IsEmpty) return;

        int srcOffsetX = clipped.X - p.DestX;
        int srcOffsetY = clipped.Y - p.DestY;

        if (p.Source == null)
        {
            // Fill operation
            uint fillValue = p.FillColor.ToArgb();
            Fill32Bit(destPixels, destStride, clipped, fillValue, p.BlendMode);
        }
        else if (p.Source.Depth == PixelDepth.ThirtyTwoBit)
        {
            // 32-bit to 32-bit copy
            using var srcLock = p.Source.Lock(TextureAccess.Read);
            var srcPixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(srcLock.Data);
            int srcStride = p.Source.Width;

            Copy32BitTo32Bit(
                srcPixels, srcStride, p.SourceX + srcOffsetX, p.SourceY + srcOffsetY,
                destPixels, destStride, clipped.X, clipped.Y,
                clipped.Width, clipped.Height, p.BlendMode);
        }
        else if (p.Source.Depth == PixelDepth.OneBit)
        {
            // 1-bit to 32-bit copy (for font rendering, etc.)
            using var srcLock = p.Source.Lock(TextureAccess.Read);
            var srcBits = srcLock.Data;

            Copy1BitTo32Bit(
                srcBits, p.Source.Width, p.SourceX + srcOffsetX, p.SourceY + srcOffsetY,
                destPixels, destStride, clipped.X, clipped.Y,
                clipped.Width, clipped.Height, p.BlendMode,
                Color32.Black, Color32.White);
        }
    }

    private void BitBltGeneric(in BitBltParams p)
    {
        // Generic fallback - pixel by pixel with depth conversion
        // This is slow but correct for all cases
        // TODO: Optimize specific depth combinations as needed

        var clipRect = p.ClipRect.IsEmpty
            ? new GraphicsRect(0, 0, p.Destination.Width, p.Destination.Height)
            : p.ClipRect;

        var destRect = new GraphicsRect(p.DestX, p.DestY, p.Width, p.Height);
        var clipped = destRect.Intersect(clipRect);

        if (clipped.IsEmpty) return;

        using var destLock = p.Destination.Lock(TextureAccess.ReadWrite);
        ITextureLock? srcLock = p.Source?.Lock(TextureAccess.Read);

        try
        {
            for (int y = 0; y < clipped.Height; y++)
            {
                for (int x = 0; x < clipped.Width; x++)
                {
                    int destX = clipped.X + x;
                    int destY = clipped.Y + y;
                    int srcX = p.SourceX + (clipped.X - p.DestX) + x;
                    int srcY = p.SourceY + (clipped.Y - p.DestY) + y;

                    uint srcColor = p.Source != null && srcLock != null
                        ? GetPixel32(srcLock.Data, p.Source.Width, p.Source.Depth, srcX, srcY)
                        : p.FillColor.ToArgb();

                    uint destColor = GetPixel32(destLock.Data, p.Destination.Width, p.Destination.Depth, destX, destY);

                    uint result = ApplyBlendMode32(srcColor, destColor, p.BlendMode, p.ConstantAlpha);

                    SetPixel32(destLock.Data, p.Destination.Width, p.Destination.Depth, destX, destY, result);
                }
            }
        }
        finally
        {
            srcLock?.Dispose();
        }
    }

    private static void Fill32Bit(Span<uint> dest, int stride, GraphicsRect rect, uint color, BlendMode mode)
    {
        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            int rowOffset = y * stride;
            for (int x = rect.X; x < rect.Right; x++)
            {
                if (mode == BlendMode.Copy)
                {
                    dest[rowOffset + x] = color;
                }
                else
                {
                    dest[rowOffset + x] = ApplyBlendMode32(color, dest[rowOffset + x], mode, 255);
                }
            }
        }
    }

    private static void Copy32BitTo32Bit(
        ReadOnlySpan<uint> src, int srcStride, int srcX, int srcY,
        Span<uint> dest, int destStride, int destX, int destY,
        int width, int height, BlendMode mode)
    {
        for (int y = 0; y < height; y++)
        {
            int srcRow = (srcY + y) * srcStride + srcX;
            int destRow = (destY + y) * destStride + destX;

            if (mode == BlendMode.Copy)
            {
                // Fast path - direct copy
                src.Slice(srcRow, width).CopyTo(dest.Slice(destRow, width));
            }
            else if (mode == BlendMode.AlphaBlend)
            {
                // Alpha blending
                for (int x = 0; x < width; x++)
                {
                    dest[destRow + x] = AlphaBlend(src[srcRow + x], dest[destRow + x]);
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    dest[destRow + x] = ApplyBlendMode32(src[srcRow + x], dest[destRow + x], mode, 255);
                }
            }
        }
    }

    private static void Copy1BitTo32Bit(
        ReadOnlySpan<byte> src, int srcWidth, int srcX, int srcY,
        Span<uint> dest, int destStride, int destX, int destY,
        int width, int height, BlendMode mode,
        Color32 foreground, Color32 background)
    {
        int srcStride = (srcWidth + 7) / 8; // Bytes per row in 1-bit source
        uint fg = foreground.ToArgb();
        uint bg = background.ToArgb();

        for (int y = 0; y < height; y++)
        {
            int destRow = (destY + y) * destStride + destX;

            for (int x = 0; x < width; x++)
            {
                int sx = srcX + x;
                int sy = srcY + y;

                // Get 1-bit pixel value
                int byteIndex = sy * srcStride + sx / 8;
                int bitIndex = 7 - (sx % 8); // MSB first
                bool bit = byteIndex < src.Length && ((src[byteIndex] >> bitIndex) & 1) != 0;

                uint srcColor = bit ? fg : bg;

                if (mode == BlendMode.Copy)
                {
                    dest[destRow + x] = srcColor;
                }
                else
                {
                    dest[destRow + x] = ApplyBlendMode32(srcColor, dest[destRow + x], mode, 255);
                }
            }
        }
    }

    private static uint GetPixel32(ReadOnlySpan<byte> data, int width, PixelDepth depth, int x, int y)
    {
        return depth switch
        {
            PixelDepth.ThirtyTwoBit =>
                System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(data)[y * width + x],

            PixelDepth.EightBit =>
            {
                byte gray = data[y * ((width + 3) / 4 * 4) + x];
                return 0xFF000000u | ((uint)gray << 16) | ((uint)gray << 8) | gray;
            },

            PixelDepth.OneBit =>
            {
                int stride = (width + 7) / 8;
                int byteIndex = y * stride + x / 8;
                int bitIndex = 7 - (x % 8);
                bool bit = byteIndex < data.Length && ((data[byteIndex] >> bitIndex) & 1) != 0;
                return bit ? 0xFF000000u : 0xFFFFFFFFu;
            },

            _ => 0
        };
    }

    private static void SetPixel32(Span<byte> data, int width, PixelDepth depth, int x, int y, uint color)
    {
        switch (depth)
        {
            case PixelDepth.ThirtyTwoBit:
                System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(data)[y * width + x] = color;
                break;

            case PixelDepth.EightBit:
                // Convert to grayscale
                byte r = (byte)(color >> 16);
                byte g = (byte)(color >> 8);
                byte b = (byte)color;
                byte gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
                data[y * ((width + 3) / 4 * 4) + x] = gray;
                break;

            case PixelDepth.OneBit:
                int stride = (width + 7) / 8;
                int byteIndex = y * stride + x / 8;
                int bitIndex = 7 - (x % 8);
                // Threshold at 50% gray
                byte lum = (byte)(((color >> 16) & 0xFF) * 77 +
                                  ((color >> 8) & 0xFF) * 150 +
                                  (color & 0xFF) * 29 >> 8);
                if (lum < 128)
                    data[byteIndex] |= (byte)(1 << bitIndex);
                else
                    data[byteIndex] &= (byte)~(1 << bitIndex);
                break;
        }
    }

    private static uint ApplyBlendMode32(uint src, uint dest, BlendMode mode, byte constAlpha)
    {
        return mode switch
        {
            BlendMode.Clear => 0x00000000,
            BlendMode.Copy => src,
            BlendMode.Noop => dest,
            BlendMode.Set => 0xFFFFFFFF,
            BlendMode.Invert => ~dest | 0xFF000000,

            BlendMode.And => (src & dest) | 0xFF000000,
            BlendMode.Or => (src | dest) | 0xFF000000,
            BlendMode.Xor => (src ^ dest) | 0xFF000000,

            BlendMode.AlphaBlend => AlphaBlend(src, dest),
            BlendMode.AlphaBlendConstant => AlphaBlendConstant(src, dest, constAlpha),

            BlendMode.RgbAdd => RgbAdd(src, dest),
            BlendMode.RgbSubtract => RgbSubtract(src, dest),
            BlendMode.RgbMultiply => RgbMultiply(src, dest),

            _ => src // Default to copy for unknown modes
        };
    }

    private static uint AlphaBlend(uint src, uint dst)
    {
        uint srcA = (src >> 24) & 0xFF;
        if (srcA == 255) return src;
        if (srcA == 0) return dst;

        uint invA = 255 - srcA;

        uint r = (((src >> 16) & 0xFF) * srcA + ((dst >> 16) & 0xFF) * invA) / 255;
        uint g = (((src >> 8) & 0xFF) * srcA + ((dst >> 8) & 0xFF) * invA) / 255;
        uint b = ((src & 0xFF) * srcA + (dst & 0xFF) * invA) / 255;

        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    private static uint AlphaBlendConstant(uint src, uint dst, byte alpha)
    {
        uint a = alpha;
        uint invA = 255 - a;

        uint r = (((src >> 16) & 0xFF) * a + ((dst >> 16) & 0xFF) * invA) / 255;
        uint g = (((src >> 8) & 0xFF) * a + ((dst >> 8) & 0xFF) * invA) / 255;
        uint b = ((src & 0xFF) * a + (dst & 0xFF) * invA) / 255;

        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    private static uint RgbAdd(uint src, uint dst)
    {
        uint r = Math.Min(255, ((src >> 16) & 0xFF) + ((dst >> 16) & 0xFF));
        uint g = Math.Min(255, ((src >> 8) & 0xFF) + ((dst >> 8) & 0xFF));
        uint b = Math.Min(255, (src & 0xFF) + (dst & 0xFF));
        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    private static uint RgbSubtract(uint src, uint dst)
    {
        int r = (int)((dst >> 16) & 0xFF) - (int)((src >> 16) & 0xFF);
        int g = (int)((dst >> 8) & 0xFF) - (int)((src >> 8) & 0xFF);
        int b = (int)(dst & 0xFF) - (int)(src & 0xFF);
        return 0xFF000000 | ((uint)Math.Max(0, r) << 16) | ((uint)Math.Max(0, g) << 8) | (uint)Math.Max(0, b);
    }

    private static uint RgbMultiply(uint src, uint dst)
    {
        uint r = (((src >> 16) & 0xFF) * ((dst >> 16) & 0xFF)) / 255;
        uint g = (((src >> 8) & 0xFF) * ((dst >> 8) & 0xFF)) / 255;
        uint b = ((src & 0xFF) * (dst & 0xFF)) / 255;
        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    public void FillRect(ITexture dest, GraphicsRect rect, Color32 color, BlendMode mode = BlendMode.Copy)
    {
        BitBlt(new BitBltParams
        {
            Destination = dest,
            DestX = rect.X,
            DestY = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            ClipRect = new GraphicsRect(0, 0, dest.Width, dest.Height),
            BlendMode = mode,
            FillColor = color
        });
    }

    public void CopyTexture(ITexture dest, int destX, int destY,
                            ITexture source, int srcX, int srcY,
                            int width, int height)
    {
        BitBlt(new BitBltParams
        {
            Destination = dest,
            Source = source,
            DestX = destX,
            DestY = destY,
            SourceX = srcX,
            SourceY = srcY,
            Width = width,
            Height = height,
            ClipRect = new GraphicsRect(0, 0, dest.Width, dest.Height),
            BlendMode = BlendMode.Copy
        });
    }

    public void Clear(ITexture dest, Color32 color)
    {
        FillRect(dest, new GraphicsRect(0, 0, dest.Width, dest.Height), color);
    }

    public void BeginFrame() { }
    public void EndFrame() { }
    public void Flush() { }

    public void SetDisplayTexture(ITexture display)
    {
        _displayTexture = display;
    }

    public void Present()
    {
        // For software backend, actual presentation is handled by the display provider
    }

    public void InvalidateDisplay(GraphicsRect rect)
    {
        _onDisplayInvalidated?.Invoke(rect);
    }

    public void ConvertPixels(ReadOnlySpan<byte> source, PixelDepth sourceDepth,
                              Span<byte> dest, PixelDepth destDepth,
                              int width, int height)
    {
        if (sourceDepth == destDepth)
        {
            source.CopyTo(dest);
            return;
        }

        // 1-bit to 32-bit
        if (sourceDepth == PixelDepth.OneBit && destDepth == PixelDepth.ThirtyTwoBit)
        {
            var destPixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(dest);
            int srcStride = (width + 7) / 8;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int byteIndex = y * srcStride + x / 8;
                    int bitIndex = 7 - (x % 8);
                    bool bit = (source[byteIndex] >> bitIndex & 1) != 0;
                    destPixels[y * width + x] = bit ? 0xFF000000u : 0xFFFFFFFFu;
                }
            }
        }
        // 32-bit to 1-bit
        else if (sourceDepth == PixelDepth.ThirtyTwoBit && destDepth == PixelDepth.OneBit)
        {
            var srcPixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(source);
            int destStride = (width + 7) / 8;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pixel = srcPixels[y * width + x];
                    byte r = (byte)(pixel >> 16);
                    byte g = (byte)(pixel >> 8);
                    byte b = (byte)pixel;
                    byte lum = (byte)((r * 77 + g * 150 + b * 29) >> 8);

                    int byteIndex = y * destStride + x / 8;
                    int bitIndex = 7 - (x % 8);

                    if (lum < 128)
                        dest[byteIndex] |= (byte)(1 << bitIndex);
                    else
                        dest[byteIndex] &= (byte)~(1 << bitIndex);
                }
            }
        }
        // Add other conversions as needed
    }

    public void Dispose()
    {
        _displayTexture = null;
    }
}
