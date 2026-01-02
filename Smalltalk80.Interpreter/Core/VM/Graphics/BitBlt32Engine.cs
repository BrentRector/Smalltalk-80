using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// 32bpp BitBlt (Bit Block Transfer) engine for the 32bpp-everywhere architecture.
/// Performs raster operations between 32-bit ARGB bitmaps with clipping,
/// halftone patterns, and all 16 combination rules.
///
/// Key differences from 1bpp BitBltEngine:
/// - Each pixel is one uint (RGB format, alpha implicit)
/// - Direct array indexing (no bit packing/unpacking)
/// - Stride = width (pixels per row, not bytes)
/// - Combination rules operate on 24-bit RGB values
///
/// RGB-only mode (alpha is implicit, treated as opaque):
/// - Black: 0x000000 (RGB)
/// - White: 0xFFFFFF (RGB)
/// </summary>
public static class BitBlt32Engine
{
    /// <summary>Black pixel in RGB format (alpha implicit).</summary>
    public const uint BlackPixel = 0x000000;

    /// <summary>White pixel in RGB format (alpha implicit).</summary>
    public const uint WhitePixel = 0xFFFFFF;

    /// <summary>
    /// Perform a 32bpp BitBlt operation copying from source to destination.
    /// </summary>
    /// <param name="destPixels">Destination pixel buffer (width * height uints).</param>
    /// <param name="destWidth">Destination bitmap width in pixels.</param>
    /// <param name="destHeight">Destination bitmap height in pixels.</param>
    /// <param name="destX">Destination X coordinate (top-left of operation).</param>
    /// <param name="destY">Destination Y coordinate (top-left of operation).</param>
    /// <param name="sourcePixels">Source pixel buffer (can be same as dest, or empty for fill).</param>
    /// <param name="sourceWidth">Source bitmap width in pixels.</param>
    /// <param name="sourceHeight">Source bitmap height in pixels.</param>
    /// <param name="sourceX">Source X coordinate (top-left of source region).</param>
    /// <param name="sourceY">Source Y coordinate (top-left of source region).</param>
    /// <param name="width">Width of operation in pixels.</param>
    /// <param name="height">Height of operation in pixels.</param>
    /// <param name="clipX">Clipping rectangle X (in destination coordinates).</param>
    /// <param name="clipY">Clipping rectangle Y (in destination coordinates).</param>
    /// <param name="clipWidth">Clipping rectangle width.</param>
    /// <param name="clipHeight">Clipping rectangle height.</param>
    /// <param name="halftonePixels">Optional halftone pattern (empty if not used).</param>
    /// <param name="halftoneWidth">Halftone pattern width (typically 16).</param>
    /// <param name="halftoneHeight">Halftone pattern height (typically 16).</param>
    /// <param name="rule">Combination rule (0-15).</param>
    public static void CopyBits(
        Span<uint> destPixels, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
        CombinationRule rule)
    {
        // Step 1: Calculate clipped bounds
        int clipLeft = Math.Max(destX, clipX);
        int clipTop = Math.Max(destY, clipY);
        int clipRight = Math.Min(destX + width, clipX + clipWidth);
        int clipBottom = Math.Min(destY + height, clipY + clipHeight);

        // Also clip to destination bounds
        clipLeft = Math.Max(clipLeft, 0);
        clipTop = Math.Max(clipTop, 0);
        clipRight = Math.Min(clipRight, destWidth);
        clipBottom = Math.Min(clipBottom, destHeight);

        // Nothing to do if clipped out entirely
        if (clipLeft >= clipRight || clipTop >= clipBottom)
            return;

        // Step 2: Adjust source coordinates for clipping offset
        int adjustedSourceX = sourceX + (clipLeft - destX);
        int adjustedSourceY = sourceY + (clipTop - destY);

        // Clipped dimensions
        int clippedWidth = clipRight - clipLeft;
        int clippedHeight = clipBottom - clipTop;

        // Detect nil/empty source (fill operation)
        bool isNilSource = sourceWidth == 0 || sourceHeight == 0 || sourcePixels.Length == 0;

        // Validate buffer lengths
        int destRequiredPixels = destWidth * destHeight;
        if (destPixels.Length < destRequiredPixels)
            return;

        if (!isNilSource)
        {
            int sourceRequiredPixels = sourceWidth * sourceHeight;
            if (sourcePixels.Length < sourceRequiredPixels)
                return;
        }

        bool hasHalftone = halftonePixels.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0;

        if (hasHalftone)
        {
            int halftoneRequiredPixels = halftoneWidth * halftoneHeight;
            if (halftonePixels.Length < halftoneRequiredPixels)
                return;
        }

        // Fast path: Simple copy operation (Rule 3) without halftone
        if (rule == CombinationRule.Copy && !hasHalftone && !isNilSource)
        {
            CopyBitsOptimized(destPixels, destWidth, clipLeft, clipTop,
                             sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                             clippedWidth, clippedHeight);
            return;
        }

        // General path: Handle all rules, halftone, and nil source
        CopyBitsGeneral(destPixels, destWidth, clipLeft, clipTop,
                       sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                       clippedWidth, clippedHeight,
                       halftonePixels, halftoneWidth, halftoneHeight,
                       rule, isNilSource);
    }

    /// <summary>
    /// Fast path for simple copy operations (Rule 3) without halftone.
    /// Uses row-by-row span copying for maximum performance.
    /// </summary>
    private static void CopyBitsOptimized(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            int destOffset = (destY + y) * destWidth + destX;
            int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

            // Validate bounds
            if (destOffset >= 0 && sourceOffset >= 0 &&
                destOffset + width <= destPixels.Length &&
                sourceOffset + width <= sourcePixels.Length)
            {
                sourcePixels.Slice(sourceOffset, width)
                           .CopyTo(destPixels.Slice(destOffset, width));
            }
        }
    }

    /// <summary>
    /// General implementation handling all rules, halftone patterns, and nil source.
    /// </summary>
    private static void CopyBitsGeneral(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
        CombinationRule rule, bool isNilSource)
    {
        // DEBUG: Trace first pixel of fill operations
        bool debugFirstPixel = isNilSource && halftonePixels.Length > 0 && width == 800 && height == 600;

        for (int y = 0; y < height; y++)
        {
            int destRow = destY + y;
            int sourceRow = sourceY + y;

            for (int x = 0; x < width; x++)
            {
                int destCol = destX + x;
                int sourceCol = sourceX + x;

                // Get source pixel
                uint sourcePixel;
                if (isNilSource)
                {
                    // For fill operations (nil source), use halftone pattern AS the source
                    if (halftonePixels.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0)
                    {
                        int htX = destCol % halftoneWidth;
                        int htY = destRow % halftoneHeight;
                        sourcePixel = halftonePixels[htY * halftoneWidth + htX];
                    }
                    else
                    {
                        sourcePixel = WhitePixel; // No halftone = white
                    }
                }
                else
                {
                    // Normal source from bitmap
                    int sourceIndex = sourceRow * sourceWidth + sourceCol;
                    if (sourceIndex >= 0 && sourceIndex < sourcePixels.Length)
                        sourcePixel = sourcePixels[sourceIndex];
                    else
                        sourcePixel = WhitePixel;
                }

                // Get destination pixel
                int destIndex = destRow * destWidth + destCol;
                if (destIndex < 0 || destIndex >= destPixels.Length)
                    continue;

                uint destPixel = destPixels[destIndex];

                // Apply halftone mask to source (only for non-nil source)
                if (!isNilSource && halftonePixels.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0)
                {
                    int htX = destCol % halftoneWidth;
                    int htY = destRow % halftoneHeight;
                    uint halftonePixel = halftonePixels[htY * halftoneWidth + htX];
                    // Halftone acts as a mask: if halftone pixel is black, source passes through
                    // If halftone pixel is white, source becomes white (masked out)
                    sourcePixel = sourcePixel & halftonePixel;
                }

                // Apply combination rule
                uint resultPixel = ApplyRule(sourcePixel, destPixel, rule);

                // DEBUG: Trace first pixel
                if (debugFirstPixel && x == 0 && y == 0)
                {
                    Console.WriteLine($"[BitBlt32] First pixel: destIndex={destIndex}, src={sourcePixel:X8}, dest={destPixel:X8}, result={resultPixel:X8}");
                }

                // Set destination pixel
                destPixels[destIndex] = resultPixel;

                // DEBUG: Verify write
                if (debugFirstPixel && x == 0 && y == 0)
                {
                    Console.WriteLine($"[BitBlt32] After write: destPixels[{destIndex}]={destPixels[destIndex]:X8}");
                }
            }
        }
    }

    /// <summary>
    /// Apply a combination rule to compute the result pixel.
    /// Implements all 16 boolean operations per Blue Book specification.
    /// Operations work on full 32-bit values (all channels simultaneously).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ApplyRule(uint source, uint dest, CombinationRule rule)
    {
        return rule switch
        {
            CombinationRule.Clear => 0,                    // 0: Always 0 (transparent black)
            CombinationRule.And => source & dest,          // 1: src AND dst
            CombinationRule.AndReverse => source & ~dest,  // 2: src AND (NOT dst)
            CombinationRule.Copy => source,                // 3: src
            CombinationRule.AndInverted => ~source & dest, // 4: (NOT src) AND dst
            CombinationRule.Noop => dest,                  // 5: dst (no change)
            CombinationRule.Xor => source ^ dest,          // 6: src XOR dst
            CombinationRule.Or => source | dest,           // 7: src OR dst
            CombinationRule.Nor => ~(source | dest),       // 8: NOT (src OR dst)
            CombinationRule.Equiv => ~(source ^ dest),     // 9: NOT (src XOR dst)
            CombinationRule.Invert => ~dest,               // 10: NOT dst
            CombinationRule.OrReverse => source | ~dest,   // 11: src OR (NOT dst)
            CombinationRule.CopyInverted => ~source,       // 12: NOT src
            CombinationRule.OrInverted => ~source | dest,  // 13: (NOT src) OR dst
            CombinationRule.Nand => ~(source & dest),      // 14: NOT (src AND dst)
            CombinationRule.Set => 0xFFFFFFFF,             // 15: Always 1 (opaque white)
            _ => dest // Default to noop for unknown rules
        };
    }

    /// <summary>
    /// Fill a rectangular region with a solid color.
    /// Convenience method for common fill operations.
    /// </summary>
    public static void FillRect(
        Span<uint> destPixels, int destWidth, int destHeight,
        int x, int y, int width, int height,
        uint color)
    {
        // Clip to destination bounds
        int left = Math.Max(x, 0);
        int top = Math.Max(y, 0);
        int right = Math.Min(x + width, destWidth);
        int bottom = Math.Min(y + height, destHeight);

        if (left >= right || top >= bottom)
            return;

        int fillWidth = right - left;

        for (int row = top; row < bottom; row++)
        {
            int offset = row * destWidth + left;
            if (offset + fillWidth <= destPixels.Length)
            {
                destPixels.Slice(offset, fillWidth).Fill(color);
            }
        }
    }

    /// <summary>
    /// Fill a rectangular region with a halftone pattern.
    /// </summary>
    public static void FillRectWithHalftone(
        Span<uint> destPixels, int destWidth, int destHeight,
        int x, int y, int width, int height,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight)
    {
        // Use CopyBits with nil source and halftone pattern
        CopyBits(
            destPixels, destWidth, destHeight, x, y,
            ReadOnlySpan<uint>.Empty, 0, 0, 0, 0,  // nil source
            width, height,
            0, 0, destWidth, destHeight,  // full clip
            halftonePixels, halftoneWidth, halftoneHeight,
            CombinationRule.Copy);
    }
}
