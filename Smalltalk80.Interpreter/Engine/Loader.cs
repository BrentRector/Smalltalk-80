using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Attributes;
using System.Diagnostics;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// New 6-phase cold start loader orchestrator.
/// Implements a streamlined architecture that consolidates related operations
/// and establishes clear data flow between phases.
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
    private readonly ColdstartReport _report;
    private readonly string _bundlePath;
    private readonly DebugOptions _debugOptions;

    /// <summary>
    /// Maps localId strings to allocated OOP values.
    /// Shared across all phases for object resolution.
    /// </summary>
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId = new();

    /// <summary>
    /// Tracks which fields have been initialized to prevent double-initialization.
    /// Shared across phases that set instance variables.
    /// </summary>
    private readonly HashSet<(SmalltalkOOP objectOOP, int fieldIndex)> _initializedFields = new();

    /// <summary>
    /// Gets the layout resolver (available after Phase 2 completes).
    /// </summary>
    public LayoutResolver? LayoutResolver { get; private set; }

    /// <summary>
    /// Gets the localId to OOP mapping (available after Run() completes).
    /// </summary>
    public IReadOnlyDictionary<string, SmalltalkOOP> LocalIdToObjectId => _localIdToObjectId;

    /// <summary>
    /// Gets the semantic allocator.
    /// </summary>
    public SemanticAllocator Allocator => _allocator;

    /// <summary>
    /// Gets the coldstart execution report.
    /// </summary>
    public ColdstartReport Report => _report;

    // InitExpressionsArrayOOP removed - accessible via SpecialObjectsArray.Instance.InitExpressionMethods

    /// <summary>
    /// Initializes a new instance of the Loader class.
    /// </summary>
    /// <param name="bundle">The cold start bundle containing the Smalltalk image data.</param>
    /// <param name="heapManager">The heap manager for allocating objects.</param>
    /// <param name="bundlePath">Optional path to the bundle file for diagnostics.</param>
    /// <param name="debugOptions">Optional debug options for diagnostic output.</param>
    public Loader(ColdStartBundle bundle, ISmalltalkHeapManager heapManager, string? bundlePath = null, DebugOptions? debugOptions = null)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
        _allocator = new SemanticAllocator(heapManager, bundle);
        _bundlePath = bundlePath ?? "coldstart.json";
        _debugOptions = debugOptions ?? DebugOptions.None;

        // Initialize SpecialObjectsArray (will be populated during Phase 3)
        SpecialObjectsArray.Initialize();

        // Initialize the report with bundle metadata
        _report = new ColdstartReport
        {
            BundleVersion = bundle.Metadata.Version,
            BundleHash = ComputeBundleHash(),
            TimestampUtc = DateTime.UtcNow
        };

        // Set initial metrics
        _report.Metrics.SymbolCount = bundle.Symbols.Length;
        _report.Metrics.ClassCount = bundle.Classes.Length;
        _report.Metrics.MethodCount = bundle.Methods.Length;
    }

    /// <summary>
    /// Executes all cold start phases in order.
    /// Does NOT execute init expressions - that should be done separately after Run() completes.
    /// </summary>
    public void Run()
    {
        Console.WriteLine("\n=== Cold Start Loader (New 6-Phase Architecture) ===\n");

        try
        {
            // Configure heap manager with class name field index for GetClassName() to work
            ConfigureHeapManager();

        // Phase 1: Preflight + Symbol Interning
        // Validates bundle and creates all Symbol objects
        RunPhase("Phase 1: Preflight + Symbol Interning", () =>
        {
            var phase = new Phase1_PreflightAndSymbols(_bundle, _allocator, _report);
            phase.Run();

            // Freeze symbol registry - no more symbol registration after this point
            SymbolRegistry.Freeze();
        });

        // Phase 2: Class Infrastructure
        // Creates class/metaclass shells, wires inheritance, resolves layouts
        Phase2_ClassInfrastructure? phase2 = null;
        RunPhase("Phase 2: Class Infrastructure", () =>
        {
            phase2 = new Phase2_ClassInfrastructure(_bundle, _allocator, _heapManager, _localIdToObjectId, _initializedFields, _report);
            phase2.Run();
            LayoutResolver = phase2.LayoutResolver;
        });

        // Phase 3: Object Allocation
        // Allocates all remaining objects (globals, singletons, strings, etc.)
        RunPhase("Phase 3: Object Allocation", () =>
        {
            if (LayoutResolver == null)
                throw new InvalidOperationException("LayoutResolver not initialized after Phase 2");

            var phase = new Phase3_ObjectAllocation(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report);
            phase.Run();
        });

        // Phase 4: Method Compilation
        // Compiles all methods, installs in dictionaries, prepares init expressions
        RunPhase("Phase 4: Method Compilation", () =>
        {
            if (LayoutResolver == null)
                throw new InvalidOperationException("LayoutResolver not initialized after Phase 2");

            var phase = new Phase4_MethodCompilation(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report);
            phase.Run();
            // Init expressions array is set in SpecialObjectsArray by Phase4
        });

        // Phase 5: Globals + Patches
        // Populates USTable, installs globals, applies patches
        RunPhase("Phase 5: Globals + Patches", () =>
        {
            if (LayoutResolver == null)
                throw new InvalidOperationException("LayoutResolver not initialized after Phase 2");

            var phase = new Phase5_GlobalsAndPatches(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report, _debugOptions);
            phase.Run();
        });

        // Phase 6: Validation + GC Verification
        // Comprehensive validation and GC verification to ensure cold start correctness
        RunPhase("Phase 6: Validation + GC Verification", () =>
        {
            if (LayoutResolver == null)
                throw new InvalidOperationException("LayoutResolver not initialized after Phase 2");

            var phase = new Phase6_ValidationAndGC(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _report);
            phase.Run();
        });

            // Freeze global registry - no more registrations after this point
            GlobalRegistry.Freeze();

            Console.WriteLine("\n=== Cold Start Complete ===\n");
        }
        catch (Exception ex)
        {
            // Create snapshot writer to capture failure details
            try
            {
                var snapshotWriter = new SnapshotWriter(_heapManager, _bundle, _localIdToObjectId);
                snapshotWriter.WriteSnapshot(_bundlePath, _report.Failure, ex);
            }
            catch
            {
                // Snapshot writing is best-effort, don't mask original exception
            }

            // Re-throw the original exception
            throw;
        }
    }

    /// <summary>
    /// Builds VM metadata from the loaded cold start data.
    /// Must be called after Run() completes.
    /// </summary>
    public VMMetadata BuildVMMetadata()
    {
        if (LayoutResolver == null)
            throw new InvalidOperationException("Cannot build VM metadata before cold start completes");

        int thisClassIdx = LayoutResolver.GetFieldIndex("class:Metaclass", "thisClass");

        var metadata = new VMMetadata
        {
            ClassNameFieldIndex = LayoutResolver.GetFieldIndex("class:Class", "name"),
            ClassSuperclassFieldIndex = LayoutResolver.GetFieldIndex("class:Class", "superclass"),
            ClassMethodDictFieldIndex = LayoutResolver.GetFieldIndex("class:Class", "methodDict"),
            ClassFormatFieldIndex = LayoutResolver.GetFieldIndex("class:Class", "format"),
            MetaclassThisClassFieldIndex = thisClassIdx,
            MethodDictTallyFieldIndex = LayoutResolver.GetFieldIndex("class:MethodDictionary", "tally"),
            MethodDictKeysFieldIndex = LayoutResolver.GetFieldIndex("class:MethodDictionary", "keys"),
            MethodDictValuesFieldIndex = LayoutResolver.GetFieldIndex("class:MethodDictionary", "values"),
            ClassClassOOP = GlobalRegistry.Get("class:Class"),
            MetaclassClassOOP = GlobalRegistry.Get("class:Metaclass"),
            SymbolClassOOP = GlobalRegistry.Get("class:Symbol"),
            StringClassOOP = GlobalRegistry.Get("class:String"),
            ArrayClassOOP = GlobalRegistry.Get("class:Array"),
            CompiledMethodClassOOP = GlobalRegistry.Get("class:CompiledMethod"),
            SmallIntegerClassOOP = GlobalRegistry.Get("class:SmallInteger"),
            BlockContextClassOOP = GlobalRegistry.Get("class:BlockContext")
        };

        return metadata;
    }

    /// <summary>
    /// Computes a simple hash of the bundle for tracking.
    /// Format: "symbolCount-classCount-methodCount"
    /// </summary>
    private string ComputeBundleHash()
    {
        return $"{_bundle.Symbols.Length}-{_bundle.Classes.Length}-{_bundle.Methods.Length}";
    }

    /// <summary>
    /// Configures the heap manager with the class name field index.
    /// This is needed for GetClassName() to work properly during cold start.
    /// </summary>
    private void ConfigureHeapManager()
    {
        if (_heapManager is SmalltalkHeapManager concreteHeapManager)
        {
            // Find the index of the 'name' field in Class
            int nameFieldIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "name", _bundle);
            if (nameFieldIndex >= 0)
            {
                concreteHeapManager.SetClassNameFieldIndex(nameFieldIndex);
            }
        }
    }

    /// <summary>
    /// Executes a phase with timing and error handling.
    /// </summary>
    /// <param name="phaseName">The name of the phase for display.</param>
    /// <param name="phaseAction">The action to execute for the phase.</param>
    private void RunPhase(string phaseName, Action phaseAction)
    {
        Console.Write($"{phaseName}... ");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            phaseAction();
            stopwatch.Stop();
            int durationMs = (int)stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"✓ ({durationMs}ms)");

            // Record phase duration in report
            _report.RecordPhaseDuration(phaseName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            int durationMs = (int)stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"✗ FAILED ({durationMs}ms)");
            Console.WriteLine($"  Error: {ex.Message}");

            // Record phase duration even on failure
            _report.RecordPhaseDuration(phaseName, durationMs);

            // Set failure information in report
            _report.SetFailure(phaseName, ex.Message);

            // Include inner exception details if present
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }

            throw new InvalidOperationException($"Cold start failed at {phaseName}", ex);
        }
    }

    /// <summary>
    /// Runs a GC checkpoint to verify all objects are reachable at this point in cold start.
    /// NOTE: This is READ-ONLY - it reports unreachable objects but does NOT collect them.
    /// </summary>
    /// <param name="checkpointName">Name of the checkpoint for diagnostics.</param>
    private void RunGCCheckpoint(string checkpointName)
    {
        // Setup GC roots
        var gcRootsList = new List<SmalltalkOOP>();

        // Add fundamental sentinels if they exist
        if (SpecialObjectsArray.Instance.Nil != default)
            gcRootsList.Add(SpecialObjectsArray.Instance.Nil);
        if (SpecialObjectsArray.Instance.True != default)
            gcRootsList.Add(SpecialObjectsArray.Instance.True);
        if (SpecialObjectsArray.Instance.False != default)
            gcRootsList.Add(SpecialObjectsArray.Instance.False);

        // Add Smalltalk SystemDictionary if it exists
        if (GlobalRegistry.SystemDictionaryOOP != default)
            gcRootsList.Add(GlobalRegistry.SystemDictionaryOOP);

        // Add init expression methods if they exist
        if (SpecialObjectsArray.Instance.InitExpressionMethods != default)
        {
            var initMethodsOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
            gcRootsList.Add(initMethodsOOP);
        }

        // Set GC roots and get unreachable objects (READ-ONLY - does not collect)
        _heapManager.SetGCRoots(gcRootsList.ToArray());
        var unreachableOOPs = _heapManager.GetUnreachableObjects();

        // Report results
        if (unreachableOOPs.Length == 0)
        {
            int totalObjects = _heapManager.GetActiveObjectCount();
            Console.WriteLine($"  [GC Checkpoint {checkpointName}] ✓ All {totalObjects} objects reachable");
        }
        else
        {
            Console.WriteLine($"  [GC Checkpoint {checkpointName}] ⚠ {unreachableOOPs.Length} unreachable objects:");

            // Group by class
            var byClass = unreachableOOPs
                .GroupBy(oop =>
                {
                    try
                    {
                        return _heapManager.GetClassName(oop);
                    }
                    catch
                    {
                        return "<error>";
                    }
                })
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var group in byClass.Take(10))  // Show top 10 classes
            {
                // Show first 5 OOPs from this class
                var sampleOOPs = group.Take(5).Select(oop => oop.ToString()).ToList();
                string sampleStr = sampleOOPs.Count > 0 ? $" (sample OOPs: {string.Join(", ", sampleOOPs)})" : "";
                Console.WriteLine($"    {group.Key}: {group.Count()} objects{sampleStr}");
            }

            if (byClass.Count > 10)
            {
                Console.WriteLine($"    ... and {byClass.Count - 10} more classes");
            }
        }
    }
}