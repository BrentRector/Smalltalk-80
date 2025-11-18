using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

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
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();

            // args[0]: Block argument count (SmallInteger)
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            // args[1]: argStartIndex in unified temp frame (SmallInteger)
            if (!context.Arguments[1].IsSmallInteger) return PrimitiveResult.Fail();

            SmalltalkOOP blockArgumentCount = context.Arguments[0];
            int argCount = blockArgumentCount.AsSmallInteger();

            SmalltalkOOP argStartIndexOOP = context.Arguments[1];
            int argStartIndex = argStartIndexOOP.AsSmallInteger();

            // Argument count must be non-negative
            if (argCount < 0 || argStartIndex < 0) return PrimitiveResult.Fail();

            // Get active context (receiver of blockCopy: bytecode)
            SmalltalkOOP activeContext = context.Receiver;

            // Get BlockContext class from VM metadata
            SmalltalkOOP blockContextClassOOP = context.GetBlockContextClassOOP();

            // Determine home context for lexical scoping
            SmalltalkOOP homeContext;
            SmalltalkOOP contextClassOOP = context.HeapManager.GetClassOOP(activeContext);

            if (contextClassOOP == context.GetBlockContextClassOOP())
            {
                // Nested block - inherit the outer block's home
                homeContext = context.HeapManager.GetInstanceVariable(
                    activeContext,
                    VMConstants.BLOCKCONTEXT_HOME_INDEX
                );
            }
            else
            {
                // Top-level block - active context IS the MethodContext
                homeContext = activeContext;
            }

            // Get current PC and calculate startpc
            int currentPC = context.InstructionPointer;
            int blockBodyPC = currentPC + 3;  // Skip 3-byte LONG_JUMP
            SmalltalkOOP startPC = SmalltalkOOP.FromSmallInteger(blockBodyPC);

            // Allocate BlockContext with indexed slots for temps/stack
            int indexedSize = 32;
            SmalltalkOOP newBlockContext = context.HeapManager.AllocateInstanceOf(blockContextClassOOP, indexedSize);

            // Initialize BlockContext fields
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, SpecialObjectsArray.Instance.Nil);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_PC_INDEX, SpecialObjectsArray.Instance.Nil);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SpecialObjectsArray.Instance.Nil);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX, blockArgumentCount);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, startPC);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContext);
            context.HeapManager.SetInstanceVariable(newBlockContext, VMConstants.BLOCKCONTEXT_ARG_START_INDEX, argStartIndexOOP);

            return PrimitiveResult.Ok(newBlockContext);
        }
    }

    /// <summary>Primitive 81: value* - Activates a BlockContext with 0-3 direct arguments</summary>
    public class Value : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Handles value, value:, value:value:, value:value:value:
            if (context.Arguments.Length > 3) return PrimitiveResult.Fail();

            int argumentCount = context.Arguments.Length;

            // Receiver should be a BlockContext
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            SmalltalkOOP blockContext = context.Receiver;

            // Get block's expected argument count
            SmalltalkOOP blockArgumentCountOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            if (!blockArgumentCountOOP.IsSmallInteger) return PrimitiveResult.Fail();

            int blockArgumentCount = blockArgumentCountOOP.AsSmallInteger();

            // Verify argument count matches
            if (argumentCount != blockArgumentCount) return PrimitiveResult.Fail();

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

            return PrimitiveResult.Ok(SpecialObjectsArray.Instance.Nil);
        }
    }

    /// <summary>Primitive 82: valueWithArguments: - Activates a BlockContext with an Array of arguments</summary>
    public class ValueWithArguments : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            // Receiver should be a BlockContext
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            SmalltalkOOP arrayArg = context.Arguments[0];

            // Argument must be an Array (not SmallInteger)
            if (arrayArg.IsSmallInteger) return PrimitiveResult.Fail();

            // Validate it's actually an Array class
            string className = context.HeapManager.GetClassName(arrayArg);
            if (className != "Array") return PrimitiveResult.Fail();

            SmalltalkOOP blockContext = context.Receiver;

            // Get block's expected argument count
            SmalltalkOOP blockArgumentCountOOP = context.HeapManager.GetInstanceVariable(blockContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            if (!blockArgumentCountOOP.IsSmallInteger) return PrimitiveResult.Fail();
            int blockArgumentCount = blockArgumentCountOOP.AsSmallInteger();

            // Verify array size matches block's argument count
            int arraySize = context.HeapManager.GetIndexedSize(arrayArg);
            if (arraySize != blockArgumentCount) return PrimitiveResult.Fail();

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

            return PrimitiveResult.Ok(SpecialObjectsArray.Instance.Nil);
        }
    }

    /// <summary>Primitive 83: perform: - Dynamically sends a message with no arguments</summary>
    public class Perform : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            // args[0] should be a Symbol
            if (context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            if (context.MessageSender == null) return PrimitiveResult.Fail();

            SmalltalkOOP selectorOop = context.Arguments[0];

            // Send the message with empty arguments
            SmalltalkOOP resultOop = context.MessageSender.SendMessage(
                context.Receiver, selectorOop, Array.Empty<SmalltalkOOP>());

            return PrimitiveResult.Ok(resultOop);
        }
    }

    /// <summary>Primitive 84: perform:with: - Dynamically sends a message with one argument</summary>
    public class PerformWith : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();

            // args[0] = selector (Symbol), args[1] = argument
            if (context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            if (context.MessageSender == null) return PrimitiveResult.Fail();

            SmalltalkOOP selectorOop = context.Arguments[0];

            // Send the message with one argument
            SmalltalkOOP resultOop = context.MessageSender.SendMessage(
                context.Receiver, selectorOop, new[] { context.Arguments[1] });

            return PrimitiveResult.Ok(resultOop);
        }
    }

    /// <summary>Primitive 85: signal - Signals a Semaphore</summary>
    public class Signal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail();

            // Get Semaphore fields
            SmalltalkOOP firstLinkOop = context.GetFieldValue(context.Receiver, className, "firstLink");
            SmalltalkOOP excessSignalsOop = context.GetFieldValue(context.Receiver, className, "excessSignals");

            if (firstLinkOop == SpecialObjectsArray.Instance.Nil)
            {
                // No processes waiting - increment excessSignals
                if (!excessSignalsOop.IsSmallInteger) return PrimitiveResult.Fail();

                int excessCount = excessSignalsOop.AsSmallInteger();
                context.SetFieldValue(context.Receiver, className, "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount + 1));
            }
            else
            {
                // Processes are waiting - wake up the first one
                SmalltalkOOP processOop = RemoveFirstLink(context, context.Receiver, className);

                if (processOop == SpecialObjectsArray.Instance.Nil)
                    return PrimitiveResult.Fail();

                if (!ResumeProcess(context, processOop))
                    return PrimitiveResult.Fail();
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 86: wait - Waits on a Semaphore</summary>
    public class Wait : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Receiver must be a Semaphore
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail();

            // Get excessSignals field
            SmalltalkOOP excessSignalsOop = context.GetFieldValue(context.Receiver, className, "excessSignals");

            if (!excessSignalsOop.IsSmallInteger) return PrimitiveResult.Fail();

            int excessCount = excessSignalsOop.AsSmallInteger();

            if (excessCount > 0)
            {
                // Signal available - decrement and continue
                context.SetFieldValue(context.Receiver, className, "excessSignals", SmalltalkOOP.FromSmallInteger(excessCount - 1));
                return PrimitiveResult.Ok(context.Receiver);
            }
            else
            {
                // No signal available - suspend active process
                SmalltalkOOP activeProcessOop = GetActiveProcess(context);

                if (activeProcessOop == SpecialObjectsArray.Instance.Nil)
                    return PrimitiveResult.Fail();

                // Add active process to semaphore's waiting list
                AddLastLink(context, context.Receiver, className, activeProcessOop);

                // Actual suspension requires context switch - fail to let Smalltalk handle it
                return PrimitiveResult.Fail();
            }
        }
    }

    /// <summary>Primitive 87: resume - Resumes a suspended Process</summary>
    public class Resume : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail();

            // Get Process fields: myList must be nil (not already scheduled)
            SmalltalkOOP myListOop = context.GetFieldValue(context.Receiver, className, "myList");

            // Fail if already on a list
            if (myListOop != SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            // Resume the process
            if (!ResumeProcess(context, context.Receiver))
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 88: suspend - Suspends a Process</summary>
    public class Suspend : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Receiver must be a Process
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            string? className = context.GetClassNameForObject(context.Receiver);
            if (className == null) return PrimitiveResult.Fail();

            SmalltalkOOP activeProcessOop = GetActiveProcess(context);

            if (context.Receiver == activeProcessOop)
            {
                // Suspending active process requires context switch - fail
                return PrimitiveResult.Fail();
            }
            else
            {
                // Suspending a non-active process - remove it from its list
                SmalltalkOOP myListOop = context.GetFieldValue(context.Receiver, className, "myList");

                // Process must be on a list to suspend
                if (myListOop == SpecialObjectsArray.Instance.Nil)
                    return PrimitiveResult.Fail();

                // Remove process from its list
                if (!RemoveProcessFromList(context, context.Receiver, myListOop))
                    return PrimitiveResult.Fail();

                // Clear myList reference
                context.SetFieldValue(context.Receiver, className, "myList", SpecialObjectsArray.Instance.Nil);

                return PrimitiveResult.Ok(context.Receiver);
            }
        }
    }

    /// <summary>Primitive 89: flushCache - Clears the method lookup cache</summary>
    public class FlushCache : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            if (context.CacheManager == null) return PrimitiveResult.Fail();

            // Clear all caches
            context.CacheManager.ClearMethodCache();
            context.CacheManager.InvalidateAllRuntimeCaches();

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    #region Helper Methods

    private static SmalltalkOOP GetActiveProcess(PrimitiveExecutionContext context)
    {
        SmalltalkOOP schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler;

        if (schedulerOop == SpecialObjectsArray.Instance.Nil)
            return SpecialObjectsArray.Instance.Nil;

        return context.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "activeProcess");
    }

    private static SmalltalkOOP RemoveFirstLink(PrimitiveExecutionContext context, SmalltalkOOP listOop, string className)
    {
        SmalltalkOOP firstLinkOop = context.GetFieldValue(listOop, className, "firstLink");

        if (firstLinkOop == SpecialObjectsArray.Instance.Nil)
            return SpecialObjectsArray.Instance.Nil;

        SmalltalkOOP nextLinkOop = context.GetFieldValue(firstLinkOop, "class:Link", "nextLink");

        context.SetFieldValue(listOop, className, "firstLink", nextLinkOop);

        if (nextLinkOop == SpecialObjectsArray.Instance.Nil)
        {
            context.SetFieldValue(listOop, className, "lastLink", SpecialObjectsArray.Instance.Nil);
        }

        context.SetFieldValue(firstLinkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

        // If this is a Process, clear its myList reference
        SmalltalkOOP linkClassOop = context.HeapManager.GetClassOOP(firstLinkOop);
        SmalltalkOOP processClassOop = context.GetGlobalObjectByName("class:Process");

        if (linkClassOop == processClassOop)
        {
            context.SetFieldValue(firstLinkOop, "class:Process", "myList", SpecialObjectsArray.Instance.Nil);
        }

        return firstLinkOop;
    }

    private static void AddLastLink(PrimitiveExecutionContext context, SmalltalkOOP listOop, string className, SmalltalkOOP linkOop)
    {
        SmalltalkOOP lastLinkOop = context.GetFieldValue(listOop, className, "lastLink");

        if (lastLinkOop == SpecialObjectsArray.Instance.Nil)
        {
            context.SetFieldValue(listOop, className, "firstLink", linkOop);
            context.SetFieldValue(listOop, className, "lastLink", linkOop);
        }
        else
        {
            context.SetFieldValue(lastLinkOop, "class:Link", "nextLink", linkOop);
            context.SetFieldValue(listOop, className, "lastLink", linkOop);
        }

        context.SetFieldValue(linkOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

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

        SmalltalkOOP schedulerOop = SpecialObjectsArray.Instance.ProcessorScheduler;

        if (schedulerOop == SpecialObjectsArray.Instance.Nil)
            return false;

        SmalltalkOOP processListsOop = context.GetFieldValue(schedulerOop, "class:ProcessorScheduler", "quiescentProcessLists");

        if (processListsOop == SpecialObjectsArray.Instance.Nil)
            return false;

        int index = priority - 1;

        SmalltalkOOP priorityListOop = context.HeapManager.GetIndexedOOP(processListsOop, index);
        if (priorityListOop == SpecialObjectsArray.Instance.Nil)
            return false;

        AddLastLink(context, priorityListOop, "class:LinkedList", processOop);

        return true;
    }

    private static bool RemoveProcessFromList(PrimitiveExecutionContext context, SmalltalkOOP processOop, SmalltalkOOP listOop)
    {
        SmalltalkOOP currentOop = context.GetFieldValue(listOop, "class:LinkedList", "firstLink");
        SmalltalkOOP previousOop = SpecialObjectsArray.Instance.Nil;

        while (currentOop != SpecialObjectsArray.Instance.Nil)
        {
            if (currentOop == processOop)
            {
                SmalltalkOOP nextLinkOop = context.GetFieldValue(processOop, "class:Link", "nextLink");

                if (previousOop == SpecialObjectsArray.Instance.Nil)
                {
                    context.SetFieldValue(listOop, "class:LinkedList", "firstLink", nextLinkOop);

                    if (nextLinkOop == SpecialObjectsArray.Instance.Nil)
                    {
                        context.SetFieldValue(listOop, "class:LinkedList", "lastLink", SpecialObjectsArray.Instance.Nil);
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

                context.SetFieldValue(processOop, "class:Link", "nextLink", SpecialObjectsArray.Instance.Nil);

                return true;
            }

            previousOop = currentOop;
            currentOop = context.GetFieldValue(currentOop, "class:Link", "nextLink");
        }

        return false;
    }

    #endregion
}
