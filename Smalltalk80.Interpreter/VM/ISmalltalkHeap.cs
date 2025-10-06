using Smalltalk80.Interpreter.Schema;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Interface to the Virtual Machine's heap abstraction.
/// This interface keeps VM implementation details private while providing
/// the cold start loader with necessary heap operations.
/// </summary>
public interface ISmalltalkHeap
{
    /// <summary>
    /// Allocates a new object with the specified class, format, and size.
    /// </summary>
    /// <param name="className">Name of the class to instantiate</param>
    /// <param name="formatHint">Format hint (e.g., "pointer", "byte", "word")</param>
    /// <param name="fixedSize">Number of fixed (named) fields</param>
    /// <param name="indexedSize">Number of indexed fields</param>
    /// <returns>Object ID of the newly allocated object</returns>
    int AllocateObject(string className, string formatHint, int fixedSize, int indexedSize);

    /// <summary>
    /// Sets a fixed (named) field in an object.
    /// </summary>
    /// <param name="objectId">Object ID</param>
    /// <param name="fieldIndex">Zero-based field index</param>
    /// <param name="valueId">Object ID of the value to store</param>
    void SetField(int objectId, int fieldIndex, int valueId);

    /// <summary>
    /// Sets an indexed field in an object.
    /// </summary>
    /// <param name="objectId">Object ID</param>
    /// <param name="index">Zero-based index</param>
    /// <param name="valueId">Object ID of the value to store</param>
    void SetIndexedField(int objectId, int index, int valueId);

    /// <summary>
    /// Sets a named slot in an object (for class-level slots like superclass, methodDict, etc.).
    /// </summary>
    /// <param name="objectId">Object ID</param>
    /// <param name="slotName">Name of the slot (e.g., "superclass", "methodDict")</param>
    /// <param name="valueId">Object ID of the value to store</param>
    void SetNamedSlot(int objectId, string slotName, int valueId);

    /// <summary>
    /// Installs a compiled method in a class's method dictionary.
    /// </summary>
    /// <param name="classId">Object ID of the class</param>
    /// <param name="selectorId">Object ID of the selector symbol</param>
    /// <param name="methodId">Object ID of the compiled method</param>
    void InstallMethod(int classId, int selectorId, int methodId);

    /// <summary>
    /// Executes a method (typically for initialization expressions).
    /// </summary>
    /// <param name="methodId">Object ID of the method to execute</param>
    void ExecuteMethod(int methodId);

    /// <summary>
    /// Interns a symbol string and returns its object ID.
    /// Symbols with the same name always return the same object ID.
    /// </summary>
    /// <param name="name">Symbol name to intern</param>
    /// <returns>Object ID of the interned symbol</returns>
    int InternSymbol(string name);

    /// <summary>
    /// Interns a string literal and returns its object ID.
    /// Unlike symbols, strings may not be deduplicated.
    /// </summary>
    /// <param name="value">String value to intern</param>
    /// <returns>Object ID of the string object</returns>
    int InternString(string value);

    /// <summary>
    /// Creates a compiled method object.
    /// </summary>
    /// <param name="method">Method descriptor containing metadata</param>
    /// <param name="literalIds">Array of object IDs for method literals</param>
    /// <param name="bytecodes">Method bytecode array</param>
    /// <returns>Object ID of the compiled method</returns>
    int CreateCompiledMethod(MethodDescriptor method, int[] literalIds, byte[] bytecodes);

    /// <summary>
    /// Creates a method dictionary with exact capacity for the specified number of methods.
    /// </summary>
    /// <param name="methodCount">Exact number of methods that will be installed</param>
    /// <returns>Object ID of the method dictionary</returns>
    int CreateMethodDictionary(int methodCount);

    /// <summary>
    /// Creates a Universal Symbol Table (USTable) with the specified size.
    /// </summary>
    /// <param name="size">Size of the USTable</param>
    /// <returns>Object ID of the USTable</returns>
    int CreateUSTable(int size);

    /// <summary>
    /// Inserts a symbol into the USTable at the specified hash index.
    /// </summary>
    /// <param name="ustableId">Object ID of the USTable</param>
    /// <param name="symbolId">Object ID of the symbol to insert</param>
    /// <param name="hash">Hash value for the symbol</param>
    void InsertIntoUSTable(int ustableId, int symbolId, int hash);

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP (does NOT allocate on heap).
    /// SmallIntegers use immediate encoding with tag bits.
    /// </summary>
    /// <param name="value">Integer value to encode</param>
    /// <returns>Tagged OOP representing the SmallInteger</returns>
    int EncodeSmallInteger(int value);

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    /// <param name="codePoint">Unicode code point</param>
    /// <returns>Object ID of the character</returns>
    int AllocateCharacter(int codePoint);

    /// <summary>
    /// Allocates a byte array object.
    /// </summary>
    /// <param name="encoding">Encoding type (e.g., "base64", "hex")</param>
    /// <param name="data">Encoded byte data</param>
    /// <returns>Object ID of the byte array</returns>
    int AllocateByteArray(string encoding, string data);

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    /// <param name="keyId">Object ID of the key</param>
    /// <param name="valueId">Object ID of the value</param>
    /// <returns>Object ID of the association</returns>
    int AllocateAssociation(int keyId, int valueId);

    /// <summary>
    /// Allocates an Array object with the given elements.
    /// </summary>
    /// <param name="elementIds">Array of object IDs for array elements</param>
    /// <returns>Object ID of the array</returns>
    int AllocateArray(int[] elementIds);

    /// <summary>
    /// Executes an initialization expression.
    /// </summary>
    /// <param name="code">Bytecode or expression code</param>
    /// <param name="literalIds">Array of object IDs for literals used in the expression</param>
    void ExecuteInitExpression(object code, int[] literalIds);

    /// <summary>
    /// Gets a listing of all classes in the heap.
    /// Returns tuples of (className, instanceCount, instanceSideMethodCount, classSideMethodCount).
    /// </summary>
    /// <returns>Enumerable of class information tuples</returns>
    IEnumerable<(string ClassName, int InstanceCount, int InstanceMethodCount, int ClassMethodCount)> GetAllClasses();

    /// <summary>
    /// Sets the special objects array for garbage collection.
    /// </summary>
    /// <param name="specialObjects">Array of object IDs that are GC roots</param>
    void SetSpecialObjectsArray(int[] specialObjects);

    /// <summary>
    /// Performs a mark-and-sweep garbage collection.
    /// </summary>
    /// <returns>Number of objects collected</returns>
    int GarbageCollect();

    /// <summary>
    /// Gets garbage collection statistics.
    /// </summary>
    /// <returns>Tuple of (totalObjects, liveObjects)</returns>
    (int TotalObjects, int LiveObjects) GetGCStatistics();

    /// <summary>
    /// Gets comprehensive information about a class by name.
    /// </summary>
    /// <param name="className">Name of the class</param>
    /// <returns>ClassInfo object or null if class not found</returns>
    ClassInfo? GetClassInfo(string className);

    /// <summary>
    /// Gets detailed information about a method in a class.
    /// </summary>
    /// <param name="classId">Object ID of the class</param>
    /// <param name="selector">Selector name</param>
    /// <returns>MethodInfo object or null if method not found</returns>
    MethodInfo? GetMethod(int classId, string selector);

    /// <summary>
    /// Gets the string name of a symbol object.
    /// </summary>
    /// <param name="symbolId">Object ID of the symbol</param>
    /// <returns>Symbol name or null if not a symbol</returns>
    string? GetSymbolName(int symbolId);

    /// <summary>
    /// Gets a displayable representation of any object's value.
    /// </summary>
    /// <param name="objectId">Object ID</param>
    /// <returns>String representation of the object</returns>
    string GetObjectDisplay(int objectId);

    /// <summary>
    /// Sets the optimized selector names for bytecode disassembly.
    /// These are the special selectors used by bytecodes 176-207.
    /// </summary>
    /// <param name="selectorNames">Array of selector names</param>
    void SetOptimizedSelectors(string[] selectorNames);

    /// <summary>
    /// Gets the optimized selector names for bytecode disassembly.
    /// </summary>
    /// <returns>Array of selector names</returns>
    string[] GetOptimizedSelectors();
}
