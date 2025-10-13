using Smalltalk80.Interpreter.Core.Values;
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
    public static SObjectRef BlockCopy(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
        int homeContextOop = ctx.GetActiveContext();

        // Get the current instruction pointer
        // The block starts at PC + 2 (after the blockCopy: bytecode and its argument)
        int currentPC = ctx.GetInstructionPointer();
        int blockStartPC = currentPC + 2;

        // Create the BlockContext
        int blockContextOop = ctx.CreateBlockContext(homeContextOop, blockStartPC, argumentCount);

        // Return the BlockContext as the result
        return SObjectRef.FromObjectPointer(blockContextOop);
    }

    /// <summary>
    /// Primitive 81: value - Activate a zero-argument block.
    /// </summary>
    public static SObjectRef Value(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int blockContextOop = (int)receiver.AsObjectPointer();

        // Create empty arguments array
        int emptyArrayOop = ctx.AllocateObject("class:Array", 0);

        // Activate the block
        int resultOop = ctx.ActivateBlockContext(blockContextOop, emptyArrayOop);

        // Check if activation failed (argument count mismatch returns nil)
        if (resultOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return the result of block execution
        return SObjectRef.FromObjectPointer(resultOop);
    }

    /// <summary>
    /// Primitive 82: value: - Activate a one-argument block.
    /// </summary>
    public static SObjectRef ValueWithArg(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int blockContextOop = (int)receiver.AsObjectPointer();

        // Create arguments array with one element
        int argsArrayOop = ctx.AllocateObject("class:Array", 1);
        int argOop = args[0].IsSmallInteger
            ? (int)args[0].AsSmallInteger()
            : (int)args[0].AsObjectPointer();
        ctx.Heap.SetIndexedField(argsArrayOop, 0, argOop);

        // Activate the block
        int resultOop = ctx.ActivateBlockContext(blockContextOop, argsArrayOop);

        // Check if activation failed (argument count mismatch returns nil)
        if (resultOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return the result of block execution
        return SObjectRef.FromObjectPointer(resultOop);
    }

    /// <summary>
    /// Primitive 83: perform: - Dynamically send a message with no arguments.
    /// </summary>
    public static SObjectRef Perform(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        // Convert receiver and selector to object pointers
        int receiverOop = receiver.IsSmallInteger
            ? (int)receiver.AsSmallInteger()
            : (int)receiver.AsObjectPointer();
        int selectorOop = (int)args[0].AsObjectPointer();

        // Create empty arguments array
        int emptyArrayOop = ctx.AllocateObject("class:Array", 0);

        // Send the message using VM's message dispatch
        int resultOop = ctx.SendMessage(receiverOop, selectorOop, emptyArrayOop);

        // Convert result back to SObjectRef
        return SObjectRef.FromObjectPointer(resultOop);
    }

    /// <summary>
    /// Primitive 84: perform:with: - Dynamically send a message with one argument.
    /// </summary>
    public static SObjectRef PerformWith(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        // Convert receiver and selector to object pointers
        int receiverOop = receiver.IsSmallInteger
            ? (int)receiver.AsSmallInteger()
            : (int)receiver.AsObjectPointer();
        int selectorOop = (int)args[0].AsObjectPointer();

        // Create arguments array with one element
        int argsArrayOop = ctx.AllocateObject("class:Array", 1);
        int argOop = args[1].IsSmallInteger
            ? (int)args[1].AsSmallInteger()
            : (int)args[1].AsObjectPointer();
        ctx.Heap.SetIndexedField(argsArrayOop, 0, argOop);

        // Send the message using VM's message dispatch
        int resultOop = ctx.SendMessage(receiverOop, selectorOop, argsArrayOop);

        // Convert result back to SObjectRef
        return SObjectRef.FromObjectPointer(resultOop);
    }

    /// <summary>
    /// Primitive 85: signal - Signal a semaphore.
    /// Blue Book: If processes are waiting, resume first one; otherwise increment excessSignals.
    /// </summary>
    public static SObjectRef Signal(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int semaphoreOop = (int)receiver.AsObjectPointer();

        // Get Semaphore fields: firstLink, lastLink, excessSignals
        int firstLinkOop = ctx.GetFieldValue(semaphoreOop, "class:Semaphore", "firstLink");
        int excessSignalsOop = ctx.GetFieldValue(semaphoreOop, "class:Semaphore", "excessSignals");

        // Check if any processes are waiting (firstLink is not nil)
        if (firstLinkOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            // No processes waiting - increment excessSignals
            if (!IsSmallInteger(excessSignalsOop))
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            int excessCount = DecodeSmallInteger(excessSignalsOop);
            ctx.SetFieldValue(semaphoreOop, "class:Semaphore", "excessSignals", EncodeSmallInteger(excessCount + 1));
        }
        else
        {
            // Remove first waiting process from semaphore's linked list
            int processOop = RemoveFirstLink(ctx, semaphoreOop, "class:Semaphore");

            if (processOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
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
    public static SObjectRef Wait(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int semaphoreOop = (int)receiver.AsObjectPointer();

        // Get excessSignals field
        int excessSignalsOop = ctx.GetFieldValue(semaphoreOop, "class:Semaphore", "excessSignals");

        if (!IsSmallInteger(excessSignalsOop))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int excessCount = DecodeSmallInteger(excessSignalsOop);

        if (excessCount > 0)
        {
            // Signal available - decrement and continue
            ctx.SetFieldValue(semaphoreOop, "class:Semaphore", "excessSignals", EncodeSmallInteger(excessCount - 1));
            return receiver;
        }
        else
        {
            // No signal available - suspend active process
            int activeProcessOop = GetActiveProcess(ctx);

            if (activeProcessOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Add active process to semaphore's waiting list
            AddLastLink(ctx, semaphoreOop, "class:Semaphore", activeProcessOop);

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
    public static SObjectRef Resume(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int processOop = (int)receiver.AsObjectPointer();

        // Get Process fields: myList must be nil (not already scheduled)
        int myListOop = ctx.GetFieldValue(processOop, "class:Process", "myList");

        // Fail if already on a list (already scheduled)
        if (myListOop != SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Resume the process
        if (!ResumeProcess(ctx, processOop))
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
    public static SObjectRef Suspend(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int processOop = (int)receiver.AsObjectPointer();
        int activeProcessOop = GetActiveProcess(ctx);

        if (processOop == activeProcessOop)
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
            int myListOop = ctx.GetFieldValue(processOop, "class:Process", "myList");

            // Process must be on a list to suspend
            if (myListOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Remove process from its list
            if (!RemoveProcessFromList(ctx, processOop, myListOop))
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Clear myList reference
            ctx.SetFieldValue(processOop, "class:Process", "myList", SpecialObjectsArray.Instance.Nil.AsObjectPointer());

            return receiver;
        }
    }

    #region Helper Methods

    /// <summary>
    /// Gets the currently active process from ProcessorScheduler.
    /// </summary>
    private static int GetActiveProcess(IPrimitiveContext ctx)
    {
        // ProcessorScheduler is at index 4 in Special Objects Array
        int schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler.AsObjectPointer();

        if (schedulerOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            return SpecialObjectsArray.Instance.Nil.AsObjectPointer();

        // Get activeProcess field from ProcessorScheduler
        return ctx.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "activeProcess");
    }

    /// <summary>
    /// Removes and returns the first link from a LinkedList or Semaphore.
    /// </summary>
    private static int RemoveFirstLink(IPrimitiveContext ctx, int listOop, string className)
    {
        int firstLinkOop = ctx.GetFieldValue(listOop, className, "firstLink");

        if (firstLinkOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            return SpecialObjectsArray.Instance.Nil.AsObjectPointer();

        // Get nextLink from the first element
        int nextLinkOop = ctx.GetFieldValue(firstLinkOop, "class:Link", "nextLink");

        // Update list's firstLink
        ctx.SetFieldValue(listOop, className, "firstLink", nextLinkOop);

        // If list is now empty, clear lastLink too
        if (nextLinkOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            ctx.SetFieldValue(listOop, className, "lastLink", SpecialObjectsArray.Instance.Nil.AsObjectPointer());
        }

        // Clear the removed link's nextLink pointer
        ctx.SetFieldValue(firstLinkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil.AsObjectPointer());

        // If it's a Process, clear myList field
        try
        {
            ctx.SetFieldValue(firstLinkOop, "class:Process", "myList", SpecialObjectsArray.Instance.Nil.AsObjectPointer());
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
    private static void AddLastLink(IPrimitiveContext ctx, int listOop, string className, int linkOop)
    {
        int lastLinkOop = ctx.GetFieldValue(listOop, className, "lastLink");

        if (lastLinkOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
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
        ctx.SetFieldValue(linkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil.AsObjectPointer());

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
    private static bool ResumeProcess(IPrimitiveContext ctx, int processOop)
    {
        // Get process priority
        int priorityOop = ctx.GetFieldValue(processOop, "class:Process", "priority");

        if (!IsSmallInteger(priorityOop))
            return false;

        int priority = DecodeSmallInteger(priorityOop);

        // Validate priority range (typically 1-8 in Smalltalk-80, but allow up to 100)
        if (priority < 1 || priority > 100)
            return false;

        // Get ProcessorScheduler
        int schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler.AsObjectPointer();

        if (schedulerOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            return false;

        // Get quiescentProcessLists array
        int processListsOop = ctx.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "quiescentProcessLists");

        if (processListsOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            return false;

        // Priority is 1-based, array is 0-based
        int index = priority - 1;

        // Get the LinkedList at the priority level
        int priorityListOop = ctx.Heap.GetIndexedField(processListsOop, index);

        if (priorityListOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
            return false;

        // Add process to the priority queue
        AddLastLink(ctx, priorityListOop, "class:LinkedList", processOop);

        return true;
    }

    /// <summary>
    /// Removes a process from its current list.
    /// </summary>
    private static bool RemoveProcessFromList(IPrimitiveContext ctx, int processOop, int listOop)
    {
        int currentOop = ctx.GetFieldValue(listOop, "class:LinkedList", "firstLink");
        int previousOop = SpecialObjectsArray.Instance.Nil.AsObjectPointer();

        // Search for the process in the list
        while (currentOop != SpecialObjectsArray.Instance.Nil.AsObjectPointer())
        {
            if (currentOop == processOop)
            {
                // Found it - get nextLink
                int nextLinkOop = ctx.GetFieldValue(processOop, "class:Link", "nextLink");

                if (previousOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
                {
                    // Removing first element
                    ctx.SetFieldValue(listOop, "class:LinkedList", "firstLink", nextLinkOop);

                    if (nextLinkOop == SpecialObjectsArray.Instance.Nil.AsObjectPointer())
                    {
                        // List is now empty
                        ctx.SetFieldValue(listOop, "class:LinkedList", "lastLink", SpecialObjectsArray.Instance.Nil.AsObjectPointer());
                    }
                }
                else
                {
                    // Removing middle or last element
                    ctx.SetFieldValue(previousOop, "class:Link", "nextLink", nextLinkOop);

                    // Update lastLink if we're removing the last element
                    int lastLinkOop = ctx.GetFieldValue(listOop, "class:LinkedList", "lastLink");
                    if (lastLinkOop == processOop)
                    {
                        ctx.SetFieldValue(listOop, "class:LinkedList", "lastLink", previousOop);
                    }
                }

                // Clear the process's nextLink
                ctx.SetFieldValue(processOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil.AsObjectPointer());

                return true;
            }

            previousOop = currentOop;
            currentOop = ctx.GetFieldValue(currentOop, "class:Link", "nextLink");
        }

        return false;
    }

    private static bool IsSmallInteger(int oop) => (oop & 1) == 1;
    private static int EncodeSmallInteger(int value) => (value << 1) | 1;
    private static int DecodeSmallInteger(int oop) => oop >> 1;

    #endregion
}
