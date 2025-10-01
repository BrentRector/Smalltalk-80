namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk Array.
/// </summary>
public sealed class SArray : SObject
{
    private readonly SObject?[] elements;
    
    /// <summary>
    /// Initializes a new SArray with the specified size.
    /// </summary>
    public SArray(int size)
    {
        if (size < 0)
        {
            throw new ArgumentException("Array size cannot be negative");
        }
        elements = new SObject?[size];
    }
    
    /// <summary>
    /// Initializes a new SArray with the specified elements.
    /// </summary>
    public SArray(params SObject?[] elements)
    {
        this.elements = elements ?? [];
    }
    
    /// <summary>
    /// Gets the size of the array.
    /// </summary>
    public int Size => elements.Length;
    
    /// <summary>
    /// Gets the class of Array.
    /// </summary>
    public override SClass Class => SystemDictionary.ArrayClass;
    
    /// <summary>
    /// Gets an element at the specified index (1-based like Smalltalk).
    /// </summary>
    public SObject? At(int index)
    {
        if (index < 1 || index > elements.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Array index out of bounds");
        }
        return elements[index - 1];
    }
    
    /// <summary>
    /// Sets an element at the specified index (1-based like Smalltalk).
    /// </summary>
    public void AtPut(int index, SObject? value)
    {
        if (index < 1 || index > elements.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Array index out of bounds");
        }
        elements[index - 1] = value;
    }
    
    /// <summary>
    /// Gets the elements as a .NET array (0-based).
    /// </summary>
    public SObject?[] GetElements() => (SObject?[])elements.Clone();
    
    /// <summary>
    /// Creates an SArray from a collection of objects.
    /// </summary>
    public static SArray From(params SObject?[] elements) => new(elements);
    
    /// <summary>
    /// Converts this SArray to a display string.
    /// </summary>
    public override string ToDisplayString()
    {
        var items = elements.Select(e => e?.ToDisplayString() ?? "nil");
        return $"#({string.Join(" ", items)})";
    }
    
    /// <summary>
    /// Gets the hash code for this SArray.
    /// </summary>
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    
    /// <summary>
    /// Determines whether this SArray equals another object.
    /// </summary>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
}