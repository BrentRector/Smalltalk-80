using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Exception thrown when a block performs a non-local return (^ from within a block).
/// This exception unwinds the stack back to the block's return context.
/// </summary>
public class NonLocalReturnException : Exception
{
    /// <summary>
    /// The value being returned.
    /// </summary>
    public SmalltalkOOP ReturnValue { get; }

    /// <summary>
    /// The context to return to (the block's returnContext).
    /// </summary>
    public SmalltalkOOP TargetContext { get; }

    public NonLocalReturnException(SmalltalkOOP returnValue, SmalltalkOOP targetContext)
        : base($"Non-local return to context {targetContext} with value {returnValue}")
    {
        ReturnValue = returnValue;
        TargetContext = targetContext;
    }
}
