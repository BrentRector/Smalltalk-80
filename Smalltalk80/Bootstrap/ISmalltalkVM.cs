namespace Smalltalk80.Bootstrap;

/// <summary>
/// Interface for primitive-level VM operations used during bootstrap.
/// These primitives construct actual Smalltalk objects inside the VM.
/// </summary>
public interface ISmalltalkVM
{
    /// <summary>
    /// Create a new class object (not metaclass).
    /// Returns the object ID (OOP) of the created class.
    /// </summary>
    int PrimitiveNewClass();

    /// <summary>
    /// Create a new metaclass object.
    /// Returns the object ID (OOP) of the created metaclass.
    /// </summary>
    int PrimitiveNewMetaclass();

    /// <summary>
    /// Create a new instance of a class.
    /// </summary>
    /// <param name="classId">The OOP of the class to instantiate.</param>
    /// <returns>The OOP of the new instance.</returns>
    int PrimitiveNewInstance(int classId);

    /// <summary>
    /// Get slot value at a specific index.
    /// </summary>
    /// <param name="objectId">The OOP of the object.</param>
    /// <param name="slotIndex">The 0-based slot index.</param>
    /// <returns>The OOP of the value at the slot, or -1 if not found.</returns>
    int GetSlot(int objectId, int slotIndex);
    
    /// <summary>
    /// Set slot value at a specific index.
    /// </summary>
    /// <param name="objectId">The OOP of the object.</param>
    /// <param name="slotIndex">The 0-based slot index.</param>
    /// <param name="valueId">The OOP of the value to store.</param>
    void PrimitiveSetSlot(int objectId, int slotIndex, int valueId);

    /// <summary>
    /// Set the class pointer of an object.
    /// </summary>
    /// <param name="objectId">The OOP of the object.</param>
    /// <param name="classId">The OOP of the class.</param>
    void PrimitiveSetClass(int objectId, int classId);

    /// <summary>
    /// Intern a symbol and return its OOP.
    /// </summary>
    /// <param name="name">The symbol name to intern.</param>
    /// <returns>The OOP of the interned symbol.</returns>
    int PrimitiveInternSymbol(string name);

    /// <summary>
    /// Create a SmallInteger object.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>The OOP of the created SmallInteger.</returns>
    int PrimitiveNewSmallInteger(int value);
    
    /// <summary>
    /// Create a new array.
    /// </summary>
    /// <param name="size">The size of the array.</param>
    /// <returns>The OOP of the created array.</returns>
    int PrimitiveNewArray(int size);

    /// <summary>
    /// Set array element at index.
    /// </summary>
    /// <param name="arrayId">The OOP of the array.</param>
    /// <param name="index">The 0-based index.</param>
    /// <param name="valueId">The OOP of the value to store.</param>
    void PrimitiveArrayAtPut(int arrayId, int index, int valueId);

    /// <summary>
    /// Create a new dictionary.
    /// </summary>
    /// <returns>The OOP of the created dictionary.</returns>
    int PrimitiveNewDictionary();

    /// <summary>
    /// Add a key-value pair to a dictionary.
    /// </summary>
    /// <param name="dictId">The OOP of the dictionary.</param>
    /// <param name="keyId">The OOP of the key.</param>
    /// <param name="valueId">The OOP of the value.</param>
    void PrimitiveDictionaryAtPut(int dictId, int keyId, int valueId);
    
    /// <summary>
    /// Look up a value in a dictionary by key.
    /// </summary>
    /// <param name="dictId">The OOP of the dictionary.</param>
    /// <param name="keyId">The OOP of the key.</param>
    /// <returns>The OOP of the value, or -1 if not found.</returns>
    int PrimitiveDictionaryAt(int dictId, int keyId);

    /// <summary>
    /// Compile and install a method in a class.
    /// </summary>
    /// <param name="classId">The OOP of the class.</param>
    /// <param name="selector">The method selector.</param>
    /// <param name="sourceCode">The Smalltalk source code.</param>
    void PrimitiveInstallMethod(int classId, string selector, string sourceCode);

    /// <summary>
    /// Get the nil object OOP.
    /// </summary>
    int PrimitiveNil();

    /// <summary>
    /// Register a global variable in Smalltalk dictionary.
    /// </summary>
    /// <param name="name">The global variable name.</param>
    /// <param name="valueId">The OOP of the value.</param>
    void PrimitiveRegisterGlobal(string name, int valueId);

    /// <summary>
    /// Get a registered class by name.
    /// </summary>
    /// <param name="className">The class name.</param>
    /// <returns>The OOP of the class, or -1 if not found.</returns>
    int PrimitiveLookupClass(string className);
    
    /// <summary>
    /// Look up a global variable by name.
    /// </summary>
    /// <param name="name">The global variable name.</param>
    /// <returns>The OOP of the global, or -1 if not found.</returns>
    int PrimitiveLookupGlobal(string name);
    
    /// <summary>
    /// Intern a string and return its OOP.
    /// </summary>
    /// <param name="value">The string value to intern.</param>
    /// <returns>The OOP of the interned string.</returns>
    int PrimitiveInternString(string value);
    
    /// <summary>
    /// Send a message to an object.
    /// </summary>
    /// <param name="receiverId">The OOP of the receiver object.</param>
    /// <param name="selectorId">The OOP of the selector symbol.</param>
    /// <param name="argumentIds">The OOPs of the arguments.</param>
    /// <returns>The OOP of the result.</returns>
    int PrimitiveSendMessage(int receiverId, int selectorId, int[] argumentIds);
    
    /// <summary>
    /// Execute bytecode with receiver and arguments.
    /// </summary>
    /// <param name="receiverId">The OOP of the receiver object.</param>
    /// <param name="bytecode">The bytecode array to execute.</param>
    /// <param name="argumentIds">The OOPs of the arguments.</param>
    /// <returns>The OOP of the result.</returns>
    int ExecuteBytecode(int receiverId, byte[] bytecode, int[] argumentIds);
}