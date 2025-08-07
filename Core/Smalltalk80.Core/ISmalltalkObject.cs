namespace Smalltalk80.Core;

/// <summary>
/// Represents the fundamental contract for all Smalltalk objects in the VM.
/// Every object has a class pointer and can contain fields (instance variables).
/// </summary>
public interface ISmalltalkObject
{
    /// <summary>
    /// Gets or sets the class of this object.
    /// Every Smalltalk object has a class, including classes themselves (whose class is a metaclass).
    /// </summary>
    ISmalltalkClass Class { get; set; }

    /// <summary>
    /// Gets the object identifier (OOP - Object-Oriented Pointer) for this object.
    /// </summary>
    int ObjectId { get; }

    /// <summary>
    /// Gets the number of named instance variable fields in this object.
    /// </summary>
    int FieldCount { get; }

    /// <summary>
    /// Gets or sets a named instance variable field by index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field value.</returns>
    ISmalltalkObject? this[int index] { get; set; }

    /// <summary>
    /// Gets or sets a named instance variable field by name.
    /// </summary>
    /// <param name="fieldName">The name of the instance variable.</param>
    /// <returns>The field value.</returns>
    ISmalltalkObject? this[string fieldName] { get; set; }

    /// <summary>
    /// Determines if this object is an indexed object (like Array).
    /// </summary>
    bool IsIndexed { get; }

    /// <summary>
    /// Gets the size of indexed fields for indexed objects.
    /// </summary>
    int IndexedSize { get; }

    /// <summary>
    /// Gets or sets an indexed field (for Array-like objects).
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The indexed field value.</returns>
    ISmalltalkObject? GetIndexed(int index);
    
    /// <summary>
    /// Sets an indexed field value.
    /// </summary>
    void SetIndexed(int index, ISmalltalkObject? value);

    /// <summary>
    /// Determines if this object is a byte object (like String or Symbol).
    /// </summary>
    bool IsByteObject { get; }

    /// <summary>
    /// Gets the byte data for byte objects.
    /// </summary>
    byte[]? GetBytes();

    /// <summary>
    /// Sets the byte data for byte objects.
    /// </summary>
    void SetBytes(byte[] bytes);

    /// <summary>
    /// Creates a shallow copy of this object.
    /// </summary>
    ISmalltalkObject ShallowCopy();

    /// <summary>
    /// Gets a string representation for debugging.
    /// </summary>
    string ToDebugString();
}

/// <summary>
/// Represents a Smalltalk class object.
/// Classes are special objects that can create instances and contain method dictionaries.
/// </summary>
public interface ISmalltalkClass : ISmalltalkObject
{
    /// <summary>
    /// Gets or sets the superclass of this class.
    /// ProtoObject is the only class with a null superclass.
    /// </summary>
    ISmalltalkClass? Superclass { get; set; }

    /// <summary>
    /// Gets or sets the method dictionary containing this class's methods.
    /// </summary>
    IMethodDictionary MethodDictionary { get; set; }

    /// <summary>
    /// Gets or sets the class name symbol.
    /// </summary>
    ISymbol Name { get; set; }

    /// <summary>
    /// Gets or sets the array of instance variable names.
    /// </summary>
    ISymbol[] InstanceVariables { get; set; }

    /// <summary>
    /// Gets or sets the class variable dictionary (at field index 6).
    /// </summary>
    IDictionary<ISymbol, ISmalltalkObject>? ClassVariables { get; set; }

    /// <summary>
    /// Gets or sets the shared pool dictionaries.
    /// </summary>
    IDictionary<ISymbol, ISmalltalkObject>? PoolDictionaries { get; set; }

    /// <summary>
    /// Gets or sets the format of instances of this class.
    /// Format encodes: indexed/non-indexed, byte/pointer, field count.
    /// </summary>
    int Format { get; set; }

    /// <summary>
    /// Gets the metaclass of this class (for class-side methods).
    /// </summary>
    ISmalltalkClass Metaclass { get; }

    /// <summary>
    /// Looks up a method by selector in this class or its superclasses.
    /// </summary>
    /// <param name="selector">The method selector to find.</param>
    /// <returns>The compiled method, or null if not found.</returns>
    ICompiledMethod? LookupMethod(ISymbol selector);

    /// <summary>
    /// Adds a method to this class's method dictionary.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <param name="method">The compiled method.</param>
    void AddMethod(ISymbol selector, ICompiledMethod method);

    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    /// <returns>A new instance with fields initialized to nil.</returns>
    ISmalltalkObject NewInstance();

    /// <summary>
    /// Creates a new indexed instance of this class.
    /// </summary>
    /// <param name="size">The number of indexed slots.</param>
    /// <returns>A new indexed instance.</returns>
    ISmalltalkObject NewInstance(int size);

    /// <summary>
    /// Determines if this class is a subclass of another class.
    /// </summary>
    /// <param name="other">The potential superclass.</param>
    /// <returns>True if this class inherits from the other class.</returns>
    bool IsSubclassOf(ISmalltalkClass other);
}

/// <summary>
/// Represents a compiled Smalltalk method.
/// </summary>
public interface ICompiledMethod : ISmalltalkObject
{
    /// <summary>
    /// Gets the bytecode instructions for this method.
    /// </summary>
    byte[] Bytecodes { get; }

    /// <summary>
    /// Gets the literal objects referenced by this method.
    /// </summary>
    ISmalltalkObject[] Literals { get; }

    /// <summary>
    /// Gets the number of temporary variables used by this method.
    /// </summary>
    int TemporaryCount { get; }

    /// <summary>
    /// Gets the number of arguments expected by this method.
    /// </summary>
    int ArgumentCount { get; }

    /// <summary>
    /// Gets the primitive index if this is a primitive method, or 0 if not.
    /// </summary>
    int PrimitiveIndex { get; }

    /// <summary>
    /// Gets the method header that encodes temporary count, argument count, etc.
    /// </summary>
    int Header { get; }

    /// <summary>
    /// Gets the source code for this method if available.
    /// </summary>
    string? SourceCode { get; }

    /// <summary>
    /// Gets the selector (method name) for this method.
    /// </summary>
    ISymbol? Selector { get; }
}

/// <summary>
/// Represents a Smalltalk symbol (interned string).
/// </summary>
public interface ISymbol : ISmalltalkObject
{
    /// <summary>
    /// Gets the string value of this symbol.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Gets the hash code for this symbol.
    /// </summary>
    int Hash { get; }
}

/// <summary>
/// Represents a method dictionary mapping selectors to compiled methods.
/// </summary>
public interface IMethodDictionary : ISmalltalkObject
{
    /// <summary>
    /// Gets a method by selector.
    /// </summary>
    ICompiledMethod? GetMethod(ISymbol selector);

    /// <summary>
    /// Adds or updates a method.
    /// </summary>
    void SetMethod(ISymbol selector, ICompiledMethod method);

    /// <summary>
    /// Removes a method.
    /// </summary>
    bool RemoveMethod(ISymbol selector);

    /// <summary>
    /// Gets all selectors in this dictionary.
    /// </summary>
    IEnumerable<ISymbol> Selectors { get; }
}