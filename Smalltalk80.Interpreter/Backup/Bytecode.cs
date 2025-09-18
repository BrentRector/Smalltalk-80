using System;

/// <summary>
///     Enumerates the bytecodes understood by the interpreter.
///     This follows the canonical Blue Book (Goldberg and Robson, 1983) bytecode specification.
/// </summary>
public enum Bytecode : byte
{
    // 0-15: Push receiver instance variables
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

    // 16-31: Push temporary variables
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

    // 32-63: Push literal constants
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

    // 64-95: Push literal variables (Association values)
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

    // 96-103: Store and pop receiver variables
    /// <summary>Store and pop receiver variable.</summary>
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

    // 104-111: Store and pop temporary variables
    /// <summary>Store and pop temporary variable.</summary>
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

    // 112: Push receiver (self)
    /// <summary>Push receiver (self).</summary>
    PushReceiver = 112,

    // 113-119: UNDEFINED in canonical Blue Book
    Undefined113 = 113,
    Undefined114,
    Undefined115,
    Undefined116,
    Undefined117,
    Undefined118,
    Undefined119,

    // 120: Return receiver
    /// <summary>Return receiver (self).</summary>
    ReturnReceiver = 120,

    // 121-123: UNDEFINED in canonical Blue Book
    Undefined121 = 121,
    Undefined122,
    Undefined123,

    // 124-125: Return top of stack
    /// <summary>Return top of stack from method.</summary>
    ReturnTop = 124,
    /// <summary>Return top of stack from block.</summary>
    ReturnTopFromBlock = 125,

    // 126-127: UNDEFINED in canonical Blue Book
    Undefined126 = 126,
    Undefined127,

    // 128-137: Extended operations
    /// <summary>Extended push.</summary>
    ExtendedPush = 128,
    /// <summary>Extended store.</summary>
    ExtendedStore = 129,
    /// <summary>Extended store and pop.</summary>
    ExtendedStoreAndPop = 130,
    /// <summary>Single extended send.</summary>
    SingleExtendedSend = 131,
    /// <summary>Double extended send.</summary>
    DoubleExtendedSend = 132,
    /// <summary>Single extended super send.</summary>
    SingleExtendedSuperSend = 133,
    /// <summary>Double extended super send.</summary>
    DoubleExtendedSuperSend = 134,
    /// <summary>Pop stack.</summary>
    PopStack = 135,
    /// <summary>Duplicate top of stack.</summary>
    DuplicateTop = 136,
    /// <summary>Push active context.</summary>
    PushActiveContext = 137,

    // 138-143: UNDEFINED in canonical Blue Book
    Undefined138 = 138,
    Undefined139,
    Undefined140,
    Undefined141,
    Undefined142,
    Undefined143,

    // 144-151: Short unconditional jump
    /// <summary>Short unconditional jump 1.</summary>
    ShortUnconditionalJump1 = 144,
    /// <summary>Short unconditional jump 2.</summary>
    ShortUnconditionalJump2,
    /// <summary>Short unconditional jump 3.</summary>
    ShortUnconditionalJump3,
    /// <summary>Short unconditional jump 4.</summary>
    ShortUnconditionalJump4,
    /// <summary>Short unconditional jump 5.</summary>
    ShortUnconditionalJump5,
    /// <summary>Short unconditional jump 6.</summary>
    ShortUnconditionalJump6,
    /// <summary>Short unconditional jump 7.</summary>
    ShortUnconditionalJump7,
    /// <summary>Short unconditional jump 8.</summary>
    ShortUnconditionalJump8,

    // 152-159: Short conditional jump (pop and jump on false)
    /// <summary>Short conditional jump 1 (pop and jump on false).</summary>
    ShortConditionalJump1 = 152,
    /// <summary>Short conditional jump 2 (pop and jump on false).</summary>
    ShortConditionalJump2,
    /// <summary>Short conditional jump 3 (pop and jump on false).</summary>
    ShortConditionalJump3,
    /// <summary>Short conditional jump 4 (pop and jump on false).</summary>
    ShortConditionalJump4,
    /// <summary>Short conditional jump 5 (pop and jump on false).</summary>
    ShortConditionalJump5,
    /// <summary>Short conditional jump 6 (pop and jump on false).</summary>
    ShortConditionalJump6,
    /// <summary>Short conditional jump 7 (pop and jump on false).</summary>
    ShortConditionalJump7,
    /// <summary>Short conditional jump 8 (pop and jump on false).</summary>
    ShortConditionalJump8,

    // 160-167: Long unconditional jump
    /// <summary>Long unconditional jump.</summary>
    LongUnconditionalJump0 = 160,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump1,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump2,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump3,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump4,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump5,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump6,
    /// <inheritdoc cref="LongUnconditionalJump0" />
    LongUnconditionalJump7,

    // 168-171: Long conditional jump (pop and jump on true)
    /// <summary>Long conditional jump (pop and jump on true).</summary>
    LongConditionalJumpTrue0 = 168,
    /// <inheritdoc cref="LongConditionalJumpTrue0" />
    LongConditionalJumpTrue1,
    /// <inheritdoc cref="LongConditionalJumpTrue0" />
    LongConditionalJumpTrue2,
    /// <inheritdoc cref="LongConditionalJumpTrue0" />
    LongConditionalJumpTrue3,

    // 172-175: Long conditional jump (pop and jump on false)
    /// <summary>Long conditional jump (pop and jump on false).</summary>
    LongConditionalJumpFalse0 = 172,
    /// <inheritdoc cref="LongConditionalJumpFalse0" />
    LongConditionalJumpFalse1,
    /// <inheritdoc cref="LongConditionalJumpFalse0" />
    LongConditionalJumpFalse2,
    /// <inheritdoc cref="LongConditionalJumpFalse0" />
    LongConditionalJumpFalse3,

    // 176-191: Send arithmetic selector (special selectors 0-15)
    /// <summary>Send arithmetic selector 0 (+).</summary>
    SendArithmeticSelector0 = 176,
    /// <summary>Send arithmetic selector 1 (-).</summary>
    SendArithmeticSelector1,
    /// <summary>Send arithmetic selector 2 (&lt;).</summary>
    SendArithmeticSelector2,
    /// <summary>Send arithmetic selector 3 (&gt;).</summary>
    SendArithmeticSelector3,
    /// <summary>Send arithmetic selector 4 (&lt;=).</summary>
    SendArithmeticSelector4,
    /// <summary>Send arithmetic selector 5 (&gt;=).</summary>
    SendArithmeticSelector5,
    /// <summary>Send arithmetic selector 6 (=).</summary>
    SendArithmeticSelector6,
    /// <summary>Send arithmetic selector 7 (~=).</summary>
    SendArithmeticSelector7,
    /// <summary>Send arithmetic selector 8 (*).</summary>
    SendArithmeticSelector8,
    /// <summary>Send arithmetic selector 9 (/).</summary>
    SendArithmeticSelector9,
    /// <summary>Send arithmetic selector 10 (\\).</summary>
    SendArithmeticSelector10,
    /// <summary>Send arithmetic selector 11 (@).</summary>
    SendArithmeticSelector11,
    /// <summary>Send arithmetic selector 12 (bitShift:).</summary>
    SendArithmeticSelector12,
    /// <summary>Send arithmetic selector 13 (//).</summary>
    SendArithmeticSelector13,
    /// <summary>Send arithmetic selector 14 (bitAnd:).</summary>
    SendArithmeticSelector14,
    /// <summary>Send arithmetic selector 15 (bitOr:).</summary>
    SendArithmeticSelector15,

    // 192-207: Send common selector (special selectors 16-31)
    /// <summary>Send common selector 16 (at:).</summary>
    SendCommonSelector0 = 192,
    /// <summary>Send common selector 17 (at:put:).</summary>
    SendCommonSelector1,
    /// <summary>Send common selector 18 (size).</summary>
    SendCommonSelector2,
    /// <summary>Send common selector 19 (next).</summary>
    SendCommonSelector3,
    /// <summary>Send common selector 20 (nextPut:).</summary>
    SendCommonSelector4,
    /// <summary>Send common selector 21 (atEnd).</summary>
    SendCommonSelector5,
    /// <summary>Send common selector 22 (==).</summary>
    SendCommonSelector6,
    /// <summary>Send common selector 23 (class).</summary>
    SendCommonSelector7,
    /// <summary>Send common selector 24 (~~).</summary>
    SendCommonSelector8,
    /// <summary>Send common selector 25 (value).</summary>
    SendCommonSelector9,
    /// <summary>Send common selector 26 (value:).</summary>
    SendCommonSelector10,
    /// <summary>Send common selector 27 (do:).</summary>
    SendCommonSelector11,
    /// <summary>Send common selector 28 (new).</summary>
    SendCommonSelector12,
    /// <summary>Send common selector 29 (new:).</summary>
    SendCommonSelector13,
    /// <summary>Send common selector 30 (x).</summary>
    SendCommonSelector14,
    /// <summary>Send common selector 31 (y).</summary>
    SendCommonSelector15,

    // 208-223: Send literal selector with 0 args
    /// <summary>Send literal selector 0 with 0 args.</summary>
    SendLiteralSelector0With0Args = 208,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector1With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector2With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector3With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector4With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector5With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector6With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector7With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector8With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector9With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector10With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector11With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector12With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector13With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector14With0Args,
    /// <inheritdoc cref="SendLiteralSelector0With0Args" />
    SendLiteralSelector15With0Args,

    // 224-239: Send literal selector with 1 arg
    /// <summary>Send literal selector 0 with 1 arg.</summary>
    SendLiteralSelector0With1Arg = 224,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector1With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector2With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector3With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector4With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector5With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector6With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector7With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector8With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector9With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector10With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector11With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector12With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector13With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector14With1Arg,
    /// <inheritdoc cref="SendLiteralSelector0With1Arg" />
    SendLiteralSelector15With1Arg,

    // 240-255: Send literal selector with 2 args
    /// <summary>Send literal selector 0 with 2 args.</summary>
    SendLiteralSelector0With2Args = 240,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector1With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector2With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector3With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector4With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector5With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector6With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector7With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector8With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector9With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector10With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector11With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector12With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector13With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector14With2Args,
    /// <inheritdoc cref="SendLiteralSelector0With2Args" />
    SendLiteralSelector15With2Args,
}