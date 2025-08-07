using System;

/// <summary>
///     Represents a generic Smalltalk object instance.
/// </summary>
public class SmalltalkObject
{
    /// <summary>
    ///     Gets or sets the object's class pointer. This may be <c>null</c>
    ///     during deserialization until the class table is resolved.
    /// </summary>
    public SmalltalkObject? ClassPointer { get; set; }

    /// <summary>
    ///     Gets the encoded object pointer assigned by <see cref="ObjectMemory"/>.
    /// </summary>
    public int Oop { get; internal set; }

    /// <summary>
    ///     Gets the raw field values stored in the object.
    /// </summary>
    public object?[] Fields { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmalltalkObject"/> class.
    /// </summary>
    /// <param name="cls">The object's class pointer.</param>
    /// <param name="fieldCount">The number of fields to allocate.</param>
    public SmalltalkObject(SmalltalkObject? cls, int fieldCount)
    {
        ClassPointer = cls;
        Fields = new object?[fieldCount];
        ObjectMemory.Register(this);
    }

    /// <summary>
    ///     Reads the value of the specified field.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field value.</returns>
    public object? GetField(int index) => Fields[index];

    /// <summary>
    ///     Sets the value of the specified field.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <param name="value">The value to store.</param>
    public void SetField(int index, object? value) => Fields[index] = value;

    /// <summary>
    ///     STUB: Looks up a method - simplified for shadow class removal.
    /// </summary>
    public CompiledMethod? Lookup(Symbol selector)
    {
        // Stub implementation - would need to examine VM object fields
        // for method dictionary in a full implementation
        return null;
    }

    /// <summary>
    ///     STUB: Gets superclass - simplified for shadow class removal.  
    /// </summary>
    public SmalltalkObject? Superclass => ClassPointer; // Simplified stub

    /// <summary>
    ///     STUB: Instance variables list - simplified for shadow class removal.
    /// </summary>
    public List<string> InstanceVariables { get; } = new();

    /// <summary>
    ///     STUB: Class variables list - simplified for shadow class removal.
    /// </summary>
    public List<string> ClassVariables { get; } = new();

    /// <summary>
    ///     STUB: Method dictionary - simplified for shadow class removal.
    /// </summary>
    public Dictionary<Symbol, CompiledMethod> MethodDictionary { get; } = new();

    /// <summary>
    ///     STUB: Metaclass reference - simplified for shadow class removal.
    /// </summary>
    public SmalltalkObject? Metaclass { get; set; }
    
    /// <summary>
    /// Checks if this object appears to be a SmallInteger.
    /// </summary>
    public bool IsSmallInteger => false; // TODO: Implement proper SmallInteger check
    
    /// <summary>
    /// Gets the SmallInteger value if this is a SmallInteger.
    /// </summary>
    public int SmallIntegerValue => 0; // TODO: Implement proper SmallInteger value extraction
    
    /// <summary>
    /// Tries to get a field value by name.
    /// </summary>
    public bool TryGetField(string fieldName, out object? value)
    {
        // TODO: Implement field lookup by name
        value = null;
        return false;
    }
    
    /// <summary>
    /// Tries to get the string value if this object represents a string.
    /// </summary>
    public bool TryGetStringValue(out string value)
    {
        // TODO: Implement proper string extraction
        value = string.Empty;
        return false;
    }
}