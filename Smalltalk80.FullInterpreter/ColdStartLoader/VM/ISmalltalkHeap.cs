namespace Smalltalk80.Interpreter.ColdStartLoader.VM;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Abstraction layer between the cold-start loader and the VM's heap implementation.
/// This interface allows the cold-start loader to be independent of ObjectMemory internals.
///
/// All methods accept and return SObjectRef values for type safety.
/// The heap implementation is responsible for managing actual object pointers.
/// </summary>
public interface ISmalltalkHeap
{
    /// <summary>
    /// Allocates a new object with the specified class, format, and size.
    /// </summary>
    /// <param name="classRef">The class of the object to allocate.</param>
    /// <param name="format">The object format (FixedPointer, PointerIndexed, etc.).</param>
    /// <param name="fixedFieldCount">Number of fixed instance variables.</param>
    /// <param name="indexedFieldCount">Number of indexed slots.</param>
    /// <returns>Reference to the allocated object.</returns>
    SObjectRef AllocateObject(SObjectRef classRef, Core.Objects.ObjectFormat format, int fixedFieldCount, int indexedFieldCount);

    /// <summary>
    /// Sets a fixed instance variable field by index.
    /// </summary>
    /// <param name="objectRef">The object to modify.</param>
    /// <param name="fieldIndex">Zero-based field index.</param>
    /// <param name="valueRef">The value to set.</param>
    void SetField(SObjectRef objectRef, int fieldIndex, SObjectRef valueRef);

    /// <summary>
    /// Sets an indexed field by index.
    /// </summary>
    /// <param name="objectRef">The object to modify.</param>
    /// <param name="index">Zero-based indexed slot.</param>
    /// <param name="valueRef">The value to set.</param>
    void SetIndexedField(SObjectRef objectRef, int index, SObjectRef valueRef);

    /// <summary>
    /// Gets a fixed instance variable field by index.
    /// </summary>
    /// <param name="objectRef">The object to read from.</param>
    /// <param name="fieldIndex">Zero-based field index.</param>
    /// <returns>The field value.</returns>
    SObjectRef GetField(SObjectRef objectRef, int fieldIndex);

    /// <summary>
    /// Gets an indexed field by index.
    /// </summary>
    /// <param name="objectRef">The object to read from.</param>
    /// <param name="index">Zero-based indexed slot.</param>
    /// <returns>The indexed value.</returns>
    SObjectRef GetIndexedField(SObjectRef objectRef, int index);

    /// <summary>
    /// Sets the class pointer of an object.
    /// </summary>
    /// <param name="objectRef">The object to modify.</param>
    /// <param name="classRef">The new class reference.</param>
    void SetClass(SObjectRef objectRef, SObjectRef classRef);

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    /// <param name="objectRef">The object to query.</param>
    /// <returns>The class reference.</returns>
    SObjectRef GetClass(SObjectRef objectRef);

    /// <summary>
    /// Interns a symbol string and returns its object reference.
    /// Creates a new Symbol object if it doesn't exist.
    /// </summary>
    /// <param name="symbolName">The symbol string to intern.</param>
    /// <returns>Reference to the Symbol object.</returns>
    SObjectRef InternSymbol(string symbolName);

    /// <summary>
    /// Creates a String object from a string value.
    /// </summary>
    /// <param name="stringValue">The string content.</param>
    /// <returns>Reference to the String object.</returns>
    SObjectRef CreateString(string stringValue);

    /// <summary>
    /// Creates a CompiledMethod object with the specified structure.
    /// </summary>
    /// <param name="header">The method header SmallInteger.</param>
    /// <param name="literals">Array of literal values.</param>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <returns>Reference to the CompiledMethod object.</returns>
    SObjectRef CreateCompiledMethod(SObjectRef header, SObjectRef[] literals, byte[] bytecodes);

    /// <summary>
    /// Creates an empty MethodDictionary with the specified capacity.
    /// </summary>
    /// <param name="capacity">Initial capacity for the dictionary.</param>
    /// <returns>Reference to the MethodDictionary object.</returns>
    SObjectRef CreateMethodDictionary(int capacity);

    /// <summary>
    /// Installs a method in a class's method dictionary.
    /// </summary>
    /// <param name="classRef">The class to install the method in.</param>
    /// <param name="selectorSymbol">The selector symbol.</param>
    /// <param name="methodRef">The compiled method.</param>
    void InstallMethod(SObjectRef classRef, SObjectRef selectorSymbol, SObjectRef methodRef);

    /// <summary>
    /// Creates the USTable (universal symbol table) with the specified size.
    /// </summary>
    /// <param name="size">The size of the table.</param>
    /// <returns>Reference to the USTable object.</returns>
    SObjectRef CreateUSTable(int size);

    /// <summary>
    /// Inserts a symbol into the USTable using the canonical hash.
    /// </summary>
    /// <param name="ustableRef">The USTable object.</param>
    /// <param name="symbolRef">The symbol to insert.</param>
    /// <param name="hash">The canonical hash value.</param>
    void InsertIntoUSTable(SObjectRef ustableRef, SObjectRef symbolRef, int hash);

    /// <summary>
    /// Executes a method (for init expressions).
    /// </summary>
    /// <param name="methodRef">The compiled method to execute.</param>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>The result of execution.</returns>
    SObjectRef ExecuteMethod(SObjectRef methodRef, SObjectRef receiver, params SObjectRef[] arguments);

    /// <summary>
    /// Gets a well-known class by name.
    /// </summary>
    /// <param name="className">The class name (e.g., "Array", "String").</param>
    /// <returns>Reference to the class, or Nil if not found.</returns>
    SObjectRef GetClassByName(string className);

    /// <summary>
    /// Registers a global object in the SystemDictionary.
    /// </summary>
    /// <param name="globalName">The global variable name.</param>
    /// <param name="objectRef">The object reference.</param>
    void RegisterGlobal(string globalName, SObjectRef objectRef);

    /// <summary>
    /// Gets a global object from the SystemDictionary.
    /// </summary>
    /// <param name="globalName">The global variable name.</param>
    /// <returns>The object reference, or Nil if not found.</returns>
    SObjectRef GetGlobal(string globalName);
}
