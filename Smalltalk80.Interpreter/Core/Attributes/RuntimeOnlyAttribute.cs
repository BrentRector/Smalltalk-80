namespace Smalltalk80.Interpreter.Core.Attributes;

/// <summary>
/// Marks a class, method, or field that should ONLY be used during runtime execution.
/// These components must NOT access cold start proxy data (registries, bundles, symbol arrays).
///
/// Components marked with this attribute typically:
/// - Execute Smalltalk bytecode
/// - Access only the Smalltalk heap (ISmalltalkHeap)
/// - Use LayoutResolver for dynamic field lookups
/// - Never touch ColdStartBundle or stale proxy data
///
/// Examples: VirtualMachine, primitive classes
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
public class RuntimeOnlyAttribute : Attribute
{
    /// <summary>
    /// Optional explanation of why this component is runtime-only.
    /// </summary>
    public string? Reason { get; set; }
}
