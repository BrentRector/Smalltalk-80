namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Represents a Smalltalk pool variable binding metadata.
/// Pool variables are NOT stored as Associations - they're binding metadata
/// that tells the VM which pool dictionary to search and what key to look up.
/// At runtime, the VM creates a 2-element Array: (#PoolDictionaryName, #Key).
/// Records provide value equality and immutability by default.
/// </summary>
/// <param name="Key">The key (variable name as a Symbol).</param>
/// <param name="PoolDictionaryName">The pool dictionary name (e.g., "TextConstants").</param>
public sealed record PoolVariable(Symbol Key, string PoolDictionaryName)
{
    /// <summary>
    /// Creates a PoolVariable with a string key (convenience constructor).
    /// </summary>
    /// <param name="keyName">The key as a string (will be converted to Symbol).</param>
    /// <param name="poolDictionaryName">The pool dictionary name.</param>
    public PoolVariable(string keyName, string poolDictionaryName)
        : this(new Symbol(keyName), poolDictionaryName)
    {
    }

    /// <summary>
    /// Returns a string representation of the pool variable.
    /// </summary>
    public override string ToString() => $"PoolVariable(key: {Key}, poolDictionary: {PoolDictionaryName})";
}
