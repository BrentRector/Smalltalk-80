namespace Smalltalk80.Interpreter.Core.Diagnostics;

/// <summary>
/// Flags indicating which diagnostic events are enabled.
/// Used for fast-path filtering before event creation.
/// </summary>
[Flags]
public enum DiagnosticEventTypes : ulong
{
    None = 0,

    // Method events (runtime)
    MethodEntry = 1UL << 0,
    MethodExit = 1UL << 1,
    PrimitiveCall = 1UL << 2,
    PrimitiveFailure = 1UL << 3,
    MessageNotUnderstood = 1UL << 4,
    NonLocalReturn = 1UL << 5,

    // Bytecode events (runtime)
    InstructionExecute = 1UL << 10,
    StackPush = 1UL << 11,
    StackPop = 1UL << 12,
    Jump = 1UL << 13,

    // Context events (runtime)
    ContextActivate = 1UL << 20,
    ContextDeactivate = 1UL << 21,
    BlockCreate = 1UL << 22,

    // Memory events (both phases)
    ObjectAllocate = 1UL << 30,
    GarbageCollect = 1UL << 31,  // Legacy: maps to GCEnd
    Become = 1UL << 32,

    // GC lifecycle events (granular)
    GCStart = 1UL << 51,           // GC cycle beginning
    GCMarkPhaseStart = 1UL << 52,  // Mark phase beginning
    GCMarkPhaseEnd = 1UL << 53,    // Mark phase complete
    GCSweepPhaseStart = 1UL << 54, // Sweep phase beginning
    GCSweepPhaseEnd = 1UL << 55,   // Sweep phase complete
    GCEnd = 1UL << 56,             // GC cycle complete (summary)

    // GC per-object events (very detailed)
    GCObjectMarked = 1UL << 57,    // Individual object marked reachable
    GCObjectCollected = 1UL << 58, // Individual object collected (unreachable)

    // Cold start phase events
    PhaseStart = 1UL << 40,
    PhaseEnd = 1UL << 41,
    SymbolInterned = 1UL << 42,
    ClassCreated = 1UL << 43,
    MethodCompiled = 1UL << 44,
    PatchApplied = 1UL << 45,
    GlobalInstalled = 1UL << 46,
    AssociationCreated = 1UL << 47,

    // Generic message event (both phases)
    Message = 1UL << 50,

    // Compound groups - Runtime
    AllMethod = MethodEntry | MethodExit | PrimitiveCall | PrimitiveFailure | MessageNotUnderstood | NonLocalReturn,
    AllBytecode = InstructionExecute | StackPush | StackPop | Jump,
    AllContext = ContextActivate | ContextDeactivate | BlockCreate,
    AllGC = GCStart | GCMarkPhaseStart | GCMarkPhaseEnd | GCSweepPhaseStart | GCSweepPhaseEnd | GCEnd,
    AllGCDetail = AllGC | GCObjectMarked | GCObjectCollected,
    AllMemory = ObjectAllocate | AllGC | Become,
    AllRuntime = AllMethod | AllBytecode | AllContext | AllMemory,

    // Compound groups - Cold Start
    AllColdStartPhase = PhaseStart | PhaseEnd,
    AllColdStartDetail = SymbolInterned | ClassCreated | MethodCompiled | PatchApplied | GlobalInstalled | AssociationCreated,
    AllColdStart = AllColdStartPhase | AllColdStartDetail | Message,

    // Everything
    All = AllRuntime | AllColdStart | Message
}

/// <summary>
/// Message severity levels for DiagnosticMessageEvent.
/// </summary>
public enum MessageSeverity
{
    Debug,
    Info,
    Warning,
    Error
}
