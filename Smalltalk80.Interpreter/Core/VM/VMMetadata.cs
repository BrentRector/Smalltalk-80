using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Immutable VM metadata provided by the cold start loader.
/// Contains field indices, well-known class OOPs, and known objects that the VM needs to execute code.
/// This allows the VM to operate purely on the Smalltalk heap without depending on
/// cold start data structures or hard-coded indices.
/// </summary>
public record VMMetadata
{
    // Class field indices
    public required int ClassNameFieldIndex { get; init; }
    public required int ClassSuperclassFieldIndex { get; init; }
    public required int ClassMethodDictFieldIndex { get; init; }
    public required int ClassFormatFieldIndex { get; init; }

    // Metaclass field index
    public required int MetaclassThisClassFieldIndex { get; init; }

    // MethodDictionary field indices
    public required int MethodDictTallyFieldIndex { get; init; }
    public required int MethodDictKeysFieldIndex { get; init; }
    public required int MethodDictValuesFieldIndex { get; init; }

    // Well-known class OOPs
    public required SmalltalkOOP ClassClassOOP { get; init; }
    public required SmalltalkOOP MetaclassClassOOP { get; init; }
    public required SmalltalkOOP SymbolClassOOP { get; init; }
    public required SmalltalkOOP StringClassOOP { get; init; }
    public required SmalltalkOOP ArrayClassOOP { get; init; }
    public required SmalltalkOOP ByteArrayClassOOP { get; init; }
    public required SmalltalkOOP WordArrayClassOOP { get; init; }
    public required SmalltalkOOP DisplayBitmapClassOOP { get; init; }
    public required SmalltalkOOP CompiledMethodClassOOP { get; init; }
    public required SmalltalkOOP SmallIntegerClassOOP { get; init; }
    public required SmalltalkOOP BlockContextClassOOP { get; init; }
    public required SmalltalkOOP OrderedCollectionClassOOP { get; init; }

    // Well-known objects for fast VM access (Nil, True, False, Smalltalk)
    public required KnownObjects KnownObjects { get; init; }

    // Special objects that need VM access (GC roots)
    public required SmalltalkOOP ProcessorScheduler { get; init; }
    public SmalltalkOOP InitExpressionMethods { get; init; } // Optional, set during Phase 8b
}
