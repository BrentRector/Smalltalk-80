using System;
using System.Collections.Generic;

/// <summary>
/// Implements a method lookup cache to improve performance of message sends.
/// Reference: Smalltalk-80 specification Chapter 29
/// </summary>
public class MethodCache
{
    /// <summary>
    /// Cache entry storing a compiled method for a specific class/selector combination.
    /// </summary>
    private struct CacheEntry
    {
        public SmalltalkClass Class;
        public Symbol Selector;
        public CompiledMethod Method;
        public bool IsValid;
    }

    /// <summary>
    /// The method cache table. Size should be a power of 2 for efficient hashing.
    /// </summary>
    private CacheEntry[] cache = new CacheEntry[1024];

    /// <summary>
    /// Number of cache hits (for performance monitoring).
    /// </summary>
    public int Hits { get; private set; }

    /// <summary>
    /// Number of cache misses (for performance monitoring).
    /// </summary>
    public int Misses { get; private set; }

    /// <summary>
    /// Looks up a method in the cache.
    /// </summary>
    /// <param name="receiverClass">The class of the receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <returns>The cached method, or null if not cached.</returns>
    public CompiledMethod? Lookup(SmalltalkClass receiverClass, Symbol selector)
    {
        int index = HashFunction(receiverClass, selector);
        ref var entry = ref cache[index];

        if (entry.IsValid && 
            ReferenceEquals(entry.Class, receiverClass) && 
            ReferenceEquals(entry.Selector, selector))
        {
            Hits++;
            return entry.Method;
        }

        Misses++;
        return null;
    }

    /// <summary>
    /// Stores a method in the cache.
    /// </summary>
    /// <param name="receiverClass">The class of the receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="method">The compiled method.</param>
    public void Store(SmalltalkClass receiverClass, Symbol selector, CompiledMethod method)
    {
        int index = HashFunction(receiverClass, selector);
        cache[index] = new CacheEntry
        {
            Class = receiverClass,
            Selector = selector,
            Method = method,
            IsValid = true
        };
    }

    /// <summary>
    /// Flushes the entire method cache.
    /// This is called when classes are modified or when primitive 89 is executed.
    /// </summary>
    public void FlushCache()
    {
        for (int i = 0; i < cache.Length; i++)
        {
            cache[i].IsValid = false;
        }
    }

    /// <summary>
    /// Flushes cache entries for a specific class.
    /// This is called when a class is modified.
    /// </summary>
    /// <param name="classToFlush">The class whose entries should be removed.</param>
    public void FlushClass(SmalltalkClass classToFlush)
    {
        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i].IsValid && ReferenceEquals(cache[i].Class, classToFlush))
            {
                cache[i].IsValid = false;
            }
        }
    }

    /// <summary>
    /// Hash function for mapping class/selector pairs to cache indices.
    /// Uses XOR of class and selector hash codes, masked to cache size.
    /// </summary>
    /// <param name="receiverClass">The receiver class.</param>
    /// <param name="selector">The message selector.</param>
    /// <returns>Cache index.</returns>
    private int HashFunction(SmalltalkClass receiverClass, Symbol selector)
    {
        int classHash = receiverClass.GetHashCode();
        int selectorHash = selector.GetHashCode();
        return (classHash ^ selectorHash) & (cache.Length - 1);
    }

    /// <summary>
    /// Gets cache statistics for performance monitoring.
    /// </summary>
    /// <returns>Cache hit ratio as a percentage.</returns>
    public double GetHitRatio()
    {
        int total = Hits + Misses;
        return total == 0 ? 0.0 : (double)Hits / total * 100.0;
    }

    /// <summary>
    /// Resets cache statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Hits = 0;
        Misses = 0;
    }
}
