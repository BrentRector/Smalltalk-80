namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.BlueBook;

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
    /// Index constants for array-based access.
    /// </summary>
    public const int NilIndex = 0;
    public const int FalseIndex = 1;
    public const int TrueIndex = 2;
    public const int SmalltalkIndex = 3;
    public const int ProcessorSchedulerIndex = 4;
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

            // ProcessorScheduler layout: [0]=superclass, [1]=methodDict, [2]=format, [3]=activeProcess, ...
            return ProcessorScheduler.GetInstanceVariable(BlueBookLayouts.ProcessorScheduler.ActiveProcess);
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
}
