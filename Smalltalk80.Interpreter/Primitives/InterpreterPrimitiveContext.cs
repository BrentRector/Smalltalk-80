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

    /// <summary>
    /// Initializes a new instance of the InterpreterPrimitiveContext class.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    public InterpreterPrimitiveContext(Execution.ContextBasedInterpreterNew interpreter)
    {
        this.interpreter = interpreter;
    }

    /// <summary>
    /// Gets a special object from the Special Objects Array.
    /// </summary>
    /// <param name="index">The index of the special object.</param>
    /// <returns>The special object at the specified index.</returns>
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

    /// <summary>
    /// Gets a class object by name.
    /// </summary>
    /// <param name="className">The name of the class.</param>
    /// <returns>The class object, or Nil if not found.</returns>
    public SObjectRef GetClass(string className)
    {
        var classRef = SystemDictionary.LookupGlobalRef(className);
        return classRef ?? SObjectRef.Nil;
    }

    /// <summary>
    /// Creates a new instance of a class.
    /// </summary>
    /// <param name="classRef">The class to instantiate.</param>
    /// <param name="indexedSize">The size of the indexed portion.</param>
    /// <returns>A new instance of the class.</returns>
    public SObjectRef NewInstance(SObjectRef classRef, int indexedSize)
    {
        if (!classRef.IsHeapObject)
            return SObjectRef.Nil;

        // For now, use a simplified implementation
        // Assume PointerIndexed format with 0 instance variables
        var oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.PointerIndexed, 0, indexedSize);
        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Sends a message to a receiver.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    public SObjectRef Send(SObjectRef receiver, string selector, SObjectRef[] arguments)
    {
        // This would need to be implemented by calling back into the interpreter
        // For now, return nil - this will be needed for primitives like perform:
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Interns a symbol.
    /// </summary>
    /// <param name="value">The string value to intern.</param>
    /// <returns>The interned symbol.</returns>
    public SObjectRef InternSymbol(string value)
    {
        return ObjectMemoryNew.InternSymbol(value);
    }

    /// <summary>
    /// Checks if an object reference is a small integer.
    /// </summary>
    /// <param name="objRef">The object reference to check.</param>
    /// <returns>True if the object is a small integer.</returns>
    public bool IsSmallInteger(SObjectRef objRef)
    {
        return objRef.IsSmallInteger;
    }

    /// <summary>
    /// Checks if an object reference is a heap object.
    /// </summary>
    /// <param name="objRef">The object reference to check.</param>
    /// <returns>True if the object is a heap object.</returns>
    public bool IsHeapObject(SObjectRef objRef)
    {
        return objRef.IsHeapObject;
    }

    /// <summary>
    /// Gets the size of the indexed portion of an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <returns>The size of the indexed portion.</returns>
    public int GetIndexedSize(nint oop)
    {
        return ObjectMemoryNew.GetIndexedSize(oop);
    }

    /// <summary>
    /// Gets an indexed pointer from an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <param name="index">The index.</param>
    /// <returns>The value at the specified index.</returns>
    public SObjectRef GetIndexedPointer(nint oop, int index)
    {
        return ObjectMemoryNew.GetIndexedPointer(oop, index);
    }

    /// <summary>
    /// Sets an indexed pointer in an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <param name="index">The index.</param>
    /// <param name="value">The value to set.</param>
    public void SetIndexedPointer(nint oop, int index, SObjectRef value)
    {
        ObjectMemoryNew.SetIndexedPointer(oop, index, value);
    }

    /// <summary>
    /// Gets an instance variable from an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <param name="index">The instance variable index.</param>
    /// <returns>The value of the instance variable.</returns>
    public SObjectRef GetInstanceVariable(nint oop, int index)
    {
        return ObjectMemoryNew.GetInstanceVariable(oop, index);
    }

    /// <summary>
    /// Sets an instance variable in an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <param name="index">The instance variable index.</param>
    /// <param name="value">The value to set.</param>
    public void SetInstanceVariable(nint oop, int index, SObjectRef value)
    {
        ObjectMemoryNew.SetInstanceVariable(oop, index, value);
    }

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    /// <param name="oop">The object pointer.</param>
    /// <returns>The class of the object.</returns>
    public SObjectRef GetObjectClass(nint oop)
    {
        return ObjectMemoryNew.GetClass(oop);
    }

    /// <summary>
    /// Allocates a new object.
    /// </summary>
    /// <param name="classRef">The class of the object.</param>
    /// <param name="instanceVarCount">The number of instance variables.</param>
    /// <param name="indexedSize">The size of the indexed portion.</param>
    /// <returns>A pointer to the newly allocated object.</returns>
    public nint AllocateObject(SObjectRef classRef, int instanceVarCount, int indexedSize)
    {
        // Use PointerIndexed format by default
        return ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.PointerIndexed, instanceVarCount, indexedSize);
    }
}
