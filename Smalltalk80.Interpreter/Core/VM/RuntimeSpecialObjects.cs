using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Singleton for frequently-accessed runtime classes with cache invalidation.
/// Provides lazy-loaded, cached OOPs for common classes like Array, String, etc.
/// Invalidated by primitive 89 (flushCache) when classes are modified.
/// </summary>
public class RuntimeSpecialObjects
{
    private static RuntimeSpecialObjects? _instance;
    public static RuntimeSpecialObjects Instance => _instance ??= new();

    // Cached class OOPs (populated on first use)
    private SmalltalkOOP? _compiledMethodClass;
    private SmalltalkOOP? _byteArrayClass;
    private SmalltalkOOP? _methodContextClass;
    private SmalltalkOOP? _dictionaryClass;
    private SmalltalkOOP? _arrayClass;
    private SmalltalkOOP? _stringClass;
    private SmalltalkOOP? _symbolClass;
    private SmalltalkOOP? _associationClass;

    // Reference to heap access for lookups
    private RuntimeHeapAccessor? _heapAccess;

    /// <summary>
    /// Initializes the singleton with heap accessor reference.
    /// Must be called during VM initialization.
    /// </summary>
    public void Initialize(RuntimeHeapAccessor heapAccess)
    {
        _heapAccess = heapAccess;
    }

    private SmalltalkOOP LookupClass(string className)
    {
        if (_heapAccess == null)
            throw new InvalidOperationException("RuntimeSpecialObjects not initialized");
        return _heapAccess.GetClass(className);
    }

    public SmalltalkOOP CompiledMethodClass =>
        _compiledMethodClass ??= LookupClass("CompiledMethod");

    public SmalltalkOOP ByteArrayClass =>
        _byteArrayClass ??= LookupClass("ByteArray");

    public SmalltalkOOP MethodContextClass =>
        _methodContextClass ??= LookupClass("MethodContext");

    public SmalltalkOOP DictionaryClass =>
        _dictionaryClass ??= LookupClass("Dictionary");

    public SmalltalkOOP ArrayClass =>
        _arrayClass ??= LookupClass("Array");

    public SmalltalkOOP StringClass =>
        _stringClass ??= LookupClass("String");

    public SmalltalkOOP SymbolClass =>
        _symbolClass ??= LookupClass("Symbol");

    public SmalltalkOOP AssociationClass =>
        _associationClass ??= LookupClass("Association");

    /// <summary>
    /// Invalidates all cached class OOPs.
    /// Called by primitive 89 (flushCache) when classes are modified.
    /// </summary>
    public void InvalidateCache()
    {
        _compiledMethodClass = null;
        _byteArrayClass = null;
        _methodContextClass = null;
        _dictionaryClass = null;
        _arrayClass = null;
        _stringClass = null;
        _symbolClass = null;
        _associationClass = null;
    }
}
