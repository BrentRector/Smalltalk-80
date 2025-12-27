namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Represents a Smalltalk global variable reference.
/// According to the Blue Book, global variables are stored as Associations in the literal frame.
/// Globals reference the Smalltalk SystemDictionary.
/// </summary>
public class GlobalVariable
{
    /// <summary>
    /// The key (variable name as a Symbol).
    /// </summary>
    public Symbol Key { get; }

    /// <summary>
    /// The value (initially null, resolved at runtime).
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the GlobalVariable class.
    /// </summary>
    /// <param name="key">The key (variable name as a Symbol).</param>
    /// <param name="value">The value (typically null for global variables).</param>
    public GlobalVariable(Symbol key, object? value = null)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the GlobalVariable class with a string key.
    /// </summary>
    /// <param name="keyName">The key as a string (will be converted to Symbol).</param>
    /// <param name="value">The value (typically null for global variables).</param>
    public GlobalVariable(string keyName, object? value = null)
    {
        Key = new Symbol(keyName);
        Value = value;
    }

    public override bool Equals(object? obj)
    {
        return obj is GlobalVariable other && Key.Equals(other.Key);
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }

    public override string ToString()
    {
        return $"GlobalVariable(key: {Key}, value: {Value ?? "null"})";
    }
}
