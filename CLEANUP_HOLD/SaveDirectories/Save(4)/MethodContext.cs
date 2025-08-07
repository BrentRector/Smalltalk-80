using System.Collections.Generic;

/// <summary>
///     Represents the execution of a Smalltalk compiled method in response
///     to a message send.
/// </summary>
public sealed class MethodContext : Context
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MethodContext"/> class.
    /// </summary>
    public MethodContext() { }

    /// <summary>
    ///     Creates a new <see cref="MethodContext"/> configured for invocation of
    ///     <paramref name="method"/> on the specified <paramref name="receiver"/>.
    /// </summary>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="arguments">Arguments to the method.</param>
    /// <param name="sender">The sending context.</param>
    /// <returns>A fully initialized method context.</returns>
    public static MethodContext Create(
        CompiledMethod method,
        SmalltalkObject receiver,
        object?[] arguments,
        Context? sender)
    {
        return new MethodContext
        {
            Sender = sender,
            Method = method,
            Receiver = receiver,
            Arguments = arguments,
            Temporaries = new object?[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
    }
}
