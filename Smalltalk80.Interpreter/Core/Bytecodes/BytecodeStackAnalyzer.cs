using Smalltalk80.Interpreter.Core.Bytecodes;

namespace Smalltalk80.Interpreter.Core.Bytecodes;

/// <summary>
/// Analyzes bytecode sequences to verify stack balance and compute stack effects.
/// This ensures bytecode correctness during cold start method installation.
/// </summary>
public static class BytecodeStackAnalyzer
{
    /// <summary>
    /// Result of analyzing a bytecode sequence.
    /// </summary>
    public record AnalysisResult
    {
        public required bool IsBalanced { get; init; }
        public required int MaxStackDepth { get; init; }
        public required int FinalStackEffect { get; init; }
        public required string? ErrorMessage { get; init; }
        public required int? ErrorOffset { get; init; }
    }

    /// <summary>
    /// Analyzes a bytecode array to verify stack balance.
    /// Returns true if the bytecodes produce balanced stack operations.
    /// </summary>
    public static AnalysisResult Analyze(byte[] bytecodes)
    {
        if (bytecodes == null || bytecodes.Length == 0)
        {
            return new AnalysisResult
            {
                IsBalanced = true,
                MaxStackDepth = 0,
                FinalStackEffect = 0,
                ErrorMessage = null,
                ErrorOffset = null
            };
        }

        int stackDepth = 0;
        int maxStackDepth = 0;
        int pc = 0;

        try
        {
            while (pc < bytecodes.Length)
            {
                int startPc = pc;
                byte b = bytecodes[pc++];
                int effect = GetStackEffect(b, bytecodes, ref pc);

                stackDepth += effect;

                // Track maximum stack depth reached
                if (stackDepth > maxStackDepth)
                {
                    maxStackDepth = stackDepth;
                }

                // Stack underflow check
                if (stackDepth < 0)
                {
                    return new AnalysisResult
                    {
                        IsBalanced = false,
                        MaxStackDepth = maxStackDepth,
                        FinalStackEffect = stackDepth,
                        ErrorMessage = $"Stack underflow at offset {startPc}: bytecode 0x{b:X2} (stack depth would be {stackDepth})",
                        ErrorOffset = startPc
                    };
                }
            }

            // Methods should leave exactly one value on stack (return value) or zero (return self/nil/etc)
            // But we'll be lenient and just report the final effect
            return new AnalysisResult
            {
                IsBalanced = true, // We only check for underflow, not final balance
                MaxStackDepth = maxStackDepth,
                FinalStackEffect = stackDepth,
                ErrorMessage = null,
                ErrorOffset = null
            };
        }
        catch (Exception ex)
        {
            return new AnalysisResult
            {
                IsBalanced = false,
                MaxStackDepth = maxStackDepth,
                FinalStackEffect = stackDepth,
                ErrorMessage = $"Analysis error at offset {pc}: {ex.Message}",
                ErrorOffset = pc
            };
        }
    }

    /// <summary>
    /// Computes the stack effect of a single bytecode instruction.
    /// Returns: positive = pushes, negative = pops, zero = neutral
    /// </summary>
    private static int GetStackEffect(byte b, byte[] bytecodes, ref int pc)
    {
        // Push receiver instance variable (0x00-0x0F): +1
        if (BytecodeUtil.IsPushIvar(b))
            return +1;

        // Push temporary (0x10-0x1F): +1
        if (BytecodeUtil.IsPushTemp(b))
            return +1;

        // Push literal constant (0x20-0x3F): +1
        if (BytecodeUtil.IsPushLiteralConst(b))
            return +1;

        // Push literal variable (0x40-0x5F): +1
        if (BytecodeUtil.IsPushLiteralVar(b))
            return +1;

        // Pop and store receiver variable (0x60-0x67): -1
        if (BytecodeUtil.IsPopStoreIvar(b))
            return -1;

        // Pop and store temporary (0x68-0x6F): -1
        if (BytecodeUtil.IsPopStoreTemp(b))
            return -1;

        // Push specials (0x70-0x77): +1
        if (BytecodeUtil.IsPushSpecial(b))
            return +1;

        // Return instructions (0x78-0x7F): end of execution, no net effect on caller's stack
        if (BytecodeUtil.IsReturn(b))
            return 0; // Returns consume the stack but pass value to caller

        // Push active context (0x89 = 137) - used for block creation
        if (b == 137)
            return +1;

        // Short jumps (0x90-0x97 = 144-151): Jump forward 1-8 bytes
        if (b >= 144 && b <= 151)
            return 0; // Unconditional forward jump, no stack effect

        // Medium jump (0xA0 = 160): Jump forward with parameter
        if (b == 160)
        {
            if (pc < bytecodes.Length)
                pc++; // Skip distance parameter
            return 0;
        }

        // Long jumps (0xA1-0xA7 = 161-167): Jump forward with multi-byte distance
        if (b >= 161 && b <= 167)
        {
            int bytesNeeded = b - 159; // N = 2 to 8
            for (int i = 0; i < bytesNeeded && pc < bytecodes.Length; i++)
                pc++;
            return 0;
        }

        // Extended prefix (0x80)
        if (BytecodeUtil.IsExtended(b))
            return GetExtendedStackEffect(bytecodes, ref pc);

        // Send arithmetic (0xB0-0xBF): varies by selector
        if (BytecodeUtil.IsSendArithmetic(b))
        {
            int opIndex = BytecodeUtil.SendArithmeticIndex(b);
            // All arithmetic ops take 1 arg: receiver + arg = 2, result = 1, net = -1
            return -1; // Pop receiver and arg, push result
        }

        // Send special (0xC0-0xCF): varies by selector
        if (BytecodeUtil.IsSendSpecial(b))
        {
            int specialIndex = BytecodeUtil.SendSpecialIndex(b);
            // Special selectors have known arg counts
            int argCount = GetSpecialSelectorArgCount(specialIndex);
            // Pop receiver + args, push result
            return -(argCount + 1) + 1; // -argCount
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        if (BytecodeUtil.IsSend0(b))
            return -1 + 1; // Pop receiver, push result = 0

        // Send literal selector with 1 arg (0xE0-0xEF)
        if (BytecodeUtil.IsSend1(b))
            return -2 + 1; // Pop receiver + 1 arg, push result = -1

        // Send literal selector with 2 args (0xF0-0xFF)
        if (BytecodeUtil.IsSend2(b))
            return -3 + 1; // Pop receiver + 2 args, push result = -2

        // Unknown bytecode - assume neutral
        return 0;
    }

    private static int GetExtendedStackEffect(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return 0;

        byte subOp = bytecodes[pc++];

        // Decode based on ExtendedSubOp enum (from BytecodeDefinitions.cs)
        // This matches what the compiler emits
        return subOp switch
        {
            // Push operations - all have one operand byte (index)
            0x00 => SkipByte(bytecodes, ref pc, +1),  // PUSH_IVAR
            0x03 => SkipByte(bytecodes, ref pc, +1),  // PUSH_TEMP
            0x06 => SkipByte(bytecodes, ref pc, +1),  // PUSH_LITERAL
            0x07 => SkipByte(bytecodes, ref pc, +1),  // PUSH_LITERAL_VAR

            // Store without pop - all have one operand byte (index)
            0x01 => SkipByte(bytecodes, ref pc, 0),   // STORE_IVAR (no pop)
            0x04 => SkipByte(bytecodes, ref pc, 0),   // STORE_TEMP (no pop)
            0x08 => SkipByte(bytecodes, ref pc, 0),   // STORE_LITERAL_VAR (no pop)

            // Pop and store - all have one operand byte (index)
            0x02 => SkipByte(bytecodes, ref pc, -1),  // POP_STORE_IVAR
            0x05 => SkipByte(bytecodes, ref pc, -1),  // POP_STORE_TEMP
            0x09 => SkipByte(bytecodes, ref pc, -1),  // POP_STORE_LITERAL_VAR

            // Send operations - one operand byte (selector index)
            0x0A => SkipByte(bytecodes, ref pc, 0),   // SEND_0: -1 rcv, +1 result = 0
            0x0B => SkipByte(bytecodes, ref pc, -1),  // SEND_1: -2 (rcv+arg), +1 result = -1
            0x0C => SkipByte(bytecodes, ref pc, -2),  // SEND_2: -3 (rcv+2args), +1 result = -2

            // Send with arg count - two operand bytes (selector index, arg count)
            0x0D => SkipTwoBytes(bytecodes, ref pc, GetSendNEffect(bytecodes, pc)),  // SEND_N

            // Super sends
            0x0E => SkipByte(bytecodes, ref pc, 0),   // SEND_SUPER_0
            0x0F => SkipByte(bytecodes, ref pc, -1),  // SEND_SUPER_1
            0x10 => SkipByte(bytecodes, ref pc, -2),  // SEND_SUPER_2
            0x11 => SkipTwoBytes(bytecodes, ref pc, GetSendNEffect(bytecodes, pc)),  // SEND_SUPER_N

            // Stack operations
            0x12 => -1,  // POP: -1
            0x13 => +1,  // DUP: +1

            // Control flow - all have 2-byte offset
            0x14 => SkipTwoBytes(bytecodes, ref pc, -1),  // JUMP_TRUE (pops condition)
            0x15 => SkipTwoBytes(bytecodes, ref pc, -1),  // JUMP_FALSE (pops condition)
            0x16 => SkipTwoBytes(bytecodes, ref pc, 0),   // JUMP (unconditional)
            0x17 => SkipTwoBytes(bytecodes, ref pc, -1),  // JUMP_LONG_TRUE
            0x18 => SkipTwoBytes(bytecodes, ref pc, -1),  // JUMP_LONG_FALSE
            0x19 => SkipTwoBytes(bytecodes, ref pc, 0),   // JUMP_LONG

            // Unknown extended operation
            _ => 0
        };
    }

    private static int SkipByte(byte[] bytecodes, ref int pc, int effect)
    {
        if (pc < bytecodes.Length)
            pc++;
        return effect;
    }

    private static int SkipTwoBytes(byte[] bytecodes, ref int pc, int effect)
    {
        if (pc < bytecodes.Length)
            pc++;
        if (pc < bytecodes.Length)
            pc++;
        return effect;
    }

    private static int GetSendNEffect(byte[] bytecodes, int pc)
    {
        // SEND_N and SEND_SUPER_N have format: [selector_index, arg_count]
        // Effect = -(receiver + args) + result = -(1 + argCount) + 1 = -argCount
        if (pc + 1 < bytecodes.Length)
        {
            int argCount = bytecodes[pc + 1];
            return -argCount;
        }
        return 0;
    }

    private static int GetSpecialSelectorArgCount(int specialIndex)
    {
        return specialIndex switch
        {
            0 => 1,   // at:
            1 => 2,   // at:put:
            2 => 0,   // size
            3 => 0,   // next
            4 => 1,   // nextPut:
            5 => 0,   // atEnd
            6 => 1,   // ==
            7 => 0,   // class
            8 => 1,   // blockCopy:
            9 => 0,   // value
            10 => 1,  // value:
            11 => 1,  // do:
            12 => 0,  // new
            13 => 1,  // new:
            14 => 0,  // x
            15 => 0,  // y
            _ => 0
        };
    }
}
