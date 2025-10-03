namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Registers all primitive methods with the primitive table.
/// </summary>
public static class PrimitiveRegistry
{
    /// <summary>
    /// Registers all Blue Book primitives and extensions.
    /// </summary>
    public static void RegisterAll(PrimitiveTable table)
    {
        RegisterArithmeticPrimitives(table);
        RegisterComparisonPrimitives(table);
        RegisterArrayPrimitives(table);
        RegisterObjectPrimitives(table);
        RegisterControlPrimitives(table);
        RegisterProcessPrimitives(table);
        RegisterStringPrimitives(table);
        RegisterSystemPrimitives(table);
    }

    private static void RegisterArithmeticPrimitives(PrimitiveTable table)
    {
        // Primitives 1-17: SmallInteger arithmetic
        table.Register(1, "SmallInteger+", ArithmeticPrimitives.PrimitiveAdd);
        table.Register(2, "SmallInteger-", ArithmeticPrimitives.PrimitiveSubtract);
        table.Register(3, "SmallInteger<", ArithmeticPrimitives.PrimitiveLessThan);
        table.Register(4, "SmallInteger>", ArithmeticPrimitives.PrimitiveGreaterThan);
        table.Register(5, "SmallInteger<=", ArithmeticPrimitives.PrimitiveLessThanOrEqual);
        table.Register(6, "SmallInteger>=", ArithmeticPrimitives.PrimitiveGreaterThanOrEqual);
        table.Register(7, "SmallInteger=", ArithmeticPrimitives.PrimitiveEqual);
        table.Register(8, "SmallInteger~=", ArithmeticPrimitives.PrimitiveNotEqual);
        table.Register(9, "SmallInteger*", ArithmeticPrimitives.PrimitiveMultiply);
        table.Register(10, "SmallInteger/", ArithmeticPrimitives.PrimitiveDivide);
        table.Register(11, "SmallInteger\\\\", ArithmeticPrimitives.PrimitiveModulo);
        table.Register(12, "SmallInteger//", ArithmeticPrimitives.PrimitiveIntegerDivide);
        table.Register(13, "SmallInteger quo:", ArithmeticPrimitives.PrimitiveQuo);
        table.Register(14, "SmallInteger bitAnd:", ArithmeticPrimitives.PrimitiveBitAnd);
        table.Register(15, "SmallInteger bitOr:", ArithmeticPrimitives.PrimitiveBitOr);
        table.Register(16, "SmallInteger bitXor:", ArithmeticPrimitives.PrimitiveBitXor);
        table.Register(17, "SmallInteger bitShift:", ArithmeticPrimitives.PrimitiveBitShift);
    }

    private static void RegisterComparisonPrimitives(PrimitiveTable table)
    {
        // Comparison primitives are registered in ArithmeticPrimitives (3-8)
        // ComparisonPrimitives.cs exists for organizational purposes
    }

    private static void RegisterArrayPrimitives(PrimitiveTable table)
    {
        // Primitives 60-67: Array and Stream operations
        table.Register(60, "at:", ArrayPrimitives.PrimitiveAt);
        table.Register(61, "at:put:", ArrayPrimitives.PrimitiveAtPut);
        table.Register(62, "size", ArrayPrimitives.PrimitiveSize);
        table.Register(63, "String at:", ArrayPrimitives.PrimitiveStringAt);
        table.Register(64, "String at:put:", ArrayPrimitives.PrimitiveStringAtPut);
        table.Register(65, "next", ArrayPrimitives.PrimitiveNext);
        table.Register(66, "nextPut:", ArrayPrimitives.PrimitiveNextPut);
        table.Register(67, "atEnd", ArrayPrimitives.PrimitiveAtEnd);
    }

    private static void RegisterObjectPrimitives(PrimitiveTable table)
    {
        // Primitives 68-79: Storage management
        // TODO: Many of these need further adaptation
        // table.Register(68, "objectAt:", ObjectPrimitives.PrimitiveObjectAt);
        // table.Register(69, "objectAt:put:", ObjectPrimitives.PrimitiveObjectAtPut);
        table.Register(70, "new", ObjectPrimitives.PrimitiveNew);
        table.Register(71, "new:", ObjectPrimitives.PrimitiveNewWithSize);
        // table.Register(72, "become:", ObjectPrimitives.PrimitiveBecome);
        // table.Register(73, "instVarAt:", ObjectPrimitives.PrimitiveInstVarAt);
        // table.Register(74, "instVarAt:put:", ObjectPrimitives.PrimitiveInstVarAtPut);
        // table.Register(75, "asOop", ObjectPrimitives.PrimitiveAsOop);
        // table.Register(76, "asObject", ObjectPrimitives.PrimitiveAsObject);
        // table.Register(77, "someInstance", ObjectPrimitives.PrimitiveSomeInstance);
        // table.Register(78, "nextInstance", ObjectPrimitives.PrimitiveNextInstance);
        // table.Register(79, "newMethod:header:", ObjectPrimitives.PrimitiveNewMethod);

        // Primitives 110-111: Object identity
        table.Register(110, "==", ObjectPrimitives.PrimitiveIdentical);
        table.Register(111, "class", ObjectPrimitives.PrimitiveClass);
    }

    private static void RegisterControlPrimitives(PrimitiveTable table)
    {
        // Primitives 80-89: Control structures
        table.Register(80, "blockCopy:", ControlPrimitives.PrimitiveBlockCopy);
        table.Register(81, "value", ControlPrimitives.PrimitiveValue);
        table.Register(82, "value:", ControlPrimitives.PrimitiveValueWithArg);
        table.Register(83, "perform:", ControlPrimitives.PrimitivePerform);
        // table.Register(84, "perform:with:", ControlPrimitives.PrimitivePerformWithArgs); // TODO: Not ported yet
        table.Register(85, "signal", ControlPrimitives.PrimitiveSignal);
        table.Register(86, "wait", ControlPrimitives.PrimitiveWait);
        table.Register(87, "resume", ControlPrimitives.PrimitiveResume);
        table.Register(88, "suspend", ControlPrimitives.PrimitiveSuspend);
        // table.Register(89, "flushCache", ControlPrimitives.PrimitiveFlushCache); // TODO: Not ported yet
    }

    private static void RegisterProcessPrimitives(PrimitiveTable table)
    {
        // Process primitives are handled in ControlPrimitives (85-88)
        // ProcessPrimitives.cs contains more detailed implementations
    }

    private static void RegisterStringPrimitives(PrimitiveTable table)
    {
        // Primitives for string operations
        table.Register(105, "replaceFrom:to:with:startingAt:", StringPrimitives.PrimitiveStringReplace);
        // table.Register(158, "String =", StringPrimitives.PrimitiveStringEqual); // TODO: Not ported yet
        // table.Register(159, "String <", StringPrimitives.PrimitiveStringLessThan); // TODO: Not ported yet
    }

    private static void RegisterSystemPrimitives(PrimitiveTable table)
    {
        // Primitives 112-116: System operations
        table.Register(112, "coreLeft", SystemPrimitives.PrimitiveCoreLeft);
        table.Register(113, "quit", SystemPrimitives.PrimitiveQuit);
        table.Register(114, "exitToDebugger", SystemPrimitives.PrimitiveExitToDebugger);
        table.Register(115, "oopsLeft", SystemPrimitives.PrimitiveOopsLeft);
        table.Register(116, "signalAtOopsLeftWordsLeft", SystemPrimitives.PrimitiveSignalAtOopsLeftWordsLeft);

        // Additional system primitives
        table.Register(133, "forceGC", SystemPrimitives.PrimitiveForceGC);
        table.Register(139, "nextObject", SystemPrimitives.PrimitiveNextObject);
    }
}
