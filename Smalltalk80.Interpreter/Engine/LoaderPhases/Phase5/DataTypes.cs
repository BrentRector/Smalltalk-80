using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Result of patch application containing counts by type.
/// </summary>
public record PatchApplicationResult(
    int SetInstanceVarCount,
    int ReplaceLiteralCount,
    int InstallMethodDictCount,
    int ReplacedArrayCount,
    int SkippedPoolBindingCount
);

/// <summary>
/// Shared context for all Phase 5 components.
/// </summary>
public record Phase5Context(
    ColdStartBundle Bundle,
    ObjectGraphAllocator Allocator,
    ISmalltalkHeapManager HeapManager,
    LayoutResolver LayoutResolver,
    Dictionary<string, SmalltalkOOP> LocalIdToObjectId,
    HashSet<(SmalltalkOOP oop, int slot)> InitializedFields,
    ColdstartReport Report,
    IDiagnosticHub? Diagnostics
);
