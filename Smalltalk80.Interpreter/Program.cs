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
using Smalltalk80.Interpreter.Platform;
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

            // Build VM metadata (needed by Smalltalk thread)
            var vmMetadata = BuildVMMetadata(layoutResolver, loader);

            // NOTE: Window and Display both use 800x600
            // Smalltalk Display object is 800x600 (created during cold start)
            // When beDisplay primitive is called, window size matches Display (no resize needed)
            // Resize flow is tested when user calls: DisplayScreen displayExtent: width@height

            // Create event queue (shared between threads)
            Console.WriteLine("\n=== Creating Event Queue ===");
            var eventQueue = new SmalltalkEventQueue();

            // Create display window (UI thread object - has heap access for Display bitmap rendering)
            // Window size: 800x600 (matches Smalltalk Display size)
            Console.WriteLine("\n=== Creating Display Window ===");
            Console.WriteLine("  Window size: 800x600 (matches Smalltalk Display)");
            var displayProvider = new WinFormsDisplayProvider(800, 600, eventQueue, "Smalltalk-80", heapManager, vmMetadata);
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
                EventQueue = eventQueue,
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

            // TEMPORARY: Monitor Smalltalk thread and close window when it completes (for debugging)
            var monitorThread = new Thread(() =>
            {
                smalltalkThread.Join();
                Console.WriteLine("[Monitor] Smalltalk thread completed, closing window...");
                displayProvider.Invoke((Action)(() => displayProvider.Close()));
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
                Console.WriteLine("Warning: Smalltalk thread did not shut down cleanly");
                smalltalkThread.Interrupt();
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
        var displayOOP = GlobalRegistry.Get("Display");
        // Get ProcessorScheduler INSTANCE (not class) - registered as "Processor" in Phase 3
        var processorSchedulerOOP = GlobalRegistry.Get("Processor");

        var knownObjects = new KnownObjects(
            nil: nilOOP,
            @true: trueOOP,
            @false: falseOOP,
            smalltalk: smalltalkOOP,
            display: displayOOP
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
