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
        // Priority 8 = TimingPriority (highest standard Smalltalk priority)
        // This prevents preemption during init expressions:
        // - Delay's timing process at priority 8 won't preempt (8 > 8 is false)
        // - InputSensor's process at priority 6 won't preempt (6 > 8 is false)
        // After init expressions complete, the controller loop will create normal processes.
        SmalltalkOOP initialProcessOOP = CreateInitialProcess(priority: 8);

        // Set as active process
        _heapManager.SetInstanceVariable(schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
            initialProcessOOP);

        Console.WriteLine($"[ProcessScheduler] Initialized for runtime:");
        Console.WriteLine($"  Active process: {initialProcessOOP}");
        Console.WriteLine($"  Priority: 8 (TimingPriority - highest standard priority)");

        return initialProcessOOP;
    }

    /// <summary>
    /// Creates an initial Process with suspendedContext set to nil.
    /// When init expressions or other code runs, they will create their own contexts
    /// and set them as the suspended context of this process.
    /// </summary>
    private SmalltalkOOP CreateInitialProcess(int priority)
    {
        // Get Process class from VMMetadata (not from GlobalRegistry which is disposed)
        var processClassOOP = _metadata.ProcessClassOOP;
        if (processClassOOP == _heapManager.GetNil())
        {
            throw new InvalidOperationException("Process class not found in VMMetadata");
        }

        // Create Process object (4 instance variables: nextLink, suspendedContext, priority, myList)
        const int PROCESS_INSTANCE_VAR_COUNT = 4;
        var processOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, PROCESS_INSTANCE_VAR_COUNT, indexedSize: 0);
        _heapManager.SetClassOOP(processOOP, processClassOOP);

        // Set Process instance variables
        // NOTE: suspendedContext starts as nil - will be set when code executes
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_NEXTLINK_INDEX, _heapManager.GetNil());
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, _heapManager.GetNil());
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX, SmalltalkOOP.FromSmallInteger(priority));
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, _heapManager.GetNil());

        return processOOP;
    }
}
