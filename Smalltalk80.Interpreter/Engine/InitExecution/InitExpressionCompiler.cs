using System.Text.Json;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Compiles init expressions on-demand from ColdStartBundle.
/// This allows init expressions to be compiled only when needed, ensuring they're
/// reachable via Processor.activeProcess during execution (avoiding GC collection).
/// RUNTIME SAFE: Uses only runtime-available infrastructure, not cold-start classes.
/// </summary>
public class InitExpressionCompiler
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly ColdStartBundle _bundle;
    private readonly VMMetadata _metadata;

    public InitExpressionCompiler(
        ISmalltalkHeap heapManager,
        ColdStartBundle bundle,
        VMMetadata metadata)
    {
        _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <summary>
    /// Compiles a single init expression from the bundle on-demand.
    /// Returns the CompiledMethod OOP.
    /// </summary>
    public SmalltalkOOP CompileInitExpression(InitExpression expr)
    {
        // Resolve literals at runtime from the heap
        var literalOOPs = expr.Literals.Select(ResolveLiteralValueAtRuntime).ToArray();

        // Determine homeClass from the resolved literals
        string homeClassLocalId = ExtractHomeClassFromInitExpression(expr, literalOOPs);

        // Get required class OOPs (runtime-safe lookups)
        var compiledMethodClassOOP = LookupGlobal("CompiledMethod");
        var byteArrayClassOOP = LookupGlobal("ByteArray");

        if (compiledMethodClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException("CompiledMethod class not found");
        if (byteArrayClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException("ByteArray class not found");

        // Get homeClass OOP (runtime-safe lookup)
        var homeClassOOP = LookupGlobal(homeClassLocalId.Replace("meta:", ""));
        if (homeClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException($"Home class not found: {homeClassLocalId}");

        // If it's a metaclass (meta:ClassName), get the metaclass
        SmalltalkOOP actualHomeClassOOP;
        if (homeClassLocalId.StartsWith("meta:"))
        {
            // Get the metaclass (class of the class)
            actualHomeClassOOP = _heapManager.GetClassOOP(homeClassOOP);
        }
        else
        {
            actualHomeClassOOP = homeClassOOP;
        }

        // Decode bytecodes
        var bytecodes = ClassHierarchyTraversal.DecodeBytecodes(expr.Bytecodes);

        // Create the CompiledMethod
        var methodOOP = CreateCompiledMethod(
            expr,
            literalOOPs,
            bytecodes,
            actualHomeClassOOP,
            compiledMethodClassOOP,
            byteArrayClassOOP);

        return methodOOP;
    }

    /// <summary>
    /// Extracts the home class local ID from an init expression.
    /// </summary>
    private string ExtractHomeClassFromInitExpression(InitExpression expr, SmalltalkOOP[] literalOOPs)
    {
        // Primary: Parse from source if available
        if (!string.IsNullOrEmpty(expr.Source))
        {
            string className = ParseClassNameFromInitExpression(expr.Source);
            string homeClassLocalId = "meta:" + className;
            return homeClassLocalId;
        }

        // Fallback: Extract from first literal (should be an Association)
        if (literalOOPs.Length > 0)
        {
            var firstLiteral = literalOOPs[0];
            if (!firstLiteral.IsSmallInteger && firstLiteral != _heapManager.GetNil())
            {
                // Try to get the Association's value (the class)
                var classOOP = _heapManager.GetInstanceVariable(firstLiteral, VMConstants.ASSOCIATION_VALUE_INDEX);
                var className = _heapManager.GetClassName(classOOP);
                if (className != null)
                {
                    return "meta:" + className;
                }
            }
        }

        throw new InvalidOperationException($"Cannot determine home class for init expression: {expr.LocalId}");
    }

    /// <summary>
    /// Parses the class name from an init expression source string.
    /// Examples:
    /// "Float class initialize" -> "Float"
    /// "Object initialize" -> "Object"
    /// </summary>
    private string ParseClassNameFromInitExpression(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException("Init expression source is null or empty");
        }

        // Split on whitespace
        var parts = source.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Invalid init expression format: {source}");
        }

        // First part is the class name (or "ClassName class")
        string className = parts[0];

        // Handle "ClassName class initialize" format
        if (parts.Length >= 3 && parts[1].Equals("class", StringComparison.OrdinalIgnoreCase))
        {
            // It's a class-side method, but we still return the base class name
            return className;
        }

        // Handle "ClassName initialize" format
        return className;
    }

    /// <summary>
    /// Creates a CompiledMethod object from the method descriptor and resolved literals.
    /// </summary>
    private SmalltalkOOP CreateCompiledMethod(
        InitExpression expr,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes,
        SmalltalkOOP homeClassOOP,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // Decode header to get field counts
        int headerWord = expr.Header.Word;  // Get the actual 16-bit header value
        int literalCount = expr.Header.LiteralCount;

        // Create ByteArray for bytecodes
        // For ByteIndexed format, indexedSize represents the number of BYTES
        var bytecodeArrayOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: bytecodes.Length);
        _heapManager.SetClassOOP(bytecodeArrayOOP, byteArrayClassOOP);

        for (int i = 0; i < bytecodes.Length; i++)
        {
            _heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // Create CompiledMethod object
        // Format: PointerIndexed with header + literals + bytecode array OOP
        int methodLiteralCount = literalCount + 2; // +1 for header, +1 for bytecode array OOP
        var methodOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed,
            fixedSlots: 0,
            indexedSize: methodLiteralCount);

        _heapManager.SetClassOOP(methodOOP, compiledMethodClassOOP);

        // Set header (first indexed slot)
        _heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(headerWord));

        // Set literals (remaining indexed slots)
        for (int i = 0; i < literalOOPs.Length && i < literalCount; i++)
        {
            _heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
        }

        // Pad remaining literal slots with nil if needed
        for (int i = literalOOPs.Length; i < literalCount; i++)
        {
            _heapManager.SetIndexedOOP(methodOOP, i + 1, _heapManager.GetNil());
        }

        // Store bytecode array OOP as last indexed slot
        // Blue Book: CompiledMethod format is [header, literals..., bytecodeArrayOOP]
        _heapManager.SetIndexedOOP(methodOOP, methodLiteralCount - 1, bytecodeArrayOOP);

        return methodOOP;
    }

    /// <summary>
    /// Resolves a literal value from the bundle to a heap OOP at runtime.
    /// RUNTIME SAFE: Only uses runtime-available infrastructure.
    /// </summary>
    private SmalltalkOOP ResolveLiteralValueAtRuntime(LiteralValue literalValue)
    {
        switch (literalValue.Type)
        {
            case "smallInt":
                // Value is an int for SmallInteger
                if (literalValue.Value is int intValue)
                    return SmalltalkOOP.FromSmallInteger(intValue);
                if (literalValue.Value is long longValue)
                    return SmalltalkOOP.FromSmallInteger((int)longValue);
                if (literalValue.Value is JsonElement jsonInt && jsonInt.ValueKind == JsonValueKind.Number)
                    return SmalltalkOOP.FromSmallInteger(jsonInt.GetInt32());
                throw new InvalidOperationException($"Invalid SmallInteger value: {literalValue.Value}");

            case "float":
                // Float value - create a Float object
                double floatValue = GetFloatValue(literalValue.Value!);
                return AllocateFloat(floatValue);

            case "symbol":
                // Symbols use Index to look up in bundle's symbol table
                if (literalValue.Index.HasValue)
                {
                    string symbolText = _bundle.Symbols[literalValue.Index.Value];
                    return FindSymbol(symbolText);
                }
                throw new InvalidOperationException("Symbol literal missing Index");

            case "string":
                // String literals - allocate a new String object
                if (literalValue.Index.HasValue)
                {
                    string stringText = _bundle.Strings[literalValue.Index.Value];
                    return AllocateString(stringText);
                }
                throw new InvalidOperationException("String literal missing Index");

            case "association":
                // Association has a key (symbol) and value
                if (literalValue.Association != null)
                {
                    string keySymbolText = _bundle.Symbols[literalValue.Association.KeyIndex];
                    var smalltalkOOP = _metadata.KnownObjects.Smalltalk;
                    var keySymbolOOP = FindSymbol(keySymbolText);

                    // DEBUG: Track association resolution
                    Console.WriteLine($"[InitExpressionCompiler] Resolving association for '{keySymbolText}'");
                    Console.WriteLine($"  keySymbolOOP: {keySymbolOOP} (isNil={keySymbolOOP == _heapManager.GetNil()})");

                    var assocOOP = FindAssociation(smalltalkOOP, keySymbolOOP);

                    Console.WriteLine($"  assocOOP: {assocOOP} (isNil={assocOOP == _heapManager.GetNil()})");

                    if (assocOOP != _heapManager.GetNil())
                    {
                        var valueOOP = _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
                        Console.WriteLine($"  valueOOP: {valueOOP} (isNil={valueOOP == _heapManager.GetNil()})");
                        if (!valueOOP.IsSmallInteger && valueOOP != _heapManager.GetNil())
                        {
                            var valueClass = _heapManager.GetClassName(valueOOP);
                            Console.WriteLine($"  value class: {valueClass ?? "unknown"}");
                        }
                    }

                    return assocOOP;
                }
                throw new InvalidOperationException("Association literal missing Association data");

            case "nil":
                return _heapManager.GetNil();

            case "true":
                return _metadata.KnownObjects.True;

            case "false":
                return _metadata.KnownObjects.False;

            case "compiledMethod":
                // For block literals - they reference a method by localId
                // This should have been resolved during cold start
                if (literalValue.LocalId != null)
                {
                    // Blocks reference methods that should already exist
                    // This is a runtime limitation - we can't resolve these on-demand
                    throw new InvalidOperationException($"Block literals (compiledMethod) cannot be resolved at runtime: {literalValue.LocalId}");
                }
                throw new InvalidOperationException("CompiledMethod literal missing LocalId");

            default:
                throw new InvalidOperationException($"Unsupported literal type at runtime: {literalValue.Type}");
        }
    }

    /// <summary>
    /// Finds a symbol OOP by name from the existing heap.
    /// </summary>
    private SmalltalkOOP FindSymbol(string symbolText)
    {
        foreach (var (index, symbolOOP) in SymbolRegistry.AllSymbols())
        {
            var symbolString = ReadSymbolString(symbolOOP);
            if (symbolString == symbolText)
            {
                return symbolOOP;
            }
        }

        return _heapManager.GetNil();
    }

    /// <summary>
    /// Reads a Symbol object's string value from the heap.
    /// </summary>
    private string ReadSymbolString(SmalltalkOOP symbolOOP)
    {
        int size = _heapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Finds an Association in the SystemDictionary by key symbol.
    /// </summary>
    private SmalltalkOOP FindAssociation(SmalltalkOOP systemDictOOP, SmalltalkOOP keySymbolOOP)
    {
        int size = _heapManager.GetIndexedSize(systemDictOOP);
        string targetSymbol = ReadSymbolString(keySymbolOOP);

        // DEBUG: Log search parameters
        Console.WriteLine($"[FindAssociation] Searching for symbol '{targetSymbol}' (OOP={keySymbolOOP}) in SystemDictionary (size={size})");

        for (int i = 0; i < size; i++)
        {
            var assocOOP = _heapManager.GetIndexedOOP(systemDictOOP, i);
            if (assocOOP == _heapManager.GetNil())
                continue;

            var keyOOP = _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);

            // DEBUG: Compare both OOP and string to diagnose symbol mismatch
            if (keyOOP != _heapManager.GetNil() && !keyOOP.IsSmallInteger)
            {
                string keyString = ReadSymbolString(keyOOP);
                if (keyString == targetSymbol)
                {
                    if (keyOOP != keySymbolOOP)
                    {
                        Console.WriteLine($"[FindAssociation] WARNING: Symbol '{targetSymbol}' found but OOP mismatch!");
                        Console.WriteLine($"  Expected OOP: {keySymbolOOP}");
                        Console.WriteLine($"  Actual OOP: {keyOOP}");
                    }
                    // Return association even if OOPs don't match (string match is sufficient)
                    return assocOOP;
                }
            }

            if (keyOOP == keySymbolOOP)
            {
                return assocOOP;
            }
        }

        return _heapManager.GetNil();
    }

    /// <summary>
    /// Looks up a global variable in the SystemDictionary (runtime-safe).
    /// </summary>
    private SmalltalkOOP LookupGlobal(string globalName)
    {
        var smalltalkOOP = _metadata.KnownObjects.Smalltalk;
        int size = _heapManager.GetIndexedSize(smalltalkOOP);

        // Find the symbol OOP for the global name
        var symbolOOP = FindSymbol(globalName);
        if (symbolOOP == _heapManager.GetNil())
        {
            return _heapManager.GetNil();
        }

        // Search SystemDictionary for Association with this key
        for (int i = 0; i < size; i++)
        {
            var assocOOP = _heapManager.GetIndexedOOP(smalltalkOOP, i);
            if (assocOOP == _heapManager.GetNil())
                continue;

            var keyOOP = _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == symbolOOP)
            {
                return _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            }
        }

        return _heapManager.GetNil();
    }

    /// <summary>
    /// Extracts a double value from various possible runtime representations.
    /// </summary>
    private double GetFloatValue(object value)
    {
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetDouble(),
            _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name ?? "null"} to double")
        };
    }

    /// <summary>
    /// Allocates a Float object at runtime using the heap manager.
    /// Float is WordIndexed with 4 words (IEEE 754 double precision).
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value)
    {
        // Allocate WordIndexed object with 4 words for IEEE 754 double
        var floatOOP = _heapManager.AllocateObject(ObjectFormat.WordIndexed, fixedSlots: 0, indexedSize: 4);

        // Set Float class
        var floatClassOOP = LookupGlobal("Float");
        if (floatClassOOP != _heapManager.GetNil())
        {
            _heapManager.SetClassOOP(floatOOP, floatClassOOP);
        }

        // Convert double to IEEE 754 binary representation in little-endian byte order
        byte[] bytes = BitConverter.GetBytes(value);

        // Store as 4 words (16-bit each) preserving little-endian byte order
        for (int i = 0; i < 4; i++)
        {
            // Combine 2 bytes into a 16-bit word (little-endian)
            ushort word = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            _heapManager.SetIndexedWord(floatOOP, i, word);
        }

        return floatOOP;
    }

    /// <summary>
    /// Allocates a String object at runtime using the heap manager.
    /// String is ByteIndexed with one byte per character.
    /// </summary>
    private SmalltalkOOP AllocateString(string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object for the string bytes
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: bytes.Length);

        // Set String class
        var stringClassOOP = LookupGlobal("String");
        if (stringClassOOP != _heapManager.GetNil())
        {
            _heapManager.SetClassOOP(stringOOP, stringClassOOP);
        }

        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, bytes[i]);
        }

        return stringOOP;
    }
}
