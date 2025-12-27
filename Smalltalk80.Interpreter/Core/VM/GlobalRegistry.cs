using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for well-known global OOPs during cold start.
/// Tracks singleton objects, system objects, and class objects by name.
/// CRITICAL: Stores SmalltalkOOP values, NEVER object table indices.
///
/// LIFECYCLE:
/// 1. During Phases 1-5: Register/RegisterClass/Set populate the registry
/// 2. After Phase 5: MakeImmutable() - reads allowed, writes throw
/// 3. After Phase 6: Dispose() - all access throws, data cleared
///
/// Runtime code must use VMMetadata.KnownObjects or query the Smalltalk heap directly.
/// </summary>
public static class GlobalRegistry
{
    private static Dictionary<string, SmalltalkOOP>? _globals = new();
    private static Dictionary<string, object>? _objects = new();
    private static bool _isImmutable = false;
    private static bool _isDisposed = false;

    // Well-known singleton OOPs (remain accessible after immutable, cleared after dispose)
    public static SmalltalkOOP NilOOP { get; private set; }
    public static SmalltalkOOP TrueOOP { get; private set; }
    public static SmalltalkOOP FalseOOP { get; private set; }
    public static SmalltalkOOP SystemDictionaryOOP { get; private set; }

    /// <summary>
    /// Makes the registry immutable (read-only).
    /// Call this after the last phase that needs to write.
    /// After this, Register/RegisterClass/Set throw but reads are still allowed.
    /// </summary>
    public static void MakeImmutable()
    {
        _isImmutable = true;
    }

    /// <summary>
    /// Disposes the registry, blocking all access and releasing memory.
    /// Call this at the end of cold start (after Phase 6).
    /// After this, ALL operations throw ObjectDisposedException.
    /// </summary>
    public static void Dispose()
    {
        _isDisposed = true;
        _globals?.Clear();
        _globals = null;
        _objects?.Clear();
        _objects = null;
        NilOOP = default;
        TrueOOP = default;
        FalseOOP = default;
        SystemDictionaryOOP = default;
    }

    /// <summary>
    /// Registers a well-known global object OOP.
    /// Used during cold start to track special objects (nil, true, false, etc).
    /// THROWS after MakeImmutable() - only for cold start use.
    /// </summary>
    public static void Register(string name, SmalltalkOOP oop)
    {
        ThrowIfDisposed();
        ThrowIfImmutable("Register", name);

        _globals![name] = oop;

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
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - runtime code must use VMMetadata class OOPs or query heap.
    /// </summary>
    public static SmalltalkOOP LookupClassOOP(string className)
    {
        ThrowIfDisposed();

        if (_globals!.TryGetValue($"class:{className}", out var oop))
            return oop;

        throw new KeyNotFoundException($"Class not found: {className}");
    }

    /// <summary>
    /// Registers a class object by name.
    /// THROWS after MakeImmutable() - only for cold start use.
    /// </summary>
    public static void RegisterClass(string className, SmalltalkOOP oop)
    {
        ThrowIfDisposed();
        ThrowIfImmutable("RegisterClass", className);

        _globals![$"class:{className}"] = oop;
    }

    /// <summary>
    /// Gets a global object OOP by name.
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - runtime code must use VMMetadata.KnownObjects or query heap.
    /// </summary>
    public static SmalltalkOOP Get(string name)
    {
        ThrowIfDisposed();
        return _globals![name];
    }

    /// <summary>
    /// Tries to get a global object OOP by name.
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - runtime code must use VMMetadata.KnownObjects or query heap.
    /// </summary>
    public static bool TryGet(string name, out SmalltalkOOP oop)
    {
        ThrowIfDisposed();
        return _globals!.TryGetValue(name, out oop);
    }

    /// <summary>
    /// Sets a generic object in the registry (for non-OOP data like maps, lists, etc.)
    /// THROWS after MakeImmutable() - only for cold start use.
    /// </summary>
    public static void Set(string name, object value)
    {
        ThrowIfDisposed();
        ThrowIfImmutable("Set", name);

        _objects![name] = value;
    }

    /// <summary>
    /// Gets a generic object from the registry with type casting.
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - runtime code must use VMMetadata or heap.
    /// </summary>
    public static T? GetObject<T>(string name) where T : class
    {
        ThrowIfDisposed();

        if (_objects!.TryGetValue(name, out var obj))
        {
            return obj as T;
        }
        return null;
    }

    /// <summary>
    /// Tries to get a generic object from the registry.
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - runtime code must use VMMetadata or heap.
    /// </summary>
    public static bool TryGetObject<T>(string name, out T? value) where T : class
    {
        ThrowIfDisposed();

        if (_objects!.TryGetValue(name, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Returns all registered globals as an enumerable of key-value pairs.
    /// Allowed during cold start phases.
    /// THROWS after Dispose() - cold start structure not available at runtime.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, SmalltalkOOP>> GetAll()
    {
        ThrowIfDisposed();
        return _globals!;
    }

    /// <summary>
    /// Clears all registered globals. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _globals?.Clear();
        _globals = new();
        _objects?.Clear();
        _objects = new();
        NilOOP = default;
        TrueOOP = default;
        FalseOOP = default;
        SystemDictionaryOOP = default;
        _isImmutable = false;
        _isDisposed = false;
    }

    private static void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(GlobalRegistry),
                "GlobalRegistry has been disposed. Runtime code must use VMMetadata.KnownObjects or query the Smalltalk heap directly.");
        }
    }

    private static void ThrowIfImmutable(string operation, string name)
    {
        if (_isImmutable)
        {
            throw new InvalidOperationException(
                $"GlobalRegistry.{operation}() called after MakeImmutable(). " +
                $"Write operations only allowed during cold start. Attempted: '{name}'");
        }
    }
}
