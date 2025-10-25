using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// BlockClosure primitive operations (Primitive 207).
/// Implements block evaluation with proper lexical scoping.
/// </summary>
public static class BlockClosurePrimitives
{
    // Primitive 207: BlockClosure valueWithArguments: OR BlockClosure value
    public static SmalltalkOOP ValueWithArguments(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // This primitive is called from BOTH:
        // 1. value/value:/value:value: - args are passed DIRECTLY (not wrapped in array)
        // 2. valueWithArguments: - single arg is an Array

        SmalltalkOOP[] blockArgs;

        if (args.Length == 0)
        {
            // value with no arguments
            blockArgs = new SmalltalkOOP[0];
        }
        else if (args.Length == 1 && !args[0].IsSmallInteger)
        {
            // Distinguish between value: (direct arg) and valueWithArguments: (array arg)
            // by checking if argument has indexed size (is array-like)
            // GetIndexedSize returns 0 for objects without indexed slots
            int indexedSize = ctx.GetIndexedSize(args[0]);

            if (indexedSize > 0)
            {
                // valueWithArguments: - arg is an Array, extract elements
                blockArgs = ExtractArrayElements(ctx, args[0]);
            }
            else
            {
                // value: - arg is a direct parameter
                blockArgs = args;
            }
        }
        else
        {
            // Multiple arguments (from value:, value:value:, etc.) - use directly
            blockArgs = args;
        }

        // Extract block method from BlockClosure instance variable 'method'
        var methodIndex = ctx.GetFieldIndex("class:BlockClosure", "method");
        var blockMethodOOP = ctx.GetInstanceVariable(receiver, methodIndex);

        // Extract homeContext from BlockClosure instance variable 'homeContext'
        var homeContextIndex = ctx.GetFieldIndex("class:BlockClosure", "homeContext");
        var homeContextOOP = ctx.GetInstanceVariable(receiver, homeContextIndex);

        // Extract numArgs from BlockClosure to validate argument count
        var numArgsIndex = ctx.GetFieldIndex("class:BlockClosure", "numArgs");
        var numArgsOOP = ctx.GetInstanceVariable(receiver, numArgsIndex);
        int expectedArgCount = numArgsOOP.IsSmallInteger ? numArgsOOP.AsSmallInteger() : 0;

        // Validate argument count matches BlockClosure's numArgs
        if (blockArgs.Length != expectedArgCount)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Invoke the block using the VM method
        // Pass the BlockClosure instance (receiver) as the 4th parameter for nested-block
        // support and proper lexical scoping
        var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP, receiver);

        return result;
    }

    /// <summary>
    /// Extracts SmallInteger and OOP elements from a Smalltalk Array.
    /// </summary>
    private static SmalltalkOOP[] ExtractArrayElements(IPrimitiveContext ctx, SmalltalkOOP arrayOOP)
    {
        // Get the array size
        var indexedSize = ctx.GetIndexedSize(arrayOOP);

        var elements = new SmalltalkOOP[indexedSize];
        for (int i = 0; i < indexedSize; i++)
        {
            elements[i] = ctx.GetIndexedOOP(arrayOOP, i);
        }

        return elements;
    }
}
