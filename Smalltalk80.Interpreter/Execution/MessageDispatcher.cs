using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Primitives;
using Smalltalk80.Interpreter.VM;

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
    private readonly IFieldAccessor fieldAccessor;
    
    // Pre-interned common selectors
    private OOP doesNotUnderstandSelector = OOP.Nil;
    
    // Cached field indices for performance
    private int behaviorSuperclassIndex = -1;
    private int behaviorMethodDictIndex = -1;
    private int dictTallyIndex = -1;
    private int dictArrayIndex = -1;
    
    /// <summary>
    /// Creates a new message dispatcher.
    /// </summary>
    /// <param name="memory">Object memory for accessing objects.</param>
    /// <param name="primitiveTable">Table of primitive methods.</param>
    /// <param name="symbolTable">Symbol table for selector interning.</param>
    /// <param name="fieldAccessor">Field accessor for object field access.</param>
    public MessageDispatcher(ObjectMemory memory, PrimitiveTable primitiveTable, ISymbolTable symbolTable, IFieldAccessor fieldAccessor)
    {
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        this.primitiveTable = primitiveTable ?? throw new ArgumentNullException(nameof(primitiveTable));
        this.symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        this.fieldAccessor = fieldAccessor ?? throw new ArgumentNullException(nameof(fieldAccessor));
        this.methodCache = new MethodCache();
    }
    
    /// <summary>
    /// Initializes pre-interned selectors and cached field indices after bootstrap.
    /// </summary>
    public void Initialize()
    {
        doesNotUnderstandSelector = symbolTable.Intern("doesNotUnderstand:");
        
        // Cache field indices for performance
        behaviorSuperclassIndex = fieldAccessor.GetFieldIndex("Behavior", "superclass");
        behaviorMethodDictIndex = fieldAccessor.GetFieldIndex("Behavior", "methodDict");
        
        // MethodDictionary fields: tally is at 0 (from Set), valueArray is at 1 (from IdentityDictionary)
        dictTallyIndex = 0; // tally is the first field (from Set)
        dictArrayIndex = 1; // valueArray is the second field (from IdentityDictionary)
        
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
        int depth = 0;
        const int MAX_DEPTH = 20; // Prevent infinite loops in class hierarchy
        
        // Debug: Show method lookup start
        if (Config.DebugOptions.EnableMethodTrace)
        {
            var selectorStr = GetSelectorString(selector);
            var startClassName = GetClassName(classOop);
            Console.WriteLine($"[LOOKUP] Looking up selector #{selectorStr} starting from class {startClassName}");
        }
        
        while (!currentClass.IsNil && depth < MAX_DEPTH)
        {
            depth++;
            var classObj = memory.GetObject(currentClass);
            if (classObj == null)
                break;
            
            // Debug: Show which class we're checking
            if (Config.DebugOptions.EnableMethodTrace)
            {
                var className = GetClassName(currentClass);
                Console.WriteLine($"[LOOKUP]   Checking class {className} (hierarchy level {depth})");
            }
            
            // Get method dictionary using cached field index
            var methodDict = behaviorMethodDictIndex >= 0 
                ? fieldAccessor.GetFieldByIndex(currentClass, behaviorMethodDictIndex)
                : OOP.Nil;
                
            // Special debug for Object class's initialize
            if (Config.DebugOptions.EnableMethodTrace)
            {
                var className = GetClassName(currentClass);
                if (className == "Object class" && GetSelectorString(selector) == "initialize")
                {
                    Console.WriteLine($"[LOOKUP]   DEBUG: Looking for initialize in Object class, methodDict={methodDict}");
                }
            }
                
            if (!methodDict.IsNil)
            {
                var method = LookupInDictionary(methodDict, selector);
                if (!method.IsNil)
                {
                    if (Config.DebugOptions.EnableMethodTrace)
                    {
                        Console.WriteLine($"[LOOKUP]   Found method {method} in class {currentClass}");
                    }
                    return (method, currentClass);
                }
            }
            
            // Move to superclass using cached field index
            var previousClass = currentClass;
            currentClass = behaviorSuperclassIndex >= 0 
                ? fieldAccessor.GetFieldByIndex(currentClass, behaviorSuperclassIndex)
                : OOP.Nil;
            
            // Debug: Show superclass navigation
            if (Config.DebugOptions.EnableMethodTrace)
            {
                var prevClassName = GetClassName(previousClass);
                var nextClassName = currentClass.IsNil ? "nil" : GetClassName(currentClass);
                Console.WriteLine($"[LOOKUP]   Moving from {prevClassName} to superclass {nextClassName}");
            }
        }
        
        if (depth >= MAX_DEPTH)
        {
            Console.WriteLine($"[LOOKUP] WARNING: Max depth {MAX_DEPTH} reached in method lookup!");
        }
        
        if (Config.DebugOptions.EnableMethodTrace)
        {
            Console.WriteLine($"[LOOKUP]   Method not found");
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
        
        // Special debug for initialize selector
        if (Config.DebugOptions.EnableMethodTrace && GetSelectorString(selector) == "initialize")
        {
            Console.WriteLine($"[LOOKUP]   DEBUG: Looking up 'initialize' in dictionary {dictionary}");
            Console.WriteLine($"[LOOKUP]   DEBUG: Dictionary has {dictObj.Fields.Length} fields");
        }
        
        // MethodDictionary format as installed by MethodInstaller:
        // Fields[0] = tally
        // Fields[1] = valueArray (array of compiled methods)
        // Fields[2..n] = keys (selectors) in indexed portion
        
        var tallyOop = dictTallyIndex >= 0 
            ? fieldAccessor.GetFieldByIndex(dictionary, dictTallyIndex)
            : OOP.Nil;
            
        if (!tallyOop.IsSmallInteger)
            return OOP.Nil;
        
        var tally = tallyOop.SmallIntegerValue;
        if (tally == 0)
            return OOP.Nil;
        
        var valueArrayOop = dictArrayIndex >= 0 
            ? fieldAccessor.GetFieldByIndex(dictionary, dictArrayIndex)
            : OOP.Nil;
            
        var valueArrayObj = memory.GetObject(valueArrayOop);
        if (valueArrayObj == null)
            return OOP.Nil;
        
        // Search the indexed portion of the dictionary for the selector
        // Keys are in dictObj.Fields[2..n], values are in valueArrayObj.Fields at corresponding index
        var capacity = dictObj.Fields.Length - 2; // Subtract tally and valueArray fields
        
        // Special debug for initialize  
        if (Config.DebugOptions.EnableMethodTrace && GetSelectorString(selector) == "initialize")
        {
            Console.WriteLine($"[LOOKUP]   DEBUG: Searching {capacity} slots in dictionary");
            Console.WriteLine($"[LOOKUP]   DEBUG: Looking for selector OOP {selector.RawValue}");
            
            // Show keys around index 20-22
            for (int j = 18; j < Math.Min(25, capacity); j++)
            {
                var k = dictObj.Fields[2 + j];
                if (!k.IsNil)
                {
                    var keyStr = GetSelectorString(k);
                    Console.WriteLine($"[LOOKUP]   DEBUG: Key at field index {2+j}, array index {j}: {keyStr} (OOP {k.RawValue})");
                    if (j < valueArrayObj.Fields.Length && !valueArrayObj.Fields[j].IsNil)
                    {
                        Console.WriteLine($"[LOOKUP]   DEBUG:   -> value at index {j}: OOP {valueArrayObj.Fields[j].RawValue}");
                    }
                }
            }
        }
        
        for (int i = 0; i < capacity; i++)
        {
            var keyFieldIndex = 2 + i;
            var key = dictObj.Fields[keyFieldIndex];
            
            if (SelectorEquals(key, selector))
            {
                // Found the selector, return the corresponding method from valueArray
                if (i < valueArrayObj.Fields.Length)
                {
                    var methodOop = valueArrayObj.Fields[i];
                    if (Config.DebugOptions.EnableMethodTrace && GetSelectorString(selector) == "initialize")
                    {
                        Console.WriteLine($"[LOOKUP]   DEBUG: FOUND at index {i}");
                        Console.WriteLine($"[LOOKUP]   DEBUG: valueArrayOop = {valueArrayOop}");
                        Console.WriteLine($"[LOOKUP]   DEBUG: valueArrayObj.Fields[{i}] = {methodOop}");
                        Console.WriteLine($"[LOOKUP]   DEBUG: Returning method OOP {methodOop.RawValue}");
                    }
                    return methodOop;
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
    
    /// <summary>
    /// Gets the class name for debug output.
    /// </summary>
    private string GetClassName(OOP classOop)
    {
        // Try to use the VM's GetClassName method if available
        if (fieldAccessor is VirtualMachine vm)
        {
            return vm.GetClassName(classOop);
        }
        return classOop.ToString();
    }
    
    /// <summary>
    /// Gets the selector string for debug output.
    /// </summary>
    private string GetSelectorString(OOP selector)
    {
        // Try to use the VM's GetSymbolString method if available
        if (symbolTable is VirtualMachine vm)
        {
            return vm.GetSymbolString(selector);
        }
        return selector.ToString();
    }
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

