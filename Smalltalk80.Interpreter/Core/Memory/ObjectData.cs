using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.Memory;

/// <summary>
/// Data structure for a Smalltalk object.
/// Contains class reference, format, instance variables, and indexed data.
/// </summary>
public sealed class ObjectData
{
    /// <summary>Class reference for this object.</summary>
    public SmalltalkOOP ClassRef { get; set; }

    /// <summary>Storage format determining how indexed data is interpreted.</summary>
    public ObjectFormat Format { get; set; }

    /// <summary>Named instance variables (null for zero-length).</summary>
    public SmalltalkOOP[]? InstanceVariables { get; set; }

    /// <summary>Indexed data: byte[], ushort[], or SmalltalkOOP[] depending on Format.</summary>
    public object? IndexedData { get; set; }

    /// <summary>GC mark bit for mark-and-sweep collection.</summary>
    public bool Marked { get; set; }
}
