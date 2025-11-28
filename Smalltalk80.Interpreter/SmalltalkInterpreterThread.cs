using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Utilities;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Manages the Smalltalk interpreter thread.
/// This class encapsulates all Smalltalk execution logic that runs on a separate thread
/// from the Windows UI thread.
/// </summary>
public class SmalltalkInterpreterThread
{
    private readonly SmalltalkThreadContext _context;
    private VirtualMachine? _vm;

    public SmalltalkInterpreterThread(SmalltalkThreadContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Main entry point for Smalltalk thread.
    /// This method runs on the Smalltalk thread (not main UI thread).
    /// </summary>
    public void Run()
    {
        try
        {
            Console.WriteLine("\n[Smalltalk Thread] Starting...");

            // TESTING: Wait for UI thread to show window before executing init expressions
            // This allows us to observe the window resize when DisplayScreen initialize runs
            Console.WriteLine("[Smalltalk Thread] Waiting for window to be displayed...");
            Thread.Sleep(1000);  // Wait 1 second for window to be fully shown

            // STEP 1: Create VM
            Console.WriteLine("[Smalltalk Thread] Creating virtual machine...");
            _vm = CreateVirtualMachine();

            // STEP 2: Execute init expressions
            Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
            ExecuteInitExpressions();

            // STEP 3: Execute user expression if requested
            if (_context.Config.ExecuteExpression != null)
            {
                Console.WriteLine("[Smalltalk Thread] Executing user expression...");
                ExecuteUserExpression();
            }

            // Future: STEP 4 - Start controller loop
            // Console.WriteLine("[Smalltalk Thread] Starting controller loop...");
            // StartControllerLoop();

            Console.WriteLine("\n[Smalltalk Thread] Completed successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Smalltalk Thread] Fatal error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    private VirtualMachine CreateVirtualMachine()
    {
        var vm = new VirtualMachine(
            _context.HeapManager,
            _context.LayoutResolver,
            _context.VMMetadata,
            platformBridge: _context.PlatformBridge,
            _context.Diagnostics
        );

        vm.VerboseDebug = _context.Config.VerboseDebug;
        vm.InitializeSelectorCaches();

        return vm;
    }

    private void ExecuteInitExpressions()
    {
        if (_context.Bundle.InitExpressions.Length == 0)
        {
            Console.WriteLine("  No init expressions found");
            return;
        }

        Console.WriteLine("\n=== Init Expression Execution ===");

        var executor = new InitExpressionExecutor(
            _context.HeapManager,
            _vm!,
            _context.VMMetadata,
            _context.Diagnostics,
            InitPolicy.ContinueAndCollect,
            defaultTimeoutMs: 5000
        );

        // Essential init expressions (always run, even with --skip-init-expressions)
        var essentialList = new HashSet<string>
        {
            "Character initialize",
            "SmallInteger initialize",
            "Float initialize",
            "InputSensor initialize",
            "InputSensor install",
            "InputSensor initSensor",
            "BlockContextTest validate",
        };

        // Skip list (requires features not yet implemented)
        var skipList = new HashSet<string>
        {
            // FormMenuView initialize now enabled - file I/O primitives implemented
        };

        // Build phase lists using explicit ordering from InitExpressionOrdering
        var phase1Indices = new List<int>();
        var skippedIndices = new List<(int index, string reason)>();

        // Build source-to-index map for the bundle
        var sourceToIndex = new Dictionary<string, int>();
        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var source = _context.Bundle.InitExpressions[i].Source;
            if (source != null)
            {
                sourceToIndex[source] = i;
            }
        }

        // Add init expressions in the explicit order defined in InitExpressionOrdering
        foreach (var source in InitExpressionOrdering.GetExecutionOrder())
        {
            if (sourceToIndex.TryGetValue(source, out int idx))
            {
                // Check skip list
                if (skipList.Contains(source) && !essentialList.Contains(source))
                {
                    skippedIndices.Add((idx, "Requires features not yet available"));
                }
                else
                {
                    phase1Indices.Add(idx);
                }
            }
        }

        // Build comment expression list: ClassName comment: 'text' expressions
        var commentIndices = new List<int>();
        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var expr = _context.Bundle.InitExpressions[i];
            if (expr.Source == null) continue;

            // Include comment: expressions (e.g., "ClassName comment:'text'")
            if (expr.Source.Contains(" comment:"))
            {
                commentIndices.Add(i);
            }
        }

        // Build phase 2 list: Test expressions (run AFTER init expressions)
        var phase2Indices = new List<int>();
        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var expr = _context.Bundle.InitExpressions[i];
            if (expr.Source == null) continue;

            // Include GraphicsTest method calls (FloatTest removed - no longer needed)
            if (expr.Source.StartsWith("GraphicsTest "))
            {
                phase2Indices.Add(i);
            }
        }

        // Execute all selected expressions
        var compiler = new InitExpressionCompiler(
            _context.HeapManager, _context.Bundle, _context.VMMetadata);

        int succeeded = 0;
        int failed = 0;

        // PHASE 1: Execute init expressions
        Console.WriteLine("\n--- Phase 1: Initialize expressions ---");
        foreach (int index in phase1Indices)
        {
            var expr = _context.Bundle.InitExpressions[index];
            var result = executor.ExecuteInitExpressionOnDemand(expr, compiler);

            if (result.Status == InitStatus.Success)
            {
                succeeded++;

                // Display return value for test runners
                if (expr.Source != null && expr.Source.Contains("TestRunner") && result.ReturnValueOop?.IsSmallInteger == true)
                {
                    var returnOOP = result.ReturnValueOop.Value;
                    int testResult = returnOOP.AsSmallInteger();
                    Console.WriteLine($"\n  ✓✓✓ TEST RESULT: {testResult} ✓✓✓");

                    // BlockContextTestRunner: 120 tests × 9 + 111 = 1191
                    if (expr.Source.Contains("BlockContextTestRunner") && testResult == 1191)
                    {
                        Console.WriteLine($"  ✓✓✓ ALL 120 BLOCK CONTEXT TESTS PASSED ✓✓✓\n");
                    }
                }
            }
            else
            {
                failed++;
                Console.WriteLine($"    ❌ Failed: {expr.Source}");
                Console.WriteLine($"       Status: {result.Status}");
                if (result.Diagnostic?.Message != null)
                {
                    Console.WriteLine($"       Error: {result.Diagnostic.Message}");
                }
            }
        }

        // Comment expressions disabled - uncomment to enable
        // Execute comment expressions (ClassName comment: 'text') - show abbreviated progress
        // if (commentIndices.Count > 0)
        // {
        //     Console.WriteLine($"\n--- Comment expressions ({commentIndices.Count} total) ---");
        //     int commentSucceeded = 0;
        //     int commentFailed = 0;
        //     long totalCommentTimeMs = 0;
        //     foreach (int index in commentIndices)
        //     {
        //         var expr = _context.Bundle.InitExpressions[index];
        //
        //         // Extract class name from "ClassName comment: 'text'"
        //         var className = expr.Source?.Split(' ')[0] ?? "Unknown";
        //         Console.Write($"  {className} comment: ... ");
        //
        //         var result = executor.ExecuteInitExpressionOnDemand(expr, compiler, verbose: false);
        //         totalCommentTimeMs += result.ElapsedMilliseconds;
        //
        //         if (result.Status == InitStatus.Success)
        //         {
        //             Console.WriteLine($"✓ {result.ElapsedMilliseconds}ms");
        //             commentSucceeded++;
        //             succeeded++;
        //         }
        //         else
        //         {
        //             Console.WriteLine($"❌ {result.ElapsedMilliseconds}ms");
        //             commentFailed++;
        //             failed++;
        //         }
        //     }
        //     Console.WriteLine($"  Comment expressions: {commentSucceeded}/{commentIndices.Count} succeeded in {totalCommentTimeMs / 1000.0:F1}s total");
        // }

        // Run GC after all initialization to show stats
        Console.WriteLine($"\n=== Running GC after all initialization ===");
        var gcRoots = _context.HeapManager.GC.GetGCRoots();
        Console.WriteLine($"  GC Roots: {gcRoots.Length}");
        var gcStats = _context.HeapManager.GC.GarbageCollect();
        Console.WriteLine($"  {gcStats.GetSummary()}");
        if (gcStats.CollectedObjects > 0 && gcStats.CollectedObjects <= 20)
        {
            Console.WriteLine($"  Collected objects by class:");
            foreach (var (oop, format, size, className) in gcStats.CollectedObjectDetails.Take(20))
            {
                Console.WriteLine($"    OOP({oop}): {className} ({format}, {size} words)");
            }
        }

        // Display cumulative GC statistics and reset for next phase
        var cumulativeStats = _context.HeapManager.GC.GetAndResetCumulativeStatistics();
        Console.WriteLine($"  {cumulativeStats.GetSummary()}");

        // File primitive diagnostics (disabled - enable for performance debugging)
        // FilePrimitiveDiagnostics.PrintReport();

        // PHASE 2: Execute Test expressions (GraphicsTest, etc.)
        if (phase2Indices.Count > 0)
        {
            Console.WriteLine($"\n--- Phase 2: Test expressions ---");
            bool isFirstTest = true;
            foreach (int index in phase2Indices)
            {
                var expr = _context.Bundle.InitExpressions[index];

                // Add 5-second delay between graphics tests to allow viewing each result
                if (!isFirstTest)
                {
                    Console.WriteLine("  [Waiting 5 seconds before next test...]");
                    Thread.Sleep(5000);
                }
                isFirstTest = false;

                var result = executor.ExecuteInitExpressionOnDemand(expr, compiler);

                if (result.Status == InitStatus.Success)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                    Console.WriteLine($"    ❌ Failed: {expr.Source}");
                    Console.WriteLine($"       Status: {result.Status}");
                    if (result.Diagnostic?.Message != null)
                    {
                        Console.WriteLine($"       Error: {result.Diagnostic.Message}");
                    }
                }
            }
        }

        // Report skipped expressions
        if (skippedIndices.Count > 0)
        {
            Console.WriteLine($"\n  Skipped {skippedIndices.Count} expressions:");
            foreach (var (index, reason) in skippedIndices.Take(5))
            {
                var expr = _context.Bundle.InitExpressions[index];
                Console.WriteLine($"    - {expr.Source}: {reason}");
            }
            if (skippedIndices.Count > 5)
            {
                Console.WriteLine($"    ... and {skippedIndices.Count - 5} more");
            }
        }

        Console.WriteLine($"\n=== Init Expression Execution Complete ===");
        Console.WriteLine($"  {succeeded} succeeded, {failed} failed, {skippedIndices.Count} skipped");
        if (phase2Indices.Count > 0)
        {
            Console.WriteLine($"  Phase 1: {phase1Indices.Count} init expressions, Phase 2: {phase2Indices.Count} test expressions");
        }
    }

    private void ExecuteUserExpression()
    {
        if (_context.Config.ExecuteExpression == null)
            return;

        Console.WriteLine($"\n=== Executing User Expression ===");
        Console.WriteLine($"Expression: {_context.Config.ExecuteExpression}");

        try
        {
            // Create MessageSender for runtime message sending
            var messageSender = new MessageSender(
                _context.HeapManager,
                _vm!,
                _context.VMMetadata,
                _vm!.MessageDispatcher,
                _vm!.ContextManager);

            // Step 1: Look up required classes at runtime
            var stringClassOOP = LookupGlobal("String");
            var compilerClassOOP = LookupGlobal("Compiler");

            // Step 2: Create Smalltalk String from user expression
            // DoIt expressions should NOT have explicit return - Parser adds returnLast automatically
            var expression = _context.Config.ExecuteExpression;
            Console.WriteLine($"  [DIAGNOSTIC] Expression as-is: \"{expression}\"");
            var sourceString = StringHelper.CreateString(_context.HeapManager, stringClassOOP, expression);

            // Step 3: Use Compiler class>>evaluate: to compile and execute
            // This is the class-side method that handles DoIt semantics
            // Compiler evaluate: sourceString
            Console.WriteLine("  Evaluating expression using Compiler class>>evaluate:...");
            var resultOOP = messageSender.SendKeyword(
                compilerClassOOP,
                "evaluate:",
                sourceString);

            // Step 5: Display the result
            Console.WriteLine($"\n=== Result ===");
            DisplayResult(resultOOP);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n❌ Error executing user expression: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }

    private void DisplayResult(SmalltalkOOP resultOOP)
    {
        if (resultOOP.IsSmallInteger)
        {
            Console.WriteLine($"SmallInteger: {resultOOP.AsSmallInteger()}");
        }
        else if (resultOOP == _context.HeapManager.GetNil())
        {
            Console.WriteLine("nil");
        }
        else if (resultOOP == LookupGlobal("true"))
        {
            Console.WriteLine("true");
        }
        else if (resultOOP == LookupGlobal("false"))
        {
            Console.WriteLine("false");
        }
        else if (StringHelper.IsString(_context.HeapManager, resultOOP))
        {
            var str = StringHelper.ExtractString(_context.HeapManager, resultOOP);
            Console.WriteLine($"String: \"{str}\"");
        }
        else
        {
            // Generic object - show class and OOP
            var classOOP = _context.HeapManager.GetClassOOP(resultOOP);
            var className = GetClassName(classOOP);
            Console.WriteLine($"{className} instance (OOP: {resultOOP})");
        }
    }

    /// <summary>
    /// Looks up a global variable in the Smalltalk dictionary at runtime.
    /// This is the runtime-safe way to access globals (not using GlobalRegistry).
    /// </summary>
    private SmalltalkOOP LookupGlobal(string globalName)
    {
        // Get Smalltalk global dictionary from VMMetadata (runtime-safe)
        var smalltalkOOP = _context.VMMetadata.KnownObjects.Smalltalk;

        // Search for the global by name
        int size = _context.HeapManager.GetIndexedSize(smalltalkOOP);

        // Find the symbol OOP for the global name
        var symbolOOP = FindSymbol(globalName);
        if (symbolOOP == _context.HeapManager.GetNil())
        {
            throw new InvalidOperationException($"Global '{globalName}' not found - symbol does not exist");
        }

        // Search dictionary for association with this key
        for (int i = 0; i < size; i++)
        {
            var assocOOP = _context.HeapManager.GetIndexedOOP(smalltalkOOP, i);
            if (assocOOP == _context.HeapManager.GetNil())
                continue;

            // Association has key at instance variable 0
            var keyOOP = _context.HeapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == symbolOOP)
            {
                // Return value at instance variable 1
                return _context.HeapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            }
        }

        throw new InvalidOperationException($"Global '{globalName}' not found in Smalltalk dictionary");
    }

    /// <summary>
    /// Finds a symbol OOP by searching through Smalltalk dictionary keys at runtime.
    /// This finds symbols used as global names (classes, global variables).
    /// Runtime-safe: only accesses heap via ISmalltalkHeap interface.
    /// </summary>
    private SmalltalkOOP FindSymbol(string symbolText)
    {
        var smalltalkOOP = _context.VMMetadata.KnownObjects.Smalltalk;
        int size = _context.HeapManager.GetIndexedSize(smalltalkOOP);

        // Linear search through all associations in the Smalltalk dictionary
        for (int i = 0; i < size; i++)
        {
            var assocOOP = _context.HeapManager.GetIndexedOOP(smalltalkOOP, i);
            if (assocOOP == _context.HeapManager.GetNil())
                continue;

            // Get key (Symbol) from the association
            var keyOOP = _context.HeapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == _context.HeapManager.GetNil())
                continue;

            // Check if this Symbol matches the text we're looking for
            var keyString = ExtractSymbolString(keyOOP);
            if (keyString == symbolText)
            {
                return keyOOP;
            }
        }

        return _context.HeapManager.GetNil();
    }

    /// <summary>
    /// Extracts the string value from a Symbol OOP.
    /// </summary>
    private string ExtractSymbolString(SmalltalkOOP symbolOOP)
    {
        int size = _context.HeapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = _context.HeapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        return _context.HeapManager.GetClassName(classOOP);
    }

    // Future: Start Smalltalk controller loop
    // private void StartControllerLoop()
    // {
    //     // TODO: Execute Smalltalk code:
    //     // Processor hiddenBackgroundProcess.
    //     // ScheduledControllers _ ControlManager new.
    //     // ScheduledControllers searchForActiveController.
    //     Console.WriteLine("  Controller loop start: NOT YET IMPLEMENTED");
    // }
}
