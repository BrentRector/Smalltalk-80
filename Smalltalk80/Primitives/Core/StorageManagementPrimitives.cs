using System;

namespace Smalltalk80;

/// <summary>
/// Implements storage management primitives such as instance creation,
/// pointer conversion and object enumeration.
/// </summary>
public static class StorageManagementPrimitives
{
    /// <summary>
    /// Retrieves the pointer field at the given one-based index.
    /// </summary>
    public static bool ObjectAt(Context ctx)
    {
        if (ctx.Stack.Pop() is not int index || ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        if (index < 1 || index > obj.Fields.Length)
            return false;
        ctx.Stack.Push(obj.Fields[index - 1]);
        return true;
    }

    /// <summary>
    /// Stores a value into the pointer field at the specified index.
    /// </summary>
    public static bool ObjectAtPut(Context ctx)
    {
        var value = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not int index || ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        if (index < 1 || index > obj.Fields.Length)
            return false;
        obj.Fields[index - 1] = value;
        ctx.Stack.Push(value);
        return true;
    }

    /// <summary>Creates a new instance of a class with no indexable fields.</summary>
    public static bool New(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject cls)
            return false;
        var instance = new SmalltalkObject(cls, 0);
        ctx.Stack.Push(instance);
        return true;
    }

    /// <summary>Creates a new instance of a class with a given field count.</summary>
    public static bool NewWithArg(Context ctx)
    {
        if (ctx.Stack.Pop() is not int size || ctx.Stack.Pop() is not SmalltalkObject cls)
            return false;
        var instance = new SmalltalkObject(cls, size);
        ctx.Stack.Push(instance);
        return true;
    }

    /// <summary>Swaps the identities of two objects.</summary>
    public static bool Become(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject other || ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
        ObjectMemory.SwapPointers(receiver, other);
        UpdateContexts(ctx, receiver, other);
        ctx.Stack.Push(receiver);
        return true;
    }

    private static void UpdateContexts(Context? ctx, SmalltalkObject a, SmalltalkObject b)
    {
        for (var c = ctx; c != null; c = c.Sender)
        {
            UpdateArray(c.Temporaries, a, b);
            UpdateArray(c.Arguments, a, b);
            if (ReferenceEquals(c.Receiver, a)) c.Receiver = b;
            else if (ReferenceEquals(c.Receiver, b)) c.Receiver = a;
            var items = c.Stack.ToArray();
            bool changed = false;
            for (int i = 0; i < items.Length; i++)
            {
                if (ReferenceEquals(items[i], a)) { items[i] = b; changed = true; }
                else if (ReferenceEquals(items[i], b)) { items[i] = a; changed = true; }
            }
            if (changed)
            {
                c.Stack.Clear();
                for (int i = items.Length - 1; i >= 0; i--)
                    c.Stack.Push(items[i]);
            }
        }
    }

    private static void UpdateArray(object?[] array, SmalltalkObject a, SmalltalkObject b)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (ReferenceEquals(array[i], a)) array[i] = b;
            else if (ReferenceEquals(array[i], b)) array[i] = a;
        }
    }

    /// <summary>Retrieves an object's instance variable by index.</summary>
    public static bool InstVarAt(Context ctx) => ObjectAt(ctx);

    /// <summary>Sets an object's instance variable by index.</summary>
    public static bool InstVarAtPut(Context ctx) => ObjectAtPut(ctx);

    /// <summary>Converts an object to its encoded pointer value.</summary>
    public static bool AsOop(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        ctx.Stack.Push(ObjectMemory.ToOop(obj));
        return true;
    }

    /// <summary>Resolves a pointer to the corresponding object.</summary>
    public static bool AsObject(Context ctx)
    {
        if (ctx.Stack.Pop() is not int oop)
            return false;
        var obj = ObjectMemory.FromOop(oop);
        if (obj == null)
            return false;
        ctx.Stack.Push(obj);
        return true;
    }

    /// <summary>Pushes an arbitrary instance of the specified class.</summary>
    public static bool SomeInstance(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject cls)
            return false;
        var instance = ObjectMemory.SomeInstance(cls);
        if (instance == null) return false;
        ctx.Stack.Push(instance);
        return true;
    }

    /// <summary>Pushes the next instance of the same class.</summary>
    public static bool NextInstance(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        var next = ObjectMemory.NextInstance(obj);
        if (next == null) return false;
        ctx.Stack.Push(next);
        return true;
    }

    /// <summary>Creates a new CompiledMethod-like object.</summary>
    public static bool NewMethod(Context ctx)
    {
        if (ctx.Stack.Pop() is not int header || ctx.Stack.Pop() is not int byteCount || ctx.Stack.Pop() is not SmalltalkObject cls)
            return false;
        var method = new SmalltalkObject(cls, byteCount + 1);
        method.Fields[0] = header;
        ctx.Stack.Push(method);
        return true;
    }
}
