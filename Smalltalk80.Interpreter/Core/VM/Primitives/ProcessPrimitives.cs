using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Objects;

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
    private readonly RuntimeHeapAccess _runtimeHeapAccess;
    private readonly VirtualMachine _vm;

    public ProcessPrimitives(
        ISmalltalkHeapManager heapManager,
        RuntimeHeapAccess runtimeHeapAccess,
        VirtualMachine vm)
    {
        _heapManager = heapManager;
        _runtimeHeapAccess = runtimeHeapAccess;
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
    // from SpecialObjectsArray and accessing Process fields via RuntimeHeapAccess.</para>
    // </remarks>
    // <param name="processOOP">The Process object to resume</param>
    // <returns>The result of executing the process's suspended context</returns>
    // <exception cref="InvalidOperationException">If ProcessorScheduler not found or process switching needed</exception>
    public SmalltalkOOP Resume(SmalltalkOOP processOOP)
    {
        // Get ProcessorScheduler from SpecialObjectsArray (heap-only access)
        var schedulerOOP = SpecialObjectsArray.Instance.ProcessorScheduler;
        if (schedulerOOP == SpecialObjectsArray.Instance.Nil)
            throw new InvalidOperationException("ProcessorScheduler not found in SpecialObjectsArray");

        var schedulerClassOOP = _heapManager.GetSlot(schedulerOOP, 0);
        int activeProcessIndex = _runtimeHeapAccess.GetFieldIndex(schedulerClassOOP, "activeProcess");
        var currentActiveOOP = _heapManager.GetInstanceVariable(schedulerOOP, activeProcessIndex);

        if (currentActiveOOP != SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Process switching not yet implemented");
        }

        _heapManager.SetInstanceVariable(
            schedulerOOP,
            activeProcessIndex,
            processOOP
        );

        try
        {
            var processClassOOP = _heapManager.GetSlot(processOOP, 0);
            int suspendedContextIndex = _runtimeHeapAccess.GetFieldIndex(processClassOOP, "suspendedContext");
            var suspendedContextOOP = _heapManager.GetInstanceVariable(processOOP, suspendedContextIndex);

            var resultOOP = _vm.ExecuteExistingContext(suspendedContextOOP);

            return resultOOP;
        }
        finally
        {
            // Always clear activeProcess, even if execution fails
            // This ensures subsequent test expressions can run
            _heapManager.SetInstanceVariable(
                schedulerOOP,
                activeProcessIndex,
                SpecialObjectsArray.Instance.Nil
            );
        }
    }
}
