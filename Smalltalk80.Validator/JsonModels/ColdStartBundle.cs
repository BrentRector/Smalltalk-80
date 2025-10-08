using System.Text.Json.Serialization;

namespace Smalltalk80.Validator.JsonModels;

/// <summary>
/// Root model for coldstart.json file.
/// Matches schema: docs/schemas/coldstart-v1.0.0.schema.json
/// </summary>
public class ColdStartBundle
{
    [JsonPropertyName("metadata")]
    public required Metadata Metadata { get; init; }

    [JsonPropertyName("symbols")]
    public required string[] Symbols { get; init; }

    [JsonPropertyName("strings")]
    public required string[] Strings { get; init; }

    [JsonPropertyName("optimizedSelectors")]
    public required int[] OptimizedSelectors { get; init; }

    [JsonPropertyName("classes")]
    public required ClassDefinition[] Classes { get; init; }

    [JsonPropertyName("objects")]
    public required ObjectDefinition[] Objects { get; init; }

    [JsonPropertyName("methods")]
    public required MethodDefinition[] Methods { get; init; }

    [JsonPropertyName("initExpressions")]
    public required InitExpression[] InitExpressions { get; init; }

    [JsonPropertyName("installOrder")]
    public required InstallOrder InstallOrder { get; init; }

    [JsonPropertyName("patches")]
    public required Patch[] Patches { get; init; }
}

public class Metadata
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("generator")]
    public required string Generator { get; init; }

    [JsonPropertyName("generatedAt")]
    public required string GeneratedAt { get; init; }

    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }

    [JsonPropertyName("sourceChecksum")]
    public required string SourceChecksum { get; init; }

    [JsonPropertyName("symbolCount")]
    public int SymbolCount { get; init; }

    [JsonPropertyName("stringCount")]
    public int StringCount { get; init; }

    [JsonPropertyName("classCount")]
    public int ClassCount { get; init; }

    [JsonPropertyName("methodCount")]
    public int MethodCount { get; init; }

    [JsonPropertyName("objectCount")]
    public int ObjectCount { get; init; }

    [JsonPropertyName("initExpressionCount")]
    public int InitExpressionCount { get; init; }
}

public class ClassDefinition
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("nameIndex")]
    public required int NameIndex { get; init; }

    [JsonPropertyName("isMetaclass")]
    public required bool IsMetaclass { get; init; }

    [JsonPropertyName("format")]
    public required int Format { get; init; }

    [JsonPropertyName("instVarNames")]
    public required int[] InstVarNames { get; init; }

    [JsonPropertyName("classVarNames")]
    public required int[] ClassVarNames { get; init; }

    [JsonPropertyName("poolLocalIds")]
    public required string[] PoolLocalIds { get; init; }

    [JsonPropertyName("superNameIndex")]
    public int? SuperNameIndex { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }
}

public class ObjectDefinition
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("classNameIndex")]
    public int? ClassNameIndex { get; init; }

    [JsonPropertyName("formatHint")]
    public string? FormatHint { get; init; }

    [JsonPropertyName("fields")]
    public List<object>? Fields { get; init; }

    [JsonPropertyName("indexedFields")]
    public List<object>? IndexedFields { get; init; }
}

public class MethodDefinition
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("homeClassLocalId")]
    public required string HomeClassLocalId { get; init; }

    [JsonPropertyName("selectorIndex")]
    public required int SelectorIndex { get; init; }

    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }

    [JsonPropertyName("literals")]
    public required List<object> Literals { get; init; }

    [JsonPropertyName("bytecodes")]
    public required int[] Bytecodes { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("argNames")]
    public string[]? ArgNames { get; init; }

    [JsonPropertyName("tempNames")]
    public string[]? TempNames { get; init; }
}

public class MethodHeader
{
    [JsonPropertyName("word")]
    public required int Word { get; init; }

    [JsonPropertyName("literalCount")]
    public int? LiteralCount { get; init; }

    [JsonPropertyName("tempCount")]
    public int? TempCount { get; init; }

    [JsonPropertyName("argCount")]
    public int? ArgCount { get; init; }

    [JsonPropertyName("primitiveIndex")]
    public int? PrimitiveIndex { get; init; }

    [JsonPropertyName("largeContext")]
    public bool? LargeContext { get; init; }
}

public class InitExpression
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }

    [JsonPropertyName("literals")]
    public required List<object> Literals { get; init; }

    [JsonPropertyName("bytecodes")]
    public required int[] Bytecodes { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

public class InstallOrder
{
    [JsonPropertyName("phases")]
    public required InstallPhase[] Phases { get; init; }
}

public class InstallPhase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("localIds")]
    public required string[] LocalIds { get; init; }
}

public class Patch
{
    [JsonPropertyName("patchId")]
    public required string PatchId { get; init; }

    [JsonPropertyName("target")]
    public required object Target { get; init; }

    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
