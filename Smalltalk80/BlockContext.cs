using System;
using System.Collections.Generic;

/// <summary>
///     An execution context used for evaluating Smalltalk blocks.
/// </summary>
public sealed class BlockContext : Context
{
    /// <summary>
    ///     Gets the context in which the block was defined.
    /// </summary>
    public required Context HomeContext { get; init; }

    /// <summary>
    ///     Gets the bytecode offset of the first instruction in the block.
    /// </summary>
    public int StartPC { get; init; }

    /// <summary>
    ///     Gets the number of arguments expected by the block.
    /// </summary>
    public int ArgumentCount { get; init; }

    /// <summary>
    ///     Creates a new block context.
    /// </summary>
    /// <param name="homeContext">The context that defined the block.</param>
    /// <param name="startPC">The bytecode position where the block begins.</param>
    /// <param name="argCount">The number of arguments accepted by the block.</param>
    /// <param name="args">The argument values.</param>
    /// <returns>A fully initialized <see cref="BlockContext"/>.</returns>
    public static BlockContext Create(Context homeContext, int startPC, int argCount, object[] args)
    {
        return new BlockContext
        {
            HomeContext = homeContext,
            StartPC = startPC,
            Sender = null,
            Method = homeContext.Method,
            Receiver = homeContext.Receiver,
            Arguments = args,
            ArgumentCount = argCount,
            Temporaries = new object[homeContext.Method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = startPC
        };
    }
}