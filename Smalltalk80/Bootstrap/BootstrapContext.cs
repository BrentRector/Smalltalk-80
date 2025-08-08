using System;
using System.Collections.Generic;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Encapsulates C# shadow dictionaries used ONLY during the initial bootstrap phase.
/// These dictionaries are necessary to track objects before the Smalltalk SystemDictionary
/// and message sending infrastructure are operational.
/// 
/// IMPORTANT: This class should ONLY be used during Phase 1 bootstrap.
/// After Phase 1 completes and the Smalltalk SystemDictionary exists,
/// all lookups should use the Smalltalk dictionary via primitive operations.
/// </summary>
internal sealed class BootstrapContext
{
    private readonly Dictionary<string, int> temporaryGlobals = new();
    private readonly Dictionary<string, int> temporaryClasses = new();
    private readonly Dictionary<string, int> temporarySymbols = new();
    private bool isActive = true;
    private int smalltalkDictionaryOop = -1;

    /// <summary>
    /// Check if the bootstrap context is still active.
    /// Once deactivated, all operations should fail.
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Register a temporary global during bootstrap.
    /// This should ONLY be called during Phase 1.
    /// </summary>
    public void RegisterTemporaryGlobal(string name, int oop)
    {
        if (!isActive)
            throw new InvalidOperationException("Bootstrap context has been deactivated. Use Smalltalk dictionary instead.");
        
        temporaryGlobals[name] = oop;
        
        // Track the Smalltalk dictionary specially
        if (name == "Smalltalk")
        {
            smalltalkDictionaryOop = oop;
        }
    }

    /// <summary>
    /// Register a temporary class during bootstrap.
    /// This should ONLY be called during Phase 1.
    /// </summary>
    public void RegisterTemporaryClass(string name, int oop)
    {
        if (!isActive)
            throw new InvalidOperationException("Bootstrap context has been deactivated. Use Smalltalk dictionary instead.");
        
        temporaryClasses[name] = oop;
    }

    /// <summary>
    /// Register a temporary symbol during bootstrap.
    /// This ensures symbol uniqueness during Phase 1.
    /// </summary>
    public bool TryGetTemporarySymbol(string value, out int oop)
    {
        if (!isActive)
            throw new InvalidOperationException("Bootstrap context has been deactivated. Use Smalltalk dictionary instead.");
        
        return temporarySymbols.TryGetValue(value, out oop);
    }

    /// <summary>
    /// Register a new symbol in the temporary table.
    /// </summary>
    public void RegisterTemporarySymbol(string value, int oop)
    {
        if (!isActive)
            throw new InvalidOperationException("Bootstrap context has been deactivated. Use Smalltalk dictionary instead.");
        
        temporarySymbols[value] = oop;
    }

    /// <summary>
    /// Lookup a temporary global during bootstrap.
    /// Returns -1 if not found.
    /// </summary>
    public int LookupTemporaryGlobal(string name)
    {
        if (!isActive)
            return -1;
        
        return temporaryGlobals.TryGetValue(name, out var oop) ? oop : -1;
    }

    /// <summary>
    /// Lookup a temporary class during bootstrap.
    /// Returns -1 if not found.
    /// </summary>
    public int LookupTemporaryClass(string name)
    {
        if (!isActive)
            return -1;
        
        return temporaryClasses.TryGetValue(name, out var oop) ? oop : -1;
    }

    /// <summary>
    /// Get the Smalltalk SystemDictionary OOP if it has been registered.
    /// </summary>
    public int GetSmalltalkDictionaryOop()
    {
        return smalltalkDictionaryOop;
    }

    /// <summary>
    /// Migrate all temporary entries to the Smalltalk SystemDictionary
    /// and deactivate this bootstrap context.
    /// After this call, all methods will throw or return -1.
    /// </summary>
    public void MigrateToSmalltalkDictionary(Action<string, int> registerInSmalltalk)
    {
        if (!isActive)
            return;

        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Migrating temporary bootstrap dictionaries to Smalltalk SystemDictionary...");
        
        // Migrate all globals to Smalltalk dictionary
        foreach (var kvp in temporaryGlobals)
        {
            // Register all globals including Smalltalk itself
            // It's important to have "Smalltalk" as an entry in the Smalltalk dictionary
            registerInSmalltalk(kvp.Key, kvp.Value);
        }
        
        // Migrate all classes to Smalltalk dictionary
        foreach (var kvp in temporaryClasses)
        {
            registerInSmalltalk(kvp.Key, kvp.Value);
        }
        
        JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Migrated {temporaryGlobals.Count} globals and {temporaryClasses.Count} classes");
        
        // Clear and deactivate
        temporaryGlobals.Clear();
        temporaryClasses.Clear();
        temporarySymbols.Clear();
        isActive = false;
        
        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Bootstrap context deactivated. All lookups now use Smalltalk SystemDictionary.");
    }

    /// <summary>
    /// Get statistics about the temporary dictionaries (for debugging).
    /// </summary>
    public (int globals, int classes, int symbols) GetStatistics()
    {
        return (temporaryGlobals.Count, temporaryClasses.Count, temporarySymbols.Count);
    }
}