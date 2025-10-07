using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Interpreter.Core.Bytecodes;

/// <summary>
/// Validates that all bytecode operands (literal indices, temp indices, etc.)
/// reference valid entries in the compiled method.
/// </summary>
public static class BytecodeOperandValidator
{
    /// <summary>
    /// Result of validating bytecode operands.
    /// </summary>
    public record ValidationResult
    {
        public required bool IsValid { get; init; }
        public required string? ErrorMessage { get; init; }
        public required int? ErrorOffset { get; init; }
    }

    /// <summary>
    /// Validates all bytecode operands against method metadata.
    /// </summary>
    /// <param name="bytecodes">The bytecode array to validate</param>
    /// <param name="literalCount">Number of literals in the method</param>
    /// <param name="tempCount">Number of temporary variables</param>
    /// <param name="argCount">Number of arguments</param>
    /// <param name="instVarCount">Number of instance variables in the class</param>
    /// <returns>Validation result indicating success or first error found</returns>
    public static ValidationResult Validate(
        byte[] bytecodes,
        int literalCount,
        int tempCount,
        int argCount,
        int instVarCount)
    {
        if (bytecodes == null || bytecodes.Length == 0)
        {
            return new ValidationResult
            {
                IsValid = true,
                ErrorMessage = null,
                ErrorOffset = null
            };
        }

        int pc = 0;

        // In Smalltalk, arguments and temps share the same frame space
        // Arguments occupy indices 0..argCount-1, temps occupy argCount..argCount+tempCount-1
        int totalTempSlots = argCount + tempCount;

        while (pc < bytecodes.Length)
        {
            int startPc = pc;
            byte b = bytecodes[pc++];

            // Validate based on bytecode type per Interpreter-Architecture.md specification
            var error = b switch
            {
                // 0x00-0x0F: PUSH_IVAR_0..15
                >= 0 and < 16 => ValidateInstVar(b, instVarCount, startPc),

                // 0x10-0x1F: PUSH_TEMP_0..15
                >= 16 and < 32 => ValidateTemp(b - 16, totalTempSlots, startPc),

                // 0x20-0x3F: PUSH_LITERAL_CONST_0..31
                >= 32 and < 64 => ValidateLiteral(b - 32, literalCount, startPc),

                // 0x40-0x5F: PUSH_LITERAL_VAR_0..31
                >= 64 and < 96 => ValidateLiteral(b - 64, literalCount, startPc),

                // 0x60-0x67: POP_STORE_IVAR_0..7
                >= 96 and < 104 => ValidateInstVar(b - 96, instVarCount, startPc),

                // 0x68-0x6F: POP_STORE_TEMP_0..7
                >= 104 and < 112 => ValidateTemp(b - 104, totalTempSlots, startPc),

                // 0x70-0x77: Push special constants (self, true, false, nil, -1, 0, 1, 2)
                >= 112 and < 120 => null,

                // 0x78-0x7F: Return variants (self, true, false, nil, top, block returns, NOP)
                >= 120 and < 128 => null,

                // 0x80: EXTENDED_PREFIX
                128 => ValidateExtended(bytecodes, ref pc, literalCount, totalTempSlots, instVarCount, startPc),

                // 0x81-0x88: Reserved
                >= 129 and < 137 => null,

                // 0x89: PUSH_ACTIVE_CONTEXT (for block creation)
                137 => null,

                // 0x8A-0x8F: Reserved
                >= 138 and < 144 => null,

                // 0x90-0x97: SHORT_JUMP_FWD_1..8 (single byte, no operands)
                >= 144 and < 152 => null,

                // 0x98-0x9F: Reserved
                >= 152 and < 160 => null,

                // 0xA0: MEDIUM_JUMP_FWD (1 byte offset)
                160 => SkipOneByte(bytecodes, ref pc, startPc),

                // 0xA1-0xA7: LONG_JUMP_FWD_2..8 (multi-byte offset)
                >= 161 and < 168 => SkipLongJump(bytecodes, ref pc, b - 159, startPc),

                // 0xA8-0xAF: Reserved
                >= 168 and < 176 => null,

                // 0xB0-0xBF: SEND_ARITH (arithmetic operators)
                >= 176 and < 192 => null,

                // 0xC0-0xCF: SEND_SPECIAL (special selectors)
                >= 192 and < 208 => null,

                // 0xD0-0xDF: SEND_LIT_0_ARGS (send literal selector 0-15 with 0 args)
                >= 208 and < 224 => ValidateLiteral(b - 208, literalCount, startPc),

                // 0xE0-0xEF: SEND_LIT_1_ARG (send literal selector 0-15 with 1 arg)
                >= 224 and < 240 => ValidateLiteral(b - 224, literalCount, startPc),

                // 0xF0-0xFF: SEND_LIT_2_ARGS (send literal selector 0-15 with 2 args)
                >= 240 and <= 255 => ValidateLiteral(b - 240, literalCount, startPc)
            };

            if (error != null)
                return error;
        }

        return new ValidationResult
        {
            IsValid = true,
            ErrorMessage = null,
            ErrorOffset = null
        };
    }

    private static ValidationResult? ValidateInstVar(int index, int instVarCount, int offset)
    {
        if (index >= instVarCount)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Instance variable index {index} out of range (max {instVarCount - 1})",
                ErrorOffset = offset
            };
        }
        return null;
    }

    private static ValidationResult? ValidateTemp(int index, int tempCount, int offset)
    {
        if (index >= tempCount)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Temporary variable index {index} out of range (max {tempCount - 1})",
                ErrorOffset = offset
            };
        }
        return null;
    }

    private static ValidationResult? ValidateLiteral(int index, int literalCount, int offset)
    {
        if (index >= literalCount)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Literal index {index} out of range (max {literalCount - 1})",
                ErrorOffset = offset
            };
        }
        return null;
    }

    private static ValidationResult? ValidateExtended(
        byte[] bytecodes,
        ref int pc,
        int literalCount,
        int tempCount,
        int instVarCount,
        int startPc)
    {
        if (pc >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Truncated extended bytecode (missing sub-opcode)",
                ErrorOffset = startPc
            };
        }

        byte subOp = bytecodes[pc++];

        return subOp switch
        {
            // PUSH_IVAR - has operand byte (inst var index)
            0x00 => ValidateOperandByte(bytecodes, ref pc, instVarCount, "instance variable", startPc),

            // STORE_IVAR, POP_STORE_IVAR
            0x01 or 0x02 => ValidateOperandByte(bytecodes, ref pc, instVarCount, "instance variable", startPc),

            // PUSH_TEMP - has operand byte (temp index)
            0x03 => ValidateOperandByte(bytecodes, ref pc, tempCount, "temporary variable", startPc),

            // STORE_TEMP, POP_STORE_TEMP
            0x04 or 0x05 => ValidateOperandByte(bytecodes, ref pc, tempCount, "temporary variable", startPc),

            // PUSH_LITERAL - has operand byte (literal index)
            0x06 => ValidateOperandByte(bytecodes, ref pc, literalCount, "literal", startPc),

            // PUSH_LITERAL_VAR - has operand byte (literal index)
            0x07 => ValidateOperandByte(bytecodes, ref pc, literalCount, "literal", startPc),

            // STORE_LITERAL_VAR, POP_STORE_LITERAL_VAR
            0x08 or 0x09 => ValidateOperandByte(bytecodes, ref pc, literalCount, "literal", startPc),

            // SEND_0, SEND_1, SEND_2 - has operand byte (selector literal index)
            0x0A or 0x0B or 0x0C => ValidateOperandByte(bytecodes, ref pc, literalCount, "selector literal", startPc),

            // SEND_N - has two operand bytes (selector literal index, arg count)
            0x0D => ValidateSendN(bytecodes, ref pc, literalCount, startPc),

            // SEND_SUPER_0, SEND_SUPER_1, SEND_SUPER_2
            0x0E or 0x0F or 0x10 => ValidateOperandByte(bytecodes, ref pc, literalCount, "selector literal", startPc),

            // SEND_SUPER_N - has two operand bytes (selector literal index, arg count)
            0x11 => ValidateSendN(bytecodes, ref pc, literalCount, startPc),

            // POP, DUP - no operands
            0x12 or 0x13 => null,

            // Jump bytecodes - have 2-byte offset, don't reference method metadata
            0x14 or 0x15 or 0x16 or 0x17 or 0x18 or 0x19 => SkipTwoBytes(bytecodes, ref pc, startPc),

            _ => new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Unknown extended sub-opcode 0x{subOp:X2}",
                ErrorOffset = startPc
            }
        };
    }

    private static ValidationResult? ValidateOperandByte(
        byte[] bytecodes,
        ref int pc,
        int maxValue,
        string valueName,
        int startPc)
    {
        if (pc >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Truncated extended bytecode (missing {valueName} index)",
                ErrorOffset = startPc
            };
        }

        int index = bytecodes[pc++];
        if (index >= maxValue)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Extended {valueName} index {index} out of range (max {maxValue - 1})",
                ErrorOffset = startPc
            };
        }

        return null;
    }

    private static ValidationResult? ValidateSendN(
        byte[] bytecodes,
        ref int pc,
        int literalCount,
        int startPc)
    {
        // First byte: selector literal index
        var error = ValidateOperandByte(bytecodes, ref pc, literalCount, "selector literal", startPc);
        if (error != null) return error;

        // Second byte: arg count (just verify it exists, any value 0-255 is valid)
        if (pc >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Truncated SEND_N bytecode (missing arg count)",
                ErrorOffset = startPc
            };
        }

        pc++; // Skip arg count
        return null;
    }

    private static ValidationResult? SkipOneByte(byte[] bytecodes, ref int pc, int startPc)
    {
        if (pc >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Truncated jump bytecode (missing offset byte)",
                ErrorOffset = startPc
            };
        }

        pc++; // Skip the offset byte
        return null;
    }

    private static ValidationResult? SkipLongJump(byte[] bytecodes, ref int pc, int numBytes, int startPc)
    {
        // Long jumps (0xA1-0xA7) have N-byte offsets where N = bytecode - 159
        // N ranges from 2 to 8
        if (pc + numBytes - 1 >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Truncated long jump bytecode (missing {numBytes} offset bytes)",
                ErrorOffset = startPc
            };
        }

        pc += numBytes; // Skip N offset bytes
        return null;
    }

    private static ValidationResult? SkipTwoBytes(byte[] bytecodes, ref int pc, int startPc)
    {
        if (pc + 1 >= bytecodes.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Truncated jump bytecode (missing offset bytes)",
                ErrorOffset = startPc
            };
        }

        pc += 2;
        return null;
    }
}
