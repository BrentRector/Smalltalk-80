using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Scheduling;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing process scheduling support methods.
/// Implements IProcessScheduler to provide a unified API for process scheduling
/// used by both the VM interpreter loop and primitive implementations.
/// </summary>
public partial class VirtualMachine : IProcessScheduler
{
    /// <summary>
    /// Checks if a timer has expired and signals the associated semaphore.
    /// This is the core mechanism for the Delay class.
    /// Called periodically from the interpreter loop.
    /// </summary>
    private void CheckAndSignalExpiredTimer()
    {
        var semaphoreOOP = _timerManager.GetExpiredTimerSemaphore();
        if (semaphoreOOP == default || semaphoreOOP == _metadata.KnownObjects.Nil)
            return;

        // Signal the semaphore - this may wake a process and trigger preemption
        SignalSemaphoreWithPreemption(semaphoreOOP);
    }

    #region IProcessScheduler Implementation

    /// <inheritdoc/>
    public SmalltalkOOP GetActiveProcess()
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        return _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);
    }

    /// <inheritdoc/>
    public bool ActivateProcess(SmalltalkOOP processOOP, string reason)
    {
        var schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return false;

        // Phase 4: Get old process before switching (for context switch hook)
        var oldProcessOOP = GetActiveProcess();

        // Get the process's suspended context
        var contextOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
        if (contextOOP == _metadata.KnownObjects.Nil)
        {
            Console.WriteLine($"[ACTIVATE] ERROR: Process {processOOP} has nil suspendedContext");
            return false;
        }

        // Get process priority for logging
        var priorityOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX);
        int priority = priorityOOP.IsSmallInteger ? priorityOOP.AsSmallInteger() : -1;

        // Check if this is an unactivated BlockContext (PC is nil)
        // This happens when resuming a newly created process via [block] newProcess
        var pcOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_PC_INDEX);
        if (pcOOP == _metadata.KnownObjects.Nil)
        {
            // Activate the block context - set PC from startPC
            var startPC = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            if (startPC == _metadata.KnownObjects.Nil || !startPC.IsSmallInteger)
            {
                Console.WriteLine($"[ACTIVATE] ERROR: Process {processOOP} BlockContext has invalid startPC");
                return false;
            }

            _heapManager.SetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_PC_INDEX, startPC);
            _heapManager.SetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));
            _heapManager.SetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_CALLER_INDEX, _metadata.KnownObjects.Nil);
        }

        // Update ProcessorScheduler.activeProcess
        _heapManager.SetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX, processOOP);

        // Phase 4: Context switch hook - clear latch when InputProcess blocks
        // If switching away from InputProcess and it's blocked (waiting on semaphore), clear the latch
        if (_inputProcessOOP != default && _inputProcessOOP != _metadata.KnownObjects.Nil)
        {
            if (oldProcessOOP == _inputProcessOOP && processOOP != _inputProcessOOP)
            {
                // Switching away from InputProcess - check if it's blocked
                if (IsProcessBlocked(oldProcessOOP))
                {
                    // InputProcess is blocked - clear latch to allow next signal
                    _inputProcessRunnableOrDraining = false;
                }
            }
        }

        // Switch C# execution context
        _contextManager.SetActiveContext(contextOOP);

        return true;
    }

    /// <summary>
    /// Phase 4: Check if a process is blocked (waiting on a semaphore).
    /// A process is blocked if its myList field is not nil (it's on a semaphore's wait queue).
    /// </summary>
    private bool IsProcessBlocked(SmalltalkOOP processOOP)
    {
        var myListOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX);
        return myListOOP != _metadata.KnownObjects.Nil;
    }

    /// <inheritdoc/>
    public void SuspendActiveProcess(bool returnToQueue)
    {
        var activeProcessOOP = GetActiveProcess();
        if (activeProcessOOP == _metadata.KnownObjects.Nil)
            return;

        // Save current context to the process's suspendedContext
        var currentContext = _contextManager.ActiveContext;
        _heapManager.SetInstanceVariable(activeProcessOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, currentContext);

        if (returnToQueue)
        {
            var priorityOOP = _heapManager.GetInstanceVariable(activeProcessOOP, VMConstants.PROCESS_PRIORITY_INDEX);
            int priority = priorityOOP.IsSmallInteger ? priorityOOP.AsSmallInteger() : 0;
            AddProcessToReadyQueue(activeProcessOOP, priority);
        }
    }

    /// <inheritdoc/>
    public void AddProcessToReadyQueue(SmalltalkOOP processOOP, int priority)
    {
        var schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return;

        var processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
            return;

        // Get the LinkedList for this priority (index = priority - 1)
        int index = priority - 1;
        if (index < 0 || index >= _heapManager.GetIndexedSize(processListsOOP))
            return;

        var listOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
        if (listOOP == _metadata.KnownObjects.Nil)
            return;

        // Add process to end of list (LinkedList>>addLast:)
        var lastLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);
        if (lastLinkOOP == _metadata.KnownObjects.Nil)
        {
            // List is empty
            _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, processOOP);
        }
        else
        {
            // Append to end
            _heapManager.SetInstanceVariable(lastLinkOOP, VMConstants.LINK_NEXTLINK_INDEX, processOOP);
        }
        _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, processOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, listOOP);
    }

    /// <inheritdoc/>
    public void RemoveProcessFromList(SmalltalkOOP processOOP, SmalltalkOOP listOOP)
    {
        SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
        SmalltalkOOP lastLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);

        if (processOOP == firstLinkOOP)
        {
            // Process is first in list
            SmalltalkOOP nextLinkOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
            _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, nextLinkOOP);

            if (processOOP == lastLinkOOP)
            {
                // Was also the last (only process in list)
                _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, _metadata.KnownObjects.Nil);
            }
        }
        else
        {
            // Process is not first - find predecessor
            SmalltalkOOP prevLinkOOP = firstLinkOOP;
            while (prevLinkOOP != _metadata.KnownObjects.Nil)
            {
                SmalltalkOOP nextLinkOOP = _heapManager.GetInstanceVariable(prevLinkOOP, VMConstants.LINK_NEXTLINK_INDEX);
                if (nextLinkOOP == processOOP)
                {
                    // Found predecessor - relink
                    SmalltalkOOP afterRemoved = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
                    _heapManager.SetInstanceVariable(prevLinkOOP, VMConstants.LINK_NEXTLINK_INDEX, afterRemoved);

                    if (processOOP == lastLinkOOP)
                    {
                        _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, prevLinkOOP);
                    }
                    break;
                }
                prevLinkOOP = nextLinkOOP;
            }
        }

        // Clear the removed process's next link
        _heapManager.SetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);
    }

    /// <inheritdoc/>
    public SmalltalkOOP FindHighestPriorityRunnableProcess()
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        SmalltalkOOP processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        // Search from highest priority (8) down to lowest (1)
        // Array indices are 0-7 for priorities 1-8
        for (int priority = 8; priority >= 1; priority--)
        {
            int index = priority - 1;
            SmalltalkOOP priorityListOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
            if (priorityListOOP == _metadata.KnownObjects.Nil)
                continue;

            // Check if list has any processes (firstLink != nil)
            SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
            if (firstLinkOOP != _metadata.KnownObjects.Nil)
            {
                return firstLinkOOP;
            }
        }

        return _metadata.KnownObjects.Nil;
    }

    /// <inheritdoc/>
    public bool SwitchToNextRunnableProcess(SmalltalkOOP terminatingProcess)
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return false;

        // Find highest priority runnable process
        SmalltalkOOP nextProcess = FindHighestPriorityRunnableProcess();
        if (nextProcess == _metadata.KnownObjects.Nil)
        {
            // This is the error condition - dump queues for debugging
            Console.WriteLine($"[SwitchToNextRunnableProcess] No runnable process found!");
            DumpProcessQueues(schedulerOOP);
            return false;
        }

        // Remove the process from its priority queue (it's about to become active)
        SmalltalkOOP myListOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_MYLIST_INDEX);
        if (myListOOP != _metadata.KnownObjects.Nil)
        {
            RemoveProcessFromList(nextProcess, myListOOP);
            _heapManager.SetInstanceVariable(nextProcess, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
        }

        // Activate the process using shared helper
        return ActivateProcess(nextProcess, "termination");
    }

    /// <inheritdoc/>
    public bool ShouldPreempt(SmalltalkOOP processOOP)
    {
        SmalltalkOOP activeProcessOOP = GetActiveProcess();
        if (activeProcessOOP == _metadata.KnownObjects.Nil)
            return false;

        int processPriority = GetProcessPriority(processOOP);
        int activePriority = GetProcessPriority(activeProcessOOP);

        // Preempt if new process has STRICTLY higher priority
        return processPriority > activePriority;
    }

    /// <inheritdoc/>
    public bool SignalSemaphoreWithPreemption(SmalltalkOOP semaphoreOOP)
    {
        // Semaphore layout: firstLink (0), lastLink (1), excessSignals (2)
        var firstLinkOOP = _heapManager.GetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);

        if (firstLinkOOP == _metadata.KnownObjects.Nil)
        {
            // No processes waiting - increment excessSignals
            var excessOOP = _heapManager.GetInstanceVariable(semaphoreOOP, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX);
            if (excessOOP.IsSmallInteger)
            {
                int excess = excessOOP.AsSmallInteger();
                _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX,
                    SmalltalkOOP.FromSmallInteger(excess + 1));
            }
            return false;  // No process woken, no preemption
        }

        // Process is waiting - wake it up
        var wokenProcessOOP = firstLinkOOP;

        // Remove from semaphore's wait list
        var nextLinkOOP = _heapManager.GetInstanceVariable(wokenProcessOOP, VMConstants.LINK_NEXTLINK_INDEX);
        _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, nextLinkOOP);
        if (nextLinkOOP == _metadata.KnownObjects.Nil)
        {
            _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, _metadata.KnownObjects.Nil);
        }

        // Clear process's myList and nextLink
        _heapManager.SetInstanceVariable(wokenProcessOOP, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
        _heapManager.SetInstanceVariable(wokenProcessOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);

        // Get priorities for preemption check
        int wokenPriority = GetProcessPriority(wokenProcessOOP);
        int activePriority = GetProcessPriority(GetActiveProcess());

        // Check for preemption: if woken process has higher priority, switch to it
        if (wokenPriority > activePriority)
        {
            // Suspend current process and return it to ready queue
            SuspendActiveProcess(returnToQueue: true);

            // Activate the woken process
            ActivateProcess(wokenProcessOOP, "preemption");

            // NOTE: Do NOT set _contextSwitchedByPrimitive here!
            // Only the Signal primitive should set it (after pre-pushing result).
            // When VM signals semaphores directly (e.g., InputSemaphore), the flag
            // would incorrectly affect the next unrelated primitive call.
            return true;  // Preemption occurred
        }
        else
        {
            // No preemption needed - just add to ready queue
            AddProcessToReadyQueue(wokenProcessOOP, wokenPriority);
            return false;  // No preemption
        }
    }

    /// <inheritdoc/>
    public bool WaitOnSemaphore(SmalltalkOOP semaphoreOOP, SmalltalkOOP processOOP)
    {
        // Add process to semaphore's wait list (LinkedList>>addLast:)
        var lastLinkOOP = _heapManager.GetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);
        if (lastLinkOOP == _metadata.KnownObjects.Nil)
        {
            // List is empty
            _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, processOOP);
        }
        else
        {
            // Append to end
            _heapManager.SetInstanceVariable(lastLinkOOP, VMConstants.LINK_NEXTLINK_INDEX, processOOP);
        }
        _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, processOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, semaphoreOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);

        // Switch to next runnable process
        return SwitchToNextRunnableProcess(processOOP);
    }

    /// <inheritdoc/>
    public int GetProcessPriority(SmalltalkOOP processOOP)
    {
        if (processOOP == _metadata.KnownObjects.Nil)
            return 0;

        var priorityOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX);
        return priorityOOP.IsSmallInteger ? priorityOOP.AsSmallInteger() : 0;
    }

    /// <inheritdoc/>
    public SmalltalkOOP GetProcessMyList(SmalltalkOOP processOOP)
    {
        return _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX);
    }

    /// <inheritdoc/>
    public void SetProcessMyList(SmalltalkOOP processOOP, SmalltalkOOP listOOP)
    {
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, listOOP);
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Diagnostic method to dump the contents of all process queues.
    /// </summary>
    private void DumpProcessQueues(SmalltalkOOP schedulerOOP)
    {
        SmalltalkOOP processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
        {
            Console.WriteLine("  quiescentProcessLists is nil!");
            return;
        }

        int arraySize = _heapManager.GetIndexedSize(processListsOOP);
        Console.WriteLine($"  quiescentProcessLists array size: {arraySize}");

        for (int priority = 1; priority <= 8; priority++)
        {
            int index = priority - 1;
            if (index >= arraySize)
            {
                Console.WriteLine($"  Priority {priority}: index out of bounds");
                continue;
            }

            SmalltalkOOP priorityListOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
            if (priorityListOOP == _metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"  Priority {priority}: LinkedList is nil");
                continue;
            }

            SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);

            if (firstLinkOOP == _metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"  Priority {priority}: empty (firstLink=nil)");
            }
            else
            {
                // Count processes in the list
                int count = 0;
                SmalltalkOOP current = firstLinkOOP;
                while (current != _metadata.KnownObjects.Nil && count < 100)
                {
                    count++;
                    SmalltalkOOP suspCtx = _heapManager.GetInstanceVariable(current, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
                    Console.WriteLine($"  Priority {priority}: Process {current} (suspendedContext={suspCtx})");
                    current = _heapManager.GetInstanceVariable(current, VMConstants.LINK_NEXTLINK_INDEX);
                }
            }
        }
    }

    #endregion
}
