using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using ClassHierarchyUtils = Smalltalk80.Interpreter.Engine.LoaderPhases.ClassHierarchyUtils;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// Loads a cold start bundle and initializes the virtual machine.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================\n");

        try
        {
            // Parse command line arguments
            var (coldStartFile, dumpClassName) = ParseCommandLine(args);

            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {coldStartFile}");
                Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
                Console.Error.WriteLine("                         (default: coldstart.json in executable directory)");
                Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
                Console.Error.WriteLine("                         after cold start completes");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Loading cold start bundle: {coldStartFile}");

            // Load the cold start bundle from JSON
            var bundle = ColdStartBundle.LoadFromFile(coldStartFile);

            Console.WriteLine($"Bundle loaded: {bundle.Metadata.Version}");
            Console.WriteLine($"  Symbols: {bundle.Symbols.Length}");
            Console.WriteLine($"  Classes: {bundle.Classes.Length}");
            Console.WriteLine($"  Objects: {bundle.Objects.Length}");
            Console.WriteLine($"  Methods: {bundle.Methods.Length}");
            Console.WriteLine();

            // Create the heap manager
            Console.WriteLine("Initializing heap manager...");
            var heapManager = new SmalltalkHeapManager();

            // Create and run the loader
            Console.WriteLine("Starting cold start loader...");
            var loader = new Loader(bundle, heapManager);
            loader.Run();

            // Execute init expressions
            if (bundle.InitExpressions.Length > 0)
            {
                Console.WriteLine("\n=== Init Expression Execution ===");
                Console.WriteLine($"  Found {bundle.InitExpressions.Length} init expression(s) in bundle");

                if (loader.LayoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                    return;
                }

                var vmMetadata = loader.BuildVMMetadata();
                var vm = new VirtualMachine(heapManager, loader.LayoutResolver, vmMetadata, bundle.Symbols);

                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();

                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
                    loader.LayoutResolver,
                    loader.LocalIdToObjectId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    InitPolicy.AbortOnFirstError,  // Stop on first error for debugging
                    defaultTimeoutMs: 5000,
                    symbols: bundle.Symbols,
                    strings: bundle.Strings,
                    allocator: loader.Allocator
                );

                // Sort init expressions by instruction count (simplest first)
                var sortedExprs = bundle.InitExpressions
                    .Select(e => new {
                        Expr = e,
                        InstructionCount = CountInstructions(ClassHierarchyUtils.DecodeBytecodes(e.Bytecodes))
                    })
                    .OrderBy(x => x.InstructionCount)
                    .Select(x => x.Expr)
                    .ToArray();

                Console.WriteLine($"\n  Sorted by instruction count (simplest to most complex)");
                Console.WriteLine($"  Execution mode: INTERACTIVE (press Enter after each expression)\n");

                int executed = 0;
                int succeeded = 0;
                int failed = 0;

                foreach (var initExpr in sortedExprs)
                {
                    var bytecodes = ClassHierarchyUtils.DecodeBytecodes(initExpr.Bytecodes);
                    int instrCount = CountInstructions(bytecodes);

                    Console.WriteLine($"[{executed + 1}/{sortedExprs.Length}] {initExpr.LocalId} ({instrCount} instructions)");
                    Console.WriteLine($"Source: {initExpr.Source ?? "<no source>"}");

                    // Wait for user input
                    Console.Write("Press Enter to execute (or 'q' to quit): ");
                    string? input = Console.ReadLine();
                    if (input?.ToLower() == "q")
                    {
                        Console.WriteLine("\n⚠️  Execution stopped by user");
                        break;
                    }

                    try
                    {
                        // Resolve literals from the init expression
                        var literalOOPs = new SmalltalkOOP[initExpr.Literals.Length];
                        for (int i = 0; i < initExpr.Literals.Length; i++)
                        {
                            literalOOPs[i] = ResolveLiteralValueAsOOP(initExpr.Literals[i], bundle, loader.Allocator, loader.LocalIdToObjectId);
                        }

                        // Create temporary CompiledMethod using allocator
                        if (!loader.LocalIdToObjectId.TryGetValue("class:CompiledMethod", out var compiledMethodClassOOP))
                        {
                            throw new InvalidOperationException("CompiledMethod class not found");
                        }

                        if (!loader.LocalIdToObjectId.TryGetValue("class:ByteArray", out var byteArrayClassOOP))
                        {
                            throw new InvalidOperationException("ByteArray class not found");
                        }

                        var tempMethodOOP = loader.Allocator.AllocateCompiledMethod(
                            compiledMethodClassOOP,
                            byteArrayClassOOP,
                            initExpr.Header.Word,
                            literalOOPs,
                            bytecodes);

                        // Execute the init expression
                        var result = executor.ExecuteInitExpression(initExpr.LocalId, tempMethodOOP);

                        executed++;

                        if (result.Status == InitStatus.Success)
                        {
                            succeeded++;
                            Console.WriteLine($"✓ Success ({result.ElapsedMilliseconds}ms)");
                            Console.WriteLine($"  Returned: {FormatReturnValue(heapManager, bundle, result.ReturnValueOop ?? GlobalRegistry.NilOOP)}");
                        }
                        else
                        {
                            failed++;
                            Console.WriteLine($"✗ FAILED: {result.Status} ({result.ElapsedMilliseconds}ms)");

                            // Verbose debugging output on failure
                            DumpInitExpressionDebugInfo(initExpr, bytecodes, literalOOPs, result, heapManager, bundle);

                            Console.WriteLine("\n⚠️  Execution stopped due to error (AbortOnFirstError policy)");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.WriteLine($"✗ FATAL ERROR: {ex.Message}");
                        Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                        Console.WriteLine("\n⚠️  Execution stopped due to fatal error");
                        break;
                    }

                    Console.WriteLine(); // Blank line between executions
                }

                Console.WriteLine($"\n=== Execution Summary ===");
                Console.WriteLine($"  Executed: {executed}/{sortedExprs.Length}");
                Console.WriteLine($"  Succeeded: {succeeded}");
                Console.WriteLine($"  Failed: {failed}");
            }

            Console.WriteLine("\n✓ All systems ready!");
            Console.WriteLine("Virtual machine initialized successfully.");

            // TODO: Implement --dump-class after ClassPrettyPrinter is migrated to new heap manager
            if (dumpClassName != null)
            {
                Console.WriteLine($"\n⚠️  --dump-class not yet implemented (ClassPrettyPrinter not migrated to new heap manager)");
            }

            // TODO: Start REPL or execute user code
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFatal error: {ex.Message}");
            Console.Error.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses command line arguments and returns the cold start file path and optional class name to dump.
    /// Supports: --coldstart path, --dump-class ClassName
    /// Default: coldstart.json in the same directory as the executable
    /// </summary>
    /// <summary>
    /// Counts the number of bytecode instructions (not bytes, since some are multi-byte).
    /// </summary>
    private static int CountInstructions(byte[] bytecodes)
    {
        int count = 0;
        int i = 0;

        while (i < bytecodes.Length)
        {
            byte opcode = bytecodes[i];
            count++;
            i++;

            // Extended prefix - consumes additional bytes
            if (opcode == 0x80)
            {
                if (i < bytecodes.Length)
                {
                    byte subop = bytecodes[i];
                    i++; // subop byte

                    // Check if this subop needs more bytes
                    if (subop == 0x0D || subop == 0x11) // SEND_N, SEND_SUPER_N
                    {
                        i += 2; // selector index + arg count
                    }
                    else if (subop >= 0x14 && subop <= 0x19) // Jump operations
                    {
                        i += 2; // offset (2 bytes)
                    }
                    else
                    {
                        i++; // most extended ops have 1 operand byte
                    }
                }
            }
            // Long jumps (0xA1-0xA7) - consume N bytes
            else if (opcode >= 0xA1 && opcode <= 0xA7)
            {
                int n = opcode - 159; // Number of bytes for distance
                i += n;
            }
            // Medium jump (0xA0) - consumes 1 byte
            else if (opcode == 0xA0)
            {
                i++;
            }
        }

        return count;
    }

    /// <summary>
    /// Helper method to find a method literal for debugging.
    /// </summary>
    private static SmalltalkOOP FindMethodLiteral(Loader loader, ColdStartBundle bundle, string methodLocalId, int literalIndex)
    {
        if (loader.LocalIdToObjectId.TryGetValue(methodLocalId, out var methodOOP))
        {
            return loader.Allocator.GetIndexedOOP(methodOOP, literalIndex + 1); // +1 to skip header
        }
        return new SmalltalkOOP(0);
    }

    /// <summary>
    /// Resolves a literal value to a SmalltalkOOP (from InitExpressionExecutor).
    /// </summary>
    private static SmalltalkOOP ResolveLiteralValueAsOOP(
        LiteralValue literal,
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        IReadOnlyDictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        return literal.Type switch
        {
            "symbol" => SymbolRegistry.GetSymbolOOP(literal.Index!.Value),
            "string" => allocator.AllocateString(bundle.Strings[literal.Index!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => allocator.AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => GlobalRegistry.NilOOP,
            "true" => GlobalRegistry.TrueOOP,
            "false" => GlobalRegistry.FalseOOP,
            "objectRef" => localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP1)
                ? objOOP1
                : throw new InvalidOperationException($"objectRef not found: '{literal.LocalId}'"),
            "compiledMethod" => localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP2)
                ? objOOP2
                : throw new InvalidOperationException($"compiledMethod not found: '{literal.LocalId}'"),
            "character" => AllocateCharacter(literal.CodePoint!.Value, allocator),
            "bytes" => AllocateByteArray(literal.Encoding!, literal.Data!, allocator),
            "association" => ResolveAssociation(literal.Association!, bundle, allocator, localIdToObjectId),
            "array" => AllocateArray(literal.Elements!.Select(l => ResolveLiteralValueAsOOP(l, bundle, allocator, localIdToObjectId)).ToArray(), allocator),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    private static SmalltalkOOP AllocateCharacter(int codePoint, SemanticAllocator allocator)
    {
        var characterOOP = allocator.AllocateFixedObject("Character");
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);
        return characterOOP;
    }

    private static SmalltalkOOP AllocateByteArray(string encoding, string data, SemanticAllocator allocator)
    {
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };
        return allocator.AllocateByteArray("ByteArray", bytes);
    }

    private static SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs, SemanticAllocator allocator)
    {
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = allocator.AllocateArray(arrayClassOOP, elementOOPs.Length);
        for (int i = 0; i < elementOOPs.Length; i++)
        {
            allocator.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
        }
        return arrayOOP;
    }

    private static SmalltalkOOP ResolveAssociation(
        AssociationLiteral assoc,
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        IReadOnlyDictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        var keyOOP = SymbolRegistry.GetSymbolOOP(assoc.KeyIndex);
        string keyName = bundle.Symbols[assoc.KeyIndex];

        if (assoc.ValueRef?.LocalId == null)
        {
            if (GlobalRegistry.TryGet($"class:{keyName}", out SmalltalkOOP classOop))
            {
                var sysDictOOP = GlobalRegistry.SystemDictionaryOOP;
                int sysDictSize = allocator.GetIndexedSize(sysDictOOP);

                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP == GlobalRegistry.NilOOP) continue;

                    var assocKeyOOP = allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP == keyOOP)
                    {
                        return assocOOP;
                    }
                }
            }
        }

        var valueOOP = assoc.ValueRef?.LocalId != null
            ? localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilOOP;

        var associationOOP = allocator.AllocateFixedObject("Association");
        allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        return associationOOP;
    }

    private static byte[] ConvertHexStringToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (hex.Length % 2 != 0)
            throw new InvalidOperationException("Hex string must have even number of characters");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private static int GetIntValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    private static double GetFloatValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Formats a return value OOP for display.
    /// </summary>
    private static string FormatReturnValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP returnOOP)
    {
        if (returnOOP.IsSmallInteger)
            return $"SmallInteger({returnOOP.AsSmallInteger()})";

        if (returnOOP == GlobalRegistry.NilOOP)
            return "nil";

        if (returnOOP == GlobalRegistry.TrueOOP)
            return "true";

        if (returnOOP == GlobalRegistry.FalseOOP)
            return "false";

        try
        {
            // Get the class OOP, then get the class name from it
            var classOOP = heapManager.GetClassOOP(returnOOP);
            var className = heapManager.GetClassName(classOOP);

            // GetClassName returns the actual class name, never "UnknownClass"
            return $"{className} (OOP={returnOOP})";
        }
        catch (Exception ex)
        {
            // Even if there's an error, try to get some useful info
            try
            {
                var classOOP = heapManager.GetClassOOP(returnOOP);
                return $"<class={classOOP}> (OOP={returnOOP})";
            }
            catch
            {
                return $"<error: {ex.Message}> (OOP={returnOOP})";
            }
        }
    }

    /// <summary>
    /// Dumps verbose debugging information when an init expression fails.
    /// </summary>
    private static void DumpInitExpressionDebugInfo(
        InitExpression initExpr,
        byte[] bytecodes,
        SmalltalkOOP[] literalOOPs,
        InitResult result,
        ISmalltalkHeapManager heapManager,
        ColdStartBundle bundle)
    {
        Console.WriteLine("\n=== VERBOSE DEBUG OUTPUT ===");

        // 1. Source code
        Console.WriteLine($"\nSource Code:");
        Console.WriteLine($"  {initExpr.Source ?? "<no source>"}");

        // 2. Error details
        Console.WriteLine($"\nError:");
        Console.WriteLine($"  Status: {result.Status}");
        if (result.Diagnostic?.Message != null)
        {
            Console.WriteLine($"  Message: {result.Diagnostic.Message}");
        }

        // 3. Literals
        Console.WriteLine($"\nLiterals ({literalOOPs.Length}):");
        for (int i = 0; i < literalOOPs.Length; i++)
        {
            Console.WriteLine($"  [{i}] = {FormatReturnValue(heapManager, bundle, literalOOPs[i])}");
        }

        // 4. Bytecode disassembly (symbolic form)
        Console.WriteLine($"\nBytecode ({bytecodes.Length} bytes, {CountInstructions(bytecodes)} instructions):");
        DisassembleBytecodes(bytecodes, bundle);

        // 5. Stack trace (if available)
        if (result.Diagnostic?.StackTrace != null)
        {
            Console.WriteLine($"\nStack Trace:");
            Console.WriteLine($"  {result.Diagnostic.StackTrace}");
        }

        Console.WriteLine("\n=== END DEBUG OUTPUT ===");
    }

    /// <summary>
    /// Disassembles bytecodes to symbolic form for debugging.
    /// </summary>
    private static void DisassembleBytecodes(byte[] bytecodes, ColdStartBundle bundle)
    {
        int pc = 0;
        int instrNum = 0;

        while (pc < bytecodes.Length)
        {
            byte opcode = bytecodes[pc];
            Console.Write($"  [{instrNum:D3}] @{pc:D4}: ");

            pc++;

            // Decode based on bytecode ranges from Interpreter-Architecture.md
            if (opcode <= 0x0F) // Push receiver variable 0-15
            {
                Console.WriteLine($"PUSH_IVAR_{opcode}");
            }
            else if (opcode <= 0x1F) // Push temporary 0-15
            {
                Console.WriteLine($"PUSH_TEMP_{opcode - 0x10}");
            }
            else if (opcode <= 0x3F) // Push literal constant 0-31
            {
                Console.WriteLine($"PUSH_LITERAL_CONST_{opcode - 0x20}");
            }
            else if (opcode <= 0x5F) // Push literal variable 0-31
            {
                Console.WriteLine($"PUSH_LITERAL_VAR_{opcode - 0x40}");
            }
            else if (opcode <= 0x67) // Pop/store receiver variable 0-7
            {
                Console.WriteLine($"POP_STORE_IVAR_{opcode - 0x60}");
            }
            else if (opcode <= 0x6F) // Pop/store temporary 0-7
            {
                Console.WriteLine($"POP_STORE_TEMP_{opcode - 0x68}");
            }
            else if (opcode == 0x70) Console.WriteLine("PUSH_SELF");
            else if (opcode == 0x71) Console.WriteLine("PUSH_TRUE");
            else if (opcode == 0x72) Console.WriteLine("PUSH_FALSE");
            else if (opcode == 0x73) Console.WriteLine("PUSH_NIL");
            else if (opcode == 0x74) Console.WriteLine("PUSH_MINUS_ONE");
            else if (opcode == 0x75) Console.WriteLine("PUSH_ZERO");
            else if (opcode == 0x76) Console.WriteLine("PUSH_ONE");
            else if (opcode == 0x77) Console.WriteLine("PUSH_TWO");
            else if (opcode == 0x78) Console.WriteLine("RETURN_SELF");
            else if (opcode == 0x79) Console.WriteLine("RETURN_TRUE");
            else if (opcode == 0x7A) Console.WriteLine("RETURN_FALSE");
            else if (opcode == 0x7B) Console.WriteLine("RETURN_NIL");
            else if (opcode == 0x7C) Console.WriteLine("RETURN_TOP");
            else if (opcode == 0x7D) Console.WriteLine("BLOCK_RETURN_SELF");
            else if (opcode == 0x7E) Console.WriteLine("BLOCK_RETURN_TOP");
            else if (opcode == 0x7F) Console.WriteLine("NOP");
            else if (opcode == 0x80) // Extended prefix
            {
                if (pc < bytecodes.Length)
                {
                    byte subop = bytecodes[pc++];
                    Console.Write($"EXTENDED_{subop:X2} ");

                    string[] extendedNames = new[] {
                        "PUSH_IVAR", "STORE_IVAR", "POP_STORE_IVAR", "PUSH_TEMP", "STORE_TEMP", "POP_STORE_TEMP",
                        "PUSH_LITERAL", "PUSH_LITERAL_VAR", "STORE_LITERAL_VAR", "POP_STORE_LITERAL_VAR",
                        "SEND_0", "SEND_1", "SEND_2", "SEND_N", "SEND_SUPER_0", "SEND_SUPER_1",
                        "SEND_SUPER_2", "SEND_SUPER_N", "POP", "DUP", "JUMP_TRUE", "JUMP_FALSE",
                        "JUMP", "JUMP_LONG_TRUE", "JUMP_LONG_FALSE", "JUMP_LONG"
                    };

                    if (subop < extendedNames.Length)
                    {
                        Console.Write($"({extendedNames[subop]})");
                        if (subop <= 0x09 && pc < bytecodes.Length) // Has index operand
                        {
                            Console.Write($" {bytecodes[pc++]}");
                        }
                        else if ((subop == 0x0D || subop == 0x11) && pc + 1 < bytecodes.Length) // SEND_N
                        {
                            Console.Write($" selector={bytecodes[pc++]}, argc={bytecodes[pc++]}");
                        }
                        else if (subop >= 0x14 && subop <= 0x19 && pc + 1 < bytecodes.Length) // Jumps
                        {
                            int offset = (bytecodes[pc] << 8) | bytecodes[pc + 1];
                            pc += 2;
                            Console.Write($" offset={offset}");
                        }
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("(incomplete)");
                }
            }
            else if (opcode == 0x89) Console.WriteLine("PUSH_ACTIVE_CONTEXT");
            else if (opcode >= 0x90 && opcode <= 0x97) // Short jumps
            {
                Console.WriteLine($"SHORT_JUMP_FWD_{opcode - 0x8F}");
            }
            else if (opcode == 0xA0 && pc < bytecodes.Length) // Medium jump
            {
                Console.WriteLine($"MEDIUM_JUMP_FWD {bytecodes[pc++] + 1}");
            }
            else if (opcode >= 0xA1 && opcode <= 0xA7) // Long jumps
            {
                int n = opcode - 159;
                Console.Write($"LONG_JUMP_FWD_{n} ");
                if (pc + n <= bytecodes.Length)
                {
                    int distance = 0;
                    for (int i = 0; i < n; i++)
                    {
                        distance = (distance << 8) | bytecodes[pc++];
                    }
                    Console.WriteLine($"{distance + 1}");
                }
                else
                {
                    Console.WriteLine("(incomplete)");
                }
            }
            else if (opcode >= 0xB0 && opcode <= 0xBF) // Arithmetic sends
            {
                string[] arithOps = new[] { "+", "-", "<", ">", "<=", ">=", "=", "~=", "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:" };
                Console.WriteLine($"SEND_ARITH ({arithOps[opcode - 0xB0]})");
            }
            else if (opcode >= 0xC0 && opcode <= 0xCF) // Special sends
            {
                string[] specialOps = new[] { "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "==", "class", "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y" };
                Console.WriteLine($"SEND_SPECIAL ({specialOps[opcode - 0xC0]})");
            }
            else if (opcode >= 0xD0 && opcode <= 0xDF) // Send literal 0 args
            {
                Console.WriteLine($"SEND_LIT_0_ARGS_{opcode - 0xD0}");
            }
            else if (opcode >= 0xE0 && opcode <= 0xEF) // Send literal 1 arg
            {
                Console.WriteLine($"SEND_LIT_1_ARG_{opcode - 0xE0}");
            }
            else if (opcode >= 0xF0 && opcode <= 0xFF) // Send literal 2 args
            {
                Console.WriteLine($"SEND_LIT_2_ARGS_{opcode - 0xF0}");
            }
            else
            {
                Console.WriteLine($"UNKNOWN_{opcode:X2}");
            }

            instrNum++;
        }
    }

    private static (string coldStartFile, string? dumpClassName) ParseCommandLine(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--coldstart" && i + 1 < args.Length)
            {
                coldStartFile = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i] == "--dump-class" && i + 1 < args.Length)
            {
                dumpClassName = args[i + 1];
                i++; // Skip the next argument
            }
        }

        // Default: coldstart.json in the executable directory
        if (coldStartFile == null)
        {
            string exeDir = AppContext.BaseDirectory;
            coldStartFile = Path.Combine(exeDir, "coldstart.json");
        }

        return (coldStartFile, dumpClassName);
    }

    private static void DiagnoseFloatClassVariables(ISmalltalkHeapManager heapManager, Loader loader, ColdStartBundle bundle)
    {
        try
        {
            // Expected values from Float>>initialize source code
            var expectedValues = new Dictionary<string, string>
            {
                { "Pi", "3.14159" },
                { "Halfpi", "1.5708 (Pi / 2.0)" },
                { "Fourthpi", "0.78540 (Pi / 4.0)" },
                { "Twopi", "6.28318 (Pi * 2.0)" },
                { "RadiansPerDegree", "0.01745 (Pi / 180.0)" },
                { "Ln2", "0.693147" },
                { "Sqrt2", "1.41421" },
                { "SinCoefficients", "Array: #(-0.166667 0.00833333 -1.98409e-4 2.7526e-6 -2.39e-8)" },
                { "TanCoefficients", "Array: #(0.333331 0.133392 0.0533741 0.0245651 0.00290052 0.00951681)" },
                { "ExpPCoefficients", "Array: #(28.8756 2525.04)" },
                { "ExpQCoefficients", "Array: #(1.0 375.022 7285.73)" },
                { "LnCoefficients", "Array: #(0.237625 0.285254 0.400006 0.666667 2.0)" }
            };

            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                Console.WriteLine("  ⚠️  Float class not found in GlobalRegistry");
                return;
            }
            var floatClassOOP = floatClassId;

            // Get the metaclass (Float class)
            var floatMetaclassOOP = heapManager.GetClassOOP(floatClassOOP);

            Console.WriteLine($"Float class: OOP={floatClassOOP} (index={floatClassId})");
            Console.WriteLine($"Float metaclass: OOP={floatMetaclassOOP}");
            Console.WriteLine();

            // Get class variables from SystemDictionary associations
            foreach (var varName in expectedValues.Keys)
            {
                var actual = GetClassVariableValue(heapManager, bundle, varName);
                var expected = expectedValues[varName];
                
                Console.WriteLine($"{varName}:");
                Console.WriteLine($"  Expected: {expected}");
                Console.WriteLine($"  Actual:   {actual}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Error diagnosing Float class variables: {ex.Message}");
        }
    }

    private static string GetClassVariableValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, string varName)
    {
        try
        {
            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                return "Float class not found";
            }
            var floatClassOOP = floatClassId;

            // Float class is an instance of Class, which has instance variables from Behavior/ClassDescription/Class hierarchy:
            // Behavior: superclass, methodDict, format, subclasses
            // ClassDescription: instanceVariables, organization
            // Class: name, classPool, sharedPools
            // So classPool is at index: 4 (Behavior) + 2 (ClassDescription) + 1 (name) = 7
            
            // Get the classPool instance variable (Dictionary containing class variables)
            var classPoolOOP = heapManager.GetInstanceVariable(floatClassOOP, 7);
            
            if (classPoolOOP == GlobalRegistry.NilOOP)
            {
                return "classPool is nil";
            }

            // classPool is a Dictionary with indexed array of associations
            int size = heapManager.GetIndexedSize(classPoolOOP);
            
            // Check if classPool is empty (no entries yet - they're lazily created)
            int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", bundle);
            if (tallyIndex >= 0)
            {
                var tallyOOP = heapManager.GetInstanceVariable(classPoolOOP, tallyIndex);
                if (tallyOOP.IsSmallInteger && tallyOOP.AsSmallInteger() == 0)
                {
                    return "classPool empty (variable not yet created - awaiting first access)";
                }
            }
            
            // Search for the variable name association
            for (int i = 0; i < size; i++)
            {
                var assocOOP = heapManager.GetIndexedOOP(classPoolOOP, i);
                if (assocOOP == GlobalRegistry.NilOOP)
                    continue;

                // Association has key at slot 1, value at slot 2
                var keyOOP = heapManager.GetSlot(assocOOP, 1);
                var keyName = GetSymbolName(heapManager, bundle, keyOOP);
                
                if (keyName == varName)
                {
                    var valueOOP = heapManager.GetSlot(assocOOP, 2);
                    return FormatValue(heapManager, bundle, valueOOP);
                }
            }

            return $"Not found in Float classPool (searched {size} slots)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string GetSymbolName(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP symbolOOP)
    {
        if (symbolOOP.IsSmallInteger)
            return $"SmallInteger({symbolOOP.AsSmallInteger()})";

        try
        {
            // Symbol is a byte-indexed object containing characters
            int size = heapManager.GetIndexedSize(symbolOOP);
            char[] chars = new char[size];
            for (int i = 0; i < size; i++)
            {
                chars[i] = (char)heapManager.GetIndexedByte(symbolOOP, i);
            }
            return new string(chars);
        }
        catch
        {
            return $"<Symbol OOP={symbolOOP}>";
        }
    }

    private static string FormatValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP valueOOP)
    {
        if (valueOOP.IsSmallInteger)
            return $"{valueOOP.AsSmallInteger()}";

        try
        {
            var format = heapManager.GetObjectFormat(valueOOP);
            
            // Check if it's a Float (ByteIndexed with 8 bytes)
            if (format == ObjectFormat.ByteIndexed && heapManager.GetIndexedSize(valueOOP) == 8)
            {
                byte[] bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    bytes[i] = heapManager.GetIndexedByte(valueOOP, i);
                }
                double value = BitConverter.ToDouble(bytes, 0);
                return $"{value:F6}";
            }
            
            // Check if it's an Array
            var classOOP = heapManager.GetClassOOP(valueOOP);
            if (GlobalRegistry.TryGet("class:Array", out SmalltalkOOP arrayClassOOP))
            {
                if (classOOP == arrayClassOOP)
                {
                    int size = heapManager.GetIndexedSize(valueOOP);
                    var elements = new List<string>();
                    for (int i = 0; i < size && i < 10; i++)  // Limit to 10 elements
                    {
                        var elemOOP = heapManager.GetIndexedOOP(valueOOP, i);
                        elements.Add(FormatValue(heapManager, bundle, elemOOP));
                    }
                    if (size > 10)
                        elements.Add("...");
                    return $"Array({size}): #({string.Join(" ", elements)})";
                }
            }

            return $"<Object OOP={valueOOP}, format={format}>";
        }
        catch (Exception ex)
        {
            return $"<Error: {ex.Message}>";
        }
    }

    private static void DiagnoseCursorClassVariables(ISmalltalkHeapManager heapManager, Loader loader, ColdStartBundle bundle)
    {
        try
        {
            // Cursor class variables (based on typical Smalltalk-80 Cursor implementation)
            // Note: Actual variables may vary - this is a diagnostic to see what's actually created
            var expectedVariables = new[] 
            { 
                "Normal", "Read", "Write", "Execute", "Move", "Copy", "Corner", "CrossHair",
                "Marker", "Origin", "Down", "Up", "Frame", "BlankCursor", "CurrentCursor"
            };

            // Get Cursor class OOP
            if (!GlobalRegistry.TryGet("class:Cursor", out SmalltalkOOP cursorClassId))
            {
                Console.WriteLine("  ⚠️  Cursor class not found in GlobalRegistry");
                return;
            }
            var cursorClassOOP = cursorClassId;

            // Get the metaclass (Cursor class)
            var cursorMetaclassOOP = heapManager.GetClassOOP(cursorClassOOP);

            Console.WriteLine($"Cursor class: OOP={cursorClassOOP} (index={cursorClassId})");
            Console.WriteLine($"Cursor metaclass: OOP={cursorMetaclassOOP}");
            Console.WriteLine();

            // Get classPool instance variable (Dictionary containing class variables)
            // Class hierarchy: Behavior (4 vars) + ClassDescription (2 vars) + Class (name, classPool, sharedPools)
            // classPool is at index 7
            var classPoolOOP = heapManager.GetInstanceVariable(cursorClassOOP, 7);
            
            if (classPoolOOP == GlobalRegistry.NilOOP)
            {
                Console.WriteLine("  classPool is nil - no class variables created yet");
                return;
            }

            // Check tally to see if dictionary is empty
            int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", bundle);
            if (tallyIndex >= 0)
            {
                var tallyOOP = heapManager.GetInstanceVariable(classPoolOOP, tallyIndex);
                if (tallyOOP.IsSmallInteger && tallyOOP.AsSmallInteger() == 0)
                {
                    Console.WriteLine("  classPool empty (tally=0) - no class variables created yet");
                    return;
                }
                else if (tallyOOP.IsSmallInteger)
                {
                    Console.WriteLine($"  classPool contains {tallyOOP.AsSmallInteger()} class variable(s)");
                }
            }

            // Enumerate all class variables found in classPool
            int size = heapManager.GetIndexedSize(classPoolOOP);
            Console.WriteLine($"  Scanning {size} dictionary slots...");
            Console.WriteLine();

            var foundVariables = new List<string>();
            for (int i = 0; i < size; i++)
            {
                var assocOOP = heapManager.GetIndexedOOP(classPoolOOP, i);
                if (assocOOP == GlobalRegistry.NilOOP)
                    continue;

                // Association has key at slot 1, value at slot 2
                var keyOOP = heapManager.GetSlot(assocOOP, 1);
                var keyName = GetSymbolName(heapManager, bundle, keyOOP);
                var valueOOP = heapManager.GetSlot(assocOOP, 2);
                var value = FormatValue(heapManager, bundle, valueOOP);
                
                foundVariables.Add(keyName);
                Console.WriteLine($"{keyName}:");
                Console.WriteLine($"  Value: {value}");
                Console.WriteLine();
            }

            // Report on expected vs found
            if (foundVariables.Count == 0)
            {
                Console.WriteLine("  ⚠️  No class variables found (dictionary has entries but none were readable)");
            }
            else
            {
                Console.WriteLine($"Summary: Found {foundVariables.Count} class variable(s): {string.Join(", ", foundVariables)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Error diagnosing Cursor class variables: {ex.Message}");
            Console.WriteLine($"     {ex.StackTrace}");
        }
    }
}
