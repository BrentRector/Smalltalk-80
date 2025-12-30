using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Cache for runtime class lookups with invalidation support.
/// Used by RuntimeServices to avoid repeated heap walks.
/// Invalidated by primitive 89 (flushCache) when classes are modified.
/// </summary>
public class RuntimeClassCache
{
    private readonly RuntimeServices _runtimeServices;
    private readonly ISmalltalkHeap _heapManager;
    private Dictionary<string, SmalltalkOOP> _classCache = new();
    private bool _cacheValid = true;

    public RuntimeClassCache(RuntimeServices runtimeServices, ISmalltalkHeap heapManager)
    {
        _runtimeServices = runtimeServices;
        _heapManager = heapManager;
    }

    /// <summary>
    /// Gets a class OOP by name, using cache if valid.
    /// Falls back to heap lookup if cache miss or invalidated.
    /// </summary>
    public SmalltalkOOP GetClass(string className)
    {
        if (!_cacheValid || !_classCache.TryGetValue(className, out var classOOP))
        {
            classOOP = _runtimeServices.LookupClassInHeap(className);
            if (_cacheValid)
                _classCache[className] = classOOP;
        }
        return classOOP;
    }

    /// <summary>
    /// Invalidates the cache. Called by primitive 89 (flushCache).
    /// </summary>
    public void InvalidateCache()
    {
        _cacheValid = false;
        _classCache.Clear();
    }
}
