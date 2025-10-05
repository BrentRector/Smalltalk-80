namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Main orchestrator for JSON-based cold start bootstrap process.
/// Executes 10 phases to create a complete Smalltalk-80 system from JSON data.
///
/// Critical Design Goals:
/// 1. Zero C# proxy objects - everything is SObjectRef/OOP in Smalltalk heap
/// 2. Zero garbage - all objects reachable from 5 GC roots after bootstrap
/// 3. Deterministic OOP allocation following JSON install order
/// 4. Blue Book compliance throughout
///
/// CRITICAL BOOTSTRAP ORDER:
/// - Symbol/String CLASSES must be created before we can create Symbol/String instances
/// - Use temporary C# storage for symbols until Symbol class + USTable exist
/// - SystemDictionary populated with global associations (all class names, pools, etc.)
/// - Symbols migrated from C# temp storage to Smalltalk USTable using Smalltalk hash
/// </summary>
public class JsonColdStartBootstrap(string jsonPath)
{
    private readonly ColdStartJsonLoader _loader = new(jsonPath);
    private LayoutManager? _layoutManager;
    private ClassLayout? _classLayout;

    /// <summary>
    /// Executes the complete 10-phase bootstrap process and returns the Special Objects Array
    /// with 5 GC roots (nil, false, true, Smalltalk, ProcessorScheduler).
    ///
    /// This is the ONLY entry point for JSON-based cold start. After execution,
    /// the Smalltalk heap contains a fully-wired object graph with zero garbage.
    /// </summary>
    public SpecialObjectsArray Execute()
    {
        Console.WriteLine("[JsonColdStart] Starting JSON-based cold start bootstrap...");
        Console.WriteLine($"[JsonColdStart] JSON version: {_loader.Bundle.Metadata.Version}");
        Console.WriteLine($"[JsonColdStart] Source file: {_loader.Bundle.Metadata.SourceFile}");
        Console.WriteLine($"[JsonColdStart] Symbols: {_loader.Bundle.Symbols.Length}");
        Console.WriteLine($"[JsonColdStart] Strings: {_loader.Bundle.Strings.Length}");
        Console.WriteLine($"[JsonColdStart] Classes: {_loader.Bundle.Classes.Length}");
        Console.WriteLine($"[JsonColdStart] Methods: {_loader.Bundle.Methods.Length}");
        Console.WriteLine($"[JsonColdStart] Init expressions: {_loader.Bundle.InitExpressions.Length}");

        // Phase 1: Initialize layout system
        // The LayoutManager computes field indices from JSON class definitions
        // and caches them for fast access throughout bootstrap
        Console.WriteLine("[JsonColdStart] Phase 1: Initializing layout system...");
        _layoutManager = new LayoutManager(_loader);
        _classLayout = new ClassLayout(_layoutManager);
        LayoutManagement.Initialize(_classLayout);

        // Phase 2: Create temporary C# symbol/string storage
        // CRITICAL: We CANNOT create Symbol objects yet because Symbol class doesn't exist!
        // Instead, maintain temporary C# mapping: symbol/string text → temp placeholder OOP
        // These will be migrated to proper Symbol objects with USTable after classes exist
        Console.WriteLine("[JsonColdStart] Phase 2: Creating temporary symbol/string storage...");
        var symbolInterning = new SymbolInterning(_loader);
        symbolInterning.CreateTemporaryStorage();

        // Phase 3-5: Process install order phases sequentially
        // The JSON specifies exact order: bootstrap classes → core classes → methods
        // After classes are created, Symbol and String classes will exist and have proper structure
        Console.WriteLine("[JsonColdStart] Phase 3-5: Processing install order phases...");
        ProcessInstallOrderPhases();

        // Phase 6: Create SystemDictionary (Smalltalk global) and populate with globals
        // This includes creating Association objects for all global variables:
        // - All class names (e.g., Smalltalk at: #Object put: Object)
        // - Pool dictionaries (e.g., Smalltalk at: #TextConstants put: TextConstants)
        // - System globals (e.g., Smalltalk at: #Processor put: ProcessorScheduler)
        Console.WriteLine("[JsonColdStart] Phase 6: Creating SystemDictionary and global associations...");
        var systemDictCreator = new SystemDictionaryCreator(_loader);
        systemDictCreator.CreateAndPopulate();

        // Phase 7: Migrate symbols from C# temp storage to Smalltalk USTable
        // Now that Symbol class exists and has proper structure, we can:
        // 1. Get Symbol class's USTable field
        // 2. Create proper Symbol objects using Smalltalk hash algorithm
        // 3. Add Symbol objects to USTable (Array or Dictionary)
        // 4. Update ALL existing references (in methods, classes, etc.) to use real Symbol OOPs
        Console.WriteLine("[JsonColdStart] Phase 7: Migrating symbols to USTable...");
        symbolInterning.MigrateToUSTable();

        // Phase 8: Apply forward reference patches
        // Some references are circular (e.g., metaclass.class = metaclass)
        // or can't be set during initial allocation - patches resolve these
        Console.WriteLine("[JsonColdStart] Phase 8: Applying forward reference patches...");
        var patchApplicator = new PatchApplicator(_loader);
        patchApplicator.ApplyAll();

        // Phase 9: Execute initialization expressions
        // Runs global expressions like class comments and initialize methods
        // These may create additional objects but should all be reachable from Smalltalk
        Console.WriteLine("[JsonColdStart] Phase 9: Executing initialization expressions...");
        var initExecutor = new InitExpressionExecutor(_loader);
        initExecutor.ExecuteAll();

        // Phase 10: Create Special Objects Array with 5 GC roots
        // Everything in the system must be reachable from these 5 roots:
        // - nil, false, true (singletons)
        // - Smalltalk (SystemDictionary → all classes → all methods → all symbols)
        // - ProcessorScheduler (→ all processes → all contexts)
        Console.WriteLine("[JsonColdStart] Phase 10: Creating Special Objects Array...");
        var soa = CreateSpecialObjectsArray();

        Console.WriteLine("[JsonColdStart] Bootstrap complete!");
        Console.WriteLine("[JsonColdStart] All objects should now be reachable from 5 GC roots.");
        Console.WriteLine("[JsonColdStart] Next GC should mark all objects and collect nothing (zero garbage).");

        return soa;
    }

    /// <summary>
    /// Processes install order phases sequentially as specified in JSON.
    /// Each phase kind (classes, objects, methods) uses a specialized handler.
    ///
    /// Why sequential? Dependencies must be satisfied:
    /// - Classes must exist before their instances
    /// - Metaclasses must exist before classes
    /// - Symbol/String classes must exist before we can create Symbol/String instances
    /// </summary>
    private void ProcessInstallOrderPhases()
    {
        foreach (var phase in _loader.Bundle.InstallOrder.Phases)
        {
            Console.WriteLine($"[JsonColdStart]   Phase: {phase.Name} ({phase.Kind}, {phase.LocalIds.Length} items)");

            switch (phase.Kind)
            {
                case PhaseKind.Classes:
                    // Allocate class/metaclass objects and populate their fields
                    var allocator = new ClassAllocator(_loader);
                    var populator = new ClassPopulator(_loader);

                    foreach (var localId in phase.LocalIds)
                    {
                        allocator.AllocateClass(localId);
                    }

                    // Populate after all allocated (handles forward references to other classes)
                    foreach (var localId in phase.LocalIds)
                    {
                        populator.PopulateClass(localId);
                    }
                    break;

                case PhaseKind.Objects:
                    // Create pool dictionaries, associations, and other global objects
                    var objectAllocator = new ObjectAllocator(_loader);
                    foreach (var localId in phase.LocalIds)
                    {
                        objectAllocator.AllocateObject(localId);
                    }
                    break;

                case PhaseKind.Methods:
                    // Create CompiledMethod objects and install in method dictionaries
                    var methodInstaller = new MethodInstaller(_loader);
                    foreach (var localId in phase.LocalIds)
                    {
                        methodInstaller.InstallMethod(localId);
                    }
                    break;

                case PhaseKind.InitExpressions:
                    // These are handled in Phase 9 after patches
                    // Skip here to avoid double execution
                    break;

                default:
                    throw new InvalidOperationException($"Unknown install phase kind: {phase.Kind}");
            }
        }
    }

    /// <summary>
    /// Creates the Special Objects Array with 5 GC roots per Blue Book specification.
    ///
    /// These 5 roots make the entire object graph reachable:
    /// 1. nil → UndefinedObject singleton
    /// 2. false → False singleton
    /// 3. true → True singleton
    /// 4. Smalltalk → SystemDictionary → all classes → all methods → all symbols
    /// 5. ProcessorScheduler → all processes → all contexts → all execution state
    /// </summary>
    private SpecialObjectsArray CreateSpecialObjectsArray()
    {
        // Resolve the 5 well-known roots from registered localIds
        var nilObj = _loader.ResolveLocalId("object:nil");
        var falseObj = _loader.ResolveLocalId("object:false");
        var trueObj = _loader.ResolveLocalId("object:true");
        var smalltalk = _loader.ResolveLocalId("object:Smalltalk");
        var processorScheduler = _loader.ResolveLocalId("object:ProcessorScheduler");

        return new SpecialObjectsArray(
            nilObj,
            falseObj,
            trueObj,
            smalltalk,
            processorScheduler
        );
    }
}
