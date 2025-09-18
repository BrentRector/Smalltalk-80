using System;
using System.Text;

namespace Smalltalk80.Interpreter.ObjectModel;
/// <summary>
/// Complete memory layout for a Smalltalk object.
/// Combines header and fields into a single coherent structure.
/// This is the fundamental unit of allocation in object memory.
/// </summary>
public sealed class ObjectLayout
{
    /// <summary>
    /// Object metadata (class, size, format, hash, GC flags).
    /// </summary>
    public ObjectHeader Header { get; }

    /// <summary>
    /// Object fields (instance variables and indexed fields).
    /// Can contain OOPs (object references) or raw data (bytes).
    /// </summary>
    public OOP[] Fields { get; }

    /// <summary>
    /// Creates a new object layout with the given header and field count.
    /// </summary>
    public ObjectLayout(ObjectHeader header, int fieldCount)
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
        Fields = new OOP[fieldCount];
        
        // Initialize all fields to nil
        Array.Fill(Fields, OOP.Nil);
    }

    /// <summary>
    /// Creates a new object layout for a class object.
    /// </summary>
    public static ObjectLayout ForClass(OOP classOOP, int fieldCount = 8) =>
        new(new ObjectHeader(classOOP, fieldCount, format: 0x10), fieldCount);

    /// <summary>
    /// Creates a new object layout for a regular object.
    /// </summary>
    public static ObjectLayout ForObject(OOP classOOP, int fieldCount) =>
        new(new ObjectHeader(classOOP, fieldCount, format: 0x10), fieldCount);

    /// <summary>
    /// Creates a new object layout for a byte array.
    /// </summary>
    public static ObjectLayout ForByteArray(OOP byteArrayClassOOP, int byteCount) =>
        new(new ObjectHeader(byteArrayClassOOP, byteCount, format: 0x60), byteCount);

    /// <summary>
    /// Creates a new object layout for a word array.
    /// </summary>
    public static ObjectLayout ForWordArray(OOP arrayClassOOP, int elementCount) =>
        new(new ObjectHeader(arrayClassOOP, elementCount, format: 0x30), elementCount);

    /// <summary>
    /// Gets a field value at the given index.
    /// </summary>
    public OOP GetField(int index) => (uint)index >= (uint)Fields.Length
        ? throw new ArgumentOutOfRangeException(nameof(index))
        : Fields[index];

    /// <summary>
    /// Sets a field value at the given index.
    /// </summary>
    public void SetField(int index, OOP value)
    {
        if ((uint)index >= (uint)Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        Fields[index] = value;
    }

    /// <summary>
    /// Checks if this object has pointer fields (vs bytes).
    /// </summary>
    public bool HasPointers => (Header.Format & 0x10) != 0;

    /// <summary>
    /// Checks if this object is indexable.
    /// </summary>
    public bool IsIndexable => (Header.Format & 0x20) != 0;

    /// <summary>
    /// Checks if this object contains bytes (vs words).
    /// </summary>
    public bool IsBytes => (Header.Format & 0x40) != 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Object[Class={Header.ClassOOP}, Fields={Fields.Length}]");
        
        var displayCount = Math.Min(Fields.Length, 5);
        for (int i = 0; i < displayCount; i++)
        {
            sb.AppendLine($"  [{i}] = {Fields[i]}");
        }
        
        if (Fields.Length > 5)
        {
            sb.AppendLine($"  ... and {Fields.Length - 5} more fields");
        }
        
        return sb.ToString();
    }
}