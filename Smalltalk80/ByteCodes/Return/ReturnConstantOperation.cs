using System;
using Smalltalk80;

namespace Smalltalk80.ByteCodes.Return;

/// <summary>
///     Returns a fixed constant from the current method.
/// </summary>
public sealed class ReturnConstantOperation : IBytecodeOperation
{
    /// <summary>
    ///     Distinguishes which constant value should be returned.
    /// </summary>
    public enum Kind
    {
        /// <summary>Return the current message receiver.</summary>
        Self,

        /// <summary>Return the boolean constant <c>true</c>.</summary>
        True,

        /// <summary>Return the boolean constant <c>false</c>.</summary>
        False,

        /// <summary>Return the value <c>nil</c>.</summary>
        Nil
    }

    private readonly Kind kind;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReturnConstantOperation"/>
    ///     class.
    /// </summary>
    /// <param name="kind">The constant to return.</param>
    public ReturnConstantOperation(Kind kind) => this.kind = kind;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        object? value = kind switch
        {
            Kind.Self => interpreter.CurrentContext.Receiver,
            Kind.True => true,
            Kind.False => false,
            _ => null
        };

        interpreter.ReturnFrom(value!);
    }
}
