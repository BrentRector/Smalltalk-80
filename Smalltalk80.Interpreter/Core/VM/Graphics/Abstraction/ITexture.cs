// Graphics Abstraction Layer - Texture Interface
// Abstracts bitmap/surface storage for different backends

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Pixel depth for textures and surfaces.
/// </summary>
public enum PixelDepth
{
    /// <summary>1 bit per pixel - monochrome (original Smalltalk-80)</summary>
    OneBit = 1,

    /// <summary>8 bits per pixel - grayscale or indexed color</summary>
    EightBit = 8,

    /// <summary>32 bits per pixel - ARGB (target for modern rendering)</summary>
    ThirtyTwoBit = 32
}

/// <summary>
/// Represents a 2D bitmap/texture surface.
/// Implementations may store data in CPU memory, GPU memory, or both.
/// </summary>
public interface ITexture : IDisposable
{
    /// <summary>Width in pixels</summary>
    int Width { get; }

    /// <summary>Height in pixels</summary>
    int Height { get; }

    /// <summary>Bits per pixel (1, 8, or 32)</summary>
    PixelDepth Depth { get; }

    /// <summary>
    /// Row stride in bytes (may include padding for alignment).
    /// For GPU textures this may differ from width * bytesPerPixel.
    /// </summary>
    int Stride { get; }

    /// <summary>
    /// Whether this texture can be used as a render target (destination).
    /// </summary>
    bool IsRenderTarget { get; }

    /// <summary>
    /// Lock the texture for CPU read/write access.
    /// For GPU textures, this may involve a synchronization/download.
    /// </summary>
    /// <param name="access">Type of access needed</param>
    /// <returns>Lock context that must be disposed when done</returns>
    ITextureLock Lock(TextureAccess access);

    /// <summary>
    /// Get a read-only view of the pixel data (if available without lock).
    /// Returns null if data is not directly accessible.
    /// </summary>
    ReadOnlyMemory<byte>? TryGetPixelData();

    /// <summary>
    /// Invalidate a region to trigger re-upload to GPU (if applicable).
    /// </summary>
    void Invalidate(int x, int y, int width, int height);

    /// <summary>
    /// Upload pixel data to the texture.
    /// For CPU textures, this copies data.
    /// For GPU textures, this may trigger an upload.
    /// </summary>
    void SetPixels(ReadOnlySpan<byte> data);

    /// <summary>
    /// Upload pixel data to a region of the texture.
    /// </summary>
    void SetPixels(int x, int y, int width, int height, ReadOnlySpan<byte> data);
}

/// <summary>
/// Type of access when locking a texture.
/// </summary>
public enum TextureAccess
{
    /// <summary>Read-only access</summary>
    Read,

    /// <summary>Write-only access (contents may be undefined)</summary>
    Write,

    /// <summary>Read and write access</summary>
    ReadWrite
}

/// <summary>
/// Lock context for accessing texture pixel data.
/// Dispose when done to release the lock.
/// </summary>
public interface ITextureLock : IDisposable
{
    /// <summary>Pointer to pixel data (for unsafe access)</summary>
    nint DataPointer { get; }

    /// <summary>Span view of pixel data</summary>
    Span<byte> Data { get; }

    /// <summary>Row stride in bytes</summary>
    int Stride { get; }
}

/// <summary>
/// A simple CPU-based texture implementation.
/// Stores bitmap data in managed memory.
/// </summary>
public sealed class CpuTexture : ITexture
{
    private readonly byte[] _data;
    private readonly int _stride;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public PixelDepth Depth { get; }
    public int Stride => _stride;
    public bool IsRenderTarget => true;

    public CpuTexture(int width, int height, PixelDepth depth)
    {
        Width = width;
        Height = height;
        Depth = depth;

        // Calculate stride with alignment
        int bitsPerPixel = (int)depth;
        int bitsPerRow = width * bitsPerPixel;

        // Align to 32-bit boundaries for performance
        _stride = depth switch
        {
            PixelDepth.OneBit => ((width + 31) / 32) * 4,      // 32-bit aligned
            PixelDepth.EightBit => ((width + 3) / 4) * 4,     // 32-bit aligned
            PixelDepth.ThirtyTwoBit => width * 4,              // Already 32-bit per pixel
            _ => throw new ArgumentException($"Unsupported depth: {depth}")
        };

        _data = new byte[_stride * height];
    }

    public ITextureLock Lock(TextureAccess access) => new CpuTextureLock(_data, _stride);

    public ReadOnlyMemory<byte>? TryGetPixelData() => _data;

    public void Invalidate(int x, int y, int width, int height)
    {
        // No-op for CPU textures - data is always current
    }

    public void SetPixels(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_data);
    }

    public void SetPixels(int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        int bytesPerPixel = (int)Depth / 8;
        if (bytesPerPixel == 0) bytesPerPixel = 1; // For 1-bit, handle specially

        int srcStride = width * bytesPerPixel;
        int destOffset = y * _stride + x * bytesPerPixel;

        for (int row = 0; row < height; row++)
        {
            var srcRow = data.Slice(row * srcStride, srcStride);
            srcRow.CopyTo(_data.AsSpan(destOffset + row * _stride, srcStride));
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private sealed class CpuTextureLock : ITextureLock
    {
        private readonly byte[] _data;

        public CpuTextureLock(byte[] data, int stride)
        {
            _data = data;
            Stride = stride;
        }

        public nint DataPointer => throw new NotSupportedException("Use Data span instead");
        public Span<byte> Data => _data;
        public int Stride { get; }

        public void Dispose() { }
    }
}
