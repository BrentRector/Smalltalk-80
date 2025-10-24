namespace Smalltalk80.Interpreter.Core.Attributes;

/// <summary>
/// Marks a class, method, or field that should ONLY be used during cold start (Phases 0-15).
/// Runtime code (VM, primitives) must NOT access these components.
///
/// Components marked with this attribute typically:
/// - Access ColdStartBundle data structures
/// - Modify registries (GlobalRegistry, SymbolRegistry)
/// - Operate on proxy data that becomes stale after cold start
///
/// Examples: Loader, SemanticAllocator, Phase classes
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
public class ColdStartOnlyAttribute : Attribute
{
    /// <summary>
    /// Optional explanation of why this component is cold-start only.
    /// </summary>
    public string? Reason { get; set; }
}
