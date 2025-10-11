using Smalltalk80.Interpreter.LoaderPhases;
using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Orchestrates the cold start loader.
/// Executes phases in order to build up the Smalltalk-80 object memory from the JSON bundle.
/// Bytecode Validation is optional and runs after GC verification; can be skipped by setting SKIP_BYTECODE_VALIDATION=1.
/// </summary>
public class Loader
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId = new();
    private LayoutResolver? _layoutResolver;
    private int _initExpressionsArrayId;

    public Loader(ColdStartBundle bundle, ISmalltalkHeap heap)
    {
        _bundle = bundle;
        _heap = heap;
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
            var phase = new Phase1_SymbolInterning(_bundle, _heap);
            phase.Run();

            // Also set optimized selectors for bytecode disassembly
            var optimizedSelectorNames = _bundle.OptimizedSelectors
                .Select(index => _bundle.Symbols[index])
                .ToArray();
            _heap.SetOptimizedSelectors(optimizedSelectorNames);
        });

        // Phase 2: Class Shells
        RunPhase("Phase 2: Class Shells", () =>
        {
            var phase = new Phase1_ClassShells(_bundle, _heap, _localIdToObjectId);
            phase.Run();
        });

        // Phase 3: Metaclass Wiring
        RunPhase("Phase 3: Metaclass Wiring", () =>
        {
            var phase = new Phase2_MetaclassWiring(_bundle, _heap, _localIdToObjectId);
            phase.Run();
        });

        // Phase 4: Class Resolution
        RunPhase("Phase 4: Class Resolution", () =>
        {
            var phase = new Phase3_ClassResolution(_bundle, _heap, _localIdToObjectId, _bundle.Symbols, _bundle.Strings);
            phase.Run();
        });

        // Phase 5: Layout Computation
        RunPhase("Phase 5: Layout Computation", () =>
        {
            _layoutResolver = new LayoutResolver();
            _layoutResolver.ComputeLayouts(_bundle.Classes, _bundle.Symbols);
        });

        // Phase 6: Object Allocation
        RunPhase("Phase 6: Object Allocation", () =>
        {
            var phase = new Phase5_ObjectAllocation(_bundle, _heap, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // Phase 7: Method Installation
        RunPhase("Phase 7: Method Installation", () =>
        {
            var phase = new Phase6_MethodInstallation(_bundle, _heap, _localIdToObjectId, _bundle.Symbols, _bundle.Strings);
            phase.Run();
        });

        // Phase 8: Patch Application
        RunPhase("Phase 8: Patch Application", () =>
        {
            var phase = new Phase7_PatchApplication(_bundle, _heap, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
        });

        // Phase 9: Prepare Init Expressions (but don't execute them yet)
        RunPhase("Phase 9: Prepare Init Expressions", () =>
        {
            var phase = new Phase8_InitExecution(_bundle, _heap, _localIdToObjectId);
            _initExpressionsArrayId = phase.Run();
        });

        // Phase 10: USTable Population
        RunPhase("Phase 10: USTable Population", () =>
        {
            var phase = new Phase9_USTablePopulation(_bundle, _heap);
            phase.Run();
        });

        Console.WriteLine("\n=== Cold Start Complete ===");
        Console.WriteLine($"Total objects created: {_localIdToObjectId.Count}");

        // Create Special Objects Array for GC roots
        RunPhase("Creating Special Objects Array", () =>
        {
            // Get USTable from GlobalRegistry
            int ustableId = 0;
            GlobalRegistry.TryGet("USTable", out ustableId);

            var specialObjects = new int[]
            {
                GlobalRegistry.NilObjectId,
                GlobalRegistry.FalseObjectId,
                GlobalRegistry.TrueObjectId,
                GlobalRegistry.SystemDictionaryId,
                0, // ProcessorScheduler - will be created later when process system is initialized
                ustableId, // USTable contains all symbols
                _initExpressionsArrayId // Temporary: keep init expressions reachable during GC verification
            };
            _heap.SetSpecialObjectsArray(specialObjects);
        });

        // Run GC to verify all objects are reachable
        Console.Write("Garbage Collection Verification... ");
        {
            int collected = _heap.GarbageCollect();
            var stats = _heap.GetGCStatistics();

            if (collected > 0)
            {
                Console.WriteLine("✗ FAILED");
                throw new InvalidOperationException(
                    $"COLD START BUG: {collected} objects were collected during GC! " +
                    $"All {stats.TotalObjects} objects should be reachable from special objects array.");
            }
            else
            {
                Console.WriteLine("✓");
                Console.WriteLine($"  Objects: {stats.TotalObjects} total, {stats.LiveObjects} marked, {collected} collected");
                Console.WriteLine("  ✓ All objects reachable - no garbage collected");
            }
        }

        // Phase 12: Bytecode Validation (Optional - runs after GC verification)
        // This phase may allocate temporary objects for validation, so it runs after GC check
        // Note: Phase 12 prints its own checkmark inline with detailed output
        bool runValidation = Environment.GetEnvironmentVariable("SKIP_BYTECODE_VALIDATION") != "1";
        if (runValidation)
        {
            Console.WriteLine("\n=== Optional Bytecode Validation ===\n");
            Console.Write("Phase 12: Bytecode Validation... ");
            try
            {
                var phase = new Phase12_BytecodeValidation(_bundle, _heap, _localIdToObjectId, _bundle.Symbols.ToArray(), _layoutResolver!);
                phase.Run();
                // Note: Phase 12 prints ✓ itself at the start of its output
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ FAILED");
                Console.WriteLine($"  Error: {ex.Message}");
                throw new InvalidOperationException("Cold start failed at Phase 12: Bytecode Validation", ex);
            }
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
