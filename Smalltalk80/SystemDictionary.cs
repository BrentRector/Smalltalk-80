using System;
using System.Collections.Generic;

/// <summary>
///     Global repository mapping class names to <see cref="SmalltalkClass"/> instances.
/// </summary>
public static class SystemDictionary
{
    private static readonly Dictionary<string, SmalltalkClass> classes = new(StringComparer.Ordinal);

    /// <summary>
    ///     Registers a new class object under the specified name. If a class with the
    ///     name already exists it is returned instead.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="superclass">The optional superclass reference.</param>
    /// <param name="instanceVariables">Names of instance variables for the class.</param>
    /// <returns>The created or existing <see cref="SmalltalkClass"/>.</returns>
    public static SmalltalkClass DefineClass(
        string name,
        SmalltalkClass? superclass = null,
        IReadOnlyList<string>? instanceVariables = null)
    {
        if (classes.TryGetValue(name, out var existing))
        {
            if (superclass is not null && existing.Superclass is null)
                existing.Superclass = superclass;
            if (instanceVariables is not null)
            {
                foreach (var ivar in instanceVariables)
                {
                    if (!existing.InstanceVariables.Contains(ivar))
                        existing.InstanceVariables.Add(ivar);
                }
            }
            return existing;
        }

        var cls = new SmalltalkClass { Superclass = superclass };
        if (instanceVariables is not null)
            cls.InstanceVariables.AddRange(instanceVariables);
        classes[name] = cls;
        return cls;
    }

    /// <summary>
    ///     Gets the class object registered under the provided name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <returns>The <see cref="SmalltalkClass"/> instance.</returns>
    public static SmalltalkClass GetClass(string name) => classes[name];

    /// <summary>
    ///     Attempts to retrieve a class with the given name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="cls">When this method returns, contains the class object if found.</param>
    /// <returns><c>true</c> if the class exists; otherwise <c>false</c>.</returns>
    public static bool TryGetClass(string name, out SmalltalkClass cls) => classes.TryGetValue(name, out cls!);

    /// <summary>
    ///     Gets a read-only view of all classes currently registered in the
    ///     system dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, SmalltalkClass> AllClasses => classes;
}
