using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// GDI+/WinForms implementation of IFontBridge.
/// Uses System.Drawing for font operations with antialiased rendering.
/// 32bpp-everywhere: Fonts are rendered to 32bpp bitmaps with antialiasing preserved.
/// </summary>
[SupportedOSPlatform("windows")]
public class WinFormsFontBridge : IFontBridge, IDisposable
{
    private const int FixedDpi = 96;
    private readonly Dictionary<FontCacheKey, StrikeFontData> _fontCache = new();
    private readonly Dictionary<FontDescriptor, Font> _fontObjectCache = new();
    private bool _disposed;

    /// <inheritdoc/>
    public int RenderingDpi => FixedDpi;

    /// <inheritdoc/>
    public bool IsDeterministicMode => true;

    /// <summary>
    /// Gets or creates a GDI+ Font object for the given descriptor.
    /// </summary>
    private Font GetOrCreateFont(FontDescriptor descriptor)
    {
        if (_fontObjectCache.TryGetValue(descriptor, out var cached))
            return cached;

        var style = FontStyle.Regular;
        if (descriptor.Bold) style |= FontStyle.Bold;
        if (descriptor.Italic) style |= FontStyle.Italic;

        // Convert pixel size to points (at 96 DPI, 1 point = 1.333... pixels)
        float pointSize = descriptor.SizePixels * 72f / FixedDpi;

        var font = new Font(descriptor.FamilyName, pointSize, style, GraphicsUnit.Point);
        _fontObjectCache[descriptor] = font;
        return font;
    }

    /// <inheritdoc/>
    public bool ResolveFace(FontDescriptor descriptor)
    {
        try
        {
            using var fontFamily = new FontFamily(descriptor.FamilyName);
            return fontFamily.IsStyleAvailable(GetFontStyle(descriptor));
        }
        catch
        {
            return false;
        }
    }

    private static FontStyle GetFontStyle(FontDescriptor descriptor)
    {
        var style = FontStyle.Regular;
        if (descriptor.Bold) style |= FontStyle.Bold;
        if (descriptor.Italic) style |= FontStyle.Italic;
        return style;
    }

    /// <inheritdoc/>
    public FontMetrics GetMetrics(FontDescriptor descriptor)
    {
        var font = GetOrCreateFont(descriptor);
        var fontFamily = font.FontFamily;
        var style = GetFontStyle(descriptor);

        // Get design units and convert to pixels
        int emHeight = fontFamily.GetEmHeight(style);
        int cellAscent = fontFamily.GetCellAscent(style);
        int cellDescent = fontFamily.GetCellDescent(style);
        int lineSpacing = fontFamily.GetLineSpacing(style);

        // Convert design units to pixels
        float pixelScale = font.Size / emHeight * FixedDpi / 72f;
        int ascent = (int)Math.Ceiling(cellAscent * pixelScale);
        int descent = (int)Math.Ceiling(cellDescent * pixelScale);
        int lineHeight = (int)Math.Ceiling(lineSpacing * pixelScale);
        int leading = lineHeight - ascent - descent;

        // Measure space width and average width
        int spaceWidth = GetCharWidth(descriptor, ' ');
        int avgWidth = GetCharWidth(descriptor, 'x');

        // Calculate max width by measuring some wide characters
        int maxWidth = 0;
        foreach (char c in "MW@#$%&")
        {
            int w = GetCharWidth(descriptor, c);
            if (w > maxWidth) maxWidth = w;
        }

        return new FontMetrics
        {
            Ascent = ascent,
            Descent = descent,
            Leading = Math.Max(0, leading),
            MaxWidth = maxWidth,
            SpaceWidth = spaceWidth,
            AverageWidth = avgWidth,
            SubscriptOffset = descent,
            SuperscriptOffset = ascent / 2
        };
    }

    /// <inheritdoc/>
    public int GetCharWidth(FontDescriptor descriptor, char character)
    {
        var font = GetOrCreateFont(descriptor);

        // Create a small bitmap to measure on
        using var measureBitmap = new Bitmap(1, 1);
        measureBitmap.SetResolution(FixedDpi, FixedDpi);
        using var g = System.Drawing.Graphics.FromImage(measureBitmap);
        ConfigureGraphicsForDeterminism(g);

        // Use MeasureString with StringFormat to get precise width
        var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip
        };

        var size = g.MeasureString(character.ToString(), font, PointF.Empty, format);
        return (int)Math.Ceiling(size.Width);
    }

    /// <inheritdoc/>
    public int GetStringWidth(FontDescriptor descriptor, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var font = GetOrCreateFont(descriptor);

        using var measureBitmap = new Bitmap(1, 1);
        measureBitmap.SetResolution(FixedDpi, FixedDpi);
        using var g = System.Drawing.Graphics.FromImage(measureBitmap);
        ConfigureGraphicsForDeterminism(g);

        var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip
        };

        var size = g.MeasureString(text, font, PointF.Empty, format);
        return (int)Math.Ceiling(size.Width);
    }

    /// <inheritdoc/>
    public GlyphData RenderGlyph(FontDescriptor descriptor, char character)
    {
        var font = GetOrCreateFont(descriptor);
        var metrics = GetMetrics(descriptor);
        int width = GetCharWidth(descriptor, character);

        if (width <= 0) width = 1;

        int height = metrics.Ascent + metrics.Descent;
        if (height <= 0) height = 1;

        // Create bitmap for rendering
        using var bitmap = new Bitmap(width, height);
        bitmap.SetResolution(FixedDpi, FixedDpi);

        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            // Use anti-aliased rendering for smooth fonts
            ConfigureGraphicsForAntialiasing(g);

            // Clear to white (background)
            g.Clear(Color.White);

            // Draw character in black at baseline
            var format = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.NoClip
            };

            g.DrawString(character.ToString(), font, Brushes.Black, 0, 0, format);
        }

        // Convert to 32bpp pixels (preserving anti-aliased grayscale)
        var bitmapData = ConvertBitmapTo32bppPixels(bitmap, width, height);

        return new GlyphData
        {
            Character = character,
            Width = width,
            Bitmap = bitmapData,
            Height = height,
            BitmapWidth = width
        };
    }

    /// <inheritdoc/>
    public StrikeFontData BuildStrikeFont(FontDescriptor descriptor, int minAscii = 0, int maxAscii = 255)
    {
        // Check cache first
        var cacheKey = new FontCacheKey
        {
            FamilyName = descriptor.FamilyName,
            SizePixels = descriptor.SizePixels,
            Dpi = FixedDpi,
            Bold = descriptor.Bold,
            Italic = descriptor.Italic,
            HintingEnabled = false
        };

        if (_fontCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var metrics = GetMetrics(descriptor);
        int charCount = maxAscii - minAscii + 1;
        int fontHeight = metrics.Ascent + metrics.Descent;

        var font = GetOrCreateFont(descriptor);

        // For fixed-pitch fonts, use uniform cell width from a representative character
        // This ensures proper alignment - all characters placed at exact cell boundaries
        int cellWidth = GetCellWidth(font);

        // Build xTable with uniform cell widths (fixed-pitch font)
        int[] widths = new int[charCount];
        int[] xTable = new int[charCount + 2];
        for (int i = 0; i < charCount; i++)
        {
            widths[i] = cellWidth;
            xTable[i] = i * cellWidth;
        }
        xTable[charCount] = charCount * cellWidth;
        xTable[charCount + 1] = charCount * cellWidth;

        int strikeLength = charCount * cellWidth;

        // Create the combined glyphs bitmap with explicit 32bpp format for anti-aliasing
        using var glyphsBitmap = new Bitmap(strikeLength, fontHeight, PixelFormat.Format32bppArgb);
        glyphsBitmap.SetResolution(FixedDpi, FixedDpi);

        using (var g = System.Drawing.Graphics.FromImage(glyphsBitmap))
        {
            // Use full anti-aliasing settings for smooth edges
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.White);

            // GenericTypographic format with no extra spacing
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.FitBlackBox,
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near
            };

            // Draw each character at its exact cell position
            for (int i = 0; i < charCount; i++)
            {
                char c = (char)(minAscii + i);
                // Skip control characters (0-31) - they render as boxes or nothing
                if (c < 32) continue;
                // Replace underscore with left arrow (Smalltalk convention)
                if (c == '_') c = '←';
                g.DrawString(c.ToString(), font, Brushes.Black, i * cellWidth, 0, format);
            }
        }

        // Convert entire bitmap to 32bpp pixels
        uint[] glyphsData = ConvertBitmapTo32bppPixels(glyphsBitmap, strikeLength, fontHeight);

        var strikeFontData = new StrikeFontData
        {
            Name = $"{descriptor.FamilyName}-{descriptor.SizePixels}",
            Metrics = metrics,
            MinAscii = minAscii,
            MaxAscii = maxAscii,
            XTable = xTable,
            GlyphsBitmap = glyphsData,
            StrikeLength = strikeLength,
            XOffset = 0,
            Emphasis = (descriptor.Bold ? 1 : 0) | (descriptor.Italic ? 2 : 0),
            Type = 0,
            WidthTable = widths
        };

        _fontCache[cacheKey] = strikeFontData;
        return strikeFontData;
    }

    /// <inheritdoc/>
    public FontIdentity GetFontIdentity(FontDescriptor descriptor)
    {
        var font = GetOrCreateFont(descriptor);
        return new FontIdentity
        {
            FamilyName = font.FontFamily.Name,
            FilePath = null, // GDI+ doesn't expose font file path easily
            FileHash = null,
            Weight = descriptor.Bold ? 700 : 400,
            Style = descriptor.Italic ? "Italic" : "Normal",
            Dpi = FixedDpi,
            HintingEnabled = false
        };
    }

    /// <summary>
    /// Gets the fixed-pitch cell width for a monospace font.
    /// Uses GDI+ MeasureString with GenericTypographic for accurate width.
    /// </summary>
    private int GetCellWidth(Font font)
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetResolution(FixedDpi, FixedDpi);
        using var g = System.Drawing.Graphics.FromImage(bitmap);

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.MeasureTrailingSpaces
        };

        // Measure 'M' - a representative full-width character
        var size = g.MeasureString("M", font, PointF.Empty, format);
        return (int)Math.Ceiling(size.Width);
    }

    /// <summary>
    /// Configures Graphics for deterministic rendering (no antialiasing, fixed DPI).
    /// Used for measurement and legacy compatibility.
    /// </summary>
    private static void ConfigureGraphicsForDeterminism(System.Drawing.Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.CompositingQuality = CompositingQuality.HighSpeed;
    }

    /// <summary>
    /// Configures Graphics for antialiased rendering.
    /// 32bpp-everywhere: Enables smooth font rendering with grayscale antialiasing.
    /// </summary>
    private static void ConfigureGraphicsForAntialiasing(System.Drawing.Graphics g)
    {
        // Use grayscale antialiasing (not ClearType) for platform-independent rendering
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    /// <summary>
    /// Converts a bitmap to RGB pixels (one uint per pixel, no alpha).
    /// Preserves anti-aliased grayscale values for smooth font rendering.
    /// Standard RGB: 0x000000=black, 0xFFFFFF=white.
    /// </summary>
    private static uint[] ConvertBitmapTo32bppPixels(Bitmap bitmap, int width, int height)
    {
        // Standard RGB: one uint per pixel (24 bits used)
        uint[] result = new uint[width * height];

        // Use LockBits for fast pixel access
        BitmapData bmpData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            bitmap.PixelFormat);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                int stride = bmpData.Stride;

                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + (y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + (x * bytesPerPixel);
                        // Get RGB values (works for 24bpp and 32bpp formats)
                        byte b = pixel[0];
                        byte g = pixel[1];
                        byte r = pixel[2];

                        // Standard RGB format (no inversion)
                        // Preserves anti-aliased grayscale for smooth font edges
                        uint rgb = ((uint)r << 16) | ((uint)g << 8) | b;
                        result[y * width + x] = rgb;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var font in _fontObjectCache.Values)
        {
            font.Dispose();
        }
        _fontObjectCache.Clear();
        _fontCache.Clear();

        GC.SuppressFinalize(this);
    }
}
