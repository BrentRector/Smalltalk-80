using System.Text.Json;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 11: Prepare Init Expressions
/// Creates method objects for initialization expressions but does NOT add them to SystemDictionary.
/// Instead, returns an array of init expression methods that will be:
/// 1. Temporarily stored as a GC root during verification
/// 2. Executed as DoIts after GC verification passes
/// 3. Discarded after execution (allowing garbage collection)
/// </summary>
public class Phase11_InitExecution
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase11_InitExecution(ColdStartBundle bundle, SemanticAllocator allocator, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdMap;
    }

    /// <summary>
    /// Creates init expression method objects and returns the array ID for temporary GC root storage.
    /// Returns 0 if there are no init expressions.
    /// </summary>
    public int Run()
    {
        // Create array to hold all init expression method objects
        var initExprMethods = new List<int>();

        foreach (var expr in _bundle.InitExpressions)
        {
            var literalOOPs = expr.Literals.Select(ResolveLiteralAsOOP).ToArray();

            // Create a method object for the init expression to keep its literals alive
            // This will be executed after GC verification
            var bytecodes = ClassHierarchyUtils.DecodeBytecodes(expr.Bytecodes);
            int initMethodId = CreateCompiledMethod(
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
                bytecodes);

            initExprMethods.Add(initMethodId);
            _localIdToObjectId[expr.LocalId] = initMethodId;
        }

        // Return the array (or 0 if no init expressions)
        // This will be stored temporarily as a GC root, then executed and discarded
        if (initExprMethods.Count > 0)
        {
            return AllocateArray(initExprMethods.Select(id => SmalltalkOOP.FromObjectTableIndex(id)).ToArray());
        }

        return 0;
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
            "symbol" => SmalltalkOOP.FromObjectTableIndex(InternSymbol(_bundle.Symbols[literal.Index!.Value])),
            "string" => SmalltalkOOP.FromObjectTableIndex(InternString(_bundle.Symbols[literal.Index!.Value])),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => SmalltalkOOP.FromObjectTableIndex(AllocateFloat(GetFloatValue(literal.Value!))),
            "nil" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
            "true" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.TrueObjectId),
            "false" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.FalseObjectId),
            "objectRef" => SmalltalkOOP.FromObjectTableIndex(_localIdToObjectId[literal.LocalId!]),
            "compiledMethod" => SmalltalkOOP.FromObjectTableIndex(_localIdToObjectId[literal.LocalId!]),
            "character" => SmalltalkOOP.FromObjectTableIndex(AllocateCharacter(literal.CodePoint!.Value)),
            "bytes" => SmalltalkOOP.FromObjectTableIndex(AllocateByteArray(literal.Encoding!, literal.Data!)),
            "association" => SmalltalkOOP.FromObjectTableIndex(ResolveAssociation(literal.Association!)),
            "array" => SmalltalkOOP.FromObjectTableIndex(AllocateArrayLiteral(literal.Elements!.Select(ResolveLiteralAsOOP).ToArray())),
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
    private int CreateCompiledMethod(MethodDescriptor method, SmalltalkOOP[] literalOOPs, byte[] bytecodes)
    {
        // Look up CompiledMethod class layout
        var compiledMethodClassDefinition = FindClassDefinition("CompiledMethod");
        if (compiledMethodClassDefinition == null)
        {
            throw new InvalidOperationException("CompiledMethod class not found in bundle");
        }

        // Calculate OOP slots: header + literals + bytecode array reference  
        // (AllocateCompiledMethod will create the ByteArray internally)
        int oopSlots = 1 + literalOOPs.Length + 1; // header + literals + bytecodeArrayOOP
        int totalWords = CalculateTotalInstanceVariableCount(compiledMethodClassDefinition) + oopSlots;
        
        
        if (totalWords > 255)
        {
            throw new InvalidOperationException($"Init expression {method.LocalId} too large: {totalWords} words (literals: {literalOOPs.Length}, bytecodes: {bytecodes.Length})");
        }
        
        var methodOOP = _allocator.AllocateCompiledMethod("CompiledMethod", method.Header.Word, literalOOPs, bytecodes);

        // AllocateCompiledMethod already sets up the header, literals, and bytecode array reference
        return methodOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Allocates an Array object for literal arrays with debug output.
    /// </summary>
    private int AllocateArrayLiteral(SmalltalkOOP[] elementOOPs)
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
    private int AllocateArray(SmalltalkOOP[] elementOOPs)
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

        return arrayOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private int InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetObjectId(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    private int InternString(string value)
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
        
        return stringOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP.
    /// </summary>
    private int EncodeSmallInteger(int value)
    {
        var smallIntOOP = SmalltalkOOP.FromSmallInteger(value);
        return smallIntOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private int AllocateFloat(double value)
    {
        // Look up Float class layout
        var floatClassDefinition = FindClassDefinition("Float");
        if (floatClassDefinition == null)
        {
            throw new InvalidOperationException("Float class not found in bundle");
        }

        var floatOOP = _allocator.AllocateFloat(value);

        return floatOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private int AllocateCharacter(int codePoint)
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

        return characterOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private int AllocateByteArray(string encoding, string data)
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

        return byteArrayOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private int AllocateAssociation(int keyId, int valueId)
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
        _allocator.SetInstanceVariable(associationOOP, 0, SmalltalkOOP.FromObjectTableIndex(keyId));
        _allocator.SetInstanceVariable(associationOOP, 1, SmalltalkOOP.FromObjectTableIndex(valueId));
        
        return associationOOP.AsObjectTableIndex();
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
    private int ResolveAssociation(AssociationLiteral assoc)
    {
        int keyId = InternSymbol(_bundle.Symbols[assoc.KeyIndex]);
        string keyName = _bundle.Symbols[assoc.KeyIndex];

        // CRITICAL FIX: If this is a class association and valueRef is None,
        // look up the existing association from SystemDictionary instead of creating new one with nil
        if (assoc.ValueRef?.LocalId == null)
        {
            // Try to find existing class association in SystemDictionary
            if (GlobalRegistry.TryGet($"class:{keyName}", out int classOop))
            {
                // This is a known class - find its existing association in SystemDictionary
                int sysDictId = GlobalRegistry.SystemDictionaryId;
                var sysDictOOP = SmalltalkOOP.FromObjectTableIndex(sysDictId);
                int sysDictSize = _allocator.GetIndexedSize(sysDictOOP);

                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP.AsObjectTableIndex() == GlobalRegistry.NilObjectId) continue;

                    var assocKeyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP.AsObjectTableIndex() == keyId)
                    {
                        // Found the existing association - reuse it!
                        return assocOOP.AsObjectTableIndex();
                    }
                }
            }
        }

        // Fall back to creating new association
        int valueId = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilObjectId;

        return AllocateAssociation(keyId, valueId);
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
