# Bytecode Enums and Utilities

This document describes the bytecode enum definitions and helper utilities added to improve code clarity and maintainability in the Smalltalk-80 interpreter.

## Overview

The bytecode handling code previously used magic numbers and range checks (e.g., `if (b >= 176 && b <= 191)`). This has been improved with:

1. **Bytecode enum** - Named constants for all 256 bytecode instructions
2. **ExtendedSubOp enum** - Sub-operation codes for extended prefix (0x80) instructions
3. **BytecodeUtil helpers** - Range checking and index extraction utilities

## Files

### BytecodeDefinitions.cs

Location: `Smalltalk80.Interpreter/Core/Bytecodes/BytecodeDefinitions.cs`

Contains:
- `Bytecode` enum with all 256 opcodes (0x00-0xFF)
- `ExtendedSubOp` enum for extended prefix sub-operations
- `BytecodeUtil` static class with helper methods

## Bytecode Enum Structure

### Push Instructions (0x00-0x5F)

```csharp
// 0x00-0x0F: Push receiver instance variables 0-15
PUSH_IVAR_0 = 0x00,
PUSH_IVAR_1 = 0x01,
// ...
PUSH_IVAR_15 = 0x0F,

// 0x10-0x1F: Push temporary/argument 0-15
PUSH_TEMP_0 = 0x10,
PUSH_TEMP_1 = 0x11,
// ...
PUSH_TEMP_15 = 0x1F,

// 0x20-0x3F: Push literal constant 0-31
PUSH_LITERAL_CONST_0 = 0x20,
// ...
PUSH_LITERAL_CONST_31 = 0x3F,

// 0x40-0x5F: Push literal variable 0-31
PUSH_LITERAL_VAR_0 = 0x40,
// ...
PUSH_LITERAL_VAR_31 = 0x5F,
```

### Store Instructions (0x60-0x6F)

```csharp
// 0x60-0x67: Pop and store instance variable 0-7
POP_STORE_IVAR_0 = 0x60,
// ...
POP_STORE_IVAR_7 = 0x67,

// 0x68-0x6F: Pop and store temporary 0-7
POP_STORE_TEMP_0 = 0x68,
// ...
POP_STORE_TEMP_7 = 0x6F,
```

### Special Constants (0x70-0x77)

```csharp
PUSH_SELF      = 0x70,
PUSH_TRUE      = 0x71,
PUSH_FALSE     = 0x72,
PUSH_NIL       = 0x73,
PUSH_MINUS_ONE = 0x74,
PUSH_ZERO      = 0x75,
PUSH_ONE       = 0x76,
PUSH_TWO       = 0x77,
```

### Return Instructions (0x78-0x7F)

```csharp
RETURN_TOP         = 0x78,  // pop top and return it
RETURN_SELF        = 0x79,  // return receiver
RETURN_FALSE       = 0x7A,
RETURN_TRUE        = 0x7B,
RETURN_NIL         = 0x7C,
RETURN_SELF_POP    = 0x7D,  // variant; treat as alias of RETURN_SELF
RETURN_TOP_POP     = 0x7E,  // variant; treat as alias of RETURN_TOP
RESERVED_RETURN    = 0x7F,
```

### Send Instructions (0xB0-0xFF)

```csharp
// 0xB0-0xBF: Send arithmetic selector
SEND_ARITH_PLUS    = 0xB0,  // +
SEND_ARITH_MINUS   = 0xB1,  // -
SEND_ARITH_LT      = 0xB2,  // <
SEND_ARITH_GT      = 0xB3,  // >
// ...
SEND_ARITH_BITOR   = 0xBF,  // bitOr:

// 0xC0-0xCF: Send special selector
SEND_SPECIAL_AT        = 0xC0,  // at:
SEND_SPECIAL_AT_PUT    = 0xC1,  // at:put:
SEND_SPECIAL_SIZE      = 0xC2,  // size
// ...
SEND_SPECIAL_Y         = 0xCF,  // y

// 0xD0-0xDF: Send literal selector with 0 args
SEND_LITERAL_0_BASE = 0xD0,
SEND_LITERAL_0_MAX  = 0xDF,

// 0xE0-0xEF: Send literal selector with 1 arg
SEND_LITERAL_1_BASE = 0xE0,
SEND_LITERAL_1_MAX  = 0xEF,

// 0xF0-0xFF: Send literal selector with 2 args
SEND_LITERAL_2_BASE = 0xF0,
SEND_LITERAL_2_MAX  = 0xFF,
```

## ExtendedSubOp Enum

Sub-operations for the extended prefix (0x80):

```csharp
public enum ExtendedSubOp : byte
{
    // Push/Store extended
    PUSH_IVAR      = 0x00,  // next byte = ivar index
    STORE_IVAR     = 0x01,  // next byte = ivar index
    POP_STORE_IVAR = 0x02,  // next byte = ivar index
    PUSH_TEMP      = 0x03,  // next byte = temp index
    STORE_TEMP     = 0x04,  // next byte = temp index
    POP_STORE_TEMP = 0x05,  // next byte = temp index
    PUSH_LITERAL   = 0x06,  // next byte = literal index
    PUSH_LITERAL_VAR = 0x07,  // next byte = literal var index
    STORE_LITERAL_VAR = 0x08,  // next byte = literal var index
    POP_STORE_LITERAL_VAR = 0x09,  // next byte = literal var index

    // Send extended
    SEND_0         = 0x0A,  // next byte = selector index (0 args)
    SEND_1         = 0x0B,  // next byte = selector index (1 arg)
    SEND_2         = 0x0C,  // next byte = selector index (2 args)
    SEND_N         = 0x0D,  // next 2 bytes: selector index, arg count
    SEND_SUPER_0   = 0x0E,  // next byte = selector index (super, 0 args)
    SEND_SUPER_1   = 0x0F,  // next byte = selector index (super, 1 arg)
    SEND_SUPER_2   = 0x10,  // next byte = selector index (super, 2 args)
    SEND_SUPER_N   = 0x11,  // next 2 bytes: selector index, arg count (super)

    // Stack operations
    POP            = 0x12,  // pop and discard top
    DUP            = 0x13,  // duplicate top

    // Control flow
    JUMP_TRUE      = 0x14,  // next 2 bytes = signed offset; pop and jump if true
    JUMP_FALSE     = 0x15,  // next 2 bytes = signed offset; pop and jump if false
    JUMP           = 0x16,  // next 2 bytes = signed offset; unconditional jump
    JUMP_LONG_TRUE = 0x17,  // next 2 bytes = offset; like JUMP_TRUE
    JUMP_LONG_FALSE = 0x18,  // next 2 bytes = offset; like JUMP_FALSE
    JUMP_LONG      = 0x19,  // next 2 bytes = offset; like JUMP
}
```

## BytecodeUtil Helper Methods

### Range Checkers

```csharp
// Check if bytecode is in a specific range
public static bool IsPushIvar(byte op) => op <= 0x0F;
public static bool IsPushTemp(byte op) => op >= 0x10 && op <= 0x1F;
public static bool IsPushLiteralConst(byte op) => op >= 0x20 && op <= 0x3F;
public static bool IsPushLiteralVar(byte op) => op >= 0x40 && op <= 0x5F;
public static bool IsPopStoreIvar(byte op) => op >= 0x60 && op <= 0x67;
public static bool IsPopStoreTemp(byte op) => op >= 0x68 && op <= 0x6F;
public static bool IsPushSpecial(byte op) => op >= 0x70 && op <= 0x77;
public static bool IsReturn(byte op) => op >= 0x78 && op <= 0x7F;
public static bool IsExtended(byte op) => op == 0x80;
public static bool IsSendArithmetic(byte op) => op >= 0xB0 && op <= 0xBF;
public static bool IsSendSpecial(byte op) => op >= 0xC0 && op <= 0xCF;
public static bool IsSend0(byte op) => op >= 0xD0 && op <= 0xDF;
public static bool IsSend1(byte op) => op >= 0xE0 && op <= 0xEF;
public static bool IsSend2(byte op) => op >= 0xF0 && op <= 0xFF;
public static bool IsSend(byte op) => op >= 0xB0;
```

### Index Extractors

```csharp
// Extract embedded index from bytecode
public static int PushIvarIndex(byte op) => op & 0x0F;
public static int PushTempIndex(byte op) => op & 0x0F;
public static int PushLiteralConstIndex(byte op) => op & 0x1F;
public static int PushLiteralVarIndex(byte op) => op & 0x1F;
public static int PopStoreIvarIndex(byte op) => op & 0x07;
public static int PopStoreTempIndex(byte op) => op & 0x07;
public static int SendArithmeticIndex(byte op) => op & 0x0F;
public static int SendSpecialIndex(byte op) => op & 0x0F;
public static int Send0Index(byte op) => op & 0x0F;
public static int Send1Index(byte op) => op & 0x0F;
public static int Send2Index(byte op) => op & 0x0F;
```

## Usage Examples

### Before (Magic Numbers)

```csharp
// Hard to understand what this checks
if (b >= 176 && b <= 191)
{
    int opIndex = b - 176;
    // ...
}
```

### After (Named Enums and Helpers)

```csharp
// Crystal clear intent
if (BytecodeUtil.IsSendArithmetic(b))
{
    int opIndex = BytecodeUtil.SendArithmeticIndex(b);
    // ...
}
```

### Disassembler Pattern Matching

```csharp
// Using enum in switch expressions
result = ((Bytecode)b) switch
{
    Bytecode.PUSH_SELF => "pushSelf",
    Bytecode.PUSH_TRUE => "pushTrue",
    Bytecode.PUSH_FALSE => "pushFalse",
    Bytecode.PUSH_NIL => "pushNil",
    // ...
};
```

### Extended Sub-Operation Handling

```csharp
// Clear sub-operation dispatch
return ((ExtendedSubOp)subOp) switch
{
    ExtendedSubOp.PUSH_IVAR => "pushReceiver ...",
    ExtendedSubOp.POP_STORE_TEMP => "popStoreTemporary ...",
    ExtendedSubOp.DUP => "dup",
    ExtendedSubOp.POP => "pop",
    // ...
};
```

## Benefits

1. **Self-Documenting Code**: Named constants make bytecode semantics immediately clear
2. **Type Safety**: Enum provides compile-time checking and IntelliSense support
3. **Reduced Errors**: Helper methods eliminate manual range/mask calculations
4. **Maintainability**: Changes to bytecode ranges only require updating one location
5. **Readability**: `IsSendArithmetic(b)` is clearer than `b >= 0xB0 && b <= 0xBF`
6. **Testing**: Easy to enumerate all bytecodes for comprehensive test coverage

## Integration

The BytecodeDisassembler has been fully refactored to use these enums and utilities:

- [BytecodeDefinitions.cs](../Smalltalk80.Interpreter/Core/Bytecodes/BytecodeDefinitions.cs) - Enum and helper definitions
- [BytecodeDisassembler.cs](../Smalltalk80.Interpreter/VM/BytecodeDisassembler.cs) - Disassembler using enums

## Future Enhancements

Possible additions:

1. **Stack Effect Calculator**: Compute stack depth changes per bytecode
2. **Bytecode Validator**: Verify bytecode sequences are well-formed
3. **Instruction Length Table**: Quick lookup for multi-byte instruction sizes
4. **Semantic Categories**: Group bytecodes by semantic function (push, pop, send, jump)
5. **Unit Tests**: Comprehensive test suite covering all 256 bytecodes
