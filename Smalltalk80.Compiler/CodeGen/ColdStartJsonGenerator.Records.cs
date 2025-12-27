using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Smalltalk80.Compiler.CodeGen;

// JSON Data Models matching the cold-start bundle schema

/// <summary>
/// Root structure of the cold-start JSON bundle.
/// Contains all data needed to bootstrap the Smalltalk-80 interpreter.
/// </summary>
internal sealed record ColdStartBundle
{
    public required Metadata Metadata { get; init; }
    public required List<string> Symbols { get; init; }
    public required List<string> Strings { get; init; }
    public required List<int> OptimizedSelectors { get; init; }
    public required List<JsonClass> Classes { get; init; }
    public List<object>? Associations { get; init; }  // v1.0.9: Canonical Associations
    public required List<JsonObject> Objects { get; init; }
    public required List<JsonMethod> Methods { get; init; }
    public required List<JsonInitExpr> InitExpressions { get; init; }
    public required InstallOrder InstallOrder { get; init; }
    public required List<PatchDescriptor> Patches { get; init; }
    public List<UndeclaredVariable>? Undeclared { get; init; }  // v1.0.8: Compiler-tracked undeclared variables
    public required PoolDependencyGraph PoolDependencyGraph { get; init; }  // v1.0.8: Pool variable dependency analysis
    /// <summary>
    /// Source file metadata for Blue Book source retrieval (FileIndex 1 = .sources, 2 = .changes).
    /// </summary>
    public List<SourceFileInfo>? SourceFiles { get; init; }
}

/// <summary>
/// Source file descriptor for the cold-start bundle.
/// </summary>
internal sealed record SourceFileInfo
{
    /// <summary>
    /// Index in the SourceFiles array (1-based: 1 = .sources, 2 = .changes).
    /// </summary>
    public required int Index { get; init; }
    /// <summary>
    /// Relative path to the source file from the coldstart.json location.
    /// </summary>
    public required string Path { get; init; }
    /// <summary>
    /// Optional checksum of the source file contents.
    /// </summary>
    public string? Checksum { get; init; }
    /// <summary>
    /// Optional notes about the source file.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Metadata about the cold-start bundle generation.
/// </summary>
internal sealed record Metadata
{
    public required string Version { get; init; }
    public required string Generator { get; init; }
    public required string GeneratedAt { get; init; }
    public string? SourceFile { get; init; }
    public required string SourceChecksum { get; init; }
    public required int SymbolCount { get; init; }
    public required int StringCount { get; init; }
    public required int ClassCount { get; init; }
    public required int MethodCount { get; init; }
    public required int ObjectCount { get; init; }
    public required int InitExpressionCount { get; init; }
    public int? UndeclaredCount { get; init; }
    public int? AssociationCount { get; init; }  // v1.0.9: Canonical Association count
}

/// <summary>
/// v1.0.8: Represents an undeclared variable found during compilation.
/// </summary>
internal sealed record UndeclaredVariable
{
    /// <summary>
    /// Name of the undeclared variable.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// List of method identifiers that reference this variable.
    /// Format: "ClassName>>selector" or "class:ClassName>>selector"
    /// </summary>
    public required List<string> References { get; init; }
}

/// <summary>
/// v1.0.8: Represents a pool variable dependency graph for initialization expression ordering.
/// </summary>
internal sealed record PoolDependencyGraph
{
    /// <summary>
    /// Classes that produce pool variable values (typically via initialize methods).
    /// </summary>
    public required List<Producer> Producers { get; init; }

    /// <summary>
    /// Classes that consume pool variable values.
    /// </summary>
    public required List<Consumer> Consumers { get; init; }

    /// <summary>
    /// Dependency edges representing producer-consumer relationships.
    /// </summary>
    public required List<Edge> Edges { get; init; }

    /// <summary>
    /// Topologically sorted execution order for initialization expressions.
    /// </summary>
    public required List<string> ExecutionOrder { get; init; }

    /// <summary>
    /// Optional diagnostic information about the dependency analysis.
    /// </summary>
    public Diagnostics? Diagnostics { get; init; }
}

/// <summary>
/// v1.0.8: Represents a class that produces pool variable values.
/// </summary>
internal sealed record Producer
{
    /// <summary>
    /// Local ID of the producing class.
    /// </summary>
    public required string ClassLocalId { get; init; }

    /// <summary>
    /// Local ID of the pool dictionary being produced.
    /// </summary>
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// Symbol indices of the variables being produced.
    /// </summary>
    public required List<int> Symbols { get; init; }

    /// <summary>
    /// Optional local ID of the specific method that produces the variables.
    /// </summary>
    public string? MethodLocalId { get; init; }

    /// <summary>
    /// Optional notes about the production relationship.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// v1.0.8: Represents a class that consumes pool variable values.
/// </summary>
internal sealed record Consumer
{
    /// <summary>
    /// Local ID of the consuming class.
    /// </summary>
    public required string ClassLocalId { get; init; }

    /// <summary>
    /// Local ID of the pool dictionary being consumed.
    /// </summary>
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// Symbol indices of the variables being consumed.
    /// </summary>
    public required List<int> Symbols { get; init; }

    /// <summary>
    /// Optional local ID of the specific method that consumes the variables.
    /// </summary>
    public string? MethodLocalId { get; init; }

    /// <summary>
    /// Optional notes about the consumption relationship.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// v1.0.8: Represents a dependency edge between a consumer and producer.
/// </summary>
internal sealed record Edge
{
    /// <summary>
    /// Local ID of the consumer class that depends on the producer.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Local ID of the producer class that the consumer depends on.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Optional symbol indices that are part of this dependency.
    /// </summary>
    public List<int>? Symbols { get; init; }

    /// <summary>
    /// Optional notes about the dependency.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// v1.0.8: Diagnostic information about pool dependency analysis.
/// </summary>
internal sealed record Diagnostics
{
    /// <summary>
    /// General diagnostic messages.
    /// </summary>
    public List<string>? Messages { get; init; }

    /// <summary>
    /// Error messages from the analysis.
    /// </summary>
    public List<string>? Errors { get; init; }

    /// <summary>
    /// Additional notes about the analysis.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// v1.0.8: Represents a reference to a pool variable within a method or init expression.
/// </summary>
internal sealed record PoolReference
{
    /// <summary>
    /// Local ID of the pool dictionary being referenced.
    /// </summary>
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// Symbol index of the variable being referenced.
    /// </summary>
    public required int SymbolIndex { get; init; }

    /// <summary>
    /// Usage type: "consume" for reading, "produce" for writing.
    /// </summary>
    public required string Usage { get; init; }
}

/// <summary>
/// v1.0.6+: Represents a single field in a class's precomputed layout.
/// Emitted by compiler to eliminate runtime topological sort in interpreter.
/// </summary>
internal sealed record JsonFieldLayoutEntry
{
    public required int NameIndex { get; init; }
    public required int SlotIndex { get; init; }
    public string? InheritedFrom { get; init; }
}

/// <summary>
/// Represents a class definition in the cold-start JSON bundle.
/// </summary>
internal sealed record JsonClass
{
    public required string LocalId { get; init; }
    public required int NameIndex { get; init; }
    public required bool IsMetaclass { get; init; }
    public string? MetaclassOfLocalId { get; init; }
    public required int Format { get; init; }
    public required List<int> InstVarNames { get; init; }
    public required List<int> ClassVarNames { get; init; }
    public required List<string> PoolLocalIds { get; init; }
    public int? SuperNameIndex { get; init; }
    public required string Category { get; init; }
    public int? CommentStringIndex { get; init; }
    public string? ClassSideInitMethodLocalId { get; init; }

    // v1.0.6+: Precomputed layout (eliminates runtime topological sort)
    public required int TotalInstanceVarCount { get; init; }
    public required List<JsonFieldLayoutEntry> FieldLayout { get; init; }
}

/// <summary>
/// Represents an object in the cold-start JSON bundle.
/// </summary>
internal sealed record JsonObject
{
    public required string LocalId { get; init; }
    public required int ClassNameIndex { get; init; }
    public required string FormatHint { get; init; }
    public required List<object> Fields { get; init; }
    public required List<object> IndexedFields { get; init; }
}

/// <summary>
/// Represents a compiled method in the cold-start JSON bundle.
/// </summary>
internal sealed record JsonMethod
{
    public required string LocalId { get; init; }
    public required string HomeClassLocalId { get; init; }
    public required int SelectorIndex { get; init; }
    public string? Category { get; init; }
    public required MethodHeader Header { get; init; }
    public required List<object> Literals { get; init; }
    public required List<int> Bytecodes { get; init; }
    public string? Source { get; init; }
    /// <summary>
    /// Index into sourceFiles array (1 = .sources, 2 = .changes).
    /// </summary>
    public int? FileIndex { get; init; }
    /// <summary>
    /// Byte position in the source file where this method's source begins.
    /// </summary>
    public int? SourcePosition { get; init; }
    public string[]? ArgNames { get; init; }
    public string[]? TempNames { get; init; }
    /// <summary>
    /// v1.0.8: Optional pool variable references in this method.
    /// </summary>
    public List<PoolReference>? PoolReferences { get; init; }
}

/// <summary>
/// Represents an initialization expression in the cold-start JSON bundle.
/// </summary>
internal sealed record JsonInitExpr
{
    public required string LocalId { get; init; }
    public required MethodHeader Header { get; init; }
    public required List<object> Literals { get; init; }
    public required List<int> Bytecodes { get; init; }
    public string? Source { get; init; }
    /// <summary>
    /// Index into sourceFiles array (1 = .sources, 2 = .changes).
    /// </summary>
    public int? FileIndex { get; init; }
    /// <summary>
    /// Byte position in the source file where this init expression's source begins.
    /// </summary>
    public int? SourcePosition { get; init; }
    /// <summary>
    /// v1.0.8: Optional pool variable references in this init expression.
    /// </summary>
    public List<PoolReference>? PoolReferences { get; init; }
}

/// <summary>
/// Method header encoding per Blue Book specification.
/// </summary>
internal sealed record MethodHeader
{
    public required int Word { get; init; }
    public int? LiteralCount { get; init; }
    public int? TempCount { get; init; }
    public int? ArgCount { get; init; }
    public int? PrimitiveIndex { get; init; }
    public bool? LargeContext { get; init; }
}

/// <summary>
/// Installation order specification for cold-start phases.
/// </summary>
internal sealed record InstallOrder
{
    public required List<InstallPhase> Phases { get; init; }
}

/// <summary>
/// A single installation phase in the cold-start process.
/// </summary>
internal sealed record InstallPhase
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required List<string> LocalIds { get; init; }
}

/// <summary>
/// Patch descriptor matching the interpreter's PatchDescriptor schema.
/// Used to patch forward references and circular dependencies after allocation.
/// </summary>
internal sealed record PatchDescriptor
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("targetLocalId")]
    public string? TargetLocalId { get; init; }

    [JsonPropertyName("fieldName")]
    public string? FieldName { get; init; }

    [JsonPropertyName("value")]
    public FieldValue? Value { get; init; }

    [JsonPropertyName("classLocalId")]
    public string? ClassLocalId { get; init; }

    [JsonPropertyName("methodLocalId")]
    public string? MethodLocalId { get; init; }

    [JsonPropertyName("literalIndex")]
    public int? LiteralIndex { get; init; }
}

/// <summary>
/// Field value discriminated union matching the interpreter's FieldValue schema.
/// </summary>
internal sealed record FieldValue
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }

    [JsonPropertyName("index")]
    public int? Index { get; init; }

    [JsonPropertyName("localId")]
    public string? LocalId { get; init; }

    [JsonPropertyName("codePoint")]
    public int? CodePoint { get; init; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
