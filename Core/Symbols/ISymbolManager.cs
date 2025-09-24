using Core.Memory;

namespace Core.Symbols;

/// <summary>
/// Manages Smalltalk symbol interning and lookup.
/// Symbols are unique string instances used as method selectors and identifiers.
/// </summary>
public interface ISymbolManager
{
    /// <summary>
    /// Interns a string as a symbol, returning existing symbol if already interned.
    /// </summary>
    /// <param name="value">The string value to intern.</param>
    /// <returns>The OOP of the interned symbol.</returns>
    OOP Intern(string value);
    
    /// <summary>
    /// Gets a symbol if it exists, returns Nil if not interned.
    /// </summary>
    /// <param name="value">The string value to look up.</param>
    /// <returns>The OOP of the symbol or Nil.</returns>
    OOP Lookup(string value);
    
    /// <summary>
    /// Checks if a string is already interned as a symbol.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <returns>True if the symbol exists.</returns>
    bool Contains(string value);
    
    /// <summary>
    /// Gets the string value of a symbol OOP.
    /// </summary>
    /// <param name="symbolOop">The symbol OOP.</param>
    /// <returns>The string value of the symbol.</returns>
    string GetString(OOP symbolOop);
    
    /// <summary>
    /// Gets all interned symbols.
    /// </summary>
    /// <returns>Collection of all symbol OOPs.</returns>
    IReadOnlyCollection<OOP> GetAllSymbols();
    
    /// <summary>
    /// Gets the count of interned symbols.
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Clears all interned symbols (use with caution, mainly for testing).
    /// </summary>
    void Clear();
}