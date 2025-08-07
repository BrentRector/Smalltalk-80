using System;
using System.Collections.Generic;

/// <summary>
/// STUB IMPLEMENTATION - Minimal compatibility layer for shadow class removal.
/// Provides basic global variable management without C# shadow classes.
/// </summary>
public static class SystemDictionary
{
    private static readonly Dictionary<string, SmalltalkObject> globals = new();

    /// <summary>
    /// Stub method - returns false since shadow classes removed.
    /// </summary>
    public static bool TryGetClass(string name, out SmalltalkObject? cls)
    {
        cls = null;
        return false;
    }

    /// <summary>
    /// Stub method - returns null since shadow classes removed.
    /// </summary>
    public static SmalltalkObject? GetClass(string name) => null;

    /// <summary>
    /// Stub method - no-op since shadow classes removed.
    /// </summary>
    public static SmalltalkObject DefineClass(string name, 
        SmalltalkObject? superclass = null,
        IReadOnlyList<string>? instanceVariables = null,
        IReadOnlyList<string>? classVariables = null,
        IReadOnlyList<string>? poolDictionaries = null,
        string? category = null)
    {
        Console.WriteLine($"[SYSTEMDICT] Stub DefineClass: {name} - shadow classes removed");
        var stubClass = new SmalltalkObject(null, 0);
        globals[name] = stubClass;
        return stubClass;
    }

    /// <summary>
    /// Empty collection - shadow classes removed.
    /// </summary>
    public static IReadOnlyDictionary<string, SmalltalkObject> AllClasses => 
        new Dictionary<string, SmalltalkObject>();

    /// <summary>
    /// Stub methods for compatibility.
    /// </summary>
    public static void Clear() => globals.Clear();
    public static void InitializeGlobals() { /* stub */ }
    public static void InitializeMetaclassSystem() { /* stub */ }
    public static void SetGlobal(string name, object value) => globals[name] = value as SmalltalkObject ?? new SmalltalkObject(null, 0);
    public static bool TryGetGlobal(string name, out object? value) { value = globals.GetValueOrDefault(name); return value != null; }
}