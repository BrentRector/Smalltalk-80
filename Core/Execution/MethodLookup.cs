using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Memory;

namespace Core.Execution;

/// <summary>
/// Implements method lookup with class hierarchy traversal and caching.
/// Follows the Blue Book's method lookup algorithm.
/// </summary>
public sealed class MethodLookup(IObjectMemory objectMemory) : IMethodLookup
{
    private readonly ConcurrentDictionary<(OOP classOop, string selector), OOP> _methodCache = new();
    private const int MaxCacheSize = 1024;

    /// <summary>
    /// Looks up a method in the class hierarchy starting from the given class.
    /// </summary>
    /// <param name="classOop">The class to start searching from.</param>
    /// <param name="selector">The method selector to find.</param>
    /// <returns>The compiled method if found; otherwise, null.</returns>
    public OOP? LookupMethod(OOP classOop, string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        
        // Check cache first
        var cacheKey = (classOop, selector);
        if (_methodCache.TryGetValue(cacheKey, out var cachedMethod))
        {
            return cachedMethod;
        }

        // Traverse class hierarchy
        var currentClass = classOop;
        while (!currentClass.IsNil)
        {
            if (TryFindMethodInClass(currentClass, selector, out var method))
            {
                CacheMethod(cacheKey, method.Value);
                return method;
            }

            // Move to superclass
            currentClass = objectMemory.FetchPointer(currentClass, SuperclassIndex);
        }

        return null;
    }

    /// <summary>
    /// Looks up a method starting from the superclass of the given class.
    /// Used for super sends.
    /// </summary>
    /// <param name="classOop">The class whose superclass to start searching from.</param>
    /// <param name="selector">The method selector to find.</param>
    /// <returns>The compiled method if found; otherwise, null.</returns>
    public OOP? LookupMethodInSuper(OOP classOop, string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        
        var superclass = objectMemory.FetchPointer(classOop, SuperclassIndex);
        return superclass.IsNil ? null : LookupMethod(superclass, selector);
    }

    /// <summary>
    /// Clears the method cache. Called when methods are added or removed.
    /// </summary>
    public void FlushCache()
    {
        _methodCache.Clear();
    }

    /// <summary>
    /// Flushes cache entries for a specific class.
    /// </summary>
    /// <param name="classOop">The class whose methods should be flushed from cache.</param>
    public void FlushCacheForClass(OOP classOop)
    {
        var keysToRemove = _methodCache.Keys
            .Where(key => key.classOop == classOop)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _methodCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Adds a method to the cache.
    /// </summary>
    public void CacheMethod(OOP classOop, string selector, OOP methodOop)
    {
        CacheMethod((classOop, selector), methodOop);
    }

    /// <summary>
    /// Flushes cache entries for a specific selector across all classes.
    /// </summary>
    /// <param name="selector">The selector to flush from cache.</param>
    public void FlushCacheForSelector(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        
        var keysToRemove = _methodCache.Keys
            .Where(key => key.selector == selector)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _methodCache.TryRemove(key, out _);
        }
    }

    private bool TryFindMethodInClass(OOP classOop, string selector, [NotNullWhen(true)] out OOP? method)
    {
        method = null;

        // Get method dictionary from class
        var methodDict = objectMemory.FetchPointer(classOop, MethodDictionaryIndex);
        if (methodDict.IsNil)
        {
            return false;
        }

        // Look up selector in method dictionary
        var selectorArray = objectMemory.FetchPointer(methodDict, 0); // Array of selectors
        var methodArray = objectMemory.FetchPointer(methodDict, 1);   // Array of methods

        if (selectorArray.IsNil || methodArray.IsNil)
        {
            return false;
        }

        var selectorCount = objectMemory.FetchWordLengthOf(selectorArray);
        for (var i = 0; i < selectorCount; i++)
        {
            var selectorOop = objectMemory.FetchPointer(selectorArray, i);
            if (!selectorOop.IsNil && MatchesSelector(selectorOop, selector))
            {
                method = objectMemory.FetchPointer(methodArray, i);
                return !method.Value.IsNil;
            }
        }

        return false;
    }

    private bool MatchesSelector(OOP selectorOop, string selector)
    {
        // Compare selector strings - simplified for now
        // In full implementation, would convert Symbol OOP to string and compare
        return true; // Placeholder
    }

    private void CacheMethod((OOP classOop, string selector) key, OOP method)
    {
        // Implement simple cache size limit
        if (_methodCache.Count >= MaxCacheSize)
        {
            // Remove oldest entries (simplified - in production would use LRU)
            var keysToRemove = _methodCache.Keys.Take(MaxCacheSize / 4).ToList();
            foreach (var oldKey in keysToRemove)
            {
                _methodCache.TryRemove(oldKey, out _);
            }
        }

        _methodCache.TryAdd(key, method);
    }

    // Class format indices per Blue Book
    private const int SuperclassIndex = 0;
    private const int MethodDictionaryIndex = 1;
    private const int InstanceSpecificationIndex = 2;
}