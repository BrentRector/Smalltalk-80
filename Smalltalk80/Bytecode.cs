using System;

#pragma warning disable CS1591

/// <summary>
///     Enumerates the bytecodes understood by the interpreter.
///     Only a subset is currently implemented.
/// </summary>
public enum Bytecode : byte
{
    // 0-15
    /// <summary>Push receiver instance variable.</summary>
    PushReceiverVariable0 = 0,
    PushReceiverVariable1,
    PushReceiverVariable2,
    PushReceiverVariable3,
    PushReceiverVariable4,
    PushReceiverVariable5,
    PushReceiverVariable6,
    PushReceiverVariable7,
    PushReceiverVariable8,
    PushReceiverVariable9,
    PushReceiverVariable10,
    PushReceiverVariable11,
    PushReceiverVariable12,
    PushReceiverVariable13,
    PushReceiverVariable14,
    PushReceiverVariable15,

    // 16-31
    /// <summary>Push temporary variable.</summary>
    PushTemporaryVariable0 = 16,
    PushTemporaryVariable1,
    PushTemporaryVariable2,
    PushTemporaryVariable3,
    PushTemporaryVariable4,
    PushTemporaryVariable5,
    PushTemporaryVariable6,
    PushTemporaryVariable7,
    PushTemporaryVariable8,
    PushTemporaryVariable9,
    PushTemporaryVariable10,
    PushTemporaryVariable11,
    PushTemporaryVariable12,
    PushTemporaryVariable13,
    PushTemporaryVariable14,
    PushTemporaryVariable15,

    // 32-63
    /// <summary>Push literal constant from the method literal frame.</summary>
    PushLiteralConstant0 = 32,
    PushLiteralConstant1,
    PushLiteralConstant2,
    PushLiteralConstant3,
    PushLiteralConstant4,
    PushLiteralConstant5,
    PushLiteralConstant6,
    PushLiteralConstant7,
    PushLiteralConstant8,
    PushLiteralConstant9,
    PushLiteralConstant10,
    PushLiteralConstant11,
    PushLiteralConstant12,
    PushLiteralConstant13,
    PushLiteralConstant14,
    PushLiteralConstant15,
    PushLiteralConstant16,
    PushLiteralConstant17,
    PushLiteralConstant18,
    PushLiteralConstant19,
    PushLiteralConstant20,
    PushLiteralConstant21,
    PushLiteralConstant22,
    PushLiteralConstant23,
    PushLiteralConstant24,
    PushLiteralConstant25,
    PushLiteralConstant26,
    PushLiteralConstant27,
    PushLiteralConstant28,
    PushLiteralConstant29,
    PushLiteralConstant30,
    PushLiteralConstant31,

    // 64-95
    /// <summary>Push value from Association in the literal frame.</summary>
    PushLiteralVariable0 = 64,
    PushLiteralVariable1,
    PushLiteralVariable2,
    PushLiteralVariable3,
    PushLiteralVariable4,
    PushLiteralVariable5,
    PushLiteralVariable6,
    PushLiteralVariable7,
    PushLiteralVariable8,
    PushLiteralVariable9,
    PushLiteralVariable10,
    PushLiteralVariable11,
    PushLiteralVariable12,
    PushLiteralVariable13,
    PushLiteralVariable14,
    PushLiteralVariable15,
    PushLiteralVariable16,
    PushLiteralVariable17,
    PushLiteralVariable18,
    PushLiteralVariable19,
    PushLiteralVariable20,
    PushLiteralVariable21,
    PushLiteralVariable22,
    PushLiteralVariable23,
    PushLiteralVariable24,
    PushLiteralVariable25,
    PushLiteralVariable26,
    PushLiteralVariable27,
    PushLiteralVariable28,
    PushLiteralVariable29,
    PushLiteralVariable30,
    PushLiteralVariable31,

    // 96-103
    /// <summary>Pop and store into receiver variable.</summary>
    StoreAndPopReceiverVariable0 = 96,
    StoreAndPopReceiverVariable1,
    StoreAndPopReceiverVariable2,
    StoreAndPopReceiverVariable3,
    StoreAndPopReceiverVariable4,
    StoreAndPopReceiverVariable5,
    StoreAndPopReceiverVariable6,
    StoreAndPopReceiverVariable7,

    // 104-111
    /// <summary>Pop and store into temporary variable.</summary>
    StoreAndPopTemporaryVariable0 = 104,
    StoreAndPopTemporaryVariable1,
    StoreAndPopTemporaryVariable2,
    StoreAndPopTemporaryVariable3,
    StoreAndPopTemporaryVariable4,
    StoreAndPopTemporaryVariable5,
    StoreAndPopTemporaryVariable6,
    StoreAndPopTemporaryVariable7,

    // 112-119
    /// <summary>Push constant object.</summary>
    PushSelf = 112,
    PushTrue,
    PushFalse,
    PushNil,
    PushMinusOne,
    PushZero,
    PushOne,
    PushTwo,

    // 120-123
    /// <summary>Return constant object.</summary>
    ReturnSelf = 120,
    ReturnTrue,
    ReturnFalse,
    ReturnNil,

    // 124-125
    /// <summary>Return top of stack.</summary>
    ReturnTopFromMethod = 124,
    ReturnTopFromBlock,

    // 126-127 unused
    Unused126,
    Unused127,

    // 128+ miscellaneous bytecodes not yet fully enumerated
    /// <summary>Extended store (no pop).</summary>
    ExtendedStore = 129,
    /// <summary>Extended store and pop.</summary>
    ExtendedStoreAndPop = 130,

    // send bytecodes and others are above; enumeration truncated

    /// <summary>Pop the stack top.</summary>
    PopStack = 135,
    /// <summary>Duplicate the stack top.</summary>
    DuplicateTop = 136,
    /// <summary>Push the active context.</summary>
    PushActiveContext = 137,
}

#pragma warning restore CS1591

