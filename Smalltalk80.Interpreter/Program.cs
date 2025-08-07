using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Smalltalk-80 Interpreter - Runs the Smalltalk virtual machine and executes Smalltalk code.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================");
        Console.WriteLine();

        try
        {
            Console.WriteLine("Initializing Virtual Machine...");
            
            // Create the VM with bootstrap only (no standard library for now)
            var vm = new VirtualMachine(skipStandardLibrary: true);
            
            Console.WriteLine("✓ Virtual Machine created");
            Console.WriteLine();
            
            // Phase 1 and Phase 2 bootstraps are executed in the VirtualMachine constructor
            // Now pretty print the VM state
            PrettyPrintVMState(vm);
            
            // If additional files were specified, load them
            if (args.Length > 0)
            {
                foreach (var file in args)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Loading: {file}");
                    try
                    {
                        vm.LoadSourceFile(file);
                        Console.WriteLine($"✓ Successfully loaded: {file}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed to load {file}: {ex.Message}");
                    }
                }
                
                // Print updated state
                Console.WriteLine();
                Console.WriteLine("=== Updated VM State After Loading Files ===");
                PrettyPrintVMState(vm);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Pretty prints the internal state of the Smalltalk VM.
    /// </summary>
    private static void PrettyPrintVMState(VirtualMachine vm)
    {
        Console.WriteLine("=== Smalltalk VM State ===");
        Console.WriteLine();
        
        // Get all objects from the VM's object memory
        var allObjects = ObjectMemory.Objects.ToList();
        
        Console.WriteLine($"Total objects in memory: {allObjects.Count}");
        Console.WriteLine();
        
        // Find class-like objects (objects that look like classes)
        var classObjects = new List<(SmalltalkObject obj, string name)>();
        
        foreach (var obj in allObjects)
        {
            // Try to determine if this is a class object
            if (IsLikelyClass(obj))
            {
                var className = GetClassName(vm, obj);
                if (!string.IsNullOrEmpty(className))
                {
                    classObjects.Add((obj, className));
                }
            }
        }
        
        // Sort classes by name
        classObjects.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        
        Console.WriteLine($"Classes found: {classObjects.Count}");
        Console.WriteLine("=" + new string('=', 40));
        
        foreach (var (classObj, className) in classObjects)
        {
            PrettyPrintClass(vm, classObj, className);
        }
        
        // Print global variables
        Console.WriteLine();
        Console.WriteLine("=== Global Variables ===");
        var globals = vm.GetGlobalVariables();
        foreach (var kvp in globals.OrderBy(k => k.Key))
        {
            var valueDesc = DescribeObject(vm, kvp.Value);
            Console.WriteLine($"  {kvp.Key} = {valueDesc}");
        }
    }
    
    /// <summary>
    /// Pretty prints a single class object.
    /// </summary>
    private static void PrettyPrintClass(VirtualMachine vm, SmalltalkObject classObj, string className)
    {
        Console.WriteLine();
        Console.WriteLine($"Class: {className}");
        Console.WriteLine("-" + new string('-', className.Length + 6));
        
        // Get superclass
        var superclass = GetSuperclass(classObj);
        if (superclass != null)
        {
            var superclassName = GetClassName(vm, superclass);
            Console.WriteLine($"  Superclass: {superclassName ?? "Unknown"}");
        }
        else
        {
            Console.WriteLine($"  Superclass: nil");
        }
        
        // Get type (regular class or metaclass)
        var isMetaclass = className.EndsWith(" class");
        Console.WriteLine($"  Type: {(isMetaclass ? "Metaclass" : "Class")}");
        
        // Get instance variables
        var instVars = GetInstanceVariables(classObj);
        if (instVars.Count > 0)
        {
            Console.WriteLine($"  Instance Variables: {string.Join(", ", instVars)}");
        }
        
        // Get methods
        var methods = GetMethods(classObj);
        if (methods.Count > 0)
        {
            Console.WriteLine($"  Methods ({methods.Count}):");
            foreach (var method in methods.OrderBy(m => m.selector))
            {
                PrintMethod(method.selector, method.method);
            }
        }
    }
    
    /// <summary>
    /// Prints information about a method.
    /// </summary>
    private static void PrintMethod(string selector, CompiledMethod method)
    {
        Console.Write($"    {selector}");
        
        // Show method header info
        var header = method.Header;
        Console.Write($" (temps: {header.TemporaryCount}, args: {header.ArgumentCount})");
        
        // Show bytecode length
        if (method.Bytecode != null && method.Bytecode.Length > 0)
        {
            Console.Write($" [{method.Bytecode.Length} bytes]");
            
            // Show first few bytecodes if small
            if (method.Bytecode.Length <= 10)
            {
                Console.Write(" <");
                Console.Write(string.Join(" ", method.Bytecode.Select(b => b.ToString("X2"))));
                Console.Write(">");
            }
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Determines if an object looks like a class.
    /// </summary>
    private static bool IsLikelyClass(SmalltalkObject obj)
    {
        // Classes typically have certain fields set
        // This is a heuristic - adjust based on actual VM implementation
        if (obj == null) return false;
        
        // Check if it has a method dictionary
        if (obj.MethodDictionary != null && obj.MethodDictionary.Count > 0)
            return true;
            
        // Check if it's referenced as a class by other objects
        // Or has other class-like properties
        return false;
    }
    
    /// <summary>
    /// Gets the name of a class object.
    /// </summary>
    private static string? GetClassName(VirtualMachine vm, SmalltalkObject classObj)
    {
        // Try to find the class name
        // This might be stored in various ways depending on the VM implementation
        
        // First check if this object is registered as a global
        var globals = vm.GetGlobalVariables();
        foreach (var kvp in globals)
        {
            if (ReferenceEquals(kvp.Value, classObj))
            {
                return kvp.Key;
            }
        }
        
        // For metaclasses, the name might be derived
        // Check if there's a 'thisClass' field pointing to the instance side
        if (classObj.TryGetField("thisClass", out var thisClass) && thisClass is SmalltalkObject thisClassObj)
        {
            var baseName = GetClassName(vm, thisClassObj);
            if (baseName != null)
            {
                return baseName + " class";
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the superclass of a class object.
    /// </summary>
    private static SmalltalkObject? GetSuperclass(SmalltalkObject classObj)
    {
        return classObj.Superclass;
    }
    
    /// <summary>
    /// Gets the instance variables of a class.
    /// </summary>
    private static List<string> GetInstanceVariables(SmalltalkObject classObj)
    {
        if (classObj.InstanceVariables != null)
        {
            return classObj.InstanceVariables;
        }
        return new List<string>();
    }
    
    /// <summary>
    /// Gets the methods of a class.
    /// </summary>
    private static List<(string selector, CompiledMethod method)> GetMethods(SmalltalkObject classObj)
    {
        var methods = new List<(string selector, CompiledMethod method)>();
        
        if (classObj.MethodDictionary != null)
        {
            foreach (var kvp in classObj.MethodDictionary)
            {
                methods.Add((kvp.Key.Value, kvp.Value));
            }
        }
        
        return methods;
    }
    
    /// <summary>
    /// Provides a string description of an object.
    /// </summary>
    private static string DescribeObject(VirtualMachine vm, SmalltalkObject? obj)
    {
        if (obj == null)
            return "nil";
            
        // Check if it's a known special object
        // TODO: Check against global objects once ObjectMemory is updated
            
        // Check if it's a class
        var className = GetClassName(vm, obj);
        if (!string.IsNullOrEmpty(className))
            return $"<Class: {className}>";
            
        // Check if it's a small integer
        if (obj.IsSmallInteger)
            return obj.SmallIntegerValue.ToString();
            
        // Check if it's a string
        if (obj.TryGetStringValue(out var strValue))
            return $"'{strValue}'";
            
        // Check if it's a symbol
        // Symbol would need special handling
            
        // Default description
        return $"<{obj.GetType().Name}@{obj.GetHashCode():X}>";
    }
}