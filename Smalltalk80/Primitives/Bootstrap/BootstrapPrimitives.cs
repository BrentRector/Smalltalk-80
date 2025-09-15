using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Primitives.Bootstrap;

/// <summary>
/// Primitives for Phase 3 bootstrap - class creation and method installation.
/// These primitives enable Smalltalk to build itself via message sends.
/// </summary>
public static class BootstrapPrimitives
{
    // We'll need access to the VM - this would need to be set during initialization
    private static Smalltalk80.Bootstrap.ISmalltalkVM? vm;
    
    /// <summary>
    /// Set the VM instance for use by primitives.
    /// </summary>
    public static void SetVM(Smalltalk80.Bootstrap.ISmalltalkVM vmInstance)
    {
        vm = vmInstance;
    }
    
    /// <summary>
    /// Primitive 170: Create a new class and metaclass pair.
    /// This is used by Metaclass>>newNamed: to create the actual class objects.
    /// Arguments: [className (Symbol)]
    /// Receiver: The metaclass of the superclass
    /// Returns: The new class object
    /// </summary>
    public static bool CreateSubclass(Context ctx)
    {
        if (vm == null)
        {
            Console.WriteLine("[PRIMITIVE 170] ERROR: VM not initialized");
            return false;
        }
        
        // Get the class name argument
        var classNameObj = ctx.Stack.Pop();
        if (classNameObj == null)
        {
            Console.WriteLine("[PRIMITIVE 170] ERROR: Class name is nil");
            ctx.Stack.Push(null); // Push nil on failure
            return false;
        }
        
        // Pop receiver (the metaclass of the superclass)
        var superMetaclass = ctx.Stack.Pop();
        
        // Extract the class name string
        string className;
        if (classNameObj is SmalltalkObject symObj)
        {
            // It's a Symbol object - extract the string value
            // Symbols have their string in field 0 or as a byte array
            className = ExtractStringFromObject(symObj);
        }
        else if (classNameObj is string str)
        {
            className = str;
        }
        else
        {
            Console.WriteLine($"[PRIMITIVE 170] ERROR: Invalid class name type: {classNameObj?.GetType()}");
            ctx.Stack.Push(null);
            return false;
        }
        
        Console.WriteLine($"[PRIMITIVE 170] Creating class: {className}");
        
        // Create the new class object
        int classOop = vm.PrimitiveNewClass();
        
        // Create the new metaclass object
        int metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set up the metaclass relationship
        // The metaclass's thisClass field (slot 5) points to the class
        vm.PrimitiveSetSlot(metaclassOop, 5, classOop);
        
        // The class's class pointer points to its metaclass
        vm.PrimitiveSetClass(classOop, metaclassOop);
        
        // Set up superclass relationships
        if (superMetaclass is SmalltalkObject superMeta)
        {
            // Get the superclass from the superMetaclass's thisClass field
            var superClassOop = vm.GetSlot(superMeta.Oop, 5);
            if (superClassOop > 0)
            {
                // Set the class's superclass (slot 0)
                vm.PrimitiveSetSlot(classOop, 0, superClassOop);
                
                // Set the metaclass's superclass to the superclass's metaclass
                vm.PrimitiveSetSlot(metaclassOop, 0, superMeta.Oop);
            }
        }
        
        // Set the class name (slot 5)
        int nameSymbol = vm.PrimitiveInternSymbol(className);
        vm.PrimitiveSetSlot(classOop, 5, nameSymbol);
        
        // Initialize method dictionaries
        int methodDict = vm.PrimitiveNewDictionary();
        vm.PrimitiveSetSlot(classOop, 1, methodDict);
        
        int metaMethodDict = vm.PrimitiveNewDictionary();
        vm.PrimitiveSetSlot(metaclassOop, 1, metaMethodDict);
        
        // Initialize format (slot 2) - default format for pointer objects
        vm.PrimitiveSetSlot(classOop, 2, vm.PrimitiveNewSmallInteger(0));
        vm.PrimitiveSetSlot(metaclassOop, 2, vm.PrimitiveNewSmallInteger(0));
        
        // Register the class in Smalltalk dictionary
        vm.PrimitiveRegisterGlobal(className, classOop);
        
        // Get the actual class object to push
        // We need to find the object by its OOP
        SmalltalkObject? classObject = null;
        var objectArray = ObjectMemory.Objects;
        int index = classOop >> 1; // OOP is index * 2
        if (index >= 0 && index < objectArray.Count)
        {
            classObject = objectArray[index];
        }
        
        if (classObject == null)
        {
            Console.WriteLine($"[PRIMITIVE 170] ERROR: Could not find created class object at OOP {classOop}");
            ctx.Stack.Push(null);
            return false;
        }
        
        // Push the new class object
        ctx.Stack.Push(classObject);
        
        Console.WriteLine($"[PRIMITIVE 170] Successfully created class {className} (OOP {classOop}) with metaclass (OOP {metaclassOop})");
        return true;
    }
    
    /// <summary>
    /// Helper method to extract string from a Symbol or String object.
    /// </summary>
    private static string ExtractStringFromObject(SmalltalkObject obj)
    {
        // Try to get string from fields
        if (obj.Fields != null && obj.Fields.Length > 0)
        {
            // Check if first field is a string
            if (obj.Fields[0] is string str)
            {
                return str;
            }
            
            // Check if it's stored as bytes
            if (obj.Fields[0] is byte[] bytes)
            {
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            
            // Check if it's a ByteArray object stored in the fields
            if (obj.Fields[0] is SmalltalkObject byteArrayObj)
            {
                // Try to extract bytes from the ByteArray's fields
                var byteList = new List<byte>();
                foreach (var field in byteArrayObj.Fields)
                {
                    if (field is byte b)
                        byteList.Add(b);
                    else if (field is int i && i >= 0 && i <= 255)
                        byteList.Add((byte)i);
                }
                if (byteList.Count > 0)
                {
                    return System.Text.Encoding.UTF8.GetString(byteList.ToArray());
                }
            }
        }
        
        // Fallback: use the object's string representation
        return obj.ToString() ?? "UnknownClass";
    }
    
    /// <summary>
    /// Primitive 171: Compile a method (simplified).
    /// </summary>
    public static bool CompileMethod(Context ctx)
    {
        // Pop source code
        ctx.Stack.Pop();
        
        // Pop receiver (the class)
        ctx.Stack.Pop();
        
        // Push nil for now
        ctx.Stack.Push(null);
        
        Console.WriteLine("[PRIMITIVE 171] CompileMethod - simplified stub implementation");
        return true;
    }
    
    /// <summary>
    /// Primitive 172: Compile a method with category (simplified).
    /// </summary>
    public static bool CompileMethodClassified(Context ctx)
    {
        // Pop category
        ctx.Stack.Pop();
        
        // Pop source
        ctx.Stack.Pop();
        
        // Pop receiver (the class)
        ctx.Stack.Pop();
        
        // Push nil for now
        ctx.Stack.Push(null);
        
        Console.WriteLine("[PRIMITIVE 172] CompileMethodClassified - simplified stub implementation");
        return true;
    }
    
    /// <summary>
    /// Primitive 173: Create a class with full specification (simplified).
    /// </summary>
    public static bool CreateClassFull(Context ctx)
    {
        // Pop all 7 arguments
        for (int i = 0; i < 7; i++)
        {
            ctx.Stack.Pop();
        }
        
        // Pop receiver (Class class)
        ctx.Stack.Pop();
        
        // Push nil for now
        ctx.Stack.Push(null);
        
        Console.WriteLine("[PRIMITIVE 173] CreateClassFull - simplified stub implementation");
        return true;
    }
    
    /// <summary>
    /// Primitive 174: Install a method in a class (simplified).
    /// </summary>
    public static bool InstallMethodInClass(Context ctx)
    {
        // Pop target class
        ctx.Stack.Pop();
        
        // Pop method
        var method = ctx.Stack.Pop();
        
        // Push the method back
        ctx.Stack.Push(method);
        
        Console.WriteLine("[PRIMITIVE 174] InstallMethodInClass - simplified stub implementation");
        return true;
    }
    
    /// <summary>
    /// Primitive 175: Create a CompiledMethod from source (simplified).
    /// </summary>
    public static bool NewMethodFromSource(Context ctx)
    {
        // Pop source
        ctx.Stack.Pop();
        
        // Pop receiver (CompiledMethod class)
        ctx.Stack.Pop();
        
        // Create a simple CompiledMethod object
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { 112, 124 }, // push self, return
            Literals = new object[0]
        };
        
        ctx.Stack.Push(method);
        
        Console.WriteLine("[PRIMITIVE 175] NewMethodFromSource - created simple CompiledMethod");
        return true;
    }
}