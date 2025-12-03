using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages the execution stack for the Smalltalk-80 VM.
/// Handles Push, Pop, and Peek operations on the current context's operand stack.
/// </summary>
/// <param name="heapManager">The heap manager for memory operations.</param>
/// <param name="metadata">VM metadata containing class OOPs.</param>
/// <param name="helpers">VM helper methods.</param>
/// <param name="getActiveContext">Function to get the currently active context.</param>
/// <param name="getSelectorTraceEnabled">Function to check if selector tracing is enabled.</param>
/// <param name="getPushTraceCounter">Function to get the current push trace counter.</param>
/// <param name="incrementPushTraceCounter">Action to increment the push trace counter.</param>
internal class StackManager(
    ISmalltalkHeap heapManager,
    VMMetadata metadata,
    IObjectMetadataAccessor helpers,
    Func<SmalltalkOOP> getActiveContext,
    Func<bool> getSelectorTraceEnabled,
    Func<int> getPushTraceCounter,
    Action incrementPushTraceCounter) : IStackManager
{
    // Suppress unused parameter warning - getPushTraceCounter available for future diagnostics
    private readonly Func<int> _getPushTraceCounter = getPushTraceCounter;
    /// <inheritdoc />
    public void Push(SmalltalkOOP valueOOP)
    {
        var activeContextOOP = getActiveContext();

        // Track push operations when tracing is enabled
        if (getSelectorTraceEnabled())
        {
            incrementPushTraceCounter();
        }

        // DIAGNOSTIC: Validate activeContextOOP is a valid context (MethodContext or BlockContext)
        var activeContextFormat = heapManager.GetObjectFormat(activeContextOOP);
        var activeContextClass = helpers.GetClassName(helpers.GetClass(activeContextOOP));

        // Both MethodContext (PointerIndexed) and BlockContext (should be PointerIndexed but may be Pointer due to allocation bug) are valid
        bool isValidContext = activeContextFormat == ObjectFormat.PointerIndexed ||
                             (activeContextFormat == ObjectFormat.Pointer && (activeContextClass == "MethodContext" || activeContextClass == "BlockContext"));

        if (!isValidContext)
        {
            var valueClass = valueOOP.IsSmallInteger ? "SmallInteger" : helpers.GetClassName(helpers.GetClass(valueOOP));
            Console.WriteLine($"[ERROR Push] activeContextOOP is not a valid context!");
            Console.WriteLine($"  activeContextOOP: {activeContextOOP}");
            Console.WriteLine($"  Expected: MethodContext or BlockContext");
            Console.WriteLine($"  Actual format: {activeContextFormat}");
            Console.WriteLine($"  Actual class: {activeContextClass}");
            Console.WriteLine($"  Trying to push value: {valueOOP}");
            Console.WriteLine($"  Value class: {valueClass}");
            throw new InvalidOperationException($"Push() called with invalid context: {activeContextOOP}, format={activeContextFormat}, class={activeContextClass}");
        }

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Check for stack overflow
        int stackSize = heapManager.GetIndexedSize(activeContextOOP);
        if (sp >= stackSize)
        {
            throw new InvalidOperationException(
                $"Stack overflow: sp={sp}, allocated size={stackSize}, context={activeContextOOP}");
        }

        heapManager.SetIndexedOOP(activeContextOOP, sp, valueOOP);
        heapManager.SetInstanceVariable(activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp + 1));
    }

    /// <inheritdoc />
    public SmalltalkOOP Pop()
    {
        var activeContextOOP = getActiveContext();

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Get method to determine where execution stack starts
        // sp is absolute index: [0..numArgs-1][numArgs..numArgs+numTemps-1][numArgs+numTemps..stackSize-1]
        //                        ^--arguments--^------temporaries--------^-------execution stack----------^
        // Execution stack empty when sp == (numArgs + numTemps)
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        // Check if this is a BlockContext - they retrieve method from home MethodContext
        var contextClassOOP = heapManager.GetClassOOP(activeContextOOP);
        bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;

        int initialSP;
        SmalltalkOOP methodOOP;

        if (isBlockContext)
        {
            // BlockContext: Blue Book model - arguments are pushed onto stack by value: primitive
            // The block's first bytecodes pop them into temp slots via STORE_TEMP
            // Therefore the execution stack starts at index 0 (no reserved slots)
            initialSP = 0;

            // Get method from home context (needed for diagnostic messages)
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            var methodContextOOP = helpers.FindMethodContext(activeContextOOP);
            methodOOP = heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
        }
        else
        {
            // MethodContext: execution stack starts after args + temps
            // Method's indexed area: [0..numArgs-1] = args, [numArgs..numArgs+numTemps-1] = temps, [numArgs+numTemps..] = execution stack
            methodOOP = heapManager.GetInstanceVariable(activeContextOOP, methodIndex);
            var header = helpers.ParseMethodHeader(methodOOP);
            initialSP = header.NumArgs + header.NumTemps;
        }

        if (sp <= initialSP)
        {
            // Diagnostic: show what context we're in
            var contextClass = helpers.GetClassName(helpers.GetClass(activeContextOOP));
            var methodClass = methodOOP.IsSmallInteger ? "SmallInteger" : helpers.GetClassName(helpers.GetClass(methodOOP));
            Console.WriteLine($"[Pop ERROR] Stack underflow diagnostics:");
            Console.WriteLine($"  activeContext: {activeContextOOP}, class: {contextClass}");
            Console.WriteLine($"  methodOOP: {methodOOP}, class: {methodClass}");
            Console.WriteLine($"  sp={sp}, initialSP={initialSP}");

            // Get program counter for debugging
            var pcOOP = heapManager.GetInstanceVariable(activeContextOOP, VMConstants.CONTEXT_PC_INDEX);
            Console.WriteLine($"  PC={pcOOP.AsSmallInteger()}");

            if (isBlockContext)
            {
                var nargsOOP = heapManager.GetInstanceVariable(activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
                Console.WriteLine($"  BlockContext: nargs={nargsOOP.AsSmallInteger()}");
            }
            else
            {
                var header = helpers.ParseMethodHeader(methodOOP);
                Console.WriteLine($"  MethodContext: numArgs={header.NumArgs}, numTemps={header.NumTemps}, numLiterals={header.NumLiterals}");

                // Print first few literals to help identify the method
                // Literals start at indexed slot 1 (slot 0 is header)
                Console.WriteLine($"  Literals (first 5):");
                for (int i = 0; i < Math.Min(header.NumLiterals, 5); i++)
                {
                    var litOOP = heapManager.GetIndexedOOP(methodOOP, i + 1);
                    var litClass = litOOP.IsSmallInteger ? "SmallInteger" : helpers.GetClassName(helpers.GetClass(litOOP));
                    Console.WriteLine($"    [{i}] {litClass}: {litOOP}");
                }
            }

            throw new InvalidOperationException(
                $"Stack underflow: sp={sp}, initialSP={initialSP} in {contextClass}");
        }

        sp--;
        heapManager.SetInstanceVariable(activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp));
        var value = heapManager.GetIndexedOOP(activeContextOOP, sp);

        return value;
    }

    /// <inheritdoc />
    public SmalltalkOOP Peek()
    {
        var activeContextOOP = getActiveContext();

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        if (sp <= 0)
        {
            throw new InvalidOperationException("Stack underflow in Peek()");
        }

        var value = heapManager.GetIndexedOOP(activeContextOOP, sp - 1);
        return value;
    }
}
