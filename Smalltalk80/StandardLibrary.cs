using System;
using System.Collections.Generic;

/// <summary>
///     Provides a minimal set of global Smalltalk objects used by the interpreter.
///     STUB IMPLEMENTATION - Simplified for shadow class removal.
/// </summary>
public static class StandardLibrary
{
    private static readonly Dictionary<string, SmalltalkObject> globals = new();

    /// <summary>
    ///     Initializes the standard library with core classes.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[STDLIB] Stub initialization - shadow classes removed");
        // Stub implementation - no longer creates shadow classes
        // All class creation now happens via ObjectMemory only
    }

    /// <summary>
    ///     Gets or sets a global object by name.
    /// </summary>
    public static SmalltalkObject? GetGlobal(string name)
    {
        globals.TryGetValue(name, out var result);
        return result;
    }

    /// <summary>
    ///     Sets a global object by name.
    /// </summary>
    public static void SetGlobal(string name, SmalltalkObject value)
    {
        globals[name] = value;
    }

    /// <summary>
    ///     Stub methods - simplified for shadow class removal
    /// </summary>
    private static void InitializeNumberClasses() { /* stub */ }
    private static void InitializeBooleanClasses() { /* stub */ }
    private static void InitializeCollectionClasses() { /* stub */ }
    private static void InitializeControlClasses() { /* stub */ }
    private static void InitializeFileSystemClasses() { /* stub */ }
    private static void InitializeUtilityClasses() { /* stub */ }
    
    public static bool TryGetGlobal(string name, out SmalltalkObject? value)
    {
        return globals.TryGetValue(name, out value);
    }
}