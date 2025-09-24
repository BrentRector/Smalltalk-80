using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Implements process scheduling and synchronization primitives for Smalltalk-80.
/// These primitives provide the foundation for concurrent process execution and synchronization.
/// </summary>
/// <remarks>
/// This implementation handles the process scheduling primitives (85-88) according to the Blue Book specification.
/// Full context switching requires deeper VM integration and is deferred to the interpreter.
/// The primitives maintain process state and queue management while acknowledging that actual
/// execution context switching must be handled by the VM's execution engine.
/// </remarks>
public static class ProcessPrimitives
{
    /// <summary>
    /// Registers all process-related primitives with the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register primitives with.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        
        table.Register(85, "semaphoreSignal", PrimitiveSemaphoreSignal);
        table.Register(86, "semaphoreWait", PrimitiveSemaphoreWait);
        table.Register(87, "processResume", PrimitiveProcessResume);
        table.Register(88, "processSuspend", PrimitiveProcessSuspend);
    }

    /// <summary>
    /// Primitive 85: Semaphore signal.
    /// Sends a signal through the semaphore receiver.
    /// </summary>
    /// <remarks>
    /// If one or more processes are suspended waiting for this semaphore,
    /// resumes the first waiting process. Otherwise, increments the semaphore's
    /// excess signals counter to remember the signal for future waits.
    /// </remarks>
    /// <param name="receiver">The Semaphore instance to signal.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>Success with the receiver or failure with appropriate reason.</returns>
    private static PrimitiveResult PrimitiveSemaphoreSignal(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
        // Validate receiver is a Semaphore
        var memory = context.Memory;
        var receiverObj = memory.GetObject(receiver);
        if (receiverObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // Semaphore inherits from LinkedList, which has:
        // - firstLink (field 0)
        // - lastLink (field 1)
        // Semaphore adds:
        // - excessSignals (field 2)
        
        if (receiverObj.Fields.Length < 3)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var firstLink = receiverObj.Fields[0];
        var excessSignals = receiverObj.Fields[2];

        // Get excess signals count
        if (!excessSignals.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var excessCount = excessSignals.SmallIntegerValue;

        // Check if any processes are waiting
        if (firstLink.IsNil)
        {
            // No processes waiting, increment excess signals
            receiverObj.Fields[2] = OOP.FromSmallInteger(excessCount + 1);
        }
        else
        {
            // Remove first waiting process from semaphore's list
            var processToResume = RemoveFirstLink(memory, receiver);
            if (processToResume.IsNil)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

            // Resume the process
            if (context is VirtualMachine vmSignal && vmSignal.IsSchedulerEnabled)
            {
                vmSignal.ResumeProcess(processToResume);
            }
            else
            {
                // Fallback to direct queue manipulation
                if (!ResumeProcess(processToResume, context))
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            }
        }

        return PrimitiveResult.CreateSuccess(receiver);
        }

    /// <summary>
    /// Primitive 86: Semaphore wait.
    /// The active process waits for a signal from the semaphore.
    /// </summary>
    /// <remarks>
    /// If the semaphore has excess signals (excessSignals > 0), decrements the counter
    /// and continues execution. Otherwise, suspends the active process and adds it to
    /// the semaphore's waiting queue until a signal is received.
    /// </remarks>
    /// <param name="receiver">The Semaphore instance to wait on.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>Success with the receiver or failure with appropriate reason.</returns>
    private static PrimitiveResult PrimitiveSemaphoreWait(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
        // Validate receiver is a Semaphore
        var memory = context.Memory;
        var receiverObj = memory.GetObject(receiver);
        if (receiverObj == null || receiverObj.Fields.Length < 3)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var excessSignals = receiverObj.Fields[2];

        // Get excess signals count
        if (!excessSignals.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var excessCount = excessSignals.SmallIntegerValue;

        if (excessCount > 0)
        {
            // Signal available, decrement and continue
            receiverObj.Fields[2] = OOP.FromSmallInteger(excessCount - 1);
            return PrimitiveResult.CreateSuccess(receiver);
        }
        else
        {
            // No signal available, must suspend current process
            var activeProcess = GetActiveProcess(context);
            if (activeProcess.IsNil)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

            // Add active process to semaphore's waiting list
            AddLastLink(memory, receiver, activeProcess);

            // Suspend the active process and switch to another
            if (context is VirtualMachine vmWait && vmWait.IsSchedulerEnabled)
            {
                var suspendedContext = vmWait.SuspendProcess(activeProcess);
                if (!suspendedContext.IsNil)
                {
                    // Process was suspended, waiting on semaphore
                    return PrimitiveResult.CreateSuccess(receiver);
                }
            }
            // Fall back to Smalltalk implementation if scheduler not available
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
        }
        }

    /// <summary>
    /// Primitive 87: Process resume.
    /// Allows the specified process to continue execution.
    /// </summary>
    /// <remarks>
    /// Places the receiver process in the scheduler's queue at its priority level,
    /// making it eligible to become the active process. Fails if the process is
    /// already on a list (myList field is non-nil), preventing double-scheduling.
    /// If the resumed process has higher priority than the current active process,
    /// it may preempt the current process.
    /// </remarks>
    /// <param name="receiver">The Process instance to resume.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>Success with the receiver or failure if already scheduled.</returns>
    private static PrimitiveResult PrimitiveProcessResume(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
        var memory = context.Memory;
        var processObj = memory.GetObject(receiver);
        if (processObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // Process has these instance variables (inherits from Link):
        // - nextLink (field 0, from Link)
        // - suspendedContext (field 1)
        // - priority (field 2) 
        // - myList (field 3)

        if (processObj.Fields.Length < 4)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var myList = processObj.Fields[3];
        
        // Fail if already on a list
        if (!myList.IsNil)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        var priority = processObj.Fields[2];
        if (!priority.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var priorityLevel = priority.SmallIntegerValue;

        // Validate priority range (typically 1-8 in Smalltalk-80)
        if (priorityLevel < 1 || priorityLevel > 100)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        // Add to scheduler's queue at appropriate priority
        if (context is VirtualMachine vmResume && vmResume.IsSchedulerEnabled)
        {
            vmResume.ResumeProcess(receiver);
        }
        else
        {
            // Fallback to direct queue manipulation
            if (!AddProcessToScheduler(receiver, priorityLevel, context))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }

        // Check if we need to preempt current process
        var activeProcess = GetActiveProcess(context);
        if (!activeProcess.IsNil)
        {
            var activeObj = memory.GetObject(activeProcess);
            if (activeObj != null && activeObj.Fields.Length > 2)
            {
                var activePriority = activeObj.Fields[2];
                if (activePriority.IsSmallInteger && 
                    priorityLevel > activePriority.SmallIntegerValue)
                {
                    // Higher priority process should preempt
                    if (context is VirtualMachine vmPreempt && vmPreempt.IsSchedulerEnabled)
                    {
                        // The scheduler will handle preemption automatically
                        // when we added the process to the ready queue
                    }
                }
            }
        }

        return PrimitiveResult.CreateSuccess(receiver);
        }

    /// <summary>
    /// Primitive 88: Process suspend.
    /// Stops the specified process in a resumable state.
    /// </summary>
    /// <remarks>
    /// If the receiver is the active process, suspends it and switches to another
    /// runnable process. If the receiver is not active, removes it from its current
    /// list (scheduler queue or semaphore wait queue). The process can be restarted
    /// later by sending it the resume message.
    /// </remarks>
    /// <param name="receiver">The Process instance to suspend.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>Success with the receiver or failure with appropriate reason.</returns>
    private static PrimitiveResult PrimitiveProcessSuspend(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
        var activeProcess = GetActiveProcess(context);
        
        if (receiver.Equals(activeProcess))
        {
            // Suspending the active process requires context switching
            // Delegate to the VM's process scheduler
            if (context is VirtualMachine virtualMachine && virtualMachine.IsSchedulerEnabled)
            {
                var suspendedContext = virtualMachine.SuspendProcess(receiver);
                if (!suspendedContext.IsNil)
                {
                    return PrimitiveResult.CreateSuccess(receiver);
                }
            }
            // Fall back to Smalltalk implementation if scheduler not available
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
        }
        else
        {
            // Suspending a non-active process - remove it from its list
            var memory = context.Memory;
            var processObj = memory.GetObject(receiver);
            if (processObj == null || processObj.Fields.Length < 4)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var myList = processObj.Fields[3];
            
            // Process must be on a list to suspend
            if (myList.IsNil)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

            // Remove from the list
            if (!RemoveProcessFromList(receiver, myList, context))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

            // Clear myList reference
            processObj.Fields[3] = OOP.Nil;

            return PrimitiveResult.CreateSuccess(receiver);
        }
        }

    #region Helper Methods

    /// <summary>
    /// Gets the currently executing process from the ProcessorScheduler.
    /// </summary>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>The OOP of the active process or Nil if not found.</returns>
    private static OOP GetActiveProcess(IPrimitiveContext context)
        {
        // The active process is tracked by the ProcessorScheduler
        var scheduler = GetProcessorScheduler(context);
        if (scheduler.IsNil)
            return OOP.Nil;

        var schedulerObj = context.Memory.GetObject(scheduler);
        if (schedulerObj == null || schedulerObj.Fields.Length < 2)
            return OOP.Nil;

        // ProcessorScheduler has:
        // - quiescentProcessLists (field 0): Array of LinkedLists
        // - activeProcess (field 1): The currently running Process
        return schedulerObj.Fields[1];
        }

    /// <summary>
    /// Gets the ProcessorScheduler singleton from the special objects array.
    /// </summary>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>The OOP of the ProcessorScheduler or Nil if not found.</returns>
    private static OOP GetProcessorScheduler(IPrimitiveContext context)
        {
        // ProcessorScheduler is at index 8 in Special Objects Array
        return context.GetSpecialObject(SpecialObjects.ProcessorScheduler);
        }

    /// <summary>
    /// Removes and returns the first link from a LinkedList.
    /// </summary>
    /// <param name="memory">The object memory for accessing objects.</param>
    /// <param name="listOop">The LinkedList to remove from.</param>
    /// <returns>The removed link or Nil if the list was empty.</returns>
    private static OOP RemoveFirstLink(ObjectMemory memory, OOP listOop)
        {
        var listObj = memory.GetObject(listOop);
        if (listObj == null || listObj.Fields.Length < 2)
            return OOP.Nil;

        var firstLink = listObj.Fields[0];
        if (firstLink.IsNil)
            return OOP.Nil;

        var firstLinkObj = memory.GetObject(firstLink);
        if (firstLinkObj == null)
            return OOP.Nil;

        // Get the next link
        var nextLink = firstLinkObj.Fields[0];
        
        // Update list's first link
        listObj.Fields[0] = nextLink;
        
        // If list is now empty, clear last link too
        if (nextLink.IsNil)
        {
            listObj.Fields[1] = OOP.Nil;
        }
        else
        {
            // If there was only one element, update last link
            if (listObj.Fields[1].Equals(firstLink))
                listObj.Fields[1] = nextLink;
        }

        // Clear the removed link's next pointer and myList if it's a Process
        firstLinkObj.Fields[0] = OOP.Nil;
        if (firstLinkObj.Fields.Length > 3)
            firstLinkObj.Fields[3] = OOP.Nil;

        return firstLink;
        }

    /// <summary>
    /// Adds a link to the end of a LinkedList.
    /// </summary>
    /// <param name="memory">The object memory for accessing objects.</param>
    /// <param name="listOop">The LinkedList to add to.</param>
    /// <param name="linkOop">The link to add to the list.</param>
    private static void AddLastLink(ObjectMemory memory, OOP listOop, OOP linkOop)
        {
        var listObj = memory.GetObject(listOop);
        if (listObj == null || listObj.Fields.Length < 2)
            return;

        var linkObj = memory.GetObject(linkOop);
        if (linkObj == null)
            return;

        var lastLink = listObj.Fields[1];
        
        if (lastLink.IsNil)
        {
            // List was empty
            listObj.Fields[0] = linkOop; // firstLink
            listObj.Fields[1] = linkOop; // lastLink
        }
        else
        {
            // Add to end
            var lastLinkObj = memory.GetObject(lastLink);
            if (lastLinkObj != null)
            {
                lastLinkObj.Fields[0] = linkOop; // Set nextLink of previous last
            }
            listObj.Fields[1] = linkOop; // Update lastLink
        }

        // Clear new link's next pointer
        linkObj.Fields[0] = OOP.Nil;
        
        // Set myList if this is a Process (field 3)
        if (linkObj.Fields.Length > 3)
            linkObj.Fields[3] = listOop;
        }

    /// <summary>
    /// Resumes a process by adding it to the scheduler's ready queue.
    /// </summary>
    /// <param name="process">The process to resume.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>True if the process was successfully added to the scheduler; otherwise, false.</returns>
    private static bool ResumeProcess(OOP process, IPrimitiveContext context)
        {
        var memory = context.Memory;
        var processObj = memory.GetObject(process);
        if (processObj == null || processObj.Fields.Length < 4)
            return false;

        var priority = processObj.Fields[2];
        if (!priority.IsSmallInteger)
            return false;

        var priorityLevel = priority.SmallIntegerValue;
        
        // Add to scheduler and update myList
        if (!AddProcessToScheduler(process, priorityLevel, context))
            return false;
            
        return true;
        }

    /// <summary>
    /// Adds a process to the appropriate priority queue in the scheduler.
    /// </summary>
    /// <param name="process">The process to add.</param>
    /// <param name="priority">The priority level (1-based).</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>True if the process was successfully added; otherwise, false.</returns>
    private static bool AddProcessToScheduler(OOP process, int priority, IPrimitiveContext context)
        {
        var scheduler = GetProcessorScheduler(context);
        if (scheduler.IsNil)
            return false;

        var memory = context.Memory;
        var schedulerObj = memory.GetObject(scheduler);
        if (schedulerObj == null || schedulerObj.Fields.Length < 1)
            return false;

        var processLists = schedulerObj.Fields[0];
        var listsObj = memory.GetObject(processLists);
        if (listsObj == null)
            return false;

        // Priority is 1-based, array is 0-based
        var index = priority - 1;
        if (index < 0 || index >= listsObj.Fields.Length)
            return false;

        var priorityList = listsObj.Fields[index];
        
        // The priority list should exist (initialized during system bootstrap)
        if (priorityList.IsNil)
            return false;

        // Add process to the priority queue
        AddLastLink(memory, priorityList, process);
        
        // Update the process's myList field to the priority list
        var processObj = memory.GetObject(process);
        if (processObj != null && processObj.Fields.Length > 3)
            processObj.Fields[3] = priorityList;
            
        return true;
        }

    /// <summary>
    /// Removes a process from its current list (scheduler queue or semaphore).
    /// </summary>
    /// <param name="process">The process to remove.</param>
    /// <param name="list">The list to remove the process from.</param>
    /// <param name="context">The primitive execution context.</param>
    /// <returns>True if the process was successfully removed; otherwise, false.</returns>
    private static bool RemoveProcessFromList(OOP process, OOP list, IPrimitiveContext context)
    {
        var memory = context.Memory;
        var listObj = memory.GetObject(list);
        if (listObj == null || listObj.Fields.Length < 2)
            return false;

        var current = listObj.Fields[0];
        OOP previous = OOP.Nil;

        // Search for the process in the list
        while (!current.IsNil)
        {
            if (current.Equals(process))
            {
                var processObj = memory.GetObject(process);
                if (processObj == null)
                    return false;

                var nextLink = processObj.Fields[0];

                if (previous.IsNil)
                {
                    // Removing first element
                    listObj.Fields[0] = nextLink;
                    if (nextLink.IsNil)
                    {
                        // List is now empty
                        listObj.Fields[1] = OOP.Nil;
                    }
                    else if (listObj.Fields[1].Equals(process))
                    {
                        // Was the only element
                        listObj.Fields[1] = nextLink;
                    }
                }
                else
                {
                    // Removing middle or last element
                    var prevObj = memory.GetObject(previous);
                    if (prevObj != null)
                        prevObj.Fields[0] = nextLink;
                    
                    // Update last link if we're removing the last element
                    if (listObj.Fields[1].Equals(process))
                        listObj.Fields[1] = previous;
                }

                // Clear the process's next link and myList
                processObj.Fields[0] = OOP.Nil;
                if (processObj.Fields.Length > 3)
                    processObj.Fields[3] = OOP.Nil;
                    
                return true;
            }

            previous = current;
            var currentObj = memory.GetObject(current);
            if (currentObj == null)
                break;
            current = currentObj.Fields[0];
        }

        return false;
    }

    #endregion
}