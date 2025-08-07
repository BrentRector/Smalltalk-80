using System;

/// <summary>
/// Represents a Smalltalk message send including receiver, selector and argument values.
/// </summary>
public sealed class Message
{
    /// <summary>
    ///     Gets the message selector.
    /// </summary>
    public required Symbol Selector { get; init; }

    /// <summary>
    ///     Gets the receiver to which the message will be sent.
    /// </summary>
    public required object? Receiver { get; init; }

    /// <summary>
    ///     Gets the arguments supplied with the message.
    /// </summary>
    public required object?[] Arguments { get; init; } = Array.Empty<object?>();

    /// <summary>
    ///     Gets or sets a value indicating whether message lookup should start in
    ///     the superclass of the method that sent the message.
    /// </summary>
    public bool SuperSend { get; init; }
}
