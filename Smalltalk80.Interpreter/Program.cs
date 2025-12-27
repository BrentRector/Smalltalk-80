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
            var executeExpression = config.ExecuteExpression;
            var diagnosticCategories = config.DiagnosticCategories;

            // Create diagnostic hub for event-based logging
            var diagnostics = new DiagnosticHub();

            // Register diagnostic listeners (returns profile listener if enabled)
            var profileListener = RegisterDiagnosticListeners(diagnostics, diagnosticCategories);

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
            var layoutResolver = loader.LayoutResolver ?? throw new InvalidOperationException("LayoutResolver is null after cold start");

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

            // Get VM metadata (built by orchestrator as final step of cold start)
            var vmMetadata = loader.VMMetadata
                ?? throw new InvalidOperationException("VMMetadata is null after cold start");

            // NOTE: Window and Display both use default dimensions
            // Smalltalk Display object is created during cold start with same dimensions
            // When beDisplay primitive is called, window size matches Display (no resize needed)
            // Resize flow is tested when user calls: DisplayScreen displayExtent: width@height

            // Create display window (UI thread object - has heap access for Display bitmap rendering)
            Console.WriteLine("\n=== Creating Display Window ===");
            Console.WriteLine($"  Window size: {InterpreterConstants.DefaultDisplayWidth}x{InterpreterConstants.DefaultDisplayHeight} (matches Smalltalk Display)");
            var displayProvider = new WinFormsDisplayProvider(
                InterpreterConstants.DefaultDisplayWidth,
                InterpreterConstants.DefaultDisplayHeight,
                "Smalltalk-80",
                heapManager,
                vmMetadata);
            Console.WriteLine("Display window created (not shown yet)...");

            // Run GC immediately after cold start to verify heap integrity
            Console.WriteLine("\n=== Running GC after cold start ===");
            var gcStatsBefore = heapManager.GC.GarbageCollect();
            Console.WriteLine($"  {gcStatsBefore.GetSummary()}");

            // Enable allocation-triggered GC now that cold start is complete and roots are established
            // During cold start, objects are built but not yet rooted, so AutoGC was disabled
            heapManager.AutoGCEnabled = true;
            Console.WriteLine("  Auto-GC enabled for runtime");

            // Create Smalltalk thread context
            var threadContext = new SmalltalkThreadContext
            {
                HeapManager = heapManager,
                LayoutResolver = layoutResolver,
                VMMetadata = vmMetadata,
                Bundle = bundle,
                PlatformBridge = displayProvider,  // Smalltalk thread calls methods on UI object
                Diagnostics = diagnostics,
                Config = config
            };

            // Create and start Smalltalk thread
            Console.WriteLine("\n=== Starting Smalltalk Thread ===");
            var interpreterThread = new SmalltalkInterpreterThread(threadContext);

            var smalltalkThread = new Thread(interpreterThread.Run);
            smalltalkThread.SetApartmentState(ApartmentState.MTA);
            smalltalkThread.IsBackground = false; // Foreground thread
            smalltalkThread.Name = "Smalltalk Interpreter";
            smalltalkThread.Start();

            // Monitor Smalltalk thread and close window when it completes (for --execute mode)
            // This ensures the window closes when the expression finishes executing
            var monitorThread = new Thread(() =>
            {
                smalltalkThread.Join();
                // Only close window if it's still open (not already closed by user)
                // Use try-catch to handle race condition where window may be disposed between check and Invoke
                try
                {
                    if (!displayProvider.IsDisposed && displayProvider.IsHandleCreated)
                    {
                        Console.WriteLine("[Monitor] Smalltalk thread completed, closing window...");
                        displayProvider.Invoke((Action)(() => displayProvider.Close()));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Window was disposed between check and Invoke - expected during shutdown
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("cross-thread") || ex.Message.Contains("disposed"))
                {
                    // Window handle invalidated - expected during shutdown
                }
            });
            monitorThread.IsBackground = true;
            monitorThread.Start();

            // Phase 2: Wait for event callbacks to be registered before showing window
            // This prevents race condition where UI events arrive before VM is ready to handle them
            Console.WriteLine("\n=== Waiting for event architecture to initialize ===");
            threadContext.EventCallbacksReady.Wait();
            Console.WriteLine("  Event callbacks registered - safe to show window");

            // Show display window
            Console.WriteLine("\n=== Showing Display Window ===");
            displayProvider.Show();

            // Signal Smalltalk thread that window is displayed
            // This replaces the old Thread.Sleep with proper synchronization
            threadContext.WindowDisplayed.Set();
            Console.WriteLine("  Window displayed signal sent");

            // Run WinForms event loop (blocks until window closes)
            Console.WriteLine("\n=== Entering GUI Event Loop ===");
            Console.WriteLine("Display window is visible. Close window to exit.\n");
            Application.Run(displayProvider);

            // When window closes, signal Smalltalk thread to shutdown
            Console.WriteLine("\n=== Shutting Down ===");
            threadContext.ShutdownTokenSource.Cancel();  // Signal VM to stop

            // Wait for Smalltalk thread to finish (with timeout)
            if (!smalltalkThread.Join(TimeSpan.FromSeconds(5)))
            {
                // Thread.Interrupt() is deprecated and dangerous - just log warning
                // The cancellation token should have signaled the thread to stop
                Console.WriteLine("Warning: Smalltalk thread did not shut down cleanly within 5 seconds");
            }

            // Report profiling statistics if enabled
            profileListener?.ReportStatistics();

            Console.WriteLine("Interpreter shutdown complete");
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
    /// Registers diagnostic listeners based on command-line flags.
    /// Returns the MethodProfileListener if profiling is enabled.
    /// </summary>
    private static MethodProfileListener? RegisterDiagnosticListeners(DiagnosticHub diagnostics, string? diagnosticCategories)
    {
        MethodProfileListener? profileListener = null;

        // Always register cold start progress listener
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

                    case "profile":
                        profileListener = new MethodProfileListener();
                        diagnostics.Register(profileListener, DiagnosticEventTypes.AllMethod);
                        Console.WriteLine("Diagnostics: Method profiling enabled");
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

        return profileListener;
    }

}
