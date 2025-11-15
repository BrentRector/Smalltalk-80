using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Pool dependency graph structure for v1.0.8 schema.
/// Represents the complete dependency graph for pool dictionary initialization order.
/// Contains producers (classes that populate pools), consumers (classes that use pools),
/// edges (dependency relationships), and the computed execution order.
/// </summary>
public sealed record PoolDependencyGraph
{
    /// <summary>
    /// List of pool dictionary producers (classes that populate pool dictionaries).
    /// Typically classes with initialize methods that call at:put: on pool dictionaries.
    /// </summary>
    [JsonPropertyName("producers")]
    public required List<Producer> Producers { get; init; }

    /// <summary>
    /// List of pool dictionary consumers (classes that reference pool variables).
    /// Classes that declare pool dictionaries and have methods that reference pool variables.
    /// </summary>
    [JsonPropertyName("consumers")]
    public required List<Consumer> Consumers { get; init; }

    /// <summary>
    /// Dependency edges between classes in the pool dependency graph.
    /// Each edge represents that one class must initialize before another.
    /// </summary>
    [JsonPropertyName("edges")]
    public required List<Edge> Edges { get; init; }

    /// <summary>
    /// Topologically sorted execution order for initialize expressions.
    /// Classes should have their initialize methods executed in this order
    /// to ensure all pool dependencies are satisfied.
    /// </summary>
    [JsonPropertyName("executionOrder")]
    public required List<string> ExecutionOrder { get; init; }

    /// <summary>
    /// Optional diagnostic information about the dependency graph.
    /// Includes warnings, errors, and validation results.
    /// </summary>
    [JsonPropertyName("diagnostics")]
    public Diagnostics? Diagnostics { get; init; }
}