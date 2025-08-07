using System;

/// <summary>
///     Represents an association between a symbol and a value. Shared variables
///     in the literal frame are stored as <see cref="Association"/> objects.
/// </summary>
public sealed class Association
{
    /// <summary>
    ///     Gets the symbol naming the variable.
    /// </summary>
    public Symbol Key { get; }

    /// <summary>
    ///     Gets or sets the value of the variable.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Association"/> class.
    /// </summary>
    /// <param name="key">The identifier of the variable.</param>
    /// <param name="value">The initial value.</param>
    public Association(Symbol key, object? value = null)
    {
        Key = key;
        Value = value;
    }
}
