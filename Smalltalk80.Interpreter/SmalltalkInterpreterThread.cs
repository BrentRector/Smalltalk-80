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

        try
        {
            // Create MessageSender for runtime message sending
            var messageSender = new MessageSender(
                _context.HeapManager,
                _vm!,
                _context.VMMetadata,
                _vm!.MessageDispatcher,
                _vm!.ContextManager);

            // Step 1: Create Smalltalk String from user expression
            var sourceString = StringHelper.CreateString(_context.HeapManager, _context.Config.ExecuteExpression);

            // Step 2: Get Compiler class and UndefinedObject class
            var compilerClassOOP = GlobalRegistry.Get("class:Compiler");
            var undefinedObjectClassOOP = GlobalRegistry.Get("class:UndefinedObject");

            // Step 3: Send compile:in: to Compiler
            // Compiler compile: sourceString in: UndefinedObject
            Console.WriteLine("  Compiling expression using Smalltalk Compiler...");
            var compiledMethodOOP = messageSender.SendKeyword(
                compilerClassOOP,
                "compile:in:",
                sourceString,
                undefinedObjectClassOOP);

            Console.WriteLine($"  Compiled method: {compiledMethodOOP}");

            // Step 4: Execute the compiled method
            // We need to execute it with nil as receiver (DoIt semantics)
            var nilOOP = _context.HeapManager.GetNil();

            // Create a MethodContext and execute
            var contextOOP = _vm!.ContextManager.CreateMethodContext(
                compiledMethodOOP,
                nilOOP,
                Array.Empty<SmalltalkOOP>(),
                callerContextOOP: _context.HeapManager.GetNil());

            Console.WriteLine("  Executing compiled method...");
            var resultOOP = _vm!.ExecuteExistingContext(contextOOP);

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
        else if (resultOOP == GlobalRegistry.Get("true"))
        {
            Console.WriteLine("true");
        }
        else if (resultOOP == GlobalRegistry.Get("false"))
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

    private string GetClassName(SmalltalkOOP classOOP)
    {
        try
        {
            var nameOOP = _context.HeapManager.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_NAME_INDEX);
            if (nameOOP == _context.HeapManager.GetNil())
                return "<unnamed>";

            int length = _context.HeapManager.GetIndexedSize(nameOOP);
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = _context.HeapManager.GetIndexedByte(nameOOP, i);
            }

            return System.Text.Encoding.ASCII.GetString(bytes);
        }
        catch
        {
            return "<unknown>";
        }
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
