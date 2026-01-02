namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// Describes a font to be resolved by the font bridge.
/// </summary>
public readonly struct FontDescriptor
{
    /// <summary>Font family name (e.g., "Courier", "Arial").</summary>
    public string FamilyName { get; init; }

    /// <summary>Size in pixels (not points).</summary>
    public int SizePixels { get; init; }

    /// <summary>Whether to use bold weight.</summary>
    public bool Bold { get; init; }

    /// <summary>Whether to use italic style.</summary>
    public bool Italic { get; init; }

    public FontDescriptor(string familyName, int sizePixels, bool bold = false, bool italic = false)
    {
        FamilyName = familyName;
        SizePixels = sizePixels;
        Bold = bold;
        Italic = italic;
    }

    public override string ToString() =>
        $"{FamilyName} {SizePixels}px{(Bold ? " Bold" : "")}{(Italic ? " Italic" : "")}";
}

/// <summary>
/// Font metrics returned by the font bridge.
/// All values are in pixels at the fixed DPI (96).
/// </summary>
public readonly struct FontMetrics
{
    /// <summary>Height above baseline in pixels.</summary>
    public int Ascent { get; init; }

    /// <summary>Height below baseline in pixels.</summary>
    public int Descent { get; init; }

    /// <summary>Extra space between lines in pixels.</summary>
    public int Leading { get; init; }

    /// <summary>Total line height (ascent + descent + leading).</summary>
    public int LineHeight => Ascent + Descent + Leading;

    /// <summary>Maximum character width in pixels.</summary>
    public int MaxWidth { get; init; }

    /// <summary>Width of space character in pixels.</summary>
    public int SpaceWidth { get; init; }

    /// <summary>Average character width in pixels.</summary>
    public int AverageWidth { get; init; }

    /// <summary>Offset for subscript characters.</summary>
    public int SubscriptOffset { get; init; }

    /// <summary>Offset for superscript characters.</summary>
    public int SuperscriptOffset { get; init; }
}

/// <summary>
/// Rendered glyph data for a single character.
/// RGB-only: Each pixel is stored as one uint (0x00RRGGBB, alpha implicit).
/// </summary>
public readonly struct GlyphData
{
    /// <summary>Character this glyph represents.</summary>
    public char Character { get; init; }

    /// <summary>Width of this character in pixels.</summary>
    public int Width { get; init; }

    /// <summary>
    /// Bitmap data for the glyph (24 bits per pixel, stored in uint).
    /// Each uint is one pixel in 0x00RRGGBB format (alpha implicit).
    /// Array size is Width * Height.
    /// </summary>
    public uint[] Bitmap { get; init; }

    /// <summary>Height of the glyph bitmap in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Width of the glyph bitmap in pixels (may differ from character Width).</summary>
    public int BitmapWidth { get; init; }
}

/// <summary>
/// Complete StrikeFont data structure ready to be converted to Smalltalk objects.
/// Contains all metrics and glyph data for a font at a specific size.
/// </summary>
public class StrikeFontData
{
    /// <summary>Font name (e.g., "Courier-12").</summary>
    public string Name { get; init; } = "";

    /// <summary>Font metrics.</summary>
    public FontMetrics Metrics { get; init; }

    /// <summary>First ASCII code in the font (typically 0).</summary>
    public int MinAscii { get; init; }

    /// <summary>Last ASCII code in the font (typically 255).</summary>
    public int MaxAscii { get; init; }

    /// <summary>
    /// X-coordinates table: xTable[i] gives the left x-coordinate of character i in the glyphs form.
    /// Array size is (maxAscii - minAscii + 2) to include the sentinel value.
    /// </summary>
    public int[] XTable { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Combined glyph bitmap (RGB-only). All glyphs placed side by side.
    /// Height is font height (ascent + descent), width is strikeLength.
    /// Each uint is one pixel in 0x00RRGGBB format (alpha implicit).
    /// Array size is StrikeLength * StrikeHeight.
    /// Antialiased grayscale values are preserved for smooth rendering.
    /// </summary>
    public uint[] GlyphsBitmap { get; init; } = Array.Empty<uint>();

    /// <summary>Width of the combined glyphs form in pixels.</summary>
    public int StrikeLength { get; init; }

    /// <summary>Height of the combined glyphs form in pixels.</summary>
    public int StrikeHeight => Metrics.Ascent + Metrics.Descent;

    /// <summary>Number of 32-bit words per row in the glyphs form. For 32bpp, equals StrikeLength.</summary>
    public int Raster => StrikeLength;

    /// <summary>X offset for rendering (typically 0).</summary>
    public int XOffset { get; init; }

    /// <summary>Emphasis code (0 = normal, 1 = bold, 2 = italic, etc.).</summary>
    public int Emphasis { get; init; }

    /// <summary>Font type code (typically 0 for StrikeFont).</summary>
    public int Type { get; init; }

    /// <summary>
    /// Per-character width table for quick lookup.
    /// widthTable[char - minAscii] gives the pixel width of that character.
    /// </summary>
    public int[] WidthTable { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Font identity information for reproducibility tracking.
/// Used to ensure deterministic font rendering across machines.
/// </summary>
public readonly struct FontIdentity
{
    /// <summary>Font family name.</summary>
    public string FamilyName { get; init; }

    /// <summary>Font file path (if available).</summary>
    public string? FilePath { get; init; }

    /// <summary>Hash of font file for verification.</summary>
    public string? FileHash { get; init; }

    /// <summary>Font weight (400 = normal, 700 = bold).</summary>
    public int Weight { get; init; }

    /// <summary>Font style.</summary>
    public string Style { get; init; }

    /// <summary>DPI used for rendering.</summary>
    public int Dpi { get; init; }

    /// <summary>Whether hinting was enabled.</summary>
    public bool HintingEnabled { get; init; }
}

/// <summary>
/// Cache key for font data to ensure deterministic results.
/// </summary>
public readonly struct FontCacheKey : IEquatable<FontCacheKey>
{
    public string FamilyName { get; init; }
    public int SizePixels { get; init; }
    public int Dpi { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool HintingEnabled { get; init; }

    public bool Equals(FontCacheKey other) =>
        FamilyName == other.FamilyName &&
        SizePixels == other.SizePixels &&
        Dpi == other.Dpi &&
        Bold == other.Bold &&
        Italic == other.Italic &&
        HintingEnabled == other.HintingEnabled;

    public override bool Equals(object? obj) => obj is FontCacheKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(FamilyName, SizePixels, Dpi, Bold, Italic, HintingEnabled);
}

/// <summary>
/// Interface for abstracting font operations from the platform.
/// Implementations can use GDI+, DirectWrite, or other font systems.
/// </summary>
public interface IFontBridge
{
    /// <summary>
    /// Resolves a font descriptor to verify the font exists and get actual metrics.
    /// </summary>
    /// <param name="descriptor">Font to resolve.</param>
    /// <returns>True if font was found, false otherwise.</returns>
    bool ResolveFace(FontDescriptor descriptor);

    /// <summary>
    /// Gets metrics for a font.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <returns>Font metrics.</returns>
    FontMetrics GetMetrics(FontDescriptor descriptor);

    /// <summary>
    /// Gets the pixel width of a single character.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <param name="character">Character to measure.</param>
    /// <returns>Width in pixels.</returns>
    int GetCharWidth(FontDescriptor descriptor, char character);

    /// <summary>
    /// Gets the pixel width of a string.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <param name="text">String to measure.</param>
    /// <returns>Width in pixels.</returns>
    int GetStringWidth(FontDescriptor descriptor, string text);

    /// <summary>
    /// Renders a single glyph to an RGB bitmap with antialiased grayscale.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <param name="character">Character to render.</param>
    /// <returns>Glyph data with RGB bitmap (one uint per pixel, alpha implicit).</returns>
    GlyphData RenderGlyph(FontDescriptor descriptor, char character);

    /// <summary>
    /// Builds a complete StrikeFont with all glyphs for ASCII range.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <param name="minAscii">First ASCII code (typically 0).</param>
    /// <param name="maxAscii">Last ASCII code (typically 255).</param>
    /// <returns>Complete StrikeFont data.</returns>
    StrikeFontData BuildStrikeFont(FontDescriptor descriptor, int minAscii = 0, int maxAscii = 255);

    /// <summary>
    /// Gets font identity information for reproducibility tracking.
    /// </summary>
    /// <param name="descriptor">Font descriptor.</param>
    /// <returns>Font identity information.</returns>
    FontIdentity GetFontIdentity(FontDescriptor descriptor);

    /// <summary>
    /// Gets the DPI used for font rendering (should be fixed at 96 for determinism).
    /// </summary>
    int RenderingDpi { get; }

    /// <summary>
    /// Gets whether ClearType/antialiasing is disabled for deterministic rendering.
    /// </summary>
    bool IsDeterministicMode { get; }
}
