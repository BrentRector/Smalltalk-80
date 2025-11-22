using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Diagnostics;
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
            if (!_context.Config.SkipInitExpressions || HasEssentialInitExpressions())
            {
                Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
                ExecuteInitExpressions();
            }
            else
            {
                Console.WriteLine("[Smalltalk Thread] Skipping all init expressions (--skip-init-expressions)");
            }

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

    private bool HasEssentialInitExpressions()
    {
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

        return _context.Bundle.InitExpressions.Any(expr =>
            expr.Source != null && essentialList.Contains(expr.Source));
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

            // Skip non-essential if --skip-init-expressions flag set
            if (_context.Config.SkipInitExpressions && !isEssential)
            {
                skippedIndices.Add((i, "Skipped by --skip-init-expressions"));
                continue;
            }

            // Include initialize methods and test validation
            if (expr.Source.EndsWith(" initialize") ||
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

        // Execute all selected expressions
        var compiler = new InitExpressionCompiler(
            _context.HeapManager, _context.Bundle, _context.VMMetadata);

        int succeeded = 0;
        int failed = 0;

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
    }

    private void ExecuteUserExpression()
    {
        if (_context.Config.ExecuteExpression == null)
            return;

        Console.WriteLine($"\n=== Executing User Expression ===");
        Console.WriteLine($"Expression: {_context.Config.ExecuteExpression}");

        // TODO: Implement user expression compilation and execution
        // This requires a full Smalltalk source compiler (not just bytecode from bundle)
        // For now, user can execute init expressions from the bundle via --execute flag
        Console.WriteLine("NOTE: User expression execution not yet implemented in two-thread architecture");
        Console.WriteLine("      Use init expressions in the coldstart bundle for now");
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
