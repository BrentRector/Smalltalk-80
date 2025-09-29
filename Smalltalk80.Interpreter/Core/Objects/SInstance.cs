namespace Smalltalk80.Interpreter.Core.Objects;

using System.Runtime.CompilerServices;

/// <summary>
/// Represents an instance of a Smalltalk class with instance variables.
/// </summary>
public class SInstance : SObject
{
    /// <summary>
    /// The array of instance variables for this object.
    /// </summary>
    protected SObject?[] instanceVariables;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SInstance"/> class.
    /// </summary>
    /// <param name="instanceClass">The class of this instance.</param>
    /// <param name="variableCount">The number of instance variables.</param>
    public SInstance(SClass instanceClass, int variableCount = 0)
    {
        Class = instanceClass ?? throw new ArgumentNullException(nameof(instanceClass));
        instanceVariables = new SObject?[variableCount];
        
        // Initialize all instance variables to nil
        for (var i = 0; i < variableCount; i++)
        {
            instanceVariables[i] = SNil.Instance;
        }
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
    /// Gets an instance variable by index.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <returns>The value of the instance variable, or nil if not set.</returns>
    public SObject? GetInstanceVariable(int index)
    {
        if (index < 0 || index >= instanceVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Instance variable index {index} is out of range [0, {instanceVariables.Length})");
        }
        
        return instanceVariables[index];
    }
    
    /// <summary>
    /// Sets an instance variable by index.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <param name="value">The value to set.</param>
    public void SetInstanceVariable(int index, SObject? value)
    {
        if (index < 0 || index >= instanceVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Instance variable index {index} is out of range [0, {instanceVariables.Length})");
        }
        
        instanceVariables[index] = value ?? SNil.Instance;
    }
    
    /// <summary>
    /// Gets an instance variable by name.
    /// </summary>
    /// <param name="name">The name of the instance variable.</param>
    /// <returns>The value of the instance variable, or nil if not found.</returns>
    public SObject? GetInstanceVariableByName(string name)
    {
        var index = Class.GetInstanceVariableIndex(name);
        return index >= 0 ? GetInstanceVariable(index) : SNil.Instance;
    }
    
    /// <summary>
    /// Gets the instance variables array (for internal use).
    /// </summary>
    internal SObject?[] InstanceVariables => instanceVariables;
    
    /// <summary>
    /// Resizes the instance variables array (used by MethodContext).
    /// </summary>
    /// <param name="newSize">The new size for the instance variables array.</param>
    protected void ResizeInstanceVariables(int newSize)
    {
        if (newSize <= instanceVariables.Length)
            return; // No need to resize
        
        var newArray = new SObject?[newSize];
        
        // Copy existing values
        Array.Copy(instanceVariables, newArray, instanceVariables.Length);
        
        // Initialize new slots to nil
        for (var i = instanceVariables.Length; i < newSize; i++)
        {
            newArray[i] = SNil.Instance;
        }
        
        instanceVariables = newArray;
    }
    
    /// <summary>
    /// Sets an instance variable by name.
    /// </summary>
    /// <param name="name">The name of the instance variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if the variable was set; false if the variable name was not found.</returns>
    public bool SetInstanceVariableByName(string name, SObject? value)
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
    /// </summary>
    internal SObject?[] GetAllInstanceVariables()
    {
        var copy = new SObject?[instanceVariables.Length];
        Array.Copy(instanceVariables, copy, instanceVariables.Length);
        return copy;
    }
    
    /// <summary>
    /// Sets all instance variables (for become: primitive).
    /// </summary>
    internal void SetAllInstanceVariables(SObject?[] variables)
    {
        if (variables.Length != instanceVariables.Length)
        {
            throw new ArgumentException("Variable count mismatch");
        }
        Array.Copy(variables, instanceVariables, variables.Length);
    }
    
    /// <summary>
    /// Gets an indexed variable (0-based for internal use).
    /// </summary>
    internal virtual SObject? GetIndexedVariable(int index)
    {
        // Default instances don't have indexed variables
        return null;
    }
    
    /// <summary>
    /// Sets an indexed variable (0-based for internal use).
    /// </summary>
    internal virtual void SetIndexedVariable(int index, SObject? value)
    {
        // Default instances don't have indexed variables
        throw new NotSupportedException("This instance does not support indexed variables");
    }
}

/// <summary>
/// Represents a Smalltalk instance with indexed variables (like Array).
/// </summary>
public class SIndexedInstance : SInstance
{
    private readonly SObject?[] indexedVariables;
    
    /// <summary>
    /// Initializes a new indexed instance.
    /// </summary>
    /// <param name="instanceClass">The class of this instance.</param>
    /// <param name="instanceVarCount">The number of named instance variables.</param>
    /// <param name="indexedVarCount">The number of indexed variables.</param>
    public SIndexedInstance(SClass instanceClass, int instanceVarCount, int indexedVarCount)
        : base(instanceClass, instanceVarCount)
    {
        indexedVariables = new SObject?[indexedVarCount];
        
        // Initialize indexed variables to nil
        for (var i = 0; i < indexedVarCount; i++)
        {
            indexedVariables[i] = SNil.Instance;
        }
    }
    
    /// <summary>
    /// Gets the number of indexed variables.
    /// </summary>
    public int Size => indexedVariables.Length;
    
    /// <summary>
    /// Gets an indexed variable (1-based indexing like Smalltalk).
    /// </summary>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The value at the index.</returns>
    public SObject? At(int index)
    {
        if (index < 1 || index > indexedVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range [1, {indexedVariables.Length}]");
        }
        
        return indexedVariables[index - 1];
    }
    
    /// <summary>
    /// Sets an indexed variable (1-based indexing like Smalltalk).
    /// </summary>
    /// <param name="index">The 1-based index.</param>
    /// <param name="value">The value to set.</param>
    public void AtPut(int index, SObject? value)
    {
        if (index < 1 || index > indexedVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range [1, {indexedVariables.Length}]");
        }
        
        indexedVariables[index - 1] = value ?? SNil.Instance;
    }
    
    /// <summary>
    /// Creates a shallow copy of this indexed instance.
    /// </summary>
    public override SInstance ShallowCopy()
    {
        var copy = new SIndexedInstance(Class, InstanceVariableCount, indexedVariables.Length);
        
        // Copy instance variables
        for (var i = 0; i < InstanceVariableCount; i++)
        {
            copy.SetInstanceVariable(i, GetInstanceVariable(i));
        }
        
        // Copy indexed variables
        Array.Copy(indexedVariables, copy.indexedVariables, indexedVariables.Length);
        
        return copy;
    }
    
    /// <summary>
    /// Returns a string representation of this indexed instance.
    /// </summary>
    public override string ToDisplayString()
    {
        return $"a {Class.Name}({Size})";
    }
    
    /// <summary>
    /// Gets an indexed variable (0-based for internal use).
    /// </summary>
    internal override SObject? GetIndexedVariable(int index)
    {
        if (index < 0 || index >= indexedVariables.Length)
        {
            return null;
        }
        return indexedVariables[index];
    }
    
    /// <summary>
    /// Sets an indexed variable (0-based for internal use).
    /// </summary>
    internal override void SetIndexedVariable(int index, SObject? value)
    {
        if (index < 0 || index >= indexedVariables.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        indexedVariables[index] = value ?? SNil.Instance;
    }
}