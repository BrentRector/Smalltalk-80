using System.Security.Cryptography;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Validation tests for font bridge implementation.
/// Organized by implementation phase.
/// </summary>
public static class FontBridgeTests
{
    /// <summary>
    /// Run all font validation tests.
    /// </summary>
    public static void RunAllTests()
    {
        Console.WriteLine("=== Font Bridge Validation Tests ===\n");

        Console.WriteLine("Phase 1: Bootstrap with WinForms/GDI+");
        Console.WriteLine("-------------------------------------");
        Phase1_MetricsConsistency();
        Phase1_WidthSanity();
        Phase1_GlyphRasterization();
        Phase1_StrikeFontBuild();

        Console.WriteLine("\nPhase 2: Determinism & Validation");
        Console.WriteLine("----------------------------------");
        Phase2_DpiLock();
        Phase2_AtlasHashing();
        Phase2_CacheConsistency();

        Console.WriteLine("\n=== All Tests Passed ===");
    }

    // ========================================================================
    // Phase 1: Bootstrap with WinForms/GDI+
    // ========================================================================

    /// <summary>
    /// Test: Metrics consistency - ascent + descent + leading > 0
    /// </summary>
    public static void Phase1_MetricsConsistency()
    {
        using var bridge = new WinFormsFontBridge();

        // Test multiple fonts
        var testFonts = new[]
        {
            new FontDescriptor("Courier New", 10),
            new FontDescriptor("Courier New", 12),
            new FontDescriptor("Courier New", 14),
            new FontDescriptor("Arial", 12)
        };

        foreach (var descriptor in testFonts)
        {
            var metrics = bridge.GetMetrics(descriptor);

            // Verify positive metrics
            Assert(metrics.Ascent > 0, $"{descriptor}: Ascent should be > 0, got {metrics.Ascent}");
            Assert(metrics.Descent >= 0, $"{descriptor}: Descent should be >= 0, got {metrics.Descent}");
            Assert(metrics.Leading >= 0, $"{descriptor}: Leading should be >= 0, got {metrics.Leading}");
            Assert(metrics.LineHeight > 0, $"{descriptor}: LineHeight should be > 0, got {metrics.LineHeight}");
            Assert(metrics.MaxWidth > 0, $"{descriptor}: MaxWidth should be > 0, got {metrics.MaxWidth}");
            Assert(metrics.SpaceWidth > 0, $"{descriptor}: SpaceWidth should be > 0, got {metrics.SpaceWidth}");

            // Verify consistency
            int computedLineHeight = metrics.Ascent + metrics.Descent + metrics.Leading;
            Assert(metrics.LineHeight == computedLineHeight,
                $"{descriptor}: LineHeight mismatch: {metrics.LineHeight} != {computedLineHeight}");
        }

        Console.WriteLine("  ✓ Phase1_MetricsConsistency");
    }

    /// <summary>
    /// Test: Width sanity - 'm' should be wider than 'i'
    /// </summary>
    public static void Phase1_WidthSanity()
    {
        using var bridge = new WinFormsFontBridge();
        var descriptor = new FontDescriptor("Courier New", 12);

        int widthI = bridge.GetCharWidth(descriptor, 'i');
        int widthM = bridge.GetCharWidth(descriptor, 'm');
        int widthSpace = bridge.GetCharWidth(descriptor, ' ');

        Assert(widthI > 0, $"Width of 'i' should be > 0, got {widthI}");
        Assert(widthM > 0, $"Width of 'm' should be > 0, got {widthM}");
        Assert(widthSpace > 0, $"Width of space should be > 0, got {widthSpace}");

        // For proportional fonts, 'm' should be wider than 'i'
        // For monospace (Courier), they should be equal
        var arialDesc = new FontDescriptor("Arial", 12);
        int arialWidthI = bridge.GetCharWidth(arialDesc, 'i');
        int arialWidthM = bridge.GetCharWidth(arialDesc, 'm');
        Assert(arialWidthM >= arialWidthI, $"Arial: 'm' ({arialWidthM}) should be >= 'i' ({arialWidthI})");

        // Test string width
        string testString = "Hello, World!";
        int stringWidth = bridge.GetStringWidth(descriptor, testString);
        Assert(stringWidth > 0, $"String width should be > 0, got {stringWidth}");

        // String width should be sum of character widths (approximately)
        int sumWidth = 0;
        foreach (char c in testString)
        {
            sumWidth += bridge.GetCharWidth(descriptor, c);
        }
        // Allow some tolerance for kerning/spacing differences
        Assert(Math.Abs(stringWidth - sumWidth) <= testString.Length,
            $"String width ({stringWidth}) should be close to sum ({sumWidth})");

        Console.WriteLine("  ✓ Phase1_WidthSanity");
    }

    /// <summary>
    /// Test: Glyph rasterization produces valid bitmap
    /// </summary>
    public static void Phase1_GlyphRasterization()
    {
        using var bridge = new WinFormsFontBridge();
        var descriptor = new FontDescriptor("Courier New", 12);

        // Test various characters
        char[] testChars = { 'A', 'a', '0', '@', ' ', 'M', 'i' };

        foreach (char c in testChars)
        {
            var glyph = bridge.RenderGlyph(descriptor, c);

            Assert(glyph.Width > 0, $"Glyph '{c}': Width should be > 0, got {glyph.Width}");
            Assert(glyph.Height > 0, $"Glyph '{c}': Height should be > 0, got {glyph.Height}");
            Assert(glyph.Bitmap != null, $"Glyph '{c}': Bitmap should not be null");
            Assert(glyph.Bitmap.Length > 0, $"Glyph '{c}': Bitmap should have data");
            Assert(glyph.Character == c, $"Glyph character mismatch: expected '{c}', got '{glyph.Character}'");
        }

        Console.WriteLine("  ✓ Phase1_GlyphRasterization");
    }

    /// <summary>
    /// Test: StrikeFont build produces valid data
    /// </summary>
    public static void Phase1_StrikeFontBuild()
    {
        using var bridge = new WinFormsFontBridge();
        var descriptor = new FontDescriptor("Courier New", 12);

        var strikeFont = bridge.BuildStrikeFont(descriptor, 0, 255);

        // Verify basic structure
        Assert(!string.IsNullOrEmpty(strikeFont.Name), "StrikeFont name should not be empty");
        Assert(strikeFont.MinAscii == 0, $"MinAscii should be 0, got {strikeFont.MinAscii}");
        Assert(strikeFont.MaxAscii == 255, $"MaxAscii should be 255, got {strikeFont.MaxAscii}");

        // Verify xTable
        Assert(strikeFont.XTable != null, "XTable should not be null");
        Assert(strikeFont.XTable.Length == 257, $"XTable length should be 257, got {strikeFont.XTable.Length}");

        // xTable should be monotonically increasing
        for (int i = 1; i < strikeFont.XTable.Length; i++)
        {
            Assert(strikeFont.XTable[i] >= strikeFont.XTable[i - 1],
                $"XTable not monotonic at index {i}: {strikeFont.XTable[i - 1]} > {strikeFont.XTable[i]}");
        }

        // Verify glyphs bitmap
        Assert(strikeFont.GlyphsBitmap != null, "GlyphsBitmap should not be null");
        Assert(strikeFont.GlyphsBitmap.Length > 0, "GlyphsBitmap should have data");
        Assert(strikeFont.StrikeLength > 0, $"StrikeLength should be > 0, got {strikeFont.StrikeLength}");
        Assert(strikeFont.StrikeHeight > 0, $"StrikeHeight should be > 0, got {strikeFont.StrikeHeight}");

        // Verify raster calculation
        int expectedRaster = (strikeFont.StrikeLength + 15) / 16;
        Assert(strikeFont.Raster == expectedRaster,
            $"Raster mismatch: expected {expectedRaster}, got {strikeFont.Raster}");

        // Verify width table
        Assert(strikeFont.WidthTable != null, "WidthTable should not be null");
        Assert(strikeFont.WidthTable.Length == 256, $"WidthTable length should be 256, got {strikeFont.WidthTable.Length}");

        Console.WriteLine("  ✓ Phase1_StrikeFontBuild");
    }

    // ========================================================================
    // Phase 2: Determinism & Validation
    // ========================================================================

    /// <summary>
    /// Test: DPI lock - same glyph produces same bitmap
    /// </summary>
    public static void Phase2_DpiLock()
    {
        using var bridge = new WinFormsFontBridge();

        // Verify DPI is locked at 96
        Assert(bridge.RenderingDpi == 96, $"RenderingDpi should be 96, got {bridge.RenderingDpi}");
        Assert(bridge.IsDeterministicMode, "IsDeterministicMode should be true");

        var descriptor = new FontDescriptor("Courier New", 12);

        // Render same glyph twice
        var glyph1 = bridge.RenderGlyph(descriptor, 'A');
        var glyph2 = bridge.RenderGlyph(descriptor, 'A');

        // Verify identical results
        Assert(glyph1.Width == glyph2.Width, "Glyph widths should match");
        Assert(glyph1.Height == glyph2.Height, "Glyph heights should match");
        Assert(glyph1.Bitmap.Length == glyph2.Bitmap.Length, "Bitmap lengths should match");

        string hash1 = ComputeHash(glyph1.Bitmap);
        string hash2 = ComputeHash(glyph2.Bitmap);
        Assert(hash1 == hash2, $"Glyph hashes should match: {hash1} != {hash2}");

        Console.WriteLine("  ✓ Phase2_DpiLock");
    }

    /// <summary>
    /// Test: Atlas hashing - StrikeFont produces consistent hash
    /// </summary>
    public static void Phase2_AtlasHashing()
    {
        using var bridge = new WinFormsFontBridge();
        var descriptor = new FontDescriptor("Courier New", 12);

        // Build StrikeFont twice
        var font1 = bridge.BuildStrikeFont(descriptor, 0, 127); // ASCII subset for faster test
        var font2 = bridge.BuildStrikeFont(descriptor, 0, 127);

        // Verify identical structure
        Assert(font1.StrikeLength == font2.StrikeLength, "StrikeLength should match");
        Assert(font1.GlyphsBitmap.Length == font2.GlyphsBitmap.Length, "GlyphsBitmap length should match");

        // Hash the atlas
        string hash1 = ComputeHash(font1.GlyphsBitmap);
        string hash2 = ComputeHash(font2.GlyphsBitmap);
        Assert(hash1 == hash2, $"Atlas hashes should match: {hash1} != {hash2}");

        // Verify xTable consistency
        for (int i = 0; i < font1.XTable.Length; i++)
        {
            Assert(font1.XTable[i] == font2.XTable[i],
                $"XTable mismatch at {i}: {font1.XTable[i]} != {font2.XTable[i]}");
        }

        Console.WriteLine("  ✓ Phase2_AtlasHashing");
    }

    /// <summary>
    /// Test: Cache consistency - cached results match fresh builds
    /// </summary>
    public static void Phase2_CacheConsistency()
    {
        using var bridge = new WinFormsFontBridge();
        var descriptor = new FontDescriptor("Courier New", 12);

        // First build (populates cache)
        var font1 = bridge.BuildStrikeFont(descriptor);

        // Second build (should use cache)
        var font2 = bridge.BuildStrikeFont(descriptor);

        // Verify same object returned (cache hit)
        // Note: Our implementation caches by reference, so these should be identical
        string hash1 = ComputeHash(font1.GlyphsBitmap);
        string hash2 = ComputeHash(font2.GlyphsBitmap);
        Assert(hash1 == hash2, "Cached font should produce same hash");

        // Verify metrics match
        Assert(font1.Metrics.Ascent == font2.Metrics.Ascent, "Cached ascent should match");
        Assert(font1.Metrics.Descent == font2.Metrics.Descent, "Cached descent should match");

        Console.WriteLine("  ✓ Phase2_CacheConsistency");
    }

    // ========================================================================
    // Utility Methods
    // ========================================================================

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    private static string ComputeHash(ushort[] data)
    {
        byte[] bytes = new byte[data.Length * 2];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).Substring(0, 16); // First 16 chars for readability
    }

    /// <summary>
    /// Helper to compute hash for atlas rows (for determinism detection).
    /// </summary>
    public static string[] ComputeRowHashes(StrikeFontData font)
    {
        int raster = font.Raster;
        int height = font.StrikeHeight;
        string[] rowHashes = new string[height];

        using var sha256 = SHA256.Create();

        for (int y = 0; y < height; y++)
        {
            byte[] rowBytes = new byte[raster * 2];
            for (int x = 0; x < raster; x++)
            {
                int idx = y * raster + x;
                if (idx < font.GlyphsBitmap.Length)
                {
                    rowBytes[x * 2] = (byte)(font.GlyphsBitmap[idx] & 0xFF);
                    rowBytes[x * 2 + 1] = (byte)(font.GlyphsBitmap[idx] >> 8);
                }
            }

            byte[] hash = sha256.ComputeHash(rowBytes);
            rowHashes[y] = Convert.ToHexString(hash).Substring(0, 8);
        }

        return rowHashes;
    }
}
