using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Comparison primitives for SmallInteger operations (primitives 3-8).
/// These primitives handle relational comparisons between SmallIntegers.
/// Note: These are duplicated from ArithmeticPrimitives for organizational clarity.
/// In the actual VM, primitives 3-8 are part of the arithmetic primitive set.
/// </summary>
public static class ComparisonPrimitives
{
    /// <summary>
    /// Primitive 3: SmallInteger &lt; (less than).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isLess = receiver.AsSmallInteger() < arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isLess ? 2 : 1);
    }

    /// <summary>
    /// Primitive 4: SmallInteger &gt; (greater than).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isGreater = receiver.AsSmallInteger() > arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isGreater ? 2 : 1);
    }

    /// <summary>
    /// Primitive 5: SmallInteger &lt;= (less than or equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isLessOrEqual = receiver.AsSmallInteger() <= arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isLessOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 6: SmallInteger &gt;= (greater than or equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isGreaterOrEqual = receiver.AsSmallInteger() >= arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isGreaterOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 7: SmallInteger = (equality).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isEqual = receiver.AsSmallInteger() == arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 8: SmallInteger ~= (not equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver not equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveNotEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isNotEqual = receiver.AsSmallInteger() != arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isNotEqual ? 2 : 1);
    }

    /// <summary>
    /// Registers all comparison primitives in the primitive table.
    /// Note: These primitives are already registered by ArithmeticPrimitives.
    /// This registration is provided for completeness if needed separately.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        // These are typically registered by ArithmeticPrimitives
        // Uncomment if you want to register them separately:
        /*
        table.Register(3, "primitiveLessThan", PrimitiveLessThan);
        table.Register(4, "primitiveGreaterThan", PrimitiveGreaterThan);
        table.Register(5, "primitiveLessThanOrEqual", PrimitiveLessThanOrEqual);
        table.Register(6, "primitiveGreaterThanOrEqual", PrimitiveGreaterThanOrEqual);
        table.Register(7, "primitiveEqual", PrimitiveEqual);
        table.Register(8, "primitiveNotEqual", PrimitiveNotEqual);
        */
    }
}