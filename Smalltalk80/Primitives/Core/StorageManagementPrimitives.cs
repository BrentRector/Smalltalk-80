using System;
using System.Collections.Generic;

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
        Console.WriteLine("[PRIM70] Primitive 70 (New) called");
        if (ctx.Stack.Pop() is not SmalltalkObject cls)
        {
            Console.WriteLine("[PRIM70] Failed: receiver is not a SmalltalkObject");
            return false;
        }
        
        Console.WriteLine($"[PRIM70] Creating instance of class OOP {cls.Oop}");
        
        // Determine the number of instance variables for this class
        // This needs to walk up the class hierarchy and count all instance variables
        int fieldCount = CountInstanceVariables(cls);
        
        Console.WriteLine($"[PRIM70] Calculated field count: {fieldCount}");
        
        var instance = new SmalltalkObject(cls, fieldCount);
        ctx.Stack.Push(instance);
        
        Console.WriteLine($"[PRIM70] Success: created instance with {fieldCount} fields, OOP {instance.Oop}");
        Console.WriteLine($"[PRIM70] Returning true from primitive 70");
        return true;
    }
    
    /// <summary>
    /// Counts the total number of instance variables for a class including inherited ones.
    /// </summary>
    private static int CountInstanceVariables(SmalltalkObject cls)
    {
        int count = 0;
        var currentClass = cls;
        var visitedClasses = new HashSet<int>(); // Track visited OOPs to detect cycles
        
        Console.WriteLine($"[PRIM70] CountInstanceVariables starting with class OOP {cls.Oop}");
        
        // Walk up the superclass chain counting instance variables
        while (currentClass != null)
        {
            // Check for cycles
            if (!visitedClasses.Add(currentClass.Oop))
            {
                Console.WriteLine($"[PRIM70] WARNING: Cycle detected at class OOP {currentClass.Oop}");
                break;
            }
            
            Console.WriteLine($"[PRIM70] Checking class OOP {currentClass.Oop}");
            
            // Get instanceVariables from slot 3 (for ClassDescription and subclasses)
            // This is at index 3 for ClassDescription: [superclass, methodDict, format, instanceVariables, ...]
            if (currentClass.Fields.Length > 3 && currentClass.Fields[3] is SmalltalkObject instVars)
            {
                // instanceVariables is an Array of Symbol objects
                int varCount = instVars.Fields.Length;
                count += varCount;
                Console.WriteLine($"[PRIM70]   Found {varCount} instance variables");
            }
            
            // Get superclass from slot 0
            if (currentClass.Fields.Length > 0)
            {
                var superclassField = currentClass.Fields[0];
                
                // Check if it's nil (the Smalltalk nil object)
                if (superclassField == null || superclassField == ObjectMemory.NilObject)
                {
                    Console.WriteLine($"[PRIM70]   Superclass is nil, stopping");
                    break;
                }
                
                if (superclassField is SmalltalkObject superclass)
                {
                    Console.WriteLine($"[PRIM70]   Moving to superclass OOP {superclass.Oop}");
                    currentClass = superclass;
                }
                else
                {
                    Console.WriteLine($"[PRIM70]   Superclass field is not a SmalltalkObject, stopping");
                    break;
                }
            }
            else
            {
                Console.WriteLine($"[PRIM70]   No fields in class, stopping");
                break;
            }
        }
        
        // Fallback for core classes that have known field counts
        if (count == 0)
        {
            // Check the class name to determine field count for bootstrap classes
            // This is a workaround for bootstrap phase where instance variables might not be set yet
            var className = GetClassName(cls);
            count = className switch
            {
                "Metaclass" or "MetaclassClass" => 6,     // 3 from Behavior + 2 from ClassDescription + 1 from Metaclass
                "Class" or "ClassClass" => 6,             // 3 from Behavior + 2 from ClassDescription + 1 from Class
                "ClassDescription" => 5,                  // 3 from Behavior + 2 from ClassDescription
                "Behavior" => 3,                          // superclass, methodDict, format
                "Object" => 0,                            // No instance variables
                "Association" => 2,                       // key, value
                "LookupKey" => 1,                        // key
                "CompiledMethod" => 4,                    // selector, bytecode, literals, header
                "MethodDictionary" or "Dictionary" => 1,  // tally (plus variable part)
                "Symbol" or "String" => 1,                // value/characters
                _ => 0
            };
        }
        
        return count;
    }
    
    /// <summary>
    /// Attempts to get the class name for debugging/bootstrap purposes.
    /// </summary>
    private static string? GetClassName(SmalltalkObject cls)
    {
        // Try to get the name from slot 5 (for Class objects)
        if (cls.Fields.Length > 5 && cls.Fields[5] is Symbol nameSymbol)
        {
            return nameSymbol.Value;
        }
        
        // For metaclasses, try to get the thisClass and then its name
        if (cls.Fields.Length > 5 && cls.Fields[5] is SmalltalkObject thisClass)
        {
            if (thisClass.Fields.Length > 5 && thisClass.Fields[5] is Symbol className)
            {
                return className.Value + "Class";
            }
        }
        
        return null;
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
