using System.Text.Json;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 16: Prepare Init Expressions
/// Creates method objects for initialization expressions but does NOT add them to SystemDictionary.
/// Instead, returns an array of init expression methods that will be:
/// 1. Temporarily stored as a GC root during verification
/// 2. Executed as DoIts after GC verification passes
/// 3. Discarded after execution (allowing garbage collection)
/// </summary>
public class Phase16_InitExecution
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;

    public Phase16_InitExecution(ColdStartBundle bundle, SemanticAllocator allocator, Dictionary<string, SmalltalkOOP> localIdMap)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdMap;
    }

    /// <summary>
    /// Creates init expression method objects and returns the array OOP for temporary GC root storage.
    /// Returns NIL OOP if there are no init expressions.
    /// </summary>
    public SmalltalkOOP Run()
    {
        // Look up CompiledMethod and ByteArray classes - they were created in Phase2
        if (!_localIdToObjectId.TryGetValue("class:CompiledMethod", out var compiledMethodClassOOP))
        {
            throw new InvalidOperationException("CompiledMethod class not found in _localIdToObjectId - Phase 2 must run first");
        }
        
        if (!_localIdToObjectId.TryGetValue("class:ByteArray", out var byteArrayClassOOP))
        {
            throw new InvalidOperationException("ByteArray class not found in _localIdToObjectId - Phase 2 must run first");
        }
        
        // Create array to hold all init expression method objects
        var initExprMethods = new List<SmalltalkOOP>();

        foreach (var expr in _bundle.InitExpressions)
        {
            var literalOOPs = expr.Literals.Select(ResolveLiteralAsOOP).ToArray();

            // Create a method object for the init expression to keep its literals alive
            // This will be executed after GC verification
            var bytecodes = ClassHierarchyUtils.DecodeBytecodes(expr.Bytecodes);
            SmalltalkOOP initMethodOOP = CreateCompiledMethod(
                new MethodDescriptor
                {
                    LocalId = expr.LocalId,
                    HomeClassLocalId = "class:UndefinedObject", // placeholder
                    SelectorIndex = 0,
                    Header = expr.Header, // Use the actual header from init expression
                    Literals = Array.Empty<LiteralValue>(), // already resolved
                    Bytecodes = expr.Bytecodes
                },
                literalOOPs,
                bytecodes,
                compiledMethodClassOOP,
                byteArrayClassOOP);

            initExprMethods.Add(initMethodOOP);
            _localIdToObjectId[expr.LocalId] = initMethodOOP;
        }

        // Return the array (or NIL if no init expressions)
        // This will be stored temporarily as a GC root, then executed and discarded
        if (initExprMethods.Count > 0)
        {
            return AllocateArray(initExprMethods.ToArray());
        }

        return GlobalRegistry.NilOOP;
    }

    // DecodeBytecodes method moved to ClassHierarchyUtils to eliminate duplication

    /// <summary>
    /// Resolves an init expression literal to an object ID.
    /// Init expressions use the same LiteralValue type as methods.
    /// v1.0.3: Supports "compiledMethod" for nested blocks.
    /// </summary>
    private SmalltalkOOP ResolveLiteralAsOOP(LiteralValue literal)
    {
        return literal.Type switch
        {
            "symbol" => InternSymbol(_bundle.Symbols[literal.Index!.Value]),
            "string" => InternString(_bundle.Strings[literal.Index!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => GlobalRegistry.NilOOP,
            "true" => GlobalRegistry.TrueOOP,
            "false" => GlobalRegistry.FalseOOP,
            "objectRef" => _localIdToObjectId[literal.LocalId!],
            "compiledMethod" => _localIdToObjectId[literal.LocalId!],
            "character" => AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveAssociation(literal.Association!),
            "array" => AllocateArrayLiteral(literal.Elements!.Select(ResolveLiteralAsOOP).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    private int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    private double GetFloatValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Creates a CompiledMethod object using the new PointerIndexed design.
    /// Layout: [header] [literal1] [literal2] ... [literalN] [bytecodeArrayOOP]
    /// </summary>
    private SmalltalkOOP CreateCompiledMethod(
        MethodDescriptor method, 
        SmalltalkOOP[] literalOOPs, 
        byte[] bytecodes,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // Calculate OOP slots: header + literals + bytecode array reference  
        // (AllocateCompiledMethod will create the ByteArray internally)
        int oopSlots = 1 + literalOOPs.Length + 1; // header + literals + bytecodeArrayOOP
        int totalWords = 0 + oopSlots; // CompiledMethod has no instance variables
        
        if (totalWords > 255)
        {
            throw new InvalidOperationException($"Init expression {method.LocalId} too large: {totalWords} words (literals: {literalOOPs.Length}, bytecodes: {bytecodes.Length})");
        }
        
        var methodOOP = _allocator.AllocateCompiledMethod(
            compiledMethodClassOOP,
            byteArrayClassOOP,
            method.Header.Word, 
            literalOOPs, 
            bytecodes);

        return methodOOP;
    }

    /// <summary>
    /// Allocates an Array object for literal arrays with debug output.
    /// </summary>
    private SmalltalkOOP AllocateArrayLiteral(SmalltalkOOP[] elementOOPs)
    {
        if (elementOOPs.Length > 200)
        {
            Console.WriteLine($"WARNING: Large array literal with {elementOOPs.Length} elements!");
        }
        return AllocateArray(elementOOPs);
    }

    /// <summary>
    /// Allocates an Array object.
    /// </summary>
    private SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs)
    {
        // Look up Array class layout
        var arrayClassDefinition = FindClassDefinition("Array");
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        int arrayInstanceVars = CalculateTotalInstanceVariableCount(arrayClassDefinition);
        int totalWords = arrayInstanceVars + elementOOPs.Length;

        var arrayOOP = _allocator.AllocateArray("Array", elementOOPs.Length);

        // Store the elements
        for (int i = 0; i < elementOOPs.Length; i++)
        {
            _allocator.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
        }

        return arrayOOP;
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private SmalltalkOOP InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        
        // Look up String class layout from bundle data
        var stringClassDefinition = FindClassDefinition("String");
        if (stringClassDefinition == null)
        {
            throw new InvalidOperationException("String class not found in bundle");
        }
        
        // Use placeholder class (will be fixed later when String class exists)
        
        // Allocate string object (AllocateString handles the bytes internally)
        var stringOOP = _allocator.AllocateString(value);
        
        return stringOOP;
    }

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP.
    /// </summary>
    private SmalltalkOOP EncodeSmallInteger(int value)
    {
        var smallIntOOP = SmalltalkOOP.FromSmallInteger(value);
        return smallIntOOP;
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value)
    {
        // Look up Float class layout
        var floatClassDefinition = FindClassDefinition("Float");
        if (floatClassDefinition == null)
        {
            throw new InvalidOperationException("Float class not found in bundle");
        }

        var floatOOP = _allocator.AllocateFloat(value);

        return floatOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private SmalltalkOOP AllocateCharacter(int codePoint)
    {
        // Look up Character class layout
        var characterClassDefinition = FindClassDefinition("Character");
        if (characterClassDefinition == null)
        {
            throw new InvalidOperationException("Character class not found in bundle");
        }

        var characterOOP = _allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Look up ByteArray class layout
        var byteArrayClassDefinition = FindClassDefinition("ByteArray");
        if (byteArrayClassDefinition == null)
        {
            throw new InvalidOperationException("ByteArray class not found in bundle");
        }

        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes);

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        // Look up Association class layout from bundle data
        var associationClassDefinition = FindClassDefinition("Association");
        if (associationClassDefinition == null)
        {
            throw new InvalidOperationException("Association class not found in bundle");
        }
        
        var placeholderClassOOP = SmalltalkOOP.FromObjectTableIndex(0); // Will be fixed later
        var associationOOP = _allocator.AllocateFixedObject("Association");
        
        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        
        return associationOOP;
    }

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// </summary>
    private ClassDescriptor? FindClassDefinition(string className)
    {
        return _bundle.Classes.FirstOrDefault(c => 
            !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    private byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove any whitespace or separators
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");
        
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even number of characters");
            
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Resolves an association literal (key/value pair).
    /// CRITICAL: Reuse existing SystemDictionary associations instead of creating duplicates with nil value.
    /// </summary>
    private SmalltalkOOP ResolveAssociation(AssociationLiteral assoc)
    {
        SmalltalkOOP keyOOP = InternSymbol(_bundle.Symbols[assoc.KeyIndex]);
        string keyName = _bundle.Symbols[assoc.KeyIndex];

        // CRITICAL FIX: If this is a class association and valueRef is None,
        // look up the existing association from SystemDictionary instead of creating new one with nil
        if (assoc.ValueRef?.LocalId == null)
        {
            // Try to find existing class association in SystemDictionary
            if (GlobalRegistry.TryGet($"class:{keyName}", out SmalltalkOOP classOop))
            {
                // This is a known class - find its existing association in SystemDictionary
                var sysDictId = GlobalRegistry.SystemDictionaryOOP;
                var sysDictOOP = sysDictId;
                int sysDictSize = _allocator.GetIndexedSize(sysDictOOP);

                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP == GlobalRegistry.NilOOP) continue;

                    var assocKeyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP == keyOOP)
                    {
                        // Found the existing association - reuse it!
                        return assocOOP;
                    }
                }
            }
        }

        // Fall back to creating new association
        SmalltalkOOP valueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilOOP;

        return AllocateAssociation(keyOOP, valueOOP);
    }

    /// <summary>
    /// Calculates the total instance variable count for a class including inherited ones.
    /// </summary>
    private int CalculateTotalInstanceVariableCount(ClassDescriptor classDefinition)
    {
        int totalCount = 0;
        var currentClass = classDefinition;
        
        // Traverse superclass chain to count all instance variables
        while (currentClass != null)
        {
            totalCount += currentClass.InstVarNames.Length;
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = _bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = _bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        return totalCount;
    }
}
