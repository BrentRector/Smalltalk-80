namespace Smalltalk80.Interpreter.ColdStartLoader.VM;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Execution;

/// <summary>
/// Adapter that implements ISmalltalkHeap by delegating to ObjectMemory.
/// This allows the cold-start loader to work with ObjectMemory without tight coupling.
/// </summary>
public class ObjectMemoryHeapAdapter : ISmalltalkHeap
{
    private readonly Interpreter? _interpreter;

    public ObjectMemoryHeapAdapter(Interpreter? interpreter = null)
    {
        _interpreter = interpreter;
    }

    public SObjectRef AllocateObject(SObjectRef classRef, ObjectFormat format, int fixedFieldCount, int indexedFieldCount)
    {
        var oop = ObjectMemory.AllocateObject(classRef, format, fixedFieldCount, indexedFieldCount);
        return SObjectRef.FromObjectPointer(oop);
    }

    public void SetField(SObjectRef objectRef, int fieldIndex, SObjectRef valueRef)
    {
        if (!objectRef.IsHeapObject)
            throw new ArgumentException("Cannot set field on non-heap object", nameof(objectRef));

        ObjectMemory.SetInstanceVariable(objectRef.AsObjectPointer(), fieldIndex, valueRef);
    }

    public void SetIndexedField(SObjectRef objectRef, int index, SObjectRef valueRef)
    {
        if (!objectRef.IsHeapObject)
            throw new ArgumentException("Cannot set indexed field on non-heap object", nameof(objectRef));

        ObjectMemory.SetIndexedPointer(objectRef.AsObjectPointer(), index, valueRef);
    }

    public SObjectRef GetField(SObjectRef objectRef, int fieldIndex)
    {
        if (!objectRef.IsHeapObject)
            throw new ArgumentException("Cannot get field from non-heap object", nameof(objectRef));

        return ObjectMemory.GetInstanceVariable(objectRef.AsObjectPointer(), fieldIndex);
    }

    public SObjectRef GetIndexedField(SObjectRef objectRef, int index)
    {
        if (!objectRef.IsHeapObject)
            throw new ArgumentException("Cannot get indexed field from non-heap object", nameof(objectRef));

        return ObjectMemory.GetIndexedPointer(objectRef.AsObjectPointer(), index);
    }

    public void SetClass(SObjectRef objectRef, SObjectRef classRef)
    {
        if (!objectRef.IsHeapObject)
            throw new ArgumentException("Cannot set class on non-heap object", nameof(objectRef));

        ObjectMemory.SetClass(objectRef.AsObjectPointer(), classRef);
    }

    public SObjectRef GetClass(SObjectRef objectRef)
    {
        if (!objectRef.IsHeapObject)
            return SObjectRef.Nil;

        return ObjectMemory.GetClass(objectRef.AsObjectPointer());
    }

    public SObjectRef InternSymbol(string symbolName)
    {
        return ObjectMemory.InternSymbol(symbolName);
    }

    public SObjectRef CreateString(string stringValue)
    {
        return ObjectMemory.CreateString(stringValue);
    }

    public SObjectRef CreateCompiledMethod(SObjectRef header, SObjectRef[] literals, byte[] bytecodes)
    {
        if (!header.IsSmallInteger)
            throw new ArgumentException("Method header must be a SmallInteger", nameof(header));

        var methodClass = ObjectMemory.GetCompiledMethodClass();
        var literalCount = literals.Length;

        // CompiledMethod format: [header, literal1, literal2, ..., literalN, bytecode1, bytecode2, ...]
        // Fixed fields: header + literals
        // Indexed fields: bytecodes

        var oop = ObjectMemory.AllocateObject(
            methodClass,
            ObjectFormat.CompiledMethod,
            1 + literalCount, // header + literals as fixed fields
            bytecodes.Length  // bytecodes as indexed bytes
        );

        // Set header
        ObjectMemory.SetInstanceVariable(oop, 0, header);

        // Set literals
        for (int i = 0; i < literals.Length; i++)
        {
            ObjectMemory.SetInstanceVariable(oop, i + 1, literals[i]);
        }

        // Set bytecodes
        for (int i = 0; i < bytecodes.Length; i++)
        {
            ObjectMemory.SetIndexedByte(oop, i, bytecodes[i]);
        }

        return SObjectRef.FromObjectPointer(oop);
    }

    public SObjectRef CreateMethodDictionary(int capacity)
    {
        var dictClass = ObjectMemory.GetMethodDictionaryClass();

        // MethodDictionary has:
        // - Fixed fields: tally, array (instance variables)
        // - Indexed fields: selector-method pairs

        var oop = ObjectMemory.AllocateObject(
            dictClass,
            ObjectFormat.PointerIndexed,
            2, // tally and array instance variables
            capacity // capacity for selector slots
        );

        // Initialize tally to 0
        ObjectMemory.SetInstanceVariable(oop, 0, SObjectRef.FromSmallInteger(0));

        // Create values array
        var arrayClass = ObjectMemory.GetArrayClass();
        var valuesArray = ObjectMemory.AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            0,
            capacity
        );

        // Set array field
        ObjectMemory.SetInstanceVariable(oop, 1, SObjectRef.FromObjectPointer(valuesArray));

        return SObjectRef.FromObjectPointer(oop);
    }

    public void InstallMethod(SObjectRef classRef, SObjectRef selectorSymbol, SObjectRef methodRef)
    {
        if (!classRef.IsHeapObject)
            throw new ArgumentException("Class must be a heap object", nameof(classRef));

        classRef.AddMethod(selectorSymbol.ToString() ?? "", methodRef);
    }

    public SObjectRef CreateUSTable(int size)
    {
        // USTable is typically a Set or Dictionary
        var setClass = GetClassByName("Set");
        if (setClass.IsNil)
            throw new InvalidOperationException("Set class not found");

        var oop = ObjectMemory.AllocateObject(
            setClass,
            ObjectFormat.PointerIndexed,
            2, // tally and array instance variables
            size
        );

        // Initialize tally
        ObjectMemory.SetInstanceVariable(oop, 0, SObjectRef.FromSmallInteger(0));

        // Create array
        var arrayClass = ObjectMemory.GetArrayClass();
        var array = ObjectMemory.AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            0,
            size
        );

        ObjectMemory.SetInstanceVariable(oop, 1, SObjectRef.FromObjectPointer(array));

        return SObjectRef.FromObjectPointer(oop);
    }

    public void InsertIntoUSTable(SObjectRef ustableRef, SObjectRef symbolRef, int hash)
    {
        if (!ustableRef.IsHeapObject)
            throw new ArgumentException("USTable must be a heap object", nameof(ustableRef));

        // TODO: Implement USTable insertion logic using hash
        // This needs to match Smalltalk's Set/Dictionary insertion algorithm
        throw new NotImplementedException("USTable insertion not yet implemented");
    }

    public SObjectRef ExecuteMethod(SObjectRef methodRef, SObjectRef receiver, params SObjectRef[] arguments)
    {
        if (_interpreter == null)
            throw new InvalidOperationException("No interpreter available for method execution");

        return _interpreter.Execute(methodRef, receiver, arguments);
    }

    public SObjectRef GetClassByName(string className)
    {
        // TODO: Implement global lookup once SystemDictionary is available
        throw new NotImplementedException("Global class lookup not yet implemented");
    }

    public void RegisterGlobal(string globalName, SObjectRef objectRef)
    {
        // TODO: Implement global registration once SystemDictionary is available
        throw new NotImplementedException("Global registration not yet implemented");
    }

    public SObjectRef GetGlobal(string globalName)
    {
        // TODO: Implement global lookup once SystemDictionary is available
        throw new NotImplementedException("Global lookup not yet implemented");
    }
}
