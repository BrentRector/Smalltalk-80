namespace Smalltalk80.Core.Bytecodes;

/// <summary>
/// Bytecode opcodes for the Smalltalk-80 VM.
/// Values match the Blue Book specification (0x00..0xFF).
/// Range-based groups (push ivars, push temps, push literals, send ranges, etc.)
/// are documented with comments and helper utilities.
/// </summary>
public enum Bytecode : byte
{
    // 0x00 - 0x0F  Push receiver instance variables 0..15
    PUSH_IVAR_0 = 0x00,
    PUSH_IVAR_1 = 0x01,
    PUSH_IVAR_2 = 0x02,
    PUSH_IVAR_3 = 0x03,
    PUSH_IVAR_4 = 0x04,
    PUSH_IVAR_5 = 0x05,
    PUSH_IVAR_6 = 0x06,
    PUSH_IVAR_7 = 0x07,
    PUSH_IVAR_8 = 0x08,
    PUSH_IVAR_9 = 0x09,
    PUSH_IVAR_10 = 0x0A,
    PUSH_IVAR_11 = 0x0B,
    PUSH_IVAR_12 = 0x0C,
    PUSH_IVAR_13 = 0x0D,
    PUSH_IVAR_14 = 0x0E,
    PUSH_IVAR_15 = 0x0F,

    // 0x10 - 0x1F  Push temporary/argument 0..15
    PUSH_TEMP_0 = 0x10,
    PUSH_TEMP_1 = 0x11,
    PUSH_TEMP_2 = 0x12,
    PUSH_TEMP_3 = 0x13,
    PUSH_TEMP_4 = 0x14,
    PUSH_TEMP_5 = 0x15,
    PUSH_TEMP_6 = 0x16,
    PUSH_TEMP_7 = 0x17,
    PUSH_TEMP_8 = 0x18,
    PUSH_TEMP_9 = 0x19,
    PUSH_TEMP_10 = 0x1A,
    PUSH_TEMP_11 = 0x1B,
    PUSH_TEMP_12 = 0x1C,
    PUSH_TEMP_13 = 0x1D,
    PUSH_TEMP_14 = 0x1E,
    PUSH_TEMP_15 = 0x1F,

    // 0x20 - 0x3F  Push literal constant 0..31
    PUSH_LITERAL_CONST_0 = 0x20,
    PUSH_LITERAL_CONST_1 = 0x21,
    PUSH_LITERAL_CONST_2 = 0x22,
    PUSH_LITERAL_CONST_3 = 0x23,
    PUSH_LITERAL_CONST_4 = 0x24,
    PUSH_LITERAL_CONST_5 = 0x25,
    PUSH_LITERAL_CONST_6 = 0x26,
    PUSH_LITERAL_CONST_7 = 0x27,
    PUSH_LITERAL_CONST_8 = 0x28,
    PUSH_LITERAL_CONST_9 = 0x29,
    PUSH_LITERAL_CONST_10 = 0x2A,
    PUSH_LITERAL_CONST_11 = 0x2B,
    PUSH_LITERAL_CONST_12 = 0x2C,
    PUSH_LITERAL_CONST_13 = 0x2D,
    PUSH_LITERAL_CONST_14 = 0x2E,
    PUSH_LITERAL_CONST_15 = 0x2F,
    PUSH_LITERAL_CONST_16 = 0x30,
    PUSH_LITERAL_CONST_17 = 0x31,
    PUSH_LITERAL_CONST_18 = 0x32,
    PUSH_LITERAL_CONST_19 = 0x33,
    PUSH_LITERAL_CONST_20 = 0x34,
    PUSH_LITERAL_CONST_21 = 0x35,
    PUSH_LITERAL_CONST_22 = 0x36,
    PUSH_LITERAL_CONST_23 = 0x37,
    PUSH_LITERAL_CONST_24 = 0x38,
    PUSH_LITERAL_CONST_25 = 0x39,
    PUSH_LITERAL_CONST_26 = 0x3A,
    PUSH_LITERAL_CONST_27 = 0x3B,
    PUSH_LITERAL_CONST_28 = 0x3C,
    PUSH_LITERAL_CONST_29 = 0x3D,
    PUSH_LITERAL_CONST_30 = 0x3E,
    PUSH_LITERAL_CONST_31 = 0x3F,

    // 0x40 - 0x5F  Push literal association value (global var / association) 0..31
    PUSH_LITERAL_VAR_0 = 0x40,
    PUSH_LITERAL_VAR_1 = 0x41,
    PUSH_LITERAL_VAR_2 = 0x42,
    PUSH_LITERAL_VAR_3 = 0x43,
    PUSH_LITERAL_VAR_4 = 0x44,
    PUSH_LITERAL_VAR_5 = 0x45,
    PUSH_LITERAL_VAR_6 = 0x46,
    PUSH_LITERAL_VAR_7 = 0x47,
    PUSH_LITERAL_VAR_8 = 0x48,
    PUSH_LITERAL_VAR_9 = 0x49,
    PUSH_LITERAL_VAR_10 = 0x4A,
    PUSH_LITERAL_VAR_11 = 0x4B,
    PUSH_LITERAL_VAR_12 = 0x4C,
    PUSH_LITERAL_VAR_13 = 0x4D,
    PUSH_LITERAL_VAR_14 = 0x4E,
    PUSH_LITERAL_VAR_15 = 0x4F,
    PUSH_LITERAL_VAR_16 = 0x50,
    PUSH_LITERAL_VAR_17 = 0x51,
    PUSH_LITERAL_VAR_18 = 0x52,
    PUSH_LITERAL_VAR_19 = 0x53,
    PUSH_LITERAL_VAR_20 = 0x54,
    PUSH_LITERAL_VAR_21 = 0x55,
    PUSH_LITERAL_VAR_22 = 0x56,
    PUSH_LITERAL_VAR_23 = 0x57,
    PUSH_LITERAL_VAR_24 = 0x58,
    PUSH_LITERAL_VAR_25 = 0x59,
    PUSH_LITERAL_VAR_26 = 0x5A,
    PUSH_LITERAL_VAR_27 = 0x5B,
    PUSH_LITERAL_VAR_28 = 0x5C,
    PUSH_LITERAL_VAR_29 = 0x5D,
    PUSH_LITERAL_VAR_30 = 0x5E,
    PUSH_LITERAL_VAR_31 = 0x5F,

    // 0x60 - 0x67  Pop and store into instance var 0..7
    POP_STORE_IVAR_0 = 0x60,
    POP_STORE_IVAR_1 = 0x61,
    POP_STORE_IVAR_2 = 0x62,
    POP_STORE_IVAR_3 = 0x63,
    POP_STORE_IVAR_4 = 0x64,
    POP_STORE_IVAR_5 = 0x65,
    POP_STORE_IVAR_6 = 0x66,
    POP_STORE_IVAR_7 = 0x67,

    // 0x68 - 0x6F  Pop and store into temp 0..7
    POP_STORE_TEMP_0 = 0x68,
    POP_STORE_TEMP_1 = 0x69,
    POP_STORE_TEMP_2 = 0x6A,
    POP_STORE_TEMP_3 = 0x6B,
    POP_STORE_TEMP_4 = 0x6C,
    POP_STORE_TEMP_5 = 0x6D,
    POP_STORE_TEMP_6 = 0x6E,
    POP_STORE_TEMP_7 = 0x6F,

    // 0x70 - 0x77  Push specials
    PUSH_SELF      = 0x70, // push receiver
    PUSH_TRUE      = 0x71,
    PUSH_FALSE     = 0x72,
    PUSH_NIL       = 0x73,
    PUSH_MINUS_ONE = 0x74,
    PUSH_ZERO      = 0x75,
    PUSH_ONE       = 0x76,
    PUSH_TWO       = 0x77,

    // 0x78 - 0x7F  Return variants (Blue Book Chapter 28)
    RETURN_SELF        = 0x78, // return receiver (self)
    RETURN_TRUE        = 0x79, // return true singleton
    RETURN_FALSE       = 0x7A, // return false singleton
    RETURN_NIL         = 0x7B, // return nil
    // Blue Book 0x7C: "Return Stack Top From Message"
    // - In MethodContext: normal method return
    // - In BlockContext: non-local return to home.sender
    RETURN_TOP_FROM_MESSAGE = 0x7C,
    // Blue Book 0x7D: "Return Stack Top From Block"
    // - Local return from block to whoever sent value/value:/etc.
    // - Used for implicit block returns (last statement value)
    RETURN_TOP_FROM_BLOCK = 0x7D,
    // 0x7E-0x7F: Reserved/unused per Blue Book
    RESERVED_126       = 0x7E,
    RESERVED_127       = 0x7F,

    // Legacy aliases for backward compatibility during transition
    RETURN_TOP         = RETURN_TOP_FROM_MESSAGE, // alias

    // 0x80 - 0x86  Blue Book Extended Operations
    // These follow the Blue Book encoding: 0x80-0x82 use type:index format,
    // 0x83-0x86 are extended sends with their own formats
    EXTENDED_PUSH         = 0x80, // Blue Book: ttiiiii format (tt=type, iiiiii=index)
    EXTENDED_STORE        = 0x81, // Blue Book: ttiiiii format (tt=type, iiiiii=index), no pop
    EXTENDED_POP_STORE    = 0x82, // Blue Book: ttiiiii format (tt=type, iiiiii=index), with pop
    SINGLE_EXTENDED_SEND  = 0x83, // Blue Book: nnnlllll (nnn=argCount, lllll=literalIndex)
    DOUBLE_EXTENDED_SEND  = 0x84, // Blue Book: 2 bytes (argCount, literalIndex)
    SINGLE_EXTENDED_SUPER = 0x85, // Blue Book: nnnlllll (nnn=argCount, lllll=literalIndex)
    DOUBLE_EXTENDED_SUPER = 0x86, // Blue Book: 2 bytes (argCount, literalIndex)

    // 0x87 - 0x88  Blue Book Stack Operations
    POP                   = 0x87, // Pop stack top (Blue Book standard)
    DUP                   = 0x88, // Duplicate stack top (Blue Book standard)

    // 0x89 - Push active context (for blockCopy: primitive)
    PUSH_ACTIVE_CONTEXT = 0x89, // LdThisContext - pushes current execution context

    // 0x8A..0x8F reserved

    // 0x90 - 0x97  Blue Book Short Unconditional Jump (jump forward 1-8 bytes)
    SHORT_JUMP_0 = 0x90, // Jump forward 1 byte
    SHORT_JUMP_1 = 0x91, // Jump forward 2 bytes
    SHORT_JUMP_2 = 0x92, // Jump forward 3 bytes
    SHORT_JUMP_3 = 0x93, // Jump forward 4 bytes
    SHORT_JUMP_4 = 0x94, // Jump forward 5 bytes
    SHORT_JUMP_5 = 0x95, // Jump forward 6 bytes
    SHORT_JUMP_6 = 0x96, // Jump forward 7 bytes
    SHORT_JUMP_7 = 0x97, // Jump forward 8 bytes

    // 0x98 - 0x9F  Blue Book Short Conditional Jump on False (jump forward 1-8 bytes if false)
    SHORT_JUMP_FALSE_0 = 0x98, // Jump forward 1 byte if false
    SHORT_JUMP_FALSE_1 = 0x99, // Jump forward 2 bytes if false
    SHORT_JUMP_FALSE_2 = 0x9A, // Jump forward 3 bytes if false
    SHORT_JUMP_FALSE_3 = 0x9B, // Jump forward 4 bytes if false
    SHORT_JUMP_FALSE_4 = 0x9C, // Jump forward 5 bytes if false
    SHORT_JUMP_FALSE_5 = 0x9D, // Jump forward 6 bytes if false
    SHORT_JUMP_FALSE_6 = 0x9E, // Jump forward 7 bytes if false
    SHORT_JUMP_FALSE_7 = 0x9F, // Jump forward 8 bytes if false

    // 0xA0 - 0xA7  Blue Book Long Unconditional Jump
    // Blue Book formula: offset = ((bytecode & 0x07) - 4) * 256 + nextByte
    // This gives range -1024 to +1023 (backward and forward jumps)
    LONG_JUMP_0 = 0xA0, // Long jump with offset formula
    LONG_JUMP_1 = 0xA1,
    LONG_JUMP_2 = 0xA2,
    LONG_JUMP_3 = 0xA3,
    LONG_JUMP_4 = 0xA4,
    LONG_JUMP_5 = 0xA5,
    LONG_JUMP_6 = 0xA6,
    LONG_JUMP_7 = 0xA7,

    // 0xA8 - 0xAB  Blue Book Long Conditional Jump on True
    // Blue Book formula: offset = (bytecode & 0x03) * 256 + nextByte
    // Range 0-1023 (forward only)
    LONG_JUMP_TRUE_0 = 0xA8,
    LONG_JUMP_TRUE_1 = 0xA9,
    LONG_JUMP_TRUE_2 = 0xAA,
    LONG_JUMP_TRUE_3 = 0xAB,

    // 0xAC - 0xAF  Blue Book Long Conditional Jump on False
    // Blue Book formula: offset = (bytecode & 0x03) * 256 + nextByte
    // Range 0-1023 (forward only)
    LONG_JUMP_FALSE_0 = 0xAC,
    LONG_JUMP_FALSE_1 = 0xAD,
    LONG_JUMP_FALSE_2 = 0xAE,
    LONG_JUMP_FALSE_3 = 0xAF,

    // 0xB0 - 0xBF  Send arithmetic selector with 0, 1, or 2 args
    SEND_ARITH_PLUS        = 0xB0, // +
    SEND_ARITH_MINUS       = 0xB1, // -
    SEND_ARITH_LT          = 0xB2, // <
    SEND_ARITH_GT          = 0xB3, // >
    SEND_ARITH_LE          = 0xB4, // <=
    SEND_ARITH_GE          = 0xB5, // >=
    SEND_ARITH_EQ          = 0xB6, // =
    SEND_ARITH_NE          = 0xB7, // ~=
    SEND_ARITH_TIMES       = 0xB8, // *
    SEND_ARITH_DIVIDE      = 0xB9, // /
    SEND_ARITH_MOD         = 0xBA, // \\
    SEND_ARITH_AT          = 0xBB, // @
    SEND_ARITH_BITSHIFT    = 0xBC, // bitShift:
    SEND_ARITH_INTDIV      = 0xBD, // //
    SEND_ARITH_BITAND      = 0xBE, // bitAnd:
    SEND_ARITH_BITOR       = 0xBF, // bitOr:

    // 0xC0 - 0xCF  Send special selector
    SEND_SPECIAL_AT        = 0xC0, // at:
    SEND_SPECIAL_AT_PUT    = 0xC1, // at:put:
    SEND_SPECIAL_SIZE      = 0xC2, // size
    SEND_SPECIAL_NEXT      = 0xC3, // next
    SEND_SPECIAL_NEXTPUT   = 0xC4, // nextPut:
    SEND_SPECIAL_ATEND     = 0xC5, // atEnd
    SEND_SPECIAL_IDENTICAL = 0xC6, // ==
    SEND_SPECIAL_CLASS     = 0xC7, // class
    SEND_SPECIAL_BLOCKCOPY = 0xC8, // blockCopy:
    SEND_SPECIAL_VALUE     = 0xC9, // value
    SEND_SPECIAL_VALUE1    = 0xCA, // value:
    SEND_SPECIAL_DO        = 0xCB, // do:
    SEND_SPECIAL_NEW       = 0xCC, // new
    SEND_SPECIAL_NEW1      = 0xCD, // new:
    SEND_SPECIAL_X         = 0xCE, // x
    SEND_SPECIAL_Y         = 0xCF, // y

    // 0xD0 - 0xDF  Send literal selector with 0 args
    SEND_LITERAL_0_BASE = 0xD0,
    SEND_LITERAL_0_MAX = 0xDF,

    // 0xE0 - 0xEF  Send literal selector with 1 arg
    SEND_LITERAL_1_BASE = 0xE0,
    SEND_LITERAL_1_MAX = 0xEF,

    // 0xF0 - 0xFF  Send literal selector with 2 args
    SEND_LITERAL_2_BASE = 0xF0,
    SEND_LITERAL_2_MAX = 0xFF,
}

/// <summary>
/// Blue Book Extended Bytecode Type Codes.
/// Used in the extension byte for bytecodes 0x80 (extended push), 0x81 (extended store),
/// and 0x82 (extended pop and store). The type is encoded in bits 6-7 of the extension byte.
/// </summary>
public enum ExtendedType : byte
{
    ReceiverVariable = 0,  // Instance variable
    TemporaryVariable = 1, // Temp/arg
    LiteralConstant = 2,   // Literal constant
    LiteralVariable = 3    // Association/global/pool variable
}

/// <summary>
/// LEGACY: Sub-opcodes for non-Blue Book extended format (0x80 prefix system).
/// This is the OLD C# compiler format that is being phased out.
/// New code should use the Blue Book bytecodes (0x80-0x82 with type:index encoding).
/// </summary>
[Obsolete("Use Blue Book bytecode format instead. This legacy format is being phased out.")]
public enum ExtendedSubOp : byte
{
    // Push/Store extended (LEGACY - use Blue Book 0x80-0x82 with type:index instead)
    PUSH_IVAR      = 0x00, // next byte = ivar index
    STORE_IVAR     = 0x01, // next byte = ivar index
    POP_STORE_IVAR = 0x02, // next byte = ivar index
    PUSH_TEMP      = 0x03, // next byte = temp index
    STORE_TEMP     = 0x04, // next byte = temp index
    POP_STORE_TEMP = 0x05, // next byte = temp index
    PUSH_LITERAL   = 0x06, // next byte = literal index
    PUSH_LITERAL_VAR = 0x07, // next byte = literal var index
    STORE_LITERAL_VAR = 0x08, // next byte = literal var index
    POP_STORE_LITERAL_VAR = 0x09, // next byte = literal var index

    // Send extended (LEGACY - use Blue Book 0x83-0x86 instead)
    SEND_0         = 0x0A, // next byte = selector index (0 args)
    SEND_1         = 0x0B, // next byte = selector index (1 arg)
    SEND_2         = 0x0C, // next byte = selector index (2 args)
    SEND_N         = 0x0D, // next 2 bytes: selector index, arg count
    SEND_SUPER_0   = 0x0E, // next byte = selector index (super, 0 args)
    SEND_SUPER_1   = 0x0F, // next byte = selector index (super, 1 arg)
    SEND_SUPER_2   = 0x10, // next byte = selector index (super, 2 args)
    SEND_SUPER_N   = 0x11, // next 2 bytes: selector index, arg count (super)

    // Stack operations (LEGACY - use Blue Book 0x87/0x88 instead)
    POP            = 0x12, // pop and discard top
    DUP            = 0x13, // duplicate top

    // Control flow (LEGACY - use Blue Book 0x90-0xAF instead)
    JUMP_TRUE      = 0x14, // next 2 bytes = signed offset; pop and jump if true
    JUMP_FALSE     = 0x15, // next 2 bytes = signed offset; pop and jump if false
    JUMP           = 0x16, // next 2 bytes = signed offset; unconditional jump
    JUMP_LONG_TRUE = 0x17, // next 2 bytes = offset; like JUMP_TRUE
    JUMP_LONG_FALSE = 0x18, // next 2 bytes = offset; like JUMP_FALSE
    JUMP_LONG      = 0x19, // next 2 bytes = offset; like JUMP

    // 0x1A..0xFF reserved or VM-specific
}

/// <summary>
/// Helper utilities for decoding bytecode ranges and extracting embedded indices.
/// Updated for Blue Book compliance.
/// </summary>
public static class BytecodeUtil
{
    // Push instance variable (0x00 - 0x0F)
    public static bool IsPushIvar(byte op) => op <= 0x0F;
    public static int PushIvarIndex(byte op) => op & 0x0F;

    // Push temporary/argument (0x10 - 0x1F)
    // Blue Book Chapter 28: "16-31 | 0001iiii | Push Temporary Location #iiii"
    // All 16 bytecodes in this range are PUSH_TEMP (0x10 through 0x1F)
    public static bool IsPushTemp(byte op) => op >= 0x10 && op <= 0x1F;
    public static int PushTempIndex(byte op) => op & 0x0F;

    // Push literal constant (0x20 - 0x3F)
    public static bool IsPushLiteralConst(byte op) => op >= 0x20 && op <= 0x3F;
    public static int PushLiteralConstIndex(byte op) => op & 0x1F;

    // Push literal variable/association (0x40 - 0x5F)
    public static bool IsPushLiteralVar(byte op) => op >= 0x40 && op <= 0x5F;
    public static int PushLiteralVarIndex(byte op) => op & 0x1F;

    // Pop and store instance variable (0x60 - 0x67)
    public static bool IsPopStoreIvar(byte op) => op >= 0x60 && op <= 0x67;
    public static int PopStoreIvarIndex(byte op) => op & 0x07;

    // Pop and store temporary (0x68 - 0x6F)
    public static bool IsPopStoreTemp(byte op) => op >= 0x68 && op <= 0x6F;
    public static int PopStoreTempIndex(byte op) => op & 0x07;

    // Push specials (0x70 - 0x77)
    public static bool IsPushSpecial(byte op) => op >= 0x70 && op <= 0x77;

    // Return variants (0x78 - 0x7F)
    public static bool IsReturn(byte op) => op >= 0x78 && op <= 0x7F;

    // Blue Book Extended Operations (0x80 - 0x86)
    // 0x80: Extended push with type:index encoding
    // 0x81: Extended store (no pop) with type:index encoding
    // 0x82: Extended pop and store with type:index encoding
    // 0x83: Single extended send
    // 0x84: Double extended send
    // 0x85: Single extended super send
    // 0x86: Double extended super send
    public static bool IsExtendedPush(byte op) => op == 0x80;
    public static bool IsExtendedStore(byte op) => op == 0x81;
    public static bool IsExtendedPopStore(byte op) => op == 0x82;
    public static bool IsSingleExtendedSend(byte op) => op == 0x83;
    public static bool IsDoubleExtendedSend(byte op) => op == 0x84;
    public static bool IsSingleExtendedSuperSend(byte op) => op == 0x85;
    public static bool IsDoubleExtendedSuperSend(byte op) => op == 0x86;

    // LEGACY: Extended prefix check (for backward compatibility during transition)
    public static bool IsExtended(byte op) => op == 0x80;

    // Blue Book Stack Operations (0x87 - 0x88)
    public static bool IsPop(byte op) => op == 0x87;
    public static bool IsDup(byte op) => op == 0x88;

    // Blue Book Short Unconditional Jump (0x90 - 0x97)
    // Jump distance = (bytecode & 0x07) + 1 (range 1-8)
    public static bool IsShortJump(byte op) => op >= 0x90 && op <= 0x97;
    public static int ShortJumpDistance(byte op) => (op & 0x07) + 1;

    // Blue Book Short Conditional Jump on False (0x98 - 0x9F)
    // Jump distance = (bytecode & 0x07) + 1 (range 1-8)
    public static bool IsShortJumpFalse(byte op) => op >= 0x98 && op <= 0x9F;
    public static int ShortJumpFalseDistance(byte op) => (op & 0x07) + 1;

    // Blue Book Long Unconditional Jump (0xA0 - 0xA7)
    // Offset = ((bytecode & 0x07) - 4) * 256 + nextByte
    // Range: -1024 to +1023 (backward and forward)
    public static bool IsLongJump(byte op) => op >= 0xA0 && op <= 0xA7;
    public static int LongJumpBaseOffset(byte op) => ((op & 0x07) - 4) * 256;

    // Blue Book Long Conditional Jump on True (0xA8 - 0xAB)
    // Offset = (bytecode & 0x03) * 256 + nextByte
    // Range: 0-1023 (forward only)
    public static bool IsLongJumpTrue(byte op) => op >= 0xA8 && op <= 0xAB;
    public static int LongJumpTrueBaseOffset(byte op) => (op & 0x03) * 256;

    // Blue Book Long Conditional Jump on False (0xAC - 0xAF)
    // Offset = (bytecode & 0x03) * 256 + nextByte
    // Range: 0-1023 (forward only)
    public static bool IsLongJumpFalse(byte op) => op >= 0xAC && op <= 0xAF;
    public static int LongJumpFalseBaseOffset(byte op) => (op & 0x03) * 256;

    // Any jump bytecode (0x90 - 0xAF)
    public static bool IsJump(byte op) => op >= 0x90 && op <= 0xAF;

    // Send arithmetic (0xB0 - 0xBF)
    public static bool IsSendArithmetic(byte op) => op >= 0xB0 && op <= 0xBF;
    public static int SendArithmeticIndex(byte op) => op & 0x0F;

    // Send special (0xC0 - 0xCF)
    public static bool IsSendSpecial(byte op) => op >= 0xC0 && op <= 0xCF;
    public static int SendSpecialIndex(byte op) => op & 0x0F;

    // Send literal selector with 0 args (0xD0 - 0xDF)
    public static bool IsSend0(byte op) => op >= 0xD0 && op <= 0xDF;
    public static int Send0Index(byte op) => op & 0x0F;

    // Send literal selector with 1 arg (0xE0 - 0xEF)
    public static bool IsSend1(byte op) => op >= 0xE0 && op <= 0xEF;
    public static int Send1Index(byte op) => op & 0x0F;

    // Send literal selector with 2 args (0xF0 - 0xFF)
    public static bool IsSend2(byte op) => op >= 0xF0 && op <= 0xFF;
    public static int Send2Index(byte op) => op & 0x0F;

    // Any send operation (0xB0 - 0xFF)
    public static bool IsSend(byte op) => op >= 0xB0;

    // Blue Book Extended Type Decoding Helpers
    // For bytecodes 0x80-0x82, the extension byte has format: ttiiiiiii
    // tt = type (00=ivar, 01=temp, 10=literal const, 11=literal var)
    // iiiiii = index (0-63)
    public static ExtendedType GetExtendedType(byte extensionByte) =>
        (ExtendedType)((extensionByte >> 6) & 0x03);

    public static int GetExtendedIndex(byte extensionByte) =>
        extensionByte & 0x3F;

    // Blue Book Single Extended Send Decoding Helpers
    // For bytecode 0x83, extension byte has format: nnnlllll
    // nnn = arg count (0-7)
    // lllll = literal index (0-31)
    public static int GetSingleExtendedSendArgCount(byte extensionByte) =>
        (extensionByte >> 5) & 0x07;

    public static int GetSingleExtendedSendLiteralIndex(byte extensionByte) =>
        extensionByte & 0x1F;

    // Helper to create Blue Book extended push/store extension byte
    public static byte MakeExtendedByte(ExtendedType type, int index) =>
        (byte)(((int)type << 6) | (index & 0x3F));

    // Helper to create Blue Book single extended send extension byte
    public static byte MakeSingleExtendedSendByte(int argCount, int literalIndex) =>
        (byte)(((argCount & 0x07) << 5) | (literalIndex & 0x1F));
}
