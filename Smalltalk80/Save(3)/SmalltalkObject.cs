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
    public SmalltalkClass? ClassPointer { get; set; }

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
    public SmalltalkObject(SmalltalkClass? cls, int fieldCount)
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
}