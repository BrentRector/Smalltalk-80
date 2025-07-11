using System;
using System.Collections.Generic;

/// <summary>
///     Provides a minimal set of global Smalltalk objects used by the interpreter.
/// </summary>
public static class StandardLibrary
{
    private static readonly Dictionary<string, SmalltalkObject> globals = new();

    /// <summary>
    ///     Initializes the standard library with core classes.
    /// </summary>
    public static void Initialize()
    {
        var objectClass = SystemDictionary.DefineClass("Object");
        globals["Object"] = new SmalltalkObject(objectClass, 0);
    }

    /// <summary>
    ///     Retrieves a global object by name.
    /// </summary>
    /// <param name="name">The name of the global to fetch.</param>
    /// <returns>The requested global object.</returns>
    public static SmalltalkObject Get(string name) => globals[name];
}