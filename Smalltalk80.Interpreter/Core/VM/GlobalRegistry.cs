using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for well-known global OOPs during cold start.
/// Tracks singleton objects, system objects, and class objects by name.
/// CRITICAL: Stores SmalltalkOOP values, NEVER object table indices.
/// </summary>
public static class GlobalRegistry
{
    private static readonly Dictionary<string, SmalltalkOOP> _globals = new();
    private static readonly Dictionary<string, object> _objects = new();

    // Well-known singleton OOPs
    public static SmalltalkOOP NilOOP { get; private set; }
    public static SmalltalkOOP TrueOOP { get; private set; }
    public static SmalltalkOOP FalseOOP { get; private set; }
    public static SmalltalkOOP SystemDictionaryOOP { get; private set; }

    /// <summary>
    /// Registers a well-known global object OOP.
    /// </summary>
    public static void Register(string name, SmalltalkOOP oop)
    {
        _globals[name] = oop;

        // Update well-known properties
        switch (name)
        {
            case "nil":
                NilOOP = oop;
                break;
            case "true":
                TrueOOP = oop;
                break;
            case "false":
                FalseOOP = oop;
                break;
            case "SystemDictionary":
            case "Smalltalk":
                SystemDictionaryOOP = oop;
                break;
        }
    }

    /// <summary>
    /// Looks up a class object OOP by class name.
    /// </summary>
    public static SmalltalkOOP LookupClassOOP(string className)
    {
        if (_globals.TryGetValue($"class:{className}", out var oop))
            return oop;

        throw new KeyNotFoundException($"Class not found: {className}");
    }

    /// <summary>
    /// Registers a class object by name.
    /// </summary>
    public static void RegisterClass(string className, SmalltalkOOP oop)
    {
        _globals[$"class:{className}"] = oop;
    }

    /// <summary>
    /// Gets a global object OOP by name.
    /// </summary>
    public static SmalltalkOOP Get(string name)
    {
        return _globals[name];
    }

    /// <summary>
    /// Tries to get a global object OOP by name.
    /// </summary>
    public static bool TryGet(string name, out SmalltalkOOP oop)
    {
        return _globals.TryGetValue(name, out oop);
    }

    /// <summary>
    /// Sets a generic object in the registry (for non-OOP data like maps, lists, etc.)
    /// </summary>
    public static void Set(string name, object value)
    {
        _objects[name] = value;
    }

    /// <summary>
    /// Gets a generic object from the registry with type casting.
    /// </summary>
    public static T? GetObject<T>(string name) where T : class
    {
        if (_objects.TryGetValue(name, out var obj))
        {
            return obj as T;
        }
        return null;
    }

    /// <summary>
    /// Tries to get a generic object from the registry.
    /// </summary>
    public static bool TryGetObject<T>(string name, out T? value) where T : class
    {
        if (_objects.TryGetValue(name, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Returns all registered globals as an enumerable of key-value pairs.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, SmalltalkOOP>> GetAll()
    {
        return _globals;
    }

    /// <summary>
    /// Clears all registered globals. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _globals.Clear();
        _objects.Clear();
        NilOOP = SmalltalkOOP.Null;
        TrueOOP = SmalltalkOOP.Null;
        FalseOOP = SmalltalkOOP.Null;
        SystemDictionaryOOP = SmalltalkOOP.Null;
    }
}
