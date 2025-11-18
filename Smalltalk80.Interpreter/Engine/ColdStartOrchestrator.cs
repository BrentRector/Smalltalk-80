using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Attributes;
using Smalltalk80.Interpreter.Core.Diagnostics;
using System.Diagnostics;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// 6-phase cold start orchestrator.
/// Coordinates the loading of a Smalltalk-80 image from ColdStartBundle into the heap.
/// Implements a streamlined architecture that consolidates related operations
/// and establishes clear data flow between phases.
///
/// COLD START ONLY: This class builds the heap from ColdStartBundle and modifies registries.
/// Runtime code must NOT use this class.
/// </summary>
[ColdStartOnly(Reason = "Builds heap from ColdStartBundle, modifies frozen registries")]
public class ColdStartOrchestrator : IColdStartLoader
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly ObjectGraphAllocator _allocator;
    private readonly ColdstartReport _report;
    private readonly string _bundlePath;
    private readonly IDiagnosticHub? _diagnostics;

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
    /// List of phases for inspection/testing.
    /// Populated during Run().
    /// </summary>
    private readonly List<IPhase> _phases = new();

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
    public ObjectGraphAllocator Allocator => _allocator;

    /// <summary>
    /// Gets the coldstart execution report.
    /// </summary>
    public ColdstartReport Report => _report;

    /// <summary>
    /// Gets all phases for inspection/testing.
    /// </summary>
    public IReadOnlyList<IPhase> Phases => _phases;

    // InitExpressionsArrayOOP removed - accessible via SpecialObjectsArray.Instance.InitExpressionMethods

    /// <summary>
    /// Initializes a new instance of the ColdStartOrchestrator class.
    /// </summary>
    /// <param name="bundle">The cold start bundle containing the Smalltalk image data.</param>
    /// <param name="heapManager">The heap manager for allocating objects.</param>
    /// <param name="bundlePath">Optional path to the bundle file for diagnostics.</param>
    /// <param name="diagnostics">Optional diagnostic hub for event emission.</param>
    public ColdStartOrchestrator(ColdStartBundle bundle, ISmalltalkHeapManager heapManager, string? bundlePath = null, IDiagnosticHub? diagnostics = null)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
        _allocator = new ObjectGraphAllocator(heapManager, bundle);
        _bundlePath = bundlePath ?? "coldstart.json";
        _diagnostics = diagnostics;

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
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "ColdStart",
            Message = "=== Cold Start Loader (New 6-Phase Architecture) ===",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        try
        {
            // Configure heap manager with class name field index for GetClassName() to work
            ConfigureHeapManager();

        // Clear any previous phases (for re-runs)
        _phases.Clear();

        // Phase 1: Preflight + Symbol Interning
        // Validates bundle and creates all Symbol objects
        var phase1 = new Phase1_PreflightAndSymbols(_bundle, _allocator, _report, _diagnostics);
        _phases.Add(phase1);
        RunPhase(phase1.Name, () =>
        {
            phase1.Run();

            // Freeze symbol registry - no more symbol registration after this point
            SymbolRegistry.Freeze();
        });

        // Phase 2: Class Infrastructure
        // Creates class/metaclass shells, wires inheritance, resolves layouts
        var phase2 = new Phase2_ClassInfrastructure(_bundle, _allocator, _heapManager, _localIdToObjectId, _initializedFields, _report, _diagnostics);
        _phases.Add(phase2);
        RunPhase(phase2.Name, () =>
        {
            phase2.Run();
            LayoutResolver = phase2.LayoutResolver;
        });

        // Phase 3: Object Allocation
        // Allocates all remaining objects (globals, singletons, strings, etc.)
        if (LayoutResolver == null)
            throw new InvalidOperationException("LayoutResolver not initialized after Phase 2");

        var phase3 = new Phase3_ObjectAllocation(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report, _diagnostics);
        _phases.Add(phase3);
        RunPhase(phase3.Name, () =>
        {
            phase3.Run();
        });

        // Phase 4: Method Compilation
        // Compiles all methods, installs in dictionaries, prepares init expressions
        var phase4 = new Phase4_MethodCompilation(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report, _diagnostics);
        _phases.Add(phase4);
        RunPhase(phase4.Name, () =>
        {
            phase4.Run();
            // Init expressions array is set in SpecialObjectsArray by Phase4
        });

        // Phase 5: Globals + Patches
        // Populates USTable, installs globals, applies patches
        var phase5 = new Phase5Orchestrator(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _initializedFields, _report, _diagnostics);
        _phases.Add(phase5);
        RunPhase(phase5.Name, () =>
        {
            phase5.Run();
        });

        // Phase 6: Validation + GC Verification
        // Comprehensive validation and GC verification to ensure cold start correctness
        var phase6 = new Phase6_ValidationAndGC(_bundle, _allocator, _heapManager, LayoutResolver, _localIdToObjectId, _report, _diagnostics);
        _phases.Add(phase6);
        RunPhase(phase6.Name, () =>
        {
            phase6.Run();
        });

            // Freeze global registry - no more registrations after this point
            GlobalRegistry.Freeze();

            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "ColdStart",
                Message = "=== Cold Start Complete ===",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
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
        // Extract phase number from name (e.g., "Phase 1: ..." -> 1)
        int phaseNumber = 0;
        if (phaseName.StartsWith("Phase ") && phaseName.Length > 6)
        {
            int colonIndex = phaseName.IndexOf(':');
            if (colonIndex > 6)
            {
                int.TryParse(phaseName.Substring(6, colonIndex - 6), out phaseNumber);
            }
        }

        _diagnostics?.Emit(new PhaseStartEvent
        {
            PhaseNumber = phaseNumber,
            PhaseName = phaseName,
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            phaseAction();
            stopwatch.Stop();
            int durationMs = (int)stopwatch.ElapsedMilliseconds;

            _diagnostics?.Emit(new PhaseEndEvent
            {
                PhaseNumber = phaseNumber,
                PhaseName = phaseName,
                Duration = stopwatch.Elapsed,
                Success = true,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });

            // Record phase duration in report
            _report.RecordPhaseDuration(phaseName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            int durationMs = (int)stopwatch.ElapsedMilliseconds;

            _diagnostics?.Emit(new PhaseEndEvent
            {
                PhaseNumber = phaseNumber,
                PhaseName = phaseName,
                Duration = stopwatch.Elapsed,
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });

            // Record phase duration even on failure
            _report.RecordPhaseDuration(phaseName, durationMs);

            // Set failure information in report
            _report.SetFailure(phaseName, ex.Message);

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