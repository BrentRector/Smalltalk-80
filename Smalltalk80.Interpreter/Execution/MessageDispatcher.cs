using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Primitives;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Handles method lookup and message dispatching in the Smalltalk-80 VM.
/// Implements method caching for performance optimization.
/// </summary>
public sealed class MessageDispatcher
{
    private readonly ObjectMemory memory;
    private readonly PrimitiveTable primitiveTable;
    private readonly MethodCache methodCache;
    private readonly ISymbolTable symbolTable;
    
    // Pre-interned common selectors
    private OOP doesNotUnderstandSelector = OOP.Nil;
    
    /// <summary>
    /// Creates a new message dispatcher.
    /// </summary>
    /// <param name="memory">Object memory for accessing objects.</param>
    /// <param name="primitiveTable">Table of primitive methods.</param>
    /// <param name="symbolTable">Symbol table for selector interning.</param>
    public MessageDispatcher(ObjectMemory memory, PrimitiveTable primitiveTable, ISymbolTable symbolTable)
    {
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        this.primitiveTable = primitiveTable ?? throw new ArgumentNullException(nameof(primitiveTable));
        this.symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        this.methodCache = new MethodCache();
    }
    
    /// <summary>
    /// Initializes pre-interned selectors after bootstrap.
    /// </summary>
    public void Initialize()
    {
        doesNotUnderstandSelector = symbolTable.Intern("doesNotUnderstand:");
        // Don't pre-intern selectors we're not actually using yet
        // These would be leaked since MessageDispatcher is not traced by GC
        // messageSelector = symbolTable.Intern("message");
        // messageArgumentsSelector = symbolTable.Intern("arguments");
    }
    
    /// <summary>
    /// Dispatches a message send.
    /// </summary>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <param name="startClass">Optional starting class for super sends.</param>
    /// <returns>The method to execute and its class, or nil if not found.</returns>
    public (OOP method, OOP methodClass) Dispatch(OOP receiver, OOP selector, OOP[] arguments, OOP startClass = default)
    {
        // Get the receiver's class
        var receiverClass = startClass.IsNil ? GetClass(receiver) : startClass;
        if (receiverClass.IsNil)
        {
            return (OOP.Nil, OOP.Nil);
        }
        
        // Check method cache first
        var cacheKey = new MethodCacheKey(receiverClass, selector);
        if (methodCache.TryGet(cacheKey, out var cachedMethod, out var cachedMethodClass))
        {
            return (cachedMethod, cachedMethodClass);
        }
        
        // Perform method lookup
        var (method, methodClass) = LookupMethod(receiverClass, selector);
        
        // Cache the result (even if not found, to avoid repeated lookups)
        methodCache.Put(cacheKey, method, methodClass);
        
        return (method, methodClass);
    }
    
    /// <summary>
    /// Looks up a method in the class hierarchy.
    /// </summary>
    private (OOP method, OOP methodClass) LookupMethod(OOP classOop, OOP selector)
    {
        var currentClass = classOop;
        
        while (!currentClass.IsNil)
        {
            var classObj = memory.GetObject(currentClass);
            if (classObj == null)
                break;
            
            // Get method dictionary (usually field 1 for Behavior)
            if (classObj.Fields.Length > 1)
            {
                var methodDict = classObj.Fields[1];
                if (!methodDict.IsNil)
                {
                    var method = LookupInDictionary(methodDict, selector);
                    if (!method.IsNil)
                    {
                        return (method, currentClass);
                    }
                }
            }
            
            // Move to superclass (usually field 0 for Behavior)
            currentClass = classObj.Fields.Length > 0 ? classObj.Fields[0] : OOP.Nil;
        }
        
        return (OOP.Nil, OOP.Nil);
    }
    
    /// <summary>
    /// Looks up a selector in a method dictionary.
    /// </summary>
    private OOP LookupInDictionary(OOP dictionary, OOP selector)
    {
        var dictObj = memory.GetObject(dictionary);
        if (dictObj == null)
            return OOP.Nil;
        
        // MethodDictionary format: [tally, array]
        // Where array contains alternating keys and values
        if (dictObj.Fields.Length >= 2)
        {
            var tallyOop = dictObj.Fields[0];
            if (!tallyOop.IsSmallInteger)
                return OOP.Nil;
            
            var tally = tallyOop.SmallIntegerValue;
            if (tally == 0)
                return OOP.Nil;
            
            var arrayOop = dictObj.Fields[1];
            var arrayObj = memory.GetObject(arrayOop);
            if (arrayObj == null)
                return OOP.Nil;
            
            // Linear search through dictionary (could optimize with hashing)
            for (int i = 0; i < arrayObj.Fields.Length - 1; i += 2)
            {
                if (SelectorEquals(arrayObj.Fields[i], selector))
                {
                    return arrayObj.Fields[i + 1];
                }
            }
        }
        
        return OOP.Nil;
    }
    
    /// <summary>
    /// Compares two selectors for equality.
    /// </summary>
    private bool SelectorEquals(OOP selector1, OOP selector2)
    {
        // Fast path: same OOP
        if (selector1.RawValue == selector2.RawValue)
            return true;
        
        // Symbols should be unique, so different OOPs mean different selectors
        return false;
    }
    
    /// <summary>
    /// Creates a Message object for doesNotUnderstand:.
    /// </summary>
    public OOP CreateMessageObject(OOP selector, OOP[] arguments, OOP messageClass)
    {
        if (messageClass.IsNil)
            return OOP.Nil;
        
        // Create Message instance with selector and arguments
        var message = memory.AllocateObject(messageClass, 2);
        var messageObj = memory.GetObject(message);
        if (messageObj != null)
        {
            messageObj.Fields[0] = selector;
            
            // Create Array for arguments
            var arrayClass = GetClassByName("Array");
            if (!arrayClass.IsNil)
            {
                var argsArray = memory.AllocateWordArray(arrayClass, arguments.Length);
                var argsObj = memory.GetObject(argsArray);
                if (argsObj != null)
                {
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        argsObj.Fields[i] = arguments[i];
                    }
                }
                messageObj.Fields[1] = argsArray;
            }
        }
        
        return message;
    }
    
    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    private OOP GetClass(OOP oop)
    {
        // SmallIntegers have a special class
        if (oop.IsSmallInteger)
        {
            return GetClassByName("SmallInteger");
        }
        
        // Get class from object header
        var obj = memory.GetObject(oop);
        return obj?.Header.ClassOOP ?? OOP.Nil;
    }
    
    /// <summary>
    /// Gets a class by name from the system dictionary.
    /// This is a placeholder - should be provided by the VM.
    /// </summary>
    private OOP GetClassByName(string name)
    {
        // This would normally look up in the system dictionary
        // For now, return nil
        return OOP.Nil;
    }
    
    /// <summary>
    /// Gets the doesNotUnderstand: selector.
    /// </summary>
    public OOP DoesNotUnderstandSelector => doesNotUnderstandSelector;
    
    /// <summary>
    /// Clears the method cache.
    /// </summary>
    public void ClearCache()
    {
        methodCache.Clear();
    }
    
    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public MethodCacheStatistics GetCacheStatistics() => methodCache.GetStatistics();
}

/// <summary>
/// Method cache for accelerating method lookup.
/// Uses a simple hash table with linear probing.
/// </summary>
internal sealed class MethodCache
{
    private const int CacheSize = 1024; // Power of 2 for fast masking
    private const int CacheMask = CacheSize - 1;
    
    private readonly MethodCacheEntry[] entries = new MethodCacheEntry[CacheSize];
    private long hits;
    private long misses;
    private long collisions;
    
    /// <summary>
    /// Tries to get a cached method.
    /// </summary>
    public bool TryGet(MethodCacheKey key, out OOP method, out OOP methodClass)
    {
        int index = Hash(key) & CacheMask;
        var entry = entries[index];
        
        if (entry.Key.Equals(key))
        {
            hits++;
            method = entry.Method;
            methodClass = entry.MethodClass;
            return true;
        }
        
        // Linear probe for collision resolution
        int probeCount = 0;
        while (probeCount < 8) // Limit probing to avoid long searches
        {
            index = (index + 1) & CacheMask;
            entry = entries[index];
            
            if (entry.Key.Equals(key))
            {
                hits++;
                collisions++;
                method = entry.Method;
                methodClass = entry.MethodClass;
                return true;
            }
            
            if (entry.Key.ClassOop.IsNil)
                break; // Empty slot found, method not cached
            
            probeCount++;
        }
        
        misses++;
        method = OOP.Nil;
        methodClass = OOP.Nil;
        return false;
    }
    
    /// <summary>
    /// Caches a method lookup result.
    /// </summary>
    public void Put(MethodCacheKey key, OOP method, OOP methodClass)
    {
        int index = Hash(key) & CacheMask;
        var entry = entries[index];
        
        // Direct placement if empty or same key
        if (entry.Key.ClassOop.IsNil || entry.Key.Equals(key))
        {
            entries[index] = new MethodCacheEntry(key, method, methodClass);
            return;
        }
        
        // Linear probe for an empty slot
        int probeCount = 0;
        while (probeCount < 8)
        {
            index = (index + 1) & CacheMask;
            entry = entries[index];
            
            if (entry.Key.ClassOop.IsNil || entry.Key.Equals(key))
            {
                entries[index] = new MethodCacheEntry(key, method, methodClass);
                return;
            }
            
            probeCount++;
        }
        
        // Cache full in this region, evict the original slot
        index = Hash(key) & CacheMask;
        entries[index] = new MethodCacheEntry(key, method, methodClass);
    }
    
    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        Array.Clear(entries);
        hits = misses = collisions = 0;
    }
    
    /// <summary>
    /// Computes hash for a cache key.
    /// </summary>
    private static int Hash(MethodCacheKey key)
    {
        // Simple hash combining class and selector OOPs
        uint classHash = key.ClassOop.RawValue;
        uint selectorHash = key.Selector.RawValue;
        
        // Mix the bits
        uint hash = classHash ^ (selectorHash << 16) ^ (selectorHash >> 16);
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;
        
        return (int)hash;
    }
    
    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public MethodCacheStatistics GetStatistics() =>
        new(hits, misses, collisions, CacheSize);
}

/// <summary>
/// Key for method cache lookup.
/// </summary>
internal readonly record struct MethodCacheKey(OOP ClassOop, OOP Selector);

/// <summary>
/// Entry in the method cache.
/// </summary>
internal readonly record struct MethodCacheEntry(MethodCacheKey Key, OOP Method, OOP MethodClass);

/// <summary>
/// Statistics for method cache performance.
/// </summary>
public readonly record struct MethodCacheStatistics(
    long Hits,
    long Misses,
    long Collisions,
    int CacheSize)
{
    /// <summary>
    /// Gets the hit ratio as a percentage.
    /// </summary>
    public double HitRatio =>
        Hits + Misses == 0 ? 0.0 : (double)Hits / (Hits + Misses) * 100.0;
    
    /// <summary>
    /// Gets a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"Cache[Size={CacheSize}, Hits={Hits}, Misses={Misses}, HitRatio={HitRatio:F2}%, Collisions={Collisions}]";
}

