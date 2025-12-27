using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for well-known global OOPs during cold start.
/// Tracks singleton objects, system objects, and class objects by name.
/// CRITICAL: Stores SmalltalkOOP values, NEVER object table indices.
///
/// ARCHITECTURAL ENFORCEMENT: After Freeze() is called, all write operations
/// (Register, RegisterClass, Set) will throw exceptions. Runtime code must access
/// the Smalltalk heap directly, not these cold start proxy structures.
/// </summary>
public static class GlobalRegistry
{
    private static readonly Dictionary<string, SmalltalkOOP> _globals = new();
    private static readonly Dictionary<string, object> _objects = new();
    private static bool _isFrozen = false;

    // Well-known singleton OOPs
    public static SmalltalkOOP NilOOP { get; private set; }
    public static SmalltalkOOP TrueOOP { get; private set; }
    public static SmalltalkOOP FalseOOP { get; private set; }
    public static SmalltalkOOP SystemDictionaryOOP { get; private set; }

    /// <summary>
    /// Freezes the registry to prevent runtime modifications.
    /// Call this at the end of cold start (after Loader.Run()).
    /// Once frozen, Register/RegisterClass/Set will throw exceptions.
    /// Read-only operations (Get, TryGet) remain allowed.
    /// </summary>
    public static void Freeze()
    {
        _isFrozen = true;
        // Console.WriteLine($"[GlobalRegistry] Frozen with {_globals.Count} global entries, {_objects.Count} object entries");
    }

    /// <summary>
    /// Registers a well-known global object OOP.
    /// Used during cold start to track special objects (nil, true, false, etc).
    /// THROWS after Freeze() - only for cold start use.
    /// </summary>
    public static void Register(string name, SmalltalkOOP oop)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.Register() called after freeze. " +
                $"Runtime code must access heap directly. Attempted to register: '{name}'");
        }

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
    /// THROWS after Freeze() - runtime code must use VMMetadata class OOPs or query heap.
    /// </summary>
    public static SmalltalkOOP LookupClassOOP(string className)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.LookupClassOOP() called after freeze. " +
                $"Runtime code must use VMMetadata class OOPs or query heap directly. Attempted to lookup: '{className}'");
        }

        if (_globals.TryGetValue($"class:{className}", out var oop))
            return oop;

        throw new KeyNotFoundException($"Class not found: {className}");
    }

    /// <summary>
    /// Registers a class object by name.
    /// THROWS after Freeze() - only for cold start use.
    /// </summary>
    public static void RegisterClass(string className, SmalltalkOOP oop)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.RegisterClass() called after freeze. " +
                $"Runtime code must access heap directly. Attempted to register: '{className}'");
        }

        _globals[$"class:{className}"] = oop;
    }

    /// <summary>
    /// Gets a global object OOP by name.
    /// THROWS after Freeze() - runtime code must use VMMetadata.KnownObjects or query heap.
    /// </summary>
    public static SmalltalkOOP Get(string name)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.Get() called after freeze. " +
                $"Runtime code must use VMMetadata.KnownObjects or query heap directly. Attempted to get: '{name}'");
        }
        return _globals[name];
    }

    /// <summary>
    /// Tries to get a global object OOP by name.
    /// THROWS after Freeze() - runtime code must use VMMetadata.KnownObjects or query heap.
    /// </summary>
    public static bool TryGet(string name, out SmalltalkOOP oop)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.TryGet() called after freeze. " +
                $"Runtime code must use VMMetadata.KnownObjects or query heap directly. Attempted to get: '{name}'");
        }
        return _globals.TryGetValue(name, out oop);
    }

    /// <summary>
    /// Sets a generic object in the registry (for non-OOP data like maps, lists, etc.)
    /// THROWS after Freeze() - only for cold start use.
    /// </summary>
    public static void Set(string name, object value)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.Set() called after freeze. " +
                $"Runtime code must access heap directly. Attempted to set: '{name}'");
        }

        _objects[name] = value;
    }

    /// <summary>
    /// Gets a generic object from the registry with type casting.
    /// THROWS after Freeze() - runtime code must use VMMetadata or heap.
    /// </summary>
    public static T? GetObject<T>(string name) where T : class
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.GetObject() called after freeze. " +
                $"Runtime code must use VMMetadata or query heap directly. Attempted to get: '{name}'");
        }

        if (_objects.TryGetValue(name, out var obj))
        {
            return obj as T;
        }
        return null;
    }

    /// <summary>
    /// Tries to get a generic object from the registry.
    /// THROWS after Freeze() - runtime code must use VMMetadata or heap.
    /// </summary>
    public static bool TryGetObject<T>(string name, out T? value) where T : class
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.TryGetObject() called after freeze. " +
                $"Runtime code must use VMMetadata or query heap directly. Attempted to get: '{name}'");
        }

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
    /// THROWS after Freeze() - cold start structure not available at runtime.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, SmalltalkOOP>> GetAll()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.GetAll() called after freeze. " +
                $"Cold start structure not available at runtime.");
        }
        return _globals;
    }

    /// <summary>
    /// Clears all registered globals. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _globals.Clear();
        _objects.Clear();
        NilOOP = default;
        TrueOOP = default;
        FalseOOP = default;
        SystemDictionaryOOP = default;
        _isFrozen = false;
    }
}
