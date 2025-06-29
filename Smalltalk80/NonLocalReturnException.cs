using System;

/// <summary>
///     Exception used to implement non-local returns from blocks.
/// </summary>
public sealed class NonLocalReturnException : Exception
{
    /// <summary>
    ///     Gets the value being returned.
    /// </summary>
    public object ReturnValue { get; }

    /// <summary>
    ///     Gets the context that should receive the return value.
    /// </summary>
    public Context TargetContext { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NonLocalReturnException"/> class.
    /// </summary>
    /// <param name="value">The return value.</param>
    /// <param name="target">The target context.</param>
    public NonLocalReturnException(object value, Context target)
    {
        ReturnValue = value;
        TargetContext = target;
    }
}