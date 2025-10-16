using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Control flow primitives for blocks, message sending, and process management (primitives 80-88).
/// Stubs for now - require BlockContext, Process scheduler, and Semaphore infrastructure.
/// </summary>
public static class ControlPrimitives
{
    /// <summary>
    /// Primitive 80: blockCopy: - Create a block closure.
    /// </summary>
    public static SmalltalkOOP BlockCopy(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int argumentCount = args[0].AsSmallInteger();

        if (argumentCount < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get the active context (the home context for the block)
        SmalltalkOOP homeContextOop = ctx.GetActiveContext();

        // Get the current instruction pointer
        // The block starts at PC + 2 (after the blockCopy: bytecode and its argument)
        int currentPC = ctx.GetInstructionPointer();
        int blockStartPC = currentPC + 2;

        // Create the BlockContext
        SmalltalkOOP blockContextOop = ctx.CreateBlockContext(homeContextOop, blockStartPC, argumentCount);

        // Return the BlockContext as the result
        return blockContextOop;
    }

    /// <summary>
    /// Primitive 81: value - Activate a zero-argument block.
    /// </summary>
    public static SmalltalkOOP Value(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver should be a BlockContext
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Create empty arguments array
        SmalltalkOOP emptyArrayOop = ctx.AllocateObject("class:Array", 0);

        // Activate the block
        SmalltalkOOP resultOop = ctx.ActivateBlockContext(receiver, emptyArrayOop);

        // Check if activation failed (argument count mismatch returns nil)
        if (resultOop == SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return the result of block execution
        return resultOop;
    }

    /// <summary>
    /// Primitive 82: value: - Activate a one-argument block.
    /// </summary>
    public static SmalltalkOOP ValueWithArg(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver should be a BlockContext
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Create arguments array with one element
        SmalltalkOOP argsArrayOop = ctx.AllocateObject("class:Array", 1);
        // Note: This requires HeapManager to have SetIndexedOOP method
        // For now we'll leave this as a stub - needs heap manager support
        // ctx.HeapManager.SetIndexedOOP(argsArrayOop, 0, args[0]);

        // Activate the block
        SmalltalkOOP resultOop = ctx.ActivateBlockContext(receiver, argsArrayOop);

        // Check if activation failed (argument count mismatch returns nil)
        if (resultOop == SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return the result of block execution
        return resultOop;
    }

    /// <summary>
    /// Primitive 83: perform: - Dynamically send a message with no arguments.
    /// </summary>
    public static SmalltalkOOP Perform(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // args[0] should be a Symbol containing the selector
        if (args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP selectorOop = args[0];

        // Create empty arguments array
        SmalltalkOOP emptyArrayOop = ctx.AllocateObject("class:Array", 0);

        // Send the message using VM's message dispatch
        SmalltalkOOP resultOop = ctx.SendMessage(receiver, selectorOop, emptyArrayOop);

        // Return the result
        return resultOop;
    }

    /// <summary>
    /// Primitive 84: perform:with: - Dynamically send a message with one argument.
    /// </summary>
    public static SmalltalkOOP PerformWith(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // args[0] = selector (Symbol), args[1] = argument
        if (args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP selectorOop = args[0];

        // Create arguments array with one element
        SmalltalkOOP argsArrayOop = ctx.AllocateObject("class:Array", 1);
        // Note: This requires HeapManager to have SetIndexedOOP method
        // For now we'll leave this as a stub - needs heap manager support
        // ctx.HeapManager.SetIndexedOOP(argsArrayOop, 0, args[1]);

        // Send the message using VM's message dispatch
        SmalltalkOOP resultOop = ctx.SendMessage(receiver, selectorOop, argsArrayOop);

        // Return the result
        return resultOop;
    }

    /// <summary>
    /// Primitive 85: signal - Signal a semaphore.
    /// Blue Book: If processes are waiting, resume first one; otherwise increment excessSignals.
    /// </summary>
    public static SmalltalkOOP Signal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Semaphore (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get Semaphore fields: firstLink, lastLink, excessSignals
        SmalltalkOOP firstLinkOop = ctx.GetFieldValue(receiver, "class:Semaphore", "firstLink");
        SmalltalkOOP excessSignalsOop = ctx.GetFieldValue(receiver, "class:Semaphore", "excessSignals");

        // Check if any processes are waiting (firstLink is not nil)
        if (firstLinkOop == SpecialObjectsArray.Instance.Nil)
        {
            // No processes waiting - increment excessSignals
            if (!excessSignalsOop.IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            int excessCount = excessSignalsOop.AsSmallInteger();
            ctx.SetFieldValue(receiver, "class:Semaphore", "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount + 1));
        }
        else
        {
            // Remove first waiting process from semaphore's linked list
            SmalltalkOOP processOop = RemoveFirstLink(ctx, receiver, "class:Semaphore");

            if (processOop == SpecialObjectsArray.Instance.Nil)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Resume the process by adding it to scheduler's ready queue
            if (!ResumeProcess(ctx, processOop))
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 86: wait - Wait on a semaphore.
    /// Blue Book: If excessSignals > 0, decrement; otherwise suspend active process.
    /// </summary>
    public static SmalltalkOOP Wait(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Semaphore
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get excessSignals field
        SmalltalkOOP excessSignalsOop = ctx.GetFieldValue(receiver, "class:Semaphore", "excessSignals");

        if (!excessSignalsOop.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int excessCount = excessSignalsOop.AsSmallInteger();

        if (excessCount > 0)
        {
            // Signal available - decrement and continue
            ctx.SetFieldValue(receiver, "class:Semaphore", "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount - 1));
            return receiver;
        }
        else
        {
            // No signal available - suspend active process
            SmalltalkOOP activeProcessOop = GetActiveProcess(ctx);

            if (activeProcessOop == SpecialObjectsArray.Instance.Nil)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Add active process to semaphore's waiting list
            AddLastLink(ctx, receiver, "class:Semaphore", activeProcessOop);

            // NOTE: Actual process suspension requires VM context switching
            // This primitive sets up the data structures, but the VM must handle the context switch
            // For now, we fail the primitive and let Smalltalk code handle it
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 87: resume - Resume a suspended process.
    /// Blue Book: Add process to scheduler's ready queue at its priority level.
    /// </summary>
    public static SmalltalkOOP Resume(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Process
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get Process fields: myList must be nil (not already scheduled)
        SmalltalkOOP myListOop = ctx.GetFieldValue(receiver, "class:Process", "myList");

        // Fail if already on a list (already scheduled)
        if (myListOop != SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Resume the process
        if (!ResumeProcess(ctx, receiver))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 88: suspend - Suspend a process.
    /// Blue Book: If receiver is active process, switch to another; otherwise remove from list.
    /// </summary>
    public static SmalltalkOOP Suspend(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Process
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP activeProcessOop = GetActiveProcess(ctx);

        if (receiver == activeProcessOop)
        {
            // Suspending the active process requires VM context switching
            // This primitive cannot fully implement this without deeper VM integration
            // Fail and let Smalltalk code handle it
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        else
        {
            // Suspending a non-active process - remove it from its list
            SmalltalkOOP myListOop = ctx.GetFieldValue(receiver, "class:Process", "myList");

            // Process must be on a list to suspend
            if (myListOop == SpecialObjectsArray.Instance.Nil)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Remove process from its list
            if (!RemoveProcessFromList(ctx, receiver, myListOop))
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Clear myList reference
            ctx.SetFieldValue(receiver, "class:Process", "myList", SpecialObjectsArray.Instance.Nil);

            return receiver;
        }
    }

    #region Helper Methods

    /// <summary>
    /// Gets the currently active process from ProcessorScheduler.
    /// </summary>
    private static SmalltalkOOP GetActiveProcess(IPrimitiveContext ctx)
    {
        // ProcessorScheduler is at index 4 in Special Objects Array
        SmalltalkOOP schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler;

        if (schedulerOop == SpecialObjectsArray.Instance.Nil)
            return SpecialObjectsArray.Instance.Nil;

        // Get activeProcess field from ProcessorScheduler
        return ctx.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "activeProcess");
    }

    /// <summary>
    /// Removes and returns the first link from a LinkedList or Semaphore.
    /// </summary>
    private static SmalltalkOOP RemoveFirstLink(IPrimitiveContext ctx, SmalltalkOOP listOop, string className)
    {
        SmalltalkOOP firstLinkOop = ctx.GetFieldValue(listOop, className, "firstLink");

        if (firstLinkOop == SpecialObjectsArray.Instance.Nil)
            return SpecialObjectsArray.Instance.Nil;

        // Get nextLink from the first element
        SmalltalkOOP nextLinkOop = ctx.GetFieldValue(firstLinkOop, "class:Link", "nextLink");

        // Update list's firstLink
        ctx.SetFieldValue(listOop, className, "firstLink", nextLinkOop);

        // If list is now empty, clear lastLink too
        if (nextLinkOop == SpecialObjectsArray.Instance.Nil)
        {
            ctx.SetFieldValue(listOop, className, "lastLink", SpecialObjectsArray.Instance.Nil);
        }

        // Clear the removed link's nextLink pointer
        ctx.SetFieldValue(firstLinkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

        // If it's a Process, clear myList field
        try
        {
            ctx.SetFieldValue(firstLinkOop, "class:Process", "myList", SpecialObjectsArray.Instance.Nil);
        }
        catch
        {
            // Not a Process, just a Link - that's fine
        }

        return firstLinkOop;
    }

    /// <summary>
    /// Adds a link to the end of a LinkedList or Semaphore.
    /// </summary>
    private static void AddLastLink(IPrimitiveContext ctx, SmalltalkOOP listOop, string className, SmalltalkOOP linkOop)
    {
        SmalltalkOOP lastLinkOop = ctx.GetFieldValue(listOop, className, "lastLink");

        if (lastLinkOop == SpecialObjectsArray.Instance.Nil)
        {
            // List was empty
            ctx.SetFieldValue(listOop, className, "firstLink", linkOop);
            ctx.SetFieldValue(listOop, className, "lastLink", linkOop);
        }
        else
        {
            // Add to end
            ctx.SetFieldValue(lastLinkOop, "class:Link", "nextLink", linkOop);
            ctx.SetFieldValue(listOop, className, "lastLink", linkOop);
        }

        // Clear new link's nextLink pointer
        ctx.SetFieldValue(linkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

        // Set myList if this is a Process
        try
        {
            ctx.SetFieldValue(linkOop, "class:Process", "myList", listOop);
        }
        catch
        {
            // Not a Process, just a Link - that's fine
        }
    }

    /// <summary>
    /// Resumes a process by adding it to the scheduler's ready queue at its priority level.
    /// </summary>
    private static bool ResumeProcess(IPrimitiveContext ctx, SmalltalkOOP processOop)
    {
        // Get process priority
        SmalltalkOOP priorityOop = ctx.GetFieldValue(processOop, "class:Process", "priority");

        if (!priorityOop.IsSmallInteger)
            return false;

        int priority = priorityOop.AsSmallInteger();

        // Validate priority range (typically 1-8 in Smalltalk-80, but allow up to 100)
        if (priority < 1 || priority > 100)
            return false;

        // Get ProcessorScheduler
        SmalltalkOOP schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler;

        if (schedulerOop == SpecialObjectsArray.Instance.Nil)
            return false;

        // Get quiescentProcessLists array
        SmalltalkOOP processListsOop = ctx.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "quiescentProcessLists");

        if (processListsOop == SpecialObjectsArray.Instance.Nil)
            return false;

        // Priority is 1-based, array is 0-based
        int index = priority - 1;

        // Get the LinkedList at the priority level
        // Note: This requires HeapManager to have GetIndexedOOP method
        // For now we'll leave this as a stub - needs heap manager support
        // SmalltalkOOP priorityListOop = ctx.HeapManager.GetIndexedOOP(processListsOop, index);
        // if (priorityListOop == SpecialObjectsArray.Instance.Nil)
        //     return false;

        // Add process to the priority queue
        // AddLastLink(ctx, priorityListOop, "class:LinkedList", processOop);

        return false; // Stub for now
    }

    /// <summary>
    /// Removes a process from its current list.
    /// </summary>
    private static bool RemoveProcessFromList(IPrimitiveContext ctx, SmalltalkOOP processOop, SmalltalkOOP listOop)
    {
        SmalltalkOOP currentOop = ctx.GetFieldValue(listOop, "class:LinkedList", "firstLink");
        SmalltalkOOP previousOop = SpecialObjectsArray.Instance.Nil;

        // Search for the process in the list
        while (currentOop != SpecialObjectsArray.Instance.Nil)
        {
            if (currentOop == processOop)
            {
                // Found it - get nextLink
                SmalltalkOOP nextLinkOop = ctx.GetFieldValue(processOop, "class:Link", "nextLink");

                if (previousOop == SpecialObjectsArray.Instance.Nil)
                {
                    // Removing first element
                    ctx.SetFieldValue(listOop, "class:LinkedList", "firstLink", nextLinkOop);

                    if (nextLinkOop == SpecialObjectsArray.Instance.Nil)
                    {
                        // List is now empty
                        ctx.SetFieldValue(listOop, "class:LinkedList", "lastLink", SpecialObjectsArray.Instance.Nil);
                    }
                }
                else
                {
                    // Removing middle or last element
                    ctx.SetFieldValue(previousOop, "class:Link", "nextLink", nextLinkOop);

                    // Update lastLink if we're removing the last element
                    SmalltalkOOP lastLinkOop = ctx.GetFieldValue(listOop, "class:LinkedList", "lastLink");
                    if (lastLinkOop == processOop)
                    {
                        ctx.SetFieldValue(listOop, "class:LinkedList", "lastLink", previousOop);
                    }
                }

                // Clear the process's nextLink
                ctx.SetFieldValue(processOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

                return true;
            }

            previousOop = currentOop;
            currentOop = ctx.GetFieldValue(currentOop, "class:Link", "nextLink");
        }

        return false;
    }

    #endregion
}