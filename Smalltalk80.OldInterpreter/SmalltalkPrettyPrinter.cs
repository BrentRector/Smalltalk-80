using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Pretty printer that examines actual Smalltalk objects in memory.
/// Uses ONLY Smalltalk objects - no C# shadow types or dictionaries.
/// Accesses the Smalltalk SystemDictionary to find classes.
/// </summary>
public class SmalltalkPrettyPrinter
{
    private readonly VMPrimitiveAdapter vm;
    private SmalltalkObject? smalltalkDict;
    
    public SmalltalkPrettyPrinter(VMPrimitiveAdapter vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Print the current state of the Smalltalk system.
    /// </summary>
    public void PrintSystemState()
    {
        Console.WriteLine("=== Smalltalk System State ===");
        Console.WriteLine();
        
        // Get all objects from ObjectMemory
        var allObjects = ObjectMemory.Objects.ToList();
        Console.WriteLine($"Total objects in memory: {allObjects.Count}");
        Console.WriteLine();
        
        // Find the Smalltalk SystemDictionary
        smalltalkDict = FindSmalltalkDictionary();
        if (smalltalkDict == null)
        {
            Console.WriteLine("Warning: Smalltalk SystemDictionary not found!");
            Console.WriteLine("The system may not be fully bootstrapped.");
            Console.WriteLine();
            return;
        }
        
        Console.WriteLine($"Found Smalltalk SystemDictionary at OOP {smalltalkDict.Oop}");
        Console.WriteLine();
        
        // Find class objects from the Smalltalk dictionary
        var classes = FindClassesFromSmalltalkDict();
        Console.WriteLine($"Classes registered in Smalltalk: {classes.Count}");
        Console.WriteLine();
        
        // Print each class
        foreach (var (classObj, name) in classes.OrderBy(c => c.name))
        {
            PrintClass(classObj, name);
        }
        
        // Print dictionary statistics
        PrintDictionaryStats();
    }
    
    /// <summary>
    /// Find the Smalltalk SystemDictionary by looking for it in globals.
    /// This is the ONLY place we use the C# globals - just to find Smalltalk itself.
    /// </summary>
    private SmalltalkObject? FindSmalltalkDictionary()
    {
        // We need to bootstrap - find "Smalltalk" using the VM primitive
        var smalltalkOop = vm.PrimitiveLookupGlobal("Smalltalk");
        if (smalltalkOop > 0)
        {
            return ObjectMemory.FromOop(smalltalkOop);
        }
        
        // Fallback: look for any SystemDictionary instance
        foreach (var obj in ObjectMemory.Objects)
        {
            if (obj != null && IsSystemDictionary(obj))
            {
                return obj;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if an object is a SystemDictionary by examining its class.
    /// </summary>
    private bool IsSystemDictionary(SmalltalkObject obj)
    {
        if (obj.ClassPointer == null) return false;
        
        // Get the class name by checking if it's registered as SystemDictionary
        var systemDictClassOop = vm.PrimitiveLookupGlobal("SystemDictionary");
        if (systemDictClassOop > 0 && obj.ClassPointer.Oop == systemDictClassOop)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Find classes by examining the Smalltalk dictionary's contents.
    /// Dictionary structure: parallel arrays or alternating key-value pairs.
    /// </summary>
    private List<(SmalltalkObject obj, string name)> FindClassesFromSmalltalkDict()
    {
        var classes = new List<(SmalltalkObject, string)>();
        
        if (smalltalkDict == null) return classes;
        
        // Dictionary internal structure varies, but typically has:
        // - A tally field (number of entries)
        // - Either parallel arrays (keys, values) or alternating entries
        
        // Try to interpret as a dictionary with parallel arrays first
        if (TryGetDictionaryContents(smalltalkDict, out var entries))
        {
            foreach (var (key, value) in entries)
            {
                if (IsClassObject(value))
                {
                    classes.Add((value, key));
                }
            }
        }
        
        return classes;
    }
    
    /// <summary>
    /// Try to extract key-value pairs from a dictionary object.
    /// </summary>
    private bool TryGetDictionaryContents(SmalltalkObject dict, out List<(string key, SmalltalkObject value)> entries)
    {
        entries = new List<(string, SmalltalkObject)>();
        
        // Dictionaries typically have a structure like:
        // Field 0: tally (number of entries)
        // Field 1+: either alternating key-value or parallel arrays
        
        if (dict.Fields.Length < 2) return false;
        
        // Our dictionary implementation uses alternating key-value pairs after tally field
        // Dictionary structure: [tally, key1, value1, key2, value2, ...]
        if (dict.Fields.Length > 1)
        {
            // Start from field 1 (skip tally at field 0)
            for (int i = 1; i < dict.Fields.Length - 1; i += 2)
            {
                var keyObj = dict.Fields[i];
                var valueObj = dict.Fields[i + 1];
                
                // Skip null entries (empty slots)
                if (keyObj == null || valueObj == null)
                    continue;
                
                if (keyObj is SmalltalkObject keySymbol && valueObj is SmalltalkObject value)
                {
                    var keyStr = ExtractSymbolString(keySymbol);
                    if (!string.IsNullOrEmpty(keyStr))
                    {
                        entries.Add((keyStr, value));
                    }
                }
            }
        }
        
        return entries.Count > 0;
    }
    
    /// <summary>
    /// Check if an array contains symbol objects.
    /// </summary>
    private bool ContainsSymbols(SmalltalkObject array)
    {
        foreach (var field in array.Fields)
        {
            if (field is SmalltalkObject obj && IsSymbol(obj))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Check if an array contains only symbols.
    /// </summary>
    private bool ContainsOnlySymbols(SmalltalkObject array)
    {
        if (array.Fields.Length == 0) return false;
        
        foreach (var field in array.Fields)
        {
            if (!(field is SmalltalkObject obj) || !IsSymbol(obj))
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Check if an object is a Symbol.
    /// </summary>
    private bool IsSymbol(SmalltalkObject obj)
    {
        if (obj.ClassPointer == null) return false;
        
        // Check if the class is Symbol
        var symbolClassOop = vm.PrimitiveLookupGlobal("Symbol");
        return symbolClassOop > 0 && obj.ClassPointer.Oop == symbolClassOop;
    }
    
    /// <summary>
    /// Extract the string value from a Symbol object.
    /// </summary>
    private string ExtractSymbolString(SmalltalkObject symbol)
    {
        // Symbols typically have their string value in the first field
        if (symbol.Fields.Length > 0 && symbol.Fields[0] is string str)
        {
            return str;
        }
        return "";
    }
    
    /// <summary>
    /// Determine if an object is a class by examining its structure.
    /// Classes have: superclass (slot 0), methodDict (slot 1), format (slot 2)
    /// </summary>
    private bool IsClassObject(SmalltalkObject obj)
    {
        if (obj == null || obj.Fields.Length < 3)
            return false;
            
        // Check if the object's class is a metaclass or Class
        if (obj.ClassPointer != null)
        {
            var classOop = vm.PrimitiveLookupGlobal("Class");
            var metaclassOop = vm.PrimitiveLookupGlobal("Metaclass");
            
            // If this object's class's class is Metaclass, then this object is a class
            // (because its class is a metaclass)
            if (obj.ClassPointer?.ClassPointer != null)
            {
                if (obj.ClassPointer.ClassPointer.Oop == metaclassOop)
                    return true;
            }
            
            // If this object is a direct instance of Class, it's a class
            if (obj.ClassPointer?.Oop == classOop)
                return true;
                
            // Check if the class pointer points to a metaclass
            // Note: Without the ability to enumerate globals, we can't check for metaclass naming pattern
            // This is a limitation of the new API but acceptable for the pretty printer
        }
        
        // Fallback: Check if slot 1 looks like a method dictionary
        if (obj.Fields.Length > 1)
        {
            var methodDictField = obj.Fields[1];
            if (methodDictField is SmalltalkObject methodDict)
            {
                // Method dictionaries should have structure
                return methodDict.Fields.Length >= 2;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Print information about a class.
    /// </summary>
    private void PrintClass(SmalltalkObject classObj, string className)
    {
        Console.WriteLine($"Class: {className}");
        
        // Get superclass (slot 0)
        if (classObj.Fields.Length > 0 && classObj.Fields[0] is SmalltalkObject superclass)
        {
            var superclassName = FindObjectName(superclass);
            Console.WriteLine($"  Superclass: {superclassName ?? $"OOP {superclass.Oop}"}");
        }
        else if (classObj.Fields.Length > 0 && classObj.Fields[0] == null)
        {
            Console.WriteLine($"  Superclass: nil");
        }
        
        // Count methods in method dictionary (slot 1)
        if (classObj.Fields.Length > 1 && classObj.Fields[1] is SmalltalkObject methodDict)
        {
            var methodCount = CountMethodsInDictionary(methodDict);
            Console.WriteLine($"  Methods: {methodCount}");
        }
        
        // Show instance variables (slot 3 for full classes)
        if (classObj.Fields.Length > 3 && classObj.Fields[3] is SmalltalkObject instVars)
        {
            var varNames = ExtractInstanceVariables(instVars);
            if (varNames.Count > 0)
            {
                Console.WriteLine($"  Instance Variables: {string.Join(", ", varNames)}");
            }
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Find the name of an object by looking it up in the Smalltalk dictionary.
    /// </summary>
    private string? FindObjectName(SmalltalkObject obj)
    {
        if (smalltalkDict == null) return null;
        
        if (TryGetDictionaryContents(smalltalkDict, out var entries))
        {
            foreach (var (key, value) in entries)
            {
                if (value.Oop == obj.Oop)
                {
                    return key;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Count the number of methods in a method dictionary.
    /// </summary>
    private int CountMethodsInDictionary(SmalltalkObject methodDict)
    {
        // Method dictionaries use alternating key-value pairs after the tally
        // Structure: [tally, key1, value1, key2, value2, ...]
        // Keys are selectors (Symbols) at odd indices, values are CompiledMethods at even indices
        int count = 0;
        
        // Get nil for comparison
        var nilOop = vm.PrimitiveNil();
        
        // Values are at even indices starting from 2: [tally@0, key@1, value@2, key@3, value@4, ...]
        for (int i = 2; i < methodDict.Fields.Length; i += 2)
        {
            var value = methodDict.Fields[i];
            
            // Skip if nil or null
            if (value == null)
                continue;
                
            if (value is SmalltalkObject valueObj)
            {
                // Skip if it's nil
                if (valueObj.Oop == nilOop)
                    continue;
                    
                // Check if it's a CompiledMethod
                if (IsCompiledMethod(valueObj))
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Check if an object is a CompiledMethod.
    /// </summary>
    private bool IsCompiledMethod(SmalltalkObject obj)
    {
        if (obj.ClassPointer == null) return false;
        
        var compiledMethodOop = vm.PrimitiveLookupGlobal("CompiledMethod");
        return compiledMethodOop > 0 && obj.ClassPointer.Oop == compiledMethodOop;
    }
    
    /// <summary>
    /// Extract instance variable names from an array.
    /// </summary>
    private List<string> ExtractInstanceVariables(SmalltalkObject instVarsArray)
    {
        var names = new List<string>();
        
        foreach (var field in instVarsArray.Fields)
        {
            if (field is SmalltalkObject symbol)
            {
                var name = ExtractSymbolString(symbol);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
            else if (field is string str)
            {
                names.Add(str);
            }
        }
        
        return names;
    }
    
    /// <summary>
    /// Print statistics about the Smalltalk dictionary.
    /// </summary>
    private void PrintDictionaryStats()
    {
        if (smalltalkDict == null) return;
        
        Console.WriteLine("=== Smalltalk Dictionary Statistics ===");
        
        if (TryGetDictionaryContents(smalltalkDict, out var entries))
        {
            Console.WriteLine($"Total entries: {entries.Count}");
            
            int classCount = 0;
            int symbolCount = 0;
            int otherCount = 0;
            
            foreach (var (key, value) in entries)
            {
                if (IsClassObject(value))
                    classCount++;
                else if (IsSymbol(value))
                    symbolCount++;
                else
                    otherCount++;
            }
            
            Console.WriteLine($"  Classes: {classCount}");
            Console.WriteLine($"  Symbols: {symbolCount}");
            Console.WriteLine($"  Other objects: {otherCount}");
        }
        else
        {
            Console.WriteLine("Unable to read dictionary contents");
        }
        
        Console.WriteLine();
    }
}