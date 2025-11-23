using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// BitBlt (Bit Block Transfer) engine implementing the Blue Book specification.
/// Performs raster operations between bitmaps with clipping, halftone patterns,
/// and all 16 combination rules.
/// Optimized for 1-bit monochrome bitmaps as used in Smalltalk-80.
/// </summary>
public static class BitBltEngine
{
    /// <summary>
    /// Perform a BitBlt operation copying from source to destination.
    /// </summary>
    /// <param name="destBits">Destination bitmap buffer.</param>
    /// <param name="destWidth">Destination bitmap width in pixels.</param>
    /// <param name="destHeight">Destination bitmap height in pixels.</param>
    /// <param name="destX">Destination X coordinate (top-left of operation).</param>
    /// <param name="destY">Destination Y coordinate (top-left of operation).</param>
    /// <param name="sourceBits">Source bitmap buffer (can be same as dest).</param>
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
    /// <param name="halftone">Optional halftone pattern (empty if not used).</param>
    /// <param name="halftoneWidth">Halftone pattern width (typically 16).</param>
    /// <param name="halftoneHeight">Halftone pattern height (typically 16).</param>
    /// <param name="rule">Combination rule (0-15).</param>
    public static void CopyBits(
        Span<byte> destBits, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<byte> sourceBits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftone, int halftoneWidth, int halftoneHeight,
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

        // Calculate strides (bytes per row)
        int destStride = (destWidth + 7) / 8;
        int sourceStride = (sourceWidth + 7) / 8;
        int halftoneStride = halftoneWidth > 0 ? (halftoneWidth + 7) / 8 : 0;

        // Validate buffer lengths before accessing
        int destRequiredBytes = destStride * destHeight;
        int sourceRequiredBytes = sourceStride * sourceHeight;

        if (destBits.Length < destRequiredBytes)
            return;  // Dest buffer too small - abort operation

        if (sourceBits.Length < sourceRequiredBytes)
            return;  // Source buffer too small - abort operation

        if (halftone.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0)
        {
            int halftoneRequiredBytes = halftoneStride * halftoneHeight;
            if (halftone.Length < halftoneRequiredBytes)
                return;  // Halftone buffer too small - abort operation
        }

        // Detect nil/empty source (fill operation)
        bool isNilSource = (sourceWidth == 0 || sourceHeight == 0 || sourceBits.Length == 0);

        // Step 3: Main loop - process each pixel
        for (int y = 0; y < clippedHeight; y++)
        {
            int destRow = clipTop + y;
            int sourceRow = adjustedSourceY + y;

            // Clip source row to source bounds (skip check if nil source)
            if (!isNilSource && (sourceRow < 0 || sourceRow >= sourceHeight))
                continue;

            for (int x = 0; x < clippedWidth; x++)
            {
                int destCol = clipLeft + x;
                int sourceCol = adjustedSourceX + x;

                // Clip source column to source bounds (skip check if nil source)
                if (!isNilSource && (sourceCol < 0 || sourceCol >= sourceWidth))
                    continue;

                // Get source bit (treat nil source as white/0 for fill operations)
                bool sourceBit = isNilSource ? false : GetBit(sourceBits, sourceStride, sourceCol, sourceRow);

                // Get destination bit
                bool destBit = GetBit(destBits, destStride, destCol, destRow);

                // Get halftone bit if present (tiles across destination)
                bool halftoneBit = true;
                if (halftone.Length > 0 && halftoneWidth > 0 && halftoneHeight > 0)
                {
                    int htX = destCol % halftoneWidth;
                    int htY = destRow % halftoneHeight;
                    halftoneBit = GetBit(halftone, halftoneStride, htX, htY);
                }

                // Apply combination rule
                bool resultBit = ApplyRule(sourceBit, destBit, halftoneBit, rule);

                // Set destination bit
                SetBit(destBits, destStride, destCol, destRow, resultBit);
            }
        }
    }

    /// <summary>
    /// Get a single bit from a 1-bit bitmap at specified coordinates.
    /// Bitmap format: MSB first (bit 7 = leftmost pixel in byte).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetBit(ReadOnlySpan<byte> bits, int stride, int x, int y)
    {
        int byteOffset = y * stride + (x / 8);
        if (byteOffset < 0 || byteOffset >= bits.Length)
            return false;

        int bitOffset = 7 - (x % 8); // MSB first
        return (bits[byteOffset] & (1 << bitOffset)) != 0;
    }

    /// <summary>
    /// Set a single bit in a 1-bit bitmap at specified coordinates.
    /// Bitmap format: MSB first (bit 7 = leftmost pixel in byte).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(Span<byte> bits, int stride, int x, int y, bool value)
    {
        int byteOffset = y * stride + (x / 8);
        if (byteOffset < 0 || byteOffset >= bits.Length)
            return;

        int bitOffset = 7 - (x % 8); // MSB first
        if (value)
            bits[byteOffset] |= (byte)(1 << bitOffset);
        else
            bits[byteOffset] &= (byte)~(1 << bitOffset);
    }

    /// <summary>
    /// Apply a combination rule to compute the result bit.
    /// Implements all 16 boolean operations per Blue Book specification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyRule(bool source, bool dest, bool halftone, CombinationRule rule)
    {
        // Halftone acts as a mask on the source
        // If halftone bit is false, treat source as false
        if (!halftone)
            source = false;

        return rule switch
        {
            CombinationRule.Clear => false,                    // 0: Always 0
            CombinationRule.And => source & dest,              // 1: src AND dst
            CombinationRule.AndReverse => source & !dest,      // 2: src AND (NOT dst)
            CombinationRule.Copy => source,                    // 3: src
            CombinationRule.AndInverted => !source & dest,     // 4: (NOT src) AND dst
            CombinationRule.Noop => dest,                      // 5: dst (no change)
            CombinationRule.Xor => source ^ dest,              // 6: src XOR dst
            CombinationRule.Or => source | dest,               // 7: src OR dst
            CombinationRule.Nor => !(source | dest),           // 8: NOT (src OR dst)
            CombinationRule.Equiv => !(source ^ dest),         // 9: NOT (src XOR dst)
            CombinationRule.Invert => !dest,                   // 10: NOT dst
            CombinationRule.OrReverse => source | !dest,       // 11: src OR (NOT dst)
            CombinationRule.CopyInverted => !source,           // 12: NOT src
            CombinationRule.OrInverted => !source | dest,      // 13: (NOT src) OR dst
            CombinationRule.Nand => !(source & dest),          // 14: NOT (src AND dst)
            CombinationRule.Set => true,                       // 15: Always 1
            _ => dest // Default to noop for unknown rules
        };
    }
}
