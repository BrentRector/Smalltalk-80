using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing process scheduling support methods.
/// These methods implement the Smalltalk-80 process scheduling mechanism
/// including timer-based semaphore signaling, process queue management,
/// and context switching.
/// </summary>
public partial class VirtualMachine
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

        // Signal the semaphore by incrementing excessSignals or waking a waiter
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
        }
        else
        {
            // Process is waiting - wake it up by moving to ready queue
            // Remove first process from semaphore's wait list
            var processOOP = firstLinkOOP;
            var nextLinkOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
            _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, nextLinkOOP);
            if (nextLinkOOP == _metadata.KnownObjects.Nil)
            {
                _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, _metadata.KnownObjects.Nil);
            }

            // Clear process's myList and nextLink
            _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
            _heapManager.SetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);

            // Add process to its priority queue (make it runnable)
            var priorityOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX);
            if (priorityOOP.IsSmallInteger)
            {
                int priority = priorityOOP.AsSmallInteger();
                AddProcessToReadyQueue(processOOP, priority);
            }
        }
    }

    /// <summary>
    /// Adds a process to the appropriate priority ready queue.
    /// </summary>
    private void AddProcessToReadyQueue(SmalltalkOOP processOOP, int priority)
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

    /// <summary>
    /// Gets the currently active process OOP from ProcessorScheduler.
    /// </summary>
    private SmalltalkOOP GetActiveProcess()
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        return _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);
    }

    /// <summary>
    /// Switches execution to the next runnable process when the current process terminates.
    /// Used by HandleReturn when a forked process completes its block.
    /// Returns true if successfully switched, false if no runnable processes found.
    /// </summary>
    private bool SwitchToNextRunnableProcess(SmalltalkOOP terminatingProcess)
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return false;

        // Find highest priority runnable process
        SmalltalkOOP nextProcess = FindHighestPriorityRunnableProcess(schedulerOOP);
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
            // Get process priority for logging (only log priority 1 - background process)
            var priorityOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_PRIORITY_INDEX);
            int priority = priorityOOP.IsSmallInteger ? priorityOOP.AsSmallInteger() : -1;
            if (priority == 1)
                Console.WriteLine($"[PQ-] REMOVE process={nextProcess} from priority={priority} (activating after termination)");

            RemoveProcessFromList(nextProcess, myListOOP);
            _heapManager.SetInstanceVariable(nextProcess, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
        }

        // Get the suspended context from the next process
        SmalltalkOOP nextContext = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
        if (nextContext == _metadata.KnownObjects.Nil)
            return false;

        // Debug: Log when activating priority 1 process
        var activatingPriorityOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_PRIORITY_INDEX);
        int activatingPriority = activatingPriorityOOP.IsSmallInteger ? activatingPriorityOOP.AsSmallInteger() : -1;
        if (activatingPriority == 1)
        {
            Console.WriteLine($"[ACTIVATING PROCESS] Priority 1 (background) process={nextProcess}");
            Console.WriteLine($"  Suspended context: {nextContext}");
            var ctxClassName = _heapManager.GetClassName(nextContext);
            Console.WriteLine($"  Context class: {ctxClassName}");
            var pcOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX);
            var startPCOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            var callerOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX);
            Console.WriteLine($"  PC: {pcOOP}, startPC: {startPCOOP}, caller: {callerOOP}");
            if (ctxClassName == "BlockContext")
            {
                var homeOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                Console.WriteLine($"  Home context: {homeOOP}");
                var homeClassName = _heapManager.GetClassName(homeOOP);
                Console.WriteLine($"  Home class: {homeClassName}");
                if (homeClassName == "MethodContext")
                {
                    var methodOOP = _heapManager.GetInstanceVariable(homeOOP, VMConstants.CONTEXT_METHOD_INDEX);
                    Console.WriteLine($"  Method: {methodOOP}");
                }
            }
        }

        // Check if this is an unactivated BlockContext (PC is nil)
        // This happens when resuming a newly created process via [block] newProcess
        SmalltalkOOP nextContextPC = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX);
        if (nextContextPC == _metadata.KnownObjects.Nil)
        {
            // Activate the block context - set PC from startPC
            SmalltalkOOP startPC = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            if (startPC == _metadata.KnownObjects.Nil || !startPC.IsSmallInteger)
                return false;

            if (activatingPriority == 1)
                Console.WriteLine($"  Activating block: setting PC={startPC.AsSmallInteger()}, stackp=0, caller=nil");

            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX, startPC);
            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));
            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, _metadata.KnownObjects.Nil);
        }

        // Update Processor's activeProcess
        _heapManager.SetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX, nextProcess);

        // Switch to the new context
        _contextManager.SetActiveContext(nextContext);

        return true;
    }

    /// <summary>
    /// Finds the highest priority process that is runnable (on a quiescentProcessList).
    /// Searches from priority 8 down to priority 1.
    /// </summary>
    private SmalltalkOOP FindHighestPriorityRunnableProcess(SmalltalkOOP schedulerOOP)
    {
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
            SmalltalkOOP lastLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);

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

    /// <summary>
    /// Removes a process from a linked list (LinkedList structure).
    /// </summary>
    private void RemoveProcessFromList(SmalltalkOOP processOOP, SmalltalkOOP listOOP)
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
}
