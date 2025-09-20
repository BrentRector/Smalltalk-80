using System;

namespace Smalltalk80.Exceptions;

/// <summary>
/// Base exception for all Smalltalk runtime errors.
/// Provides consistent error reporting and debugging information.
/// </summary>
public abstract class SmalltalkRuntimeException : Exception
{
    /// <summary>
    /// Gets the context where the error occurred, if available.
    /// </summary>
    public Context? ErrorContext { get; }

    /// <summary>
    /// Gets the method being executed when the error occurred, if available.
    /// </summary>
    public CompiledMethod? ErrorMethod { get; }

    /// <summary>
    /// Gets the program counter where the error occurred, if available.
    /// </summary>
    public int? ProgramCounter { get; }

    /// <summary>
    /// Initializes a new instance of the SmalltalkRuntimeException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="context">The context where the error occurred.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected SmalltalkRuntimeException(string message, Context? context = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorContext = context;
        ErrorMethod = context?.Method;
        ProgramCounter = context?.PC;
    }

    /// <summary>
    /// Gets a detailed error message including context information.
    /// </summary>
    public virtual string DetailedMessage
    {
        get
        {
            var details = Message;
            
            if (ErrorMethod != null)
            {
                details += $"\n  Method: {ErrorMethod.SelectorName ?? "unknown"}";
            }
            
            if (ProgramCounter.HasValue)
            {
                details += $"\n  PC: {ProgramCounter.Value}";
            }
            
            if (ErrorContext != null)
            {
                details += $"\n  Context: {ErrorContext.GetType().Name}";
            }
            
            return details;
        }
    }
}