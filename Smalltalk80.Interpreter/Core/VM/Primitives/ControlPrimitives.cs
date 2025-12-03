using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Scheduling;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Control flow primitives using new IPrimitive interface.
/// Implements blocks (80-82), perform (83-84), process management (85-88), and cache (89).
///
/// Process management primitives (85-88) use IProcessScheduler for all process operations,
/// ensuring consistency with the VM's process scheduling implementation.
/// </summary>
public static class ControlPrimitives
{
    /// <summary>Primitive 80: blockCopy: - Creates a new BlockContext</summary>
    /// <remarks>
    /// BLUE BOOK COMPLIANT: Only accepts block argument count.
    /// Block arguments are stored via STORE_TEMP bytecodes at block body start.
    /// </remarks>
    public class BlockCopy : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Blue Book: Accept one argument (nargs)
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            // args[0]: Block argument count (SmallInteger)
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP blockArgumentCount = context.Arguments[0];
            int argCount = blockArgumentCount.AsSmallInteger();

            // Argument count must be non-negative
            if (argCount < 0) return PrimitiveResult.Fail(context.Metadata!);

            // Get active context (receiver of blockCopy: bytecode)
            SmalltalkOOP activeContext = context.Receiver;

            // Get BlockContext class from VM metadata
            SmalltalkOOP blockContextClassOOP = context.GetBlockContextClassOOP();

            var result = CreateBlockContext(
                context.HeapManager,
                activeContext,
                blockContextClassOOP,
                blockArgumentCount,
                context.InstructionPointer);

            // CRITICAL: Mark the home context as having created a block.
            // This prevents the home MethodContext from being pooled.
            if (result.Success && context.ContextManager != null)
            {
                var homeContextOOP = context.HeapManager.GetInstanceVariable(result.Value, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                context.ContextManager.MarkContextCreatedBlock(homeContextOOP);
            }

            return result;
        }

        /// <summary>
        /// Static helper for creating BlockContext. Called by BytecodeInterpreter directly
        /// for blockCopy: special selector handling.
        /// </summary>
        public static PrimitiveResult CreateBlockContext(
            ISmalltalkHeap heapManager,
            SmalltalkOOP activeContext,
            SmalltalkOOP blockContextClassOOP,
            SmalltalkOOP blockArgumentCount,
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

            // Calculate startpc (skip 2-byte Blue Book LONG_JUMP: opcode + extension byte)
            int blockBodyPC = currentPC + 2;
            SmalltalkOOP startPC = SmalltalkOOP.FromSmallInteger(blockBodyPC);

            // Allocate BlockContext with indexed slots for temps/stack
            int indexedSize = 32;
            SmalltalkOOP newBlockContext = heapManager.AllocateInstanceOf(blockContextClassOOP, indexedSize);

            // Initialize BlockContext fields (Blue Book standard fields only)
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, heapManager.GetNil());
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX, blockArgumentCount);
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, startPC);
            heapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContext);

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

            // Get initialIP from blockContext
            SmalltalkOOP initialIP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);

            // Set PC to initialIP
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, initialIP);

            // BLUE BOOK: Push arguments onto BlockContext's stack (in order)
            for (int i = 0; i < argumentCount; i++)
            {
                context.HeapManager.SetIndexedOOP(blockContext, i, context.Arguments[i]);
            }

            // Set stack pointer to argument count (points to next free slot after arguments)
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

            // Get initialIP from blockContext
            SmalltalkOOP initialIP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);

            // Set PC to initialIP
            context.HeapManager.SetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, initialIP);

            // BLUE BOOK: Push arguments onto BlockContext's stack (in order)
            for (int i = 0; i < arraySize; i++)
            {
                var argOOP = context.HeapManager.GetIndexedOOP(arrayArg, i);
                context.HeapManager.SetIndexedOOP(blockContext, i, argOOP);
            }

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

    /// <summary>Primitive 83: perform: variants - Dynamically sends a message</summary>
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

    /// <summary>Primitive 84: perform:withArguments: - Dynamically sends a message with Array args</summary>
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

    /// <summary>
    /// Primitive 85: signal - Signals a Semaphore.
    /// Uses IProcessScheduler for all process scheduling operations.
    /// </summary>
    public class Signal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var scheduler = context.ProcessScheduler;
            if (scheduler == null) return PrimitiveResult.Fail(context.Metadata!);

            // Use the unified signaling method from IProcessScheduler
            // This handles: wake first waiter, preemption check, or increment excessSignals
            scheduler.SignalSemaphoreWithPreemption(context.Receiver);

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>
    /// Primitive 86: wait - Waits on a Semaphore.
    /// Uses IProcessScheduler for all process scheduling operations.
    /// </summary>
    public class Wait : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var scheduler = context.ProcessScheduler;
            if (scheduler == null) return PrimitiveResult.Fail(context.Metadata!);

            // Get excessSignals field
            SmalltalkOOP excessSignalsOop = context.HeapManager.GetInstanceVariable(
                context.Receiver, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX);

            if (!excessSignalsOop.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int excessCount = excessSignalsOop.AsSmallInteger();

            if (excessCount > 0)
            {
                // Signal available - decrement and continue
                context.HeapManager.SetInstanceVariable(context.Receiver, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX,
                    SmalltalkOOP.FromSmallInteger(excessCount - 1));
                return PrimitiveResult.Ok(context.Receiver);
            }
            else
            {
                // No signal available - must suspend active process and switch to another
                SmalltalkOOP activeProcessOop = scheduler.GetActiveProcess();

                if (activeProcessOop == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                // Push result onto the suspending process's stack BEFORE switching
                // When this process resumes later, execution continues after the wait call
                if (context.StackManager != null)
                {
                    context.StackManager.Push(context.Receiver);
                }

                // CRITICAL: Save current context to the process's suspendedContext BEFORE
                // adding to wait list. When this process is woken later by a signal,
                // it needs a valid suspendedContext to resume from.
                context.HeapManager.SetInstanceVariable(activeProcessOop,
                    VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, context.GetActiveContext());

                // Add process to semaphore wait list and switch to next runnable process
                if (!scheduler.WaitOnSemaphore(context.Receiver, activeProcessOop))
                {
                    // Context switch failed - undo the push
                    if (context.StackManager != null)
                        context.StackManager.Pop();
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                // Result already pushed onto suspended process's stack
                return PrimitiveResult.Ok(context.Receiver);
            }
        }
    }

    /// <summary>
    /// Primitive 87: resume - Resumes a suspended Process.
    /// Uses IProcessScheduler for all process scheduling operations.
    /// </summary>
    public class Resume : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var scheduler = context.ProcessScheduler;
            if (scheduler == null) return PrimitiveResult.Fail(context.Metadata!);

            // Process must not already be on a list
            SmalltalkOOP myListOop = scheduler.GetProcessMyList(context.Receiver);
            if (myListOop != context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Check if resumed process has higher priority - if so, preempt
            if (scheduler.ShouldPreempt(context.Receiver))
            {
                // Preempt: suspend current process and switch to resumed process
                scheduler.SuspendActiveProcess(returnToQueue: true);
                scheduler.ActivateProcess(context.Receiver, "resume");
            }
            else
            {
                // No preemption - add to ready queue
                int priority = scheduler.GetProcessPriority(context.Receiver);
                scheduler.AddProcessToReadyQueue(context.Receiver, priority);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>
    /// Primitive 88: suspend - Suspends a Process.
    /// Uses IProcessScheduler for all process scheduling operations.
    /// </summary>
    public class Suspend : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var scheduler = context.ProcessScheduler;
            if (scheduler == null) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP activeProcessOop = scheduler.GetActiveProcess();

            if (context.Receiver == activeProcessOop)
            {
                // Suspending active process - save context and switch to next runnable
                // Push result (nil) onto the suspending process's stack BEFORE switching
                if (context.StackManager != null)
                {
                    context.StackManager.Push(context.HeapManager.GetNil());
                }

                // Suspend without returning to queue (process is suspended, not ready)
                scheduler.SuspendActiveProcess(returnToQueue: false);

                if (!scheduler.SwitchToNextRunnableProcess(activeProcessOop))
                {
                    // Context switch failed - undo the push
                    if (context.StackManager != null)
                        context.StackManager.Pop();
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                // Clear myList to indicate suspended (not on any list)
                scheduler.SetProcessMyList(context.Receiver, context.HeapManager.GetNil());

                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            else
            {
                // Suspending a non-active process - remove it from its list
                SmalltalkOOP myListOop = scheduler.GetProcessMyList(context.Receiver);

                // Process must be on a list to suspend
                if (myListOop == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                // Remove process from its list
                scheduler.RemoveProcessFromList(context.Receiver, myListOop);

                // Clear myList reference
                scheduler.SetProcessMyList(context.Receiver, context.HeapManager.GetNil());

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
}
