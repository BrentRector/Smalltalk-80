using System;

namespace Smalltalk80.Exceptions;

/// <summary>
/// Exception thrown when bytecode execution encounters an error.
/// </summary>
public sealed class BytecodeException : SmalltalkRuntimeException
{
    /// <summary>
    /// Gets the bytecode that caused the error.
    /// </summary>
    public byte Bytecode { get; }

    /// <summary>
    /// Gets the bytecode operation name, if known.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Initializes a new instance of the BytecodeException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="bytecode">The bytecode that caused the error.</param>
    /// <param name="operation">The bytecode operation name.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public BytecodeException(string message, byte bytecode, string? operation = null, Context? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
        Bytecode = bytecode;
        Operation = operation;
    }

    /// <summary>
    /// Creates a BytecodeException for an unknown or unsupported bytecode.
    /// </summary>
    /// <param name="bytecode">The unknown bytecode.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new BytecodeException instance.</returns>
    public static BytecodeException UnknownBytecode(byte bytecode, Context? context = null)
    {
        return new BytecodeException(
            $"Unknown bytecode: {bytecode} (0x{bytecode:X2})",
            bytecode,
            "unknown",
            context);
    }

    /// <summary>
    /// Creates a BytecodeException for a bytecode that is not yet implemented.
    /// </summary>
    /// <param name="bytecode">The unimplemented bytecode.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new BytecodeException instance.</returns>
    public static BytecodeException NotImplemented(byte bytecode, string operation, Context? context = null)
    {
        return new BytecodeException(
            $"Bytecode not yet implemented: {operation} ({bytecode}/0x{bytecode:X2})",
            bytecode,
            operation,
            context);
    }

    /// <inheritdoc />
    public override string DetailedMessage
    {
        get
        {
            var details = base.DetailedMessage;
            details += $"\n  Bytecode: {Bytecode} (0x{Bytecode:X2})";
            
            if (!string.IsNullOrEmpty(Operation))
            {
                details += $"\n  Operation: {Operation}";
            }
            
            return details;
        }
    }
}