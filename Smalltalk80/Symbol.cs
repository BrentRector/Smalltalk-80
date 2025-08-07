using System;

/// <summary>
///     Represents an interned symbol used for method selectors and identifiers.
/// </summary>
public sealed class Symbol
{
    /// <summary>
    ///     Gets the textual name of the symbol.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    ///     Gets the value of the symbol (alias for Name).
    /// </summary>
    public string Value => Name;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Symbol"/> class.
    /// </summary>
    /// <param name="name">The symbol text.</param>
    public Symbol(string name)
    {
        Name = name;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Symbol other && Name == other.Name;

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => "#" + Name;
}