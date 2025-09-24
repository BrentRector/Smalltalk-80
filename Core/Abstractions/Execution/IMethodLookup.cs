using Core.Memory;

namespace Core.Abstractions.Execution;

/// <summary>
/// Interface for method lookup in the Smalltalk class hierarchy.
/// </summary>
public interface IMethodLookup
{
    /// <summary>
    /// Looks up a method in a class hierarchy.
    /// </summary>
    /// <param name="classOop">The class to start lookup from.</param>
    /// <param name="selector">The selector to look up.</param>
    /// <returns>The compiled method OOP, or nil if not found.</returns>
    OOP? LookupMethod(OOP classOop, string selector);

    /// <summary>
    /// Looks up a method starting from the superclass.
    /// </summary>
    /// <param name="classOop">The class whose superclass to start from.</param>
    /// <param name="selector">The selector to look up.</param>
    /// <returns>The compiled method OOP, or nil if not found.</returns>
    OOP? LookupMethodInSuper(OOP classOop, string selector);

    /// <summary>
    /// Flushes the method cache.
    /// </summary>
    void FlushCache();

    /// <summary>
    /// Adds a method to the cache.
    /// </summary>
    void CacheMethod(OOP classOop, string selector, OOP methodOop);
}