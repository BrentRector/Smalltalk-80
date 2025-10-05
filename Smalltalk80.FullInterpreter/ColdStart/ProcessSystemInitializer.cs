namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Initializes the minimal process system infrastructure needed for method execution.
/// Creates ProcessorScheduler singleton and initial Process object.
/// </summary>
public static class ProcessSystemInitializer
{
    private static SObjectRef processorScheduler = SObjectRef.Nil;

    /// <summary>
    /// Gets the ProcessorScheduler singleton created during initialization.
    /// </summary>
    public static SObjectRef GetProcessorScheduler() => processorScheduler;

    /// <summary>
    /// Initializes the process system with ProcessorScheduler and an initial activeProcess.
    /// This is the minimal setup required to execute methods in contexts.
    /// </summary>
    public static void Initialize()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Initializing process system...");
        }

        // 1. Create ProcessorScheduler singleton
        processorScheduler = CreateProcessorScheduler();

        // 2. Create initial Process with priority 4 (UserSchedulingPriority)
        var initialProcess = CreateInitialProcess();

        // 3. Wire activeProcess to ProcessorScheduler
        SetActiveProcess(processorScheduler, initialProcess);

        // 4. Register ProcessorScheduler as global "Processor"
        SystemDictionary.RegisterGlobalRef("Processor", processorScheduler);

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Process system initialized:");
            Console.WriteLine($"[COLD START]   - ProcessorScheduler: {processorScheduler}");
            Console.WriteLine($"[COLD START]   - Initial Process: {initialProcess}");
            Console.WriteLine("[COLD START]   - Registered as global 'Processor'");
        }
    }

    /// <summary>
    /// Creates the ProcessorScheduler singleton instance.
    /// Instance variables: quiescentProcessLists (Array of 8 LinkedLists), activeProcess
    /// </summary>
    private static SObjectRef CreateProcessorScheduler()
    {
        var schedulerClassRef = SystemDictionary.LookupClassRef("ProcessorScheduler");
        if (!schedulerClassRef.HasValue || schedulerClassRef.Value.IsNil)
        {
            throw new InvalidOperationException("ProcessorScheduler class not found");
        }

        // Create ProcessorScheduler instance (2 instance variables)
        var schedulerRef = ObjectMemory.CreateInstance(
            schedulerClassRef.Value.AsObjectPointer(),
            0); // No indexed slots, just instance variables

        // Create quiescentProcessLists - Array of 8 LinkedLists (one per priority)
        var arrayClassRef = SystemDictionary.LookupClassRef("Array");
        if (!arrayClassRef.HasValue || arrayClassRef.Value.IsNil)
        {
            throw new InvalidOperationException("Array class not found");
        }
        var priorityListsRef = ObjectMemory.CreateInstance(
            arrayClassRef.Value.AsObjectPointer(),
            8); // 8 priority levels as indexed slots

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            var arrayData = ObjectMemory.GetObjectData(priorityListsRef.AsObjectPointer());
            Console.WriteLine($"[DEBUG] Created Array with format: {arrayData.Format}");
        }

        // Initialize each priority list as an empty LinkedList
        var linkedListClassRef = SystemDictionary.LookupClassRef("LinkedList");
        if (!linkedListClassRef.HasValue || linkedListClassRef.Value.IsNil)
        {
            throw new InvalidOperationException("LinkedList class not found");
        }
        for (int i = 0; i < 8; i++)
        {
            var listRef = ObjectMemory.CreateInstance(
                linkedListClassRef.Value.AsObjectPointer(),
                0); // No indexed slots, just instance variables

            // Set firstLink and lastLink to nil
            ObjectMemory.SetInstanceVariable(listRef.AsObjectPointer(), 0, SObjectRef.Nil);
            ObjectMemory.SetInstanceVariable(listRef.AsObjectPointer(), 1, SObjectRef.Nil);

            // Add to array
            ObjectMemory.SetIndexedPointer(priorityListsRef.AsObjectPointer(), i, listRef);
        }

        // Set quiescentProcessLists in ProcessorScheduler
        ObjectMemory.SetInstanceVariable(schedulerRef.AsObjectPointer(), 0, priorityListsRef);

        // activeProcess will be set later (index 1)

        return schedulerRef;
    }

    /// <summary>
    /// Creates the initial Process object with priority 4 (UserSchedulingPriority).
    /// Instance variables: nextLink, suspendedContext, priority, myList
    /// </summary>
    private static SObjectRef CreateInitialProcess()
    {
        var processClassRef = SystemDictionary.LookupClassRef("Process");
        if (!processClassRef.HasValue || processClassRef.Value.IsNil)
        {
            throw new InvalidOperationException("Process class not found");
        }

        // Create Process instance (4 instance variables)
        var processRef = ObjectMemory.CreateInstance(
            processClassRef.Value.AsObjectPointer(),
            0); // No indexed slots, just instance variables

        // Set nextLink to nil (not in a list)
        ObjectMemory.SetInstanceVariable(processRef.AsObjectPointer(), 0, SObjectRef.Nil);

        // Create a dummy MethodContext for suspendedContext
        var contextRef = CreateDummyMethodContext();
        ObjectMemory.SetInstanceVariable(processRef.AsObjectPointer(), 1, contextRef);

        // Set priority to 4 (UserSchedulingPriority)
        ObjectMemory.SetInstanceVariable(processRef.AsObjectPointer(), 2, SObjectRef.FromSmallInteger(4));

        // Set myList to nil (not on any waiting list)
        ObjectMemory.SetInstanceVariable(processRef.AsObjectPointer(), 3, SObjectRef.Nil);

        return processRef;
    }

    /// <summary>
    /// Creates a minimal dummy MethodContext for the initial process.
    /// This context serves as a placeholder - actual execution contexts will be created dynamically.
    /// </summary>
    private static SObjectRef CreateDummyMethodContext()
    {
        var contextClassRef = SystemDictionary.LookupClassRef("MethodContext");
        if (!contextClassRef.HasValue || contextClassRef.Value.IsNil)
        {
            throw new InvalidOperationException("MethodContext class not found");
        }

        // Create MethodContext with minimal size (6 fixed fields + some stack space)
        var contextRef = ObjectMemory.CreateInstance(
            contextClassRef.Value.AsObjectPointer(),
            10); // 10 indexed slots for stack space

        // Initialize fixed fields
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 0, SObjectRef.Nil); // sender
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 1, SObjectRef.FromSmallInteger(0)); // instructionPointer
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 2, SObjectRef.FromSmallInteger(6)); // stackPointer (after fixed fields)
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 3, SObjectRef.Nil); // method
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 4, SObjectRef.Nil); // closure/receiverMap
        ObjectMemory.SetInstanceVariable(contextRef.AsObjectPointer(), 5, SObjectRef.Nil); // receiver

        return contextRef;
    }

    /// <summary>
    /// Sets the activeProcess in the ProcessorScheduler.
    /// </summary>
    private static void SetActiveProcess(SObjectRef schedulerRef, SObjectRef processRef)
    {
        // Set ProcessorScheduler.activeProcess (instance variable index 1)
        ObjectMemory.SetInstanceVariable(schedulerRef.AsObjectPointer(), 1, processRef);
    }
}
