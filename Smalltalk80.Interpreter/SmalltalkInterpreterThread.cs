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

            // STEP 1.5: Initialize ProcessorScheduler with high-priority bootstrap process
            // This ensures all init expressions run at priority 8, preventing preemption
            // when they fork processes (like Delay's timing process also at priority 8)
            Console.WriteLine("[Smalltalk Thread] Initializing ProcessorScheduler for runtime...");
            var schedulerInit = new ProcessSchedulerInitializer(_context.HeapManager, _context.VMMetadata);
            schedulerInit.InitializeForRuntime();

            // STEP 2: Execute init expressions
            Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
            ExecuteInitExpressions();

            // STEP 3: Execute user expression if requested (skips controller loop)
            if (_context.Config.ExecuteExpression != null)
            {
                Console.WriteLine("[Smalltalk Thread] Executing user expression...");
                ExecuteUserExpression();
                Console.WriteLine("\n[Smalltalk Thread] Completed successfully");
                return;
            }

            // STEP 4: Start MVC controller loop (runs forever)
            Console.WriteLine("[Smalltalk Thread] Starting controller loop...");
            StartControllerLoop();

            // Note: We only reach here if the controller loop exits (unexpected)
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
        vm.ShutdownToken = _context.ShutdownToken;  // Wire up shutdown signal
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

        // Execute comment expressions (ClassName comment: 'text') - show abbreviated progress
        if (commentIndices.Count > 0)
        {
            Console.WriteLine($"\n--- Comment expressions ({commentIndices.Count} total) ---");
            int commentSucceeded = 0;
            int commentFailed = 0;
            long totalCommentTimeMs = 0;
            foreach (int index in commentIndices)
            {
                var expr = _context.Bundle.InitExpressions[index];

                // Extract class name from "ClassName comment: 'text'"
                var className = expr.Source?.Split(' ')[0] ?? "Unknown";
                Console.Write($"  {className} comment: ... ");

                var result = executor.ExecuteInitExpressionOnDemand(expr, compiler, verbose: false);
                totalCommentTimeMs += result.ElapsedMilliseconds;

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
            Console.WriteLine($"  Comment expressions: {commentSucceeded}/{commentIndices.Count} succeeded in {totalCommentTimeMs / 1000.0:F1}s total");
        }

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
        else
        {
            Console.WriteLine($"    Background process on queue: {firstLinkOOP}");
            // Debug: Check the process's suspendedContext at creation time
            var suspCtx = _context.HeapManager.GetInstanceVariable(firstLinkOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
            Console.WriteLine($"    SuspendedContext: {suspCtx}");
            var ctxClass = _context.HeapManager.GetClassName(suspCtx);
            Console.WriteLine($"    Context class: {ctxClass}");
            if (ctxClass == "BlockContext")
            {
                var homeCtx = _context.HeapManager.GetInstanceVariable(suspCtx, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                var startPC = _context.HeapManager.GetInstanceVariable(suspCtx, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
                Console.WriteLine($"    Home context: {homeCtx}, startPC: {startPC}");
                var homeMethod = _context.HeapManager.GetInstanceVariable(homeCtx, VMConstants.CONTEXT_METHOD_INDEX);
                Console.WriteLine($"    Home method: {homeMethod}");
                // Get method selector (literal 0 is at indexed slot 1, slot 0 is header)
                var methodIndexedSize = _context.HeapManager.GetIndexedSize(homeMethod);
                if (methodIndexedSize >= 2)
                {
                    var selectorOOP = _context.HeapManager.GetIndexedOOP(homeMethod, 1);
                    var selectorName = ExtractSymbolString(selectorOOP);
                    Console.WriteLine($"    Home method selector: {selectorName}");
                }
                // Get bytecodes length
                var bytecodeArrayOOP = _context.HeapManager.GetIndexedOOP(homeMethod, methodIndexedSize - 1);
                var bcLength = _context.HeapManager.GetIndexedSize(bytecodeArrayOOP);
                Console.WriteLine($"    Bytecodes length: {bcLength}, startPC: {(startPC.IsSmallInteger ? startPC.AsSmallInteger() : -1)}");
                if (startPC.IsSmallInteger && startPC.AsSmallInteger() >= bcLength)
                    Console.WriteLine($"    *** WARNING: startPC ({startPC.AsSmallInteger()}) >= bytecodes length ({bcLength})! ***");
                // Also dump actual bytecodes
                Console.Write($"    Bytecodes: ");
                for (int i = 0; i < Math.Min(bcLength, 20); i++)
                {
                    var b = _context.HeapManager.GetIndexedByte(bytecodeArrayOOP, i);
                    Console.Write($"{b} ");
                }
                Console.WriteLine();
            }
        }

        // Step 2: ControlManager new → install as ScheduledControllers global
        Console.WriteLine("  Step 2: Creating ControlManager...");
        var controlManagerClassOOP = LookupGlobal("ControlManager");
        var controlManagerOOP = messageSender.SendUnary(controlManagerClassOOP, "new");
        Console.WriteLine($"    ✓ ControlManager created: {controlManagerOOP}");

        // Install as Smalltalk global using canonical Association from cold start
        // The Association for ScheduledControllers was created during method compilation
        // (since 23 methods reference it) and is in AssociationRegistry and Undeclared.
        Console.WriteLine("    Installing ScheduledControllers global...");
        InstallScheduledControllersGlobal(controlManagerOOP);
        Console.WriteLine("    ✓ ControlManager installed as ScheduledControllers global");

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
    /// Installs ScheduledControllers global using the canonical Association from cold start.
    /// This follows the same pattern as SystemDictionaryInstaller:
    /// 1. Get canonical Association from AssociationRegistry
    /// 2. Set its value to the ControlManager instance
    /// 3. Install in SystemDictionary using hash-based insertion
    /// </summary>
    private void InstallScheduledControllersGlobal(SmalltalkOOP controlManagerOOP)
    {
        const string globalName = "ScheduledControllers";
        var heap = _context.HeapManager;

        // Get canonical Association from AssociationRegistry (created during method compilation)
        var allAssociations = AssociationRegistry.GetAll();
        if (!allAssociations.TryGetValue(globalName, out var associationOOP))
        {
            throw new InvalidOperationException(
                $"Canonical Association for '{globalName}' not found in AssociationRegistry. " +
                "This symbol should have been registered during method compilation.");
        }

        Console.WriteLine($"      Found canonical Association: {associationOOP}");

        // Set the Association's value to the ControlManager instance
        heap.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, controlManagerOOP);

        // Get the key symbol from the Association for hash calculation
        var keySymbolOOP = heap.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);

        // Install in SystemDictionary using hash-based insertion
        var systemDictOOP = _context.VMMetadata.KnownObjects.Smalltalk;
        InstallAssociationInDictionary(systemDictOOP, associationOOP, keySymbolOOP, globalName);

        // Remove from Undeclared dictionary (proper migration)
        if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
        {
            RemoveFromDictionary(undeclaredOOP, keySymbolOOP, globalName);
        }
    }

    /// <summary>
    /// Installs an Association in a dictionary using hash-based slot finding.
    /// Runtime version of DictionaryOperations.InstallAssociation.
    /// </summary>
    private void InstallAssociationInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP,
        SmalltalkOOP keySymbolOOP, string keyName)
    {
        var heap = _context.HeapManager;
        int dictSize = heap.GetIndexedSize(dictionaryOOP);
        var nilOOP = heap.GetNil();

        // Use identity hash (oop >> 1) for symbol lookup
        int hash = IdentityHashUtility.ComputeIdentityHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

        // Probe for nil slot or matching key (linear probing)
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = heap.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Found empty slot - install association
                heap.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                // Increment tally
                if (incrementTally)
                {
                    var currentTallyOOP = heap.GetInstanceVariable(dictionaryOOP, 0);
                    if (currentTallyOOP.IsSmallInteger)
                    {
                        int newTally = currentTallyOOP.AsSmallInteger() + 1;
                        heap.SetInstanceVariable(dictionaryOOP, 0, SmalltalkOOP.FromSmallInteger(newTally));
                    }
                }

                Console.WriteLine($"      Installed at slot {location} (hash={hash})");
                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = heap.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Replace existing
                heap.SetIndexedOOP(dictionaryOOP, location, associationOOP);
                Console.WriteLine($"      Replaced at slot {location}");
                return;
            }

            // Linear probe
            location = (location + 1) % dictSize;

            if (location == startLocation)
            {
                throw new InvalidOperationException(
                    $"Dictionary is full - cannot install '{keyName}' association");
            }
        }
    }

    /// <summary>
    /// Removes an Association from a dictionary (for Undeclared cleanup).
    /// Runtime version of DictionaryOperations.RemoveAssociation.
    /// </summary>
    private void RemoveFromDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbolOOP, string keyName)
    {
        var heap = _context.HeapManager;
        var nilOOP = heap.GetNil();
        int dictSize = heap.GetIndexedSize(dictionaryOOP);

        int hash = IdentityHashUtility.ComputeIdentityHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;
        int startLocation = location;

        while (true)
        {
            var probe = heap.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Not found
                return;
            }

            var probeKeyOOP = heap.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Found - remove
                heap.SetIndexedOOP(dictionaryOOP, location, nilOOP);

                // Decrement tally
                var tallyOOP = heap.GetInstanceVariable(dictionaryOOP, 0);
                if (tallyOOP.IsSmallInteger)
                {
                    int tally = tallyOOP.AsSmallInteger();
                    heap.SetInstanceVariable(dictionaryOOP, 0, SmalltalkOOP.FromSmallInteger(tally - 1));
                }

                // Fix probe chain
                FixCollisionsFrom(dictionaryOOP, location);
                Console.WriteLine($"      Removed from Undeclared at slot {location}");
                return;
            }

            location = (location + 1) % dictSize;

            if (location == startLocation)
            {
                return; // Not found after full scan
            }
        }
    }

    /// <summary>
    /// Repairs the linear probing chain after removing an entry.
    /// Runtime version of DictionaryOperations.FixCollisionsFrom.
    /// </summary>
    private void FixCollisionsFrom(SmalltalkOOP dictionaryOOP, int startIndex)
    {
        var heap = _context.HeapManager;
        var nilOOP = heap.GetNil();
        int dictSize = heap.GetIndexedSize(dictionaryOOP);
        int oldIndex = startIndex;

        while (true)
        {
            oldIndex = (oldIndex + 1) % dictSize;
            var nextObject = heap.GetIndexedOOP(dictionaryOOP, oldIndex);

            if (nextObject == nilOOP)
                break;

            // Find where this Association SHOULD be
            var keyOOP = heap.GetInstanceVariable(nextObject, 0);
            int hash = IdentityHashUtility.ComputeIdentityHash(keyOOP);
            int nextIndex = (hash % dictSize + dictSize) % dictSize;

            // Linear probe to find correct slot
            int probeCount = 0;
            while (true)
            {
                var probe = heap.GetIndexedOOP(dictionaryOOP, nextIndex);

                if (probe == nilOOP || probe == nextObject)
                    break;

                var probeKeyOOP = heap.GetInstanceVariable(probe, 0);
                if (probeKeyOOP == keyOOP)
                    break;

                nextIndex = (nextIndex + 1) % dictSize;
                probeCount++;

                if (probeCount >= dictSize)
                {
                    throw new InvalidOperationException(
                        "FixCollisionsFrom: Cannot find slot for Association during rehash");
                }
            }

            // Move if displaced
            if (nextIndex != oldIndex)
            {
                heap.SetIndexedOOP(dictionaryOOP, nextIndex, nextObject);
                heap.SetIndexedOOP(dictionaryOOP, oldIndex, nilOOP);
            }
        }
    }
}
