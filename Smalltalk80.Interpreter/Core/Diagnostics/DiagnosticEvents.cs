using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.Diagnostics;

/// <summary>
/// Base class for all diagnostic events.
/// </summary>
public abstract record DiagnosticEvent
{
    public required long Timestamp { get; init; }
    public required long InstructionCount { get; init; }
}

// ============================================================
// Cold Start Events
// ============================================================

/// <summary>Cold start phase beginning.</summary>
public record PhaseStartEvent : DiagnosticEvent
{
    public required int PhaseNumber { get; init; }
    public required string PhaseName { get; init; }
}

/// <summary>Cold start phase completion.</summary>
public record PhaseEndEvent : DiagnosticEvent
{
    public required int PhaseNumber { get; init; }
    public required string PhaseName { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Symbol interned during Phase 1.</summary>
public record SymbolInternedEvent : DiagnosticEvent
{
    public required int SymbolIndex { get; init; }
    public required string SymbolText { get; init; }
    public required SmalltalkOOP SymbolOOP { get; init; }
}

/// <summary>Class created during Phase 2.</summary>
public record ClassCreatedEvent : DiagnosticEvent
{
    public required string ClassName { get; init; }
    public required SmalltalkOOP ClassOOP { get; init; }
    public required SmalltalkOOP MetaclassOOP { get; init; }
    public required int InstanceVariableCount { get; init; }
}

/// <summary>Method compiled during Phase 4.</summary>
public record MethodCompiledEvent : DiagnosticEvent
{
    public required string ClassName { get; init; }
    public required string Selector { get; init; }
    public required SmalltalkOOP MethodOOP { get; init; }
    public required int BytecodeLength { get; init; }
    public required int LiteralCount { get; init; }
}

/// <summary>Patch applied during Phase 5.</summary>
public record PatchAppliedEvent : DiagnosticEvent
{
    public required string PatchType { get; init; }
    public required string TargetLocalId { get; init; }
    public string? FieldName { get; init; }
    public int? LiteralIndex { get; init; }
}

/// <summary>Global installed in SystemDictionary.</summary>
public record GlobalInstalledEvent : DiagnosticEvent
{
    public required string GlobalName { get; init; }
    public required SmalltalkOOP AssociationOOP { get; init; }
    public required SmalltalkOOP ValueOOP { get; init; }
}

/// <summary>Association created.</summary>
public record AssociationCreatedEvent : DiagnosticEvent
{
    public required string Key { get; init; }
    public required SmalltalkOOP AssociationOOP { get; init; }
}

// ============================================================
// Generic Message Event (for ad-hoc output)
// ============================================================

/// <summary>Generic diagnostic message for informational output.</summary>
public record DiagnosticMessageEvent : DiagnosticEvent
{
    public required MessageSeverity Severity { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public object? Data { get; init; }
}

// ============================================================
// Runtime Events
// ============================================================

/// <summary>Method entry event.</summary>
public record MethodEntryEvent : DiagnosticEvent
{
    public required SmalltalkOOP ReceiverOOP { get; init; }
    public required string ReceiverClass { get; init; }
    public required string Selector { get; init; }
    public required SmalltalkOOP[] Arguments { get; init; }
    public required int CallDepth { get; init; }
    public required bool IsSuper { get; init; }
}

/// <summary>Method exit event.</summary>
public record MethodExitEvent : DiagnosticEvent
{
    public required string Selector { get; init; }
    public required SmalltalkOOP ReturnValue { get; init; }
    public required long ElapsedInstructions { get; init; }
}

/// <summary>Bytecode instruction execution event.</summary>
public record InstructionEvent : DiagnosticEvent
{
    public required int IP { get; init; }
    public required byte Opcode { get; init; }
    public required string Mnemonic { get; init; }
    public required string? Operand { get; init; }
}

/// <summary>Primitive call event.</summary>
public record PrimitiveEvent : DiagnosticEvent
{
    public required int PrimitiveIndex { get; init; }
    public required string PrimitiveName { get; init; }
    public required SmalltalkOOP ReceiverOOP { get; init; }
    public required SmalltalkOOP[] Arguments { get; init; }
    public required bool Success { get; init; }
    public required SmalltalkOOP? Result { get; init; }
    public required string? FailureReason { get; init; }
}

/// <summary>Object allocation event.</summary>
public record AllocationEvent : DiagnosticEvent
{
    public required SmalltalkOOP ObjectOOP { get; init; }
    public required string ClassName { get; init; }
    public required ObjectFormat Format { get; init; }
    public required int SizeInWords { get; init; }
}

/// <summary>GC cycle event.</summary>
public record GCEvent : DiagnosticEvent
{
    public required int ObjectsBefore { get; init; }
    public required int ObjectsAfter { get; init; }
    public required int ObjectsCollected { get; init; }
    public required double MarkTimeMs { get; init; }
    public required double SweepTimeMs { get; init; }
    public required long BytesFreed { get; init; }
}
