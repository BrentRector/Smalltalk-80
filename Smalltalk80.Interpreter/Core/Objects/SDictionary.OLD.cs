namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a Smalltalk Dictionary object.
/// Used for SystemDictionary and other dictionary instances.
/// </summary>
public class SDictionary : SInstance
{
    private readonly Dictionary<SObject, SObject> items = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SDictionary"/> class.
    /// </summary>
    public SDictionary() : base(SystemDictionary.DictionaryClass, 2)
    {
        // Dictionary has 2 instance variables: tally and array
        instanceVariables[0] = SObjectRef.FromSmallInteger(0); // tally
        instanceVariables[1] = SObjectRef.FromObject(new SArray(16)); // initial array size
    }

    /// <summary>
    /// Adds or updates a key-value pair in the dictionary.
    /// </summary>
    public void AtPut(SObject key, SObject value)
    {
        items[key] = value;
        // Update tally
        instanceVariables[0] = SObjectRef.FromSmallInteger(items.Count);
    }
    
    /// <summary>
    /// Gets a value for the given key.
    /// </summary>
    public SObject? At(SObject key)
    {
        return items.TryGetValue(key, out var value) ? value : null;
    }
    
    /// <summary>
    /// Gets a value for the given key, or returns the result of evaluating a block if not found.
    /// </summary>
    public SObject AtIfAbsent(SObject key, Func<SObject> absentBlock)
    {
        return items.TryGetValue(key, out var value) ? value : absentBlock();
    }
    
    /// <summary>
    /// Checks if the dictionary contains a key.
    /// </summary>
    public bool IncludesKey(SObject key)
    {
        return items.ContainsKey(key);
    }
    
    /// <summary>
    /// Removes a key from the dictionary.
    /// </summary>
    public bool RemoveKey(SObject key)
    {
        var removed = items.Remove(key);
        if (removed)
        {
            instanceVariables[0] = SObjectRef.FromSmallInteger(items.Count);
        }
        return removed;
    }
    
    /// <summary>
    /// Gets the size of the dictionary.
    /// </summary>
    public int Size => items.Count;
    
    /// <summary>
    /// Enumerates all keys in the dictionary.
    /// </summary>
    public IEnumerable<SObject> Keys => items.Keys;
    
    /// <summary>
    /// Enumerates all values in the dictionary.
    /// </summary>
    public IEnumerable<SObject> Values => items.Values;
    
    /// <summary>
    /// Enumerates all associations (key-value pairs) in the dictionary.
    /// </summary>
    public IEnumerable<(SObject key, SObject value)> Associations => 
        items.Select(kvp => (kvp.Key, kvp.Value));
    
    /// <summary>
    /// Creates a SystemDictionary containing all classes and globals.
    /// </summary>
    public static SDictionary CreateSystemDictionary()
    {
        var dict = new SDictionary();
        
        // Add all classes
        foreach (var className in SystemDictionary.GetAllClassNames())
        {
            var classObj = SystemDictionary.LookupClass(className);
            if (classObj != null)
            {
                var symbol = SSymbol.Intern(className);
                dict.AtPut(symbol, classObj);
            }
        }
        
        // Add global variables
        var transcript = SystemDictionary.LookupGlobal("Transcript");
        if (transcript != null)
        {
            dict.AtPut(SSymbol.Intern("Transcript"), transcript);
        }
        
        // Add the special objects
        dict.AtPut(SSymbol.Intern("nil"), SObjectRef.Nil.AsObject());
        dict.AtPut(SSymbol.Intern("true"), STrue.Instance);
        dict.AtPut(SSymbol.Intern("false"), SFalse.Instance);
        
        return dict;
    }
    
    /// <summary>
    /// Creates a SymbolTable containing all interned symbols.
    /// </summary>
    public static SDictionary CreateSymbolTable()
    {
        var dict = new SDictionary();
        
        // Add all interned symbols
        foreach (var symbol in SSymbol.GetAllSymbols())
        {
            dict.AtPut(symbol, symbol);
        }
        
        return dict;
    }
    
    /// <inheritdoc/>
    public override string ToDisplayString()
    {
        return $"a Dictionary({items.Count} items)";
    }
}