using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Additional process management support beyond primitives 85-88.
/// Provides Process resumption for testing and initialization.
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b></para>
/// <para>This class provides supplementary process management support used primarily during
/// cold start initialization and testing. It complements the standard process primitives
/// (85-88) found in ControlPrimitives.cs.</para>
///
/// <para><b>Design Note:</b></para>
/// <para>This is not a standard primitive class but rather a helper for process management
/// during system initialization. The standard process primitives are:</para>
/// <list type="bullet">
/// <item>Primitive 85: signal (Semaphore) - in ControlPrimitives.cs</item>
/// <item>Primitive 86: wait (Semaphore) - in ControlPrimitives.cs</item>
/// <item>Primitive 87: resume (Process) - in ControlPrimitives.cs</item>
/// <item>Primitive 88: suspend (Process) - in ControlPrimitives.cs</item>
/// </list>
///
/// <para><b>Usage:</b></para>
/// <para>Used by the VM during cold start to execute initialization expressions and
/// test blocks by creating temporary Process objects and running them to completion.</para>
/// </remarks>
public class ProcessPrimitives
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VirtualMachine _vm;

    public ProcessPrimitives(
        ISmalltalkHeapManager heapManager,
        VirtualMachine vm)
    {
        _heapManager = heapManager;
        _vm = vm;
    }
    
    // <summary>
    // Resumes a Process by making it active and executing its suspended context.
    // Used during cold start to execute initialization expressions.
    // </summary>
    // <remarks>
    // <para><b>Operation:</b></para>
    // <list type="bullet">
    // <item>Sets the Process as activeProcess in ProcessorScheduler</item>
    // <item>Executes the Process's suspended context until completion</item>
    // <item>Clears activeProcess when done (even on failure)</item>
    // </list>
    //
    // <para><b>Limitations:</b></para>
    // <para>This is a simplified implementation for cold start initialization.
    // It does not support concurrent processes or process switching. If an active
    // process already exists, it throws an exception.</para>
    //
    // <para><b>Heap-Only Access:</b></para>
    // <para>All operations use heap-only access pattern, retrieving ProcessorScheduler
    // from SpecialObjectsArray and accessing Process fields via RuntimeHeapAccessor.</para>
    // </remarks>
    // <param name="processOOP">The Process object to resume</param>
    // <returns>The result of executing the process's suspended context</returns>
    // <exception cref="InvalidOperationException">If ProcessorScheduler not found or process switching needed</exception>
    public SmalltalkOOP Resume(SmalltalkOOP processOOP)
    {
        // Get ProcessorScheduler from SpecialObjectsArray (heap-only access)
        var schedulerOOP = SpecialObjectsArray.Instance.ProcessorScheduler;

        // SPECIAL CASE: During init expression execution, ProcessorScheduler doesn't exist yet!
        // It will be created by one of the init expressions (ProcessorScheduler class initialize).
        // In this case, bypass the scheduler and just execute the process directly.
        bool hasScheduler = schedulerOOP != SmalltalkOOP.Null &&
                           schedulerOOP.RawValue != 0 &&
                           schedulerOOP != SpecialObjectsArray.Instance.Nil;

        if (!hasScheduler)
        {
            // Direct execution without scheduler (init expression bootstrap mode)
            // Process has fixed field layout: suspendedContext at slot 0
            var suspendedContextOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

            return _vm.ExecuteExistingContext(suspendedContextOOP);
        }

        // Normal execution path: Use ProcessorScheduler
        // Additional safety check: Verify scheduler has a valid class
        var schedulerClassOOP = _heapManager.GetSlot(schedulerOOP, 0);
        if (schedulerClassOOP.RawValue == 0 || schedulerClassOOP == SmalltalkOOP.Null)
        {
            // Scheduler exists but class slot is invalid - fall back to direct execution
            // Process has fixed field layout: suspendedContext at slot 0
            var suspendedContextOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

            return _vm.ExecuteExistingContext(suspendedContextOOP);
        }

        // ProcessorScheduler has fixed field layout: activeProcess at slot 1
        var currentActiveOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

        if (currentActiveOOP != SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Process switching not yet implemented");
        }

        _heapManager.SetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
            processOOP
        );

        try
        {
            // Process has fixed field layout: suspendedContext at slot 0
            var suspendedContextOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

            var resultOOP = _vm.ExecuteExistingContext(suspendedContextOOP);

            return resultOOP;
        }
        finally
        {
            // Always clear activeProcess, even if execution fails
            // This ensures subsequent test expressions can run
            _heapManager.SetInstanceVariable(
                schedulerOOP,
                VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
                SpecialObjectsArray.Instance.Nil
            );
        }
    }
}
