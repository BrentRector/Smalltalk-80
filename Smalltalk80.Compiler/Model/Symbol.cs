namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Represents a Smalltalk symbol.
/// Records provide value equality and immutability by default.
/// </summary>
/// <param name="Value">The symbol's string value (without the # prefix).</param>
public sealed record Symbol(string Value)
{
    /// <summary>
    /// Returns the Smalltalk representation of the symbol (e.g., #foo).
    /// </summary>
    public override string ToString() => $"#{Value}";
}
