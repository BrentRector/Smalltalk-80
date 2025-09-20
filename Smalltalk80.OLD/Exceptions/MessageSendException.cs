using System;

namespace Smalltalk80.Exceptions;

/// <summary>
/// Exception thrown when message sending encounters an error.
/// </summary>
public sealed class MessageSendException : SmalltalkRuntimeException
{
    /// <summary>
    /// Gets the selector that could not be sent.
    /// </summary>
    public Symbol? Selector { get; }

    /// <summary>
    /// Gets the receiver that could not understand the message.
    /// </summary>
    public object? Receiver { get; }

    /// <summary>
    /// Gets the number of arguments provided with the message.
    /// </summary>
    public int ArgumentCount { get; }

    /// <summary>
    /// Initializes a new instance of the MessageSendException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="selector">The selector that could not be sent.</param>
    /// <param name="receiver">The receiver that could not understand the message.</param>
    /// <param name="argumentCount">The number of arguments provided.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public MessageSendException(string message, Symbol? selector = null, object? receiver = null, int argumentCount = 0, Context? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
        Selector = selector;
        Receiver = receiver;
        ArgumentCount = argumentCount;
    }

    /// <summary>
    /// Creates a MessageSendException for a method not found error.
    /// </summary>
    /// <param name="selector">The selector that was not found.</param>
    /// <param name="receiver">The receiver that does not understand the message.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new MessageSendException instance.</returns>
    public static MessageSendException MethodNotFound(Symbol selector, object? receiver, Context? context = null)
    {
        var receiverType = receiver?.GetType().Name ?? "null";
        return new MessageSendException(
            $"Method '{selector}' not found on {receiverType}",
            selector,
            receiver,
            context: context);
    }

    /// <summary>
    /// Creates a MessageSendException for an invalid receiver error.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="receiver">The invalid receiver.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new MessageSendException instance.</returns>
    public static MessageSendException InvalidReceiver(Symbol selector, object? receiver, Context? context = null)
    {
        var receiverType = receiver?.GetType().Name ?? "null";
        return new MessageSendException(
            $"Cannot send '{selector}' to {receiverType}",
            selector,
            receiver,
            context: context);
    }

    /// <summary>
    /// Creates a MessageSendException for incorrect argument count.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="expected">The expected number of arguments.</param>
    /// <param name="actual">The actual number of arguments provided.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new MessageSendException instance.</returns>
    public static MessageSendException WrongArgumentCount(Symbol selector, int expected, int actual, Context? context = null)
    {
        return new MessageSendException(
            $"Wrong number of arguments for '{selector}': expected {expected}, got {actual}",
            selector,
            argumentCount: actual,
            context: context);
    }

    /// <inheritdoc />
    public override string DetailedMessage
    {
        get
        {
            var details = base.DetailedMessage;
            
            if (Selector != null)
            {
                details += $"\n  Selector: {Selector}";
            }
            
            if (Receiver != null)
            {
                details += $"\n  Receiver: {Receiver.GetType().Name}";
            }
            
            details += $"\n  Arguments: {ArgumentCount}";
            
            return details;
        }
    }
}