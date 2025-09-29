namespace Smalltalk80.Interpreter.Execution;

using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Method cache for accelerating method lookup in the Smalltalk-80 VM.
/// Uses a simple hash table with linear probing for collision resolution.
/// </summary>
public sealed class MethodCache
{
    private const int CacheSize = 1024; // Power of 2 for fast masking
    private const int CacheMask = CacheSize - 1;
    private const int MaxProbeCount = 8; // Limit probing to avoid long searches
    
    private readonly MethodCacheEntry[] entries = new MethodCacheEntry[CacheSize];
    private long hits;
    private long misses;
    private long collisions;
    
    /// <summary>
    /// Tries to get a cached method.
    /// </summary>
    /// <param name="receiverClass">The class of the receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="method">The cached method if found.</param>
    /// <returns>True if the method was found in the cache; otherwise, false.</returns>
    public bool TryGet(SClass receiverClass, string selector, out SCompiledMethod? method)
    {
        var key = new MethodCacheKey(receiverClass, selector);
        var index = Hash(key) & CacheMask;
        var entry = entries[index];
        
        if (entry.Key.Equals(key))
        {
            hits++;
            method = entry.Method;
            return true;
        }
        
        // Linear probe for collision resolution
        var probeCount = 0;
        while (probeCount < MaxProbeCount)
        {
            index = (index + 1) & CacheMask;
            entry = entries[index];
            
            if (entry.Key.Equals(key))
            {
                hits++;
                collisions++;
                method = entry.Method;
                return true;
            }
            
            if (entry.Key.ReceiverClass == null)
                break; // Empty slot found, method not cached
            
            probeCount++;
        }
        
        misses++;
        method = null;
        return false;
    }
    
    /// <summary>
    /// Caches a method lookup result.
    /// </summary>
    /// <param name="receiverClass">The class of the receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="method">The method to cache (can be null for not found).</param>
    public void Put(SClass receiverClass, string selector, SCompiledMethod? method)
    {
        var key = new MethodCacheKey(receiverClass, selector);
        var index = Hash(key) & CacheMask;
        var entry = entries[index];
        
        // Direct placement if empty or same key
        if (entry.Key.ReceiverClass == null || entry.Key.Equals(key))
        {
            entries[index] = new MethodCacheEntry(key, method);
            return;
        }
        
        // Linear probe for an empty slot
        var probeCount = 0;
        while (probeCount < MaxProbeCount)
        {
            index = (index + 1) & CacheMask;
            entry = entries[index];
            
            if (entry.Key.ReceiverClass == null || entry.Key.Equals(key))
            {
                entries[index] = new MethodCacheEntry(key, method);
                return;
            }
            
            probeCount++;
        }
        
        // Cache full in this region, evict the original slot
        index = Hash(key) & CacheMask;
        entries[index] = new MethodCacheEntry(key, method);
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
    /// Invalidates cache entries for a specific class.
    /// Called when methods are added or removed from a class.
    /// </summary>
    /// <param name="targetClass">The class whose methods have changed.</param>
    public void InvalidateClass(SClass targetClass)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].Key.ReceiverClass == targetClass)
            {
                entries[i] = default;
            }
        }
    }
    
    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>The current cache statistics.</returns>
    public MethodCacheStatistics GetStatistics() =>
        new(hits, misses, collisions, CacheSize);
    
    /// <summary>
    /// Computes hash for a cache key.
    /// </summary>
    private static int Hash(MethodCacheKey key)
    {
        // Simple hash combining class hash code and selector hash
        var classHash = (uint)(key.ReceiverClass?.GetHashCode() ?? 0);
        var selectorHash = (uint)(key.Selector?.GetHashCode() ?? 0);
        
        // Mix the bits using MurmurHash-inspired mixing
        var hash = classHash ^ (selectorHash << 16) ^ (selectorHash >> 16);
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;
        
        return (int)hash;
    }
}

/// <summary>
/// Key for method cache lookup.
/// </summary>
internal readonly record struct MethodCacheKey(SClass? ReceiverClass, string? Selector);

/// <summary>
/// Entry in the method cache.
/// </summary>
internal readonly record struct MethodCacheEntry(MethodCacheKey Key, SCompiledMethod? Method);

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
        $"MethodCache[Size={CacheSize}, Hits={Hits}, Misses={Misses}, HitRatio={HitRatio:F2}%, Collisions={Collisions}]";
}