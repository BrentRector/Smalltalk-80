using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Unified pretty printer for Smalltalk system state with configurable verbosity levels.
/// Replaces both SmalltalkPrettyPrinter and SmalltalkPrettyPrinterEnhanced.
/// </summary>
public class SmalltalkPrettyPrinterUnified
{
    /// <summary>
    /// Verbosity levels for output detail.
    /// </summary>
    public enum VerbosityLevel
    {
        /// <summary>System overview: object count, class list with method counts only</summary>
        Overview = 1,
        
        /// <summary>Class details: hierarchy, instance variables, method signatures</summary>
        Detailed = 2,
        
        /// <summary>Full analysis: bytecode disassembly, literals, method headers</summary>
        Complete = 3
    }

    private readonly VMPrimitiveAdapter vm;
    private SmalltalkObject? smalltalkDict;
    private readonly HashSet<string> classFilter = new();
    private readonly VerbosityLevel verbosity;
    private readonly bool showBytecode;
    private readonly bool showMnemonics;
    
    public SmalltalkPrettyPrinterUnified(VMPrimitiveAdapter vm, VerbosityLevel verbosity = VerbosityLevel.Overview, 
                                         bool showBytecode = true, bool showMnemonics = true)
    {
        this.vm = vm;
        this.verbosity = verbosity;
        this.showBytecode = showBytecode;
        this.showMnemonics = showMnemonics;
    }
    
    /// <summary>
    /// Set which classes to print. If empty, prints all classes.
    /// </summary>
    public void SetClassFilter(params string[] classNames)
    {
        classFilter.Clear();
        foreach (var name in classNames)
        {
            classFilter.Add(name);
        }
    }
    
    /// <summary>
    /// Print the current state of the Smalltalk system.
    /// </summary>
    public void PrintSystemState()
    {
        PrintHeader();
        
        // Find the Smalltalk SystemDictionary
        smalltalkDict = FindSmalltalkDictionary();
        if (smalltalkDict == null)
        {
            Console.WriteLine("Warning: Smalltalk SystemDictionary not found!");
            Console.WriteLine("The system may not be fully bootstrapped.");
            return;
        }
        
        // Show object memory stats for all verbosity levels
        if (verbosity >= VerbosityLevel.Overview)
        {
            PrintObjectMemoryOverview();
        }
        
        // Find and filter classes
        var classes = FindClassesFromSmalltalkDict();
        if (classFilter.Count > 0)
        {
            classes = classes.Where(c => classFilter.Contains(c.name)).ToList();
            Console.WriteLine($"Showing {classes.Count} filtered classes (of {FindClassesFromSmalltalkDict().Count} total)");
        }
        else
        {
            Console.WriteLine($"Classes registered in Smalltalk: {classes.Count}");
        }
        Console.WriteLine();
        
        // Print classes based on verbosity level
        foreach (var (classObj, name) in classes.OrderBy(c => c.name))
        {
            switch (verbosity)
            {
                case VerbosityLevel.Overview:
                    PrintClassOverview(classObj, name);
                    break;
                case VerbosityLevel.Detailed:
                    PrintClassDetailed(classObj, name);
                    break;
                case VerbosityLevel.Complete:
                    PrintClassComplete(classObj, name);
                    break;
            }
        }
        
        // Show dictionary stats for detailed and complete levels
        if (verbosity >= VerbosityLevel.Detailed)
        {
            PrintDictionaryStats();
        }
    }
    
    private void PrintHeader()
    {
        Console.WriteLine("=== Smalltalk System State ===");
        Console.WriteLine($"Verbosity Level: {verbosity}");
        Console.WriteLine();
    }
    
    private void PrintObjectMemoryOverview()
    {
        var allObjects = ObjectMemory.Objects.ToList();
        Console.WriteLine($"Total objects in memory: {allObjects.Count}");
        Console.WriteLine($"Found Smalltalk SystemDictionary at OOP {smalltalkDict!.Oop}");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Print basic class information (verbosity level 1).
    /// </summary>
    private void PrintClassOverview(SmalltalkObject classObj, string className)
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
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Print detailed class information including instance variables and method signatures (verbosity level 2).
    /// </summary>
    private void PrintClassDetailed(SmalltalkObject classObj, string className)
    {
        Console.WriteLine($"Class: {className}");
        Console.WriteLine("-" + new string('-', className.Length + 6));
        
        // Get superclass (slot 0)
        if (classObj.Fields.Length > 0)
        {
            if (classObj.Fields[0] is SmalltalkObject superclass)
            {
                var superclassName = FindObjectName(superclass);
                Console.WriteLine($"  Superclass: {superclassName ?? $"OOP {superclass.Oop}"}");
            }
            else if (classObj.Fields[0] == null)
            {
                Console.WriteLine($"  Superclass: nil");
            }
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
        
        // Get and print method signatures
        if (classObj.Fields.Length > 1 && classObj.Fields[1] is SmalltalkObject methodDict)
        {
            var methods = ExtractMethodsFromDictionary(methodDict);
            if (methods.Count > 0)
            {
                Console.WriteLine($"  Methods ({methods.Count}):");
                foreach (var (selector, method) in methods.OrderBy(m => m.selector))
                {
                    PrintMethodSignature(selector, method);
                }
            }
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Print complete class information including full bytecode analysis (verbosity level 3).
    /// </summary>
    private void PrintClassComplete(SmalltalkObject classObj, string className)
    {
        Console.WriteLine($"================================================================================");
        Console.WriteLine($"Class: {className}");
        Console.WriteLine($"================================================================================");
        
        // Get superclass (slot 0)
        if (classObj.Fields.Length > 0)
        {
            if (classObj.Fields[0] is SmalltalkObject superclass)
            {
                var superclassName = FindObjectName(superclass);
                Console.WriteLine($"  Superclass: {superclassName ?? $"OOP {superclass.Oop}"}");
            }
            else if (classObj.Fields[0] == null)
            {
                Console.WriteLine($"  Superclass: nil");
            }
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
        
        // Get and print method dictionary with full details
        if (classObj.Fields.Length > 1 && classObj.Fields[1] is SmalltalkObject methodDict)
        {
            PrintMethodDictionaryComplete(methodDict);
        }
        else
        {
            Console.WriteLine("  No method dictionary found");
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Print method signature only (verbosity level 2).
    /// </summary>
    private void PrintMethodSignature(string selector, SmalltalkObject method)
    {
        // Get header for argument/temp count
        int argCount = 0, tempCount = 0, primitive = 0;
        if (method.Fields.Length > 3 && method.Fields[3] != null)
        {
            ExtractMethodHeader(method.Fields[3]!, out argCount, out tempCount, out primitive);
        }
        
        var primitiveInfo = primitive > 0 ? $" <primitive: {primitive}>" : "";
        Console.WriteLine($"    {selector} (args: {argCount}, temps: {tempCount}){primitiveInfo}");
    }
    
    /// <summary>
    /// Print the method dictionary with complete bytecode details (verbosity level 3).
    /// </summary>
    private void PrintMethodDictionaryComplete(SmalltalkObject methodDict)
    {
        var methods = ExtractMethodsFromDictionary(methodDict);
        
        if (methods.Count == 0)
        {
            Console.WriteLine("  No methods found");
            return;
        }
        
        Console.WriteLine($"  Methods ({methods.Count}):");
        Console.WriteLine("  " + new string('-', 76));
        
        foreach (var (selector, method) in methods.OrderBy(m => m.selector))
        {
            PrintMethodComplete(selector, method);
        }
    }
    
    /// <summary>
    /// Print complete method details with bytecode analysis (verbosity level 3).
    /// </summary>
    private void PrintMethodComplete(string selector, SmalltalkObject method)
    {
        Console.WriteLine($"  {selector}");
        
        // CompiledMethod structure:
        // Field 0: selector (Symbol)
        // Field 1: bytecode (ByteArray)
        // Field 2: literals (Array)
        // Field 3: header (SmallInteger or object)
        
        // Get bytecode
        if (method.Fields.Length > 1 && method.Fields[1] is SmalltalkObject bytecodeObj)
        {
            var bytecode = ExtractBytecode(bytecodeObj);
            if (bytecode != null && bytecode.Length > 0)
            {
                // Get literals for disassembly
                object?[]? literals = null;
                if (method.Fields.Length > 2 && method.Fields[2] is SmalltalkObject literalsObj)
                {
                    literals = ExtractLiterals(literalsObj);
                }
                
                // Get header for argument/temp count
                int argCount = 0, tempCount = 0, primitive = 0;
                if (method.Fields.Length > 3 && method.Fields[3] != null)
                {
                    ExtractMethodHeader(method.Fields[3]!, out argCount, out tempCount, out primitive);
                }
                
                Console.WriteLine($"    Args: {argCount}, Temps: {tempCount}, Primitive: {primitive}");
                
                if (literals != null && literals.Length > 0)
                {
                    Console.WriteLine($"    Literals: [{string.Join(", ", literals.Select(FormatLiteral))}]");
                }
                
                if (showBytecode)
                {
                    if (showMnemonics)
                    {
                        PrintBytecodeWithMnemonics(bytecode, literals!, "    ");
                    }
                    else
                    {
                        // Output in hex format to match extracted JSON files
                        var hexBytes = bytecode.Select(b => b.ToString("X2"));
                        Console.WriteLine($"    Bytecode: {string.Join(" ", hexBytes)}");
                    }
                }
            }
        }
        
        Console.WriteLine();
    }

    #region Shared Core Methods (from both original printers)
    
    /// <summary>
    /// Find the Smalltalk SystemDictionary by looking for it in globals.
    /// </summary>
    private SmalltalkObject? FindSmalltalkDictionary()
    {
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
                Console.WriteLine($"[DEBUG] Found SystemDictionary via fallback at OOP {obj.Oop}");
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
        
        var systemDictClassOop = vm.PrimitiveLookupGlobal("SystemDictionary");
        if (systemDictClassOop > 0 && obj.ClassPointer.Oop == systemDictClassOop)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Find classes by examining the Smalltalk dictionary's contents.
    /// </summary>
    private List<(SmalltalkObject obj, string name)> FindClassesFromSmalltalkDict()
    {
        var classes = new List<(SmalltalkObject, string)>();
        
        if (smalltalkDict == null) return classes;
        
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
        
        if (dict.Fields.Length < 2) return false;
        
        // Dictionary structure: [tally, key1, value1, key2, value2, ...]
        for (int i = 1; i < dict.Fields.Length - 1; i += 2)
        {
            var keyObj = dict.Fields[i];
            var valueObj = dict.Fields[i + 1];
            
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
        
        return entries.Count > 0;
    }
    
    /// <summary>
    /// Check if an object is a Symbol.
    /// </summary>
    private bool IsSymbol(SmalltalkObject obj)
    {
        if (obj.ClassPointer == null) return false;
        var symbolClassOop = vm.PrimitiveLookupGlobal("Symbol");
        return symbolClassOop > 0 && obj.ClassPointer.Oop == symbolClassOop;
    }
    
    /// <summary>
    /// Extract the string value from a Symbol object.
    /// </summary>
    private string ExtractSymbolString(SmalltalkObject symbol)
    {
        if (symbol.Fields.Length > 0 && symbol.Fields[0] is string str)
        {
            return str;
        }
        return "";
    }
    
    /// <summary>
    /// Determine if an object is a class by examining its structure.
    /// </summary>
    private bool IsClassObject(SmalltalkObject obj)
    {
        if (obj == null || obj.Fields.Length < 3)
            return false;
            
        if (obj.ClassPointer != null)
        {
            var classOop = vm.PrimitiveLookupGlobal("Class");
            var metaclassOop = vm.PrimitiveLookupGlobal("Metaclass");
            
            if (obj.ClassPointer.ClassPointer != null)
            {
                if (obj.ClassPointer.ClassPointer.Oop == metaclassOop)
                    return true;
            }
            
            if (obj.ClassPointer.Oop == classOop)
                return true;
        }
        
        // Fallback: Check if slot 1 looks like a method dictionary
        if (obj.Fields.Length > 1)
        {
            var methodDictField = obj.Fields[1];
            if (methodDictField is SmalltalkObject methodDict)
            {
                return methodDict.Fields.Length >= 2;
            }
        }
        
        return false;
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
        var methods = ExtractMethodsFromDictionary(methodDict);
        return methods.Count;
    }
    
    /// <summary>
    /// Extract methods from a method dictionary (supports both formats).
    /// </summary>
    private List<(string selector, SmalltalkObject method)> ExtractMethodsFromDictionary(SmalltalkObject methodDict)
    {
        var methods = new List<(string, SmalltalkObject)>();
        
        // Check if this is a MethodDictionary format (2 slots with arrays) or flat format
        bool isMethodDictionary = false;
        
        if (methodDict.Fields.Length == 2 && 
            methodDict.Fields[0] is SmalltalkObject keysArray &&
            methodDict.Fields[1] is SmalltalkObject valuesArray)
        {
            // MethodDictionary format: [keysArray, valuesArray]
            isMethodDictionary = true;
            
            for (int i = 0; i < keysArray.Fields.Length && i < valuesArray.Fields.Length; i++)
            {
                var keyObj = keysArray.Fields[i];
                var valueObj = valuesArray.Fields[i];
                
                if (keyObj == null || valueObj == null)
                    continue;
                
                if (keyObj is SmalltalkObject keySymbol && valueObj is SmalltalkObject method)
                {
                    if (IsCompiledMethod(method))
                    {
                        var selector = ExtractSymbolString(keySymbol);
                        if (!string.IsNullOrEmpty(selector))
                        {
                            methods.Add((selector, method));
                        }
                    }
                }
            }
        }
        
        if (!isMethodDictionary)
        {
            // Flat format: [tally, key1, value1, key2, value2, ...]
            for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
            {
                var keyObj = methodDict.Fields[i];
                var valueObj = methodDict.Fields[i + 1];
                
                if (keyObj == null || valueObj == null)
                    continue;
                
                if (keyObj is SmalltalkObject keySymbol && valueObj is SmalltalkObject method)
                {
                    if (IsCompiledMethod(method))
                    {
                        var selector = ExtractSymbolString(keySymbol);
                        if (!string.IsNullOrEmpty(selector))
                        {
                            methods.Add((selector, method));
                        }
                    }
                }
            }
        }
        
        return methods;
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

    #endregion
    
    #region Bytecode Analysis Methods (from SmalltalkPrettyPrinterEnhanced)
    
    /// <summary>
    /// Extract bytecode from a ByteArray object.
    /// </summary>
    private byte[]? ExtractBytecode(SmalltalkObject bytecodeObj)
    {
        // ByteArray objects store their data in the Fields array
        if (bytecodeObj.Fields.Length == 0) return null;
        
        var bytes = new List<byte>();
        foreach (var field in bytecodeObj.Fields)
        {
            if (field is byte b)
            {
                bytes.Add(b);
            }
            else if (field is int i && i >= 0 && i <= 255)
            {
                bytes.Add((byte)i);
            }
        }
        
        return bytes.ToArray();
    }
    
    /// <summary>
    /// Extract literals from an Array object.
    /// </summary>
    private object?[]? ExtractLiterals(SmalltalkObject literalsObj)
    {
        return literalsObj.Fields;
    }
    
    /// <summary>
    /// Extract method header information.
    /// </summary>
    private void ExtractMethodHeader(object headerObj, out int argCount, out int tempCount, out int primitive)
    {
        argCount = 0;
        tempCount = 0;
        primitive = 0;
        
        // Header is typically a SmallInteger encoding various flags
        if (headerObj is int header)
        {
            // Blue Book method header format:
            // bits 0-3: argument count
            // bits 4-7: temporary count  
            // bits 8-15: primitive number (if any)
            argCount = header & 0xF;
            tempCount = (header >> 4) & 0xF;
            primitive = (header >> 8) & 0xFF;
        }
    }
    
    /// <summary>
    /// Format a literal object for display.
    /// </summary>
    private string FormatLiteral(object? literal)
    {
        if (literal == null) return "nil";
        
        if (literal is SmalltalkObject obj)
        {
            if (IsSymbol(obj))
            {
                return $"#{ExtractSymbolString(obj)}";
            }
            else
            {
                return $"<{obj.GetType().Name}@{obj.Oop}>";
            }
        }
        
        if (literal is string str)
            return $"'{str}'";
            
        if (literal is int i)
            return i.ToString();
            
        return literal.ToString() ?? "nil";
    }
    
    /// <summary>
    /// Print bytecode with instruction mnemonics using existing disassembler.
    /// </summary>
    private void PrintBytecodeWithMnemonics(byte[] bytecode, object?[]? literals, string indent)
    {
        Console.WriteLine($"{indent}Bytecode ({bytecode.Length} bytes):");
        
        // Use the existing BytecodeDisassembler from SmalltalkPrettyPrinterEnhanced
        var disassembler = new BytecodeDisassembler();
        var disassembly = disassembler.Disassemble(bytecode, literals!);
        
        foreach (var line in disassembly)
        {
            Console.WriteLine($"{indent}  {line}");
        }
    }
    
    #endregion
}

