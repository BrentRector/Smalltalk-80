namespace Smalltalk80.Interpreter.Core.Objects;

using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents an instance of a Smalltalk class with instance variables.
/// Per Blue Book: Generic object structure that VM understands.
///
/// VM Knowledge (Object Header):
/// - Object has class pointer
/// - Object has format type (FixedPointer, PointerIndexed, ByteIndexed, etc.)
/// - Object has named instance variables (SObjectRef[])
/// - Object may have indexed data (discriminated union based on format)
///
/// VM does NOT know semantic meaning of instance variables or indexed data.
/// </summary>
public class SInstance : SObject
{
    /// <summary>
    /// Named instance variables (VM understands this exists).
    /// All values are SObjectRef (tagged SmallInteger or object pointer).
    /// </summary>
    protected SObjectRef[] instanceVariables;

    /// <summary>
    /// Object format - determines the type of indexed data (VM must understand).
    /// </summary>
    protected readonly ObjectFormat format;

    /// <summary>
    /// Indexed data (discriminated union based on format).
    /// Can be: null (no indexed data), byte[], ushort[], or SObjectRef[]
    /// VM provides format-aware access but doesn't know semantic meaning.
    /// </summary>
    protected readonly object? indexedData;

    /// <summary>
    /// Initializes a new instance with only named instance variables (no indexed data).
    /// </summary>
    /// <param name="instanceClass">The class of this instance.</param>
    /// <param name="instanceVarCount">The number of instance variables.</param>
    public SInstance(SClass instanceClass, int instanceVarCount = 0)
        : this(instanceClass, instanceVarCount, ObjectFormat.FixedPointer, indexedSize: 0)
    {
    }

    /// <summary>
    /// Initializes a new instance with named instance variables and optional indexed data.
    /// </summary>
    /// <param name="instanceClass">The class of this instance.</param>
    /// <param name="instanceVarCount">The number of named instance variables.</param>
    /// <param name="objectFormat">The format of the indexed data.</param>
    /// <param name="indexedSize">The size of the indexed data (0 if none).</param>
    public SInstance(SClass instanceClass, int instanceVarCount, ObjectFormat objectFormat, int indexedSize)
    {
        Class = instanceClass ?? throw new ArgumentNullException(nameof(instanceClass));
        format = objectFormat;
        instanceVariables = new SObjectRef[instanceVarCount];

        // Initialize all instance variables to nil
        for (var i = 0; i < instanceVarCount; i++)
        {
            instanceVariables[i] = SObjectRef.Nil;
        }

        // Initialize indexed data based on format
        if (indexedSize > 0)
        {
            indexedData = objectFormat switch
            {
                ObjectFormat.PointerIndexed => InitializePointerIndexed(indexedSize),
                ObjectFormat.ByteIndexed => new byte[indexedSize],
                ObjectFormat.WordIndexed => new ushort[indexedSize],
                ObjectFormat.CompiledMethod => InitializeCompiledMethod(indexedSize),
                _ => null
            };
        }
        else
        {
            indexedData = null;
        }
    }

    /// <summary>
    /// Initializes pointer-indexed data with nil values.
    /// </summary>
    private static SObjectRef[] InitializePointerIndexed(int size)
    {
        var data = new SObjectRef[size];
        for (int i = 0; i < size; i++)
        {
            data[i] = SObjectRef.Nil;
        }
        return data;
    }

    /// <summary>
    /// Initializes CompiledMethod indexed data (placeholder for now).
    /// CompiledMethod has both literal pointers and bytecode bytes.
    /// </summary>
    private static byte[] InitializeCompiledMethod(int size)
    {
        // For now, treat as byte array (will be refined later)
        return new byte[size];
    }

    /// <summary>
    /// Gets the class of this instance.
    /// </summary>
    public override SClass Class { get; }

    /// <summary>
    /// Gets the number of instance variables.
    /// </summary>
    public int InstanceVariableCount => instanceVariables.Length;

    /// <summary>
    /// Gets an instance variable by index (Primitive #73: instVarAt:).
    /// VM generic access - returns SObjectRef.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <returns>The value of the instance variable (SObjectRef).</returns>
    public SObjectRef GetInstanceVariable(int index)
    {
        if (index < 0 || index >= instanceVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} is out of range [0, {instanceVariables.Length})");
        }

        return instanceVariables[index];
    }

    /// <summary>
    /// Sets an instance variable by index (Primitive #74: instVarAt:put:).
    /// VM generic access - takes SObjectRef.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <param name="value">The value to set (SObjectRef).</param>
    public void SetInstanceVariable(int index, SObjectRef value)
    {
        if (index < 0 || index >= instanceVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} is out of range [0, {instanceVariables.Length})");
        }

        instanceVariables[index] = value;
    }

    /// <summary>
    /// Gets an instance variable by name.
    /// VM-level convenience method that returns SObjectRef.
    /// </summary>
    /// <param name="name">The name of the instance variable.</param>
    /// <returns>The value of the instance variable as SObjectRef, or SObjectRef.Nil if not found.</returns>
    public SObjectRef GetInstanceVariableByName(string name)
    {
        var index = Class.GetInstanceVariableIndex(name);
        return index >= 0 ? GetInstanceVariable(index) : SObjectRef.Nil;
    }

    /// <summary>
    /// Gets the instance variables array (for internal VM use).
    /// </summary>
    internal SObjectRef[] InstanceVariables => instanceVariables;

    /// <summary>
    /// Resizes the instance variables array (used by MethodContext).
    /// VM-level operation that preserves existing SObjectRef values.
    /// </summary>
    /// <param name="newSize">The new size for the instance variables array.</param>
    protected void ResizeInstanceVariables(int newSize)
    {
        if (newSize <= instanceVariables.Length)
            return; // No need to resize

        var newArray = new SObjectRef[newSize];

        // Copy existing values
        Array.Copy(instanceVariables, newArray, instanceVariables.Length);

        // Initialize new slots to nil
        for (var i = instanceVariables.Length; i < newSize; i++)
        {
            newArray[i] = SObjectRef.Nil;
        }

        instanceVariables = newArray;
    }

    /// <summary>
    /// Sets an instance variable by name.
    /// VM-level convenience method that takes SObjectRef.
    /// </summary>
    /// <param name="name">The name of the instance variable.</param>
    /// <param name="value">The value to set (SObjectRef).</param>
    /// <returns>True if the variable was set; false if the variable name was not found.</returns>
    public bool SetInstanceVariableByName(string name, SObjectRef value)
    {
        var index = Class.GetInstanceVariableIndex(name);
        if (index >= 0)
        {
            SetInstanceVariable(index, value);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a shallow copy of this instance.
    /// Copies all SObjectRef values (which are value types).
    /// </summary>
    /// <returns>A new instance with the same class and copied instance variables.</returns>
    public virtual SInstance ShallowCopy()
    {
        var copy = new SInstance(Class, instanceVariables.Length);
        Array.Copy(instanceVariables, copy.instanceVariables, instanceVariables.Length);
        return copy;
    }

    /// <summary>
    /// Returns a string representation of this instance.
    /// </summary>
    public override string ToDisplayString()
    {
        return $"a {Class.Name}";
    }

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        // Use object identity hash code
        return RuntimeHelpers.GetHashCode(this);
    }

    /// <summary>
    /// Determines whether this instance equals another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        // Smalltalk uses identity equality by default for instances
        return ReferenceEquals(this, obj);
    }

    /// <summary>
    /// Gets all instance variables (for become: primitive).
    /// Returns a copy of the SObjectRef array.
    /// </summary>
    internal SObjectRef[] GetAllInstanceVariables()
    {
        var copy = new SObjectRef[instanceVariables.Length];
        Array.Copy(instanceVariables, copy, instanceVariables.Length);
        return copy;
    }

    /// <summary>
    /// Sets all instance variables (for become: primitive).
    /// Takes an array of SObjectRef values.
    /// </summary>
    internal void SetAllInstanceVariables(SObjectRef[] variables)
    {
        if (variables.Length != instanceVariables.Length)
        {
            throw new ArgumentException("Variable count mismatch");
        }
        Array.Copy(variables, instanceVariables, variables.Length);
    }

    /// <summary>
    /// Gets an indexed variable (0-based for internal VM use).
    /// VM-level generic access returning SObjectRef.
    /// </summary>
    internal virtual SObjectRef GetIndexedVariable(int index)
    {
        // For pointer-indexed objects, return the SObjectRef directly
        if (format == ObjectFormat.PointerIndexed && indexedData is SObjectRef[] pointers)
        {
            if (index >= 0 && index < pointers.Length)
                return pointers[index];
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Sets an indexed variable (0-based for internal VM use).
    /// VM-level generic access taking SObjectRef.
    /// </summary>
    internal virtual void SetIndexedVariable(int index, SObjectRef value)
    {
        // For pointer-indexed objects, set the SObjectRef directly
        if (format == ObjectFormat.PointerIndexed && indexedData is SObjectRef[] pointers)
        {
            if (index >= 0 && index < pointers.Length)
            {
                pointers[index] = value;
                return;
            }
        }

        throw new NotSupportedException($"This instance (format={format}) does not support indexed pointer variables");
    }

    /// <summary>
    /// Gets the object format (VM needs to know this).
    /// </summary>
    public ObjectFormat Format => format;

    /// <summary>
    /// Gets the size of indexed data (0 if none).
    /// </summary>
    public int IndexedSize => indexedData switch
    {
        byte[] bytes => bytes.Length,
        ushort[] words => words.Length,
        SObjectRef[] pointers => pointers.Length,
        _ => 0
    };

    /// <summary>
    /// Gets an indexed byte (0-based).
    /// VM-level format-aware access for ByteIndexed objects.
    /// Used by primitives that work with String, ByteArray, etc.
    /// </summary>
    public byte GetIndexedByte(int index)
    {
        if (format != ObjectFormat.ByteIndexed && format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format is {format}, not ByteIndexed");

        if (indexedData is byte[] bytes)
        {
            if (index < 0 || index >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return bytes[index];
        }

        throw new InvalidOperationException("Indexed data is not byte array");
    }

    /// <summary>
    /// Sets an indexed byte (0-based).
    /// VM-level format-aware access for ByteIndexed objects.
    /// </summary>
    public void SetIndexedByte(int index, byte value)
    {
        if (format != ObjectFormat.ByteIndexed && format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format is {format}, not ByteIndexed");

        if (indexedData is byte[] bytes)
        {
            if (index < 0 || index >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            bytes[index] = value;
            return;
        }

        throw new InvalidOperationException("Indexed data is not byte array");
    }

    /// <summary>
    /// Gets an indexed word (0-based).
    /// VM-level format-aware access for WordIndexed objects.
    /// </summary>
    public ushort GetIndexedWord(int index)
    {
        if (format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format is {format}, not WordIndexed");

        if (indexedData is ushort[] words)
        {
            if (index < 0 || index >= words.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return words[index];
        }

        throw new InvalidOperationException("Indexed data is not word array");
    }

    /// <summary>
    /// Sets an indexed word (0-based).
    /// VM-level format-aware access for WordIndexed objects.
    /// </summary>
    public void SetIndexedWord(int index, ushort value)
    {
        if (format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format is {format}, not WordIndexed");

        if (indexedData is ushort[] words)
        {
            if (index < 0 || index >= words.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            words[index] = value;
            return;
        }

        throw new InvalidOperationException("Indexed data is not word array");
    }

    /// <summary>
    /// Gets an indexed pointer (0-based).
    /// VM-level format-aware access for PointerIndexed objects.
    /// </summary>
    public SObjectRef GetIndexedPointer(int index)
    {
        if (format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object format is {format}, not PointerIndexed");

        if (indexedData is SObjectRef[] pointers)
        {
            if (index < 0 || index >= pointers.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return pointers[index];
        }

        throw new InvalidOperationException("Indexed data is not pointer array");
    }

    /// <summary>
    /// Sets an indexed pointer (0-based).
    /// VM-level format-aware access for PointerIndexed objects.
    /// </summary>
    public void SetIndexedPointer(int index, SObjectRef value)
    {
        if (format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object format is {format}, not PointerIndexed");

        if (indexedData is SObjectRef[] pointers)
        {
            if (index < 0 || index >= pointers.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            pointers[index] = value;
            return;
        }

        throw new InvalidOperationException("Indexed data is not pointer array");
    }
}