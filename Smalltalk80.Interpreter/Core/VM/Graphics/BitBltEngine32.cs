using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// 32bpp BitBlt engine for rendering 1bpp Smalltalk Forms to 32bpp display surface.
///
/// Key features:
/// - Converts 1bpp source/halftone to 32bpp ARGB during BitBlt operations
/// - Halftone patterns become actual gray values (smooth rendering)
/// - All 16 combination rules supported
/// - Maintains Blue Book BitBlt semantics
///
/// Color mapping:
/// - Black (1) → 0xFF000000 (opaque black)
/// - White (0) → 0xFFFFFFFF (opaque white)
/// - Halftone patterns → gray values based on pattern density
/// </summary>
public static class BitBltEngine32
{
    // Standard ARGB colors
    private const uint Black = 0xFF000000;
    private const uint White = 0xFFFFFFFF;

    // Pre-computed gray values for common halftone densities
    // Index = number of set bits in a 16x16 pattern / 256 * 255
    private static readonly uint[] DensityToGray = ComputeDensityGrayTable();

    private static uint[] ComputeDensityGrayTable()
    {
        // Create lookup table: density (0-256) → gray ARGB value
        var table = new uint[257];
        for (int i = 0; i <= 256; i++)
        {
            // density 0 = all white, density 256 = all black
            int gray = 255 - (i * 255 / 256);
            table[i] = 0xFF000000 | ((uint)gray << 16) | ((uint)gray << 8) | (uint)gray;
        }
        return table;
    }

    /// <summary>
    /// Perform BitBlt from 1bpp source to 32bpp destination.
    /// Halftone patterns are converted to smooth gray values.
    /// </summary>
    public static void CopyBits1To32(
        Span<byte> dest32Bits, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<byte> source1Bits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftone1Bits, int halftoneWidth, int halftoneHeight,
        CombinationRule rule)
    {
        // Step 1: Calculate clipped bounds
        int clipLeft = Math.Max(destX, clipX);
        int clipTop = Math.Max(destY, clipY);
        int clipRight = Math.Min(destX + width, clipX + clipWidth);
        int clipBottom = Math.Min(destY + height, clipY + clipHeight);

        // Clip to destination bounds
        clipLeft = Math.Max(clipLeft, 0);
        clipTop = Math.Max(clipTop, 0);
        clipRight = Math.Min(clipRight, destWidth);
        clipBottom = Math.Min(clipBottom, destHeight);

        // Nothing to do if clipped out
        if (clipLeft >= clipRight || clipTop >= clipBottom)
            return;

        // Adjust source coordinates for clipping
        int adjustedSourceX = sourceX + (clipLeft - destX);
        int adjustedSourceY = sourceY + (clipTop - destY);
        int clippedWidth = clipRight - clipLeft;
        int clippedHeight = clipBottom - clipTop;

        // Calculate strides
        int sourceStride = sourceWidth > 0 ? ((sourceWidth + 15) / 16) * 2 : 0;
        int halftoneStride = halftoneWidth > 0 ? ((halftoneWidth + 15) / 16) * 2 : 0;

        // Detect nil source (fill operation)
        bool isNilSource = sourceWidth == 0 || sourceHeight == 0 || source1Bits.Length == 0;
        bool hasHalftone = halftone1Bits.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0;

        // Validate buffers
        int destRequired = destWidth * destHeight * 4;
        if (dest32Bits.Length < destRequired)
            return;

        if (!isNilSource)
        {
            int sourceRequired = sourceStride * sourceHeight;
            if (source1Bits.Length < sourceRequired)
                return;
        }

        if (hasHalftone)
        {
            int halftoneRequired = halftoneStride * halftoneHeight;
            if (halftone1Bits.Length < halftoneRequired)
                return;
        }

        // Compute halftone gray color if halftone is present
        uint halftoneGray = White;
        if (hasHalftone)
        {
            halftoneGray = ComputeHalftoneGray(halftone1Bits, halftoneWidth, halftoneHeight, halftoneStride);
        }

        // Perform the BitBlt operation
        unsafe
        {
            fixed (byte* destPtr = dest32Bits)
            {
                uint* dest = (uint*)destPtr;

                for (int y = 0; y < clippedHeight; y++)
                {
                    int destRow = clipTop + y;
                    int sourceRow = adjustedSourceY + y;
                    int dstRowOffset = destRow * destWidth;

                    for (int x = 0; x < clippedWidth; x++)
                    {
                        int destCol = clipLeft + x;
                        int sourceCol = adjustedSourceX + x;

                        // Get source pixel (converted to 32bpp)
                        uint sourcePixel;
                        if (isNilSource)
                        {
                            // Fill operation: use halftone gray as source
                            sourcePixel = hasHalftone ? halftoneGray : White;
                        }
                        else
                        {
                            // Normal source from 1bpp bitmap
                            bool srcBit = GetBit1(source1Bits, sourceStride, sourceCol, sourceRow);
                            sourcePixel = srcBit ? Black : White;

                            // Apply halftone mask if present (modulates source)
                            if (hasHalftone)
                            {
                                int htX = destCol % halftoneWidth;
                                int htY = destRow % halftoneHeight;
                                bool htBit = GetBit1(halftone1Bits, halftoneStride, htX, htY);
                                if (!htBit)
                                {
                                    // Halftone bit is off - blend source toward white
                                    sourcePixel = BlendToWhite(sourcePixel, halftoneGray);
                                }
                            }
                        }

                        // Get destination pixel
                        uint destPixel = dest[dstRowOffset + destCol];

                        // Apply combination rule
                        uint result = ApplyRule32(sourcePixel, destPixel, rule);

                        // Write result
                        dest[dstRowOffset + destCol] = result;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Compute the gray color that represents a halftone pattern's density.
    /// </summary>
    private static uint ComputeHalftoneGray(
        ReadOnlySpan<byte> halftone, int width, int height, int stride)
    {
        // Count set bits in the halftone pattern
        int totalBits = width * height;
        int setBits = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetBit1(halftone, stride, x, y))
                    setBits++;
            }
        }

        // Convert density to gray
        // 0 set bits = white (0%), all set bits = black (100%)
        int densityIndex = totalBits > 0 ? (setBits * 256 / totalBits) : 0;
        densityIndex = Math.Clamp(densityIndex, 0, 256);

        return DensityToGray[densityIndex];
    }

    /// <summary>
    /// Blend a color toward white based on halftone gray.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BlendToWhite(uint color, uint halftoneGray)
    {
        // Extract gray level from halftone (0=black, 255=white)
        int htGray = (int)((halftoneGray >> 16) & 0xFF);

        // Extract color components
        int r = (int)((color >> 16) & 0xFF);
        int g = (int)((color >> 8) & 0xFF);
        int b = (int)(color & 0xFF);

        // Blend toward white based on halftone gray
        // htGray=0 (black halftone) = no change
        // htGray=255 (white halftone) = full white
        int blend = htGray;
        r = r + ((255 - r) * blend / 255);
        g = g + ((255 - g) * blend / 255);
        b = b + ((255 - b) * blend / 255);

        return 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    /// <summary>
    /// Get a single bit from a 1bpp bitmap.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetBit1(ReadOnlySpan<byte> bits, int stride, int x, int y)
    {
        if (x < 0 || y < 0) return false;
        int byteOffset = y * stride + (x / 8);
        if (byteOffset < 0 || byteOffset >= bits.Length)
            return false;
        int bitOffset = 7 - (x % 8);  // MSB first
        return (bits[byteOffset] & (1 << bitOffset)) != 0;
    }

    /// <summary>
    /// Apply combination rule to 32bpp pixels.
    /// Uses per-component operations for proper color handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyRule32(uint source, uint dest, CombinationRule rule)
    {
        return rule switch
        {
            CombinationRule.Clear => 0xFF000000,        // 0: Black (opaque)
            CombinationRule.And => AndPixels(source, dest),
            CombinationRule.AndReverse => AndPixels(source, InvertPixel(dest)),
            CombinationRule.Copy => source,             // 3: Copy source
            CombinationRule.AndInverted => AndPixels(InvertPixel(source), dest),
            CombinationRule.Noop => dest,               // 5: No change
            CombinationRule.Xor => XorPixels(source, dest),
            CombinationRule.Or => OrPixels(source, dest),
            CombinationRule.Nor => InvertPixel(OrPixels(source, dest)),
            CombinationRule.Equiv => InvertPixel(XorPixels(source, dest)),
            CombinationRule.Invert => InvertPixel(dest),
            CombinationRule.OrReverse => OrPixels(source, InvertPixel(dest)),
            CombinationRule.CopyInverted => InvertPixel(source),
            CombinationRule.OrInverted => OrPixels(InvertPixel(source), dest),
            CombinationRule.Nand => InvertPixel(AndPixels(source, dest)),
            CombinationRule.Set => 0xFFFFFFFF,          // 15: White (opaque)
            _ => dest
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint InvertPixel(uint pixel)
    {
        // Invert RGB, keep alpha at 0xFF
        return 0xFF000000 | (~pixel & 0x00FFFFFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AndPixels(uint a, uint b)
    {
        // AND each component, keep alpha at 0xFF
        return 0xFF000000 | (a & b & 0x00FFFFFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint OrPixels(uint a, uint b)
    {
        // OR each component, keep alpha at 0xFF
        return 0xFF000000 | ((a | b) & 0x00FFFFFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint XorPixels(uint source, uint dest)
    {
        // Handle 1bpp-style XOR semantics:
        // In 1bpp: black=1, white=0. XOR(1, x) inverts x.
        // In 32bpp: black=(0,0,0), white=(255,255,255) - inverted polarity!
        //
        // For correct selection highlighting:
        // - Black source → invert destination
        // - White source → keep destination
        // - Gray source → proportional blend

        int sr = (int)((source >> 16) & 0xFF);
        int sg = (int)((source >> 8) & 0xFF);
        int sb = (int)(source & 0xFF);
        int dr = (int)((dest >> 16) & 0xFF);
        int dg = (int)((dest >> 8) & 0xFF);
        int db = (int)(dest & 0xFF);

        // "Blackness" of source: 255=black, 0=white
        int br = 255 - sr;
        int bg = 255 - sg;
        int bb = 255 - sb;

        // Inverted destination
        int ir = 255 - dr;
        int ig = 255 - dg;
        int ib = 255 - db;

        // Blend between dest and inverted_dest based on blackness
        // blackness=255 (black source): result = inverted dest
        // blackness=0 (white source): result = dest unchanged
        int rr = (ir * br + dr * (255 - br)) / 255;
        int rg = (ig * bg + dg * (255 - bg)) / 255;
        int rb = (ib * bb + db * (255 - bb)) / 255;

        return 0xFF000000 | ((uint)rr << 16) | ((uint)rg << 8) | (uint)rb;
    }

    /// <summary>
    /// Fast fill operation for 32bpp destination with halftone.
    /// Used when source is nil - fills with halftone gray color.
    /// </summary>
    public static void FillRect32(
        Span<byte> dest32Bits, int destWidth, int destHeight,
        int x, int y, int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftone1Bits, int halftoneWidth, int halftoneHeight,
        CombinationRule rule)
    {
        // Clip bounds
        int left = Math.Max(Math.Max(x, clipX), 0);
        int top = Math.Max(Math.Max(y, clipY), 0);
        int right = Math.Min(Math.Min(x + width, clipX + clipWidth), destWidth);
        int bottom = Math.Min(Math.Min(y + height, clipY + clipHeight), destHeight);

        if (left >= right || top >= bottom)
            return;

        // Compute fill color from halftone
        uint fillColor = White;
        if (halftone1Bits.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0)
        {
            int halftoneStride = ((halftoneWidth + 15) / 16) * 2;
            fillColor = ComputeHalftoneGray(halftone1Bits, halftoneWidth, halftoneHeight, halftoneStride);
        }

        // Validate buffer
        if (dest32Bits.Length < destWidth * destHeight * 4)
            return;

        unsafe
        {
            fixed (byte* destPtr = dest32Bits)
            {
                uint* dest = (uint*)destPtr;

                for (int row = top; row < bottom; row++)
                {
                    int rowOffset = row * destWidth;
                    for (int col = left; col < right; col++)
                    {
                        uint destPixel = dest[rowOffset + col];
                        dest[rowOffset + col] = ApplyRule32(fillColor, destPixel, rule);
                    }
                }
            }
        }
    }
}
