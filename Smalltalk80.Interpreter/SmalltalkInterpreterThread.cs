using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Utilities;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Manages the Smalltalk interpreter thread and (future) fiber scheduling.
/// This class encapsulates all Smalltalk execution logic that runs on a separate thread
/// from the Windows UI thread.
/// </summary>
public class SmalltalkInterpreterThread
{
    private readonly SmalltalkThreadContext _context;
    //private FiberScheduler? _fiberScheduler;  // Future: for process scheduling
    private VirtualMachine? _vm;
    // private bool _shutdownRequested;  // Future: for fiber event loop

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

            // STEP 1: Convert this thread to a Windows fiber (future enhancement)
            // Console.WriteLine("[Smalltalk Thread] Converting to fiber...");
            // ConvertThreadToFiber();

            // STEP 2: Create VM
            Console.WriteLine("[Smalltalk Thread] Creating virtual machine...");
            _vm = CreateVirtualMachine();

            // STEP 3: Create fiber scheduler (future enhancement)
            // Console.WriteLine("[Smalltalk Thread] Creating fiber scheduler...");
            // _fiberScheduler = new FiberScheduler(
            //     _context.HeapManager,
            //     _vm,
            //     _context.EventQueue
            // );
            // _fiberScheduler.Initialize();

            // STEP 4: Execute init expressions
            Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
            ExecuteInitExpressions();

            // STEP 5: Execute user expression if requested
            if (_context.Config.ExecuteExpression != null)
            {
                Console.WriteLine("[Smalltalk Thread] Executing user expression...");
                ExecuteUserExpression();
            }

            // STEP 6: Start controller loop (future enhancement)
            // Console.WriteLine("[Smalltalk Thread] Starting controller loop...");
            // StartControllerLoop();

            // STEP 7: Run fibers forever (future enhancement)
            // Console.WriteLine("[Smalltalk Thread] Entering fiber event loop...");
            // RunFiberLoop();

            Console.WriteLine("\n[Smalltalk Thread] Completed successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Smalltalk Thread] Fatal error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    // Future: Convert thread to fiber for Smalltalk process scheduling
    // private void ConvertThreadToFiber()
    // {
    //     var fiberPtr = NativeMethods.ConvertThreadToFiber(IntPtr.Zero);
    //     if (fiberPtr == IntPtr.Zero)
    //     {
    //         var error = Marshal.GetLastWin32Error();
    //         throw new InvalidOperationException(
    //             $"Failed to convert thread to fiber. Error code: {error}");
    //     }
    // }

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
            "FormMenuView initialize", // Requires file I/O primitives (92, 98, 99, 146)
        };

        // Build phase lists
        var phase1Indices = new List<int>();
        var skippedIndices = new List<(int index, string reason)>();

        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var expr = _context.Bundle.InitExpressions[i];
            if (expr.Source == null) continue;

            bool isEssential = essentialList.Contains(expr.Source);

            // Skip if in skip list and not essential
            if (!isEssential && skipList.Contains(expr.Source))
            {
                skippedIndices.Add((i, "Requires features not yet available"));
                continue;
            }

            // Include initialize methods and test validation
            // EXCEPT DisplayScreen initialize which runs last to draw the halftone square
            if ((expr.Source.EndsWith(" initialize") && expr.Source != "DisplayScreen initialize") ||
                expr.Source == "InputSensor install" ||
                expr.Source == "InputSensor initSensor" ||
                expr.Source.Contains("Test validate"))
            {
                phase1Indices.Add(i);
            }
        }

        // Apply dependency ordering
        var dependencyOrder = InitExpressionOrdering.GetDependencyOrder();
        phase1Indices = InitExpressionOrdering.ReorderByDependencies(
            phase1Indices, _context.Bundle, dependencyOrder);

        // Add DisplayScreen initialize at the END (runs last to draw halftone square)
        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var expr = _context.Bundle.InitExpressions[i];
            if (expr.Source == "DisplayScreen initialize")
            {
                phase1Indices.Add(i);
                break;
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

        // Execute comment expressions (ClassName comment: 'text') - show abbreviated progress
        if (commentIndices.Count > 0)
        {
            Console.WriteLine($"\n--- Comment expressions ({commentIndices.Count} total) ---");
            int commentSucceeded = 0;
            int commentFailed = 0;
            foreach (int index in commentIndices)
            {
                var expr = _context.Bundle.InitExpressions[index];

                // Extract class name from "ClassName comment: 'text'"
                var className = expr.Source?.Split(' ')[0] ?? "Unknown";
                Console.Write($"  {className} comment: ... ");

                var result = executor.ExecuteInitExpressionOnDemand(expr, compiler, verbose: false);

                if (result.Status == InitStatus.Success)
                {
                    Console.WriteLine($"✓ {result.ElapsedMilliseconds}ms");
                    commentSucceeded++;
                    succeeded++;
                }
                else
                {
                    Console.WriteLine($"❌ {result.ElapsedMilliseconds}ms");
                    commentFailed++;
                    failed++;
                }
            }
            Console.WriteLine($"  Comment expressions: {commentSucceeded}/{commentIndices.Count} succeeded");
        }

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

    // Future: Run fiber event loop
    // private void RunFiberLoop()
    // {
    //     // This never returns - fibers run forever
    //     // The fiber scheduler manages all process switching
    //     while (!_shutdownRequested)
    //     {
    //         // Fiber scheduling would happen here
    //         Thread.Sleep(100);
    //     }
    // }

    // Future: Request shutdown for fiber event loop
    // public void RequestShutdown()
    // {
    //     _shutdownRequested = true;
    // }
}
