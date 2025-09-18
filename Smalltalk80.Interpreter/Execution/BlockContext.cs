using System;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Execution context for a block closure.
/// Represents a block activation with captured lexical environment.
/// </summary>
public sealed class BlockContext : MethodContext
{
    /// <summary>
    /// The home context where the block was defined.
    /// </summary>
    public MethodContext Home { get; }
    
    /// <summary>
    /// The initial program counter for the block.
    /// </summary>
    public int InitialPC { get; }
    
    /// <summary>
    /// The number of block arguments.
    /// </summary>
    public int ArgumentCount { get; }
    
    /// <summary>
    /// Creates a new block context.
    /// </summary>
    /// <param name="home">The home context where the block was defined.</param>
    /// <param name="receiver">The receiver (usually same as home's receiver).</param>
    /// <param name="arguments">The block arguments.</param>
    /// <param name="initialPC">The starting PC for block execution.</param>
    /// <param name="tempCount">Number of temporaries needed by the block.</param>
    /// <param name="stackSize">Stack size for the block.</param>
    public BlockContext(
        MethodContext home,
        OOP receiver,
        OOP[] arguments,
        int initialPC,
        int tempCount,
        int stackSize)
        : base(null, receiver, home.Method, arguments, tempCount, stackSize)
    {
        Home = home ?? throw new ArgumentNullException(nameof(home));
        InitialPC = initialPC;
        ArgumentCount = arguments?.Length ?? 0;
        PC = initialPC;
    }
    
    /// <summary>
    /// Gets the home context for non-local returns.
    /// </summary>
    public override MethodContext GetHome() => Home;
    
    /// <summary>
    /// Checks if this block can perform a non-local return.
    /// </summary>
    public bool CanReturnNonLocal => Home.IsReturnable;
    
    /// <summary>
    /// Gets a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"BlockContext[Home={Home}, InitialPC={InitialPC}, Args={ArgumentCount}, PC={PC}, Stack={Stack.Count}]";
}

/// <summary>
/// Factory for creating block contexts from compiled block bytecode.
/// </summary>
public static class BlockContextFactory
{
    /// <summary>
    /// Creates a block context from a blockCopy: bytecode.
    /// Based on Blue Book block implementation.
    /// </summary>
    /// <param name="homeContext">The context where the block is being created.</param>
    /// <param name="argCount">Number of block arguments.</param>
    /// <param name="initialPC">Starting PC for block body.</param>
    /// <param name="tempCount">Number of block temporaries.</param>
    /// <param name="stackSize">Stack size needed by block.</param>
    /// <returns>A new block context ready for execution.</returns>
    public static BlockContext CreateBlock(
        MethodContext homeContext,
        int argCount,
        int initialPC,
        int tempCount,
        int stackSize)
    {
        ArgumentNullException.ThrowIfNull(homeContext);
        ArgumentOutOfRangeException.ThrowIfNegative(argCount);
        ArgumentOutOfRangeException.ThrowIfNegative(initialPC);
        ArgumentOutOfRangeException.ThrowIfNegative(tempCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(stackSize, 1);
        
        // Create the block context with empty arguments (will be filled on value:)
        var blockContext = new BlockContext(
            home: homeContext,
            receiver: homeContext.Receiver,
            arguments: new OOP[argCount],
            initialPC: initialPC,
            tempCount: tempCount,
            stackSize: stackSize);
        
        return blockContext;
    }
    
    /// <summary>
    /// Activates a block with the given arguments.
    /// Called when value, value:, value:value:, etc. is sent to a block.
    /// </summary>
    /// <param name="block">The block context to activate.</param>
    /// <param name="arguments">Arguments to pass to the block.</param>
    /// <exception cref="ArgumentException">If argument count doesn't match block's expected count.</exception>
    public static void ActivateBlock(BlockContext block, OOP[] arguments)
    {
        ArgumentNullException.ThrowIfNull(block);
        arguments ??= [];
        
        if (arguments.Length != block.ArgumentCount)
        {
            throw new ArgumentException(
                $"Block expects {block.ArgumentCount} arguments but received {arguments.Length}",
                nameof(arguments));
        }
        
        // Copy arguments into the block's argument array
        for (int i = 0; i < arguments.Length; i++)
        {
            block.Arguments[i] = arguments[i];
        }
        
        // Reset PC to initial position
        block.PC = block.InitialPC;
        
        // Clear stack for fresh execution
        block.Stack.Clear();
    }
}

/// <summary>
/// Exception thrown when a block performs a non-local return.
/// This is caught by the VM to unwind to the home context.
/// </summary>
public sealed class NonLocalReturnException : Exception
{
    /// <summary>
    /// The home context to return to.
    /// </summary>
    public MethodContext HomeContext { get; }
    
    /// <summary>
    /// The value to return.
    /// </summary>
    public OOP ReturnValue { get; }
    
    /// <summary>
    /// Creates a new non-local return exception.
    /// </summary>
    public NonLocalReturnException(MethodContext homeContext, OOP returnValue)
        : base("Non-local return from block")
    {
        HomeContext = homeContext ?? throw new ArgumentNullException(nameof(homeContext));
        ReturnValue = returnValue;
    }
}