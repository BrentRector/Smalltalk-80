using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Initializes the ProcessorScheduler for runtime execution.
/// Creates an initial Process and sets it as the active process.
/// This ensures all code execution (including init expressions) runs within a Process context,
/// which is required for canonical Smalltalk-80 semantics and proper GC reachability.
/// </summary>
public class ProcessSchedulerInitializer
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly VMMetadata _metadata;

    public ProcessSchedulerInitializer(ISmalltalkHeap heapManager, VMMetadata metadata)
    {
        _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <summary>
    /// Initializes the ProcessorScheduler for runtime execution.
    /// Creates an initial Process with a dummy suspended context and sets it as active.
    /// This process will be used to run init expressions and other runtime code.
    /// </summary>
    /// <returns>The OOP of the created initial Process.</returns>
    public SmalltalkOOP InitializeForRuntime()
    {
        // Get ProcessorScheduler singleton (Processor global)
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _heapManager.GetNil())
        {
            throw new InvalidOperationException("ProcessorScheduler not found in VMMetadata");
        }

        // Create initial Process for runtime execution
        // Priority 4 = UserSchedulingPriority (standard user code priority)
        SmalltalkOOP initialProcessOOP = CreateInitialProcess(priority: 4);

        // Set as active process
        _heapManager.SetInstanceVariable(schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
            initialProcessOOP);

        Console.WriteLine($"[ProcessScheduler] Initialized for runtime:");
        Console.WriteLine($"  Active process: {initialProcessOOP}");
        Console.WriteLine($"  Priority: 4 (UserSchedulingPriority)");

        return initialProcessOOP;
    }

    /// <summary>
    /// Creates an initial Process with a dummy suspended context.
    /// The context is a minimal MethodContext that serves as a placeholder.
    /// When init expressions or other code runs, they will create their own contexts
    /// that become the actual suspended context of this process.
    /// </summary>
    private SmalltalkOOP CreateInitialProcess(int priority)
    {
        // Get Process class
        var processClassOOP = LookupGlobal("Process");
        if (processClassOOP == _heapManager.GetNil())
        {
            throw new InvalidOperationException("Process class not found in Smalltalk dictionary");
        }

        // Create Process object (4 instance variables: nextLink, suspendedContext, priority, myList)
        const int PROCESS_INSTANCE_VAR_COUNT = 4;
        var processOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, PROCESS_INSTANCE_VAR_COUNT, indexedSize: 0);
        _heapManager.SetClassOOP(processOOP, processClassOOP);

        // Create a dummy suspended context (minimal MethodContext)
        // This context is just a placeholder - when code runs, it will be replaced
        SmalltalkOOP dummyContextOOP = CreateDummyContext();
        Console.WriteLine($"  [DEBUG] Created dummy MethodContext: {dummyContextOOP} (will be orphaned when replaced)");

        // Set Process instance variables
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_NEXTLINK_INDEX, _heapManager.GetNil());
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, dummyContextOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX, SmalltalkOOP.FromSmallInteger(priority));
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, _heapManager.GetNil());

        return processOOP;
    }

    /// <summary>
    /// Creates a minimal dummy MethodContext to serve as the initial suspended context.
    /// This context is never actually executed - it's replaced when real code runs.
    /// </summary>
    private SmalltalkOOP CreateDummyContext()
    {
        // Get MethodContext class
        var methodContextClassOOP = LookupGlobal("MethodContext");
        if (methodContextClassOOP == _heapManager.GetNil())
        {
            throw new InvalidOperationException("MethodContext class not found in Smalltalk dictionary");
        }

        // MethodContext has 6 instance variables: sender, pc, stackp, method, lexicalParent, receiver
        const int METHODCONTEXT_INSTANCE_VAR_COUNT = 6;

        // Create MethodContext with minimal stack space
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, indexedSize: 4);
        _heapManager.SetClassOOP(contextOOP, methodContextClassOOP);

        // Initialize all fields to nil (this is a placeholder context)
        var nilOOP = _heapManager.GetNil();
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX, nilOOP);
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_PC_INDEX, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_METHOD_INDEX, nilOOP);
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_LEXICALPARENT_INDEX, nilOOP);
        _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_RECEIVER_INDEX, nilOOP);

        // Initialize indexed slots (stack) with nil
        for (int i = 0; i < 4; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, nilOOP);
        }

        return contextOOP;
    }

    /// <summary>
    /// Looks up a global variable in the SystemDictionary.
    /// </summary>
    private SmalltalkOOP LookupGlobal(string globalName)
    {
        var smalltalkOOP = GlobalRegistry.Get("Smalltalk");
        int size = _heapManager.GetIndexedSize(smalltalkOOP);

        // Find the symbol OOP for the global name
        var symbolOOP = FindSymbol(globalName);
        if (symbolOOP == _heapManager.GetNil())
        {
            return _heapManager.GetNil();
        }

        // Search SystemDictionary for Association with this key
        for (int i = 0; i < size; i++)
        {
            var assocOOP = _heapManager.GetIndexedOOP(smalltalkOOP, i);
            if (assocOOP == _heapManager.GetNil())
                continue;

            var keyOOP = _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == symbolOOP)
            {
                return _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            }
        }

        return _heapManager.GetNil();
    }

    /// <summary>
    /// Finds a symbol OOP by searching through all registered symbols.
    /// </summary>
    private SmalltalkOOP FindSymbol(string symbolText)
    {
        foreach (var (index, symbolOOP) in SymbolRegistry.AllSymbols())
        {
            var symbolString = ReadSymbolString(symbolOOP);
            if (symbolString == symbolText)
            {
                return symbolOOP;
            }
        }

        return _heapManager.GetNil();
    }

    /// <summary>
    /// Reads a Symbol object's string value from the heap.
    /// </summary>
    private string ReadSymbolString(SmalltalkOOP symbolOOP)
    {
        int size = _heapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
