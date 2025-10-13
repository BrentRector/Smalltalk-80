using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Orchestrates the cold start loader.
/// Executes phases in order to build up the Smalltalk-80 object memory from the JSON bundle.
/// Bytecode Validation is optional and runs after GC verification; can be skipped by setting SKIP_BYTECODE_VALIDATION=1.
/// </summary>
public class Loader
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, int> _localIdToObjectId = new();
    private LayoutResolver? _layoutResolver;
    private int _initExpressionsArrayId;

    public Loader(ColdStartBundle bundle, ISmalltalkHeapManager heapManager)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _allocator = new SemanticAllocator(heapManager, bundle);
    }

    /// <summary>
    /// Gets the init expressions array ID (available after Run() completes).
    /// </summary>
    public int InitExpressionsArrayId => _initExpressionsArrayId;

    /// <summary>
    /// Gets the layout resolver (available after Run() completes).
    /// </summary>
    public LayoutResolver? LayoutResolver => _layoutResolver;

    /// <summary>
    /// Gets the localId to objectId mapping (available after Run() completes).
    /// </summary>
    public IReadOnlyDictionary<string, int> LocalIdToObjectId => _localIdToObjectId;

    /// <summary>
    /// Executes all cold start phases in order.
    /// Does NOT execute init expressions - that should be done separately after Run() completes.
    /// </summary>
    public void Run()
    {
        Console.WriteLine("\n=== Cold Start Loader ===\n");

        // Phase 0: Preflight Validation
        RunPhase("Phase 0: Preflight Validation", () =>
        {
            var phase = new Phase0_Preflight(_bundle);
            phase.Run();
        });

        // Phase 1: Symbol Interning
        RunPhase("Phase 1: Symbol Interning", () =>
        {
            var phase = new Phase1_SymbolInterning(_bundle, _allocator);
            phase.Run();

            // TODO: Store optimized selectors in heap manager when needed for bytecode disassembly
            // For now, this will be handled differently since the new heap manager doesn't have high-level methods
            var optimizedSelectorNames = _bundle.OptimizedSelectors
                .Select(index => _bundle.Symbols[index])
                .ToArray();
            // _heapManager doesn't have SetOptimizedSelectors - will implement differently
        });

        // Phase 2: Class Shells
        RunPhase("Phase 2: Class Shells", () =>
        {
            var phase = new Phase2_ClassShells(_bundle, _allocator, _localIdToObjectId);
            phase.Run();
        });

        // Phase 3: Metaclass Wiring
        RunPhase("Phase 3: Metaclass Wiring", () =>
        {
            var primitives = new ColdStartPrimitives(_heapManager);
            var phase = new Phase3_MetaclassWiring(_bundle, _heapManager, primitives, _localIdToObjectId);
            phase.Run();
        });

        // Phase 4: Class Resolution
        RunPhase("Phase 4: Class Resolution", () =>
        {
            var primitives = new ColdStartPrimitives(_heapManager);
            var phase = new Phase4_ClassResolution(_bundle, _heapManager, primitives, _localIdToObjectId, _bundle.Symbols, _bundle.Strings);
            phase.Run();
        });

        // Phase 5: Layout Computation
        RunPhase("Phase 5: Layout Computation", () =>
        {
            _layoutResolver = new LayoutResolver();
            _layoutResolver.ComputeLayouts(_bundle.Classes, _bundle.Symbols);
        });

        // Phase 6: Object Allocation (includes nil/true/false singletons)
        RunPhase("Phase 6: Object Allocation", () =>
        {
            var phase = new Phase6_ObjectAllocation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // === MULTI-PASS INITIALIZATION PHASES ===
        // Phase X: Class Reference Initialization (after nil exists)
        RunPhase("Phase X: Class Reference Initialization", () =>
        {
            var phase = new PhaseX_ClassReferenceInitialization(_bundle, _allocator, _localIdToObjectId);
            phase.Run();
        });

        // Phase Y: Instance Variable Initialization (after class refs set)
        RunPhase("Phase Y: Instance Variable Initialization", () =>
        {
            var phase = new PhaseY_InstanceVariableInitialization(_bundle, _allocator, _localIdToObjectId);
            phase.Run();
        });

        // === FULL ALLOCATION + INITIALIZATION PHASES ===
        // Phase 7: Method Dictionary Allocation (CRITICAL - was missing!)
        RunPhase("Phase 7: Method Dictionary Allocation", () =>
        {
            var phase = new Phase7_MethodDictionaryAllocation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // Phase 8: Method Installation
        RunPhase("Phase 8: Method Installation", () =>
        {
            var phase = new Phase8_MethodInstallation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols, _bundle.Strings);
            phase.Run();
        });

        // Phase 9: Method Dictionary Installation (CRITICAL - was missing!)
        RunPhase("Phase 9: Method Dictionary Installation", () =>
        {
            var phase = new Phase9_MethodDictionaryInstallation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // Phase 10: Patch Application
        RunPhase("Phase 10: Patch Application", () =>
        {
            var phase = new Phase10_PatchApplication(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // Phase 11: Prepare Init Expressions (but don't execute them yet)
        RunPhase("Phase 11: Prepare Init Expressions", () =>
        {
            var phase = new Phase11_InitExecution(_bundle, _allocator, _localIdToObjectId);
            _initExpressionsArrayId = phase.Run();
        });

        // Phase 12: USTable Population
        RunPhase("Phase 12: USTable Population", () =>
        {
            var phase = new Phase12_USTablePopulation(_bundle, _allocator);
            phase.Run();
        });

        // Phase 13: Global Variable Installation
        RunPhase("Phase 13: Global Variable Installation", () =>
        {
            var phase = new Phase13_GlobalVariableInstallation(_bundle, _allocator, _localIdToObjectId);
            phase.Run();
        });

        // Phase 14: Comprehensive Heap Validation
        RunPhase("Phase 14: Comprehensive Heap Validation", () =>
        {
            var phase = new Phase14_ComprehensiveValidation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        Console.WriteLine("\n=== Cold Start Complete ===");
        Console.WriteLine($"Total objects created: {_localIdToObjectId.Count}");

        // Set GC roots for garbage collection
        RunPhase("Setting GC Roots", () =>
        {
            // Get USTable from GlobalRegistry (temporary direct root until SystemDictionary integration complete)
            int ustableId = 0;
            GlobalRegistry.TryGet("USTable", out ustableId);

            // Get all class objects from localIdToObjectId (both instance classes and metaclasses)
            var classObjectIds = new List<int>();
            foreach (var cls in _bundle.Classes)
            {
                if (_localIdToObjectId.TryGetValue(cls.LocalId, out int classObjectId))
                {
                    classObjectIds.Add(classObjectId);
                }
            }

            // Create comprehensive GC roots array per Smalltalk-80 architecture:
            // - Core singletons (nil, false, true) - required for bytecode operations
            // - SystemDictionary - contains all globals 
            // - USTable - temporary direct root (should be via SystemDictionary when classes are installed)
            // - All class objects - ensures method dictionaries and methods are reachable
            var gcRootsList = new List<SmalltalkOOP>
            {
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.FalseObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.TrueObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.SystemDictionaryId),
                SmalltalkOOP.FromObjectTableIndex(ustableId), // Temporary: direct root until SystemDictionary integration
                SmalltalkOOP.FromObjectTableIndex(_initExpressionsArrayId) // Temporary: keep init expressions reachable during GC verification
            };

            // Add all class objects as GC roots (ensures method dictionaries and methods are reachable)
            foreach (int classId in classObjectIds)
            {
                gcRootsList.Add(SmalltalkOOP.FromObjectTableIndex(classId));
            }

            Console.WriteLine($"DEBUG: Setting {gcRootsList.Count} GC roots ({classObjectIds.Count} classes + 6 core objects)");
            _heapManager.SetGCRoots(gcRootsList.ToArray());
        });

        // Run GC to verify all objects are reachable
        Console.Write("Garbage Collection Verification... ");
        {
            var stats = _heapManager.GarbageCollect();

            if (stats.CollectedObjects > 0)
            {
                Console.WriteLine("✗ FAILED");
                throw new InvalidOperationException(
                    $"COLD START BUG: {stats.CollectedObjects} objects were collected during GC! " +
                    $"All {stats.TotalObjects} objects should be reachable from GC roots array.");
            }
            else
            {
                Console.WriteLine("✓");
                Console.WriteLine($"  Objects: {stats.TotalObjects} total, {stats.LiveObjects} marked, {stats.CollectedObjects} collected");
                Console.WriteLine("  ✓ All objects reachable - no garbage collected");
            }
        }

        // Phase 14: Bytecode Validation (Optional - temporarily disabled during heap manager migration)
        // TODO: Re-enable after migrating bytecode validation to new heap manager API
        bool runValidation = false; // Environment.GetEnvironmentVariable("SKIP_BYTECODE_VALIDATION") != "1";
        if (runValidation)
        {
            Console.WriteLine("\n=== Optional Bytecode Validation ===\n");
            Console.Write("Phase 14: Bytecode Validation... ");
            Console.WriteLine("⚠️ SKIPPED (temporarily disabled during heap manager migration)");
        }
    }

    private void RunPhase(string phaseName, Action phaseAction)
    {
        Console.Write($"{phaseName}... ");
        try
        {
            phaseAction();
            Console.WriteLine("✓");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAILED");
            Console.WriteLine($"  Error: {ex.Message}");
            throw new InvalidOperationException($"Cold start failed at {phaseName}", ex);
        }
    }
}
