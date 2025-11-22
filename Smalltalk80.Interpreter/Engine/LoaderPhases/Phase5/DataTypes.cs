using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

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
    ISmalltalkHeap HeapManager,
    LayoutResolver LayoutResolver,
    Dictionary<string, SmalltalkOOP> LocalIdToObjectId,
    HashSet<(SmalltalkOOP oop, int slot)> InitializedFields,
    ColdstartReport Report,
    IDiagnosticHub? Diagnostics
);
