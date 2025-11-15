using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Root cold start bundle structure.
/// Matches coldstart-v1.0.8.schema.json root schema.
/// v1.0.4: BlockContext objects moved to metadata.blockContexts[] (tooling-only, not runtime objects).
/// v1.0.7: Added Undeclared array - compiler-generated list of unresolved variable references.
/// v1.0.8: Added PoolDependencyGraph for pool dictionary dependency analysis and topological ordering.
/// </summary>
public sealed record ColdStartBundle
{
    /// <summary>
    /// Bundle metadata including version, generation info, and counts.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required Metadata Metadata { get; init; }
    /// <summary>
    /// Global symbol table containing all symbols used in the bundle.
    /// </summary>
    [JsonPropertyName("symbols")]
    public required string[] Symbols { get; init; }
    /// <summary>
    /// String table containing all string literals used in the bundle.
    /// </summary>
    [JsonPropertyName("strings")]
    public required string[] Strings { get; init; }
    /// <summary>
    /// Array of optimized selector indices for performance-critical selectors.
    /// </summary>
    [JsonPropertyName("optimizedSelectors")]
    public required int[] OptimizedSelectors { get; init; }
    /// <summary>
    /// Array of class descriptors defining all classes in the system.
    /// </summary>
    [JsonPropertyName("classes")]
    public required ClassDescriptor[] Classes { get; init; }
    /// <summary>
    /// Array of object descriptors defining all objects in the system.
    /// </summary>
    [JsonPropertyName("objects")]
    public required ObjectDescriptor[] Objects { get; init; }
    /// <summary>
    /// Array of method descriptors defining all compiled methods.
    /// </summary>
    [JsonPropertyName("methods")]
    public required MethodDescriptor[] Methods { get; init; }
    /// <summary>
    /// Array of initialization expressions (DoIts) to execute after cold start.
    /// </summary>
    [JsonPropertyName("initExpressions")]
    public required InitExpression[] InitExpressions { get; init; }
    /// <summary>
    /// Optional installation order specification for objects and patches.
    /// </summary>
    [JsonPropertyName("installOrder")]
    public InstallOrder? InstallOrder { get; init; }
    /// <summary>
    /// Optional array of patches to apply during installation.
    /// </summary>
    [JsonPropertyName("patches")]
    public PatchDescriptor[]? Patches { get; init; }
    /// <summary>
    /// Optional source file metadata for debugging and tooling.
    /// </summary>
    [JsonPropertyName("sourceFiles")]
    public SourceFile[]? SourceFiles { get; init; }
    /// <summary>
    /// Optional compiler-tracked undeclared variables (v1.0.7 compatibility).
    /// </summary>
    [JsonPropertyName("undeclared")]
    public UndeclaredVariable[]? Undeclared { get; init; }

    /// <summary>
    /// Pool dependency graph for initialization ordering (v1.0.8).
    /// Contains producers, consumers, edges, and topologically sorted execution order.
    /// </summary>
    [JsonPropertyName("poolDependencyGraph")]
    public PoolDependencyGraph? PoolDependencyGraph { get; init; }

    /// <summary>
    /// Loads a cold start bundle from a JSON file.
    /// </summary>
    public static ColdStartBundle LoadFromFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        return JsonSerializer.Deserialize<ColdStartBundle>(json, options)
            ?? throw new InvalidOperationException($"Failed to deserialize cold start bundle from {filePath}");
    }
}
