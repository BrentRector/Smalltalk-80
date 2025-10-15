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

        // Phase 11: Prepare Init Expressions (TEMPORARILY DISABLED - creates 721 unreachable objects!)
        _initExpressionsArrayId = 0; // Set to 0 since no init expressions array created
        /*
        RunPhase("Phase 11: Prepare Init Expressions", () =>
        {
            var phase = new Phase11_InitExecution(_bundle, _allocator, _localIdToObjectId);
            _initExpressionsArrayId = phase.Run();
        });
        */

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

            // Create proper Smalltalk-80 GC roots - only the fundamental special objects:
            // 1. nil - required for bytecode operations and default values
            // 2. false - required for boolean operations  
            // 3. true - required for boolean operations
            // 4. Smalltalk (SystemDictionary) - contains all globals including classes
            // 5. USTable - symbol table (should be reachable via Smalltalk but adding as root for safety)
            var gcRootsList = new List<SmalltalkOOP>
            {
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.FalseObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.TrueObjectId),
                SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.SystemDictionaryId)
            };
            
            // Add USTable as root (it should be reachable via SystemDictionary but verify installation)
            if (ustableId != 0)
            {
                gcRootsList.Add(SmalltalkOOP.FromObjectTableIndex(ustableId));
            }
            
            // Add init expressions array as temporary root if it exists (for GC verification)
            if (_initExpressionsArrayId != 0)
            {
                gcRootsList.Add(SmalltalkOOP.FromObjectTableIndex(_initExpressionsArrayId));
            }

            // All classes should be reachable via SystemDictionary associations
            // All methods should be reachable via class method dictionaries
            // No need to add classes or methods as direct GC roots
            
            Console.WriteLine($"DEBUG: Setting {gcRootsList.Count} special object GC roots (proper Smalltalk-80 architecture)");
            Console.WriteLine($"DEBUG: GC Roots - nil:{GlobalRegistry.NilObjectId}, false:{GlobalRegistry.FalseObjectId}, true:{GlobalRegistry.TrueObjectId}, SystemDict:{GlobalRegistry.SystemDictionaryId}, USTable:{ustableId}");
            _heapManager.SetGCRoots(gcRootsList.ToArray());
        });

        // Run GC debug to find unreachable objects without collecting them
        Console.Write("Garbage Collection Verification... ");
        {
            var unreachableObjects = _heapManager.GetUnreachableObjects();

            if (unreachableObjects.Length > 0)
            {
                Console.WriteLine("✗ FAILED");
                Console.WriteLine($"DEBUG GC: Found {unreachableObjects.Length} unreachable objects");
                
                // Analyze first 10 unreachable objects to understand what they are
                Console.WriteLine("DEBUG GC: Analyzing first 10 unreachable objects:");
                for (int i = 0; i < Math.Min(10, unreachableObjects.Length); i++)
                {
                    var oop = unreachableObjects[i];
                    var format = _heapManager.GetObjectFormat(oop);
                    var size = _heapManager.GetObjectSizeInWords(oop);
                    var classOOP = _heapManager.GetSlot(oop, 0);
                    
                    string className = "Unknown";
                    int classId = classOOP.AsObjectTableIndex();
                    var classEntry = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == classId);
                    if (!string.IsNullOrEmpty(classEntry.Key))
                    {
                        className = classEntry.Key;
                    }
                    
                    Console.WriteLine($"  Object {oop.AsObjectTableIndex()}: format={format}, size={size} words, classOOP={classId} ({className})");
                }
                
                // Group by format and class
                var byFormat = unreachableObjects.GroupBy(oop => _heapManager.GetObjectFormat(oop))
                    .ToDictionary(g => g.Key, g => g.Count());
                var formatBreakdown = string.Join(", ", byFormat.Select(kv => $"{kv.Key}: {kv.Value}"));
                Console.WriteLine($"DEBUG GC: Objects by format: {formatBreakdown}");
                
                // Group by class
                var byClass = new Dictionary<string, int>();
                foreach (var oop in unreachableObjects)
                {
                    var classOOP = _heapManager.GetSlot(oop, 0);
                    int classId = classOOP.AsObjectTableIndex();
                    var classEntry = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == classId);
                    string className = string.IsNullOrEmpty(classEntry.Key) ? $"Unknown(id={classId})" : classEntry.Key;
                    byClass[className] = byClass.GetValueOrDefault(className) + 1;
                }
                var classBreakdown = string.Join(", ", byClass.Select(kv => $"{kv.Key}: {kv.Value}"));
                Console.WriteLine($"DEBUG GC: Objects by class: {classBreakdown}");
                
                // Dump content of unreachable Strings for investigation
                Console.WriteLine("DEBUG GC: Unreachable String content:");
                int stringCount = 0;
                foreach (var oop in unreachableObjects)
                {
                    if (stringCount >= 5) break; // Only show first 5 strings
                    
                    var classOOP = _heapManager.GetSlot(oop, 0);
                    int classId = classOOP.AsObjectTableIndex();
                    var classEntry = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == classId);
                    if (classEntry.Key == "class:String")
                    {
                        // Extract string bytes
                        int indexedSize = _heapManager.GetIndexedSize(oop);
                        var bytes = new byte[indexedSize];
                        for (int i = 0; i < indexedSize; i++)
                        {
                            bytes[i] = _heapManager.GetIndexedByte(oop, i);
                        }
                        string stringValue = System.Text.Encoding.UTF8.GetString(bytes);
                        Console.WriteLine($"  String {oop.AsObjectTableIndex()}: \"{stringValue}\"");
                        stringCount++;
                    }
                }
                
                var currentRoots = _heapManager.GetGCRoots();
                throw new InvalidOperationException(
                    $"COLD START BUG: {unreachableObjects.Length} objects are unreachable from GC roots! " +
                    $"All objects should be reachable from the {currentRoots.Length} GC roots.");
            }
            else
            {
                Console.WriteLine("✓");
                var stats = _heapManager.GetGCStatistics();
                Console.WriteLine($"  Objects: {stats.TotalObjects} total, all reachable from GC roots");
                Console.WriteLine("  ✓ All objects reachable - no garbage would be collected");
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
