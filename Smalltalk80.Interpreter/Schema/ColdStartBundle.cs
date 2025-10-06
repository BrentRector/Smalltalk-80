using System.Text.Json;

namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Root cold start bundle structure.
/// Matches coldstart-v1.0.0.schema.json root schema.
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
    public required PatchDescriptor[] Patches { get; init; }
    public SourceFile[]? SourceFiles { get; init; }

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
