using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides a very small object table used to assign and resolve object pointers.
/// </summary>
public static class ObjectMemory
{
    private static readonly List<SmalltalkObject> objects = new();

    /// <summary>Gets a read-only view of all registered objects.</summary>
    public static IReadOnlyList<SmalltalkObject> Objects => objects;
    
    /// <summary>The singleton nil object (instance of UndefinedObject).</summary>
    public static SmalltalkObject? NilObject { get; set; }
    
    /// <summary>The singleton true object (instance of True).</summary>
    public static SmalltalkObject? TrueObject { get; set; }
    
    /// <summary>The singleton false object (instance of False).</summary>
    public static SmalltalkObject? FalseObject { get; set; }

    /// <summary>
    /// Registers a new object with the object table assigning it an <c>oop</c> value.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    internal static void Register(SmalltalkObject obj)
    {
        int index = objects.Count;
        objects.Add(obj);
        obj.Oop = index << 1;
    }

    /// <summary>Clears the object table. Primarily used for tests.</summary>
    public static void Reset() => objects.Clear();

    /// <summary>Determines whether an object exists for the specified pointer.</summary>
    /// <param name="oop">The encoded object pointer.</param>
    public static bool HasObject(int oop) => (oop >> 1) >= 0 && (oop >> 1) < objects.Count;

    /// <summary>Resolves an encoded pointer to the corresponding object.</summary>
    /// <param name="oop">The encoded object pointer.</param>
    /// <returns>The referenced object or <c>null</c>.</returns>
    public static SmalltalkObject? FromOop(int oop)
    {
        int index = oop >> 1;
        return index >= 0 && index < objects.Count ? objects[index] : null;
    }

    /// <summary>Obtains the encoded pointer for the specified object.</summary>
    /// <param name="obj">The object to encode.</param>
    /// <returns>An integer encoding the object pointer.</returns>
    public static int ToOop(SmalltalkObject obj) => obj.Oop;

    /// <summary>
    /// Finds an arbitrary instance of the given class if one exists.
    /// </summary>
    /// <param name="cls">The class to search for.</param>
    /// <returns>The first instance found or <c>null</c>.</returns>
    public static SmalltalkObject? SomeInstance(SmalltalkObject cls)
        => objects.FirstOrDefault(o => ReferenceEquals(o.ClassPointer, cls));

    /// <summary>
    /// Returns the instance following the specified object in enumeration order.
    /// </summary>
    /// <param name="obj">An existing instance.</param>
    /// <returns>The next instance of the same class or <c>null</c>.</returns>
    public static SmalltalkObject? NextInstance(SmalltalkObject obj)
    {
        int start = obj.Oop >> 1;
        var cls = obj.ClassPointer;
        for (int i = start + 1; i < objects.Count; i++)
        {
            var candidate = objects[i];
            if (ReferenceEquals(candidate.ClassPointer, cls))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Swaps the identity of two objects and updates all references.
    /// </summary>
    /// <param name="a">The first object.</param>
    /// <param name="b">The second object.</param>
    public static void SwapPointers(SmalltalkObject a, SmalltalkObject b)
    {
        int indexA = a.Oop >> 1;
        int indexB = b.Oop >> 1;

        (objects[indexA], objects[indexB]) = (objects[indexB], objects[indexA]);
        (a.Oop, b.Oop) = (b.Oop, a.Oop);

        foreach (var obj in objects)
        {
            for (int i = 0; i < obj.Fields.Length; i++)
            {
                if (ReferenceEquals(obj.Fields[i], a)) obj.Fields[i] = b;
                else if (ReferenceEquals(obj.Fields[i], b)) obj.Fields[i] = a;
            }
        }
    }
    
    /// <summary>
    /// Replace an object at a specific OOP with a new object.
    /// Used to fix objects that were created incorrectly.
    /// </summary>
    public static void ReplaceObject(int oop, SmalltalkObject newObject)
    {
        if (oop >= 0 && oop < objects.Count)
        {
            var oldObject = objects[oop];
            objects[oop] = newObject;
            newObject.Oop = oop; // Ensure the new object has the correct OOP
        }
    }
}
