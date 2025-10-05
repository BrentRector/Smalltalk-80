using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Control flow primitives for blocks, message sending, and process management (primitives 80-88).
/// Rewritten to use LayoutManagement.Layout instead of ST* structs.
/// </summary>
public static class ControlPrimitives
{
    /// <summary>
    /// Primitive 80: blockCopy: - Create a block closure.
    /// </summary>
    public static PrimitiveResult PrimitiveBlockCopy(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int argumentCount = arguments[0].AsSmallInteger();

        if (argumentCount < 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        var primitiveContext = context as InterpreterPrimitiveContext;
        if (primitiveContext?.Interpreter == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);

        var blockContext = primitiveContext.Interpreter.CreateBlockContext(argumentCount);

        return PrimitiveResult.CreateSuccess(blockContext);
    }

    /// <summary>
    /// Primitive 81: value - Activate a zero-argument block.
    /// </summary>
    public static PrimitiveResult PrimitiveValue(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        var primitiveContext = context as InterpreterPrimitiveContext;
        if (primitiveContext?.Interpreter == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);

        var result = primitiveContext.Interpreter.ActivateBlockContext(receiver, arguments);

        return PrimitiveResult.CreateSuccess(result);
    }

    /// <summary>
    /// Primitive 82: value: - Activate a one-argument block.
    /// </summary>
    public static PrimitiveResult PrimitiveValueWithArg(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        var primitiveContext = context as InterpreterPrimitiveContext;
        if (primitiveContext?.Interpreter == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);

        var result = primitiveContext.Interpreter.ActivateBlockContext(receiver, arguments);

        return PrimitiveResult.CreateSuccess(result);
    }

    /// <summary>
    /// Primitive 83: perform: - Dynamically send a message with no arguments.
    /// </summary>
    public static PrimitiveResult PrimitivePerform(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement perform using LayoutManagement
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 84: perform:with: - Dynamically send a message with one argument.
    /// </summary>
    public static PrimitiveResult PrimitivePerformWithArg(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement perform:with: using LayoutManagement
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 85: signal - Signal a semaphore.
    /// </summary>
    public static PrimitiveResult PrimitiveSignal(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement signal using LayoutManagement
        // Uses Semaphore_ExcessSignals, Semaphore_FirstLink fields
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 86: wait - Wait on a semaphore.
    /// </summary>
    public static PrimitiveResult PrimitiveWait(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement wait using LayoutManagement
        // Uses Semaphore_ExcessSignals, ProcessorScheduler_ActiveProcess fields
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 87: resume - Resume a suspended process.
    /// </summary>
    public static PrimitiveResult PrimitiveResume(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement resume using LayoutManagement
        // Uses Process_SuspendedContext, Process_Priority fields
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 88: suspend - Suspend the active process.
    /// </summary>
    public static PrimitiveResult PrimitiveSuspend(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement suspend using LayoutManagement
        // Uses ProcessorScheduler_ActiveProcess field
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Registers all control primitives with the primitive table.
    /// </summary>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(80, "blockCopy:", PrimitiveBlockCopy);
        table.Register(81, "value", PrimitiveValue);
        table.Register(82, "value:", PrimitiveValueWithArg);
        table.Register(83, "perform:", PrimitivePerform);
        table.Register(84, "perform:with:", PrimitivePerformWithArg);
        table.Register(85, "signal", PrimitiveSignal);
        table.Register(86, "wait", PrimitiveWait);
        table.Register(87, "resume", PrimitiveResume);
        table.Register(88, "suspend", PrimitiveSuspend);
    }
}
