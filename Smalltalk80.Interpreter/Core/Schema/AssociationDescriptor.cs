namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Canonical Association descriptor (v1.0.9+).
/// Ensures exactly ONE Association object per unique (key, poolDictionaryName) pair across the bundle.
/// All method literals, classPools, and Smalltalk globals reference the same Association object by localId.
/// </summary>
public sealed record AssociationDescriptor
{
    /// <summary>
    /// Unique identifier for this canonical Association (e.g., "assoc:0", "assoc:1").
    /// </summary>
    public required string LocalId { get; init; }

    /// <summary>
    /// Index into symbols[] for the Association key (variable name).
    /// </summary>
    public required int KeyIndex { get; init; }

    /// <summary>
    /// Initial value for this Association (usually null for nil).
    /// Will be set by init expressions or patches.
    /// </summary>
    public FieldValue? ValueRef { get; init; }

    /// <summary>
    /// Pool dictionary name for pool variables, null for globals and class variables.
    /// </summary>
    public string? PoolDictionaryName { get; init; }

    /// <summary>
    /// Category for diagnostic purposes: "global", "classVariable", or "poolVariable".
    /// </summary>
    public required string Category { get; init; }
}
