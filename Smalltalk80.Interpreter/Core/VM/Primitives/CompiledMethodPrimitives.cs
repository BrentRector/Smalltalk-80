using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Primitives for CompiledMethod operations (Primitive 79).
/// </summary>
public static class CompiledMethodPrimitives
{
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

        // CompiledMethod has 1 fixed slot (homeClass) plus indexed slots
        int fixedSlots = 1;  // slot 0 = homeClass
        int indexedSlots = 1 + 1; // header + bytecodeArrayOOP (no literals for primitive 79)

        // Step 1: Create separate ByteArray for bytecodes
        var bytecodeArrayOOP = ctx.HeapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, numberOfBytes);
        ctx.HeapManager.SetSlot(bytecodeArrayOOP, 0, byteArrayClassOOP);
        for (int i = 0; i < numberOfBytes; i++)
        {
            ctx.HeapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // Step 2: Allocate PointerIndexed CompiledMethod
        var methodOOP = ctx.HeapManager.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots, indexedSlots);
        ctx.HeapManager.SetSlot(methodOOP, 0, compiledMethodClassOOP);

        // Step 3: Set fixed slot 0 (homeClass) to nil for now
        ctx.HeapManager.SetInstanceVariable(methodOOP, 0, SpecialObjectsArray.Instance.Nil);

        // Step 4: Populate indexed slots
        // Indexed slot 0: Method header
        var headerOOP = SmalltalkOOP.FromSmallInteger(headerWord);
        ctx.HeapManager.SetIndexedOOP(methodOOP, 0, headerOOP);

        // Indexed slot 1: Hidden ByteArray reference
        ctx.HeapManager.SetIndexedOOP(methodOOP, 1, bytecodeArrayOOP);

        ctx.PrimitiveSuccess = true;
        return methodOOP;
    }
}
