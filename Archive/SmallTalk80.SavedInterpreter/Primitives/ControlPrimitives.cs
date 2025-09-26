using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Control flow primitives for blocks, message sending, and process management (primitives 80-88).
/// Based on Blue Book specification, these handle block closures, dynamic message sends,
/// and basic process/semaphore operations.
/// </summary>
public static class ControlPrimitives
{
    /// <summary>
    /// Primitive 80: blockCopy - Create a block closure.
    /// Creates a new block context that captures the current method context.
    /// </summary>
    /// <param name="receiver">The receiver (typically a CompiledMethod or Context).</param>
    /// <param name="arguments">Arguments containing the number of block arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>A new block closure, or failure if arguments are invalid.</returns>
    public static PrimitiveResult PrimitiveBlockCopy(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // blockCopy takes the number of arguments the block expects
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int argumentCount = arguments[0].SmallIntegerValue;
        if (argumentCount < 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        // Create a block closure object
        // In a real implementation, this would capture the current context
        // For now, we return a placeholder that would need VM support
        var blockClass = context.GetClass("BlockClosure");
        if (blockClass == OOP.Nil)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
        
        // Create a new block instance with fields for:
        // - home context
        // - argument count
        // - start PC
        var block = context.NewInstance(blockClass, 3);
        
        // Note: Actual implementation would need to:
        // 1. Get the current method context from the VM
        // 2. Store the home context reference
        // 3. Store the PC where block code starts
        // 4. Store the argument count
        
        return block;
    }

    // Primitive 81 is now implemented in ProcessPrimitives.cs

    /// <summary>
    /// Primitive 82: value: - Evaluate block with one argument.
    /// Executes the block closure with the provided argument.
    /// </summary>
    /// <param name="receiver">The block closure to evaluate.</param>
    /// <param name="arguments">Single argument to pass to the block.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Result of block evaluation, or failure if not a block.</returns>
    public static PrimitiveResult PrimitiveValueWithArg(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Verify receiver is a block
        var receiverObj = context.Memory.GetObject(receiver);
        if (receiverObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // In a real implementation, this would:
        // 1. Create a new block context with the argument
        // 2. Execute the block's bytecode with the argument on stack
        // 3. Return the result
        
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 83: perform: - Send message with selector.
    /// Dynamically sends a message to the receiver using the selector argument.
    /// </summary>
    /// <param name="receiver">The object to send the message to.</param>
    /// <param name="arguments">Single argument containing the selector (Symbol).</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Result of the message send, or failure if selector invalid.</returns>
    public static PrimitiveResult PrimitivePerform(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var selector = arguments[0];
        
        // Verify selector is a Symbol
        var selectorObj = context.Memory.GetObject(selector);
        if (selectorObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Check if it's a Symbol instance
        var classObj = context.Memory.GetObject(selectorObj.Header.ClassOOP);
        if (classObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Use the Send method from context to perform the message send
        return context.Send(receiver, selector, []);
    }

    /// <summary>
    /// Primitive 84: perform:with: - Send message with selector and one argument.
    /// Dynamically sends a message to the receiver with one argument.
    /// </summary>
    /// <param name="receiver">The object to send the message to.</param>
    /// <param name="arguments">Two arguments: selector and message argument.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Result of the message send, or failure if selector invalid.</returns>
    public static PrimitiveResult PrimitivePerformWith(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var selector = arguments[0];
        var messageArg = arguments[1];
        
        // Verify selector is a Symbol
        var selectorObj = context.Memory.GetObject(selector);
        if (selectorObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Use the Send method from context to perform the message send
        return context.Send(receiver, selector, [messageArg]);
    }

    // Primitives 85-88 are now implemented in ProcessPrimitives.cs

    /// <summary>
    /// Registers all control flow primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(80, "primitiveBlockCopy", PrimitiveBlockCopy);
        // Primitive 81 (value) is implemented in ProcessPrimitives.cs
        table.Register(82, "primitiveValueWithArg", PrimitiveValueWithArg);
        table.Register(83, "primitivePerform", PrimitivePerform);
        table.Register(84, "primitivePerformWith", PrimitivePerformWith);
        // Primitives 85-88 are implemented in ProcessPrimitives.cs
    }
}