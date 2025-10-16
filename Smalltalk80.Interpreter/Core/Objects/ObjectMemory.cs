using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Object memory management for the Smalltalk-80 interpreter.
/// This class provides the interface between SmalltalkOOP tagged pointers and actual object data.
/// </summary>
public static class ObjectMemory
{
    /// <summary>
    /// Data structure for a Smalltalk object.
    /// </summary>
    public class ObjectData
    {
        public SmalltalkOOP ClassRef { get; set; }
        public ObjectFormat Format { get; set; }
        public SmalltalkOOP[]? InstanceVariables { get; set; }
        public object? IndexedData { get; set; }  // byte[], ushort[], or SmalltalkOOP[]
        public bool Marked { get; set; }  // GC mark bit
    }

    /// <summary>
    /// Gets the SObject instance for a given OOP (compatibility method).
    /// </summary>
    public static SObject? GetObject(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetObject not yet implemented");
    }

    /// <summary>
    /// Gets the OOP for a given SObject instance (compatibility method).
    /// </summary>
    public static int GetOop(SObject obj)
    {
        throw new NotImplementedException("ObjectMemory.GetOop not yet implemented");
    }

    /// <summary>
    /// Gets the class reference for an object.
    /// </summary>
    public static SmalltalkOOP GetClass(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetClass not yet implemented");
    }

    /// <summary>
    /// Gets the format of an object.
    /// </summary>
    public static ObjectFormat GetFormat(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetFormat not yet implemented");
    }

    /// <summary>
    /// Gets the class name for an object.
    /// </summary>
    public static string? GetClassName(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetClassName not yet implemented");
    }

    /// <summary>
    /// Gets an instance variable by index.
    /// </summary>
    public static SmalltalkOOP GetInstanceVariable(int oop, int index)
    {
        throw new NotImplementedException("ObjectMemory.GetInstanceVariable not yet implemented");
    }

    /// <summary>
    /// Sets an instance variable by index.
    /// </summary>
    public static void SetInstanceVariable(int oop, int index, SmalltalkOOP value)
    {
        throw new NotImplementedException("ObjectMemory.SetInstanceVariable not yet implemented");
    }

    /// <summary>
    /// Gets the number of instance variables.
    /// </summary>
    public static int GetInstanceVariableCount(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetInstanceVariableCount not yet implemented");
    }

    /// <summary>
    /// Gets an indexed byte.
    /// </summary>
    public static byte GetIndexedByte(int oop, int index)
    {
        throw new NotImplementedException("ObjectMemory.GetIndexedByte not yet implemented");
    }

    /// <summary>
    /// Sets an indexed byte.
    /// </summary>
    public static void SetIndexedByte(int oop, int index, byte value)
    {
        throw new NotImplementedException("ObjectMemory.SetIndexedByte not yet implemented");
    }

    /// <summary>
    /// Gets an indexed word.
    /// </summary>
    public static ushort GetIndexedWord(int oop, int index)
    {
        throw new NotImplementedException("ObjectMemory.GetIndexedWord not yet implemented");
    }

    /// <summary>
    /// Sets an indexed word.
    /// </summary>
    public static void SetIndexedWord(int oop, int index, ushort value)
    {
        throw new NotImplementedException("ObjectMemory.SetIndexedWord not yet implemented");
    }

    /// <summary>
    /// Gets an indexed pointer.
    /// </summary>
    public static SmalltalkOOP GetIndexedPointer(int oop, int index)
    {
        throw new NotImplementedException("ObjectMemory.GetIndexedPointer not yet implemented");
    }

    /// <summary>
    /// Sets an indexed pointer.
    /// </summary>
    public static void SetIndexedPointer(int oop, int index, SmalltalkOOP value)
    {
        throw new NotImplementedException("ObjectMemory.SetIndexedPointer not yet implemented");
    }

    /// <summary>
    /// Gets the size of the indexed portion.
    /// </summary>
    public static int GetIndexedSize(int oop)
    {
        throw new NotImplementedException("ObjectMemory.GetIndexedSize not yet implemented");
    }
}
