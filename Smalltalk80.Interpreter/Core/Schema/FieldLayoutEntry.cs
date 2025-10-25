namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents a single field in a class's precomputed layout.
/// v1.0.6+: Precomputed by compiler during bundle generation, loaded by interpreter.
/// This eliminates the need for runtime topological sort and inheritance chain traversal.
/// </summary>
/// <param name="NameIndex">Index into symbols[] for field name</param>
/// <param name="SlotIndex">0-based slot index (must be sequential: 0, 1, 2, ...)</param>
/// <param name="InheritedFrom">LocalId of class that declared this field (null if declared by this class). Format: 'class:ClassName'. Useful for debugging.</param>
public record FieldLayoutEntry(
    int NameIndex,
    int SlotIndex,
    string? InheritedFrom
);
