using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Platform.Windows;
using ClassHierarchyUtils = Smalltalk80.Interpreter.Core.ClassHierarchyUtils;
using System.Windows.Forms;

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
            var (coldStartFile, dumpClassName, verboseDebug, testFilter, reportOutput, debugOptions, skipInitExpressions, executeExpression) = ParseCommandLine(args);

            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {coldStartFile}");
                Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
                Console.Error.WriteLine("                         (default: coldstart.json in compiler output directory)");
                Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
                Console.Error.WriteLine("                         after cold start completes");
                Console.Error.WriteLine("  --test-filter <pattern> Run only init expressions matching pattern");
                Console.Error.WriteLine("                         (e.g., 'BlockValidator' or 'Test')");
                Console.Error.WriteLine("  --skip-init-expressions Skip all init expression execution");
                Console.Error.WriteLine("  --execute <expression>  Execute specific Smalltalk expression after cold start");
                Console.Error.WriteLine("                         (e.g., 'BlockContextTest testAllCombinations')");
                Console.Error.WriteLine("  --verbose, -v          Enable verbose debugging (logs all method calls)");
                Console.Error.WriteLine("  --coldstart-report=<path>  Write coldstart metrics to JSON file");
                Console.Error.WriteLine("\nDebug Options:");
                Console.Error.WriteLine("  Cold start diagnostics:");
                Console.Error.WriteLine("    --debug-patches        Show patch application details");
                Console.Error.WriteLine("    --debug-associations   Show association scanning and validation");
                Console.Error.WriteLine("    --debug-classes        Show class resolution details");
                Console.Error.WriteLine("    --debug-methods        Show method installation details");
                Console.Error.WriteLine("    --debug-globals        Show global variable installation details");
                Console.Error.WriteLine("  Runtime VM diagnostics:");
                Console.Error.WriteLine("    --debug-bytecode       Show bytecode execution trace");
                Console.Error.WriteLine("    --debug-messages       Show message send and method lookup trace");
                Console.Error.WriteLine("  Post cold-start diagnostics:");
                Console.Error.WriteLine("    --debug-diagnostics    Show diagnostic reports (ClassDescription, BuildFieldMap)");
                Console.Error.WriteLine("  --debug-all              Enable all debug output");
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
            Console.WriteLine("Starting cold start loader (6-phase)...");

            var loader = new Loader(bundle, heapManager, coldStartFile, debugOptions);
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
            var gcStatsBefore = heapManager.GarbageCollect();
            Console.WriteLine($"  {gcStatsBefore.GetSummary()}");

            // DIAGNOSTIC: Inspect ClassDescription.instanceVariables field (slot 4)
            if (debugOptions.ShouldDebugDiagnostics)
            {
                Console.WriteLine("\n=== DIAGNOSTIC: Checking ClassDescription.instanceVariables ===");
                InspectClassDescriptionInstanceVariables(heapManager, bundle);

                // DIAGNOSTIC: Test RuntimeHeapAccess.BuildFieldMap()
                Console.WriteLine("\n=== DIAGNOSTIC: Testing RuntimeHeapAccess.BuildFieldMap() ===");
                TestBuildFieldMap(heapManager);

                // DIAGNOSTIC: Inspect File class>>initialize method's literal frame
                Console.WriteLine("\n=== DIAGNOSTIC: Checking File class>>initialize Literals AFTER Phase 4 ===");
                InspectFileInitializeLiterals(heapManager, bundle);
            }

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

                var vmMetadata = BuildVMMetadata(layoutResolver);

                // Convert Engine.ColdStartDebugOptions to Core.VM.RuntimeDebugOptions for the VirtualMachine
                var vmDebugOptions = new Smalltalk80.Interpreter.Core.VM.RuntimeDebugOptions
                {
                    ShouldDebugBytecode = debugOptions.ShouldDebugBytecode,
                    ShouldDebugMessages = debugOptions.ShouldDebugMessages,
                    ShouldDebugMethodLookup = debugOptions.ShouldDebugMessages // Method lookup is part of message debugging
                };

                var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata, debugOptions: vmDebugOptions);

                // Enable verbose debugging if requested
                vm.VerboseDebug = verboseDebug;

                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();

                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
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
                    // Known failures - require features not available during bootstrap:
                    "Delay initialize",                      // ❌ Infinite loop - forkAt: creates process with [true] whileTrue
                    "BitEditor initialize",                  // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "FormMenuView initialize",               // ❌ Infinite loop - Form readFrom: (file I/O)
                    "ChangeListController initialize",       // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "FormEditor initialize",                 // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "NotifierController initialize",         // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "ParagraphEditor initialize",            // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "ProjectController initialize",          // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "ScreenController initialize",           // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "StandardSystemController initialize",   // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "StringHolderController initialize",     // ❌ Infinite loop - PopUpMenu labels: (GUI)
                    "Form initialize",                       // ❌ Infinite loop (4M+ instructions, cycling through multiple methods)
                    
                    // All remaining failures documented:
                    "Object initialize",                  // ❌ Method not found: Sensor cursorPoint (GUI required)
                    // "Text initialize",                    // ⚠️ DEBUG - enable to test Association OOP
                    "TextList initialize",                // ❌ Infinite loop (500K+ instructions)
                    "VariableNode initialize",            // ❌ Infinite loop (500K+ instructions)
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
                        else if (expr.Source.Contains("Test "))
                        {
                            // Test expressions (e.g., "BlockContextTest validate")
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

                    var dependencyOrder = GetInitExpressionDependencyOrder();
                    phase1Indices = ReorderByDependencies(phase1Indices, bundle, dependencyOrder);

                    Console.WriteLine($"\n=== Dependency-Ordered Init Expression Execution ===");
                    Console.WriteLine($"  Total: {phase1Indices.Count} initialize expressions");
                    Console.WriteLine($"  Order: SmallInteger → ... → LargeNegativeInteger");
                    Console.WriteLine($"  See: InitExpression-DependencyAnalysis.md");
                    Console.WriteLine();
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
                        var initMethodOOP = heapManager.GetIndexedOOP(initArrayOOP, index);

                        Console.WriteLine();
                        Console.WriteLine("******************************************");
                        Console.WriteLine($"[{index}] {expr.Source}");
                        Console.WriteLine("******************************************");
                        var result = executor.ExecuteInitExpression(expr.LocalId, initMethodOOP);

                        if (result.Status == InitStatus.Success)
                        {
                            Console.WriteLine($"  ✓ Test completed successfully");
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                            Console.WriteLine($"\n❌ TEST FAILED: {expr.Source}");
                            Console.WriteLine($"   Status: {result.Status}");
                            if (result.Diagnostic?.Message != null)
                            {
                                Console.WriteLine($"   Error: {result.Diagnostic.Message}");
                            }
                            // Stack trace suppressed for cleaner output
                            // if (result.Diagnostic?.StackTrace != null)
                            // {
                            //     Console.WriteLine($"\n   Stack Trace:");
                            //     Console.WriteLine($"   {result.Diagnostic.StackTrace}");
                            // }
                        }
                    }

                    Console.WriteLine($"\n=== Test Execution Complete ===");
                    Console.WriteLine($"  Tests run: {testIndices.Count}");
                    Console.WriteLine($"  Succeeded: {succeeded}");
                    Console.WriteLine($"  Failed: {failed}");
                    return;
                }

                // Execute initialize! expressions in dependency order
                Console.WriteLine($"\n=== Executing Initialize Expressions (Dependency Order) ===");
                Console.WriteLine($"  Total initialize! expressions: {phase1Indices.Count}");
                Console.WriteLine($"  Execution order determined by class dependencies");
                Console.WriteLine();

                // Use dependency-ordered phase1 indices
                var reorderedIndices = new List<int>();
                reorderedIndices.AddRange(phase1Indices);

                succeeded = 0;
                failed = 0;
                var successfulExpressions = new List<(int index, string source)>();

                // Execute with per-expression status output
                foreach (int index in reorderedIndices)
                {
                    var expr = bundle.InitExpressions[index];
                    var initMethodOOP = heapManager.GetIndexedOOP(initArrayOOP, index);

                    // Show what we're executing
                    Console.Write($"  [{index}] {expr.Source}... ");

                    // Reset instruction count before each expression
                    vm.ResetInstructionCount();
                    var result = executor.ExecuteInitExpression(expr.LocalId, initMethodOOP);
                    long instructionCount = vm.InstructionCount;

                    if (result.Status == InitStatus.Success)
                    {
                        succeeded++;
                        successfulExpressions.Add((index, expr.Source ?? ""));
                        
                        // For Block test classes, show the actual return value
                        if (expr.Source != null && (expr.Source.Contains("Block") && expr.Source.Contains("Test")))
                        {
                            var returnOOP = result.ReturnValueOop;
                            if (returnOOP.HasValue && returnOOP.Value.IsSmallInteger)
                            {
                                int value = returnOOP.Value.AsSmallInteger();
                                Console.WriteLine($"{instructionCount:N0} instructions  ✓ SUCCESS - Returned: {value}");
                            }
                            else
                            {
                                Console.WriteLine($"{instructionCount:N0} instructions  ✓ SUCCESS - Returned: {returnOOP}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{instructionCount:N0} instructions  ✓ SUCCESS");
                        }
                    }
                    else
                    {
                        // FAIL FAST: Stop on first failure with full diagnostics
                        failed++;
                        Console.WriteLine($"{instructionCount:N0} instructions  ❌ FAILED");
                        Console.WriteLine();
                        Console.WriteLine("==========================================");
                        Console.WriteLine($"❌ FIRST FAILURE: [{index}] {expr.Source}");
                        Console.WriteLine("==========================================");
                        Console.WriteLine($"Status: {result.Status}");
                        if (result.Diagnostic?.Message != null)
                        {
                            Console.WriteLine($"\nError: {result.Diagnostic.Message}");
                        }
                        if (result.Diagnostic?.StackTrace != null)
                        {
                            Console.WriteLine($"\nStack Trace:");
                            Console.WriteLine(result.Diagnostic.StackTrace);
                        }

                        Console.WriteLine($"\n✓ Successfully executed {succeeded} expression(s) before this failure");
                        if (successfulExpressions.Count > 0)
                        {
                            Console.WriteLine($"\nSuccessful expressions:");
                            foreach (var (idx, source) in successfulExpressions)
                            {
                                Console.WriteLine($"  [{idx}] {source}");
                            }
                        }

                        // FAIL FAST - stop execution here
                        Console.WriteLine($"\n⚠️  Execution stopped at first failure to enable debugging");
                        Console.WriteLine($"⚠️  Enable VM debug output and re-run to diagnose this specific failure");
                        return;
                    }
                }

                Console.WriteLine($"\n=== Init Expression Execution Complete ===");
                Console.WriteLine($"  Total attempted: {reorderedIndices.Count} initialize! expressions");
                Console.WriteLine($"  Succeeded: {succeeded}");
                Console.WriteLine($"  Failed: {failed}");
                Console.WriteLine($"  Skipped: {skippedIndices.Count} (require runtime features)");
                Console.WriteLine($"  Comment expressions: {phase2Indices.Count} (not executed)");

                // Run GC after init expressions to collect temporary objects
                Console.WriteLine($"\n=== Running GC after init expressions ===");
                var gcStatsAfter = heapManager.GarbageCollect();
                Console.WriteLine($"  {gcStatsAfter.GetSummary()}");
                if (gcStatsAfter.CollectedObjects > 0)
                {
                    Console.WriteLine($"  {gcStatsAfter.GetFormatBreakdown()}");
                }

                // Note: Test expressions already executed as init expressions above
                // No need to re-run them here

                // Continue to GUI display window (don't exit after init expressions)

                #if false  // Phase 2 disabled - unreachable code preserved for reference
                // Phase 2: Execute all comment: expressions (DISABLED - not running)
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
                        // Stack trace suppressed for cleaner output
                        // if (result.Diagnostic?.StackTrace != null)
                        // {
                        //     Console.WriteLine($"\n   Stack Trace:");
                        //     Console.WriteLine($"   {result.Diagnostic.StackTrace}");
                        // }

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
                if (skippedIndices.Count > 0)
                {
                    Console.WriteLine($"  Skipped: {skippedIndices.Count} (require runtime features not available during bootstrap)");
                    foreach (var (index, reason) in skippedIndices)
                    {
                        Console.WriteLine($"    [{index}] {bundle.InitExpressions[index].Source}");
                    }
                }
                Console.WriteLine($"  Total: {succeeded}/{totalExpressions} succeeded, {failed} failed");
                Console.WriteLine($"\n✓ All init expressions executed successfully!");
                #endif  // End of disabled Phase 2 code
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

                    var vmMetadata = BuildVMMetadata(layoutResolver);

                    // Convert Engine.ColdStartDebugOptions to Core.VM.RuntimeDebugOptions for the VirtualMachine
                    var vmDebugOptions = new Smalltalk80.Interpreter.Core.VM.RuntimeDebugOptions
                    {
                        ShouldDebugBytecode = debugOptions.ShouldDebugBytecode,
                        ShouldDebugMessages = debugOptions.ShouldDebugMessages,
                        ShouldDebugMethodLookup = debugOptions.ShouldDebugMessages
                    };

                    var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata, debugOptions: vmDebugOptions);
                    vm.VerboseDebug = verboseDebug;
                    vm.InitializeSelectorCaches();

                    var executor = new InitExpressionExecutor(
                        heapManager,
                        vm,
                        InitPolicy.AbortOnFirstError,
                        defaultTimeoutMs: 10000
                    );

                    var expr = bundle.InitExpressions[exprIndex];
                    var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
                    var methodOOP = heapManager.GetIndexedOOP(initArrayOOP, exprIndex);

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
                            Console.WriteLine($"   Error: {result.Diagnostic.Message}");
                        }
                        if (verboseDebug && result.Diagnostic?.StackTrace != null)
                        {
                            Console.WriteLine($"   Stack trace:\n{result.Diagnostic.StackTrace}");
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
        return SpecialObjectsArray.Instance.Nil;
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

    /// <summary>
    /// Parses command line arguments and returns the cold start file path and optional class name to dump.
    /// Supports: --coldstart path, --dump-class ClassName, --verbose, --skip-init-expressions, etc.
    /// Default: coldstart.json in the compiler output directory (../net9.0/coldstart.json)
    /// </summary>
    private static (string coldStartFile, string? dumpClassName, bool verboseDebug, string? testFilter, string? reportOutput, Smalltalk80.Interpreter.Engine.ColdStartDebugOptions debugOptions, bool skipInitExpressions, string? executeExpression) ParseCommandLine(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;
        bool verboseDebug = false;
        string? testFilter = null;
        string? reportOutput = null;
        bool skipInitExpressions = false;
        string? executeExpression = null;
        var debugOptions = new Smalltalk80.Interpreter.Engine.ColdStartDebugOptions();

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
            else if (args[i] == "--skip-init-expressions")
            {
                skipInitExpressions = true;
            }
            else if (args[i] == "--execute" && i + 1 < args.Length)
            {
                executeExpression = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i] == "--verbose" || args[i] == "-v")
            {
                verboseDebug = true;
            }
            else if (args[i].StartsWith("--coldstart-report="))
            {
                reportOutput = args[i].Substring("--coldstart-report=".Length);
            }
            else if (args[i] == "--debug-all")
            {
                debugOptions.DebugAll = true;
            }
            else if (args[i] == "--debug-patches")
            {
                debugOptions.DebugPatches = true;
            }
            else if (args[i] == "--debug-associations")
            {
                debugOptions.DebugAssociations = true;
            }
            else if (args[i] == "--debug-classes")
            {
                debugOptions.DebugClasses = true;
            }
            else if (args[i] == "--debug-methods")
            {
                debugOptions.DebugMethods = true;
            }
            else if (args[i] == "--debug-globals")
            {
                debugOptions.DebugGlobals = true;
            }
            else if (args[i] == "--debug-bytecode")
            {
                debugOptions.DebugBytecode = true;
            }
            else if (args[i] == "--debug-messages")
            {
                debugOptions.DebugMessages = true;
            }
            else if (args[i] == "--debug-diagnostics")
            {
                debugOptions.DebugDiagnostics = true;
            }
        }

        // Default: coldstart.json in the compiler's output directory
        // Directory.Build.props sets BaseOutputPath to solution-level bin/
        // Compiler outputs to: Smalltalk-80/bin/Debug/net9.0/coldstart.json
        // Interpreter outputs to: Smalltalk-80/bin/Debug/net9.0-windows/
        if (coldStartFile == null)
        {
            string exeDir = AppContext.BaseDirectory;
            // exeDir = .../Smalltalk-80/bin/Debug/net9.0-windows/

            // Try compiler's output directory first (sibling directory)
            // AppContext.BaseDirectory includes trailing slash, so we need to go up to parent
            // Use Path.Combine with ".." to navigate to parent directory
            string debugDir = Path.GetFullPath(Path.Combine(exeDir, "..")); // .../bin/Debug/
            string compilerDir = Path.Combine(debugDir, "net9.0"); // .../bin/Debug/net9.0/
            string compilerColdStart = Path.Combine(compilerDir, "coldstart.json");

            if (File.Exists(compilerColdStart))
            {
                coldStartFile = compilerColdStart;
            }
            else
            {
                // Fallback to executable directory
                coldStartFile = Path.Combine(exeDir, "coldstart.json");
            }
        }

        return (coldStartFile, dumpClassName, verboseDebug, testFilter, reportOutput, debugOptions, skipInitExpressions, executeExpression);
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
            SmallIntegerClassOOP = GlobalRegistry.Get("class:SmallInteger"),
            BlockContextClassOOP = GlobalRegistry.Get("class:BlockContext")
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

    /// <summary>
    /// DIAGNOSTIC: Inspects ClassDescription.instanceVariables (slot 4) for several classes
    /// to determine if instance variable names are stored in the heap.
    /// </summary>
    private static void InspectClassDescriptionInstanceVariables(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            // Test classes: Array, Point, Process (various inheritance depths)
            string[] testClasses = { "Array", "Point", "Process", "Dictionary" };
            var nilOOP = SpecialObjectsArray.Instance.Nil;

            foreach (var className in testClasses)
            {
                Console.WriteLine($"\n{className}:");

                // Look up the class OOP
                if (!GlobalRegistry.TryGet($"class:{className}", out SmalltalkOOP classOOP))
                {
                    Console.WriteLine($"  ⚠️  Class not found in GlobalRegistry");
                    continue;
                }

                // ClassDescription has: 'instanceVariables' at slot 4 (inherited from ClassDescription)
                // Behavior: superclass(0), methodDict(1), format(2)
                // ClassDescription adds: instanceVariables(3), organization(4)
                // CORRECTION: Behavior has 3 fields, ClassDescription adds 2 more
                // So instanceVariables should be at slot 3 (0-based indexing)

                // Let's check both slot 3 and slot 4 to be sure
                for (int slotIndex = 3; slotIndex <= 4; slotIndex++)
                {
                    var fieldValueOOP = heapManager.GetInstanceVariable(classOOP, slotIndex);

                    Console.WriteLine($"  Slot {slotIndex}: {FormatOOPValue(heapManager, fieldValueOOP, nilOOP)}");

                    // If it's an Array, inspect its contents
                    if (fieldValueOOP != nilOOP && !fieldValueOOP.IsSmallInteger)
                    {
                        try
                        {
                            int arraySize = heapManager.GetIndexedSize(fieldValueOOP);
                            Console.WriteLine($"    Array size: {arraySize}");
                            if (arraySize > 0 && arraySize < 50) // Reasonable size check
                            {
                                for (int i = 0; i < Math.Min(arraySize, 10); i++) // Show first 10 elements
                                {
                                    var elementOOP = heapManager.GetIndexedOOP(fieldValueOOP, i);
                                    string elementStr = FormatOOPValue(heapManager, elementOOP, nilOOP);

                                    // If it's a String, try to read it
                                    if (elementOOP != nilOOP && !elementOOP.IsSmallInteger)
                                    {
                                        try
                                        {
                                            int strLen = heapManager.GetIndexedSize(elementOOP);
                                            if (strLen > 0 && strLen < 100)
                                            {
                                                var bytes = new byte[strLen];
                                                for (int b = 0; b < strLen; b++)
                                                {
                                                    bytes[b] = heapManager.GetIndexedByte(elementOOP, b);
                                                }
                                                string text = System.Text.Encoding.Latin1.GetString(bytes);
                                                elementStr = $"\"{text}\"";
                                            }
                                        }
                                        catch
                                        {
                                            // Not a String, keep generic format
                                        }
                                    }

                                    Console.WriteLine($"      [{i}] = {elementStr}");
                                }
                                if (arraySize > 10)
                                {
                                    Console.WriteLine($"      ... ({arraySize - 10} more elements)");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    ⚠️  Error inspecting array: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error during diagnostic: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Formats an OOP value for display in diagnostics.
    /// </summary>
    private static string FormatOOPValue(ISmalltalkHeapManager heapManager, SmalltalkOOP oop, SmalltalkOOP nilOOP)
    {
        if (oop == nilOOP)
            return "nil";
        if (oop.IsSmallInteger)
            return $"SmallInteger({oop.AsSmallInteger()})";

        // Try to get object class for better display
        try
        {
            var classOOP = heapManager.GetSlot(oop, 0);
            if (classOOP != nilOOP && !classOOP.IsSmallInteger)
            {
                // STRATEGY 1: Check if THIS object is a Class object itself
                // Class objects have their name at slot 6 (field: name)
                // Try to read slot 6 of THIS object first
                var nameOOP = heapManager.GetInstanceVariable(oop, 6);
                if (nameOOP != nilOOP && !nameOOP.IsSmallInteger)
                {
                    int nameLen = heapManager.GetIndexedSize(nameOOP);
                    if (nameLen > 0 && nameLen < 100)
                    {
                        var bytes = new byte[nameLen];
                        for (int i = 0; i < nameLen; i++)
                        {
                            bytes[i] = heapManager.GetIndexedByte(nameOOP, i);
                        }
                        string className = System.Text.Encoding.Latin1.GetString(bytes);
                        return $"{className}(oop={oop.RawValue})";
                    }
                }

                // STRATEGY 2: Get class name from the class object's name field
                // This works for instances of classes
                var classNameOOP = heapManager.GetInstanceVariable(classOOP, 6);
                if (classNameOOP != nilOOP && !classNameOOP.IsSmallInteger)
                {
                    int nameLen = heapManager.GetIndexedSize(classNameOOP);
                    if (nameLen > 0 && nameLen < 100)
                    {
                        var bytes = new byte[nameLen];
                        for (int i = 0; i < nameLen; i++)
                        {
                            bytes[i] = heapManager.GetIndexedByte(classNameOOP, i);
                        }
                        string className = System.Text.Encoding.Latin1.GetString(bytes);
                        return $"{className}(oop={oop.RawValue})";
                    }
                }
            }
        }
        catch
        {
            // Fall through to generic display
        }

        return $"OOP({oop.RawValue})";
    }

    /// <summary>
    /// DIAGNOSTIC: Tests RuntimeHeapAccess.BuildFieldMap() to verify it can read
    /// instance variable names from the heap and build complete field layouts.
    /// </summary>
    private static void TestBuildFieldMap(ISmalltalkHeapManager heapManager)
    {
        try
        {
            var runtimeHeapAccess = new RuntimeHeapAccess(heapManager);
            Console.WriteLine("Building field map from heap...");

            var startTime = System.Diagnostics.Stopwatch.StartNew();
            var fieldMap = runtimeHeapAccess.BuildFieldMap();
            startTime.Stop();

            Console.WriteLine($"Built field map in {startTime.ElapsedMilliseconds}ms");
            Console.WriteLine($"Found {fieldMap.Count} classes");
            Console.WriteLine();

            // Test specific classes
            string[] testClasses = { "class:Array", "class:Point", "class:Process", "class:Dictionary", "class:MethodContext" };

            foreach (var className in testClasses)
            {
                if (fieldMap.TryGetValue(className, out var fields))
                {
                    Console.WriteLine($"{className}:");
                    if (fields.Count == 0)
                    {
                        Console.WriteLine("  (no instance variables)");
                    }
                    else
                    {
                        for (int i = 0; i < fields.Count; i++)
                        {
                            Console.WriteLine($"  [{i}] {fields[i]}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{className}: NOT FOUND");
                }
            }

            Console.WriteLine($"\n✓ BuildFieldMap() successfully read {fieldMap.Count} class layouts from heap");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ BuildFieldMap() failed: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// DIAGNOSTIC: Inspects the File class>>initialize method's literal frame AFTER Phase 4 completion.
    /// This checks what literals are actually stored in the CompiledMethod at runtime.
    /// </summary>
    private static void InspectFileInitializeLiterals(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            Console.WriteLine("\nSearching for File class>>initialize init expression...");

            // Find the File initialize init expression
            int fileInitIndex = -1;
            for (int i = 0; i < bundle.InitExpressions.Length; i++)
            {
                if (bundle.InitExpressions[i].Source == "File initialize")
                {
                    fileInitIndex = i;
                    break;
                }
            }

            if (fileInitIndex == -1)
            {
                Console.WriteLine("  File initialize init expression not found in bundle");
                return;
            }

            Console.WriteLine($"  Found at index {fileInitIndex}");

            // Get the CompiledMethod OOP from the InitExpressionMethods array
            var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
            if (initArrayOOP == SpecialObjectsArray.Instance.Nil)
            {
                Console.WriteLine("  InitExpressionMethods array is nil");
                return;
            }

            var methodOOP = heapManager.GetIndexedOOP(initArrayOOP, fileInitIndex);
            Console.WriteLine($"  Method OOP: {methodOOP}");

            // Get the CompiledMethod's indexed size (header + literals + bytecode array)
            int indexedSize = heapManager.GetIndexedSize(methodOOP);
            Console.WriteLine($"  Indexed size: {indexedSize}");

            // Literals start at indexed slot 1 (slot 0 is header)
            // Last indexed slot is the bytecode array
            int literalCount = indexedSize - 2; // Subtract header and bytecode array
            Console.WriteLine($"  Literal count: {literalCount}");

            Console.WriteLine("\n  Literals:");
            for (int i = 0; i < literalCount; i++)
            {
                int slotIndex = i + 1; // +1 to skip header
                var literalOOP = heapManager.GetIndexedOOP(methodOOP, slotIndex);

                Console.WriteLine($"    [{i}] OOP={literalOOP}");

                try
                {
                    // Check if it's a SmallInteger
                    if (literalOOP.IsSmallInteger)
                    {
                        Console.WriteLine($"        SmallInteger: {literalOOP.AsSmallInteger()}");
                        continue;
                    }

                    // Check special values
                    if (literalOOP == SpecialObjectsArray.Instance.Nil)
                    {
                        Console.WriteLine($"        nil");
                        continue;
                    }
                    if (literalOOP == SpecialObjectsArray.Instance.True)
                    {
                        Console.WriteLine($"        true");
                        continue;
                    }
                    if (literalOOP == SpecialObjectsArray.Instance.False)
                    {
                        Console.WriteLine($"        false");
                        continue;
                    }

                    // Get object format
                    var format = heapManager.GetObjectFormat(literalOOP);
                    Console.WriteLine($"        Format: {format}");

                    // Get class OOP
                    var classOOP = heapManager.GetSlot(literalOOP, 0);
                    Console.WriteLine($"        Class OOP: {classOOP}");

                    if (format == ObjectFormat.Pointer)
                    {
                        // Check if it's an Association (2 instance variables)
                        try
                        {
                            var keyOOP = heapManager.GetInstanceVariable(literalOOP, 0);
                            var valueOOP = heapManager.GetInstanceVariable(literalOOP, 1);

                            Console.WriteLine($"        Pointer object with 2 fields:");
                            Console.WriteLine($"          Field[0] (key?): {keyOOP}");
                            Console.WriteLine($"          Field[1] (value?): {valueOOP}");

                            // Try to read key as Symbol
                            if (!keyOOP.IsSmallInteger && keyOOP != SpecialObjectsArray.Instance.Nil)
                            {
                                try
                                {
                                    var keyFormat = heapManager.GetObjectFormat(keyOOP);
                                    if (keyFormat == ObjectFormat.ByteIndexed)
                                    {
                                        int keyLen = heapManager.GetIndexedSize(keyOOP);
                                        if (keyLen > 0 && keyLen < 100)
                                        {
                                            var keyBytes = new byte[keyLen];
                                            for (int b = 0; b < keyLen; b++)
                                            {
                                                keyBytes[b] = heapManager.GetIndexedByte(keyOOP, b);
                                            }
                                            string keyName = System.Text.Encoding.Latin1.GetString(keyBytes);
                                            Console.WriteLine($"          Key is Symbol: '{keyName}'");
                                        }
                                    }
                                }
                                catch { }
                            }

                            // Check if value is SmallInteger
                            if (valueOOP.IsSmallInteger)
                            {
                                Console.WriteLine($"          Value is SmallInteger: {valueOOP.AsSmallInteger()}");
                            }
                            else if (valueOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                Console.WriteLine($"          Value is nil");
                            }
                            else
                            {
                                Console.WriteLine($"          Value is object: {valueOOP}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"        ERROR reading as Association: {ex.Message}");
                        }
                    }
                    else if (format == ObjectFormat.ByteIndexed)
                    {
                        // Probably a Symbol
                        try
                        {
                            int len = heapManager.GetIndexedSize(literalOOP);
                            if (len > 0 && len < 100)
                            {
                                var bytes = new byte[len];
                                for (int b = 0; b < len; b++)
                                {
                                    bytes[b] = heapManager.GetIndexedByte(literalOOP, b);
                                }
                                string name = System.Text.Encoding.Latin1.GetString(bytes);
                                Console.WriteLine($"        ByteIndexed (Symbol?): '{name}'");
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"        ERROR inspecting literal: {ex.Message}");
                }
            }

            Console.WriteLine("\n✓ File initialize literal inspection complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error inspecting File initialize literals: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Returns the complete dependency-ordered list of class names for init expression execution.
    /// Based on InitExpression-DependencyAnalysis.md topological sort (3 levels, no circular deps).
    /// </summary>
    private static List<string> GetInitExpressionDependencyOrder()
    {
        return new List<string>
        {
            // LEVEL 0: 81 classes with no dependencies (alphabetically sorted within level)
            // ESSENTIAL CLASSES FIRST (required for basic VM functionality)
            "Character class",        // ESSENTIAL: Required for CharacterTable (String>>at: primitive)
            "Float class",            // ESSENTIAL: Required for Float constants (Pi, E, etc.)
            "SmallInteger class",     // ESSENTIAL: Required for SmallInteger constants/utilities (moved to front)

            // Other Level 0 classes
            "Benchmark class",
            "BinaryChoiceView class",
            "BlockContextTest class",
            "BrowserView class",
            "Change class",
            "ChangesView class",
            "Class class",
            "ClassHierarchyBrowserView class",
            "ClassOrganizer class",
            "CompiledMethod class",
            "ControlManager class",
            "Cursor class",
            "Date class",
            "DebuggerView class",
            "Decompiler class",
            "DigitBuffer class",
            "DiskBrowserView class",
            "DisplayScreen class",
            "DisplayTextView class",
            "Encoder class",
            "Explainer class",
            "File class",
            "FileDirectory class",
            "FileList class",
            "Form class",
            "FormButtonView class",
            "FormEditor class",
            "FormView class",
            "InputSensor class",       // REQUIRED BEFORE Object: Object>>confirm: uses Sensor cursorPoint
            "InputState class",
            "Inspector class",
            "InspectorView class",
            "InstructionStream class",
            "Lexicon class",
            "ListController class",
            "ListView class",
            "LockedTextView class",
            "LowSpaceView class",
            "Magnitude class",
            "MenuView class",
            "MessageNode class",
            "MethodNode class",
            "MixedInspectorView class",
            "NameSetInspectorView class",
            "NoControllerView class",
            "Notifier class",
            "NotifierView class",
            "Object class",
            "OccupationView class",
            "OrderedCollection class",
            "Paragraph class",
            "ParseNode class",
            "Path class",
            "Point class",
            "ProcessorScheduler class",
            "Project class",
            "Rectangle class",
            "Scanner class",
            "SourceFileManager class",
            "SplinesView class",
            "StandardSystemView class",
            "StringHolder class",
            "StringHolderView class",
            "Symbol class",
            "SymbolAsStringTest class",
            "SystemDictionary class",
            "SystemTracer class",
            "SystemView class",
            "SystemWorkspace class",
            "Text class",
            "TextCollectorView class",
            "TextEditor class",
            "TextList class",
            "TileableView class",
            "UndefinedObject class",
            "WindowsFileDirectory class",
            "WindowsFileSystem class",
            "WorkspaceView class",

            // LEVEL 1: 5 classes that depend on Level 0
            "BitEditor class",      // depends on: OrderedCollection, Point
            "Debugger class",       // depends on: NotifierView, Project
            "FormMenuView class",   // depends on: DisplayScreen
            "LargePositiveInteger class",  // depends on: DigitBuffer
            "Spline class",         // depends on: Point

            // LEVEL 2: 1 class that depends on Level 1
            "LargeNegativeInteger class"   // depends on: LargePositiveInteger
        };
    }

    /// <summary>
    /// Reorders init expression indices according to dependency order.
    /// Maps class names from bundle to desired execution order, preserving any not in dependency list.
    /// Within each dependency level, sorts by bytecode length (shortest first).
    /// </summary>
    private static List<int> ReorderByDependencies(
        List<int> originalIndices,
        ColdStartBundle bundle,
        List<string> dependencyOrder)
    {
        var result = new List<int>();
        var remainingIndices = new HashSet<int>(originalIndices);

        // Group classes by dependency level
        var levels = new List<List<string>>();
        var currentLevel = new List<string>();

        foreach (var className in dependencyOrder)
        {
            // Check if this is a level marker comment (these are NOT in the actual list)
            // The dependency order list contains only class names, not comments
            // So we detect level boundaries by the structure of the list itself
            currentLevel.Add(className);
        }

        // Since the dependency order list doesn't contain comment markers,
        // we need to manually split based on known level sizes from the comments:
        // LEVEL 0: 81 classes (indices 0-80)
        // LEVEL 1: 5 classes (indices 81-85)
        // LEVEL 2: 1 class (index 86)
        levels.Add(dependencyOrder.Take(81).ToList());      // Level 0
        levels.Add(dependencyOrder.Skip(81).Take(5).ToList()); // Level 1
        levels.Add(dependencyOrder.Skip(86).ToList());      // Level 2

        // Process each level
        foreach (var levelClasses in levels)
        {
            // Find indices for all classes in this level
            var levelIndices = new List<(int index, int bytecodeLength)>();

            foreach (var className in levelClasses)
            {
                var bareClassName = className.Replace(" class", "");
                var expectedSource = $"{bareClassName} initialize";

                for (int i = 0; i < bundle.InitExpressions.Length; i++)
                {
                    if (!remainingIndices.Contains(i))
                        continue;

                    var initExpr = bundle.InitExpressions[i];
                    if (initExpr.Source == expectedSource)
                    {
                        // Get bytecode length - handle both int[] and base64 string formats
                        int bytecodeLength = 0;
                        if (initExpr.Bytecodes is int[] intArray)
                        {
                            bytecodeLength = intArray.Length;
                        }
                        else if (initExpr.Bytecodes is string base64String)
                        {
                            bytecodeLength = Convert.FromBase64String(base64String).Length;
                        }

                        levelIndices.Add((i, bytecodeLength));
                        remainingIndices.Remove(i);
                        break;
                    }
                }
            }

            // Sort this level by bytecode length (shortest first)
            var sortedLevel = levelIndices.OrderBy(x => x.bytecodeLength).ToList();
            result.AddRange(sortedLevel.Select(x => x.index));
        }

        // Add any remaining indices that weren't in dependency order (at the end)
        // These are init expressions that don't match our dependency analysis
        if (remainingIndices.Count > 0)
        {
            Console.WriteLine($"  ⚠️  {remainingIndices.Count} init expressions not in dependency order (appending at end)");
            result.AddRange(remainingIndices.OrderBy(i => i));
        }

        return result;
    }
}
