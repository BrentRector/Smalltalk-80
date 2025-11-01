using System.Text.Json;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Root cold start bundle structure.
/// Matches coldstart-v1.0.7.schema.json root schema.
/// v1.0.4: BlockContext objects moved to metadata.blockContexts[] (tooling-only, not runtime objects).
/// v1.0.7: Added Undeclared array - compiler-generated list of unresolved variable references.
/// </summary>
public class ColdStartBundle
{
    public required Metadata Metadata { get; init; }
    public required string[] Symbols { get; init; }
    public required string[] Strings { get; init; }
    public required int[] OptimizedSelectors { get; init; }
    public required ClassDescriptor[] Classes { get; init; }
    public required ObjectDescriptor[] Objects { get; init; }
    public required MethodDescriptor[] Methods { get; init; }
    public required InitExpression[] InitExpressions { get; init; }
    public InstallOrder? InstallOrder { get; init; }
    public PatchDescriptor[]? Patches { get; init; }  // Optional for backward compatibility
    public SourceFile[]? SourceFiles { get; init; }
    public UndeclaredVariable[]? Undeclared { get; init; }  // v1.0.7: Compiler-tracked undeclared variables

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
