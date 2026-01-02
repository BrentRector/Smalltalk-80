using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
/// Standard RGB mode:
/// - Black: 0x000000 (RGB)
/// - White: 0xFFFFFF (RGB)
///
/// Note: BitBlt combination rules have inverted visual semantics compared to 1bpp.
/// Callers should use appropriate rules for 32bpp (e.g., AND instead of OR for
/// drawing black content on white backgrounds).
/// </summary>
public static class BitBlt32Engine
{
    /// <summary>Black pixel in standard RGB.</summary>
    public const uint BlackPixel = 0x000000;

    /// <summary>White pixel in standard RGB.</summary>
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

        // Fast paths for common operations without halftone
        if (!hasHalftone && !isNilSource)
        {
            switch (rule)
            {
                case CombinationRule.Copy:
                    CopyBitsOptimized(destPixels, destWidth, clipLeft, clipTop,
                                     sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                                     clippedWidth, clippedHeight);
                    return;
                case CombinationRule.Or:
                    OrBitsOptimized(destPixels, destWidth, clipLeft, clipTop,
                                   sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                                   clippedWidth, clippedHeight);
                    return;
                case CombinationRule.And:
                    AndBitsOptimized(destPixels, destWidth, clipLeft, clipTop,
                                    sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                                    clippedWidth, clippedHeight);
                    return;
                case CombinationRule.Xor:
                    XorBitsOptimized(destPixels, destWidth, clipLeft, clipTop,
                                    sourcePixels, sourceWidth, adjustedSourceX, adjustedSourceY,
                                    clippedWidth, clippedHeight);
                    return;
            }
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
    /// Uses Buffer.MemoryCopy for each row (handles horizontal overlap).
    /// Iterates rows in correct order for vertical overlap (same-bitmap scrolling).
    /// </summary>
    private static unsafe void CopyBitsOptimized(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height)
    {
        fixed (uint* destPtr = destPixels)
        fixed (uint* sourcePtr = sourcePixels)
        {
            // Always use safe copy direction based on Y coordinates
            // This handles overlapping regions (same-Form scrolling) correctly
            // with negligible performance impact for non-overlapping cases
            bool copyBottomToTop = destY > sourceY;

            if (copyBottomToTop)
            {
                // Copy bottom-to-top for downward scrolling (dest below source)
                for (int y = height - 1; y >= 0; y--)
                {
                    int destOffset = (destY + y) * destWidth + destX;
                    int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

                    if (destOffset >= 0 && sourceOffset >= 0 &&
                        destOffset + width <= destPixels.Length &&
                        sourceOffset + width <= sourcePixels.Length)
                    {
                        Buffer.MemoryCopy(
                            sourcePtr + sourceOffset,
                            destPtr + destOffset,
                            (long)width * sizeof(uint),
                            (long)width * sizeof(uint));
                    }
                }
            }
            else
            {
                // Copy top-to-bottom for upward scrolling or non-overlapping
                for (int y = 0; y < height; y++)
                {
                    int destOffset = (destY + y) * destWidth + destX;
                    int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

                    if (destOffset >= 0 && sourceOffset >= 0 &&
                        destOffset + width <= destPixels.Length &&
                        sourceOffset + width <= sourcePixels.Length)
                    {
                        Buffer.MemoryCopy(
                            sourcePtr + sourceOffset,
                            destPtr + destOffset,
                            (long)width * sizeof(uint),
                            (long)width * sizeof(uint));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fast path for OR operations (Rule 7) without halftone.
    /// Used for paint mode text drawing.
    /// </summary>
    private static void OrBitsOptimized(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height)
    {
        bool copyBottomToTop = destY > sourceY;

        for (int yi = 0; yi < height; yi++)
        {
            int y = copyBottomToTop ? (height - 1 - yi) : yi;
            int destOffset = (destY + y) * destWidth + destX;
            int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

            if (destOffset >= 0 && sourceOffset >= 0 &&
                destOffset + width <= destPixels.Length &&
                sourceOffset + width <= sourcePixels.Length)
            {
                for (int x = 0; x < width; x++)
                {
                    destPixels[destOffset + x] |= sourcePixels[sourceOffset + x];
                }
            }
        }
    }

    /// <summary>
    /// Fast path for AND operations (Rule 1) without halftone.
    /// Used for masking operations.
    /// </summary>
    private static void AndBitsOptimized(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height)
    {
        bool copyBottomToTop = destY > sourceY;

        for (int yi = 0; yi < height; yi++)
        {
            int y = copyBottomToTop ? (height - 1 - yi) : yi;
            int destOffset = (destY + y) * destWidth + destX;
            int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

            if (destOffset >= 0 && sourceOffset >= 0 &&
                destOffset + width <= destPixels.Length &&
                sourceOffset + width <= sourcePixels.Length)
            {
                for (int x = 0; x < width; x++)
                {
                    destPixels[destOffset + x] &= sourcePixels[sourceOffset + x];
                }
            }
        }
    }

    /// <summary>
    /// Fast path for XOR operations (Rule 6) without halftone.
    /// Used for cursor drawing and selection highlight.
    /// Inverts source to preserve 1bpp semantics: "black means invert".
    /// In 1bpp, black=1 so XOR inverts. In 32bpp, black=0 so we invert source first.
    /// </summary>
    private static void XorBitsOptimized(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height)
    {
        bool copyBottomToTop = destY > sourceY;

        for (int yi = 0; yi < height; yi++)
        {
            int y = copyBottomToTop ? (height - 1 - yi) : yi;
            int destOffset = (destY + y) * destWidth + destX;
            int sourceOffset = (sourceY + y) * sourceWidth + sourceX;

            if (destOffset >= 0 && sourceOffset >= 0 &&
                destOffset + width <= destPixels.Length &&
                sourceOffset + width <= sourcePixels.Length)
            {
                for (int x = 0; x < width; x++)
                {
                    // Invert source to preserve 1bpp XOR semantics:
                    // black (0x000000) becomes 0xFFFFFF → inverts dest
                    // white (0xFFFFFF) becomes 0x000000 → no change to dest
                    destPixels[destOffset + x] ^= ~sourcePixels[sourceOffset + x];
                }
            }
        }
    }

    /// <summary>
    /// General implementation handling all rules, halftone patterns, and nil source.
    /// Handles overlapping regions by iterating in the correct direction.
    /// </summary>
    private static void CopyBitsGeneral(
        Span<uint> destPixels, int destWidth, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceX, int sourceY,
        int width, int height,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
        CombinationRule rule, bool isNilSource)
    {
        // Use safe copy direction based on coordinates for overlapping regions
        bool copyBottomToTop = destY > sourceY;
        bool copyRightToLeft = (destY == sourceY) && (destX > sourceX);

        for (int yi = 0; yi < height; yi++)
        {
            // Iterate in correct Y direction to handle vertical overlap
            int y = copyBottomToTop ? (height - 1 - yi) : yi;
            int destRow = destY + y;
            int sourceRow = sourceY + y;

            for (int xi = 0; xi < width; xi++)
            {
                // Iterate in correct X direction to handle horizontal overlap
                int x = copyRightToLeft ? (width - 1 - xi) : xi;
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

                        // 32bpp fill operation semantic correction:
                        // In 1bpp, Form black = all 1s, so XOR inverts pixels.
                        // In 32bpp standard RGB, Form black = 0x000000, so XOR would do nothing!
                        // Fix: For XOR fills, invert halftone so black=0xFFFFFF (inverts) and white=0 (no change)
                        if (rule == CombinationRule.Xor)
                        {
                            sourcePixel = ~sourcePixel;
                        }
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
                    // 32bpp halftone mask semantics:
                    // Where halftone is black (0x000000), source passes through
                    // Where halftone is white (0xFFFFFF), result becomes white
                    // Formula: result = (source & ~halftone) | halftone
                    // This matches 1bpp behavior where black=1 means "ink", white=0 means "masked"
                    sourcePixel = (sourcePixel & ~halftonePixel) | halftonePixel;
                }

                // For XOR with non-nil source, invert to preserve 1bpp semantics
                // In 1bpp: black=1, XOR with black inverts
                // In 32bpp: black=0, so we invert source: black→white (inverts), white→black (no change)
                if (!isNilSource && rule == CombinationRule.Xor)
                {
                    sourcePixel = ~sourcePixel;
                }

                // Apply combination rule
                uint resultPixel = ApplyRule(sourcePixel, destPixel, rule);

                // Set destination pixel
                destPixels[destIndex] = resultPixel;
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
