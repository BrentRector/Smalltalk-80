using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

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
    ISmalltalkHeapManager heapManager,
    VMMetadata metadata,
    IObjectMetadataAccessor helpers,
    Func<SmalltalkOOP> getActiveContext,
    Func<bool> getSelectorTraceEnabled,
    Func<int> getPushTraceCounter,
    Action incrementPushTraceCounter) : IStackManager
{
    /// <inheritdoc />
    public void Push(SmalltalkOOP valueOOP)
    {
        var activeContextOOP = getActiveContext();

        // DEBUG: Trace ALL stack pushes when tracing is enabled
        if (getSelectorTraceEnabled())
        {
            incrementPushTraceCounter();
            if (getPushTraceCounter() <= 10)
            {
                // Format: "oop=#### (ClassName)" for objects, "SmallInteger(#)" for small integers
                string valueStr;
                if (valueOOP.IsSmallInteger)
                {
                    valueStr = $"SmallInteger({valueOOP.AsSmallInteger()})";
                }
                else
                {
                    try
                    {
                        string className = helpers.GetClassName(helpers.GetClass(valueOOP));
                        valueStr = $"{valueOOP} ({className})";

                        // If Symbol, show value
                        if (className == "Symbol")
                        {
                            try
                            {
                                int byteSize = heapManager.GetIndexedSize(valueOOP);
                                var bytes = new byte[byteSize];
                                for (int i = 0; i < byteSize; i++)
                                {
                                    bytes[i] = heapManager.GetIndexedByte(valueOOP, i);
                                }

                                string symbolValue = System.Text.Encoding.Latin1.GetString(bytes);
                                valueStr = $"{valueOOP} (Symbol) = #{symbolValue}";
                            }
                            catch
                            {
                                // Ignore errors when reading symbol value
                            }
                        }
                    }
                    catch
                    {
                        // If GetClassName fails, just show the OOP
                        valueStr = valueOOP.ToString();
                    }
                }

                Console.WriteLine($"[PUSH #{getPushTraceCounter()}] Pushing {valueStr} onto stack");
            }
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
            // BlockContext: execution stack starts after block arguments
            // Block's indexed area: [0..nargs-1] = args, [nargs..] = execution stack
            var nargsOOP = heapManager.GetInstanceVariable(activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            initialSP = nargsOOP.AsSmallInteger();

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

            if (isBlockContext)
            {
                var nargsOOP = heapManager.GetInstanceVariable(activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
                Console.WriteLine($"  BlockContext: nargs={nargsOOP.AsSmallInteger()}");
            }
            else
            {
                var header = helpers.ParseMethodHeader(methodOOP);
                Console.WriteLine($"  MethodContext: numArgs={header.NumArgs}, numTemps={header.NumTemps}, numLiterals={header.NumLiterals}");
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
