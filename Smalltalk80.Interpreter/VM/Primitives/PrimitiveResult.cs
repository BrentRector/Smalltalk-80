namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Result of a primitive operation.
/// </summary>
public readonly struct PrimitiveResult
{
    public bool Success { get; init; }
    public int ReturnValue { get; init; }

    public static PrimitiveResult Succeeded(int returnValue) => new() { Success = true, ReturnValue = returnValue };
    public static PrimitiveResult Failed() => new() { Success = false, ReturnValue = 0 };
}
