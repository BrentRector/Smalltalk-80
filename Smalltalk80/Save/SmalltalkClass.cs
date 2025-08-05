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
    ///     Gets the dictionary mapping class-side method selectors to compiled methods.
    /// </summary>
    public Dictionary<Symbol, CompiledMethod> ClassMethodDictionary { get; } = new();

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
        if (MethodDictionary.TryGetValue(selector, out var method))
        {
            return method;
        }

        return Superclass?.Lookup(selector);
    }

    /// <summary>
    ///     Searches the class hierarchy for a class-side method matching the given selector.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <returns>The <see cref="CompiledMethod"/> if found; otherwise <c>null</c>.</returns>
    public CompiledMethod? LookupClassMethod(Symbol selector)
    {
        if (ClassMethodDictionary.TryGetValue(selector, out var method))
        {
            return method;
        }

        return Superclass?.LookupClassMethod(selector);
    }
}