using Smalltalk80.Interpreter.Core.Values;

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
    SObjectRef Value,
    PrimitiveFailure FailureReason)
{
    /// <summary>
    /// Creates a successful primitive result.
    /// </summary>
    public static PrimitiveResult CreateSuccess(SObjectRef value) =>
        new(true, value, PrimitiveFailure.None);

    /// <summary>
    /// Creates a failed primitive result.
    /// </summary>
    public static PrimitiveResult CreateFailure(PrimitiveFailure reason) =>
        new(false, SObjectRef.Nil, reason);

    /// <summary>
    /// Implicit conversion from SObjectRef to successful result.
    /// </summary>
    public static implicit operator PrimitiveResult(SObjectRef value) =>
        CreateSuccess(value);

    /// <summary>
    /// Returns a string representation of the primitive result.
    /// </summary>
    /// <returns>A formatted string showing success or failure.</returns>
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
    /// <summary>
    /// No failure occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Wrong number of arguments provided to primitive.
    /// </summary>
    WrongNumberOfArguments = 1,

    /// <summary>
    /// Argument is of wrong type for this primitive.
    /// </summary>
    WrongArgumentType = 2,

    /// <summary>
    /// Receiver is not valid for this primitive.
    /// </summary>
    BadReceiver = 3,

    /// <summary>
    /// Invalid argument value provided.
    /// </summary>
    BadArgument = 4,

    /// <summary>
    /// Index is out of bounds.
    /// </summary>
    BadIndex = 5,

    /// <summary>
    /// Size parameter is invalid.
    /// </summary>
    BadSize = 6,

    /// <summary>
    /// Not enough memory to complete operation.
    /// </summary>
    NoMemory = 7,

    /// <summary>
    /// Attempt to modify read-only object.
    /// </summary>
    NoModification = 8,

    /// <summary>
    /// Division by zero attempted.
    /// </summary>
    DivideByZero = 9,

    /// <summary>
    /// Primitive not yet implemented.
    /// </summary>
    NotImplemented = 10,

    /// <summary>
    /// Unknown primitive index.
    /// </summary>
    UnknownPrimitive = 11,

    /// <summary>
    /// Internal error occurred in primitive.
    /// </summary>
    InternalError = 12
}
