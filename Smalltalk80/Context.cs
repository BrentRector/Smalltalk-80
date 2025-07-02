using System;
using System.Collections.Generic;

/// <summary>
///     Represents an execution context for a Smalltalk method.
/// </summary>
public class Context
{
    /// <summary>
    ///     Gets or sets the sending context. This will be <c>null</c> for the
    ///     top-level context.
    /// </summary>
    public Context? Sender { get; init; }

    /// <summary>
    ///     Gets the compiled method being executed.
    /// </summary>
    public required CompiledMethod Method { get; init; }

    /// <summary>
    ///     Gets the receiver object for the method.
    /// </summary>
    public SmalltalkObject Receiver { get; set; } = null!;

    /// <summary>
    ///     Gets the array of argument values passed to the method.
    /// </summary>
    public required object?[] Arguments { get; init; } = Array.Empty<object?>();

    /// <summary>
    ///     Gets the storage for temporary variables defined by the method.
    /// </summary>
    public required object[] Temporaries { get; init; }

    /// <summary>
    ///     Gets the evaluation stack used while executing the method.
    /// </summary>
    public required Stack<object?> Stack { get; init; }

    /// <summary>
    ///     Gets or sets the program counter within the method.
    /// </summary>
    public int PC { get; set; }

    /// <summary>
    ///     Creates a new execution context for sending a message.
    /// </summary>
    /// <param name="selector">The selector of the method to invoke.</param>
    /// <param name="argCount">The number of arguments being sent.</param>
    /// <param name="sender">The calling context.</param>
    /// <returns>A newly initialized <see cref="Context"/>.</returns>
    public static Context CreateFor(Symbol selector, int argCount, Context sender)
    {
        var cls = sender.Receiver.ClassPointer ??
                  throw new InvalidOperationException("Receiver has no class pointer");
        var method = cls.Lookup(selector) ??
                     throw new InvalidOperationException($"Method {selector} not found");

        var args = new object?[argCount];
        for (int i = argCount - 1; i >= 0; i--) {
            args[i] = sender.Stack.Pop();
        }

        return new MethodContext {
            Sender = sender,
            Method = method,
            Receiver = sender.Receiver,
            Arguments = args,
            Temporaries = new object[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
    }
}