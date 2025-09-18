using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives;
/// <summary>
/// Result of a primitive operation.
/// Primitives can either succeed with a result or fail with a reason.
/// </summary>
/// <param name="Success">Whether the primitive succeeded.</param>
/// <param name="Value">The result value if successful, or Nil if failed.</param>
/// <param name="FailureReason">The failure reason if failed.</param>
public readonly record struct PrimitiveResult(
    bool Success,
    OOP Value,
    PrimitiveFailure FailureReason)
{
    /// <summary>
    /// Creates a successful primitive result.
    /// </summary>
    public static PrimitiveResult CreateSuccess(OOP value) =>
        new(true, value, PrimitiveFailure.None);

    /// <summary>
    /// Creates a failed primitive result.
    /// </summary>
    public static PrimitiveResult CreateFailure(PrimitiveFailure reason) =>
        new(false, OOP.Nil, reason);

    /// <summary>
    /// Implicit conversion from OOP to successful result.
    /// </summary>
    public static implicit operator PrimitiveResult(OOP value) =>
        CreateSuccess(value);

    public override string ToString() => Success 
        ? $"Success({Value})" 
        : $"Failure({FailureReason})";
}

/// <summary>
/// Reasons why a primitive can fail.
/// Based on Blue Book specification.
/// </summary>
public enum PrimitiveFailure
{
    None = 0,
    WrongNumberOfArguments = 1,
    WrongArgumentType = 2,
    BadReceiver = 3,
    BadArgument = 4,
    BadIndex = 5,
    BadSize = 6,
    NoMemory = 7,
    NoModification = 8,
    DivideByZero = 9,
    NotImplemented = 10,
    UnknownPrimitive = 11,
    InternalError = 12
}