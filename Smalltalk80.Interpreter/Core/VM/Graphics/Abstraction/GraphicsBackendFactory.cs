// Graphics Abstraction Layer - Backend Factory
// Runtime selection of graphics implementation for testing and optimization

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Available graphics backend types.
/// </summary>
public enum GraphicsBackendType
{
    /// <summary>Automatic selection based on platform capabilities</summary>
    Auto,

    /// <summary>CPU-based software rendering (always available)</summary>
    Software,

    /// <summary>SkiaSharp GPU-accelerated rendering</summary>
    SkiaSharp,

    /// <summary>Direct2D GPU-accelerated rendering (Windows only)</summary>
    Direct2D,

    /// <summary>OpenGL GPU-accelerated rendering</summary>
    OpenGL,

    /// <summary>Null backend for headless/testing</summary>
    Null
}

/// <summary>
/// Configuration for graphics backend creation.
/// </summary>
public sealed class GraphicsBackendOptions
{
    /// <summary>Preferred backend type</summary>
    public GraphicsBackendType PreferredBackend { get; set; } = GraphicsBackendType.Auto;

    /// <summary>Fallback to software if preferred backend fails</summary>
    public bool AllowFallback { get; set; } = true;

    /// <summary>Enable performance statistics collection</summary>
    public bool EnableStats { get; set; } = true;

    /// <summary>Default pixel depth for new textures</summary>
    public PixelDepth DefaultDepth { get; set; } = PixelDepth.OneBit;

    /// <summary>Target pixel depth for display</summary>
    public PixelDepth DisplayDepth { get; set; } = PixelDepth.ThirtyTwoBit;

    /// <summary>Callback when display region is invalidated</summary>
    public Action<GraphicsRect>? OnDisplayInvalidated { get; set; }

    /// <summary>Enable debug validation</summary>
    public bool DebugValidation { get; set; }
}

/// <summary>
/// Factory for creating graphics backends.
/// Supports runtime switching for testing different implementations.
/// </summary>
public static class GraphicsBackendFactory
{
    private static IGraphicsBackend? _currentBackend;
    private static readonly object _lock = new();

    /// <summary>
    /// Currently active graphics backend.
    /// </summary>
    public static IGraphicsBackend? Current
    {
        get
        {
            lock (_lock)
            {
                return _currentBackend;
            }
        }
    }

    /// <summary>
    /// Create a graphics backend with the specified options.
    /// </summary>
    public static IGraphicsBackend Create(GraphicsBackendOptions? options = null)
    {
        options ??= new GraphicsBackendOptions();

        var backendType = options.PreferredBackend;

        if (backendType == GraphicsBackendType.Auto)
        {
            backendType = DetectBestBackend();
        }

        IGraphicsBackend? backend = null;

        try
        {
            backend = backendType switch
            {
                GraphicsBackendType.Software => CreateSoftwareBackend(options),
                GraphicsBackendType.SkiaSharp => CreateSkiaSharpBackend(options),
                GraphicsBackendType.Direct2D => CreateDirect2DBackend(options),
                GraphicsBackendType.OpenGL => CreateOpenGLBackend(options),
                GraphicsBackendType.Null => CreateNullBackend(options),
                _ => CreateSoftwareBackend(options)
            };
        }
        catch (Exception ex) when (options.AllowFallback && backendType != GraphicsBackendType.Software)
        {
            // Log the failure and fall back to software
            System.Diagnostics.Debug.WriteLine($"Failed to create {backendType} backend: {ex.Message}");
            System.Diagnostics.Debug.WriteLine("Falling back to software backend");
            backend = CreateSoftwareBackend(options);
        }

        return backend ?? CreateSoftwareBackend(options);
    }

    /// <summary>
    /// Create and set as the current global backend.
    /// </summary>
    public static IGraphicsBackend CreateAndSetCurrent(GraphicsBackendOptions? options = null)
    {
        var backend = Create(options);

        lock (_lock)
        {
            _currentBackend?.Dispose();
            _currentBackend = backend;
        }

        return backend;
    }

    /// <summary>
    /// Set the current global backend (for dependency injection scenarios).
    /// </summary>
    public static void SetCurrent(IGraphicsBackend backend)
    {
        lock (_lock)
        {
            if (_currentBackend != backend)
            {
                _currentBackend?.Dispose();
                _currentBackend = backend;
            }
        }
    }

    /// <summary>
    /// Clear the current backend.
    /// </summary>
    public static void ClearCurrent()
    {
        lock (_lock)
        {
            _currentBackend?.Dispose();
            _currentBackend = null;
        }
    }

    /// <summary>
    /// Check if a specific backend type is available on this platform.
    /// </summary>
    public static bool IsBackendAvailable(GraphicsBackendType type)
    {
        return type switch
        {
            GraphicsBackendType.Software => true,
            GraphicsBackendType.Null => true,
            GraphicsBackendType.SkiaSharp => IsSkiaSharpAvailable(),
            GraphicsBackendType.Direct2D => IsDirect2DAvailable(),
            GraphicsBackendType.OpenGL => IsOpenGLAvailable(),
            _ => false
        };
    }

    /// <summary>
    /// Get list of all available backend types on this platform.
    /// </summary>
    public static IReadOnlyList<GraphicsBackendType> GetAvailableBackends()
    {
        var available = new List<GraphicsBackendType>();

        foreach (GraphicsBackendType type in Enum.GetValues<GraphicsBackendType>())
        {
            if (type != GraphicsBackendType.Auto && IsBackendAvailable(type))
            {
                available.Add(type);
            }
        }

        return available;
    }

    /// <summary>
    /// Detect the best available backend for this platform.
    /// </summary>
    private static GraphicsBackendType DetectBestBackend()
    {
        // Priority order for best performance:
        // 1. Direct2D (Windows, best performance)
        // 2. SkiaSharp (cross-platform, good performance)
        // 3. OpenGL (cross-platform, good performance)
        // 4. Software (always available)

        if (OperatingSystem.IsWindows() && IsDirect2DAvailable())
            return GraphicsBackendType.Direct2D;

        if (IsSkiaSharpAvailable())
            return GraphicsBackendType.SkiaSharp;

        if (IsOpenGLAvailable())
            return GraphicsBackendType.OpenGL;

        return GraphicsBackendType.Software;
    }

    private static IGraphicsBackend CreateSoftwareBackend(GraphicsBackendOptions options)
    {
        return new SoftwareGraphicsBackend(options.OnDisplayInvalidated);
    }

    private static IGraphicsBackend CreateSkiaSharpBackend(GraphicsBackendOptions options)
    {
        // TODO: Implement SkiaSharpGraphicsBackend
        // For now, return software backend
        throw new NotImplementedException("SkiaSharp backend not yet implemented. " +
            "Add SkiaSharp NuGet package and implement SkiaSharpGraphicsBackend class.");
    }

    private static IGraphicsBackend CreateDirect2DBackend(GraphicsBackendOptions options)
    {
        // TODO: Implement Direct2DGraphicsBackend
        // For now, return software backend
        throw new NotImplementedException("Direct2D backend not yet implemented. " +
            "Add d2dlib or Vortice.Direct2D1 NuGet package and implement Direct2DGraphicsBackend class.");
    }

    private static IGraphicsBackend CreateOpenGLBackend(GraphicsBackendOptions options)
    {
        // TODO: Implement OpenGLGraphicsBackend
        // For now, return software backend
        throw new NotImplementedException("OpenGL backend not yet implemented. " +
            "Add OpenTK NuGet package and implement OpenGLGraphicsBackend class.");
    }

    private static IGraphicsBackend CreateNullBackend(GraphicsBackendOptions options)
    {
        return new NullGraphicsBackend();
    }

    private static bool IsSkiaSharpAvailable()
    {
        try
        {
            // Check if SkiaSharp assembly is loadable
            var type = Type.GetType("SkiaSharp.SKCanvas, SkiaSharp");
            return type != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDirect2DAvailable()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // Check if d2dlib or Vortice.Direct2D1 is available
            var type = Type.GetType("unvell.D2DLib.D2DDevice, d2dlib") ??
                       Type.GetType("Vortice.Direct2D1.ID2D1Factory, Vortice.Direct2D1");
            return type != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOpenGLAvailable()
    {
        try
        {
            // Check if OpenTK is available
            var type = Type.GetType("OpenTK.Graphics.OpenGL4.GL, OpenTK.Graphics");
            return type != null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Null graphics backend for headless/testing scenarios.
/// All operations are no-ops.
/// </summary>
public sealed class NullGraphicsBackend : IGraphicsBackend
{
    private BackendStats _stats;

    public string Name => "Null (No-op)";
    public BackendCapabilities Capabilities => BackendCapabilities.All;
    public ref BackendStats Stats => ref _stats;

    public ITexture CreateTexture(int width, int height, PixelDepth depth, bool isRenderTarget = true)
        => new NullTexture(width, height, depth);

    public ITexture CreateTextureFromData(int width, int height, PixelDepth depth,
                                           ReadOnlySpan<byte> data, bool isRenderTarget = true)
        => new NullTexture(width, height, depth);

    public void BitBlt(in BitBltParams @params) { }
    public void FillRect(ITexture dest, GraphicsRect rect, Color32 color, BlendMode mode = BlendMode.Copy) { }
    public void CopyTexture(ITexture dest, int destX, int destY, ITexture source, int srcX, int srcY, int width, int height) { }
    public void Clear(ITexture dest, Color32 color) { }
    public void BeginFrame() { }
    public void EndFrame() { }
    public void Flush() { }
    public void SetDisplayTexture(ITexture display) { }
    public void Present() { }
    public void InvalidateDisplay(GraphicsRect rect) { }
    public void ConvertPixels(ReadOnlySpan<byte> source, PixelDepth sourceDepth, Span<byte> dest, PixelDepth destDepth, int width, int height) { }
    public void Dispose() { }

    private sealed class NullTexture : ITexture
    {
        public int Width { get; }
        public int Height { get; }
        public PixelDepth Depth { get; }
        public int Stride => 0;
        public bool IsRenderTarget => true;

        public NullTexture(int width, int height, PixelDepth depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }

        public ITextureLock Lock(TextureAccess access) => new NullTextureLock();
        public ReadOnlyMemory<byte>? TryGetPixelData() => null;
        public void Invalidate(int x, int y, int width, int height) { }
        public void SetPixels(ReadOnlySpan<byte> data) { }
        public void SetPixels(int x, int y, int width, int height, ReadOnlySpan<byte> data) { }
        public void Dispose() { }

        private sealed class NullTextureLock : ITextureLock
        {
            public nint DataPointer => 0;
            public Span<byte> Data => Span<byte>.Empty;
            public int Stride => 0;
            public void Dispose() { }
        }
    }
}
