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
    ///     Gets the list of instance variable names defined on this class.
    /// </summary>
    public List<string> InstanceVariables { get; } = new();

    /// <summary>
    ///     Gets or sets the superclass of this class.
    /// </summary>
    public SmalltalkClass? Superclass { get; set; }

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
}