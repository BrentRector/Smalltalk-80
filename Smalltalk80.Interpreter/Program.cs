using System.Windows.Forms;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Diagnostics.Listeners;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Platform.Windows;
using Smalltalk80.Interpreter.Shared.Types;
using ClassHierarchyTraversal = Smalltalk80.Interpreter.Core.ClassHierarchyTraversal;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// Loads a cold start bundle and initializes the virtual machine.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Enable Windows Forms visual styles
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================\n");

        try
        {
            // Parse command line arguments
            var config = CommandLineParser.Parse(args);
            if (config == null)
            {
                // Help was requested
                Environment.Exit(0);
                return;
            }

            if (!File.Exists(config.ColdStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {config.ColdStartFile}");
                CommandLineParser.PrintUsage();
                Environment.Exit(1);
                return;
            }

            // Extract config values for easier access
            var coldStartFile = config.ColdStartFile;
            var dumpClassName = config.DumpClassName;
            var verboseDebug = config.VerboseDebug;
            var testFilter = config.TestFilter;
            var reportOutput = config.ReportOutput;
            var skipInitExpressions = config.SkipInitExpressions;
            var executeExpression = config.ExecuteExpression;
            var diagnosticCategories = config.DiagnosticCategories;

            // Create diagnostic hub for event-based logging
            var diagnostics = new DiagnosticHub();

            // Register cold start progress listener for console output
            var progressListener = new ColdStartProgressListener();
            diagnostics.Register(progressListener, DiagnosticEventTypes.AllColdStart);

            // Register additional diagnostic listeners based on --diagnostics flag
            if (diagnosticCategories != null)
            {
                var categories = diagnosticCategories.ToLower().Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var category in categories)
                {
                    switch (category.Trim())
                    {
                        case "detail":
                        case "verbose":
                            var detailListener = new ColdStartDetailListener();
                            diagnostics.Register(detailListener, DiagnosticEventTypes.AllColdStartDetail);
                            Console.WriteLine("Diagnostics: Cold start detail listener enabled");
                            break;

                        case "method":
                        case "trace":
                            var methodListener = new MethodTraceListener();
                            diagnostics.Register(methodListener, DiagnosticEventTypes.AllMethod);
                            Console.WriteLine("Diagnostics: Method trace listener enabled");
                            break;

                        case "gc":
                        case "memory":
                            var memoryListener = new RuntimeMemoryListener();
                            diagnostics.Register(memoryListener, DiagnosticEventTypes.AllMemory);
                            Console.WriteLine("Diagnostics: Runtime memory listener enabled");
                            break;

                        case "bytecode":
                        case "instruction":
                            var bytecodeListener = new BytecodeTraceListener();
                            diagnostics.Register(bytecodeListener, DiagnosticEventTypes.AllBytecode);
                            Console.WriteLine("Diagnostics: Bytecode trace listener enabled");
                            break;

                        case "all":
                            diagnostics.Register(new ColdStartDetailListener(), DiagnosticEventTypes.AllColdStartDetail);
                            diagnostics.Register(new MethodTraceListener(), DiagnosticEventTypes.AllMethod);
                            diagnostics.Register(new RuntimeMemoryListener(), DiagnosticEventTypes.AllMemory);
                            diagnostics.Register(new BytecodeTraceListener(), DiagnosticEventTypes.AllBytecode);
                            Console.WriteLine("Diagnostics: All listeners enabled (detail, method, gc, bytecode)");
                            break;

                        default:
                            Console.Error.WriteLine($"Warning: Unknown diagnostic category '{category}'");
                            break;
                    }
                }
                Console.WriteLine();
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
            var heapManager = new SmalltalkHeapManager(diagnostics);

            // Create and run the loader
            Console.WriteLine("Starting cold start loader (6-phase)...");

            var loader = new ColdStartOrchestrator(bundle, heapManager, coldStartFile, diagnostics);
            loader.Run();
            var layoutResolver = loader.LayoutResolver;

            // Write report if requested
            if (reportOutput != null)
            {
                var reportJson = System.Text.Json.JsonSerializer.Serialize(loader.Report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(reportOutput, reportJson);
                Console.WriteLine($"\nColdstart report written to: {reportOutput}");
            }

            Console.WriteLine("Cold start completed successfully!");

            // Create display window immediately after cold start
            Console.WriteLine("\n=== Creating Display Window ===");
            var displayProvider = new WinFormsDisplayProvider(640, 480, "Smalltalk-80 Display");
            Console.WriteLine("Display window created - will show after init expressions complete...");

            // Run GC immediately after cold start to verify heap integrity
            Console.WriteLine("\n=== Running GC after cold start ===");
            var gcStatsBefore = heapManager.GC.GarbageCollect();
            Console.WriteLine($"  {gcStatsBefore.GetSummary()}");

            // Execute init expressions - Run normal init expressions to check for regressions
            bool executeInitExpressions = !skipInitExpressions;
            bool onlyRunTests = false; // DISABLED - Run normal init expressions for debugging

            // ALWAYS execute init expressions (essential ones ALWAYS run, others conditional on --skip-init-expressions)
            if (bundle.InitExpressions.Length > 0)
            {
                Console.WriteLine("\n=== Init Expression Execution ===");
                Console.WriteLine($"  Found {bundle.InitExpressions.Length} init expression(s) in bundle (DISABLED)");

                if (layoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                    return;
                }

                var vmMetadata = BuildVMMetadata(layoutResolver, loader);

                var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata, platformBridge: null, diagnostics);

                // Enable verbose debugging if requested
                vm.VerboseDebug = verboseDebug;

                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();

                // NOTE: Phase 3 already created an initial Process and set it as Processor.activeProcess
                // with suspendedContext = nil and priority = 4 (UserSchedulingPriority).
                // No need to create another Process here - we'll reuse the one from Phase 3.

                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
                    vmMetadata,
                    diagnostics,  // Pass diagnostic hub for GC events
                    InitPolicy.ContinueAndCollect,  // Continue to run all init expressions including tests
                    defaultTimeoutMs: 5000
                );

                // Note: Init expressions are pre-allocated during coldstart Phase 4
                // and stored in VMMetadata.InitExpressionMethods.

                // Phase-based execution strategy:
                // Phase 1: Execute all initialize! expressions (infrastructure setup)
                // Phase 2: Execute all comment: expressions (documentation)
                // Fast-fail: Stop on first error with full diagnostics

                int totalExpressions = bundle.InitExpressions.Length;

                // Essential init expressions that MUST run for basic VM functionality
                // These are required even with --skip-init-expressions flag
                var essentialList = new HashSet<string>
                {
                    "Character initialize",      // Required for CharacterTable (used by String>>at: primitive 63)
                    "SmallInteger initialize",   // Required for SmallInteger constants/utilities
                    "Float initialize",          // Required for Float constants (Pi, E, etc.)
                    "BlockContextTest validate",  // Test suite - runs even with --skip-init-expressions
                };

                // Skip list for init expressions requiring runtime features not available during bootstrap
                var skipList = new HashSet<string>
                {
                    "FormMenuView initialize",               // ❌ Requires file I/O primitives (98, 92, 99, 146) - not yet implemented
                };

                // Build index lists for each phase (preserving source file order)
                var phase1Indices = new List<int>(); // initialize! expressions
                var phase2Indices = new List<int>(); // comment: expressions
                var testIndices = new List<int>(); // test expressions
                var skippedIndices = new List<(int index, string reason)>();

                if (onlyRunTests)
                {
                    Console.WriteLine($"  DEBUG: Test mode enabled, scanning {totalExpressions} expressions...");
                }

                for (int i = 0; i < totalExpressions; i++)
                {
                    var expr = bundle.InitExpressions[i];

                    // TESTING MODE: Only run test classes
                    if (onlyRunTests)
                    {
                        if (expr.Source != null && (expr.Source.Contains("BlockContextTest") || expr.Source.Contains("SimpleBlockTest") || expr.Source.Contains("IfTrueTest") || expr.Source.Contains("PrimitiveTest")))
                        {
                            Console.WriteLine($"  DEBUG: Found test expression [{i}]: {expr.Source}");
                            testIndices.Add(i);
                        }
                        continue;
                    }

                    if (expr.Source != null)
                    {
                        // ALWAYS include essential init expressions (required for basic VM functionality)
                        bool isEssential = essentialList.Contains(expr.Source);

                        // Check skip list first (unless essential)
                        if (!isEssential && skipList.Contains(expr.Source))
                        {
                            skippedIndices.Add((i, "Requires runtime features not available during bootstrap"));
                            continue;
                        }

                        // If --skip-init-expressions flag is set, ONLY run essential init expressions
                        if (skipInitExpressions && !isEssential)
                        {
                            skippedIndices.Add((i, "Skipped by --skip-init-expressions flag (not essential)"));
                            continue;
                        }

                        // Phase-based execution: ALL initialize expressions, then ALL comments, then test expressions
                        if (expr.Source.EndsWith(" initialize"))
                        {
                            phase1Indices.Add(i);
                        }
                        else if (expr.Source.Contains(" comment:"))
                        {
                            phase2Indices.Add(i);
                        }
                        else if (expr.Source.Contains("Test ") || expr.Source.StartsWith("BlockContextTest"))
                        {
                            // Test expressions (e.g., "BlockContextTest validate", "BlockContextTest new testAllCombinations!")
                            testIndices.Add(i);
                        }
                        // Skip other expressions (run! messages) in normal mode
                    }
                }

                // Skip phase reordering when in test mode
                if (!onlyRunTests)
                {
                    // DEPENDENCY-ORDERED EXECUTION
                    // Based on comprehensive dependency analysis (see InitExpression-DependencyAnalysis.md)
                    // 68 total classes: Level 0 (62), Level 1 (5), Level 2 (1)
                    // No circular dependencies detected

                    var dependencyOrder = InitExpressionOrdering.GetDependencyOrder();
                    phase1Indices = InitExpressionOrdering.ReorderByDependencies(phase1Indices, bundle, dependencyOrder);

                    // Move font/GUI-dependent expressions to run last (tests DefaultTextStyle)
                    // Order: Form → TextList → BitEditor (each tests more font functionality)
                    var runLastExpressions = new List<string>
                    {
                        "Form initialize",           // Tests Form creation with glyphs
                        "TextList initialize",       // Tests DefaultTextStyle copy
                        "BitEditor initialize"       // Tests PopUpMenu labels: with font support
                    };

                    var lastIndices = new List<int>();
                    var indicesToRemove = new List<int>();

                    foreach (var exprName in runLastExpressions)
                    {
                        for (int i = 0; i < phase1Indices.Count; i++)
                        {
                            var expr = bundle.InitExpressions[phase1Indices[i]];
                            if (expr.Source == exprName)
                            {
                                lastIndices.Add(phase1Indices[i]);
                                indicesToRemove.Add(i);
                                break;
                            }
                        }
                    }

                    // Remove from current positions (in reverse index order)
                    foreach (var idx in indicesToRemove.OrderByDescending(x => x))
                    {
                        phase1Indices.RemoveAt(idx);
                    }
                    // Add at end in order
                    phase1Indices.AddRange(lastIndices);
                }

                int succeeded = 0;
                int failed = 0;

                // TEST MODE: Only run test expressions
                if (onlyRunTests && testIndices.Count > 0)
                {
                    Console.WriteLine($"\n=== TEST MODE: Running Block Context Tests ===");
                    Console.WriteLine($"  Executing {testIndices.Count} test expression(s)");
                    Console.WriteLine();

                    foreach (int index in testIndices)
                    {
                        var expr = bundle.InitExpressions[index];
                        var initMethodOOP = heapManager.GetIndexedOOP(vmMetadata.InitExpressionMethods, index);

                        Console.WriteLine();
                        Console.WriteLine("******************************************");
                        Console.WriteLine($"[{index}] {expr.Source}");
                        Console.WriteLine("******************************************");
                        var testResult = executor.ExecuteInitExpression(expr.LocalId, initMethodOOP);

                        if (testResult.Status == InitStatus.Success)
                        {
                            Console.WriteLine($"  ✓ Test completed successfully");
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                            Console.WriteLine($"\n❌ TEST FAILED: {expr.Source}");
                            Console.WriteLine($"   Status: {testResult.Status}");
                            if (testResult.Diagnostic?.Message != null)
                            {
                                Console.WriteLine($"   Error: {testResult.Diagnostic.Message}");
                            }
                            // Stack trace suppressed for cleaner output
                            // if (testResult.Diagnostic?.StackTrace != null)
                            // {
                            //     Console.WriteLine($"\n   Stack Trace:");
                            //     Console.WriteLine($"   {testResult.Diagnostic.StackTrace}");
                            // }
                        }
                    }

                    Console.WriteLine($"\n=== Test Execution Complete ===");
                    Console.WriteLine($"  Tests run: {testIndices.Count}");
                    Console.WriteLine($"  Succeeded: {succeeded}");
                    Console.WriteLine($"  Failed: {failed}");
                    return;
                }

                // Execute all initialize! expressions with on-demand compilation and GC verification
                Console.WriteLine($"\n=== Init Expression Execution ===");
                Console.WriteLine($"  Total initialize! expressions: {phase1Indices.Count}");
                Console.WriteLine($"  Executing all expressions (2 skipped)");
                Console.WriteLine();

                succeeded = 0;
                failed = 0;

                if (phase1Indices.Count == 0)
                {
                    Console.WriteLine("  No initialize! expressions found");
                    return;
                }

                // Create InitExpressionCompiler for on-demand compilation (runtime-safe)
                var compiler = new InitExpressionCompiler(heapManager, bundle, vmMetadata);

                // Execute all init expressions
                for (int i = 0; i < phase1Indices.Count; i++)
                {
                    int exprIndex = phase1Indices[i];
                    var expr = bundle.InitExpressions[exprIndex];

                    var result = executor.ExecuteInitExpressionOnDemand(expr, compiler);

                    if (result.Status == InitStatus.Success)
                    {
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                        Console.WriteLine($"    Status: {result.Status}");
                        if (result.Diagnostic != null)
                        {
                            Console.WriteLine($"    Error: {result.Diagnostic.Message}");
                            if (result.Diagnostic.StackTrace != null)
                            {
                                Console.WriteLine($"    Stack Trace: {result.Diagnostic.StackTrace}");
                            }
                        }
                    }
                }

                Console.WriteLine($"\n=== Init Expression Execution Complete ===");
                Console.WriteLine($"  {succeeded} succeeded, {failed} failed");
                return;
            }

            Console.WriteLine("\n✓ All systems ready!");
            Console.WriteLine("Virtual machine initialized successfully.");

            // Show the display window now that init expressions have completed
            Console.WriteLine("\n=== Showing Display Window ===");
            displayProvider.Show();

            // Execute user-specified expression if provided
            if (executeExpression != null)
            {
                Console.WriteLine($"\n=== Executing User Expression ===");
                Console.WriteLine($"  Expression: {executeExpression}");
                Console.WriteLine();

                if (layoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                }
                else
                {
                    // Find the expression in the bundle's init expressions
                    int exprIndex = -1;
                    for (int i = 0; i < bundle.InitExpressions.Length; i++)
                    {
                        var source = bundle.InitExpressions[i].Source;
                        if (source != null && source.Contains(executeExpression))
                        {
                            exprIndex = i;
                            break;
                        }
                    }

                    if (exprIndex == -1)
                    {
                        Console.WriteLine($"  ⚠️  Expression not found in bundle init expressions");
                        Console.WriteLine($"  Searching for: {executeExpression}");
                        Environment.Exit(1);
                        return;
                    }

                    var vmMetadata = BuildVMMetadata(layoutResolver, loader);

                    var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata, platformBridge: null, diagnostics);
                    vm.VerboseDebug = verboseDebug;
                    vm.InitializeSelectorCaches();

                    // NOTE: Phase 3 already created an initial Process and set it as Processor.activeProcess
                    // with suspendedContext = nil and priority = 4 (UserSchedulingPriority).
                    // No need to create another Process here - we'll reuse the one from Phase 3.

                    var executor = new InitExpressionExecutor(
                        heapManager,
                        vm,
                        vmMetadata,
                        diagnostics,  // Pass diagnostic hub for GC events
                        InitPolicy.AbortOnFirstError,
                        defaultTimeoutMs: 10000
                    );

                    var expr = bundle.InitExpressions[exprIndex];
                    var methodOOP = heapManager.GetIndexedOOP(vmMetadata.InitExpressionMethods, exprIndex);

                    Console.WriteLine($"  Found expression at index {exprIndex}: {expr.Source}");
                    Console.WriteLine();

                    var result = executor.ExecuteInitExpression(expr.LocalId, methodOOP);

                    if (result.Status == InitStatus.Success)
                    {
                        Console.WriteLine($"\n✓ Expression executed successfully");
                        if (result.ReturnValueOop.HasValue)
                        {
                            var returnOop = result.ReturnValueOop.Value;
                            if (returnOop.IsSmallInteger)
                            {
                                Console.WriteLine($"   Return value: {returnOop.AsSmallInteger()}");
                            }
                            else
                            {
                                // Try to display as String if it's a String object
                                var className = heapManager.GetClassName(returnOop);
                                if (className == "String")
                                {
                                    try
                                    {
                                        int stringSize = heapManager.GetIndexedSize(returnOop);
                                        var stringBytes = new byte[stringSize];
                                        for (int i = 0; i < stringSize; i++)
                                        {
                                            stringBytes[i] = heapManager.GetIndexedByte(returnOop, i);
                                        }
                                        var stringValue = System.Text.Encoding.UTF8.GetString(stringBytes);
                                        Console.WriteLine($"   Return value: \"{stringValue}\"");
                                    }
                                    catch
                                    {
                                        Console.WriteLine($"   Return value: String (OOP={returnOop.RawValue})");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"   Return value: {className} (OOP={returnOop.RawValue})");
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"\n❌ Expression execution failed");
                        Console.WriteLine($"   Status: {result.Status}");
                        if (result.Diagnostic?.Message != null)
                        {
                            Console.WriteLine($"   Error: {result.Diagnostic?.Message}");
                        }
                        if (verboseDebug && result.Diagnostic?.StackTrace != null)
                        {
                            Console.WriteLine($"   Stack trace:\n{result.Diagnostic?.StackTrace}");
                        }
                        Environment.Exit(1);
                    }
                }
            }

            // TODO: Implement --dump-class after ClassPrettyPrinter is migrated to new heap manager
            if (dumpClassName != null)
            {
                Console.WriteLine($"\n⚠️  --dump-class not yet implemented (ClassPrettyPrinter not migrated to new heap manager)");
            }

            // Run the Windows Forms event loop
            Console.WriteLine("\n=== Entering GUI Event Loop ===");
            Console.WriteLine("Display window is visible. Close window to exit.\n");
            Application.Run(displayProvider);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFatal error: {ex.Message}");
            Console.Error.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Helper method to find a method literal for debugging.
    /// </summary>
    private static SmalltalkOOP FindMethodLiteral(ColdStartOrchestrator loader, ColdStartBundle bundle, string methodLocalId, int literalIndex)
    {
        if (loader.LocalIdToObjectId?.TryGetValue(methodLocalId, out var methodOOP) == true)
        {
            return loader.Allocator.GetIndexedOOP(methodOOP, literalIndex + 1); // +1 to skip header
        }
        return loader.Allocator.GetNil();
    }

    /// <summary>
    /// Resolves a literal value to a SmalltalkOOP (from InitExpressionExecutor).
    /// </summary>
    private static SmalltalkOOP ResolveLiteralValueAsOOP(
        LiteralValue literal,
        ColdStartBundle bundle,
        ObjectGraphAllocator allocator,
        IReadOnlyDictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        return literal.Type switch
        {
            "symbol" => SymbolRegistry.GetSymbolOOP(literal.Index!.Value),
            "string" => allocator.AllocateString(bundle.Strings[literal.Index!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => allocator.AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => allocator.GetNil(),
            "true" => GlobalRegistry.Get("true"),
            "false" => GlobalRegistry.Get("false"),
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

    private static SmalltalkOOP AllocateCharacter(int codePoint, ObjectGraphAllocator allocator)
    {
        var characterOOP = allocator.AllocateFixedObject("Character");
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);
        return characterOOP;
    }

    private static SmalltalkOOP AllocateByteArray(string encoding, string data, ObjectGraphAllocator allocator)
    {
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };
        return allocator.AllocateByteArray("ByteArray", bytes);
    }

    private static SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs, ObjectGraphAllocator allocator)
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
        ObjectGraphAllocator allocator,
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
                    if (assocOOP == allocator.GetNil()) continue;

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
            : allocator.GetNil();

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
    private static string FormatReturnValue(ISmalltalkHeap heapManager, ColdStartBundle bundle, SmalltalkOOP returnOOP)
    {
        if (returnOOP.IsSmallInteger)
            return $"SmallInteger({returnOOP.AsSmallInteger()})";

        if (returnOOP == heapManager.GetNil())
            return "nil";

        if (returnOOP == GlobalRegistry.Get("true"))
            return "true";

        if (returnOOP == GlobalRegistry.Get("false"))
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
        ISmalltalkHeap heapManager,
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
        var disassembly = BytecodeDisassembler.Disassemble(
            bytecodes,
            literalOOPs,
            oop => FormatReturnValue(heapManager, bundle, oop),
            []  // No additional optimized selectors needed
        );
        int instructionCount = disassembly.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Console.WriteLine($"\nBytecode ({bytecodes.Length} bytes, {instructionCount} instructions):");
        Console.Write(disassembly);

        // 5. Stack trace (if available)
        if (result.Diagnostic?.StackTrace != null)
        {
            Console.WriteLine($"\nStack Trace:");
            Console.WriteLine($"  {result.Diagnostic.StackTrace}");
        }

        Console.WriteLine("\n=== END DEBUG OUTPUT ===");
    }

    private static VMMetadata BuildVMMetadata(LayoutResolver layoutResolver, ColdStartOrchestrator loader)
    {
        int thisClassIdx = layoutResolver.GetFieldIndex("class:Metaclass", "thisClass");

        // Get special objects from GlobalRegistry (allocated during cold start phases)
        var nilOOP = loader.Allocator.GetNil();
        var trueOOP = GlobalRegistry.Get("true");
        var falseOOP = GlobalRegistry.Get("false");
        var smalltalkOOP = GlobalRegistry.Get("Smalltalk");
        var processorSchedulerOOP = GlobalRegistry.Get("ProcessorScheduler");

        var knownObjects = new KnownObjects(
            nil: nilOOP,
            @true: trueOOP,
            @false: falseOOP,
            smalltalk: smalltalkOOP
        );

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
            SmallIntegerClassOOP = GlobalRegistry.Get("class:SmallInteger"),
            BlockContextClassOOP = GlobalRegistry.Get("class:BlockContext"),
            KnownObjects = knownObjects,
            ProcessorScheduler = processorSchedulerOOP,
            InitExpressionMethods = loader.InitExpressionMethods
        };
    }

}
