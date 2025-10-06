namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Installation phase descriptor.
/// Matches coldstart-v1.0.0.schema.json installOrder phase structure.
/// </summary>
public class InstallPhase
{
    public required string Name { get; init; }
    public required string Kind { get; init; }  // "classes", "objects", "methods", "initExpressions", "patches"
    public required string[] LocalIds { get; init; }
    public string? Notes { get; init; }
}
