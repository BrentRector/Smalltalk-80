using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.VM.Interfaces;

/// <summary>
/// Represents the immutable state of a Smalltalk-80 Virtual Machine.
/// This is created by the heap builder and passed to the VM for execution.
/// </summary>
public sealed record VMState
{
    /// <summary>
    /// Gets the initialized object memory containing all objects.
    /// </summary>
    public required ObjectMemory Memory { get; init; }
    
    /// <summary>
    /// Gets the Special Objects Array containing well-known system objects.
    /// </summary>
    public required OOP[] SpecialObjectsArray { get; init; }
    
    /// <summary>
    /// Gets the symbol table mapping symbol strings to their OOPs.
    /// </summary>
    public required IReadOnlyDictionary<string, OOP> SymbolTable { get; init; }
    
    /// <summary>
    /// Gets the global variables dictionary.
    /// </summary>
    public required IReadOnlyDictionary<string, OOP> GlobalVariables { get; init; }
    
    /// <summary>
    /// Gets the method cache for performance optimization.
    /// </summary>
    public IMethodCache? MethodCache { get; init; }
    
    /// <summary>
    /// Gets metadata about the state creation.
    /// </summary>
    public required VMStateMetadata Metadata { get; init; }
}

/// <summary>
/// Metadata about VM state creation.
/// </summary>
public sealed record VMStateMetadata
{
    /// <summary>
    /// Gets the timestamp when this state was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// Gets the source of this state (e.g., "ColdStart", "ImageFile", "Snapshot").
    /// </summary>
    public required string Source { get; init; }
    
    /// <summary>
    /// Gets the version of the Smalltalk system.
    /// </summary>
    public required string SystemVersion { get; init; }
    
    /// <summary>
    /// Gets optional source file path if applicable.
    /// </summary>
    public string? SourcePath { get; init; }
    
    /// <summary>
    /// Gets the number of classes in the system.
    /// </summary>
    public required int ClassCount { get; init; }
    
    /// <summary>
    /// Gets the number of methods in the system.
    /// </summary>
    public required int MethodCount { get; init; }
    
    /// <summary>
    /// Gets the total memory used in bytes.
    /// </summary>
    public required long MemoryUsedBytes { get; init; }
}

/// <summary>
/// Interface for method caching to improve performance.
/// </summary>
public interface IMethodCache
{
    /// <summary>
    /// Looks up a method in the cache.
    /// </summary>
    /// <param name="classOop">The class to search from.</param>
    /// <param name="selectorOop">The selector to find.</param>
    /// <returns>The compiled method OOP or Nil if not found.</returns>
    OOP Lookup(OOP classOop, OOP selectorOop);
    
    /// <summary>
    /// Adds a method to the cache.
    /// </summary>
    /// <param name="classOop">The class containing the method.</param>
    /// <param name="selectorOop">The method selector.</param>
    /// <param name="methodOop">The compiled method.</param>
    void Cache(OOP classOop, OOP selectorOop, OOP methodOop);
    
    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Statistics about method cache performance.
/// </summary>
public sealed record CacheStatistics
{
    /// <summary>
    /// Gets the total number of cache hits.
    /// </summary>
    public long Hits { get; init; }
    
    /// <summary>
    /// Gets the total number of cache misses.
    /// </summary>
    public long Misses { get; init; }
    
    /// <summary>
    /// Gets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double HitRatio => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
    
    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    public int EntryCount { get; init; }
    
    /// <summary>
    /// Gets the cache capacity.
    /// </summary>
    public int Capacity { get; init; }
}