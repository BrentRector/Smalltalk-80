namespace Smalltalk80;

/// <summary>
/// Enumeration of all Smalltalk-80 bytecodes.
/// </summary>
public enum Bytecode : byte
{
    /// <summary>Push receiver variable 0 onto the stack.</summary>
    PushReceiverVariable0 = 0x00,
    /// <summary>Push receiver variable 1 onto the stack.</summary>
    PushReceiverVariable1 = 0x01,
    /// <summary>Push receiver variable 2 onto the stack.</summary>
    PushReceiverVariable2 = 0x02,
    /// <summary>Push receiver variable 3 onto the stack.</summary>
    PushReceiverVariable3 = 0x03,
    /// <summary>Push receiver variable 4 onto the stack.</summary>
    PushReceiverVariable4 = 0x04,
    /// <summary>Push receiver variable 5 onto the stack.</summary>
    PushReceiverVariable5 = 0x05,
    /// <summary>Push receiver variable 6 onto the stack.</summary>
    PushReceiverVariable6 = 0x06,
    /// <summary>Push receiver variable 7 onto the stack.</summary>
    PushReceiverVariable7 = 0x07,
    /// <summary>Push receiver variable 8 onto the stack.</summary>
    PushReceiverVariable8 = 0x08,
    /// <summary>Push receiver variable 9 onto the stack.</summary>
    PushReceiverVariable9 = 0x09,
    /// <summary>Push receiver variable 10 onto the stack.</summary>
    PushReceiverVariable10 = 0x0A,
    /// <summary>Push receiver variable 11 onto the stack.</summary>
    PushReceiverVariable11 = 0x0B,
    /// <summary>Push receiver variable 12 onto the stack.</summary>
    PushReceiverVariable12 = 0x0C,
    /// <summary>Push receiver variable 13 onto the stack.</summary>
    PushReceiverVariable13 = 0x0D,
    /// <summary>Push receiver variable 14 onto the stack.</summary>
    PushReceiverVariable14 = 0x0E,
    /// <summary>Push receiver variable 15 onto the stack.</summary>
    PushReceiverVariable15 = 0x0F,
    /// <summary>Push temporary variable 0 onto the stack.</summary>
    PushTemporary0 = 0x10,
    /// <summary>Push temporary variable 1 onto the stack.</summary>
    PushTemporary1 = 0x11,
    /// <summary>Push temporary variable 2 onto the stack.</summary>
    PushTemporary2 = 0x12,
    /// <summary>Push temporary variable 3 onto the stack.</summary>
    PushTemporary3 = 0x13,
    /// <summary>Push temporary variable 4 onto the stack.</summary>
    PushTemporary4 = 0x14,
    /// <summary>Push temporary variable 5 onto the stack.</summary>
    PushTemporary5 = 0x15,
    /// <summary>Push temporary variable 6 onto the stack.</summary>
    PushTemporary6 = 0x16,
    /// <summary>Push temporary variable 7 onto the stack.</summary>
    PushTemporary7 = 0x17,
    /// <summary>Push temporary variable 8 onto the stack.</summary>
    PushTemporary8 = 0x18,
    /// <summary>Push temporary variable 9 onto the stack.</summary>
    PushTemporary9 = 0x19,
    /// <summary>Push temporary variable 10 onto the stack.</summary>
    PushTemporary10 = 0x1A,
    /// <summary>Push temporary variable 11 onto the stack.</summary>
    PushTemporary11 = 0x1B,
    /// <summary>Push temporary variable 12 onto the stack.</summary>
    PushTemporary12 = 0x1C,
    /// <summary>Push temporary variable 13 onto the stack.</summary>
    PushTemporary13 = 0x1D,
    /// <summary>Push temporary variable 14 onto the stack.</summary>
    PushTemporary14 = 0x1E,
    /// <summary>Push temporary variable 15 onto the stack.</summary>
    PushTemporary15 = 0x1F,
    /// <summary>Push literal constant 0 onto the stack.</summary>
    PushLiteralConstant0 = 0x20,
    /// <summary>Push literal constant 1 onto the stack.</summary>
    PushLiteralConstant1 = 0x21,
    /// <summary>Push literal constant 2 onto the stack.</summary>
    PushLiteralConstant2 = 0x22,
    /// <summary>Push literal constant 3 onto the stack.</summary>
    PushLiteralConstant3 = 0x23,
    /// <summary>Push literal constant 4 onto the stack.</summary>
    PushLiteralConstant4 = 0x24,
    /// <summary>Push literal constant 5 onto the stack.</summary>
    PushLiteralConstant5 = 0x25,
    /// <summary>Push literal constant 6 onto the stack.</summary>
    PushLiteralConstant6 = 0x26,
    /// <summary>Push literal constant 7 onto the stack.</summary>
    PushLiteralConstant7 = 0x27,
    /// <summary>Push literal constant 8 onto the stack.</summary>
    PushLiteralConstant8 = 0x28,
    /// <summary>Push literal constant 9 onto the stack.</summary>
    PushLiteralConstant9 = 0x29,
    /// <summary>Push literal constant 10 onto the stack.</summary>
    PushLiteralConstant10 = 0x2A,
    /// <summary>Push literal constant 11 onto the stack.</summary>
    PushLiteralConstant11 = 0x2B,
    /// <summary>Push literal constant 12 onto the stack.</summary>
    PushLiteralConstant12 = 0x2C,
    /// <summary>Push literal constant 13 onto the stack.</summary>
    PushLiteralConstant13 = 0x2D,
    /// <summary>Push literal constant 14 onto the stack.</summary>
    PushLiteralConstant14 = 0x2E,
    /// <summary>Push literal constant 15 onto the stack.</summary>
    PushLiteralConstant15 = 0x2F,
    /// <summary>Push literal constant 16 onto the stack.</summary>
    PushLiteralConstant16 = 0x30,
    /// <summary>Push literal constant 17 onto the stack.</summary>
    PushLiteralConstant17 = 0x31,
    /// <summary>Push literal constant 18 onto the stack.</summary>
    PushLiteralConstant18 = 0x32,
    /// <summary>Push literal constant 19 onto the stack.</summary>
    PushLiteralConstant19 = 0x33,
    /// <summary>Push literal constant 20 onto the stack.</summary>
    PushLiteralConstant20 = 0x34,
    /// <summary>Push literal constant 21 onto the stack.</summary>
    PushLiteralConstant21 = 0x35,
    /// <summary>Push literal constant 22 onto the stack.</summary>
    PushLiteralConstant22 = 0x36,
    /// <summary>Push literal constant 23 onto the stack.</summary>
    PushLiteralConstant23 = 0x37,
    /// <summary>Push literal constant 24 onto the stack.</summary>
    PushLiteralConstant24 = 0x38,
    /// <summary>Push literal constant 25 onto the stack.</summary>
    PushLiteralConstant25 = 0x39,
    /// <summary>Push literal constant 26 onto the stack.</summary>
    PushLiteralConstant26 = 0x3A,
    /// <summary>Push literal constant 27 onto the stack.</summary>
    PushLiteralConstant27 = 0x3B,
    /// <summary>Push literal constant 28 onto the stack.</summary>
    PushLiteralConstant28 = 0x3C,
    /// <summary>Push literal constant 29 onto the stack.</summary>
    PushLiteralConstant29 = 0x3D,
    /// <summary>Push literal constant 30 onto the stack.</summary>
    PushLiteralConstant30 = 0x3E,
    /// <summary>Push literal constant 31 onto the stack.</summary>
    PushLiteralConstant31 = 0x3F,
    /// <summary>Push literal variable 0 onto the stack.</summary>
    PushLiteralVariable0 = 0x40,
    /// <summary>Push literal variable 1 onto the stack.</summary>
    PushLiteralVariable1 = 0x41,
    /// <summary>Push literal variable 2 onto the stack.</summary>
    PushLiteralVariable2 = 0x42,
    /// <summary>Push literal variable 3 onto the stack.</summary>
    PushLiteralVariable3 = 0x43,
    /// <summary>Push literal variable 4 onto the stack.</summary>
    PushLiteralVariable4 = 0x44,
    /// <summary>Push literal variable 5 onto the stack.</summary>
    PushLiteralVariable5 = 0x45,
    /// <summary>Push literal variable 6 onto the stack.</summary>
    PushLiteralVariable6 = 0x46,
    /// <summary>Push literal variable 7 onto the stack.</summary>
    PushLiteralVariable7 = 0x47,
    /// <summary>Push literal variable 8 onto the stack.</summary>
    PushLiteralVariable8 = 0x48,
    /// <summary>Push literal variable 9 onto the stack.</summary>
    PushLiteralVariable9 = 0x49,
    /// <summary>Push literal variable 10 onto the stack.</summary>
    PushLiteralVariable10 = 0x4A,
    /// <summary>Push literal variable 11 onto the stack.</summary>
    PushLiteralVariable11 = 0x4B,
    /// <summary>Push literal variable 12 onto the stack.</summary>
    PushLiteralVariable12 = 0x4C,
    /// <summary>Push literal variable 13 onto the stack.</summary>
    PushLiteralVariable13 = 0x4D,
    /// <summary>Push literal variable 14 onto the stack.</summary>
    PushLiteralVariable14 = 0x4E,
    /// <summary>Push literal variable 15 onto the stack.</summary>
    PushLiteralVariable15 = 0x4F,
    /// <summary>Push literal variable 16 onto the stack.</summary>
    PushLiteralVariable16 = 0x50,
    /// <summary>Push literal variable 17 onto the stack.</summary>
    PushLiteralVariable17 = 0x51,
    /// <summary>Push literal variable 18 onto the stack.</summary>
    PushLiteralVariable18 = 0x52,
    /// <summary>Push literal variable 19 onto the stack.</summary>
    PushLiteralVariable19 = 0x53,
    /// <summary>Push literal variable 20 onto the stack.</summary>
    PushLiteralVariable20 = 0x54,
    /// <summary>Push literal variable 21 onto the stack.</summary>
    PushLiteralVariable21 = 0x55,
    /// <summary>Push literal variable 22 onto the stack.</summary>
    PushLiteralVariable22 = 0x56,
    /// <summary>Push literal variable 23 onto the stack.</summary>
    PushLiteralVariable23 = 0x57,
    /// <summary>Push literal variable 24 onto the stack.</summary>
    PushLiteralVariable24 = 0x58,
    /// <summary>Push literal variable 25 onto the stack.</summary>
    PushLiteralVariable25 = 0x59,
    /// <summary>Push literal variable 26 onto the stack.</summary>
    PushLiteralVariable26 = 0x5A,
    /// <summary>Push literal variable 27 onto the stack.</summary>
    PushLiteralVariable27 = 0x5B,
    /// <summary>Push literal variable 28 onto the stack.</summary>
    PushLiteralVariable28 = 0x5C,
    /// <summary>Push literal variable 29 onto the stack.</summary>
    PushLiteralVariable29 = 0x5D,
    /// <summary>Push literal variable 30 onto the stack.</summary>
    PushLiteralVariable30 = 0x5E,
    /// <summary>Push literal variable 31 onto the stack.</summary>
    PushLiteralVariable31 = 0x5F,
    /// <summary>Pop from stack and store into receiver variable 0.</summary>
    PopStoreReceiverVariable0 = 0x60,
    /// <summary>Pop from stack and store into receiver variable 1.</summary>
    PopStoreReceiverVariable1 = 0x61,
    /// <summary>Pop from stack and store into receiver variable 2.</summary>
    PopStoreReceiverVariable2 = 0x62,
    /// <summary>Pop from stack and store into receiver variable 3.</summary>
    PopStoreReceiverVariable3 = 0x63,
    /// <summary>Pop from stack and store into receiver variable 4.</summary>
    PopStoreReceiverVariable4 = 0x64,
    /// <summary>Pop from stack and store into receiver variable 5.</summary>
    PopStoreReceiverVariable5 = 0x65,
    /// <summary>Pop from stack and store into receiver variable 6.</summary>
    PopStoreReceiverVariable6 = 0x66,
    /// <summary>Pop from stack and store into receiver variable 7.</summary>
    PopStoreReceiverVariable7 = 0x67,
    /// <summary>Pop from stack and store into temporary variable 0.</summary>
    PopStoreTemporary0 = 0x68,
    /// <summary>Pop from stack and store into temporary variable 1.</summary>
    PopStoreTemporary1 = 0x69,
    /// <summary>Pop from stack and store into temporary variable 2.</summary>
    PopStoreTemporary2 = 0x6A,
    /// <summary>Pop from stack and store into temporary variable 3.</summary>
    PopStoreTemporary3 = 0x6B,
    /// <summary>Pop from stack and store into temporary variable 4.</summary>
    PopStoreTemporary4 = 0x6C,
    /// <summary>Pop from stack and store into temporary variable 5.</summary>
    PopStoreTemporary5 = 0x6D,
    /// <summary>Pop from stack and store into temporary variable 6.</summary>
    PopStoreTemporary6 = 0x6E,
    /// <summary>Pop from stack and store into temporary variable 7.</summary>
    PopStoreTemporary7 = 0x6F,
    /// <summary>Push constant Receiver onto the stack.</summary>
    PushConstReceiver = 0x70,
    /// <summary>Push constant True onto the stack.</summary>
    PushConstTrue = 0x71,
    /// <summary>Push constant False onto the stack.</summary>
    PushConstFalse = 0x72,
    /// <summary>Push constant Nil onto the stack.</summary>
    PushConstNil = 0x73,
    /// <summary>Push constant MinusOne onto the stack.</summary>
    PushConstMinusOne = 0x74,
    /// <summary>Push constant Zero onto the stack.</summary>
    PushConstZero = 0x75,
    /// <summary>Push constant One onto the stack.</summary>
    PushConstOne = 0x76,
    /// <summary>Push constant Two onto the stack.</summary>
    PushConstTwo = 0x77,
    /// <summary>Return receiver from message.</summary>
    ReturnReceiver = 0x78,
    /// <summary>Return true from message.</summary>
    ReturnTrue = 0x79,
    /// <summary>Return false from message.</summary>
    ReturnFalse = 0x7A,
    /// <summary>Return nil from message.</summary>
    ReturnNil = 0x7B,
    /// <summary>Return top of stack from message.</summary>
    ReturnTopFromMessage = 0x7C,
    /// <summary>Return top of stack from block.</summary>
    ReturnTopFromBlock = 0x7D,
    /// <summary>Unused bytecode.</summary>
    Unused126 = 0x7E,
    /// <summary>Unused bytecode.</summary>
    Unused127 = 0x7F,
    /// <summary>Extended push with descriptor byte.</summary>
    ExtendedPush = 0x80,
    /// <summary>Extended store with descriptor byte.</summary>
    ExtendedStore = 0x81,
    /// <summary>Extended store with descriptor byte and pop.</summary>
    ExtendedStoreAndPop = 0x82,
    /// <summary>Send with selector index and argument count in following byte.</summary>
    SingleExtendedSend = 0x83,
    /// <summary>Send with selector index and argument count in following two bytes.</summary>
    DoubleExtendedSend = 0x84,
    /// <summary>Send to superclass with selector index and argument count in following byte.</summary>
    SingleExtendedSuper = 0x85,
    /// <summary>Send to superclass with selector index and argument count in following two bytes.</summary>
    DoubleExtendedSuper = 0x86,
    /// <summary>Pop top of stack and discard.</summary>
    PopStackTop = 0x87,
    /// <summary>Duplicate the top element of the stack.</summary>
    DuplicateStackTop = 0x88,
    /// <summary>Push the active execution context.</summary>
    PushActiveContext = 0x89,
    /// <summary>Unused bytecode.</summary>
    Unused138 = 0x8A,
    /// <summary>Unused bytecode.</summary>
    Unused139 = 0x8B,
    /// <summary>Unused bytecode.</summary>
    Unused140 = 0x8C,
    /// <summary>Unused bytecode.</summary>
    Unused141 = 0x8D,
    /// <summary>Unused bytecode.</summary>
    Unused142 = 0x8E,
    /// <summary>Unused bytecode.</summary>
    Unused143 = 0x8F,
    /// <summary>Jump forward 1 bytecodes.</summary>
    ShortJump1 = 0x90,
    /// <summary>Jump forward 2 bytecodes.</summary>
    ShortJump2 = 0x91,
    /// <summary>Jump forward 3 bytecodes.</summary>
    ShortJump3 = 0x92,
    /// <summary>Jump forward 4 bytecodes.</summary>
    ShortJump4 = 0x93,
    /// <summary>Jump forward 5 bytecodes.</summary>
    ShortJump5 = 0x94,
    /// <summary>Jump forward 6 bytecodes.</summary>
    ShortJump6 = 0x95,
    /// <summary>Jump forward 7 bytecodes.</summary>
    ShortJump7 = 0x96,
    /// <summary>Jump forward 8 bytecodes.</summary>
    ShortJump8 = 0x97,
    /// <summary>Pop and jump forward 1 bytecodes if false.</summary>
    PopAndJumpOnFalse1 = 0x98,
    /// <summary>Pop and jump forward 2 bytecodes if false.</summary>
    PopAndJumpOnFalse2 = 0x99,
    /// <summary>Pop and jump forward 3 bytecodes if false.</summary>
    PopAndJumpOnFalse3 = 0x9A,
    /// <summary>Pop and jump forward 4 bytecodes if false.</summary>
    PopAndJumpOnFalse4 = 0x9B,
    /// <summary>Pop and jump forward 5 bytecodes if false.</summary>
    PopAndJumpOnFalse5 = 0x9C,
    /// <summary>Pop and jump forward 6 bytecodes if false.</summary>
    PopAndJumpOnFalse6 = 0x9D,
    /// <summary>Pop and jump forward 7 bytecodes if false.</summary>
    PopAndJumpOnFalse7 = 0x9E,
    /// <summary>Pop and jump forward 8 bytecodes if false.</summary>
    PopAndJumpOnFalse8 = 0x9F,
    /// <summary>Long jump with displacement index 0.</summary>
    LongJump0 = 0xA0,
    /// <summary>Long jump with displacement index 1.</summary>
    LongJump1 = 0xA1,
    /// <summary>Long jump with displacement index 2.</summary>
    LongJump2 = 0xA2,
    /// <summary>Long jump with displacement index 3.</summary>
    LongJump3 = 0xA3,
    /// <summary>Long jump with displacement index 4.</summary>
    LongJump4 = 0xA4,
    /// <summary>Long jump with displacement index 5.</summary>
    LongJump5 = 0xA5,
    /// <summary>Long jump with displacement index 6.</summary>
    LongJump6 = 0xA6,
    /// <summary>Long jump with displacement index 7.</summary>
    LongJump7 = 0xA7,
    /// <summary>Pop and long jump if true with displacement index 0.</summary>
    PopAndLongJumpOnTrue0 = 0xA8,
    /// <summary>Pop and long jump if true with displacement index 1.</summary>
    PopAndLongJumpOnTrue1 = 0xA9,
    /// <summary>Pop and long jump if true with displacement index 2.</summary>
    PopAndLongJumpOnTrue2 = 0xAA,
    /// <summary>Pop and long jump if true with displacement index 3.</summary>
    PopAndLongJumpOnTrue3 = 0xAB,
    /// <summary>Pop and long jump if false with displacement index 0.</summary>
    PopAndLongJumpOnFalse0 = 0xAC,
    /// <summary>Pop and long jump if false with displacement index 1.</summary>
    PopAndLongJumpOnFalse1 = 0xAD,
    /// <summary>Pop and long jump if false with displacement index 2.</summary>
    PopAndLongJumpOnFalse2 = 0xAE,
    /// <summary>Pop and long jump if false with displacement index 3.</summary>
    PopAndLongJumpOnFalse3 = 0xAF,
    /// <summary>Send arithmetic message #0.</summary>
    SendArithmeticMessage0 = 0xB0,
    /// <summary>Send arithmetic message #1.</summary>
    SendArithmeticMessage1 = 0xB1,
    /// <summary>Send arithmetic message #2.</summary>
    SendArithmeticMessage2 = 0xB2,
    /// <summary>Send arithmetic message #3.</summary>
    SendArithmeticMessage3 = 0xB3,
    /// <summary>Send arithmetic message #4.</summary>
    SendArithmeticMessage4 = 0xB4,
    /// <summary>Send arithmetic message #5.</summary>
    SendArithmeticMessage5 = 0xB5,
    /// <summary>Send arithmetic message #6.</summary>
    SendArithmeticMessage6 = 0xB6,
    /// <summary>Send arithmetic message #7.</summary>
    SendArithmeticMessage7 = 0xB7,
    /// <summary>Send arithmetic message #8.</summary>
    SendArithmeticMessage8 = 0xB8,
    /// <summary>Send arithmetic message #9.</summary>
    SendArithmeticMessage9 = 0xB9,
    /// <summary>Send arithmetic message #10.</summary>
    SendArithmeticMessage10 = 0xBA,
    /// <summary>Send arithmetic message #11.</summary>
    SendArithmeticMessage11 = 0xBB,
    /// <summary>Send arithmetic message #12.</summary>
    SendArithmeticMessage12 = 0xBC,
    /// <summary>Send arithmetic message #13.</summary>
    SendArithmeticMessage13 = 0xBD,
    /// <summary>Send arithmetic message #14.</summary>
    SendArithmeticMessage14 = 0xBE,
    /// <summary>Send arithmetic message #15.</summary>
    SendArithmeticMessage15 = 0xBF,
    /// <summary>Send special message #0.</summary>
    SendSpecialMessage0 = 0xC0,
    /// <summary>Send special message #1.</summary>
    SendSpecialMessage1 = 0xC1,
    /// <summary>Send special message #2.</summary>
    SendSpecialMessage2 = 0xC2,
    /// <summary>Send special message #3.</summary>
    SendSpecialMessage3 = 0xC3,
    /// <summary>Send special message #4.</summary>
    SendSpecialMessage4 = 0xC4,
    /// <summary>Send special message #5.</summary>
    SendSpecialMessage5 = 0xC5,
    /// <summary>Send special message #6.</summary>
    SendSpecialMessage6 = 0xC6,
    /// <summary>Send special message #7.</summary>
    SendSpecialMessage7 = 0xC7,
    /// <summary>Send special message #8.</summary>
    SendSpecialMessage8 = 0xC8,
    /// <summary>Send special message #9.</summary>
    SendSpecialMessage9 = 0xC9,
    /// <summary>Send special message #10.</summary>
    SendSpecialMessage10 = 0xCA,
    /// <summary>Send special message #11.</summary>
    SendSpecialMessage11 = 0xCB,
    /// <summary>Send special message #12.</summary>
    SendSpecialMessage12 = 0xCC,
    /// <summary>Send special message #13.</summary>
    SendSpecialMessage13 = 0xCD,
    /// <summary>Send special message #14.</summary>
    SendSpecialMessage14 = 0xCE,
    /// <summary>Send special message #15.</summary>
    SendSpecialMessage15 = 0xCF,
    /// <summary>Send literal selector #0 with no arguments.</summary>
    SendLiteralSelectorNoArgs0 = 0xD0,
    /// <summary>Send literal selector #1 with no arguments.</summary>
    SendLiteralSelectorNoArgs1 = 0xD1,
    /// <summary>Send literal selector #2 with no arguments.</summary>
    SendLiteralSelectorNoArgs2 = 0xD2,
    /// <summary>Send literal selector #3 with no arguments.</summary>
    SendLiteralSelectorNoArgs3 = 0xD3,
    /// <summary>Send literal selector #4 with no arguments.</summary>
    SendLiteralSelectorNoArgs4 = 0xD4,
    /// <summary>Send literal selector #5 with no arguments.</summary>
    SendLiteralSelectorNoArgs5 = 0xD5,
    /// <summary>Send literal selector #6 with no arguments.</summary>
    SendLiteralSelectorNoArgs6 = 0xD6,
    /// <summary>Send literal selector #7 with no arguments.</summary>
    SendLiteralSelectorNoArgs7 = 0xD7,
    /// <summary>Send literal selector #8 with no arguments.</summary>
    SendLiteralSelectorNoArgs8 = 0xD8,
    /// <summary>Send literal selector #9 with no arguments.</summary>
    SendLiteralSelectorNoArgs9 = 0xD9,
    /// <summary>Send literal selector #10 with no arguments.</summary>
    SendLiteralSelectorNoArgs10 = 0xDA,
    /// <summary>Send literal selector #11 with no arguments.</summary>
    SendLiteralSelectorNoArgs11 = 0xDB,
    /// <summary>Send literal selector #12 with no arguments.</summary>
    SendLiteralSelectorNoArgs12 = 0xDC,
    /// <summary>Send literal selector #13 with no arguments.</summary>
    SendLiteralSelectorNoArgs13 = 0xDD,
    /// <summary>Send literal selector #14 with no arguments.</summary>
    SendLiteralSelectorNoArgs14 = 0xDE,
    /// <summary>Send literal selector #15 with no arguments.</summary>
    SendLiteralSelectorNoArgs15 = 0xDF,
    /// <summary>Send literal selector #0 with one argument.</summary>
    SendLiteralSelectorOneArg0 = 0xE0,
    /// <summary>Send literal selector #1 with one argument.</summary>
    SendLiteralSelectorOneArg1 = 0xE1,
    /// <summary>Send literal selector #2 with one argument.</summary>
    SendLiteralSelectorOneArg2 = 0xE2,
    /// <summary>Send literal selector #3 with one argument.</summary>
    SendLiteralSelectorOneArg3 = 0xE3,
    /// <summary>Send literal selector #4 with one argument.</summary>
    SendLiteralSelectorOneArg4 = 0xE4,
    /// <summary>Send literal selector #5 with one argument.</summary>
    SendLiteralSelectorOneArg5 = 0xE5,
    /// <summary>Send literal selector #6 with one argument.</summary>
    SendLiteralSelectorOneArg6 = 0xE6,
    /// <summary>Send literal selector #7 with one argument.</summary>
    SendLiteralSelectorOneArg7 = 0xE7,
    /// <summary>Send literal selector #8 with one argument.</summary>
    SendLiteralSelectorOneArg8 = 0xE8,
    /// <summary>Send literal selector #9 with one argument.</summary>
    SendLiteralSelectorOneArg9 = 0xE9,
    /// <summary>Send literal selector #10 with one argument.</summary>
    SendLiteralSelectorOneArg10 = 0xEA,
    /// <summary>Send literal selector #11 with one argument.</summary>
    SendLiteralSelectorOneArg11 = 0xEB,
    /// <summary>Send literal selector #12 with one argument.</summary>
    SendLiteralSelectorOneArg12 = 0xEC,
    /// <summary>Send literal selector #13 with one argument.</summary>
    SendLiteralSelectorOneArg13 = 0xED,
    /// <summary>Send literal selector #14 with one argument.</summary>
    SendLiteralSelectorOneArg14 = 0xEE,
    /// <summary>Send literal selector #15 with one argument.</summary>
    SendLiteralSelectorOneArg15 = 0xEF,
    /// <summary>Send literal selector #0 with two arguments.</summary>
    SendLiteralSelectorTwoArgs0 = 0xF0,
    /// <summary>Send literal selector #1 with two arguments.</summary>
    SendLiteralSelectorTwoArgs1 = 0xF1,
    /// <summary>Send literal selector #2 with two arguments.</summary>
    SendLiteralSelectorTwoArgs2 = 0xF2,
    /// <summary>Send literal selector #3 with two arguments.</summary>
    SendLiteralSelectorTwoArgs3 = 0xF3,
    /// <summary>Send literal selector #4 with two arguments.</summary>
    SendLiteralSelectorTwoArgs4 = 0xF4,
    /// <summary>Send literal selector #5 with two arguments.</summary>
    SendLiteralSelectorTwoArgs5 = 0xF5,
    /// <summary>Send literal selector #6 with two arguments.</summary>
    SendLiteralSelectorTwoArgs6 = 0xF6,
    /// <summary>Send literal selector #7 with two arguments.</summary>
    SendLiteralSelectorTwoArgs7 = 0xF7,
    /// <summary>Send literal selector #8 with two arguments.</summary>
    SendLiteralSelectorTwoArgs8 = 0xF8,
    /// <summary>Send literal selector #9 with two arguments.</summary>
    SendLiteralSelectorTwoArgs9 = 0xF9,
    /// <summary>Send literal selector #10 with two arguments.</summary>
    SendLiteralSelectorTwoArgs10 = 0xFA,
    /// <summary>Send literal selector #11 with two arguments.</summary>
    SendLiteralSelectorTwoArgs11 = 0xFB,
    /// <summary>Send literal selector #12 with two arguments.</summary>
    SendLiteralSelectorTwoArgs12 = 0xFC,
    /// <summary>Send literal selector #13 with two arguments.</summary>
    SendLiteralSelectorTwoArgs13 = 0xFD,
    /// <summary>Send literal selector #14 with two arguments.</summary>
    SendLiteralSelectorTwoArgs14 = 0xFE,
    /// <summary>Send literal selector #15 with two arguments.</summary>
    SendLiteralSelectorTwoArgs15 = 0xFF,
}
