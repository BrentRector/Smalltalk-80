using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Control flow primitives using new IPrimitive interface.
/// Implements blocks (80-82), perform (83-84), process management (85-88), and cache (89).
/// </summary>
public static class ControlPrimitives
{
    /// <summary>Primitive 80: blockCopy: - Creates a new BlockContext</summary>
    public class BlockCopy : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // VM EXTENSION: Accept two arguments (nargs, argStartIndex)
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            // args[0]: Block argument count (SmallInteger)
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // args[1]: argStartIndex in unified temp frame (SmallInteger)
            if (!context.Arguments[1].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP blockArgumentCount = context.Arguments[0];
            int argCount = blockArgumentCount.AsSmallInteger();

            SmalltalkOOP argStartIndexOOP = context.Arguments[1];
            int argStartIndex = argStartIndexOOP.AsSmallInteger();

            // Argument count must be non-negative
            if (argCount < 0 || argStartIndex < 0) return PrimitiveResult.Fail(context.Metadata!);

            // Get active context (receiver of blockCopy: bytecode)
            SmalltalkOOP activeContext = context.Receiver;

            // Get BlockContext class from VM metadata
            SmalltalkOOP blockContextClassOOP = context.GetBlockContextClassOOP();

            return CreateBlockContext(
                context.HeapManager,
                activeContext,
                blockContextClassOOP,
                blockArgumentCount,
                argStartIndexOOP,
                context.InstructionPointer);
        }

        /// <summary>
        /// Static helper for creating BlockContext. Called by BytecodeInterpreter directly
        /// for blockCopy: special selector handling.
        /// </summary>
        /// <param name="heapManager">Heap manager for allocation and field access</param>
        /// <param name="activeContext">The current active context (receiver)</param>
        /// <param name="blockContextClassOOP">BlockContext class OOP</param>
        /// <param name="blockArgumentCount">Number of block arguments (SmallInteger)</param>
        /// <param name="argStartIndexOOP">Start index in unified temp frame (SmallInteger)</param>
        /// <param name="currentPC">Current instruction pointer</param>
        /// <returns>PrimitiveResult with newly created BlockContext OOP</returns>
        public static PrimitiveResult CreateBlockContext(
            ISmalltalkHeap heapManager,
            SmalltalkOOP activeContext,
            SmalltalkOOP blockContextClassOOP,
            SmalltalkOOP blockArgumentCount,
            SmalltalkOOP argStartIndexOOP,
            int currentPC)
        {
            // Determine home context for lexical scoping
            SmalltalkOOP homeContext;
            SmalltalkOOP contextClassOOP = heapManager.GetClassOOP(activeContext);

            if (contextClassOOP == blockContextClassOOP)
            {
                // Nested block - inherit the outer block's home
                homeContext = heapManager.GetInstanceVariable(
                    activeContext,
                    VMConstants.BLOCKCONTEXT_HOME_INDEX
                );
            }
            else
            {
                // Top-level block - active context IS the MethodContext
                homeContext = activeContext;
            }

            // Calculate startpc (skip 3-byte LONG_JUMP)
            int blockBodyPC = currentPC + 3;
            SmalltalkOOP startPC = SmalltalkOOP.FromSmallInteger(blockBodyPC);

            // Allocate BlockContext with indexed slots for temps/stack
            int indexedSize = 32;
            SmalltalkOOP newBlockContext = heapManager.AllocateInstanceOf(blockContextClassOOP, indexedSize);

            // Initialize BlockContext fields
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX, blockArgumentCount);
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, startPC);
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContext);
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_ARG_START_INDEX, argStartIndexOOP);

            return PrimitiveResult.Ok(newBlockContext);
        }
    }

    /// <summary>Primitive 81: value* - Activates a BlockContext with 0-3 direct arguments</summary>
    public class Value : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Handles value, value:, value:value:, value:value:value:
            if (context.Arguments.Length > 3) return PrimitiveResult.Fail(context.Metadata!);

            int argumentCount = context.Arguments.Length;

            // Receiver should be a BlockContext
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP blockContext = context.Receiver;

            // Get block's expected argument count
            SmalltalkOOP blockArgumentCountOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            if (!blockArgumentCountOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int blockArgumentCount = blockArgumentCountOOP.AsSmallInteger();

            // Verify argument count matches
            if (argumentCount != blockArgumentCount) return PrimitiveResult.Fail(context.Metadata!);

            // Get argStartIndex from BlockContext extension field
            SmalltalkOOP argStartIndexOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_ARG_START_INDEX);
            int argStartIndex = argStartIndexOOP.AsSmallInteger();

            // Get home MethodContext
            SmalltalkOOP homeContext = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);

            // Store arguments in home MethodContext temp frame
            for (int i = 0; i < argumentCount; i++)
            {
                context.HeapManager.SetIndexedOOP(homeContext, argStartIndex + i, context.Arguments[i]);
            }

            // Get initialIP from blockContext
            SmalltalkOOP initialIP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);

            // Set PC to initialIP
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, initialIP);

            // Set stack pointer to argument count
            SmalltalkOOP stackPointerValue = SmalltalkOOP.FromSmallInteger(argumentCount);
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, stackPointerValue);

            // Set sender to current active context
            SmalltalkOOP activeContext = context.GetActiveContext();
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, activeContext);

            // Switch to the BlockContext
            context.SetActiveContext(blockContext);

            return PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    /// <summary>Primitive 82: valueWithArguments: - Activates a BlockContext with an Array of arguments</summary>
    public class ValueWithArguments : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver should be a BlockContext
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP arrayArg = context.Arguments[0];

            // Argument must be an Array (not SmallInteger)
            if (arrayArg.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Validate it's actually an Array class
            string className = context.HeapManager.GetClassName(arrayArg);
            if (className != "Array") return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP blockContext = context.Receiver;

            // Get block's expected argument count
            SmalltalkOOP blockArgumentCountOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            if (!blockArgumentCountOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            int blockArgumentCount = blockArgumentCountOOP.AsSmallInteger();

            // Verify array size matches block's argument count
            int arraySize = context.HeapManager.GetIndexedSize(arrayArg);
            if (arraySize != blockArgumentCount) return PrimitiveResult.Fail(context.Metadata!);

            // Get argStartIndex from BlockContext extension field
            SmalltalkOOP argStartIndexOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_ARG_START_INDEX);
            int argStartIndex = argStartIndexOOP.AsSmallInteger();

            // Get home MethodContext
            SmalltalkOOP homeContext = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);

            // Extract arguments from Array and store in home MethodContext
            for (int i = 0; i < arraySize; i++)
            {
                var argOOP = context.HeapManager.GetIndexedOOP(arrayArg, i);
                context.HeapManager.SetIndexedOOP(homeContext, argStartIndex + i, argOOP);
            }

            // Get initialIP from blockContext
            SmalltalkOOP initialIP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);

            // Set PC to initialIP
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, initialIP);

            // Set stack pointer to argument count
            SmalltalkOOP stackPointerValue = SmalltalkOOP.FromSmallInteger(arraySize);
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, stackPointerValue);

            // Set sender to current active context
            SmalltalkOOP activeContext = context.GetActiveContext();
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, activeContext);

            // Switch to the BlockContext
            context.SetActiveContext(blockContext);

            return PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    /// <summary>
    /// Primitive 83: perform:, perform:with:, perform:with:with:, perform:with:with:with:
    /// Dynamically sends a message with variable arguments.
    ///
    /// Arguments:
    /// - args[0] = selector (Symbol)
    /// - args[1..n] = message arguments (0-3 additional args)
    ///
    /// This primitive handles all perform: variants:
    /// - perform: aSymbol                              → 1 arg (selector only), send with 0 args
    /// - perform: aSymbol with: arg1                   → 2 args, send with 1 arg
    /// - perform: aSymbol with: arg1 with: arg2        → 3 args, send with 2 args
    /// - perform: aSymbol with: arg1 with: arg2 with: arg3 → 4 args, send with 3 args
    /// </summary>
    public class Perform : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Must have at least 1 argument (the selector)
            if (context.Arguments.Length < 1) return PrimitiveResult.Fail(context.Metadata!);

            // Maximum 4 arguments (selector + up to 3 message args)
            if (context.Arguments.Length > 4) return PrimitiveResult.Fail(context.Metadata!);

            // args[0] should be a Symbol (selector)
            if (context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            if (context.MessageSender == null) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP selectorOop = context.Arguments[0];

            // Extract message arguments (args[1..n])
            int messageArgCount = context.Arguments.Length - 1;
            SmalltalkOOP[] messageArgs;

            if (messageArgCount == 0)
            {
                messageArgs = Array.Empty<SmalltalkOOP>();
            }
            else
            {
                messageArgs = new SmalltalkOOP[messageArgCount];
                for (int i = 0; i < messageArgCount; i++)
                {
                    messageArgs[i] = context.Arguments[i + 1];
                }
            }

            // Send the message with extracted arguments
            SmalltalkOOP resultOop = context.MessageSender.SendMessage(
                context.Receiver, selectorOop, messageArgs);

            return PrimitiveResult.Ok(resultOop);
        }
    }

    /// <summary>
    /// Primitive 84: perform:withArguments:
    /// Dynamically sends a message using arguments from an Array.
    ///
    /// Arguments:
    /// - args[0] = selector (Symbol)
    /// - args[1] = Array containing the message arguments
    ///
    /// The elements of the Array are extracted and passed as individual arguments
    /// to the message send.
    /// </summary>
    public class PerformWithArguments : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            // args[0] = selector (Symbol)
            if (context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // args[1] = Array of arguments
            if (context.Arguments[1].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            if (context.MessageSender == null) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP selectorOop = context.Arguments[0];
            SmalltalkOOP argsArrayOop = context.Arguments[1];

            // Extract elements from the Array
            int arraySize = context.HeapManager.GetIndexedSize(argsArrayOop);
            SmalltalkOOP[] messageArgs = new SmalltalkOOP[arraySize];

            for (int i = 0; i < arraySize; i++)
            {
                messageArgs[i] = context.HeapManager.GetIndexedOOP(argsArrayOop, i);
            }

            // Send the message with extracted arguments
            SmalltalkOOP resultOop = context.MessageSender.SendMessage(
                context.Receiver, selectorOop, messageArgs);

            return PrimitiveResult.Ok(resultOop);
        }
    }

    /// <summary>Primitive 85: signal - Signals a Semaphore</summary>
    public class Signal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail(context.Metadata!);

            // Get Semaphore fields
            SmalltalkOOP firstLinkOop = context.GetFieldValue(context.Receiver, className, "firstLink");
            SmalltalkOOP excessSignalsOop = context.GetFieldValue(context.Receiver, className, "excessSignals");

            if (firstLinkOop == context.HeapManager.GetNil())
            {
                // No processes waiting - increment excessSignals
                if (!excessSignalsOop.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

                int excessCount = excessSignalsOop.AsSmallInteger();
                context.SetFieldValue(context.Receiver, className, "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount + 1));
            }
            else
            {
                // Processes are waiting - wake up the first one
                SmalltalkOOP processOop = RemoveFirstLink(context, context.Receiver, className);

                if (processOop == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                // Check if woken process has higher priority - if so, preempt immediately
                if (ShouldPreempt(context, processOop))
                {
                    SmalltalkOOP activeProcessOop = GetActiveProcess(context);
                    // Put current process on its priority queue (it will run later)
                    ResumeProcess(context, activeProcessOop);
                    // Switch directly to the higher priority process (don't add it to queue)
                    SwitchToProcess(context, activeProcessOop, processOop);
                }
                else
                {
                    // Woken process has same or lower priority - add to queue, it runs later
                    if (!ResumeProcess(context, processOop))
                        return PrimitiveResult.Fail(context.Metadata!);
                }
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 86: wait - Waits on a Semaphore</summary>
    public class Wait : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail(context.Metadata!);

            // Get excessSignals field
            SmalltalkOOP excessSignalsOop = context.GetFieldValue(context.Receiver, className, "excessSignals");

            if (!excessSignalsOop.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int excessCount = excessSignalsOop.AsSmallInteger();

            if (excessCount > 0)
            {
                // Signal available - decrement and continue
                context.SetFieldValue(context.Receiver, className, "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount - 1));
                return PrimitiveResult.Ok(context.Receiver);
            }
            else
            {
                // No signal available - must suspend active process and switch to another
                SmalltalkOOP activeProcessOop = GetActiveProcess(context);

                if (activeProcessOop == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                // Add active process to semaphore's waiting list
                AddLastLink(context, context.Receiver, className, activeProcessOop);

                // Perform context switch to next runnable process
                if (!SwitchToHighestPriorityProcess(context, activeProcessOop))
                {
                    // Context switch failed - undo the AddLastLink to leave state consistent
                    RemoveProcessFromList(context, activeProcessOop, context.Receiver);
                    context.SetFieldValue(activeProcessOop, "class:Process", "myList", context.HeapManager.GetNil());
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                // Return receiver - execution continues in the NEW process's context
                return PrimitiveResult.Ok(context.Receiver);
            }
        }
    }

    /// <summary>Primitive 87: resume - Resumes a suspended Process</summary>
    public class Resume : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail(context.Metadata!);

            // Get Process fields: myList must be nil (not already scheduled)
            SmalltalkOOP myListOop = context.GetFieldValue(context.Receiver, className, "myList");

            // Fail if already on a list
            if (myListOop != context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Check if resumed process has higher priority - if so, preempt immediately
            if (ShouldPreempt(context, context.Receiver))
            {
                SmalltalkOOP activeProcessOop = GetActiveProcess(context);
                // Put current process on its priority queue (it will run later)
                ResumeProcess(context, activeProcessOop);
                // Switch directly to the higher priority process (don't add it to queue)
                SwitchToProcess(context, activeProcessOop, context.Receiver);
            }
            else
            {
                // Resumed process has same or lower priority - add to queue, it runs later
                if (!ResumeProcess(context, context.Receiver))
                    return PrimitiveResult.Fail(context.Metadata!);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 88: suspend - Suspends a Process</summary>
    public class Suspend : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP activeProcessOop = GetActiveProcess(context);

            if (context.Receiver == activeProcessOop)
            {
                // Suspending active process - need to context switch
                // The active process is NOT on a quiescentProcessList (it's running)
                // Just save its context and switch to next runnable process
                if (!SwitchToHighestPriorityProcess(context, activeProcessOop))
                    return PrimitiveResult.Fail(context.Metadata!);

                // Clear myList to indicate suspended (not on any list)
                context.SetFieldValue(context.Receiver, className, "myList", context.HeapManager.GetNil());

                // Return nil as the suspended context - execution continues in NEW process
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            else
            {
                // Suspending a non-active process - remove it from its list
                SmalltalkOOP myListOop = context.GetFieldValue(context.Receiver, className, "myList");

                // Process must be on a list to suspend
                if (myListOop == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                // Remove process from its list
                if (!RemoveProcessFromList(context, context.Receiver, myListOop))
                    return PrimitiveResult.Fail(context.Metadata!);

                // Clear myList reference
                context.SetFieldValue(context.Receiver, className, "myList", context.HeapManager.GetNil());

                return PrimitiveResult.Ok(context.Receiver);
            }
        }
    }

    /// <summary>Primitive 89: flushCache - Clears the method lookup cache</summary>
    public class FlushCache : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.CacheManager == null) return PrimitiveResult.Fail(context.Metadata!);

            // Clear all caches
            context.CacheManager.ClearMethodCache();
            context.CacheManager.InvalidateAllRuntimeCaches();

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Switches from the current active process to the highest priority runnable process.
    /// Used by wait and suspend primitives.
    /// </summary>
    private static bool SwitchToHighestPriorityProcess(PrimitiveExecutionContext context, SmalltalkOOP currentProcessOop)
    {
        // Find the highest priority runnable process
        SmalltalkOOP nextProcessOop = FindHighestPriorityProcess(context);

        if (nextProcessOop == context.HeapManager.GetNil())
            return false; // No runnable process - shouldn't happen if background process exists

        // Remove the next process from its priority queue
        SmalltalkOOP myListOop = context.HeapManager.GetInstanceVariable(nextProcessOop, VMConstants.PROCESS_MYLIST_INDEX);
        if (myListOop != context.HeapManager.GetNil())
        {
            RemoveProcessFromList(context, nextProcessOop, myListOop);
            context.HeapManager.SetInstanceVariable(nextProcessOop, VMConstants.PROCESS_MYLIST_INDEX, context.HeapManager.GetNil());
        }

        // Perform the context switch
        return SwitchToProcess(context, currentProcessOop, nextProcessOop);
    }

    /// <summary>
    /// Performs the actual context switch from one process to another.
    /// Saves the current context to the old process and restores context from the new process.
    /// </summary>
    private static bool SwitchToProcess(PrimitiveExecutionContext context, SmalltalkOOP oldProcessOop, SmalltalkOOP newProcessOop)
    {
        if (context.Metadata == null)
            return false;

        SmalltalkOOP schedulerOop = context.Metadata.ProcessorScheduler;
        if (schedulerOop == context.HeapManager.GetNil())
            return false;

        // Save current active context to old process's suspendedContext
        SmalltalkOOP currentContext = context.GetActiveContext();
        context.HeapManager.SetInstanceVariable(oldProcessOop, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, currentContext);

        // Get new process's suspended context
        SmalltalkOOP newContext = context.HeapManager.GetInstanceVariable(newProcessOop, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

        if (newContext == context.HeapManager.GetNil())
            return false; // New process has no context to resume

        // Check if this is an unactivated BlockContext (PC is nil)
        // This happens when resuming a newly created process via [block] newProcess
        SmalltalkOOP newContextPC = context.HeapManager.GetInstanceVariable(newContext, VMConstants.BLOCKCONTEXT_PC_INDEX);
        if (newContextPC == context.HeapManager.GetNil())
        {
            // This is an unactivated BlockContext - need to activate it
            // Set PC from startPC (where the block body begins)
            SmalltalkOOP startPC = context.HeapManager.GetInstanceVariable(newContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            if (startPC == context.HeapManager.GetNil() || !startPC.IsSmallInteger)
                return false;

            context.HeapManager.SetInstanceVariable(newContext, VMConstants.BLOCKCONTEXT_PC_INDEX, startPC);

            // Initialize stack pointer to 0 (process blocks typically have no arguments)
            context.HeapManager.SetInstanceVariable(newContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));

            // Caller is nil for a new process (not being called from anywhere)
            context.HeapManager.SetInstanceVariable(newContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, context.HeapManager.GetNil());
        }

        // Update Processor's activeProcess
        context.HeapManager.SetInstanceVariable(schedulerOop, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX, newProcessOop);

        // Switch to new context - this is the critical context switch!
        context.SetActiveContext(newContext);

        return true;
    }

    /// <summary>
    /// Finds the highest priority process that is runnable (on a quiescentProcessList).
    /// Searches from priority 8 down to priority 1.
    /// </summary>
    private static SmalltalkOOP FindHighestPriorityProcess(PrimitiveExecutionContext context)
    {
        if (context.Metadata == null)
            return context.HeapManager.GetNil();

        SmalltalkOOP schedulerOop = context.Metadata.ProcessorScheduler;
        if (schedulerOop == context.HeapManager.GetNil())
            return context.HeapManager.GetNil();

        SmalltalkOOP processListsOop = context.HeapManager.GetInstanceVariable(schedulerOop, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOop == context.HeapManager.GetNil())
            return context.HeapManager.GetNil();

        // Search from highest priority (8) down to lowest (1)
        // Array indices are 0-7 for priorities 1-8
        for (int priority = 8; priority >= 1; priority--)
        {
            int index = priority - 1;
            SmalltalkOOP priorityListOop = context.HeapManager.GetIndexedOOP(processListsOop, index);

            if (priorityListOop == context.HeapManager.GetNil())
                continue;

            // Check if this priority list has any processes
            SmalltalkOOP firstLinkOop = context.GetFieldValue(priorityListOop, "class:LinkedList", "firstLink");

            if (firstLinkOop != context.HeapManager.GetNil())
            {
                return firstLinkOop; // Return first process at this priority
            }
        }

        return context.HeapManager.GetNil(); // No runnable processes found
    }

    /// <summary>
    /// Checks if a process should preempt the currently active process.
    /// Returns true if the given process has higher priority than the active process.
    /// During init expressions, the bootstrap process runs at priority 9 (higher than any
    /// user process), so no preemption will occur naturally.
    /// </summary>
    private static bool ShouldPreempt(PrimitiveExecutionContext context, SmalltalkOOP processOop)
    {
        SmalltalkOOP activeProcessOop = GetActiveProcess(context);
        if (activeProcessOop == context.HeapManager.GetNil())
            return false;

        // Get priorities
        SmalltalkOOP processPriorityOop = context.HeapManager.GetInstanceVariable(processOop, VMConstants.PROCESS_PRIORITY_INDEX);
        SmalltalkOOP activePriorityOop = context.HeapManager.GetInstanceVariable(activeProcessOop, VMConstants.PROCESS_PRIORITY_INDEX);

        if (!processPriorityOop.IsSmallInteger || !activePriorityOop.IsSmallInteger)
            return false;

        int processPriority = processPriorityOop.AsSmallInteger();
        int activePriority = activePriorityOop.AsSmallInteger();

        // Preempt if new process has STRICTLY higher priority
        return processPriority > activePriority;
    }

    private static SmalltalkOOP GetActiveProcess(PrimitiveExecutionContext context)
    {
        if (context.Metadata == null)
            throw new InvalidOperationException("VMMetadata not available");

        SmalltalkOOP schedulerOop = context.Metadata.ProcessorScheduler;

        if (schedulerOop == context.HeapManager.GetNil())
            return context.HeapManager.GetNil();

        return context.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "activeProcess");
    }

    private static SmalltalkOOP RemoveFirstLink(PrimitiveExecutionContext context, SmalltalkOOP listOop, string className)
    {
        SmalltalkOOP firstLinkOop = context.GetFieldValue(listOop, className, "firstLink");

        if (firstLinkOop == context.HeapManager.GetNil())
            return context.HeapManager.GetNil();

        SmalltalkOOP nextLinkOop = context.GetFieldValue(firstLinkOop, "class:Link", "nextLink");

        context.SetFieldValue(listOop, className, "firstLink", nextLinkOop);

        if (nextLinkOop == context.HeapManager.GetNil())
        {
            context.SetFieldValue(listOop, className, "lastLink", context.HeapManager.GetNil());
        }

        context.SetFieldValue(firstLinkOop, "class:Link", "nextLink", context.HeapManager.GetNil());

        // If this is a Process, clear its myList reference
        SmalltalkOOP linkClassOop = context.HeapManager.GetClassOOP(firstLinkOop);
        SmalltalkOOP processClassOop = context.GetGlobalObjectByName("class:Process");

        if (linkClassOop == processClassOop)
        {
            context.SetFieldValue(firstLinkOop, "class:Process", "myList", context.HeapManager.GetNil());
        }

        return firstLinkOop;
    }

    private static void AddLastLink(PrimitiveExecutionContext context, SmalltalkOOP listOop, string className, SmalltalkOOP linkOop)
    {
        SmalltalkOOP lastLinkOop = context.GetFieldValue(listOop, className, "lastLink");

        if (lastLinkOop == context.HeapManager.GetNil())
        {
            context.SetFieldValue(listOop, className, "firstLink", linkOop);
            context.SetFieldValue(listOop, className, "lastLink", linkOop);
        }
        else
        {
            context.SetFieldValue(lastLinkOop, "class:Link", "nextLink", linkOop);
            context.SetFieldValue(listOop, className, "lastLink", linkOop);
        }

        context.SetFieldValue(linkOop, "class:Link", "nextLink", context.HeapManager.GetNil());

        // Set myList if this is a Process
        SmalltalkOOP linkClassOop = context.HeapManager.GetClassOOP(linkOop);
        SmalltalkOOP processClassOop = context.GetGlobalObjectByName("class:Process");

        if (linkClassOop == processClassOop)
        {
            context.SetFieldValue(linkOop, "class:Process", "myList", listOop);
        }
    }

    private static bool ResumeProcess(PrimitiveExecutionContext context, SmalltalkOOP processOop)
    {
        SmalltalkOOP priorityOop = context.GetFieldValue(processOop, "class:Process", "priority");

        if (!priorityOop.IsSmallInteger)
            return false;

        int priority = priorityOop.AsSmallInteger();

        if (priority < 1 || priority > 100)
            return false;

        if (context.Metadata == null)
            return false;

        SmalltalkOOP schedulerOop = context.Metadata.ProcessorScheduler;

        if (schedulerOop == context.HeapManager.GetNil())
            return false;

        SmalltalkOOP processListsOop = context.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "quiescentProcessLists");

        if (processListsOop == context.HeapManager.GetNil())
            return false;

        int index = priority - 1;

        SmalltalkOOP priorityListOop = context.HeapManager.GetIndexedOOP(processListsOop, index);
        if (priorityListOop == context.HeapManager.GetNil())
            return false;

        AddLastLink(context, priorityListOop, "class:LinkedList", processOop);

        return true;
    }

    private static bool RemoveProcessFromList(PrimitiveExecutionContext context, SmalltalkOOP processOop, SmalltalkOOP listOop)
    {
        SmalltalkOOP currentOop = context.GetFieldValue(listOop, "class:LinkedList", "firstLink");
        SmalltalkOOP previousOop = context.HeapManager.GetNil();

        while (currentOop != context.HeapManager.GetNil())
        {
            if (currentOop == processOop)
            {
                SmalltalkOOP nextLinkOop = context.GetFieldValue(processOop, "class:Link", "nextLink");

                if (previousOop == context.HeapManager.GetNil())
                {
                    context.SetFieldValue(listOop, "class:LinkedList", "firstLink", nextLinkOop);

                    if (nextLinkOop == context.HeapManager.GetNil())
                    {
                        context.SetFieldValue(listOop, "class:LinkedList", "lastLink", context.HeapManager.GetNil());
                    }
                }
                else
                {
                    context.SetFieldValue(previousOop, "class:Link", "nextLink", nextLinkOop);

                    SmalltalkOOP lastLinkOop = context.GetFieldValue(listOop, "class:LinkedList", "lastLink");
                    if (lastLinkOop == processOop)
                    {
                        context.SetFieldValue(listOop, "class:LinkedList", "lastLink", previousOop);
                    }
                }

                context.SetFieldValue(processOop, "class:Link", "nextLink", context.HeapManager.GetNil());

                return true;
            }

            previousOop = currentOop;
            currentOop = context.GetFieldValue(currentOop, "class:Link", "nextLink");
        }

        return false;
    }

    #endregion
}
