using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.Attributes;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Object graph allocator that coordinates the allocation of complex Smalltalk object structures.
/// Wraps the low-level heap manager with knowledge of Smalltalk semantics (object formats, slot calculations, etc.).
/// Used during cold start to build the initial object graph from ColdStartBundle.
///
/// COLD START ONLY: This class uses ColdStartBundle which is stale after cold start completes.
/// Runtime code must NOT use this allocator.
/// </summary>
[ColdStartOnly(Reason = "Uses ColdStartBundle which is stale after cold start")]
public class ObjectGraphAllocator
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly ColdStartBundle _bundle;

    public ObjectGraphAllocator(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        _heapManager = heapManager;
        _bundle = bundle;
    }

    /// <summary>
    /// Gets the placeholder nil OOP (allocated during heap manager construction).
    /// This OOP remains valid throughout cold start and becomes the real UndefinedObject via become:.
    /// </summary>
    public SmalltalkOOP GetNil() => _heapManager.GetNil();

    /// <summary>
    /// Allocates a basic object with only fixed instance variables (Pointer format).
    /// Common for: Association, Character, True/False/UndefinedObject, etc.
    /// Does NOT set class reference - must be done in separate pass.
    /// </summary>
    public SmalltalkOOP AllocateFixedObject(string className)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);
        
        // Just allocate - no cross-references set
        return _heapManager.AllocateObject(ObjectFormat.Pointer, fixedSlots, 0);
    }

    /// <summary>
    /// Allocates a string object (ByteIndexed format).
    /// Common for: Symbol, String literals.
    /// Does NOT set class reference - must be done in separate pass.
    /// </summary>
    public SmalltalkOOP AllocateString(string value)
    {
        int fixedSlots = CalculateInstanceVariableCount("String");
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);
        
        // Allocate ByteIndexed object
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots, stringBytes.Length);
        
        // Store the string bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }
        
        return stringOOP;
    }

    /// <summary>
    /// Allocates a string object (ByteIndexed format) with class reference set.
    /// Use this version when the String class is available (Phase 7 onwards).
    /// </summary>
    public SmalltalkOOP AllocateString(SmalltalkOOP classOOP, string value)
    {
        int fixedSlots = CalculateInstanceVariableCount("String");
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots, stringBytes.Length);

        // Set class reference
        _heapManager.SetClassOOP(stringOOP, classOOP);

        // Store the string bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        return stringOOP;
    }

    /// <summary>
    /// Allocates a string object (ByteIndexed format) with specific class name.
    /// Overload for backward compatibility.
    /// </summary>
    public SmalltalkOOP AllocateString(string className, int stringLength)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);

        // Just allocate - no cross-references set
        return _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots, stringLength);
    }

    /// <summary>
    /// Allocates an array object (PointerIndexed format) with class reference set.
    /// Common for: Array, USTable, MethodDictionary, CompiledMethod.
    /// REQUIRES classOOP parameter to ensure class reference is always set.
    /// This is the new API for Phase 7+ that eliminates NULL_CLASS issues.
    /// </summary>
    public SmalltalkOOP AllocateArray(SmalltalkOOP classOOP, int elementCount)
    {
        int fixedSlots = CalculateInstanceVariableCountFromClass(classOOP);
        
        // Allocate the object
        var arrayOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots, elementCount);
        
        // ALWAYS set class reference immediately - no way to forget this step
        _heapManager.SetClassOOP(arrayOOP, classOOP);
        
        return arrayOOP;
    }

    /// <summary>
    /// Allocates an array object (PointerIndexed format) - legacy API for early phases.
    /// Common for: Array, USTable, MethodDictionary, CompiledMethod.
    /// Does NOT set class reference - must be done in separate pass.
    /// This is the old API used by Phases 0-6 during bootstrap.
    /// </summary>
    public SmalltalkOOP AllocateArray(string className, int elementCount)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);
        
        // Just allocate - no cross-references set (bootstrap phase)
        return _heapManager.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots, elementCount);
    }

    /// <summary>
    /// Allocates a byte array object (ByteIndexed format).
    /// Common for: ByteArray, Float (8 bytes), bytecode arrays.
    /// Does NOT set class reference - must be done in separate pass.
    /// </summary>
    public SmalltalkOOP AllocateByteArray(string className, byte[] bytes)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);
        
        // Allocate ByteIndexed object
        var byteArrayOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots, bytes.Length);
        
        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _heapManager.SetIndexedByte(byteArrayOOP, i, bytes[i]);
        }
        
        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates a byte array object (ByteIndexed format) with size only.
    /// Overload for backward compatibility.
    /// </summary>
    public SmalltalkOOP AllocateByteArray(string className, int byteCount)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);
        
        // Just allocate - no cross-references set
        return _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots, byteCount);
    }

    /// <summary>
    /// Low-level allocation for custom object layouts.
    /// Does NOT set class reference - must be done in separate pass.
    /// </summary>
    public SmalltalkOOP AllocateObject(string className, ObjectFormat format, int indexedSize)
    {
        int fixedSlots = CalculateInstanceVariableCount(className);
        
        // Just allocate - no cross-references set
        return _heapManager.AllocateObject(format, fixedSlots, indexedSize);
    }

    /// <summary>
    /// Sets the class OOP in slot 0 (used to fix placeholder references).
    /// </summary>
    public void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
    {
        _heapManager.SetClassOOP(objectOOP, classOOP);
    }

    /// <summary>
    /// Gets indexed byte from ByteIndexed objects.
    /// </summary>
    public byte GetIndexedByte(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetIndexedByte(objectOOP, index);
    }

    /// <summary>
    /// Sets indexed byte in ByteIndexed objects.
    /// </summary>
    public void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value)
    {
        _heapManager.SetIndexedByte(objectOOP, index, value);
    }

    /// <summary>
    /// Gets indexed OOP from PointerIndexed objects.
    /// </summary>
    public SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetIndexedOOP(objectOOP, index);
    }

    /// <summary>
    /// Sets indexed OOP in PointerIndexed objects.
    /// </summary>
    public void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        _heapManager.SetIndexedOOP(objectOOP, index, value);
    }

    /// <summary>
    /// Gets instance variable from objects.
    /// </summary>
    public SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetInstanceVariable(objectOOP, index);
    }

    /// <summary>
    /// Sets instance variable in objects.
    /// </summary>
    public void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        _heapManager.SetInstanceVariable(objectOOP, index, value);
    }

    /// <summary>
    /// Calculates the total instance variable count from a class object.
    /// Currently uses conservative approach during development.
    /// TODO: Implement full class introspection when class objects are fully formed.
    /// </summary>
    private int CalculateInstanceVariableCountFromClass(SmalltalkOOP classOOP)
    {
        // For now, use well-known class mappings to determine instance variable counts
        // This is accurate for the common cases and safer during bootstrap
        
        // Look up well-known classes by their OOPs
        if (classOOP == GlobalRegistry.LookupClassOOP("Array"))
            return 0; // Array has no instance variables (pure indexed)
        else if (classOOP == GlobalRegistry.LookupClassOOP("MethodDictionary"))
            return 2; // MethodDictionary has 2 instance variables (valueArray + tally)
        else if (classOOP == GlobalRegistry.LookupClassOOP("CompiledMethod"))
            return 0; // CompiledMethod has no instance variables (pure indexed with header + literals)
        else
        {
            // For unknown classes during Phase 7+, use conservative approach
            // TODO: Implement actual class object introspection
            return 0;
        }
    }

    /// <summary>
    /// Calculates the total instance variable count for a class.
    /// </summary>
    private int CalculateInstanceVariableCount(string className)
    {
        return ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(className, _bundle);
    }

    /// <summary>
    /// Gets the indexed size of an object.
    /// </summary>
    public int GetIndexedSize(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetIndexedSize(objectOOP);
    }

    /// <summary>
    /// Gets the instance variable count of an object.
    /// </summary>
    public int GetInstanceVariableCount(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetInstanceVariableCount(objectOOP);
    }

    /// <summary>
    /// Validates that an OOP refers to a valid object.
    /// </summary>
    public bool IsValidObjectOOP(SmalltalkOOP objectOOP)
    {
        return _heapManager.IsValidObjectOOP(objectOOP);
    }

    /// <summary>
    /// Gets the object format from ObjectTableEntry.
    /// </summary>
    public ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetObjectFormat(objectOOP);
    }

    /// <summary>
    /// Gets the OOP stored in slot 0 (class reference).
    /// </summary>
    public SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetClassOOP(objectOOP);
    }

    /// <summary>
    /// Implements objectAt: primitive with proper index translation.
    /// Smalltalk objectAt: 1 → C# indexed slot 0 (header)
    /// Smalltalk objectAt: 2 → C# indexed slot 1 (literal1)
    /// Does NOT provide access to hidden bytecode array reference.
    /// </summary>
    public SmalltalkOOP ObjectAt(SmalltalkOOP objectOOP, int smalltalkIndex)
    {
        if (smalltalkIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), "Smalltalk uses 1-based indexing");
        }
        
        // Convert Smalltalk 1-based to C# 0-based
        int csharpIndex = smalltalkIndex - 1;
        
        // Get object format to determine access limits
        var format = _heapManager.GetObjectFormat(objectOOP);
        int indexedSize = _heapManager.GetIndexedSize(objectOOP);
        
        // For CompiledMethod, hide the last slot (bytecode array reference)
        if (IsCompiledMethod(objectOOP))
        {
            int visibleSlots = indexedSize - 1; // Hide bytecode array OOP
            if (csharpIndex >= visibleSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {visibleSlots}]");
            }
        }
        else
        {
            if (csharpIndex >= indexedSize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {indexedSize}]");
            }
        }
        
        return _heapManager.GetIndexedOOP(objectOOP, csharpIndex);
    }

    /// <summary>
    /// Implements objectAt:put: primitive with proper index translation.
    /// </summary>
    public void ObjectAtPut(SmalltalkOOP objectOOP, int smalltalkIndex, SmalltalkOOP value)
    {
        if (smalltalkIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), "Smalltalk uses 1-based indexing");
        }
        
        // Convert Smalltalk 1-based to C# 0-based
        int csharpIndex = smalltalkIndex - 1;
        
        // Get object format to determine access limits
        var format = _heapManager.GetObjectFormat(objectOOP);
        int indexedSize = _heapManager.GetIndexedSize(objectOOP);
        
        // For CompiledMethod, hide the last slot (bytecode array reference)
        if (IsCompiledMethod(objectOOP))
        {
            int visibleSlots = indexedSize - 1; // Hide bytecode array OOP
            if (csharpIndex >= visibleSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {visibleSlots}]");
            }
        }
        else
        {
            if (csharpIndex >= indexedSize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {indexedSize}]");
            }
        }
        
        _heapManager.SetIndexedOOP(objectOOP, csharpIndex, value);
    }

    /// <summary>
    /// Implements at: primitive for byte-indexed access.
    /// For CompiledMethod: accesses bytecodes via hidden ByteArray reference.
    /// For other ByteIndexed objects: accesses bytes directly.
    /// </summary>
    public byte At(SmalltalkOOP objectOOP, int smalltalkIndex)
    {
        if (smalltalkIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), "Smalltalk uses 1-based indexing");
        }
        
        // Convert Smalltalk 1-based to C# 0-based
        int csharpIndex = smalltalkIndex - 1;
        
        if (IsCompiledMethod(objectOOP))
        {
            // Access bytecodes via hidden ByteArray reference
            int indexedSize = _heapManager.GetIndexedSize(objectOOP);
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(objectOOP, indexedSize - 1); // Last slot
            
            int bytecodeArraySize = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            if (csharpIndex >= bytecodeArraySize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Bytecode index {smalltalkIndex} out of range [1, {bytecodeArraySize}]");
            }
            
            return _heapManager.GetIndexedByte(bytecodeArrayOOP, csharpIndex);
        }
        else
        {
            // Direct byte access for other ByteIndexed objects
            int indexedSize = _heapManager.GetIndexedSize(objectOOP);
            if (csharpIndex >= indexedSize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {indexedSize}]");
            }
            
            return _heapManager.GetIndexedByte(objectOOP, csharpIndex);
        }
    }

    /// <summary>
    /// Implements at:put: primitive for byte-indexed access.
    /// </summary>
    public void AtPut(SmalltalkOOP objectOOP, int smalltalkIndex, byte value)
    {
        if (smalltalkIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), "Smalltalk uses 1-based indexing");
        }
        
        // Convert Smalltalk 1-based to C# 0-based
        int csharpIndex = smalltalkIndex - 1;
        
        if (IsCompiledMethod(objectOOP))
        {
            // Access bytecodes via hidden ByteArray reference
            int indexedSize = _heapManager.GetIndexedSize(objectOOP);
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(objectOOP, indexedSize - 1); // Last slot
            
            int bytecodeArraySize = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            if (csharpIndex >= bytecodeArraySize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Bytecode index {smalltalkIndex} out of range [1, {bytecodeArraySize}]");
            }
            
            _heapManager.SetIndexedByte(bytecodeArrayOOP, csharpIndex, value);
        }
        else
        {
            // Direct byte access for other ByteIndexed objects
            int indexedSize = _heapManager.GetIndexedSize(objectOOP);
            if (csharpIndex >= indexedSize)
            {
                throw new ArgumentOutOfRangeException(nameof(smalltalkIndex), $"Index {smalltalkIndex} out of range [1, {indexedSize}]");
            }
            
            _heapManager.SetIndexedByte(objectOOP, csharpIndex, value);
        }
    }

    /// <summary>
    /// Determines if an object is a CompiledMethod based on its class.
    /// </summary>
    private bool IsCompiledMethod(SmalltalkOOP objectOOP)
    {
        // Get class OOP and check if it's the CompiledMethod class
        var classOOP = _heapManager.GetClassOOP(objectOOP);
        SmalltalkOOP compiledMethodClassOOP = GlobalRegistry.LookupClassOOP("CompiledMethod");
        return classOOP == compiledMethodClassOOP;
    }

    /// <summary>
    /// Allocates a Float object (WordIndexed format with 4 words for IEEE 754 double).
    /// Float is variableWordSubclass in Blue Book, so must use WordIndexed format.
    /// Stores as 4x 16-bit words to maintain proper IEEE 754 little-endian byte order.
    /// </summary>
    public SmalltalkOOP AllocateFloat(double value)
    {
        int fixedSlots = CalculateInstanceVariableCount("Float");

        // Allocate WordIndexed object with 4 words (16-bit each = 8 bytes total)
        // Blue Book: Float is "variableWordSubclass" → WordIndexed format
        // IMPORTANT: Store as 4x 16-bit words (not 2x 32-bit) to avoid byte-swapping issues
        var floatOOP = _heapManager.AllocateObject(ObjectFormat.WordIndexed, fixedSlots, 4);

        // Convert double to IEEE 754 binary representation in little-endian byte order
        byte[] bytes = BitConverter.GetBytes(value);

        // Store as 4 words (16-bit each) preserving little-endian byte order
        for (int i = 0; i < 4; i++)
        {
            // Combine 2 bytes into a 16-bit word (little-endian)
            ushort word = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            _heapManager.SetIndexedWord(floatOOP, i, word);
        }

        return floatOOP;
    }

    /// <summary>
    /// Allocates a CompiledMethod object following our C# design:
    /// Fixed slot 0: homeClass (needed for super sends)
    /// Indexed slots: [header, literal1, literal2, ..., literalN, bytecodeArrayOOP]
    /// + Separate ByteArray for actual bytecodes
    ///
    /// Smalltalk access patterns:
    /// - objectAt: 1 → header (our indexed slot 0)
    /// - objectAt: 2 → literal1 (our indexed slot 1)
    /// - at: 1 → first bytecode (via hidden ByteArray reference)
    /// </summary>
    public SmalltalkOOP AllocateCompiledMethod(
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP,
        SmalltalkOOP homeClassOOP,
        int headerWord,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes)
    {
        // Delegate to shared helper (eliminates code duplication with primitive 79)
        return CompiledMethodHelpers.AllocateCompiledMethodInternal(
            _heapManager,
            compiledMethodClassOOP,
            byteArrayClassOOP,
            homeClassOOP,
            headerWord,
            literalOOPs,
            bytecodes);
    }

    /// <summary>
    /// Gets the class name of an object for debugging/validation purposes.
    /// </summary>
    public string GetClassName(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
        {
            return "SmallInteger";
        }

        var classOOP = _heapManager.GetClassOOP(objectOOP);
        if (classOOP == _heapManager.GetNil())
        {
            return "NULL_CLASS";
        }

        // Get the class name from the class object (instance variable 6 in Class - the 'name' field)
        var nameOOP = _heapManager.GetInstanceVariable(classOOP, 6);
        return GetSymbolName(nameOOP);
    }

    /// <summary>
    /// Gets the string value of a Symbol object.
    /// </summary>
    public string GetSymbolName(SmalltalkOOP symbolOOP)
    {
        if (symbolOOP == _heapManager.GetNil() || symbolOOP.IsSmallInteger)
        {
            return "<invalid symbol>";
        }

        int byteCount = _heapManager.GetIndexedSize(symbolOOP);
        byte[] bytes = new byte[byteCount];

        for (int i = 0; i < byteCount; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

}