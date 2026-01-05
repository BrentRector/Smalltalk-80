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

            // Create VM with display factory for lazy window creation
            Console.WriteLine("[Smalltalk Thread] Creating virtual machine...");
            _vm = CreateVirtualMachine();

            // Check if restoring from saved image vs cold start
            bool isRestore = _context.Bundle == null;

            if (isRestore)
            {
                // RESTORE PATH: Resume saved active process
                Console.WriteLine("[Smalltalk Thread] Resuming from saved image...");
                ResumeFromSnapshot();
            }
            else
            {
                // COLD START PATH: Initialize and run normally
                Console.WriteLine("[Smalltalk Thread] Initializing ProcessorScheduler...");
                var schedulerInit = new ProcessSchedulerInitializer(_context.HeapManager, _context.VMMetadata);
                schedulerInit.InitializeForRuntime();

                // Execute init expressions (includes DisplayScreen initialize which calls beDisplay)
                Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
                ExecuteInitExpressions();

                // Execute user expression if requested (skips controller loop)
                if (_context.Config.ExecuteExpression != null)
                {
                    Console.WriteLine("[Smalltalk Thread] Executing user expression...");
                    ExecuteUserExpression();
                    Console.WriteLine("\n[Smalltalk Thread] Completed successfully");
                    return;
                }

                // Start MVC controller loop (runs forever)
                Console.WriteLine("[Smalltalk Thread] Starting controller loop...");
                StartControllerLoop();
            }

            Console.WriteLine("\n[Smalltalk Thread] Completed successfully");
        }
        catch (OperationCanceledException)
        {
            // Clean shutdown requested - this is expected when window closes
            Console.WriteLine("\n[Smalltalk Thread] Shutdown signal received");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Smalltalk Thread] Fatal error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            // Re-throw to propagate to main thread, allowing proper cleanup
            // instead of abruptly terminating with Environment.Exit(1)
            throw;
        }
    }

    private VirtualMachine CreateVirtualMachine()
    {
        var vm = new VirtualMachine(
            _context.HeapManager,
            _context.VMMetadata,
            display: _context.Display,  // May be null initially (lazy creation)
            _context.Diagnostics
        );

        // Set up display factory for lazy display creation
        // When BeDisplay calls GetOrCreateDisplay and no display exists,
        // this factory coordinates with the UI thread to create the window
        if (_context.DisplayFactory != null)
        {
            vm.SetDisplayFactory((width, height) =>
            {
                var display = _context.DisplayFactory(width, height, _context.HeapManager, _context.VMMetadata);
                vm.SetDisplay(display);  // Cache in VM for subsequent calls
                return display;
            });
        }

        vm.VerboseDebug = _context.Config.VerboseDebug;
        vm.ShutdownToken = _context.ShutdownToken;  // Wire up shutdown signal
        vm.InitializeSelectorCaches();

        return vm;
    }

    private void ExecuteInitExpressions()
    {
        if (_context.Bundle == null)
            return;

        var runner = new InitExpressionRunner(
            _context.HeapManager,
            _vm!,
            _context.VMMetadata,
            _context.Diagnostics,
            _context.Bundle.InitExpressions
        );
        runner.Execute();
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
    /// Uses the canonical Smalltalk-80 lookup chain: SystemDictionary → Undeclared.
    /// </summary>
    private SmalltalkOOP LookupGlobal(string globalName)
    {
        var valueOOP = _vm!.RuntimeServices.LookupGlobalValue(globalName);
        if (valueOOP == _context.HeapManager.GetNil())
        {
            throw new InvalidOperationException($"Global '{globalName}' not found");
        }
        return valueOOP;
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        return _context.HeapManager.GetClassName(classOOP);
    }

    /// <summary>
    /// Starts the Smalltalk MVC controller loop.
    /// This implements the canonical Bluebook startup sequence:
    /// 1. Processor hiddenBackgroundProcess  - Create background idle process at priority 1
    /// 2. ControlManager new → ScheduledControllers  - Create and install ControlManager
    /// 3. Lower bootstrap process priority from 8 to 4  - Allow InputSensor etc. to run
    /// 4. ScheduledControllers searchForActiveController  - Start event loop (runs forever)
    /// </summary>
    private void StartControllerLoop()
    {
        Console.WriteLine("\n=== Starting MVC Controller Loop ===");

        // Create MessageSender for runtime message sending
        var messageSender = new MessageSender(
            _context.HeapManager,
            _vm!,
            _context.VMMetadata,
            _vm!.MessageDispatcher,
            _vm!.ContextManager);

        // Step 1: ProcessorScheduler hiddenBackgroundProcess
        // Creates an infinite loop process at priority 1 (SystemRockBottomPriority)
        // Note: hiddenBackgroundProcess is a CLASS method on ProcessorScheduler,
        // not an instance method on Processor (the singleton instance)
        Console.WriteLine("  Step 1: Creating background process...");
        var processorSchedulerClassOOP = LookupGlobal("ProcessorScheduler");
        messageSender.SendUnary(processorSchedulerClassOOP, "hiddenBackgroundProcess");
        Console.WriteLine("    ✓ Background process created at priority 1");

        // Verify background process is on the queue
        var processorOOP = LookupGlobal("Processor");
        var quiescentListsOOP = _context.HeapManager.GetInstanceVariable(processorOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        var priority1ListOOP = _context.HeapManager.GetIndexedOOP(quiescentListsOOP, 0); // priority 1 is index 0
        var firstLinkOOP = _context.HeapManager.GetInstanceVariable(priority1ListOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
        var nilOOP = _context.HeapManager.GetNil();
        if (firstLinkOOP == nilOOP)
            Console.WriteLine("    WARNING: Background process NOT on priority 1 queue!");

        // Step 2: ControlManager new → install as ScheduledControllers global
        Console.WriteLine("  Step 2: Creating ControlManager...");
        var controlManagerClassOOP = LookupGlobal("ControlManager");
        var controlManagerOOP = messageSender.SendUnary(controlManagerClassOOP, "new");
        Console.WriteLine($"    ✓ ControlManager created: {controlManagerOOP}");

        // Install as Smalltalk global using canonical Association from cold start
        InstallScheduledControllersGlobal(controlManagerOOP);

        // Step 3: Lower bootstrap process priority from 8 to 4 (UserSchedulingPriority)
        // This is CRITICAL: searchForActiveController loops with Processor yield, which only
        // yields to same-or-higher priority processes. At priority 8, InputSensor (pri 6-7)
        // and other system processes never get CPU time, so cursorPoint is never updated
        // and isControlWanted always returns false.
        Console.WriteLine("  Step 3: Lowering bootstrap process priority to 4...");
        // processorOOP already declared above for diagnostic
        var activeProcessOOP = messageSender.SendUnary(processorOOP, "activeProcess");
        messageSender.SendKeyword(activeProcessOOP, "priority:", SmalltalkOOP.FromSmallInteger(4));
        Console.WriteLine("    ✓ Bootstrap process now at UserSchedulingPriority (4)");

        // Step 4: Restore display (paints gray background and all scheduled views)
        Console.WriteLine("  Step 4: Restoring display...");
        messageSender.SendUnary(controlManagerOOP, "restore");
        Console.WriteLine("    ✓ Display restored (gray background painted)");

        // Step 5: ScheduledControllers searchForActiveController
        // This runs the main event loop - it will run FOREVER until the system exits
        Console.WriteLine("  Step 5: Starting controller event loop...");
        Console.WriteLine("    (This will run until the window is closed)");

        // Disable infinite loop detection - the event loop intentionally runs forever
        _vm!.InfiniteLoopDetectionEnabled = false;

        // Use controlManagerOOP directly - we already have it from step 2
        messageSender.SendUnary(controlManagerOOP, "searchForActiveController");

        // Note: We should never reach here - searchForActiveController runs forever
        Console.WriteLine("  Controller loop exited (unexpected)");
    }

    /// <summary>
    /// Installs ScheduledControllers global at runtime.
    /// Uses the canonical Smalltalk-80 lookup chain: SystemDictionary → Undeclared → create new.
    /// The canonical Association was created during cold start and installed in SystemDictionary.
    /// Method literals point to this same Association object.
    /// </summary>
    private void InstallScheduledControllersGlobal(SmalltalkOOP controlManagerOOP)
    {
        const string globalName = "ScheduledControllers";

        // Use the canonical global lookup and set API
        // This handles: SystemDictionary lookup → Undeclared fallback → migration → creation
        _vm!.RuntimeServices.SetGlobalValue(globalName, controlManagerOOP);
        Console.WriteLine($"    ✓ Installed {globalName} global with ControlManager");
    }

    // NOTE: Dictionary operations are now in RuntimeDictionaryService
    // Use _vm.RuntimeServices for canonical global lookup operations

    /// <summary>
    /// Resumes execution from a saved snapshot image.
    /// The saved activeProcess is inside snapshotAs:thenQuit: just after snapshotPrimitive.
    /// The receiver (Smalltalk) was pushed onto the stack BEFORE the image was saved by primitive 97,
    /// so it's already there as the return value of snapshotPrimitive.
    /// This makes justSnapped = false, triggering the post-snapshot restore code.
    /// </summary>
    private void ResumeFromSnapshot()
    {
        Console.WriteLine("\n=== Resuming from Snapshot ===");

        var heap = _context.HeapManager;
        var metadata = _context.VMMetadata;

        // Step 1: Get the saved activeProcess from ProcessorScheduler
        var schedulerOOP = metadata.ProcessorScheduler;
        var activeProcessOOP = heap.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

        if (activeProcessOOP == metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("No active process found in restored image");
        }

        Console.WriteLine($"  Active process: {activeProcessOOP}");

        // Step 2: Get the process's suspended context
        var suspendedContextOOP = heap.GetInstanceVariable(activeProcessOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

        if (suspendedContextOOP == metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("Active process has no suspended context");
        }

        Console.WriteLine($"  Suspended context: {suspendedContextOOP}");

        // Step 3: Set the active context to the suspended context
        _vm!.ContextManager.SetActiveContext(suspendedContextOOP);

        // Note: The receiver (Smalltalk) is already on the stack - it was pushed by primitive 97
        // BEFORE the image was written. We do NOT need to push it again.
        // This makes justSnapped = self snapshotPrimitive isNil evaluate to false,
        // which triggers the post-snapshot restoration code (DisplayScreen displayHeight: height, etc.)
        Console.WriteLine($"  Smalltalk is already on stack as snapshotPrimitive return value");

        // Step 4: Disable infinite loop detection - the event loop intentionally runs forever
        _vm.InfiniteLoopDetectionEnabled = false;

        // Step 5: Stop restore timer and report timing
        if (_context.RestoreTimer != null)
        {
            _context.RestoreTimer.Stop();
            Console.WriteLine($"\n=== Image Restore Timing ===");
            Console.WriteLine($"  Total restore time: {_context.RestoreTimer.ElapsedMilliseconds}ms ({_context.RestoreTimer.Elapsed.TotalSeconds:F2}s)");
        }

        // Step 6: Resume execution from the saved context
        Console.WriteLine("  Resuming bytecode execution...");
        _vm.ExecuteExistingContext(suspendedContextOOP);

        // Note: We should never reach here - the restore code eventually enters the controller loop
        Console.WriteLine("  Snapshot resume completed (unexpected exit)");
    }

    /// <summary>
    /// Dumps Float class variables to verify they have initialized values (NOT nil).
    /// </summary>
    private void DumpFloatClassVariables()
    {
        Console.WriteLine($"\n[DIAGNOSTIC] Float class variables after initialize:");

        try
        {
            var heap = _context.HeapManager;

            // Find Float class using runtime heap access
            var floatClassOOP = _vm!.RuntimeServices.GetClass("Float");
            if (floatClassOOP == heap.GetNil())
            {
                Console.WriteLine("  ERROR: Float class not found!");
                return;
            }

            var classPoolOOP = heap.GetInstanceVariable(floatClassOOP, VMConstants.CLASS_CLASSPOOL_INDEX);
            int poolSize = heap.GetIndexedSize(classPoolOOP);

            Console.WriteLine($"  Float classPool size: {poolSize}");

            // Dump ALL Associations in the classPool
            Console.WriteLine($"  Dumping all {poolSize} classPool entries:");
            int nonNilCount = 0;
            for (int i = 0; i < poolSize; i++)
            {
                var entry = heap.GetIndexedOOP(classPoolOOP, i);
                if (entry != heap.GetNil())
                {
                    // It's an Association - get key and value
                    var keyOOP = heap.GetInstanceVariable(entry, VMConstants.ASSOCIATION_KEY_INDEX);
                    var valueOOP = heap.GetInstanceVariable(entry, VMConstants.ASSOCIATION_VALUE_INDEX);

                    string keyStr = heap.GetClassName(keyOOP); // Symbols have their text as "class name"
                    string valueStr = valueOOP == heap.GetNil() ? "nil" :
                                      valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" :
                                      $"Object({valueOOP})";

                    Console.WriteLine($"    [{i}] {keyStr} = {valueStr}");
                    nonNilCount++;
                }
            }
            Console.WriteLine($"  Total non-nil entries: {nonNilCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR during diagnostic: {ex.Message}");
        }
    }

    private void DumpMethodLiterals(SmalltalkOOP methodOOP, string methodName)
    {
        var heap = _context.HeapManager;
        Console.WriteLine($"\n[LITERALS] {methodName} method literals:");

        try
        {
            // Use CompiledMethodView to properly access literals
            var methodView = new Smalltalk80.Interpreter.Core.VM.Views.CompiledMethodView(
                methodOOP,
                heap
            );

            int litCount = methodView.LiteralCount;
            Console.WriteLine($"  Literal count: {litCount}");

            for (int i = 0; i < litCount; i++)
            {
                var literalOOP = methodView.GetLiteral(i);
                string desc = DescribeLiteral(literalOOP);
                Console.WriteLine($"    [{i}] {desc}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR dumping literals: {ex.Message}");
        }
    }

    private string DescribeLiteral(SmalltalkOOP oop)
    {
        var heap = _context.HeapManager;

        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        if (oop == heap.GetNil())
        {
            return "nil";
        }

        // Try to identify the object type
        try
        {
            // Check if it's an Association (has 2 instance variables: key and value)
            var classOOP = heap.GetClassOOP(oop);
            int instVarCount = heap.GetInstanceVariableCount(oop);
            int indexedSize = heap.GetIndexedSize(oop);

            if (instVarCount == 2 && indexedSize == 0)
            {
                // Likely an Association
                var keyOOP = heap.GetInstanceVariable(oop, VMConstants.ASSOCIATION_KEY_INDEX);
                var valueOOP = heap.GetInstanceVariable(oop, VMConstants.ASSOCIATION_VALUE_INDEX);
                string keyStr = heap.GetClassName(keyOOP); // Use heap method for symbol string
                string valueStr = valueOOP == heap.GetNil() ? "nil" : valueOOP.ToString();
                return $"Association(key: '{keyStr}', value: {valueStr}) OOP={oop}";
            }

            return $"Object(instVars: {instVarCount}, indexed: {indexedSize}) OOP={oop}";
        }
        catch
        {
            return $"Object OOP={oop}";
        }
    }
}
