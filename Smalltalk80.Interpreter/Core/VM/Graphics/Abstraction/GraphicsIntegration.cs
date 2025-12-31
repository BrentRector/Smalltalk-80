// Graphics Abstraction Layer - Integration with Smalltalk Primitives
// Bridges Smalltalk Form objects to IGraphicsBackend textures

using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Static helper for integrating graphics backend with Smalltalk primitives.
/// Provides methods to convert between Form objects and ITexture.
/// </summary>
public static class GraphicsIntegration
{
    /// <summary>
    /// Whether to use the graphics abstraction layer for BitBlt operations.
    /// Set to false to use the legacy BitBltEngine directly.
    /// </summary>
    public static bool UseAbstractionLayer { get; set; } = false;

    /// <summary>
    /// Perform a BitBlt operation using the graphics abstraction layer.
    /// Returns false if the abstraction layer is not available or disabled.
    /// </summary>
    public static bool TryPerformBitBlt(
        ISmalltalkHeap heap,
        SmalltalkOOP destFormOOP,
        SmalltalkOOP sourceFormOOP,
        SmalltalkOOP halftoneFormOOP,
        int combinationRule,
        int destX, int destY,
        int width, int height,
        int sourceX, int sourceY,
        int clipX, int clipY, int clipWidth, int clipHeight,
        out Memory<byte> modifiedDestBits)
    {
        modifiedDestBits = Memory<byte>.Empty;

        // Check if abstraction layer is enabled
        if (!UseAbstractionLayer)
            return false;

        var backend = GraphicsBackendFactory.Current;
        if (backend == null)
            return false;

        // For now, only use abstraction layer for 32-bit depth
        // 1-bit operations continue to use BitBltEngine for compatibility
        // This will change once we migrate to 32-bit displays
        return false;
    }

    /// <summary>
    /// Extract Form bitmap data into a managed byte array.
    /// This is the same logic as GraphicsOperations.ExtractFormData but accessible here.
    /// </summary>
    public static (Memory<byte> bits, int width, int height) ExtractFormData(
        ISmalltalkHeap heap, SmalltalkOOP formOOP)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        // Get Form instance variables: bits (0), width (1), height (2)
        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        SmalltalkOOP widthOOP = heap.GetInstanceVariable(formOOP, 1);
        SmalltalkOOP heightOOP = heap.GetInstanceVariable(formOOP, 2);

        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();

        // Get bitmap data
        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        // Check object format to determine how to extract bits
        var format = heap.GetObjectFormat(bitsOOP);
        int size = heap.GetIndexedSize(bitsOOP);
        byte[] bits;

        if (format == ObjectFormat.WordIndexed)
        {
            // WordArray: bulk copy words as big-endian bytes
            bits = new byte[size * 2];
            heap.CopyIndexedWords(bitsOOP, bits.AsSpan());
        }
        else
        {
            // ByteArray: bulk copy bytes directly
            bits = new byte[size];
            heap.CopyIndexedBytes(bitsOOP, bits.AsSpan());
        }

        return (bits, width, height);
    }

    /// <summary>
    /// Write bitmap data back to a Form object.
    /// </summary>
    public static void WriteFormData(
        ISmalltalkHeap heap, SmalltalkOOP formOOP, Memory<byte> bits)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger || bits.Length == 0)
            return;

        // Get Form bitmap OOP
        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return;

        // Write bytes back to heap using bulk operations
        var format = heap.GetObjectFormat(bitsOOP);

        if (format == ObjectFormat.WordIndexed)
        {
            // WordArray: bulk copy big-endian bytes to words
            heap.SetIndexedWords(bitsOOP, bits.Span);
        }
        else
        {
            // ByteArray: bulk copy bytes directly
            heap.SetIndexedBytes(bitsOOP, bits.Span);
        }
    }

    /// <summary>
    /// Create a CPU texture from Form data.
    /// </summary>
    public static ITexture? CreateTextureFromForm(
        ISmalltalkHeap heap, SmalltalkOOP formOOP, PixelDepth depth = PixelDepth.OneBit)
    {
        var (bits, width, height) = ExtractFormData(heap, formOOP);
        if (bits.Length == 0 || width <= 0 || height <= 0)
            return null;

        var backend = GraphicsBackendFactory.Current;
        if (backend == null)
            return null;

        return backend.CreateTextureFromData(width, height, depth, bits.Span);
    }

    /// <summary>
    /// Map Smalltalk combination rule to BlendMode.
    /// </summary>
    public static BlendMode MapCombinationRule(int rule)
    {
        // Rules 0-15 map directly
        if (rule >= 0 && rule <= 15)
            return (BlendMode)rule;

        // Rule 16 (paint) maps to Or
        if (rule == 16)
            return BlendMode.Or;

        // Default to Copy for unknown rules
        return BlendMode.Copy;
    }

    /// <summary>
    /// Map BlendMode back to CombinationRule for legacy code.
    /// </summary>
    public static CombinationRule MapBlendModeToCombinationRule(BlendMode mode)
    {
        int rule = (int)mode;
        if (rule >= 0 && rule <= 15)
            return (CombinationRule)rule;

        // Unsupported blend modes fall back to Copy
        return CombinationRule.Copy;
    }
}
