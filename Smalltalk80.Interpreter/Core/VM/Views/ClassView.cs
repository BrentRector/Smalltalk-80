using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Views;

/// <summary>
/// Type-safe view of a Class per Blue Book Chapter 26.
/// Encapsulates field access without exposing raw OOP indices.
///
/// Class hierarchy (Behavior -> ClassDescription -> Class):
///   0: superclass
///   1: methodDict
///   2: format
///   3: subclasses (ClassDescription)
///   4: instanceVariables (ClassDescription)
///   5: organization (ClassDescription)
///   6: name (Class)
///   7: classPool (Class)
///   8: sharedPools (Class)
/// </summary>
public readonly struct ClassView
{
    private readonly SmalltalkOOP _oop;
    private readonly ISmalltalkHeapManager _heap;

    /// <summary>
    /// Creates a view of a Class.
    /// </summary>
    /// <param name="oop">The Class OOP.</param>
    /// <param name="heap">The heap manager for accessing fields.</param>
    public ClassView(SmalltalkOOP oop, ISmalltalkHeapManager heap)
    {
        _oop = oop;
        _heap = heap;
    }

    /// <summary>Gets the underlying OOP.</summary>
    public SmalltalkOOP OOP => _oop;

    /// <summary>Gets the superclass.</summary>
    public SmalltalkOOP Superclass => _heap.GetInstanceVariable(_oop, 0);

    /// <summary>Gets the method dictionary.</summary>
    public SmalltalkOOP MethodDict => _heap.GetInstanceVariable(_oop, 1);

    /// <summary>Gets the format word (encodes instance variable count and storage format).</summary>
    public int Format => _heap.GetInstanceVariable(_oop, 2).AsSmallInteger();

    /// <summary>Gets the subclasses collection.</summary>
    public SmalltalkOOP Subclasses => _heap.GetInstanceVariable(_oop, 3);

    /// <summary>Gets the instance variables array.</summary>
    public SmalltalkOOP InstanceVariables => _heap.GetInstanceVariable(_oop, 4);

    /// <summary>Gets the class organization.</summary>
    public SmalltalkOOP Organization => _heap.GetInstanceVariable(_oop, 5);

    /// <summary>Gets the class name (Symbol).</summary>
    public SmalltalkOOP Name => _heap.GetInstanceVariable(_oop, 6);

    /// <summary>Gets the class pool dictionary.</summary>
    public SmalltalkOOP ClassPool => _heap.GetInstanceVariable(_oop, 7);

    /// <summary>Gets the shared pools array.</summary>
    public SmalltalkOOP SharedPools => _heap.GetInstanceVariable(_oop, 8);

    /// <summary>Gets the FormatWord for semantic format decoding.</summary>
    public FormatWord FormatWord => new FormatWord(Format);

    /// <summary>Gets the number of fixed instance variables.</summary>
    public int InstanceVariableCount => FormatWord.FixedFields;

    /// <summary>Gets whether instances are indexable.</summary>
    public bool IsIndexable => FormatWord.IsIndexable;

    /// <summary>Gets whether instances have pointer fields (vs bytes/words).</summary>
    public bool HasPointerFields => FormatWord.IsPointers;

    /// <summary>Gets the class name as a string.</summary>
    public string GetClassName()
    {
        var nameOOP = Name;
        if (nameOOP == metadata.KnownObjects.Nil || nameOOP.IsSmallInteger)
            return "<anonymous>";

        int length = _heap.GetIndexedSize(nameOOP);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)_heap.GetIndexedByte(nameOOP, i);
        }
        return new string(chars);
    }

    /// <summary>Sets the superclass.</summary>
    public void SetSuperclass(SmalltalkOOP superclass) =>
        _heap.SetInstanceVariable(_oop, 0, superclass);

    /// <summary>Sets the method dictionary.</summary>
    public void SetMethodDict(SmalltalkOOP methodDict) =>
        _heap.SetInstanceVariable(_oop, 1, methodDict);

    /// <summary>Sets the format word.</summary>
    public void SetFormat(int format) =>
        _heap.SetInstanceVariable(_oop, 2, SmalltalkOOP.FromSmallInteger(format));

    /// <summary>Sets the class name.</summary>
    public void SetName(SmalltalkOOP name) =>
        _heap.SetInstanceVariable(_oop, 6, name);

    /// <summary>Sets the class pool.</summary>
    public void SetClassPool(SmalltalkOOP classPool) =>
        _heap.SetInstanceVariable(_oop, 7, classPool);

    /// <summary>Sets the shared pools.</summary>
    public void SetSharedPools(SmalltalkOOP sharedPools) =>
        _heap.SetInstanceVariable(_oop, 8, sharedPools);

    /// <summary>Gets a view of the superclass.</summary>
    public ClassView GetSuperclassView() => new(Superclass, _heap);

    /// <summary>Checks if this is a valid class (not nil).</summary>
    public bool IsValid => _oop != metadata.KnownObjects.Nil && !_oop.IsSmallInteger;
}
