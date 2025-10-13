namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for well-known global object IDs during cold start.
/// Tracks singleton objects, system objects, and class objects by name.
/// </summary>
public static class GlobalRegistry
{
    private static readonly Dictionary<string, int> _globals = new();
    private static readonly Dictionary<string, object> _objects = new(); // For non-int global data

    // Well-known singleton object IDs
    public static int NilObjectId { get; private set; }
    public static int TrueObjectId { get; private set; }
    public static int FalseObjectId { get; private set; }
    public static int SystemDictionaryId { get; private set; }

    /// <summary>
    /// Registers a well-known global object.
    /// </summary>
    public static void Register(string name, int objectId)
    {
        _globals[name] = objectId;

        // Update well-known properties
        switch (name)
        {
            case "nil":
                NilObjectId = objectId;
                break;
            case "true":
                TrueObjectId = objectId;
                break;
            case "false":
                FalseObjectId = objectId;
                break;
            case "SystemDictionary":
            case "Smalltalk":
                SystemDictionaryId = objectId;
                break;
        }
    }

    /// <summary>
    /// Looks up a class object ID by class name.
    /// </summary>
    public static int LookupClassObjectId(string className)
    {
        if (_globals.TryGetValue($"class:{className}", out var objectId))
            return objectId;

        throw new KeyNotFoundException($"Class not found: {className}");
    }

    /// <summary>
    /// Registers a class object by name.
    /// </summary>
    public static void RegisterClass(string className, int objectId)
    {
        _globals[$"class:{className}"] = objectId;
    }

    /// <summary>
    /// Gets a global object ID by name.
    /// </summary>
    public static int Get(string name)
    {
        return _globals[name];
    }

    /// <summary>
    /// Tries to get a global object ID by name.
    /// </summary>
    public static bool TryGet(string name, out int objectId)
    {
        return _globals.TryGetValue(name, out objectId);
    }

    /// <summary>
    /// Sets a generic object in the registry (for non-int data like maps, lists, etc.)
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
    /// Clears all registered globals. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _globals.Clear();
        _objects.Clear();
        NilObjectId = 0;
        TrueObjectId = 0;
        FalseObjectId = 0;
        SystemDictionaryId = 0;
    }
}
