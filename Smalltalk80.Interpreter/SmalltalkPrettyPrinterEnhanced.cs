using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Enhanced pretty printer with bytecode mnemonics and class filtering.
/// </summary>
public class SmalltalkPrettyPrinterEnhanced
{
    private readonly VMPrimitiveAdapter vm;
    private SmalltalkObject? smalltalkDict;
    private readonly HashSet<string> classFilter = new();
    private readonly bool showBytecode;
    private readonly bool showMnemonics;
    
    public SmalltalkPrettyPrinterEnhanced(VMPrimitiveAdapter vm, bool showBytecode = true, bool showMnemonics = true)
    {
        this.vm = vm;
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
        Console.WriteLine("=== Smalltalk System State ===");
        Console.WriteLine();
        
        // Find the Smalltalk SystemDictionary
        smalltalkDict = FindSmalltalkDictionary();
        if (smalltalkDict == null)
        {
            Console.WriteLine("Warning: Smalltalk SystemDictionary not found!");
            return;
        }
        
        // Find class objects from the Smalltalk dictionary
        var classes = FindClassesFromSmalltalkDict();
        
        // Filter classes if filter is set
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
        
        // Print each class with detailed method info
        foreach (var (classObj, name) in classes.OrderBy(c => c.name))
        {
            PrintClassDetailed(classObj, name);
        }
    }
    
    /// <summary>
    /// Find the Smalltalk SystemDictionary.
    /// </summary>
    private SmalltalkObject? FindSmalltalkDictionary()
    {
        var smalltalkOop = vm.PrimitiveLookupGlobal("Smalltalk");
        if (smalltalkOop > 0)
        {
            return ObjectMemory.FromOop(smalltalkOop);
        }
        return null;
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
    /// Determine if an object is a class.
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
    /// Print detailed information about a class including methods with bytecode.
    /// </summary>
    private void PrintClassDetailed(SmalltalkObject classObj, string className)
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
        
        // Get and print method dictionary (slot 1)
        if (classObj.Fields.Length > 1 && classObj.Fields[1] is SmalltalkObject methodDict)
        {
            PrintMethodDictionary(methodDict);
        }
        else
        {
            Console.WriteLine("  No method dictionary found");
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Print the method dictionary with bytecode details.
    /// </summary>
    private void PrintMethodDictionary(SmalltalkObject methodDict)
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
            PrintMethodDetails(selector, method);
        }
    }
    
    /// <summary>
    /// Extract methods from a method dictionary.
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
            
            // Extract from parallel arrays
            for (int i = 0; i < keysArray.Fields.Length && i < valuesArray.Fields.Length; i++)
            {
                var keyObj = keysArray.Fields[i];
                var valueObj = valuesArray.Fields[i];
                
                // Skip nil entries
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
            // Start at index 1 to skip tally
            for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
            {
                var keyObj = methodDict.Fields[i];
                var valueObj = methodDict.Fields[i + 1];
                
                // Skip nil entries
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
    /// Print detailed information about a method.
    /// </summary>
    private void PrintMethodDetails(string selector, SmalltalkObject method)
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
                object[]? literals = null;
                if (method.Fields.Length > 2 && method.Fields[2] is SmalltalkObject literalsObj)
                {
                    literals = ExtractLiterals(literalsObj);
                }
                
                // Get header for argument/temp count
                int argCount = 0, tempCount = 0, primitive = 0;
                if (method.Fields.Length > 3 && method.Fields[3] != null)
                {
                    ExtractMethodHeader(method.Fields[3], out argCount, out tempCount, out primitive);
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
                        PrintBytecodeWithMnemonics(bytecode, literals, "    ");
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
    
    /// <summary>
    /// Extract bytecode from a ByteArray object.
    /// </summary>
    private byte[]? ExtractBytecode(SmalltalkObject bytecodeObj)
    {
        var bytes = new List<byte>();
        
        foreach (var field in bytecodeObj.Fields)
        {
            if (field is int intVal && intVal >= 0 && intVal <= 255)
            {
                bytes.Add((byte)intVal);
            }
            else if (field is byte b)
            {
                bytes.Add(b);
            }
        }
        
        return bytes.ToArray();
    }
    
    /// <summary>
    /// Extract literals from an Array object.
    /// </summary>
    private object[]? ExtractLiterals(SmalltalkObject literalsObj)
    {
        var literals = new List<object>();
        
        foreach (var field in literalsObj.Fields)
        {
            if (field is SmalltalkObject obj)
            {
                if (IsSymbol(obj))
                {
                    literals.Add($"#{ExtractSymbolString(obj)}");
                }
                else
                {
                    literals.Add($"<obj@{obj.Oop}>");
                }
            }
            else if (field is string str)
            {
                literals.Add($"'{str}'");
            }
            else if (field != null)
            {
                literals.Add(field);
            }
        }
        
        return literals.ToArray();
    }
    
    /// <summary>
    /// Extract method header information.
    /// </summary>
    private void ExtractMethodHeader(object? headerField, out int argCount, out int tempCount, out int primitive)
    {
        argCount = 0;
        tempCount = 0;
        primitive = 0;
        
        if (headerField is int headerOop)
        {
            // The header is stored as a SmallInteger OOP (immediate value)
            // SmallIntegers are encoded as (value << 1) | 1
            // We need to decode the actual integer value first
            int header;
            if ((headerOop & 1) == 1)
            {
                // It's a SmallInteger OOP, decode it
                header = headerOop >> 1;
            }
            else
            {
                // It's already a raw integer (shouldn't happen in normal operation)
                header = headerOop;
            }
            
            // Blue Book method header format (16-bit word):
            // Bits 0-8: Primitive number (0-511)
            // Bit 9: Large context flag
            // Bits 10-15: Number of temporaries (includes arguments)
            
            primitive = header & 0x1FF;  // Lower 9 bits
            bool largeContext = (header & 0x200) != 0;  // Bit 9
            tempCount = (header >> 10) & 0x3F;  // Bits 10-15
            
            // Note: argCount is not stored separately in Blue Book format
            // It's determined from the method's selector or bytecode analysis
            // For now, we'll leave it as 0 or derive it from context
            argCount = tempCount;  // This is a simplification
        }
    }
    
    /// <summary>
    /// Format a literal for display.
    /// </summary>
    private string FormatLiteral(object literal)
    {
        return literal?.ToString() ?? "nil";
    }
    
    /// <summary>
    /// Print bytecode with mnemonics.
    /// </summary>
    private void PrintBytecodeWithMnemonics(byte[] bytecode, object[]? literals, string indent)
    {
        Console.WriteLine($"{indent}Bytecode ({bytecode.Length} bytes):");
        
        var disassembler = new BytecodeDisassembler();
        var instructions = disassembler.Disassemble(bytecode, literals);
        
        foreach (var instruction in instructions)
        {
            Console.WriteLine($"{indent}  {instruction}");
        }
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
}

/// <summary>
/// Bytecode disassembler for Smalltalk-80 bytecodes.
/// </summary>
public class BytecodeDisassembler
{
    public List<string> Disassemble(byte[] bytecode, object[]? literals)
    {
        var instructions = new List<string>();
        int pc = 0;
        
        while (pc < bytecode.Length)
        {
            var offset = pc;
            var (mnemonic, size) = DisassembleInstruction(bytecode, pc, literals);
            instructions.Add($"{offset:D4}: {mnemonic}");
            pc += size;
        }
        
        return instructions;
    }
    
    private (string mnemonic, int size) DisassembleInstruction(byte[] bytecode, int pc, object[]? literals)
    {
        if (pc >= bytecode.Length)
            return ("", 1);
            
        byte b = bytecode[pc];
        
        // Push receiver variable (0-15)
        if (b >= 0 && b <= 15)
        {
            return ($"pushRcvrVariable: {b}", 1);
        }
        
        // Push temporary variable (16-31)
        if (b >= 16 && b <= 31)
        {
            return ($"pushTemp: {b - 16}", 1);
        }
        
        // Push literal constant (32-63)
        if (b >= 32 && b <= 63)
        {
            var literalIndex = b - 32;
            var literal = literals != null && literalIndex < literals.Length 
                ? literals[literalIndex]?.ToString() ?? "nil" 
                : $"#{literalIndex}";
            return ($"pushConstant: {literal}", 1);
        }
        
        // Push literal variable (64-95)
        if (b >= 64 && b <= 95)
        {
            var literalIndex = b - 64;
            var literal = literals != null && literalIndex < literals.Length 
                ? literals[literalIndex]?.ToString() ?? "nil" 
                : $"#{literalIndex}";
            return ($"pushLiteralVariable: {literal}", 1);
        }
        
        // Pop and store receiver variable (96-103)
        if (b >= 96 && b <= 103)
        {
            return ($"popIntoRcvrVariable: {b - 96}", 1);
        }
        
        // Pop and store temporary (104-111)
        if (b >= 104 && b <= 111)
        {
            return ($"popIntoTemp: {b - 104}", 1);
        }
        
        // Push special objects
        switch (b)
        {
            case 112: return ("pushReceiver", 1);
            case 113: return ("pushConstant: true", 1);
            case 114: return ("pushConstant: false", 1);
            case 115: return ("pushConstant: nil", 1);
            case 116: return ("pushConstant: -1", 1);
            case 117: return ("pushConstant: 0", 1);
            case 118: return ("pushConstant: 1", 1);
            case 119: return ("pushConstant: 2", 1);
            case 120: return ("returnReceiver", 1);
            case 121: return ("returnTrue", 1);
            case 122: return ("returnFalse", 1);
            case 123: return ("returnNil", 1);
            case 124: return ("returnTop", 1);
            case 125: return ("returnTopFromBlock", 1);
            case 126: return ("unknown126", 1);
            case 127: return ("unknown127", 1);
        }
        
        // Extended push/store/pop (128-130)
        if (b == 128)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int type = (ext >> 6) & 3;
                int index = ext & 63;
                
                switch (type)
                {
                    case 0: return ($"extPushRcvrVariable: {index}", 2);
                    case 1: return ($"extPushTemp: {index}", 2);
                    case 2: return ($"extPushLiteralConstant: {index}", 2);
                    case 3: return ($"extPushLiteralVariable: {index}", 2);
                }
            }
            return ("extendedPush <invalid>", 2);
        }
        
        if (b == 129)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int type = (ext >> 6) & 3;
                int index = ext & 63;
                
                switch (type)
                {
                    case 0: return ($"extStoreRcvrVariable: {index}", 2);
                    case 1: return ($"extStoreTemp: {index}", 2);
                    case 2: return ($"illegal", 2);
                    case 3: return ($"extStoreLiteralVariable: {index}", 2);
                }
            }
            return ("extendedStore <invalid>", 2);
        }
        
        if (b == 130)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int type = (ext >> 6) & 3;
                int index = ext & 63;
                
                switch (type)
                {
                    case 0: return ($"extPopIntoRcvrVariable: {index}", 2);
                    case 1: return ($"extPopIntoTemp: {index}", 2);
                    case 2: return ($"illegal", 2);
                    case 3: return ($"extPopIntoLiteralVariable: {index}", 2);
                }
            }
            return ("extendedPopStore <invalid>", 2);
        }
        
        // Single extended send (131)
        if (b == 131)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int literalIndex = ext & 31;
                int argCount = (ext >> 5) & 7;
                var literal = literals != null && literalIndex < literals.Length 
                    ? literals[literalIndex]?.ToString() ?? "?" 
                    : $"#{literalIndex}";
                return ($"send: {literal} args: {argCount}", 2);
            }
            return ("singleExtendedSend <invalid>", 2);
        }
        
        // Double extended do-anything (132)
        if (b == 132)
        {
            if (pc + 2 < bytecode.Length)
            {
                return ($"doubleExtended: {bytecode[pc + 1]:X2} {bytecode[pc + 2]:X2}", 3);
            }
            return ("doubleExtended <invalid>", 3);
        }
        
        // Single extended super send (133)
        if (b == 133)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int literalIndex = ext & 31;
                int argCount = (ext >> 5) & 7;
                var literal = literals != null && literalIndex < literals.Length 
                    ? literals[literalIndex]?.ToString() ?? "?" 
                    : $"#{literalIndex}";
                return ($"superSend: {literal} args: {argCount}", 2);
            }
            return ("singleExtendedSuperSend <invalid>", 2);
        }
        
        // Second extended send (134)
        if (b == 134)
        {
            if (pc + 1 < bytecode.Length)
            {
                byte ext = bytecode[pc + 1];
                int literalIndex = ext & 63;
                int argCount = (ext >> 6) & 3;
                var literal = literals != null && literalIndex < literals.Length 
                    ? literals[literalIndex]?.ToString() ?? "?" 
                    : $"#{literalIndex}";
                return ($"send: {literal} args: {argCount}", 2);
            }
            return ("secondExtendedSend <invalid>", 2);
        }
        
        // Pop stack (135)
        if (b == 135)
        {
            return ("pop", 1);
        }
        
        // Duplicate top (136)
        if (b == 136)
        {
            return ("duplicate", 1);
        }
        
        // Push active context (137)
        if (b == 137)
        {
            return ("pushActiveContext", 1);
        }
        
        // Unused (138-143)
        if (b >= 138 && b <= 143)
        {
            return ($"unused{b}", 1);
        }
        
        // Jump (144-151)
        if (b >= 144 && b <= 151)
        {
            int offset = (b & 7) + 1;
            return ($"jump: +{offset}", 1);
        }
        
        // Conditional jump on false (152-159)
        if (b >= 152 && b <= 159)
        {
            int offset = (b & 7) + 1;
            return ($"jumpFalse: +{offset}", 1);
        }
        
        // Long unconditional jump (160-167)
        if (b >= 160 && b <= 167)
        {
            if (pc + 1 < bytecode.Length)
            {
                int offset = ((b & 7) << 8) + bytecode[pc + 1];
                return ($"longJump: +{offset}", 2);
            }
            return ("longJump <invalid>", 2);
        }
        
        // Long conditional jump on true (168-171)
        if (b >= 168 && b <= 171)
        {
            if (pc + 1 < bytecode.Length)
            {
                int offset = ((b & 3) << 8) + bytecode[pc + 1];
                return ($"longJumpTrue: +{offset}", 2);
            }
            return ("longJumpTrue <invalid>", 2);
        }
        
        // Long conditional jump on false (172-175)
        if (b >= 172 && b <= 175)
        {
            if (pc + 1 < bytecode.Length)
            {
                int offset = ((b & 3) << 8) + bytecode[pc + 1];
                return ($"longJumpFalse: +{offset}", 2);
            }
            return ("longJumpFalse <invalid>", 2);
        }
        
        // Send arithmetic messages (176-191)
        if (b >= 176 && b <= 191)
        {
            string[] arithmeticSelectors = {
                "+", "-", "<", ">", "<=", ">=", "=", "~=",
                "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
            };
            int selectorIndex = b - 176;
            var selector = selectorIndex < arithmeticSelectors.Length 
                ? arithmeticSelectors[selectorIndex] 
                : $"arithmetic{selectorIndex}";
            return ($"sendArithmetic: {selector}", 1);
        }
        
        // Send special messages (192-207)
        if (b >= 192 && b <= 207)
        {
            string[] specialSelectors = {
                "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "==", "class",
                "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y"
            };
            int selectorIndex = b - 192;
            var selector = selectorIndex < specialSelectors.Length 
                ? specialSelectors[selectorIndex] 
                : $"special{selectorIndex}";
            return ($"sendSpecial: {selector}", 1);
        }
        
        // Send literal selector with 0 args (208-223)
        if (b >= 208 && b <= 223)
        {
            int literalIndex = b - 208;
            var literal = literals != null && literalIndex < literals.Length 
                ? literals[literalIndex]?.ToString() ?? "?" 
                : $"#{literalIndex}";
            return ($"send: {literal} args: 0", 1);
        }
        
        // Send literal selector with 1 arg (224-239)
        if (b >= 224 && b <= 239)
        {
            int literalIndex = b - 224;
            var literal = literals != null && literalIndex < literals.Length 
                ? literals[literalIndex]?.ToString() ?? "?" 
                : $"#{literalIndex}";
            return ($"send: {literal} args: 1", 1);
        }
        
        // Send literal selector with 2 args (240-255)
        if (b >= 240 && b <= 255)
        {
            int literalIndex = b - 240;
            var literal = literals != null && literalIndex < literals.Length 
                ? literals[literalIndex]?.ToString() ?? "?" 
                : $"#{literalIndex}";
            return ($"send: {literal} args: 2", 1);
        }
        
        return ($"unknown: {b:X2}", 1);
    }
}