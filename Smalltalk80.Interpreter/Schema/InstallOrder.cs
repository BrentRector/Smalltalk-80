namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Deterministic installation sequence.
/// Matches coldstart-v1.0.2.schema.json installOrder structure.
/// </summary>
public class InstallOrder
{
    public required InstallPhase[] Phases { get; init; }
}
