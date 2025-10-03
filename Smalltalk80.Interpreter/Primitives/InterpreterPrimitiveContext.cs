using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Implementation of IPrimitiveContext that provides VM services to primitives.
/// </summary>
public class InterpreterPrimitiveContext : IPrimitiveContext
{
    private readonly Execution.ContextBasedInterpreterNew interpreter;

    public InterpreterPrimitiveContext(Execution.ContextBasedInterpreterNew interpreter)
    {
        this.interpreter = interpreter;
    }

    public SObjectRef GetSpecialObject(int index)
    {
        // Get the special object reference directly from SOA
        // The SOA stores old-style SObject instances, but we need SObjectRef
        // For now, use a simple mapping for the common cases
        switch (index)
        {
            case 0: return SObjectRef.Nil;
            case 1: return SystemDictionary.LookupGlobalRef("false") ?? SObjectRef.Nil;
            case 2: return SystemDictionary.LookupGlobalRef("true") ?? SObjectRef.Nil;
            default:
                // For other indices, return nil for now
                return SObjectRef.Nil;
        }
    }

    public SObjectRef GetClass(string className)
    {
        var classRef = SystemDictionary.LookupGlobalRef(className);
        return classRef ?? SObjectRef.Nil;
    }

    public SObjectRef NewInstance(SObjectRef classRef, int indexedSize)
    {
        if (!classRef.IsHeapObject)
            return SObjectRef.Nil;

        // For now, use a simplified implementation
        // Assume PointerIndexed format with 0 instance variables
        var oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.PointerIndexed, 0, indexedSize);
        return SObjectRef.FromObjectPointer(oop);
    }

    public SObjectRef Send(SObjectRef receiver, string selector, SObjectRef[] arguments)
    {
        // This would need to be implemented by calling back into the interpreter
        // For now, return nil - this will be needed for primitives like perform:
        return SObjectRef.Nil;
    }

    public SObjectRef InternSymbol(string value)
    {
        return ObjectMemoryNew.InternSymbol(value);
    }

    public bool IsSmallInteger(SObjectRef objRef)
    {
        return objRef.IsSmallInteger;
    }

    public bool IsHeapObject(SObjectRef objRef)
    {
        return objRef.IsHeapObject;
    }

    public int GetIndexedSize(nint oop)
    {
        return ObjectMemoryNew.GetIndexedSize(oop);
    }

    public SObjectRef GetIndexedPointer(nint oop, int index)
    {
        return ObjectMemoryNew.GetIndexedPointer(oop, index);
    }

    public void SetIndexedPointer(nint oop, int index, SObjectRef value)
    {
        ObjectMemoryNew.SetIndexedPointer(oop, index, value);
    }

    public SObjectRef GetInstanceVariable(nint oop, int index)
    {
        return ObjectMemoryNew.GetInstanceVariable(oop, index);
    }

    public void SetInstanceVariable(nint oop, int index, SObjectRef value)
    {
        ObjectMemoryNew.SetInstanceVariable(oop, index, value);
    }

    public SObjectRef GetObjectClass(nint oop)
    {
        return ObjectMemoryNew.GetClass(oop);
    }

    public nint AllocateObject(SObjectRef classRef, int instanceVarCount, int indexedSize)
    {
        // Use PointerIndexed format by default
        return ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.PointerIndexed, instanceVarCount, indexedSize);
    }
}
