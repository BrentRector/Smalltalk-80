using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Interface for runtime heap access during VM execution.
/// Provides safe, read-only access to Smalltalk objects and metadata.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
///
/// Lifecycle:
/// - Created during VM initialization (after cold start completes)
/// - Used exclusively during runtime execution
/// - Caches invalidated by primitive 89 (flushCache)
/// </summary>
public interface IRuntimeHeapAccessor
{
    /// <summary>
    /// Invalidates all runtime caches.
    /// Called by primitive 89 (flushCache) when classes are modified at runtime.
    /// </summary>
    void InvalidateAllCaches();

    /// <summary>
    /// Gets a class OOP by name, using cache if valid.
    /// Searches the Smalltalk global dictionary for the class.
    /// </summary>
    /// <param name="className">The name of the class to look up.</param>
    /// <returns>The class OOP, or nil if not found.</returns>
    SmalltalkOOP GetClass(string className);

    /// <summary>
    /// Interns a string as a Symbol in the Smalltalk heap.
    /// Searches USTable for existing symbol or returns nil if not found.
    /// </summary>
    /// <param name="symbolName">The symbol name to intern.</param>
    /// <returns>The Symbol OOP, or nil if not found.</returns>
    SmalltalkOOP InternSymbol(string symbolName);

    /// <summary>
    /// Looks up a global variable by name using linear search with byte comparison.
    /// Used when hash-based lookup is not possible (Symbol>>hash is OOP-based).
    /// </summary>
    /// <param name="dictOOP">The dictionary OOP to search.</param>
    /// <param name="globalName">The global variable name to find.</param>
    /// <returns>The value OOP, or nil if not found.</returns>
    SmalltalkOOP LookupGlobalByName(SmalltalkOOP dictOOP, string globalName);

    /// <summary>
    /// Builds complete field layout map for all classes by walking the Smalltalk heap.
    /// Reads ClassDescription.instanceVariables for each class in the hierarchy.
    /// Enables full runtime reflection and supports runtime class modification.
    /// </summary>
    /// <returns>Dictionary mapping "class:ClassName" to list of field names.</returns>
    Dictionary<string, List<string>> BuildFieldMap();

    /// <summary>
    /// Gets the field index for a named field in a class by walking the heap.
    /// This is used by primitives at runtime - the heap is the source of truth.
    /// </summary>
    /// <param name="classOOP">The class OOP to query.</param>
    /// <param name="fieldName">The field name to find.</param>
    /// <returns>The zero-based field index, or -1 if not found.</returns>
    int GetFieldIndex(SmalltalkOOP classOOP, string fieldName);

    /// <summary>
    /// Looks up a global variable following the canonical Smalltalk-80 lookup chain:
    /// 1. Search SystemDictionary (Smalltalk)
    /// 2. If not found, search Undeclared
    /// 3. If found in Undeclared and migrate=true, move it to SystemDictionary
    /// 4. If not found anywhere, return nil
    /// </summary>
    /// <param name="globalName">The global variable name to find.</param>
    /// <param name="migrate">If true, migrate from Undeclared to SystemDictionary when found there.</param>
    /// <returns>The Association OOP, or nil if not found.</returns>
    SmalltalkOOP LookupGlobalAssociation(string globalName, bool migrate = false);

    /// <summary>
    /// Looks up a global variable's value following the canonical lookup chain.
    /// Convenience wrapper around LookupGlobalAssociation that extracts the value.
    /// </summary>
    /// <param name="globalName">The global variable name to find.</param>
    /// <returns>The value OOP, or nil if not found.</returns>
    SmalltalkOOP LookupGlobalValue(string globalName);

    /// <summary>
    /// Sets a global variable's value, creating the Association if necessary.
    /// Follows the canonical lookup chain: SystemDictionary → Undeclared → create new.
    /// If found in Undeclared, migrates to SystemDictionary.
    /// </summary>
    /// <param name="globalName">The global variable name.</param>
    /// <param name="valueOOP">The value to set.</param>
    /// <returns>The Association OOP (existing or newly created).</returns>
    SmalltalkOOP SetGlobalValue(string globalName, SmalltalkOOP valueOOP);
}
