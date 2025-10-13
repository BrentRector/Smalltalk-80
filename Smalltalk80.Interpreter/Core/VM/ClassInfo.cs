namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Contains comprehensive information about a Smalltalk class.
/// </summary>
public record ClassInfo
{
    public required int ClassId { get; init; }
    public required string ClassName { get; init; }
    public required int SuperclassId { get; init; }
    public required string SuperclassName { get; init; }
    public required int MetaclassId { get; init; }
    public required string Format { get; init; }
    public required int InstanceSize { get; init; }
    public required string[] InstanceVariableNames { get; init; }
    public required string[] Selectors { get; init; }
}

/// <summary>
/// Contains detailed information about a compiled method.
/// </summary>
public record MethodInfo
{
    public required int MethodId { get; init; }
    public required string Selector { get; init; }
    public required int HeaderWord { get; init; }
    public required int ArgumentCount { get; init; }
    public required int TemporaryCount { get; init; }
    public required bool LargeContext { get; init; }
    public required int PrimitiveIndex { get; init; }
    public required int[] LiteralIds { get; init; }
    public required byte[] Bytecodes { get; init; }
}
