using System;

namespace Smalltalk80.Compiler.Model;

/// <summary>
/// LEGACY: Represents a Smalltalk Association (key-value pair).
/// DEPRECATED: Use GlobalVariable or PoolVariable instead.
/// This class conflates two separate semantics and should not be used for new code.
/// </summary>
[Obsolete("Use GlobalVariable or PoolVariable instead")]
public class Association
{
    public Symbol Key { get; }
    public object? Value { get; set; }
    public string? PoolDictionaryName { get; }

    public Association(Symbol key, object? value = null, string? poolDictionaryName = null)
    {
        Key = key;
        Value = value;
        PoolDictionaryName = poolDictionaryName;
    }

    public Association(string keyName, object? value = null, string? poolDictionaryName = null)
    {
        Key = new Symbol(keyName);
        Value = value;
        PoolDictionaryName = poolDictionaryName;
    }

    public override bool Equals(object? obj)
    {
        return obj is Association other && Key.Equals(other.Key) && PoolDictionaryName == other.PoolDictionaryName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key.GetHashCode(), PoolDictionaryName?.GetHashCode() ?? 0);
    }

    public override string ToString()
    {
        if (PoolDictionaryName != null)
        {
            return $"Association(key: {Key}, value: {Value ?? "null"}, poolDictionary: {PoolDictionaryName})";
        }
        return $"Association(key: {Key}, value: {Value ?? "null"})";
    }
}
