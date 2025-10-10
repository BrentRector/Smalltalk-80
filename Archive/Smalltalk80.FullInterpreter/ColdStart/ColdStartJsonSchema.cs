using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.ColdStart;

/// <summary>
/// Root container for a complete Smalltalk-80 cold start bundle.
/// Contains all symbols, strings, classes, objects, methods, and initialization expressions
/// needed to bootstrap a Smalltalk-80 image from scratch.
/// </summary>
public record ColdStartBundle
{
    /// <summary>
    /// Metadata about the cold start bundle generation.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required Metadata Metadata { get; init; }

    /// <summary>
    /// Canonical interned symbol names; indexable by integer indices used throughout the file.
    /// </summary>
    [JsonPropertyName("symbols")]
    public required string[] Symbols { get; init; }

    /// <summary>
    /// String literal pool; indexable. Strings may be duplicated (not interned).
    /// </summary>
    [JsonPropertyName("strings")]
    public required string[] Strings { get; init; }

    /// <summary>
    /// Indices into symbols[] that the loader should arrange at low indices for single-byte send encodings.
    /// </summary>
    [JsonPropertyName("optimizedSelectors")]
    public required int[] OptimizedSelectors { get; init; }

    /// <summary>
    /// Class and metaclass descriptors.
    /// </summary>
    [JsonPropertyName("classes")]
    public required JsonClass[] Classes { get; init; }

    /// <summary>
    /// Concrete object shapes emitted by the compiler.
    /// </summary>
    [JsonPropertyName("objects")]
    public required JsonObject[] Objects { get; init; }

    /// <summary>
    /// All compiled methods (both class and instance methods).
    /// </summary>
    [JsonPropertyName("methods")]
    public required JsonMethod[] Methods { get; init; }

    /// <summary>
    /// Top-level initialization expressions (DoIts) to execute during cold start.
    /// </summary>
    [JsonPropertyName("initExpressions")]
    public required JsonInitExpression[] InitExpressions { get; init; }

    /// <summary>
    /// Deterministic installation sequence.
    /// </summary>
    [JsonPropertyName("installOrder")]
    public required InstallOrder InstallOrder { get; init; }

    /// <summary>
    /// Explicit patch operations to apply after allocation.
    /// </summary>
    [JsonPropertyName("patches")]
    public required JsonPatch[] Patches { get; init; }

    /// <summary>
    /// Optional table describing primitives for documentation and validation.
    /// </summary>
    [JsonPropertyName("primitives")]
    public Primitives? Primitives { get; init; }

    /// <summary>
    /// Optional manifest of source files referenced by methods/initExpressions.
    /// </summary>
    [JsonPropertyName("sourceFiles")]
    public SourceFile[]? SourceFiles { get; init; }

    /// <summary>
    /// Optional partition manifest if bundle is split across multiple files.
    /// </summary>
    [JsonPropertyName("partitionManifest")]
    public PartitionEntry[]? PartitionManifest { get; init; }

    /// <summary>
    /// Optional validation rules the loader should check.
    /// </summary>
    [JsonPropertyName("validationRules")]
    public ValidationRules? ValidationRules { get; init; }
}

/// <summary>
/// Metadata about the cold start bundle generation.
/// </summary>
public record Metadata
{
    /// <summary>
    /// Schema version (e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// Compiler tool name and version.
    /// </summary>
    [JsonPropertyName("generator")]
    public required string Generator { get; init; }

    /// <summary>
    /// ISO 8601 timestamp of generation.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public required string GeneratedAt { get; init; }

    /// <summary>
    /// Canonical source file or bundle name.
    /// </summary>
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }

    /// <summary>
    /// SHA256 checksum of input source for traceability.
    /// </summary>
    [JsonPropertyName("sourceChecksum")]
    public required string SourceChecksum { get; init; }

    /// <summary>
    /// Total count of symbols in the bundle.
    /// </summary>
    [JsonPropertyName("symbolCount")]
    public required int SymbolCount { get; init; }

    /// <summary>
    /// Total count of strings in the bundle.
    /// </summary>
    [JsonPropertyName("stringCount")]
    public required int StringCount { get; init; }

    /// <summary>
    /// Total count of classes in the bundle.
    /// </summary>
    [JsonPropertyName("classCount")]
    public required int ClassCount { get; init; }

    /// <summary>
    /// Total count of methods in the bundle.
    /// </summary>
    [JsonPropertyName("methodCount")]
    public required int MethodCount { get; init; }

    /// <summary>
    /// Total count of objects in the bundle.
    /// </summary>
    [JsonPropertyName("objectCount")]
    public required int ObjectCount { get; init; }

    /// <summary>
    /// Total count of initialization expressions in the bundle.
    /// </summary>
    [JsonPropertyName("initExpressionCount")]
    public required int InitExpressionCount { get; init; }

    /// <summary>
    /// Optional notes about the generation.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Class and metaclass descriptor.
/// </summary>
public record JsonClass
{
    /// <summary>
    /// Compiler-provided stable ID for this class.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    /// <summary>
    /// Index into symbols[] for class name.
    /// </summary>
    [JsonPropertyName("nameIndex")]
    public required int NameIndex { get; init; }

    /// <summary>
    /// True if this descriptor is a metaclass.
    /// </summary>
    [JsonPropertyName("isMetaclass")]
    public required bool IsMetaclass { get; init; }

    /// <summary>
    /// If isMetaclass=true, localId of the class this is the metaclass of.
    /// </summary>
    [JsonPropertyName("metaclassOfLocalId")]
    public string? MetaclassOfLocalId { get; init; }

    /// <summary>
    /// Blue Book format word (16-bit).
    /// </summary>
    [JsonPropertyName("format")]
    public required int Format { get; init; }

    /// <summary>
    /// Indices into symbols[] for instance variable names.
    /// </summary>
    [JsonPropertyName("instVarNames")]
    public required int[] InstVarNames { get; init; }

    /// <summary>
    /// Indices into symbols[] for class variable names.
    /// </summary>
    [JsonPropertyName("classVarNames")]
    public required int[] ClassVarNames { get; init; }

    /// <summary>
    /// LocalIds of pool dictionaries used by this class.
    /// </summary>
    [JsonPropertyName("poolLocalIds")]
    public required string[] PoolLocalIds { get; init; }

    /// <summary>
    /// Index into symbols[] for immediate superclass name.
    /// </summary>
    [JsonPropertyName("superNameIndex")]
    public int? SuperNameIndex { get; init; }

    /// <summary>
    /// Class organization category.
    /// </summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>
    /// Index into strings[] for class comment.
    /// </summary>
    [JsonPropertyName("commentStringIndex")]
    public int? CommentStringIndex { get; init; }

    /// <summary>
    /// Optional localId of a class-side #initialize method.
    /// </summary>
    [JsonPropertyName("classSideInitMethodLocalId")]
    public string? ClassSideInitMethodLocalId { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Concrete object shape emitted by the compiler.
/// </summary>
public record JsonObject
{
    /// <summary>
    /// Unique localId for this object.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    /// <summary>
    /// Index into symbols[] for the object's class name.
    /// </summary>
    [JsonPropertyName("classNameIndex")]
    public required int ClassNameIndex { get; init; }

    /// <summary>
    /// Hint for loader memory allocation strategy.
    /// </summary>
    [JsonPropertyName("formatHint")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required FormatHint FormatHint { get; init; }

    /// <summary>
    /// Named/fixed fields (non-indexed part).
    /// </summary>
    [JsonPropertyName("fields")]
    public required FieldValue[] Fields { get; init; }

    /// <summary>
    /// Indexed part (e.g., for Array elements).
    /// </summary>
    [JsonPropertyName("indexedFields")]
    public required FieldValue[] IndexedFields { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Format hint enumeration for object allocation.
/// </summary>
public enum FormatHint
{
    /// <summary>
    /// Object contains pointers.
    /// </summary>
    [JsonPropertyName("pointers")]
    Pointers,

    /// <summary>
    /// Object contains words.
    /// </summary>
    [JsonPropertyName("words")]
    Words,

    /// <summary>
    /// Object contains bytes.
    /// </summary>
    [JsonPropertyName("bytes")]
    Bytes,

    /// <summary>
    /// Object is a compiled method.
    /// </summary>
    [JsonPropertyName("compiledMethod")]
    CompiledMethod,

    /// <summary>
    /// Custom format.
    /// </summary>
    [JsonPropertyName("custom")]
    Custom
}

/// <summary>
/// Compiled method descriptor.
/// </summary>
public record JsonMethod
{
    /// <summary>
    /// Unique localId for this method.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    /// <summary>
    /// LocalId of the class or metaclass that owns this method.
    /// </summary>
    [JsonPropertyName("homeClassLocalId")]
    public required string HomeClassLocalId { get; init; }

    /// <summary>
    /// Index into symbols[] for selector.
    /// </summary>
    [JsonPropertyName("selectorIndex")]
    public required int SelectorIndex { get; init; }

    /// <summary>
    /// Blue Book method header.
    /// </summary>
    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }

    /// <summary>
    /// Literal frame for this method.
    /// </summary>
    [JsonPropertyName("literals")]
    public required LiteralValue[] Literals { get; init; }

    /// <summary>
    /// Bytecode array.
    /// </summary>
    [JsonPropertyName("bytecodes")]
    [JsonConverter(typeof(BytecodeConverter))]
    public required byte[] Bytecodes { get; init; }

    /// <summary>
    /// Optional Smalltalk source text for this method.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// Optional index into sourceFiles[] manifest.
    /// </summary>
    [JsonPropertyName("fileIndex")]
    public int? FileIndex { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Ordered list of argument names.
    /// </summary>
    [JsonPropertyName("argNames")]
    public string[]? ArgNames { get; init; }

    /// <summary>
    /// Ordered list of temporary variable names.
    /// </summary>
    [JsonPropertyName("tempNames")]
    public string[]? TempNames { get; init; }
}

/// <summary>
/// Method header information.
/// </summary>
public record MethodHeader
{
    /// <summary>
    /// Canonical 16-bit header word encoding.
    /// </summary>
    [JsonPropertyName("word")]
    public required int Word { get; init; }

    /// <summary>
    /// Optional: literal count (0-127).
    /// </summary>
    [JsonPropertyName("literalCount")]
    public int? LiteralCount { get; init; }

    /// <summary>
    /// Optional: temporary variable count (0-31).
    /// </summary>
    [JsonPropertyName("tempCount")]
    public int? TempCount { get; init; }

    /// <summary>
    /// Optional: argument count (0-7).
    /// </summary>
    [JsonPropertyName("argCount")]
    public int? ArgCount { get; init; }

    /// <summary>
    /// Optional: primitive index (0-255).
    /// </summary>
    [JsonPropertyName("primitiveIndex")]
    public int? PrimitiveIndex { get; init; }

    /// <summary>
    /// Optional: true if method needs extended stack.
    /// </summary>
    [JsonPropertyName("largeContext")]
    public bool? LargeContext { get; init; }
}

/// <summary>
/// Top-level initialization expression.
/// </summary>
public record JsonInitExpression
{
    /// <summary>
    /// Unique localId for this expression.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    /// <summary>
    /// Method header.
    /// </summary>
    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }

    /// <summary>
    /// Literal frame.
    /// </summary>
    [JsonPropertyName("literals")]
    public required LiteralValue[] Literals { get; init; }

    /// <summary>
    /// Bytecode array.
    /// </summary>
    [JsonPropertyName("bytecodes")]
    [JsonConverter(typeof(BytecodeConverter))]
    public required byte[] Bytecodes { get; init; }

    /// <summary>
    /// Original Smalltalk expression source.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Deterministic installation order specification.
/// </summary>
public record InstallOrder
{
    /// <summary>
    /// Sequential installation phases.
    /// </summary>
    [JsonPropertyName("phases")]
    public required InstallPhase[] Phases { get; init; }
}

/// <summary>
/// Installation phase descriptor.
/// </summary>
public record InstallPhase
{
    /// <summary>
    /// Human-readable phase name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Type of entities in this phase.
    /// </summary>
    [JsonPropertyName("kind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required PhaseKind Kind { get; init; }

    /// <summary>
    /// Ordered list of localIds to install in this phase.
    /// </summary>
    [JsonPropertyName("localIds")]
    public required string[] LocalIds { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Phase kind enumeration.
/// </summary>
public enum PhaseKind
{
    /// <summary>
    /// Classes phase.
    /// </summary>
    [JsonPropertyName("classes")]
    Classes,

    /// <summary>
    /// Objects phase.
    /// </summary>
    [JsonPropertyName("objects")]
    Objects,

    /// <summary>
    /// Methods phase.
    /// </summary>
    [JsonPropertyName("methods")]
    Methods,

    /// <summary>
    /// Initialization expressions phase.
    /// </summary>
    [JsonPropertyName("initExpressions")]
    InitExpressions,

    /// <summary>
    /// Patches phase.
    /// </summary>
    [JsonPropertyName("patches")]
    Patches
}

/// <summary>
/// Patch operation descriptor.
/// </summary>
public record JsonPatch
{
    /// <summary>
    /// Unique identifier for this patch.
    /// </summary>
    [JsonPropertyName("patchId")]
    public required string PatchId { get; init; }

    /// <summary>
    /// Describes what to patch.
    /// </summary>
    [JsonPropertyName("target")]
    public required PatchTarget Target { get; init; }

    /// <summary>
    /// Patch operation semantics.
    /// </summary>
    [JsonPropertyName("operation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required PatchOperation Operation { get; init; }

    /// <summary>
    /// Value to set.
    /// </summary>
    [JsonPropertyName("value")]
    public PatchValue? Value { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Patch target specification.
/// </summary>
public record PatchTarget
{
    /// <summary>
    /// Kind of patch target.
    /// </summary>
    [JsonPropertyName("kind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required PatchTargetKind Kind { get; init; }

    /// <summary>
    /// For methodLiteral: which method.
    /// </summary>
    [JsonPropertyName("methodLocalId")]
    public string? MethodLocalId { get; init; }

    /// <summary>
    /// For methodLiteral: which literal slot.
    /// </summary>
    [JsonPropertyName("literalIndex")]
    public int? LiteralIndex { get; init; }

    /// <summary>
    /// For objectField/objectIndexed: which object.
    /// </summary>
    [JsonPropertyName("objectLocalId")]
    public string? ObjectLocalId { get; init; }

    /// <summary>
    /// For objectField: which fixed field (0-based).
    /// </summary>
    [JsonPropertyName("fieldIndex")]
    public int? FieldIndex { get; init; }

    /// <summary>
    /// For objectIndexed: which indexed slot.
    /// </summary>
    [JsonPropertyName("indexedSlot")]
    public int? IndexedSlot { get; init; }

    /// <summary>
    /// For classSlot/metaclassWiring: which class.
    /// </summary>
    [JsonPropertyName("classLocalId")]
    public string? ClassLocalId { get; init; }

    /// <summary>
    /// For classSlot: name of class field.
    /// </summary>
    [JsonPropertyName("slotName")]
    public string? SlotName { get; init; }
}

/// <summary>
/// Patch target kind enumeration.
/// </summary>
public enum PatchTargetKind
{
    /// <summary>
    /// Method literal patch.
    /// </summary>
    [JsonPropertyName("methodLiteral")]
    MethodLiteral,

    /// <summary>
    /// Object field patch.
    /// </summary>
    [JsonPropertyName("objectField")]
    ObjectField,

    /// <summary>
    /// Object indexed slot patch.
    /// </summary>
    [JsonPropertyName("objectIndexed")]
    ObjectIndexed,

    /// <summary>
    /// Class slot patch.
    /// </summary>
    [JsonPropertyName("classSlot")]
    ClassSlot,

    /// <summary>
    /// Metaclass wiring patch.
    /// </summary>
    [JsonPropertyName("metaclassWiring")]
    MetaclassWiring,

    /// <summary>
    /// System dictionary patch.
    /// </summary>
    [JsonPropertyName("systemDictionary")]
    SystemDictionary,

    /// <summary>
    /// Custom patch.
    /// </summary>
    [JsonPropertyName("custom")]
    Custom
}

/// <summary>
/// Patch operation enumeration.
/// </summary>
public enum PatchOperation
{
    /// <summary>
    /// Set value operation.
    /// </summary>
    [JsonPropertyName("setValue")]
    SetValue,

    /// <summary>
    /// Set association value operation.
    /// </summary>
    [JsonPropertyName("setAssociationValue")]
    SetAssociationValue,

    /// <summary>
    /// Wire metaclass operation.
    /// </summary>
    [JsonPropertyName("wireMetaclass")]
    WireMetaclass,

    /// <summary>
    /// Add to system dictionary operation.
    /// </summary>
    [JsonPropertyName("addToSystemDictionary")]
    AddToSystemDictionary,

    /// <summary>
    /// Custom operation.
    /// </summary>
    [JsonPropertyName("custom")]
    Custom
}

/// <summary>
/// Patch value specification.
/// </summary>
public record PatchValue
{
    /// <summary>
    /// Type of patch value.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required PatchValueType Type { get; init; }

    /// <summary>
    /// For objectRef: localId reference.
    /// </summary>
    [JsonPropertyName("localId")]
    public string? LocalId { get; init; }

    /// <summary>
    /// For symbolRef: symbol index.
    /// </summary>
    [JsonPropertyName("symbolIndex")]
    public int? SymbolIndex { get; init; }

    /// <summary>
    /// For smallInt: integer value.
    /// </summary>
    [JsonPropertyName("intValue")]
    public int? IntValue { get; init; }
}

/// <summary>
/// Patch value type enumeration.
/// </summary>
public enum PatchValueType
{
    /// <summary>
    /// Object reference.
    /// </summary>
    [JsonPropertyName("objectRef")]
    ObjectRef,

    /// <summary>
    /// Symbol reference.
    /// </summary>
    [JsonPropertyName("symbolRef")]
    SymbolRef,

    /// <summary>
    /// Small integer.
    /// </summary>
    [JsonPropertyName("smallInt")]
    SmallInt,

    /// <summary>
    /// Nil value.
    /// </summary>
    [JsonPropertyName("nil")]
    Nil
}

/// <summary>
/// Primitives table descriptor.
/// </summary>
public record Primitives
{
    /// <summary>
    /// Primitives indexed by number.
    /// </summary>
    [JsonPropertyName("byIndex")]
    public PrimitiveEntry[]? ByIndex { get; init; }
}

/// <summary>
/// Primitive entry descriptor.
/// </summary>
public record PrimitiveEntry
{
    /// <summary>
    /// Primitive index (1-255).
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// Primitive name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Primitive signature.
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Source file manifest entry.
/// </summary>
public record SourceFile
{
    /// <summary>
    /// File index.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// File path.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// File checksum.
    /// </summary>
    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Partition manifest entry.
/// </summary>
public record PartitionEntry
{
    /// <summary>
    /// Partition filename.
    /// </summary>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// Sections contained in this partition.
    /// </summary>
    [JsonPropertyName("contains")]
    public required string[] Contains { get; init; }

    /// <summary>
    /// Partition checksum.
    /// </summary>
    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Validation rules specification.
/// </summary>
public record ValidationRules
{
    /// <summary>
    /// Check that all localIds are unique.
    /// </summary>
    [JsonPropertyName("allLocalIdsUnique")]
    public bool AllLocalIdsUnique { get; init; } = true;

    /// <summary>
    /// Check that all symbol indices are valid.
    /// </summary>
    [JsonPropertyName("allSymbolIndicesValid")]
    public bool AllSymbolIndicesValid { get; init; } = true;

    /// <summary>
    /// Check that all string indices are valid.
    /// </summary>
    [JsonPropertyName("allStringIndicesValid")]
    public bool AllStringIndicesValid { get; init; } = true;

    /// <summary>
    /// Check that all object references are resolvable.
    /// </summary>
    [JsonPropertyName("allObjectRefsResolvable")]
    public bool AllObjectRefsResolvable { get; init; } = true;

    /// <summary>
    /// Check that association keys are symbols.
    /// </summary>
    [JsonPropertyName("associationKeysMustBeSymbols")]
    public bool AssociationKeysMustBeSymbols { get; init; } = true;

    /// <summary>
    /// Check that install order covers all entities.
    /// </summary>
    [JsonPropertyName("installOrderCoversAllEntities")]
    public bool InstallOrderCoversAllEntities { get; init; } = true;
}

/// <summary>
/// Base class for field values (discriminated union).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NilValue), "nil")]
[JsonDerivedType(typeof(TrueValue), "true")]
[JsonDerivedType(typeof(FalseValue), "false")]
[JsonDerivedType(typeof(SmallIntValue), "smallInt")]
[JsonDerivedType(typeof(LargePositiveIntegerValue), "largePositiveInteger")]
[JsonDerivedType(typeof(LargeNegativeIntegerValue), "largeNegativeInteger")]
[JsonDerivedType(typeof(FloatValue), "float")]
[JsonDerivedType(typeof(SymbolRefValue), "symbolRef")]
[JsonDerivedType(typeof(StringRefValue), "stringRef")]
[JsonDerivedType(typeof(ObjectRefValue), "objectRef")]
[JsonDerivedType(typeof(CharacterValue), "character")]
[JsonDerivedType(typeof(BytesValue), "bytes")]
public abstract record FieldValue;

/// <summary>
/// Nil field value.
/// </summary>
public record NilValue : FieldValue;

/// <summary>
/// True field value.
/// </summary>
public record TrueValue : FieldValue;

/// <summary>
/// False field value.
/// </summary>
public record FalseValue : FieldValue;

/// <summary>
/// Small integer field value.
/// </summary>
public record SmallIntValue : FieldValue
{
    /// <summary>
    /// Integer value.
    /// </summary>
    [JsonPropertyName("value")]
    public required int Value { get; init; }
}

/// <summary>
/// Large positive integer field value.
/// </summary>
public record LargePositiveIntegerValue : FieldValue
{
    /// <summary>
    /// String representation of large integer.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Large negative integer field value.
/// </summary>
public record LargeNegativeIntegerValue : FieldValue
{
    /// <summary>
    /// String representation of large negative integer.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Float field value.
/// </summary>
public record FloatValue : FieldValue
{
    /// <summary>
    /// Float value.
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }
}

/// <summary>
/// Symbol reference field value.
/// </summary>
public record SymbolRefValue : FieldValue
{
    /// <summary>
    /// Index into symbols array.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }
}

/// <summary>
/// String reference field value.
/// </summary>
public record StringRefValue : FieldValue
{
    /// <summary>
    /// Index into strings array.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }
}

/// <summary>
/// Object reference field value.
/// </summary>
public record ObjectRefValue : FieldValue
{
    /// <summary>
    /// LocalId of referenced object.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }
}

/// <summary>
/// Character field value.
/// </summary>
public record CharacterValue : FieldValue
{
    /// <summary>
    /// Character code point (0-255).
    /// </summary>
    [JsonPropertyName("codePoint")]
    public required int CodePoint { get; init; }
}

/// <summary>
/// Bytes field value.
/// </summary>
public record BytesValue : FieldValue
{
    /// <summary>
    /// Encoding type (hex or base64).
    /// </summary>
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    /// <summary>
    /// Encoded byte data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

/// <summary>
/// Base class for literal values (discriminated union).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SymbolLiteral), "symbol")]
[JsonDerivedType(typeof(StringLiteral), "string")]
[JsonDerivedType(typeof(SmallIntLiteral), "smallInt")]
[JsonDerivedType(typeof(LargePositiveIntegerLiteral), "largePositiveInteger")]
[JsonDerivedType(typeof(LargeNegativeIntegerLiteral), "largeNegativeInteger")]
[JsonDerivedType(typeof(FloatLiteral), "float")]
[JsonDerivedType(typeof(AssociationLiteral), "association")]
[JsonDerivedType(typeof(ArrayLiteral), "array")]
[JsonDerivedType(typeof(ObjectRefLiteral), "objectRef")]
[JsonDerivedType(typeof(CharacterLiteral), "character")]
[JsonDerivedType(typeof(BytesLiteral), "bytes")]
public abstract record LiteralValue;

/// <summary>
/// Symbol literal.
/// </summary>
public record SymbolLiteral : LiteralValue
{
    /// <summary>
    /// Index into symbols array.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }
}

/// <summary>
/// String literal.
/// </summary>
public record StringLiteral : LiteralValue
{
    /// <summary>
    /// Index into strings array.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }
}

/// <summary>
/// Small integer literal.
/// </summary>
public record SmallIntLiteral : LiteralValue
{
    /// <summary>
    /// Integer value.
    /// </summary>
    [JsonPropertyName("value")]
    public required int Value { get; init; }
}

/// <summary>
/// Large positive integer literal.
/// </summary>
public record LargePositiveIntegerLiteral : LiteralValue
{
    /// <summary>
    /// String representation of large integer.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Large negative integer literal.
/// </summary>
public record LargeNegativeIntegerLiteral : LiteralValue
{
    /// <summary>
    /// String representation of large negative integer.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Float literal.
/// </summary>
public record FloatLiteral : LiteralValue
{
    /// <summary>
    /// Float value.
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }
}

/// <summary>
/// Association literal.
/// </summary>
public record AssociationLiteral : LiteralValue
{
    /// <summary>
    /// Association data.
    /// </summary>
    [JsonPropertyName("association")]
    public required AssociationData Association { get; init; }
}

/// <summary>
/// Association data for literals.
/// </summary>
public record AssociationData
{
    /// <summary>
    /// Key type (should always be "symbol").
    /// </summary>
    [JsonPropertyName("keyType")]
    public required string KeyType { get; init; }

    /// <summary>
    /// Index into symbols[] for the key.
    /// </summary>
    [JsonPropertyName("keyIndex")]
    public required int KeyIndex { get; init; }

    /// <summary>
    /// Value reference (may be null for unresolved).
    /// </summary>
    [JsonPropertyName("valueRef")]
    public AssociationValueRef? ValueRef { get; init; }
}

/// <summary>
/// Association value reference.
/// </summary>
public record AssociationValueRef
{
    /// <summary>
    /// Type (should be "objectRef").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// LocalId of the referenced object.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }
}

/// <summary>
/// Array literal.
/// </summary>
public record ArrayLiteral : LiteralValue
{
    /// <summary>
    /// Array elements.
    /// </summary>
    [JsonPropertyName("elements")]
    public required LiteralValue[] Elements { get; init; }
}

/// <summary>
/// Object reference literal.
/// </summary>
public record ObjectRefLiteral : LiteralValue
{
    /// <summary>
    /// LocalId of referenced object.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }
}

/// <summary>
/// Character literal.
/// </summary>
public record CharacterLiteral : LiteralValue
{
    /// <summary>
    /// Character code point (0-255).
    /// </summary>
    [JsonPropertyName("codePoint")]
    public required int CodePoint { get; init; }
}

/// <summary>
/// Bytes literal.
/// </summary>
public record BytesLiteral : LiteralValue
{
    /// <summary>
    /// Encoding type (hex or base64).
    /// </summary>
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    /// <summary>
    /// Encoded byte data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

/// <summary>
/// Custom JSON converter for bytecode arrays.
/// Handles both array and base64 string formats.
/// </summary>
public class BytecodeConverter : JsonConverter<byte[]>
{
    /// <inheritdoc/>
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<byte>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                list.Add((byte)reader.GetInt32());
            }
            return [.. list];
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var base64 = reader.GetString() ?? throw new JsonException("Null bytecode string");
            return Convert.FromBase64String(base64);
        }
        else
        {
            throw new JsonException("Expected array or string for bytecode");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        // Write as base64 for compactness
        writer.WriteStringValue(Convert.ToBase64String(value));
    }
}