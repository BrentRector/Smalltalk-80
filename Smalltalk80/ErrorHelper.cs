using System;
using Smalltalk80.Exceptions;

/// <summary>
/// Provides standardized error handling and exception creation methods.
/// Centralizes error message formatting and ensures consistent error reporting.
/// </summary>
public static class ErrorHelper
{
    /// <summary>
    /// Validates that a parameter is not null and throws a standardized exception if it is.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The name of the parameter being checked.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    /// <summary>
    /// Validates that an index is within valid bounds and throws a standardized exception if not.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="length">The maximum valid length.</param>
    /// <param name="parameterName">The name of the parameter being checked.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of bounds.</exception>
    public static void ThrowIfOutOfBounds(int index, int length, string parameterName)
    {
        if (index < 0 || index >= length)
        {
            throw new ArgumentOutOfRangeException(parameterName, index, 
                $"Index must be between 0 and {length - 1}, but was {index}");
        }
    }

    /// <summary>
    /// Validates that a collection has enough elements and throws a standardized exception if not.
    /// </summary>
    /// <param name="actualLength">The actual length of the collection.</param>
    /// <param name="requiredLength">The required minimum length.</param>
    /// <param name="collectionName">The name of the collection being checked.</param>
    /// <exception cref="InvalidOperationException">Thrown when collection is too short.</exception>
    public static void ThrowIfTooShort(int actualLength, int requiredLength, string collectionName)
    {
        if (actualLength < requiredLength)
        {
            throw new InvalidOperationException(
                $"{collectionName} length is {actualLength}, but at least {requiredLength} elements are required");
        }
    }

    /// <summary>
    /// Creates a BytecodeException for an unknown bytecode.
    /// </summary>
    /// <param name="bytecode">The unknown bytecode.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new BytecodeException.</returns>
    public static BytecodeException UnknownBytecode(byte bytecode, Context? context = null)
    {
        return BytecodeException.UnknownBytecode(bytecode, context);
    }

    /// <summary>
    /// Creates a BytecodeException for an unimplemented bytecode.
    /// </summary>
    /// <param name="bytecode">The unimplemented bytecode.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new BytecodeException.</returns>
    public static BytecodeException NotImplemented(byte bytecode, string operation, Context? context = null)
    {
        return BytecodeException.NotImplemented(bytecode, operation, context);
    }

    /// <summary>
    /// Creates a MessageSendException for a method not found error.
    /// </summary>
    /// <param name="selector">The selector that was not found.</param>
    /// <param name="receiver">The receiver that does not understand the message.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new MessageSendException.</returns>
    public static MessageSendException MethodNotFound(Symbol selector, object? receiver, Context? context = null)
    {
        return MessageSendException.MethodNotFound(selector, receiver, context);
    }

    /// <summary>
    /// Creates a MessageSendException for an invalid receiver error.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="receiver">The invalid receiver.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new MessageSendException.</returns>
    public static MessageSendException InvalidReceiver(Symbol selector, object? receiver, Context? context = null)
    {
        return MessageSendException.InvalidReceiver(selector, receiver, context);
    }

    /// <summary>
    /// Creates a MessageSendException for incorrect argument count.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="expected">The expected number of arguments.</param>
    /// <param name="actual">The actual number of arguments provided.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new MessageSendException.</returns>
    public static MessageSendException WrongArgumentCount(Symbol selector, int expected, int actual, Context? context = null)
    {
        return MessageSendException.WrongArgumentCount(selector, expected, actual, context);
    }

    /// <summary>
    /// Creates a BootstrapException for a missing file error.
    /// </summary>
    /// <param name="fileName">The missing file name.</param>
    /// <param name="phase">The bootstrap phase.</param>
    /// <returns>A new BootstrapException.</returns>
    public static BootstrapException BootstrapFileNotFound(string fileName, string? phase = null)
    {
        return BootstrapException.FileNotFound(fileName, phase);
    }

    /// <summary>
    /// Creates a standardized error message for literal access errors.
    /// </summary>
    /// <param name="index">The literal index that was invalid.</param>
    /// <param name="literalCount">The total number of literals available.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new InvalidOperationException.</returns>
    public static InvalidOperationException LiteralOutOfBounds(int index, int literalCount, Context? context = null)
    {
        var message = $"Literal index {index} out of bounds for literal frame (size: {literalCount})";
        if (context?.Method?.SelectorName != null)
        {
            message += $" in method '{context.Method.SelectorName}'";
        }
        return new InvalidOperationException(message);
    }

    /// <summary>
    /// Creates a standardized error message for null return values.
    /// </summary>
    /// <param name="operation">The operation that returned null.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A new InvalidOperationException.</returns>
    public static InvalidOperationException CannotReturnNull(string operation, Context? context = null)
    {
        var message = $"Cannot return null from {operation}";
        if (context?.Method?.SelectorName != null)
        {
            message += $" in method '{context.Method.SelectorName}'";
        }
        return new InvalidOperationException(message);
    }
}