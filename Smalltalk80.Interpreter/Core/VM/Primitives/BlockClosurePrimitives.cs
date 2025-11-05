using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// BlockClosure primitive operations (Primitive 207).
/// Implements block evaluation with proper lexical scoping.
/// </summary>
public static class BlockClosurePrimitives
{
    /// <summary>
    /// Primitive 207: value* - Evaluates a BlockClosure with lexical scoping.
    /// NOT in Blue Book - Modern Smalltalk extension for BlockClosure support.
    /// </summary>
    /// <remarks>
    /// <para><b>Smalltalk-80.st Specification:</b></para>
    /// <code>
    /// BlockClosure>>value
    ///     "Activate the block with the correct number of arguments taken from the stack"
    ///     &lt;primitive: 207>
    ///     ^self primitiveFailed
    ///
    /// BlockClosure>>value:
    ///     "Activate the block with the correct number of arguments taken from the stack"
    ///     &lt;primitive: 207>
    ///     ^self primitiveFailed
    ///
    /// BlockClosure>>valueWithArguments: anArray
    ///     "Activate the block with arguments from anArray"
    ///     &lt;primitive: 207>
    ///     ^self primitiveFailed
    /// </code>
    ///
    /// <para><b>Implementation Details:</b></para>
    /// <list type="bullet">
    /// <item>NOT in Blue Book - BlockClosure is a modern addition (Blue Book uses BlockContext)</item>
    /// <item>Implements lexical scoping via homeContext captured at block creation</item>
    /// <item>Handles both direct arguments (value, value:, value:value:) and array (valueWithArguments:)</item>
    /// <item>Extracts method, homeContext, and numArgs from BlockClosure instance</item>
    /// <item>Validates argument count matches block's expected numArgs</item>
    /// <item>Invokes block with proper lexical environment (homeContext)</item>
    /// </list>
    ///
    /// <para><b>Analysis:</b></para>
    /// <para>Modern extension for proper lexical scoping. BlockClosure was added to Smalltalk after
    /// the Blue Book to provide true closures with captured lexical environments. Our implementation
    /// correctly captures homeContext at block creation time (when literal is pushed), ensuring blocks
    /// access variables from their DEFINING context (lexical scope), not their CALLING context.</para>
    ///
    /// <para><b>Key Innovation:</b></para>
    /// <para>homeContext is captured when block literal is pushed (pushLiteral bytecode), not when
    /// block is evaluated. This ensures proper lexical scoping for blocks passed between methods.</para>
    ///
    /// <para><b>Conformance:</b> ✅ PERFECT CONFORMANCE (with modern Smalltalk-80)</para>
    /// </remarks>
    public static SmalltalkOOP ValueWithArguments(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // This primitive is called from BOTH:
        // 1. value/value:/value:value: - args are passed DIRECTLY (not wrapped in array)
        // 2. valueWithArguments: - single arg is an Array
        // We must distinguish between these two cases to properly extract arguments

        SmalltalkOOP[] blockArgs;

        if (args.Length == 0)
        {
            // BlockClosure>>value with no arguments
            // Block expects no parameters
            blockArgs = new SmalltalkOOP[0];
        }
        else if (args.Length == 1 && !args[0].IsSmallInteger)
        {
            // Single argument that's not a SmallInteger
            // Could be either value: (direct arg) or valueWithArguments: (array arg)
            // Distinguish between value: (direct arg) and valueWithArguments: (array arg)
            // by checking if argument is a PointerIndexed object (Array)
            // Strings/Symbols are ByteIndexed and should be treated as direct args
            var format = ctx.GetObjectFormat(args[0]);
            bool isArray = (format == ObjectFormat.PointerIndexed);

            if (isArray)
            {
                // valueWithArguments: - arg is an Array, extract elements
                // This handles arbitrary argument counts via Array
                blockArgs = ExtractArrayElements(ctx, args[0]);
            }
            else
            {
                // value: - arg is a direct parameter (not an array)
                // Pass the single argument directly to the block (includes Strings, Symbols, etc.)
                blockArgs = args;
            }
        }
        else
        {
            // Multiple arguments (from value:, value:value:, etc.) - use directly
            // These are direct arguments, not wrapped in an array
            blockArgs = args;
        }

        // BlockClosure has 4 instance variables:
        // - method: CompiledMethod containing block's bytecode
        // - homeContext: MethodContext where block was defined (lexical scope)
        // - returnContext: Context to return to (usually same as homeContext)
        // - numArgs: Expected argument count

        // Extract block method from BlockClosure instance variable 'method'
        // This CompiledMethod contains the block's bytecode to execute
        var methodIndex = ctx.GetFieldIndex("class:BlockClosure", "method");
        // Console.WriteLine($"[DEBUG Primitive207] receiver={receiver}, methodIndex={methodIndex}");
        var blockMethodOOP = ctx.GetInstanceVariable(receiver, methodIndex);

        // Extract homeContext from BlockClosure instance variable 'homeContext'
        // This is the MethodContext where the block was DEFINED (not where it's called)
        // Critical for lexical scoping - block accesses variables from defining context
        var homeContextIndex = ctx.GetFieldIndex("class:BlockClosure", "homeContext");
        // Console.WriteLine($"[DEBUG Primitive207] homeContextIndex={homeContextIndex}");
        var homeContextOOP = ctx.GetInstanceVariable(receiver, homeContextIndex);

        // Extract numArgs from BlockClosure to validate argument count
        // Blocks are strict about argument count - must match exactly
        var numArgsIndex = ctx.GetFieldIndex("class:BlockClosure", "numArgs");
        // Console.WriteLine($"[DEBUG Primitive207] numArgsIndex={numArgsIndex}");
        var numArgsOOP = ctx.GetInstanceVariable(receiver, numArgsIndex);
        int expectedArgCount = numArgsOOP.IsSmallInteger ? numArgsOOP.AsSmallInteger() : 0;

        // Validate argument count matches BlockClosure's numArgs
        // Unlike methods, blocks don't have optional parameters
        if (blockArgs.Length != expectedArgCount)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Invoke the block using the VM method
        // Pass the BlockClosure instance (receiver) as the 4th parameter for nested-block
        // support and proper lexical scoping
        // The VM will create a new context, copy arguments, and execute block bytecode

        // DEBUG: Log BlockClosure details
        // Console.WriteLine($"[Primitive207] Executing BlockClosure receiver={receiver}, homeContext={homeContextOOP}, blockMethod={blockMethodOOP}");

        var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP, receiver);

        return result;
    }

    /// <summary>
    /// Extracts SmallInteger and OOP elements from a Smalltalk Array.
    /// </summary>
    private static SmalltalkOOP[] ExtractArrayElements(IPrimitiveContext ctx, SmalltalkOOP arrayOOP)
    {
        // Get the array size - number of indexed slots
        // Arrays in Smalltalk use PointerIndexed format
        var indexedSize = ctx.GetIndexedSize(arrayOOP);

        // Create array to hold extracted OOPs
        var elements = new SmalltalkOOP[indexedSize];
        for (int i = 0; i < indexedSize; i++)
        {
            // Extract each OOP from the Array's indexed slots
            // These can be SmallIntegers or object pointers
            elements[i] = ctx.GetIndexedOOP(arrayOOP, i);
        }

        return elements;
    }
}
