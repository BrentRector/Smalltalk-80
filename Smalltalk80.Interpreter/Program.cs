using System.Diagnostics;
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
using Smalltalk80.Interpreter.Core.VM.Graphics;
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

            // These will be set by either cold start or restore
            SmalltalkHeapManager heapManager;
            VMMetadata vmMetadata;
            ColdStartBundle? bundle = null;
            Stopwatch? restoreTimer = null;

            // Check if restoring from image or doing cold start
            if (config.RestoreImageFile != null)
            {
                // === RESTORE FROM IMAGE ===
                // Note: File existence already validated by CommandLineParser
                restoreTimer = Stopwatch.StartNew();
                Console.WriteLine($"Restoring from image: {config.RestoreImageFile}");

                // Create fresh heap manager
                heapManager = new SmalltalkHeapManager(diagnostics);

                // Create ImageReader and restore heap
                var imageReader = new ImageReader(heapManager);
                var restoreResult = imageReader.ReadImage(config.RestoreImageFile);

                if (!restoreResult.Success)
                {
                    Console.Error.WriteLine($"Error: {restoreResult.ErrorMessage}");
                    if (restoreResult.TechnicalDetails != null)
                    {
                        Console.Error.WriteLine($"Details: {restoreResult.TechnicalDetails}");
                    }
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Image restored: {restoreResult.ObjectsRestored} objects");

                // Rebuild VMMetadata from Smalltalk dictionary (stored in image header)
                // This eliminates the need for a companion .meta file
                var metadataBuilder = new VMMetadataBuilder(heapManager, restoreResult.SmalltalkOOP);
                vmMetadata = metadataBuilder.Build();

                // Configure heap with class name field index for diagnostics
                heapManager.SetClassNameFieldIndex(vmMetadata.ClassNameFieldIndex);

                // Setup GC roots from restored metadata
                // These are the essential roots that keep the object graph alive
                heapManager.GC.ClearGCRoots();
                heapManager.GC.AddGCRoot(vmMetadata.KnownObjects.Nil);
                heapManager.GC.AddGCRoot(vmMetadata.KnownObjects.True);
                heapManager.GC.AddGCRoot(vmMetadata.KnownObjects.False);
                heapManager.GC.AddGCRoot(vmMetadata.KnownObjects.Smalltalk);
                heapManager.GC.AddGCRoot(vmMetadata.ProcessorScheduler);
                Console.WriteLine($"  GC roots set: nil, true, false, Smalltalk, Processor");

                Console.WriteLine("Image restore completed successfully!");
            }
            else
            {
                // === COLD START ===
                // Note: File existence already validated by CommandLineParser
                Console.WriteLine($"Loading cold start bundle: {coldStartFile}");

                // Load the cold start bundle from JSON
                bundle = ColdStartBundle.LoadFromFile(coldStartFile);

                Console.WriteLine($"Bundle loaded: {bundle.Metadata.Version}");
                Console.WriteLine($"  Symbols: {bundle.Symbols.Length}");
                Console.WriteLine($"  Classes: {bundle.Classes.Length}");
                Console.WriteLine($"  Objects: {bundle.Objects.Length}");
                Console.WriteLine($"  Methods: {bundle.Methods.Length}");
                Console.WriteLine();

                // Create the heap manager
                Console.WriteLine("Initializing heap manager...");
                heapManager = new SmalltalkHeapManager(diagnostics);

                // Create and run the loader
                Console.WriteLine("Starting cold start loader (6-phase)...");

                var loader = new ColdStartOrchestrator(bundle, heapManager, coldStartFile, diagnostics);
                loader.Run();
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
                vmMetadata = loader.VMMetadata
                    ?? throw new InvalidOperationException("VMMetadata is null after cold start");
            }

            // Run GC immediately after cold start to verify heap integrity
            Console.WriteLine("\n=== Running GC after cold start ===");
            var gcStatsBefore = heapManager.GC.GarbageCollect();
            Console.WriteLine($"  {gcStatsBefore.GetSummary()}");

            // Enable allocation-triggered GC now that cold start is complete and roots are established
            if (config.DisableGC)
            {
                Console.WriteLine("  Auto-GC DISABLED (--no-gc flag)");
            }
            else
            {
                heapManager.AutoGCEnabled = true;
                Console.WriteLine("  Auto-GC enabled for runtime");
            }

            // Display is created lazily when Smalltalk calls BeDisplay
            WinFormsDisplay? display = null;

            // Create Smalltalk thread context
            var threadContext = new SmalltalkThreadContext
            {
                HeapManager = heapManager,
                VMMetadata = vmMetadata,
                Bundle = bundle,
                Display = null,
                Diagnostics = diagnostics,
                Config = config,
                RestoreTimer = restoreTimer
            };

            // Set display factory after construction (lambda captures threadContext)
            threadContext.DisplayFactory = (width, height, heap, metadata) =>
            {
                // Called from Smalltalk thread when BeDisplay is invoked
                threadContext.RequestedDisplayWidth = width;
                threadContext.RequestedDisplayHeight = height;
                threadContext.DisplayNeeded.Set();
                threadContext.DisplayCreated.Wait();
                return threadContext.Display!;
            };

            // Start Smalltalk thread (runs init expressions and process scheduling)
            Console.WriteLine("\n=== Starting Smalltalk Thread ===");
            var interpreterThread = new SmalltalkInterpreterThread(threadContext);

            var smalltalkThread = new Thread(interpreterThread.Run);
            smalltalkThread.SetApartmentState(ApartmentState.MTA);
            smalltalkThread.IsBackground = false;
            smalltalkThread.Name = "Smalltalk Interpreter";
            smalltalkThread.Start();

            // Wait for Smalltalk to request display creation (BeDisplay primitive)
            Console.WriteLine("\n=== Waiting for Smalltalk to request display ===");
            threadContext.DisplayNeeded.Wait();

            int displayWidth = threadContext.RequestedDisplayWidth;
            int displayHeight = threadContext.RequestedDisplayHeight;
            Console.WriteLine($"  Display requested: {displayWidth}x{displayHeight}");

            // Create display window on main thread
            Console.WriteLine("\n=== Creating Display Window ===");
            display = new WinFormsDisplay(
                displayWidth,
                displayHeight,
                "Smalltalk-2026",
                heapManager,
                vmMetadata,
                Core.VM.Platform.PixelDepth.ThirtyTwoBit);
            Console.WriteLine($"  Window created: {displayWidth}x{displayHeight}");

            // Store display and signal Smalltalk thread
            threadContext.Display = display;
            threadContext.DisplayCreated.Set();

            // Monitor thread to close window when Smalltalk completes
            var monitorThread = new Thread(() =>
            {
                smalltalkThread.Join();
                try
                {
                    if (display != null && !display.IsDisposed && display.IsHandleCreated)
                    {
                        Console.WriteLine("[Monitor] Smalltalk thread completed, closing window...");
                        display.Invoke((Action)(() => display.Close()));
                    }
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
            monitorThread.IsBackground = true;
            monitorThread.Start();

            // Run WinForms event loop
            Console.WriteLine("\n=== Entering GUI Event Loop ===");
            display.Show();
            Application.Run(display);

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
