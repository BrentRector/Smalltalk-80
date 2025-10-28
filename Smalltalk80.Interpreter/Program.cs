using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
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
            var (coldStartFile, dumpClassName, verboseDebug, testFilter, useNewLoader, reportOutput) = ParseCommandLine(args);

            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {coldStartFile}");
                Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
                Console.Error.WriteLine("                         (default: coldstart.json in executable directory)");
                Console.Error.WriteLine("  --new-loader           Use the new 6-phase loader (default: current 17-phase)");
                Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
                Console.Error.WriteLine("                         after cold start completes");
                Console.Error.WriteLine("  --test-filter <pattern> Run only init expressions matching pattern");
                Console.Error.WriteLine("                         (e.g., 'BlockValidator' or 'Test')");
                Console.Error.WriteLine("  --verbose, -v          Enable verbose debugging (logs all method calls)");
                Console.Error.WriteLine("  --coldstart-report=<path>  Write coldstart metrics to JSON file (requires --new-loader)");
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

            // Create and run the loader (old or new based on flag)
            Console.WriteLine($"Starting cold start loader ({(useNewLoader ? "NEW 6-phase" : "current 17-phase")})...");

            LayoutResolver? layoutResolver;
            if (useNewLoader)
            {
                var newLoader = new Loader_New(bundle, heapManager, coldStartFile);
                newLoader.Run();
                layoutResolver = newLoader.LayoutResolver;

                // Write report if requested
                if (reportOutput != null)
                {
                    var reportJson = System.Text.Json.JsonSerializer.Serialize(newLoader.Report, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });

                    File.WriteAllText(reportOutput, reportJson);
                    Console.WriteLine($"\nColdstart report written to: {reportOutput}");
                }
            }
            else
            {
                var loader = new Loader(bundle, heapManager);
                loader.Run();
                layoutResolver = loader.LayoutResolver;
            }

            Console.WriteLine("Cold start completed successfully!");

            // Execute init expressions
            if (bundle.InitExpressions.Length > 0)
            {
                Console.WriteLine("\n=== Init Expression Execution ===");
                Console.WriteLine($"  Found {bundle.InitExpressions.Length} init expression(s) in bundle");

                if (layoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                    return;
                }

                var vmMetadata = BuildVMMetadata(layoutResolver);
                var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata);

                // Enable verbose debugging if requested
                vm.VerboseDebug = verboseDebug;

                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();

                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
                    layoutResolver,
                    InitPolicy.ContinueAndCollect,  // Continue to run all init expressions including tests
                    defaultTimeoutMs: 5000
                );

                // Note: Init expressions are pre-allocated during coldstart Phase 8b
                // and stored in SpecialObjectsArray.InitExpressionMethods.

                // Phase-based execution strategy:
                // Phase 1: Execute all initialize! expressions (infrastructure setup)
                // Phase 2: Execute all comment: expressions (documentation)
                // Fast-fail: Stop on first error with full diagnostics

                var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
                int totalExpressions = bundle.InitExpressions.Length;

                // Build index lists for each phase (preserving source file order)
                var phase1Indices = new List<int>(); // initialize! expressions
                var phase2Indices = new List<int>(); // comment: expressions

                for (int i = 0; i < totalExpressions; i++)
                {
                    var expr = bundle.InitExpressions[i];
                    if (expr.Source != null)
                    {
                        if (expr.Source.EndsWith(" initialize!"))
                        {
                            phase1Indices.Add(i);
                        }
                        else if (expr.Source.Contains(" comment:"))
                        {
                            phase2Indices.Add(i);
                        }
                    }
                }

                Console.WriteLine($"\n=== Phase 1: Infrastructure Initialization ===");
                Console.WriteLine($"  Executing {phase1Indices.Count} initialize! expression(s) in source file order");
                Console.WriteLine();

                int succeeded = 0;
                int failed = 0;

                // Phase 1: Execute all initialize! expressions
                foreach (int index in phase1Indices)
                {
                    var expr = bundle.InitExpressions[index];
                    var initMethodOOP = heapManager.GetIndexedOOP(initArrayOOP, index);

                    Console.WriteLine($"[{index}] {expr.Source}");
                    var result = executor.ExecuteInitExpression(expr.LocalId, initMethodOOP);

                    if (result.Status != InitStatus.Success)
                    {
                        failed++;
                        Console.WriteLine($"\n❌ FAILED: {expr.Source}");
                        Console.WriteLine($"   Status: {result.Status}");
                        if (result.Diagnostic?.Message != null)
                        {
                            Console.WriteLine($"   Error: {result.Diagnostic.Message}");
                        }
                        if (result.Diagnostic?.StackTrace != null)
                        {
                            Console.WriteLine($"\n   Stack Trace:");
                            Console.WriteLine($"   {result.Diagnostic.StackTrace}");
                        }

                        Console.WriteLine($"\n=== Execution Failed ===");
                        Console.WriteLine($"  Phase 1: {succeeded} succeeded, {failed} failed");
                        Console.WriteLine($"  Fast-fail: Stopping execution");
                        return;
                    }

                    succeeded++;
                }

                Console.WriteLine($"\n✓ Phase 1 Complete: {succeeded} initialize! expression(s) executed successfully");

                // Phase 2: Execute all comment: expressions
                Console.WriteLine($"\n=== Phase 2: Class Documentation ===");
                Console.WriteLine($"  Executing {phase2Indices.Count} comment: expression(s) in source file order");
                Console.WriteLine();

                foreach (int index in phase2Indices)
                {
                    var expr = bundle.InitExpressions[index];
                    var initMethodOOP = heapManager.GetIndexedOOP(initArrayOOP, index);

                    // Show abbreviated source for comments (they're long)
                    string displaySource = expr.Source ?? expr.LocalId;
                    if (displaySource.Length > 80)
                    {
                        displaySource = displaySource.Substring(0, 77) + "...";
                    }
                    Console.WriteLine($"[{index}] {displaySource}");

                    var result = executor.ExecuteInitExpression(expr.LocalId, initMethodOOP);

                    if (result.Status != InitStatus.Success)
                    {
                        failed++;
                        Console.WriteLine($"\n❌ FAILED: {expr.Source}");
                        Console.WriteLine($"   Status: {result.Status}");
                        if (result.Diagnostic?.Message != null)
                        {
                            Console.WriteLine($"   Error: {result.Diagnostic.Message}");
                        }
                        if (result.Diagnostic?.StackTrace != null)
                        {
                            Console.WriteLine($"\n   Stack Trace:");
                            Console.WriteLine($"   {result.Diagnostic.StackTrace}");
                        }

                        Console.WriteLine($"\n=== Execution Failed ===");
                        Console.WriteLine($"  Phase 1: {phase1Indices.Count} succeeded");
                        Console.WriteLine($"  Phase 2: {succeeded - phase1Indices.Count} succeeded, {failed} failed");
                        Console.WriteLine($"  Fast-fail: Stopping execution");
                        return;
                    }

                    succeeded++;
                }

                Console.WriteLine($"\n✓ Phase 2 Complete: {phase2Indices.Count} comment: expression(s) executed successfully");

                // Final summary
                Console.WriteLine($"\n=== Init Expression Execution Summary ===");
                Console.WriteLine($"  Phase 1 (initialize!): {phase1Indices.Count} succeeded");
                Console.WriteLine($"  Phase 2 (comment:): {phase2Indices.Count} succeeded");
                Console.WriteLine($"  Total: {succeeded}/{totalExpressions} succeeded, {failed} failed");
                Console.WriteLine($"\n✓ All init expressions executed successfully!");
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
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
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
                    if (assocOOP == SpecialObjectsArray.Instance.Nil) continue;

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
            : SpecialObjectsArray.Instance.Nil;

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

        if (returnOOP == SpecialObjectsArray.Instance.Nil)
            return "nil";

        if (returnOOP == SpecialObjectsArray.Instance.True)
            return "true";

        if (returnOOP == SpecialObjectsArray.Instance.False)
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

    private static (string coldStartFile, string? dumpClassName, bool verboseDebug, string? testFilter, bool useNewLoader, string? reportOutput) ParseCommandLine(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;
        bool verboseDebug = false;
        string? testFilter = null;
        bool useNewLoader = false;
        string? reportOutput = null;

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
            else if (args[i] == "--test-filter" && i + 1 < args.Length)
            {
                testFilter = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i] == "--verbose" || args[i] == "-v")
            {
                verboseDebug = true;
            }
            else if (args[i] == "--new-loader")
            {
                useNewLoader = true;
            }
            else if (args[i].StartsWith("--coldstart-report="))
            {
                reportOutput = args[i].Substring("--coldstart-report=".Length);
            }
        }

        // Default: coldstart.json in the executable directory
        if (coldStartFile == null)
        {
            string exeDir = AppContext.BaseDirectory;
            coldStartFile = Path.Combine(exeDir, "coldstart.json");
        }

        return (coldStartFile, dumpClassName, verboseDebug, testFilter, useNewLoader, reportOutput);
    }

    private static VMMetadata BuildVMMetadata(LayoutResolver layoutResolver)
    {
        int thisClassIdx = layoutResolver.GetFieldIndex("class:Metaclass", "thisClass");

        return new VMMetadata
        {
            ClassNameFieldIndex = layoutResolver.GetFieldIndex("class:Class", "name"),
            ClassSuperclassFieldIndex = layoutResolver.GetFieldIndex("class:Class", "superclass"),
            ClassMethodDictFieldIndex = layoutResolver.GetFieldIndex("class:Class", "methodDict"),
            ClassFormatFieldIndex = layoutResolver.GetFieldIndex("class:Class", "format"),
            MetaclassThisClassFieldIndex = thisClassIdx,
            MethodDictTallyFieldIndex = layoutResolver.GetFieldIndex("class:MethodDictionary", "tally"),
            MethodDictKeysFieldIndex = layoutResolver.GetFieldIndex("class:MethodDictionary", "keys"),
            MethodDictValuesFieldIndex = layoutResolver.GetFieldIndex("class:MethodDictionary", "values"),
            ClassClassOOP = GlobalRegistry.Get("class:Class"),
            MetaclassClassOOP = GlobalRegistry.Get("class:Metaclass"),
            SymbolClassOOP = GlobalRegistry.Get("class:Symbol"),
            StringClassOOP = GlobalRegistry.Get("class:String"),
            ArrayClassOOP = GlobalRegistry.Get("class:Array"),
            CompiledMethodClassOOP = GlobalRegistry.Get("class:CompiledMethod"),
            SmallIntegerClassOOP = GlobalRegistry.Get("class:SmallInteger")
        };
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
            
            if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
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
                if (assocOOP == SpecialObjectsArray.Instance.Nil)
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
            
            if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
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
                if (assocOOP == SpecialObjectsArray.Instance.Nil)
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
