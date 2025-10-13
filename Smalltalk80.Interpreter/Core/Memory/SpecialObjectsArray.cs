namespace Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

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
    /// Total number of roots in the Special Objects Array (5).
    /// </summary>
    public const int Count = 5;

    /// <summary>
    /// OOP of nil singleton (instance of UndefinedObject).
    /// Required for pushNil bytecode (113).
    /// </summary>
    public readonly SObjectRef Nil;

    /// <summary>
    /// OOP of false singleton (instance of False).
    /// Required for pushFalse bytecode (114).
    /// </summary>
    public readonly SObjectRef False;

    /// <summary>
    /// OOP of true singleton (instance of True).
    /// Required for pushTrue bytecode (115).
    /// </summary>
    public readonly SObjectRef True;

    /// <summary>
    /// OOP of Smalltalk global (SystemDictionary instance).
    /// Root of ALL classes, methods, symbols, and global variables.
    /// </summary>
    public readonly SObjectRef Smalltalk;

    /// <summary>
    /// OOP of ProcessorScheduler singleton.
    /// Root of ALL processes and execution contexts.
    /// </summary>
    public readonly SObjectRef ProcessorScheduler;

    /// <summary>
    /// Creates a new Special Objects Array with the 5 GC roots.
    /// </summary>
    public SpecialObjectsArray(
        SObjectRef nil,
        SObjectRef @false,
        SObjectRef @true,
        SObjectRef smalltalk,
        SObjectRef processorScheduler)
    {
        Nil = nil;
        False = @false;
        True = @true;
        Smalltalk = smalltalk;
        ProcessorScheduler = processorScheduler;
    }

    /// <summary>
    /// Gets the active process (6th root, accessed via ProcessorScheduler).
    /// This is not stored separately per Blue Book spec - it's ProcessorScheduler.activeProcess.
    /// </summary>
    public SObjectRef ActiveProcess
    {
        get
        {
            if (ProcessorScheduler.IsNil)
                return SObjectRef.Nil;

            // ProcessorScheduler.activeProcess is field index 1
            var schedulerOop = ProcessorScheduler.AsObjectPointer();
            // TODO: Get actual field index from layout when LayoutManagement is implemented
            const int activeProcessIndex = 1;
            return ObjectMemory.GetInstanceVariable(schedulerOop, activeProcessIndex);
        }
    }

    /// <summary>
    /// Returns the 5 GC roots as an array for garbage collection marking.
    /// Allocates a new array each time - only called during GC.
    /// </summary>
    public SObjectRef[] GetRootsForGC()
    {
        return new[] { Nil, False, True, Smalltalk, ProcessorScheduler };
    }

    /// <summary>
    /// Returns all roots including ActiveProcess (6 total) for comprehensive GC.
    /// </summary>
    public SObjectRef[] GetAllRootsForGC()
    {
        var activeProcess = ActiveProcess;
        if (activeProcess.IsNil)
        {
            return GetRootsForGC();
        }

        return new[] { Nil, False, True, Smalltalk, ProcessorScheduler, activeProcess };
    }

    /// <summary>
    /// Gets a root by index (for compatibility with array-based code).
    /// </summary>
    public SObjectRef this[int index] => index switch
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
        return $"SOA[Nil={Nil.AsObjectPointer():X}, False={False.AsObjectPointer():X}, True={True.AsObjectPointer():X}, Smalltalk={Smalltalk.AsObjectPointer():X}, ProcessorScheduler={ProcessorScheduler.AsObjectPointer():X}]";
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
            SObjectRef.Nil,
            SObjectRef.Nil,
            SObjectRef.Nil,
            SObjectRef.Nil,
            SObjectRef.Nil
        );
    }

    /// <summary>
    /// Initializes the Special Objects Array with a fully populated instance from JSON cold start.
    /// </summary>
    public static void Initialize(SpecialObjectsArray soa)
    {
        _instance = soa;
        // Cache nil OOP in SObjectRef for fast nil comparisons
        Core.Values.SObjectRef.SetNilOop(soa.Nil.AsObjectPointer());
    }

    /// <summary>
    /// Sets the nil singleton and caches it in SObjectRef.
    /// </summary>
    public static void SetNil(SObjectRef nilObject)
    {
        _instance = new SpecialObjectsArray(
            nilObject,
            _instance.False,
            _instance.True,
            _instance.Smalltalk,
            _instance.ProcessorScheduler
        );
        Core.Values.SObjectRef.SetNilOop(nilObject.AsObjectPointer());
    }

    /// <summary>
    /// Sets the false singleton.
    /// </summary>
    public static void SetFalse(SObjectRef falseObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            falseObject,
            _instance.True,
            _instance.Smalltalk,
            _instance.ProcessorScheduler
        );
    }

    /// <summary>
    /// Sets the true singleton.
    /// </summary>
    public static void SetTrue(SObjectRef trueObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            trueObject,
            _instance.Smalltalk,
            _instance.ProcessorScheduler
        );
    }

    /// <summary>
    /// Sets the Smalltalk global (SystemDictionary).
    /// </summary>
    public static void SetSmalltalk(SObjectRef smalltalkObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            _instance.True,
            smalltalkObject,
            _instance.ProcessorScheduler
        );
    }

    /// <summary>
    /// Sets the ProcessorScheduler singleton.
    /// </summary>
    public static void SetProcessorScheduler(SObjectRef processorSchedulerObject)
    {
        _instance = new SpecialObjectsArray(
            _instance.Nil,
            _instance.False,
            _instance.True,
            _instance.Smalltalk,
            processorSchedulerObject
        );
    }

    /// <summary>
    /// Sets the active process (no-op, kept for compatibility).
    /// ActiveProcess is accessed via ProcessorScheduler.activeProcess instance variable.
    /// </summary>
    public static void SetActiveProcess(SObjectRef activeProcessObject)
    {
        // No-op: ActiveProcess is not stored in SOA, it's ProcessorScheduler.activeProcess
        // This method exists for compatibility during migration
    }

}
