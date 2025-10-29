using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Primitives for CompiledMethod operations (Primitive 79).
/// </summary>
public static class CompiledMethodPrimitives
{
    /// <summary>
    /// Internal shared helper for allocating CompiledMethod objects.
    /// Used by both primitive 79 (runtime) and SemanticAllocator (cold start).
    /// This eliminates code duplication while maintaining the proper separation of concerns.
    /// </summary>
    /// <param name="heapManager">Heap manager for object allocation</param>
    /// <param name="compiledMethodClassOOP">OOP of class:CompiledMethod</param>
    /// <param name="byteArrayClassOOP">OOP of class:ByteArray</param>
    /// <param name="homeClassOOP">OOP of the method's home class (or nil for blocks/primitives)</param>
    /// <param name="headerWord">Method header word (SmallInteger value)</param>
    /// <param name="literalOOPs">Array of literal OOPs (can be empty for primitive 79)</param>
    /// <param name="bytecodes">Bytecode array</param>
    /// <returns>OOP of the newly allocated CompiledMethod</returns>
    internal static SmalltalkOOP AllocateCompiledMethodInternal(
        ISmalltalkHeapManager heapManager,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP,
        SmalltalkOOP homeClassOOP,
        int headerWord,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes)
    {
        // DIAGNOSTIC: Log bytecodes received for methods starting with byte 33
        if (bytecodes.Length > 0 && bytecodes[0] == 33)
        {
            Console.WriteLine($"[AllocateCompiledMethodInternal] Received bytecodes starting with 33:");
            Console.WriteLine($"  Bytes[0-4]: {string.Join(", ", bytecodes.Take(5).Select(b => $"{b}(0x{b:X2})"))}");
        }

        // CompiledMethod has 1 fixed slot (homeClass) plus indexed slots
        int fixedSlots = 1;  // slot 0 = homeClass
        int indexedSlots = 1 + literalOOPs.Length + 1; // header + literals + bytecodeArrayOOP

        // Step 1: Create separate ByteArray for bytecodes
        var bytecodeArrayOOP = heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, bytecodes.Length);
        heapManager.SetSlot(bytecodeArrayOOP, 0, byteArrayClassOOP);
        for (int i = 0; i < bytecodes.Length; i++)
        {
            heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // Step 2: Allocate PointerIndexed CompiledMethod
        // Format is PointerIndexed because we have both fixed slots and indexed slots
        var methodOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots, indexedSlots);
        heapManager.SetSlot(methodOOP, 0, compiledMethodClassOOP);

        // Step 3: Set fixed slot 0 (homeClass) - needed for super sends
        heapManager.SetInstanceVariable(methodOOP, 0, homeClassOOP);

        // Step 4: Populate indexed slots
        // Indexed slot 0: Method header (accessed via objectAt: 1)
        var headerOOP = SmalltalkOOP.FromSmallInteger(headerWord);
        heapManager.SetIndexedOOP(methodOOP, 0, headerOOP);

        // Slots 1..N: Literals (accessed via objectAt: 2, objectAt: 3, etc.)
        for (int i = 0; i < literalOOPs.Length; i++)
        {
            heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
        }

        // Slot N+1: Hidden ByteArray reference (not accessible from Smalltalk)
        heapManager.SetIndexedOOP(methodOOP, literalOOPs.Length + 1, bytecodeArrayOOP);

        return methodOOP;
    }

    // Primitive 79: CompiledMethod class>>newMethod:header:
    // Creates a new CompiledMethod with the specified number of bytecodes and header word.
    // Used by CompiledMethod initialize to measure header sizes.
    public static SmalltalkOOP NewMethodHeader(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int numberOfBytes = args[0].AsSmallInteger();
        int headerWord = args[1].AsSmallInteger();

        // Validate numberOfBytes is non-negative
        if (numberOfBytes < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get class OOPs
        if (!GlobalRegistry.TryGet("class:CompiledMethod", out var compiledMethodClassOOP) ||
            !GlobalRegistry.TryGet("class:ByteArray", out var byteArrayClassOOP))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Create bytecode array (initialized to zeros)
        byte[] bytecodes = new byte[numberOfBytes];

        // Use shared helper with empty literals array (primitive 79 creates methods with no literals)
        var methodOOP = AllocateCompiledMethodInternal(
            ctx.HeapManager,
            compiledMethodClassOOP,
            byteArrayClassOOP,
            SpecialObjectsArray.Instance.Nil,  // homeClass = nil for primitive 79
            headerWord,
            Array.Empty<SmalltalkOOP>(),       // no literals
            bytecodes);

        ctx.PrimitiveSuccess = true;
        return methodOOP;
    }
}
