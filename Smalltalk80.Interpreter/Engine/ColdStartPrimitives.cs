using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Implementation of VM primitives for cold start phases.
/// Provides the same object access semantics that Smalltalk code uses during runtime.
/// </summary>
public class ColdStartPrimitives : IColdStartPrimitives
{
    private readonly ISmalltalkHeapManager _heapManager;

    public ColdStartPrimitives(ISmalltalkHeapManager heapManager)
    {
        _heapManager = heapManager;
    }

    /// <summary>
    /// Primitive instVarAt: - Gets instance variable by 1-based index.
    /// </summary>
    public SmalltalkOOP InstVarAt(SmalltalkOOP objectOOP, int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "Instance variable indices are 1-based");

        // Convert to 0-based index for heap manager
        return _heapManager.GetInstanceVariable(objectOOP, index - 1);
    }

    /// <summary>
    /// Primitive instVarAt:put: - Sets instance variable by 1-based index.
    /// </summary>
    public void InstVarAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "Instance variable indices are 1-based");

        // Convert to 0-based index for heap manager
        _heapManager.SetInstanceVariable(objectOOP, index - 1, value);
    }

    /// <summary>
    /// Primitive 60: at: - Gets indexed content by 1-based index.
    /// </summary>
    public SmalltalkOOP At(SmalltalkOOP objectOOP, int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "Indices are 1-based in Smalltalk");

        var format = _heapManager.GetObjectFormat(objectOOP);
        
        // Convert to 0-based index for heap manager
        int zeroBasedIndex = index - 1;

        return format switch
        {
            ObjectFormat.PointerIndexed => HandlePointerIndexedAt(objectOOP, zeroBasedIndex),
            ObjectFormat.ByteIndexed => HandleByteIndexedAt(objectOOP, zeroBasedIndex),
            ObjectFormat.WordIndexed => HandleWordIndexedAt(objectOOP, zeroBasedIndex),
            _ => throw new InvalidOperationException($"Object format {format} does not support indexed access")
        };
    }

    /// <summary>
    /// Primitive 61: at:put: - Sets indexed content by 1-based index.
    /// </summary>
    public void AtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "Indices are 1-based in Smalltalk");

        var format = _heapManager.GetObjectFormat(objectOOP);
        
        // Convert to 0-based index for heap manager
        int zeroBasedIndex = index - 1;

        switch (format)
        {
            case ObjectFormat.PointerIndexed:
                HandlePointerIndexedAtPut(objectOOP, zeroBasedIndex, value);
                break;
            case ObjectFormat.ByteIndexed:
                HandleByteIndexedAtPut(objectOOP, zeroBasedIndex, value);
                break;
            case ObjectFormat.WordIndexed:
                HandleWordIndexedAtPut(objectOOP, zeroBasedIndex, value);
                break;
            default:
                throw new InvalidOperationException($"Object format {format} does not support indexed access");
        }
    }

    /// <summary>
    /// Primitive 68: objectAt: - Gets object content from CompiledMethod.
    /// </summary>
    public SmalltalkOOP ObjectAt(SmalltalkOOP objectOOP, int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "objectAt: indices are 1-based");

        // Check if this is a CompiledMethod by examining the class
        if (!IsCompiledMethod(objectOOP))
            throw new InvalidOperationException("objectAt: can only be used with CompiledMethod objects");

        // For CompiledMethod objects stored as PointerIndexed:
        // Index 1: header (at indexed position 0)
        // Index 2+: literals (at indexed positions 1, 2, 3...)
        
        // Get header to determine literal count for bounds checking
        var headerOOP = _heapManager.GetIndexedOOP(objectOOP, 0);
        if (!headerOOP.IsSmallInteger)
            throw new InvalidOperationException("CompiledMethod header must be a SmallInteger");

        // Decode literal count from header (bits 8-14)
        int headerWord = headerOOP.AsSmallInteger();
        int literalCount = (headerWord >> 8) & 0x7F;

        // Bounds checking: valid range is 1 to literalCount+1 (header + literals)
        if (index > literalCount + 1)
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Index {index} exceeds CompiledMethod bounds (header + {literalCount} literals)");

        // Convert to 0-based index and get from PointerIndexed object
        return _heapManager.GetIndexedOOP(objectOOP, index - 1);
    }

    /// <summary>
    /// Primitive 69: objectAt:put: - Sets object content in CompiledMethod.
    /// </summary>
    public void ObjectAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), "objectAt:put: indices are 1-based");

        // Check if this is a CompiledMethod by examining the class
        if (!IsCompiledMethod(objectOOP))
            throw new InvalidOperationException("objectAt:put: can only be used with CompiledMethod objects");

        // Same bounds checking as ObjectAt
        var headerOOP = _heapManager.GetIndexedOOP(objectOOP, 0);
        if (!headerOOP.IsSmallInteger)
            throw new InvalidOperationException("CompiledMethod header must be a SmallInteger");

        int headerWord = headerOOP.AsSmallInteger();
        int literalCount = (headerWord >> 8) & 0x7F;

        if (index > literalCount + 1)
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Index {index} exceeds CompiledMethod bounds (header + {literalCount} literals)");

        // Convert to 0-based index and set in PointerIndexed object
        _heapManager.SetIndexedOOP(objectOOP, index - 1, value);
    }

    // Helper methods for format-specific access

    private SmalltalkOOP HandlePointerIndexedAt(SmalltalkOOP objectOOP, int index)
    {
        // Check if this is a CompiledMethod - if so, handle bytecode delegation
        if (IsCompiledMethod(objectOOP))
        {
            // For CompiledMethod, at: should access the bytecode array
            // The bytecode array is stored as the last indexed OOP
            var headerOOP = _heapManager.GetIndexedOOP(objectOOP, 0);
            if (headerOOP.IsSmallInteger)
            {
                int headerWord = headerOOP.AsSmallInteger();
                int literalCount = (headerWord >> 8) & 0x7F;
                int bytecodeArrayIndex = 1 + literalCount; // Skip header + literals
                
                var bytecodeArrayOOP = _heapManager.GetIndexedOOP(objectOOP, bytecodeArrayIndex);
                
                // Delegate to the ByteArray's at: primitive
                return HandleByteIndexedAt(bytecodeArrayOOP, index);
            }
        }
        
        // Standard PointerIndexed access
        return _heapManager.GetIndexedOOP(objectOOP, index);
    }

    private void HandlePointerIndexedAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        // Similar delegation logic for CompiledMethod bytecode access
        if (IsCompiledMethod(objectOOP))
        {
            var headerOOP = _heapManager.GetIndexedOOP(objectOOP, 0);
            if (headerOOP.IsSmallInteger)
            {
                int headerWord = headerOOP.AsSmallInteger();
                int literalCount = (headerWord >> 8) & 0x7F;
                int bytecodeArrayIndex = 1 + literalCount;
                
                var bytecodeArrayOOP = _heapManager.GetIndexedOOP(objectOOP, bytecodeArrayIndex);
                HandleByteIndexedAtPut(bytecodeArrayOOP, index, value);
                return;
            }
        }
        
        // Standard PointerIndexed access
        _heapManager.SetIndexedOOP(objectOOP, index, value);
    }

    private SmalltalkOOP HandleByteIndexedAt(SmalltalkOOP objectOOP, int index)
    {
        byte byteValue = _heapManager.GetIndexedByte(objectOOP, index);
        return SmalltalkOOP.FromSmallInteger(byteValue);
    }

    private void HandleByteIndexedAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (!value.IsSmallInteger)
            throw new ArgumentException("ByteIndexed objects can only store SmallInteger values (0-255)");
        
        int intValue = value.AsSmallInteger();
        if (intValue < 0 || intValue > 255)
            throw new ArgumentOutOfRangeException(nameof(value), "Byte values must be 0-255");
        
        _heapManager.SetIndexedByte(objectOOP, index, (byte)intValue);
    }

    private SmalltalkOOP HandleWordIndexedAt(SmalltalkOOP objectOOP, int index)
    {
        ushort wordValue = _heapManager.GetIndexedWord(objectOOP, index);
        return SmalltalkOOP.FromSmallInteger(wordValue);
    }

    private void HandleWordIndexedAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (!value.IsSmallInteger)
            throw new ArgumentException("WordIndexed objects can only store SmallInteger values");
        
        int intValue = value.AsSmallInteger();
        if (intValue < 0 || intValue > 65535)
            throw new ArgumentOutOfRangeException(nameof(value), "Word values must be 0-65535");
        
        _heapManager.SetIndexedWord(objectOOP, index, (ushort)intValue);
    }

    /// <summary>
    /// Determines if an object is a CompiledMethod by checking its class.
    /// During cold start, we can check the class OOP once it's been set.
    /// </summary>
    private bool IsCompiledMethod(SmalltalkOOP objectOOP)
    {
        try
        {
            var classOOP = _heapManager.GetClassOOP(objectOOP);
            // TODO: Look up CompiledMethod class OOP from GlobalRegistry
            // For now, return false to disable CompiledMethod-specific behavior during early cold start
            return false;
        }
        catch
        {
            // If class OOP is not set yet, assume it's not a CompiledMethod
            return false;
        }
    }
}