using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Graphics;

/// <summary>
/// Pure C# implementation of BitBlt (Bit Block Transfer) with all 16 combination rules.
/// Optimized for 1-bit monochrome bitmaps as used in Smalltalk-80.
/// Uses unsafe code for maximum performance.
/// </summary>
public static class BitBltEngine
{
    /// <summary>
    /// Perform a BitBlt operation with the specified parameters.
    /// </summary>
    public static void CopyBits(BitBltOperation op)
    {
        // Calculate clipped bounds
        int clipLeft = Math.Max(op.DestX, op.ClipX);
        int clipTop = Math.Max(op.DestY, op.ClipY);
        int clipRight = Math.Min(op.DestX + op.Width, op.ClipX + op.ClipWidth);
        int clipBottom = Math.Min(op.DestY + op.Height, op.ClipY + op.ClipHeight);

        // Also clip to destination bounds
        clipLeft = Math.Max(clipLeft, 0);
        clipTop = Math.Max(clipTop, 0);
        clipRight = Math.Min(clipRight, op.DestWidth);
        clipBottom = Math.Min(clipBottom, op.DestHeight);

        // Nothing to do if clipped out entirely
        if (clipLeft >= clipRight || clipTop >= clipBottom)
            return;

        // Adjust source coordinates for clipping
        int sourceX = op.SourceX + (clipLeft - op.DestX);
        int sourceY = op.SourceY + (clipTop - op.DestY);

        // Clipped dimensions
        int width = clipRight - clipLeft;
        int height = clipBottom - clipTop;

        // Perform the operation
        var halftoneSpan = op.HalftoneBits.HasValue ? op.HalftoneBits.Value.Span : ReadOnlySpan<byte>.Empty;
        CopyBitsInternal(
            op.DestBits.Span, op.DestWidth, clipLeft, clipTop,
            op.SourceBits.Span, op.SourceWidth, sourceX, sourceY,
            halftoneSpan, op.HalftoneWidth, op.HalftoneHeight,
            width, height, op.Rule);
    }

    /// <summary>
    /// Internal BitBlt implementation after clipping has been applied.
    /// </summary>
    private static unsafe void CopyBitsInternal(
        Span<byte> destBits, int destWidth, int destX, int destY,
        ReadOnlySpan<byte> sourceBits, int sourceWidth, int sourceX, int sourceY,
        ReadOnlySpan<byte> halftoneBits, int halftoneWidth, int halftoneHeight,
        int width, int height, CombinationRule rule)
    {
        // Calculate row strides in bytes (8 pixels per byte for 1-bit)
        int destStride = (destWidth + 7) / 8;
        int sourceStride = (sourceWidth + 7) / 8;
        int halftoneStride = (halftoneWidth + 7) / 8;

        // Process row by row
        for (int y = 0; y < height; y++)
        {
            int destRow = destY + y;
            int sourceRow = sourceY + y;

            // Process pixel by pixel (could be optimized to byte-level later)
            for (int x = 0; x < width; x++)
            {
                int destCol = destX + x;
                int sourceCol = sourceX + x;

                // Get source bit
                bool sourceBit = GetBit(sourceBits, sourceStride, sourceCol, sourceRow);

                // Get destination bit
                bool destBit = GetBit(destBits, destStride, destCol, destRow);

                // Get halftone bit if present
                bool halftoneBit = true;
                if (halftoneBits.Length > 0)
                {
                    int htX = destCol % halftoneWidth;
                    int htY = destRow % halftoneHeight;
                    halftoneBit = GetBit(halftoneBits, halftoneStride, htX, htY);
                }

                // Apply combination rule
                bool resultBit = ApplyRule(sourceBit, destBit, halftoneBit, rule);

                // Set destination bit
                SetBit(destBits, destStride, destCol, destRow, resultBit);
            }
        }
    }

    /// <summary>
    /// Get a bit from a bitmap at the specified coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetBit(ReadOnlySpan<byte> bits, int stride, int x, int y)
    {
        int byteOffset = y * stride + (x / 8);
        if (byteOffset >= bits.Length)
            return false;

        int bitOffset = 7 - (x % 8); // MSB first
        return (bits[byteOffset] & (1 << bitOffset)) != 0;
    }

    /// <summary>
    /// Set a bit in a bitmap at the specified coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(Span<byte> bits, int stride, int x, int y, bool value)
    {
        int byteOffset = y * stride + (x / 8);
        if (byteOffset >= bits.Length)
            return;

        int bitOffset = 7 - (x % 8); // MSB first
        if (value)
            bits[byteOffset] |= (byte)(1 << bitOffset);
        else
            bits[byteOffset] &= (byte)~(1 << bitOffset);
    }

    /// <summary>
    /// Apply a combination rule to compute the result bit.
    /// Implements all 16 boolean operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyRule(bool src, bool dst, bool halftone, CombinationRule rule)
    {
        // Apply halftone to source for certain operations
        // When halftone is used, source is ANDed with halftone pattern
        if (halftone == false)
        {
            // Halftone acts as a mask - if halftone bit is 0, source is treated as 0
            src = false;
        }

        return rule switch
        {
            CombinationRule.Clear => false,                    // 0
            CombinationRule.And => src & dst,                  // src AND dst
            CombinationRule.AndReverse => src & !dst,          // src AND (NOT dst)
            CombinationRule.Copy => src,                       // src
            CombinationRule.AndInverted => !src & dst,         // (NOT src) AND dst
            CombinationRule.Noop => dst,                       // dst (no change)
            CombinationRule.Xor => src ^ dst,                  // src XOR dst
            CombinationRule.Or => src | dst,                   // src OR dst
            CombinationRule.Nor => !(src | dst),               // NOT (src OR dst)
            CombinationRule.Equiv => !(src ^ dst),             // NOT (src XOR dst)
            CombinationRule.Invert => !dst,                    // NOT dst
            CombinationRule.OrReverse => src | !dst,           // src OR (NOT dst)
            CombinationRule.CopyInverted => !src,              // NOT src
            CombinationRule.OrInverted => !src | dst,          // (NOT src) OR dst
            CombinationRule.Nand => !(src & dst),              // NOT (src AND dst)
            CombinationRule.Set => true,                       // 1
            _ => dst // Default to noop for unknown rules
        };
    }
}
