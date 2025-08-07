using System.Collections.Generic;

/// <summary>
///     Represents a Smalltalk class object.
/// </summary>
public class SmalltalkClass : SmalltalkObject
{
    /// <summary>
    ///     Gets the dictionary mapping method selectors to compiled methods.
    /// </summary>
    public Dictionary<Symbol, CompiledMethod> MethodDictionary { get; } = new();

    /// <summary>
    ///     Gets or sets the class that this metaclass describes (only set for metaclasses).
    ///     This corresponds to the 'thisClass' instance variable in Smalltalk Metaclass.
    /// </summary>
    public SmalltalkClass? ThisClass { get; set; }

    /// <summary>
    ///     Gets the list of instance variable names defined on this class.
    /// </summary>
    public List<string> InstanceVariables { get; } = new();

    /// <summary>
    ///     Gets the list of class variable names shared by all instances of this class.
    /// </summary>
    public List<string> ClassVariables { get; } = new();

    /// <summary>
    ///     Gets the names of any shared variable pools used by this class.
    /// </summary>
    public List<string> PoolDictionaries { get; } = new();

    /// <summary>
    ///     Gets or sets the organizational category this class belongs to.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the superclass of this class.
    /// </summary>
    public SmalltalkClass? Superclass { get; set; }

    /// <summary>
    ///     Gets or sets the metaclass of this class (the class of the class).
    /// </summary>
    public SmalltalkClass? Metaclass { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmalltalkClass"/> class.
    /// </summary>
    public SmalltalkClass() : base(null, 0)
    {
    }

    /// <summary>
    ///     Searches the class hierarchy for a method matching the given selector.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <returns>The <see cref="CompiledMethod"/> if found; otherwise <c>null</c>.</returns>
    public CompiledMethod? Lookup(Symbol selector)
    {
        return LookupWithVisited(selector, new HashSet<SmalltalkClass>());
    }

    /// <summary>
    ///     Internal lookup method with recursion detection.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <param name="visited">Set of already visited classes to detect cycles.</param>
    /// <returns>The <see cref="CompiledMethod"/> if found; otherwise <c>null</c>.</returns>
    private CompiledMethod? LookupWithVisited(Symbol selector, HashSet<SmalltalkClass> visited)
    {
        // Detect infinite recursion
        if (visited.Contains(this))
        {
            throw new InvalidOperationException($"Circular superclass reference detected while looking up {selector}. Class hierarchy contains cycle involving class with {MethodDictionary.Count} methods.");
        }

        visited.Add(this);

        if (MethodDictionary.TryGetValue(selector, out var method))
        {
            return method;
        }

        return Superclass?.LookupWithVisited(selector, visited);
    }

    /// <summary>
    ///     Determines if this class is a metaclass.
    /// </summary>
    /// <returns>True if this is a metaclass; otherwise false.</returns>
    public bool IsMetaclass()
    {
        // A class is a metaclass if it has ThisClass set or if its class pointer is Metaclass
        if (ThisClass != null)
            return true;
            
        // Check if this class's class is Metaclass
        if (ClassPointer != null && SystemDictionary.TryGetClass("Metaclass", out var metaclassClass))
            return ClassPointer == metaclassClass || ClassPointer.ClassPointer == metaclassClass;
            
        return false;
    }
}