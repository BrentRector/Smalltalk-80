using System;

/// <summary>
///     Enumerates the bytecodes understood by the interpreter.
///     Only a subset is currently implemented.
/// </summary>
public enum Bytecode : byte
{
    // 0-15
    /// <summary>Push receiver instance variable.</summary>
    PushReceiverVariable0 = 0,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable1,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable2,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable3,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable4,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable5,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable6,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable7,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable8,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable9,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable10,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable11,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable12,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable13,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable14,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable15,

    // 16-31
    /// <summary>Push temporary variable.</summary>
    PushTemporaryVariable0 = 16,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable1,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable2,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable3,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable4,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable5,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable6,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable7,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable8,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable9,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable10,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable11,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable12,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable13,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable14,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable15,

    // 32-63
    /// <summary>Push literal constant from the method literal frame.</summary>
    PushLiteralConstant0 = 32,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant1,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant2,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant3,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant4,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant5,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant6,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant7,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant8,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant9,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant10,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant11,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant12,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant13,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant14,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant15,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant16,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant17,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant18,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant19,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant20,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant21,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant22,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant23,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant24,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant25,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant26,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant27,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant28,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant29,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant30,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant31,

    // 64-95
    /// <summary>Push value from Association in the literal frame.</summary>
    PushLiteralVariable0 = 64,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable1,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable2,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable3,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable4,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable5,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable6,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable7,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable8,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable9,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable10,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable11,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable12,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable13,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable14,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable15,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable16,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable17,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable18,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable19,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable20,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable21,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable22,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable23,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable24,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable25,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable26,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable27,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable28,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable29,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable30,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable31,

    // 96-103
    /// <summary>Pop and store into receiver variable.</summary>
    StoreAndPopReceiverVariable0 = 96,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable1,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable2,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable3,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable4,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable5,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable6,
    /// <inheritdoc cref="StoreAndPopReceiverVariable0" />
    StoreAndPopReceiverVariable7,

    // 104-111
    /// <summary>Pop and store into temporary variable.</summary>
    StoreAndPopTemporaryVariable0 = 104,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable1,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable2,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable3,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable4,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable5,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable6,
    /// <inheritdoc cref="StoreAndPopTemporaryVariable0" />
    StoreAndPopTemporaryVariable7,

    // 112-119
    /// <summary>Push constant object.</summary>
    PushSelf = 112,
    /// <inheritdoc cref="PushSelf" />
    PushTrue,
    /// <inheritdoc cref="PushSelf" />
    PushFalse,
    /// <inheritdoc cref="PushSelf" />
    PushNil,
    /// <inheritdoc cref="PushSelf" />
    PushMinusOne,
    /// <inheritdoc cref="PushSelf" />
    PushZero,
    /// <inheritdoc cref="PushSelf" />
    PushOne,
    /// <inheritdoc cref="PushSelf" />
    PushTwo,


    // 120-123
    /// <summary>Return constant object.</summary>
    ReturnSelf = 120,
    /// <inheritdoc cref="ReturnSelf" />
    ReturnTrue,
    /// <inheritdoc cref="ReturnSelf" />
    ReturnFalse,
    /// <inheritdoc cref="ReturnSelf" />
    ReturnNil,

    // 124-125
    /// <summary>Return top of stack.</summary>
    ReturnTopFromMethod = 124,
    /// <inheritdoc cref="ReturnTopFromMethod" />
    ReturnTopFromBlock,

    // 126-127 unused
    /// <summary>Reserved for future use.</summary>
    Unused126,
    /// <summary>Reserved for future use.</summary>
    Unused127,

    // 128+
    /// <summary>
    ///     Send a message whose selector is located in the method's literal
    ///     frame at the index <c>bytecode - 128</c>.
    /// </summary>
    SendLiteralSelector = 128,
    /// <summary>Extended store (no pop).</summary>
    ExtendedStore = 129,
    /// <summary>Extended store and pop.</summary>
    ExtendedStoreAndPop = 130,

    /// <summary>
    ///     Send a message using a descriptor byte where the high-order three
    ///     bits encode the argument count and the low-order five bits encode the
    ///     selector index.
    /// </summary>
    SingleExtendedSend = 131,
    /// <summary>
    ///     Send a message using two extension bytes. The first byte specifies
    ///     the argument count while the second gives the selector index in the
    ///     literal frame.
    /// </summary>
    DoubleExtendedSend = 132,
    /// <summary>
    ///     Perform a <c>super</c> send using a single descriptor byte. Lookup of
    ///     the message begins in the superclass of the defining class stored as
    ///     the last literal of the method.
    /// </summary>
    SingleExtendedSuper = 133,
    /// <summary>
    ///     Perform a <c>super</c> send using two extension bytes that specify
    ///     the argument count and selector index respectively.
    /// </summary>
    DoubleExtendedSuper = 134,

    // send bytecodes with literal indices are above; enumeration continues

    /// <summary>Pop the stack top.</summary>
    PopStack = 135,
    /// <summary>Duplicate the stack top.</summary>
    DuplicateTop = 136,
    /// <summary>Push the active context.</summary>
    PushActiveContext = 137,

    // 138-143
    /// <summary>
    ///     Unconditional branch with an 8-bit signed offset relative to the
    ///     instruction following the offset byte.
    /// </summary>
    JumpShort = 138,
    /// <summary>
    ///     Unconditional branch with a 16-bit signed offset relative to the
    ///     instruction following the offset bytes.
    /// </summary>
    JumpLong,
    /// <summary>
    ///     Conditional branch taken when the stack top is <c>false</c>. The
    ///     branch distance is an 8-bit signed offset relative to the instruction
    ///     following the offset byte.
    /// </summary>
    JumpIfFalseShort,
    /// <summary>
    ///     Conditional branch taken when the stack top is <c>false</c>. The
    ///     branch distance is a 16-bit signed offset relative to the instruction
    ///     following the offset bytes.
    /// </summary>
    JumpIfFalseLong,
    /// <summary>
    ///     Conditional branch taken when the stack top is <c>true</c>. The
    ///     branch distance is an 8-bit signed offset relative to the instruction
    ///     following the offset byte.
    /// </summary>
    JumpIfTrueShort,
    /// <summary>
    ///     Conditional branch taken when the stack top is <c>true</c>. The
    ///     branch distance is a 16-bit signed offset relative to the instruction
    ///     following the offset bytes.
    /// </summary>
    JumpIfTrueLong,

    // 144-151
    /// <summary>Push the small integer constant 3.</summary>
    PushThree = 144,
    /// <summary>Push the small integer constant 4.</summary>
    PushFour,
    /// <summary>Push the small integer constant 5.</summary>
    PushFive,
    /// <summary>Push the small integer constant 6.</summary>
    PushSix,
    /// <summary>Push the small integer constant 7.</summary>
    PushSeven,
    /// <summary>Push the small integer constant 8.</summary>
    PushEight,
    /// <summary>Push the small integer constant 9.</summary>
    PushNine,
    /// <summary>Push the small integer constant 10.</summary>
    PushTen,
}



