using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Attributes;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Orchestrates the cold start loader.
/// Executes phases in order to build up the Smalltalk-80 object memory from the JSON bundle.
/// Bytecode Validation is optional and runs after GC verification; can be skipped by setting SKIP_BYTECODE_VALIDATION=1.
///
/// COLD START ONLY: This class builds the heap from ColdStartBundle and modifies registries.
/// Runtime code must NOT use this class.
/// </summary>
[ColdStartOnly(Reason = "Builds heap from ColdStartBundle, modifies frozen registries")]
public class Loader
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId = new();
    private readonly HashSet<(SmalltalkOOP objectOOP, int fieldIndex)> _initializedFields = new();
    private LayoutResolver? _layoutResolver;
    private int _initExpressionsArrayId = 0;

    public Loader(ColdStartBundle bundle, ISmalltalkHeapManager heapManager)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _allocator = new SemanticAllocator(heapManager, bundle);
    }

    /// <summary>
    /// Gets the layout resolver (available after Run() completes).
    /// </summary>
    public LayoutResolver? LayoutResolver => _layoutResolver;

    /// <summary>
    /// Gets the localId to OOP mapping (available after Run() completes).
    /// </summary>
    public IReadOnlyDictionary<string, SmalltalkOOP> LocalIdToObjectId => _localIdToObjectId;

    /// <summary>
    /// Gets the semantic allocator.
    /// </summary>
    public SemanticAllocator Allocator => _allocator;

    /// <summary>
    /// Gets the init expressions array ID (available after Run() completes).
    /// Returns 0 if there are no init expressions.
    /// </summary>
    public int InitExpressionsArrayId => _initExpressionsArrayId;

    /// <summary>
    /// Builds VM metadata from the loaded cold start data.
    /// Must be called after Run() completes.
    /// </summary>
    public VMMetadata BuildVMMetadata()
    {
        if (_layoutResolver == null)
            throw new InvalidOperationException("Cannot build VM metadata before cold start completes");

        int thisClassIdx = _layoutResolver.GetFieldIndex("class:Metaclass", "thisClass");
        Console.WriteLine($"VMMetadata: MetaclassThisClassFieldIndex = {thisClassIdx}");

        return new VMMetadata
        {
            ClassNameFieldIndex = _layoutResolver.GetFieldIndex("class:Class", "name"),
            ClassSuperclassFieldIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass"),
            ClassMethodDictFieldIndex = _layoutResolver.GetFieldIndex("class:Class", "methodDict"),
            ClassFormatFieldIndex = _layoutResolver.GetFieldIndex("class:Class", "format"),
            MetaclassThisClassFieldIndex = thisClassIdx,
            MethodDictTallyFieldIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "tally"),
            MethodDictKeysFieldIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "keys"),
            MethodDictValuesFieldIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "values"),
            ClassClassOOP = GlobalRegistry.Get("class:Class"),
            MetaclassClassOOP = GlobalRegistry.Get("class:Metaclass"),
            SymbolClassOOP = GlobalRegistry.Get("class:Symbol"),
            StringClassOOP = GlobalRegistry.Get("class:String"),
            ArrayClassOOP = GlobalRegistry.Get("class:Array"),
            CompiledMethodClassOOP = GlobalRegistry.Get("class:CompiledMethod"),
            SmallIntegerClassOOP = GlobalRegistry.Get("class:SmallInteger")
        };
    }

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

        // Configure heap manager with class name field index
        // This is needed for GetClassName() to work properly
        if (_heapManager is SmalltalkHeapManager concreteHeapManager)
        {
            int nameFieldIndex = LoaderPhases.ClassHierarchyUtils.FindInstanceVariableIndex("Class", "name", _bundle);
            if (nameFieldIndex >= 0)
            {
                concreteHeapManager.SetClassNameFieldIndex(nameFieldIndex);
            }
        }

        // Phase 1: Symbol Interning
        RunPhase("Phase 1: Symbol Interning", () =>
        {
            var phase = new Phase1_SymbolInterning(_bundle, _allocator);
            phase.Run();

            // Freeze symbol registry - no more symbol registration after this point
            SymbolRegistry.Freeze();

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
            var phase = new Phase3_MetaclassWiring(_bundle, _heapManager, primitives, _localIdToObjectId, _initializedFields);
            phase.Run();
        });

        // Phase 4: Class Resolution
        RunPhase("Phase 4: Class Resolution", () =>
        {
            var primitives = new ColdStartPrimitives(_heapManager);
            var phase = new Phase4_ClassResolution(_bundle, _heapManager, primitives, _localIdToObjectId, _bundle.Symbols, _bundle.Strings, _initializedFields);
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
            var phase = new PhaseX_ClassReferenceInitialization(_bundle, _allocator, _localIdToObjectId, _heapManager);
            phase.Run();
        });

        // Phase Y: Instance Variable Initialization (after class refs set)
        RunPhase("Phase Y: Instance Variable Initialization", () =>
        {
            var phase = new PhaseY_InstanceVariableInitialization(_bundle, _allocator, _localIdToObjectId, _heapManager, _initializedFields);
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

        // Phase 11: USTable Population
        RunPhase("Phase 11: USTable Population", () =>
        {
            var phase = new Phase11_USTablePopulation(_bundle, _allocator);
            phase.Run();
        });

        // Phase 12: Global Variable Installation
        RunPhase("Phase 12: Global Variable Installation", () =>
        {
            var phase = new Phase12_GlobalVariableInstallation(_bundle, _allocator, _localIdToObjectId);
            phase.Run();
        });

        // Phase 13: Comprehensive Heap Validation
        Console.WriteLine("Phase 13: Comprehensive Heap Validation...");
        try
        {
            var phase = new Phase13_ComprehensiveValidation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
            phase.Run();
            // Note: Phase prints its own ✓ after validation summary
        }
        catch (Exception ex)
        {
            Console.WriteLine("✗ FAILED");
            Console.WriteLine($"  Error: {ex.Message}");
            throw new InvalidOperationException("Cold start failed at Phase 13: Comprehensive Heap Validation", ex);
        }

        // Phase 14: GC Root Setup
        Console.WriteLine("Phase 14: GC Root Setup...");
        {
            // Create proper Smalltalk-80 GC roots - only the fundamental special objects:
            // USTable is reachable via SystemDictionary, no need for direct root
            var gcRootsList = new List<(string name, SmalltalkOOP oop)>
            {
                ("nil", SpecialObjectsArray.Instance.Nil),
                ("false", SpecialObjectsArray.Instance.False),
                ("true", SpecialObjectsArray.Instance.True),
                ("Smalltalk", GlobalRegistry.SystemDictionaryOOP)
            };

            // Set as GC roots
            var gcRoots = gcRootsList.Select(r => r.oop).ToArray();
            _heapManager.SetGCRoots(gcRoots);
            
            // Display GC roots
            Console.WriteLine($"  GC Roots ({gcRootsList.Count} objects):");
            foreach (var (name, oop) in gcRootsList)
            {
                var className = _heapManager.GetClassName(oop);
                Console.WriteLine($"    {name} ({oop}, class={className})");
            }
        }
        Console.WriteLine("✓");

        // Phase 15: GC Verification - Final test that cold start is 100% correct
        Console.WriteLine("Phase 15: GC Verification...");
        var gcStats = _heapManager.GarbageCollect();
        
        // Print GC statistics
        Console.WriteLine($"  GC Results: {gcStats.CollectedObjects}/{gcStats.TotalObjects} objects collected " +
                         $"({(gcStats.CollectedObjects * 100.0 / gcStats.TotalObjects):F1}% efficiency) " +
                         $"in {gcStats.TotalTimeMs:F2}ms " +
                         $"(Mark: {gcStats.MarkTimeMs:F2}ms, Sweep: {gcStats.SweepTimeMs:F2}ms), " +
                         $"{gcStats.EstimatedBytesFreed:N0} bytes freed");
        Console.WriteLine($"  Object table size: {gcStats.TotalObjects}");
        Console.WriteLine($"  Active objects: {gcStats.LiveObjects}");
        
        // Cold start fails if ANY objects were collected
        if (gcStats.CollectedObjects > 0)
        {
            Console.WriteLine($"\n✗ COLD START FAILED: GC collected {gcStats.CollectedObjects} objects!");
            Console.WriteLine($"  All objects must be reachable from GC roots for cold start to pass.");
            throw new InvalidOperationException(
                $"COLD START BUG: GC collected {gcStats.CollectedObjects} objects. " +
                $"All objects should be reachable from GC roots.");
        }
        
        Console.WriteLine("✓");

        // Freeze global registry - no more registrations after this point
        GlobalRegistry.Freeze();

        Console.WriteLine("\n=== Cold Start Complete ===");
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
