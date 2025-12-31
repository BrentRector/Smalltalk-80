using System.Runtime.Versioning;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// Null implementation of IFontBridge for headless/non-Windows operation.
/// Provides stub font data with minimal metrics for basic text layout.
/// </summary>
public class NullFontBridge : IFontBridge
{
    /// <inheritdoc/>
    public int RenderingDpi => 96;

    /// <inheritdoc/>
    public bool IsDeterministicMode => true;

    /// <inheritdoc/>
    public bool ResolveFace(FontDescriptor descriptor) => true;

    /// <inheritdoc/>
    public FontMetrics GetMetrics(FontDescriptor descriptor)
    {
        // Approximate metrics for monospace font
        int charWidth = descriptor.SizePixels * 6 / 10; // ~0.6 em
        int ascent = descriptor.SizePixels * 8 / 10;
        int descent = descriptor.SizePixels * 2 / 10;

        return new FontMetrics
        {
            Ascent = ascent,
            Descent = descent,
            Leading = 1,
            MaxWidth = charWidth,
            SpaceWidth = charWidth,
            AverageWidth = charWidth,
            SubscriptOffset = descent,
            SuperscriptOffset = ascent / 2
        };
    }

    /// <inheritdoc/>
    public int GetCharWidth(FontDescriptor descriptor, char character)
    {
        // Monospace: all characters same width
        return descriptor.SizePixels * 6 / 10;
    }

    /// <inheritdoc/>
    public int GetStringWidth(FontDescriptor descriptor, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length * GetCharWidth(descriptor, ' ');
    }

    /// <inheritdoc/>
    public GlyphData RenderGlyph(FontDescriptor descriptor, char character)
    {
        var metrics = GetMetrics(descriptor);
        int width = GetCharWidth(descriptor, character);
        int height = metrics.Ascent + metrics.Descent;

        // Create empty 1bpp bitmap
        int wordsPerRow = (width + 15) / 16;
        var bitmap = new ushort[wordsPerRow * height];

        return new GlyphData
        {
            Character = character,
            Width = width,
            Bitmap = bitmap,
            Height = height,
            BitmapWidth = width
        };
    }

    /// <inheritdoc/>
    public StrikeFontData BuildStrikeFont(FontDescriptor descriptor, int minAscii = 0, int maxAscii = 255)
    {
        var metrics = GetMetrics(descriptor);
        int charCount = maxAscii - minAscii + 1;
        int charWidth = GetCharWidth(descriptor, ' ');

        // Build xTable with uniform widths
        // Smalltalk widthOf: can access xTable at: (maxAscii + 1) + 2, so we need charCount + 2 entries
        int[] xTable = new int[charCount + 2];
        int[] widthTable = new int[charCount];
        for (int i = 0; i <= charCount + 1; i++)
        {
            xTable[i] = Math.Min(i, charCount) * charWidth;
            if (i < charCount) widthTable[i] = charWidth;
        }

        int strikeLength = charCount * charWidth;
        int fontHeight = metrics.Ascent + metrics.Descent;
        int raster = (strikeLength + 15) / 16;

        // Create empty glyph bitmap
        var glyphsBitmap = new ushort[raster * fontHeight];

        return new StrikeFontData
        {
            Name = $"{descriptor.FamilyName}-{descriptor.SizePixels}",
            Metrics = metrics,
            MinAscii = minAscii,
            MaxAscii = maxAscii,
            XTable = xTable,
            GlyphsBitmap = glyphsBitmap,
            StrikeLength = strikeLength,
            XOffset = 0,
            Emphasis = (descriptor.Bold ? 1 : 0) | (descriptor.Italic ? 2 : 0),
            Type = 0,
            WidthTable = widthTable
        };
    }

    /// <inheritdoc/>
    public FontIdentity GetFontIdentity(FontDescriptor descriptor)
    {
        return new FontIdentity
        {
            FamilyName = descriptor.FamilyName,
            FilePath = null,
            FileHash = null,
            Weight = descriptor.Bold ? 700 : 400,
            Style = descriptor.Italic ? "Italic" : "Normal",
            Dpi = RenderingDpi,
            HintingEnabled = false
        };
    }
}

/// <summary>
/// Factory for creating appropriate IFontBridge based on platform and configuration.
/// </summary>
public static class FontBridgeFactory
{
    /// <summary>
    /// EXPERIMENTAL: Set to true to try using GDI bitmap font (Fixedsys) instead of TrueType.
    /// This provides pixel-perfect 1bpp rendering without antialiasing artifacts.
    /// Set to false to use the standard WinForms/GDI+ font rendering.
    /// </summary>
    public static bool UseGdiBitmapFonts { get; set; } = false;

    /// <summary>
    /// The name of the bitmap font to use when UseGdiBitmapFonts is true.
    /// Default is "Fixedsys" - a classic Windows bitmap font.
    /// </summary>
    public static string GdiBitmapFontName { get; set; } = "Fixedsys";

    /// <summary>
    /// Creates the appropriate font bridge for the current platform.
    /// </summary>
    /// <returns>IFontBridge implementation suitable for the platform.</returns>
    public static IFontBridge Create()
    {
        // Check if we're on Windows with GUI support
        if (OperatingSystem.IsWindows())
        {
            // EXPERIMENTAL: Try GDI bitmap font first if enabled
            if (UseGdiBitmapFonts)
            {
                var gdiBridge = TryCreateGdiBitmapBridge();
                if (gdiBridge != null)
                    return gdiBridge;

                // Fall through to WinForms if GDI bitmap font not available
                Console.WriteLine($"[FontBridgeFactory] GDI bitmap font '{GdiBitmapFontName}' not available, falling back to WinForms.");
            }

            try
            {
                // Try to create WinForms bridge
                var bridge = new WinFormsFontBridge();

                // Verify it can resolve a common font
                if (bridge.ResolveFace(new FontDescriptor("Arial", 12)))
                {
                    return bridge;
                }

                bridge.Dispose();
            }
            catch
            {
                // Fall through to null bridge
            }
        }

        // Fallback to null bridge for non-Windows or headless operation
        return new NullFontBridge();
    }

    /// <summary>
    /// Creates a null font bridge for testing or headless operation.
    /// </summary>
    public static IFontBridge CreateNull() => new NullFontBridge();

    /// <summary>
    /// Creates WinForms font bridge (Windows only).
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown on non-Windows platforms.</exception>
    public static IFontBridge CreateWinForms()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WinFormsFontBridge requires Windows.");
        }
        return new WinFormsFontBridge();
    }

    /// <summary>
    /// Creates GDI bitmap font bridge (Windows only).
    /// Returns null if the font is not available.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IFontBridge? CreateGdiBitmap(string fontName = "Fixedsys")
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("GdiBitmapFontBridge requires Windows.");
        }

#if WINDOWS
        var bridge = new GdiBitmapFontBridge(fontName);
        if (bridge.IsFontAvailable)
            return bridge;

        bridge.Dispose();
#endif
        return null;
    }

    /// <summary>
    /// Attempts to create a GDI bitmap font bridge.
    /// Returns null if not on Windows or font not available.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static IFontBridge? TryCreateGdiBitmapBridge()
    {
#if WINDOWS
        try
        {
            var bridge = new GdiBitmapFontBridge(GdiBitmapFontName);
            if (bridge.IsFontAvailable)
            {
                Console.WriteLine($"[FontBridgeFactory] Using GDI bitmap font: {GdiBitmapFontName}");
                return bridge;
            }
            bridge.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FontBridgeFactory] Failed to create GDI bitmap bridge: {ex.Message}");
        }
#endif
        return null;
    }
}
