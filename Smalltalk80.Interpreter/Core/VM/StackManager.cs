using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages the execution stack for the Smalltalk-80 VM.
/// Handles Push, Pop, and Peek operations on the current context's operand stack.
/// </summary>
internal class StackManager : IStackManager
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VMMetadata _metadata;
    private readonly IVMHelpers _helpers;
    private readonly Func<SmalltalkOOP> _getActiveContext;
    private readonly Func<bool> _getSelectorTraceEnabled;
    private readonly Func<int> _getPushTraceCounter;
    private readonly Action _incrementPushTraceCounter;

    public StackManager(
        ISmalltalkHeapManager heapManager,
        VMMetadata metadata,
        IVMHelpers helpers,
        Func<SmalltalkOOP> getActiveContext,
        Func<bool> getSelectorTraceEnabled,
        Func<int> getPushTraceCounter,
        Action incrementPushTraceCounter)
    {
        _heapManager = heapManager;
        _metadata = metadata;
        _helpers = helpers;
        _getActiveContext = getActiveContext;
        _getSelectorTraceEnabled = getSelectorTraceEnabled;
        _getPushTraceCounter = getPushTraceCounter;
        _incrementPushTraceCounter = incrementPushTraceCounter;
    }

    public void Push(SmalltalkOOP valueOOP)
    {
        var activeContextOOP = _getActiveContext();

        // DEBUG: Trace ALL stack pushes when tracing is enabled
        if (_getSelectorTraceEnabled())
        {
            _incrementPushTraceCounter();
            if (_getPushTraceCounter() <= 10)
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
                        string className = _helpers.GetClassName(_helpers.GetClass(valueOOP));
                        valueStr = $"{valueOOP} ({className})";

                        // If Symbol, show value
                        if (className == "Symbol")
                        {
                            try
                            {
                                int byteSize = _heapManager.GetIndexedSize(valueOOP);
                                var bytes = new byte[byteSize];
                                for (int i = 0; i < byteSize; i++)
                                {
                                    bytes[i] = _heapManager.GetIndexedByte(valueOOP, i);
                                }
                                string symbolValue = System.Text.Encoding.Latin1.GetString(bytes);
                                valueStr = $"{valueOOP} (Symbol) = #{symbolValue}";
                            }
                            catch { }
                        }
                    }
                    catch
                    {
                        // If GetClassName fails, just show the OOP
                        valueStr = valueOOP.ToString();
                    }
                }
                Console.WriteLine($"[PUSH #{_getPushTraceCounter()}] Pushing {valueStr} onto stack");
            }
        }

        // DIAGNOSTIC: Validate activeContextOOP is a valid context (MethodContext or BlockContext)
        var activeContextFormat = _heapManager.GetObjectFormat(activeContextOOP);
        var activeContextClass = _helpers.GetClassName(_helpers.GetClass(activeContextOOP));

        // Both MethodContext (PointerIndexed) and BlockContext (should be PointerIndexed but may be Pointer due to allocation bug) are valid
        bool isValidContext = (activeContextFormat == ObjectFormat.PointerIndexed) ||
                             (activeContextFormat == ObjectFormat.Pointer && (activeContextClass == "MethodContext" || activeContextClass == "BlockContext"));

        if (!isValidContext)
        {
            var valueClass = valueOOP.IsSmallInteger ? "SmallInteger" : _helpers.GetClassName(_helpers.GetClass(valueOOP));
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
        var spOOP = _heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Check for stack overflow
        int stackSize = _heapManager.GetIndexedSize(activeContextOOP);
        if (sp >= stackSize)
        {
            throw new InvalidOperationException(
                $"Stack overflow: sp={sp}, allocated size={stackSize}, context={activeContextOOP}");
        }

        _heapManager.SetIndexedOOP(activeContextOOP, sp, valueOOP);
        _heapManager.SetInstanceVariable(activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp + 1));
    }

    public SmalltalkOOP Pop()
    {
        var activeContextOOP = _getActiveContext();

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Get method to determine where execution stack starts
        // sp is absolute index: [0..numArgs-1][numArgs..numArgs+numTemps-1][numArgs+numTemps..stackSize-1]
        //                        ^--arguments--^------temporaries--------^-------execution stack----------^
        // Execution stack empty when sp == (numArgs + numTemps)
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        // Check if this is a BlockContext - they retrieve method from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(activeContextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        int initialSP;
        SmalltalkOOP methodOOP;

        if (isBlockContext)
        {
            // BlockContext: execution stack starts after block arguments
            // Block's indexed area: [0..nargs-1] = args, [nargs..] = execution stack
            var nargsOOP = _heapManager.GetInstanceVariable(activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            initialSP = nargsOOP.AsSmallInteger();

            // Get method from home context (needed for diagnostic messages)
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            var methodContextOOP = _helpers.FindMethodContext(activeContextOOP);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
        }
        else
        {
            // MethodContext: execution stack starts after args + temps
            // Method's indexed area: [0..numArgs-1] = args, [numArgs..numArgs+numTemps-1] = temps, [numArgs+numTemps..] = execution stack
            methodOOP = _heapManager.GetInstanceVariable(activeContextOOP, methodIndex);
            var header = _helpers.ParseMethodHeader(methodOOP);
            initialSP = header.NumArgs + header.NumTemps;
        }

        if (sp <= initialSP)
        {
            // Diagnostic: show what context we're in
            var contextClass = _helpers.GetClassName(_helpers.GetClass(activeContextOOP));
            var methodClass = methodOOP.IsSmallInteger ? "SmallInteger" : _helpers.GetClassName(_helpers.GetClass(methodOOP));
            Console.WriteLine($"[Pop ERROR] Stack underflow diagnostics:");
            Console.WriteLine($"  activeContext: {activeContextOOP}, class: {contextClass}");
            Console.WriteLine($"  methodOOP: {methodOOP}, class: {methodClass}");
            Console.WriteLine($"  sp={sp}, initialSP={initialSP}");

            if (isBlockContext)
            {
                var nargsOOP = _heapManager.GetInstanceVariable(activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
                Console.WriteLine($"  BlockContext: nargs={nargsOOP.AsSmallInteger()}");
            }
            else
            {
                var header = _helpers.ParseMethodHeader(methodOOP);
                Console.WriteLine($"  MethodContext: numArgs={header.NumArgs}, numTemps={header.NumTemps}, numLiterals={header.NumLiterals}");
            }

            throw new InvalidOperationException(
                $"Stack underflow: sp={sp}, initialSP={initialSP} in {contextClass}");
        }

        sp--;
        _heapManager.SetInstanceVariable(activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp));
        var value = _heapManager.GetIndexedOOP(activeContextOOP, sp);

        return value;
    }

    public SmalltalkOOP Peek()
    {
        var activeContextOOP = _getActiveContext();

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        if (sp <= 0)
        {
            throw new InvalidOperationException("Stack underflow in Peek()");
        }

        var value = _heapManager.GetIndexedOOP(activeContextOOP, sp - 1);
        return value;
    }
}
