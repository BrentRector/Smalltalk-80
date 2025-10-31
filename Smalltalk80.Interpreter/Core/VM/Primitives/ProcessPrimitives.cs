using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

public class ProcessPrimitives
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly VirtualMachine _vm;
    
    public ProcessPrimitives(
        ISmalltalkHeapManager heapManager, 
        LayoutResolver layoutResolver,
        VirtualMachine vm)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _vm = vm;
    }
    
    public SmalltalkOOP Resume(SmalltalkOOP processOOP)
    {
        // Get ProcessorScheduler from SpecialObjectsArray (heap-only access)
        var schedulerOOP = SpecialObjectsArray.Instance.ProcessorScheduler;
        if (schedulerOOP == SpecialObjectsArray.Instance.Nil)
            throw new InvalidOperationException("ProcessorScheduler not found in SpecialObjectsArray");

        int activeProcessIndex = _layoutResolver.GetFieldIndex("class:ProcessorScheduler", "activeProcess");
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
            int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
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
