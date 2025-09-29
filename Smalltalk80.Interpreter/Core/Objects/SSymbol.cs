namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk Symbol (unique, interned string).
/// </summary>
public sealed class SSymbol : SObject, IEquatable<SSymbol>
{
    private static readonly Dictionary<string, SSymbol> symbolTable = [];
    private readonly string value;
    
    /// <summary>
    /// Private constructor - symbols are created through Intern.
    /// </summary>
    private SSymbol(string value)
    {
        this.value = value;
    }
    
    /// <summary>
    /// Gets the symbol value.
    /// </summary>
    public string Value => value;
    
    /// <summary>
    /// Gets the class of Symbol.
    /// </summary>
    public override SClass Class => SystemDictionary.SymbolClass;
    
    /// <summary>
    /// Interns a string to get the unique symbol.
    /// </summary>
    public static SSymbol Intern(string value)
    {
        if (symbolTable.TryGetValue(value, out var symbol))
        {
            return symbol;
        }
        
        symbol = new SSymbol(value);
        symbolTable[value] = symbol;
        return symbol;
    }
    
    /// <summary>
    /// Converts this symbol to a string.
    /// </summary>
    public SString AsString() => new(value);
    
    /// <summary>
    /// Converts this SSymbol to a display string.
    /// </summary>
    public override string ToDisplayString() => $"#{value}";
    
    /// <summary>
    /// Gets the hash code for this SSymbol.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether this SSymbol equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is SSymbol other && ReferenceEquals(this, other);
    
    /// <summary>
    /// Determines whether this SSymbol equals another SSymbol.
    /// </summary>
    public bool Equals(SSymbol? other) => ReferenceEquals(this, other);
    
    /// <summary>
    /// Clears the symbol table (for testing purposes).
    /// </summary>
    internal static void ClearSymbolTable() => symbolTable.Clear();
}