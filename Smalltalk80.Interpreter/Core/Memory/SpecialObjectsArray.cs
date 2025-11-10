namespace Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// The Special Objects Array - GC roots for the Smalltalk-80 Virtual Machine.
/// Per Smalltalk-80 Blue Book specification (Goldberg &amp; Robson, 1983).
///
/// All objects in the system must be reachable from these 5 minimal roots:
///
/// 1. nil, false, true - Singletons required for hardwired bytecode references
///    (pushNil, pushTrue, pushFalse bytecodes require direct object pointers)
///
/// 2. Smalltalk (SystemDictionary instance) - Root of ALL classes and globals
///    Reachability chain:
///    - Smalltalk at: #Symbol → Symbol class → classPool → #USTable → ALL symbols/selectors
///    - Smalltalk at: #ClassName → Class → methodDict → ALL methods
///    - Smalltalk at: #Character → Character class → classPool → #CharacterTable → 256 chars
///    - Each class → metaclass → class-side methodDict → class methods
///
/// 3. ProcessorScheduler - Active process queue
///    Reachability chain:
///    - activeProcess → Process → suspendedContext → MethodContext chain → execution state
///    - quiescentProcessLists → Array[100] → LinkedList → Process → contexts
///
/// IMPLEMENTATION NOTES:
/// - This is a readonly struct with 5 fields, enabling multiple independent VM instances
/// - Each VM gets its own SOA value with OOPs pointing into that VM's heap
/// - ActiveProcess is accessed via ProcessorScheduler.activeProcess (not a separate root)
/// - All Smalltalk objects (including SystemDictionary) live in the VM's heap
/// </summary>
public readonly struct SpecialObjectsArray
{
    /// <summary>
    /// Total number of roots in the Special Objects Array (6).
    /// </summary>
    public const int Count = 6;

    /// <summary>
    /// OOP of nil singleton (instance of UndefinedObject).
    /// Required for pushNil bytecode (113).
    /// </summary>
    public readonly SmalltalkOOP Nil;

    /// <summary>
    /// OOP of false singleton (instance of False).
    /// Required for pushFalse bytecode (114).
    /// </summary>
    public readonly SmalltalkOOP False;

    /// <summary>
    /// OOP of true singleton (instance of True).
    /// Required for pushTrue bytecode (115).
    /// </summary>
    public readonly SmalltalkOOP True;

    /// <summary>
    /// OOP of Smalltalk global (SystemDictionary instance).
    /// Root of ALL classes, methods, symbols, and global variables.
    /// </summary>
    public readonly SmalltalkOOP Smalltalk;

    /// <summary>
    /// OOP of ProcessorScheduler singleton.
    /// Root of ALL processes and execution contexts.
    /// </summary>
    public readonly SmalltalkOOP ProcessorScheduler;

    /// <summary>
    /// OOP of Array containing all init expression CompiledMethod objects.
    /// Allocated during coldstart Phase 8b, accessible at runtime.
    /// Init expressions are executed at startup before entering main loop.
    /// </summary>
    public readonly SmalltalkOOP InitExpressionMethods;

    /// <summary>
    /// Creates a new Special Objects Array with the 6 GC roots.
    /// </summary>
    public SpecialObjectsArray(
        SmalltalkOOP nil,
        SmalltalkOOP @false,
        SmalltalkOOP @true,
        SmalltalkOOP smalltalk,
        SmalltalkOOP processorScheduler,
        SmalltalkOOP initExpressionMethods = default)
    {
        Nil = nil;
        False = @false;
        True = @true;
        Smalltalk = smalltalk;
        ProcessorScheduler = processorScheduler;
        InitExpressionMethods = initExpressionMethods;
    }

    /// <summary>
    /// Gets the active process from ProcessorScheduler.activeProcess field.
    /// Requires heap manager access to dereference the field.
    /// This is not stored separately per Blue Book spec - it's ProcessorScheduler.activeProcess.
    /// </summary>
    /// <param name="heapManager">Heap manager for field access</param>
    /// <returns>The active process OOP, or Null if not available</returns>
    public SmalltalkOOP GetActiveProcess(ISmalltalkHeapManager heapManager)
    {
        if (ProcessorScheduler == SmalltalkOOP.Null)
            return SmalltalkOOP.Null;

        // ProcessorScheduler.activeProcess is field index 1 per VMConstants
        return heapManager.GetInstanceVariable(
            ProcessorScheduler,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);
    }

    /// <summary>
    /// Returns the 6 GC roots as an array for garbage collection marking.
    /// Allocates a new array each time - only called during GC.
    /// </summary>
    public SmalltalkOOP[] GetRootsForGC()
    {
        // Include InitExpressionMethods if it's set (not null)
        if (InitExpressionMethods != SmalltalkOOP.Null)
        {
            return new[] { Nil, False, True, Smalltalk, ProcessorScheduler, InitExpressionMethods };
        }
        return new[] { Nil, False, True, Smalltalk, ProcessorScheduler };
    }

    /// <summary>
    /// Returns all roots including ActiveProcess for comprehensive GC.
    /// NOTE: ActiveProcess is reachable from ProcessorScheduler, so this is mainly
    /// for explicit rooting. The standard GetRootsForGC() is sufficient for normal GC.
    /// </summary>
    /// <param name="heapManager">Heap manager for accessing activeProcess field</param>
    public SmalltalkOOP[] GetAllRootsForGC(ISmalltalkHeapManager heapManager)
    {
        var activeProcess = GetActiveProcess(heapManager);
        var roots = new List<SmalltalkOOP> { Nil, False, True, Smalltalk, ProcessorScheduler };

        if (InitExpressionMethods != SmalltalkOOP.Null)
        {
            roots.Add(InitExpressionMethods);
        }

        if (activeProcess != SmalltalkOOP.Null)
        {
            roots.Add(activeProcess);
        }

        return roots.ToArray();
    }

    /// <summary>
    /// Gets a root by index (for compatibility with array-based code).
    /// </summary>
    public SmalltalkOOP this[int index] => index switch
    {
        0 => Nil,
        1 => False,
        2 => True,
        3 => Smalltalk,
        4 => ProcessorScheduler,
        _ => throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range [0-4]")
    };

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"SOA[Nil={Nil}, False={False}, True={True}, Smalltalk={Smalltalk}, ProcessorScheduler={ProcessorScheduler}]";
    }

    // ========================================
    // Static instance management for VM
    // ========================================

    /// <summary>
    /// The current Special Objects Array instance (static for now, will be per-VM in future).
    /// </summary>
    private static SpecialObjectsArray _instance;

    /// <summary>
    /// Gets the current SOA instance.
    /// </summary>
    public static SpecialObjectsArray Instance => _instance;

    /// <summary>
    /// Initializes the Special Objects Array (called at start of cold start).
    /// </summary>
    public static void Initialize()
    {
        // Create empty SOA (will be populated during bootstrap)
        _instance = new SpecialObjectsArray(
            SmalltalkOOP.Null,
            SmalltalkOOP.Null,
            SmalltalkOOP.Null,
            SmalltalkOOP.Null,
            SmalltalkOOP.Null
        );
    }

    /// <summary>
    /// Initializes the Special Objects Array with a fully populated instance from JSON cold start.
    /// </summary>
    public static void Initialize(SpecialObjectsArray soa)
    {
        _instance = soa;
        // No need to cache nil OOP since we're using SmalltalkOOP directly
    }

    /// <summary>
    /// Sets the nil singleton and caches it in SObjectRef.
    /// </summary>
    public static void SetNil(SmalltalkOOP nilObject)
    {
        // Console.WriteLine($"[SpecialObjectsArray.SetNil] Setting nil to {nilObject} (was {_instance.Nil})");
        _instance = new SpecialObjectsArray(
            nilObject,
            _instance.False,
            _instance.True,
            _instance.Smalltalk,
            _instance.ProcessorScheduler,
            _instance.InitExpressionMethods
        );
        // Console.WriteLine($"[SpecialObjectsArray.SetNil] VERIFY: _instance.Nil is now {_instance.Nil}");
    }

    /// <summary>
    /// Sets the false singleton.
    /// </summary>
    public static void SetFalse(SmalltalkOOP falseObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            falseObject,
            _instance.True,
            _instance.Smalltalk,
            _instance.ProcessorScheduler,
            _instance.InitExpressionMethods
        );
    }

    /// <summary>
    /// Sets the true singleton.
    /// </summary>
    public static void SetTrue(SmalltalkOOP trueObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            trueObject,
            _instance.Smalltalk,
            _instance.ProcessorScheduler,
            _instance.InitExpressionMethods
        );
    }

    /// <summary>
    /// Sets the Smalltalk global (SystemDictionary).
    /// </summary>
    public static void SetSmalltalk(SmalltalkOOP smalltalkObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            _instance.True,
            smalltalkObject,
            _instance.ProcessorScheduler,
            _instance.InitExpressionMethods
        );
    }

    /// <summary>
    /// Sets the ProcessorScheduler singleton.
    /// </summary>
    public static void SetProcessorScheduler(SmalltalkOOP processorSchedulerObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            _instance.True,
            _instance.Smalltalk,
            processorSchedulerObject,
            _instance.InitExpressionMethods
        );
    }

    /// <summary>
    /// Sets the active process (no-op, kept for compatibility).
    /// ActiveProcess is accessed via ProcessorScheduler.activeProcess instance variable.
    /// </summary>
    public static void SetActiveProcess(SmalltalkOOP activeProcessObject)
    {
        // No-op: ActiveProcess is not stored in SOA, it's ProcessorScheduler.activeProcess
        // This method exists for compatibility during migration
    }

    /// <summary>
    /// Sets the init expression methods array (allocated during Phase 8b).
    /// </summary>
    public static void SetInitExpressionMethods(SmalltalkOOP initExpressionMethodsArray)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            _instance.True,
            _instance.Smalltalk,
            _instance.ProcessorScheduler,
            initExpressionMethodsArray
        );
    }

}
