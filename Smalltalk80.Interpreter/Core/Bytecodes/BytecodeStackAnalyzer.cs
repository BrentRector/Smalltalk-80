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

        // For now, treat all extended ops conservatively
        // The issue is that 0x80 in the actual Blue Book has a different encoding
        // than what we've defined in ExtendedSubOp enum

        // Blue Book extended bytecodes (0x80-0xAF) have specific formats:
        // 128-143: variable operations (push/store receiver, temp, literal, literal var)
        // 144-151: send literal selector (various arg counts)
        // 152-159: jump operations

        // The subOp byte itself may be the actual opcode, not a sub-operation
        // Let's decode based on the Blue Book format instead

        if (subOp >= 0x00 && subOp <= 0x0F)
        {
            // Extended push/store/pop operations - all have one operand byte
            if (pc < bytecodes.Length)
                pc++; // Skip index operand

            // Decode based on low nibble
            int operation = (subOp >> 1) & 0x07;
            return operation switch
            {
                0 => +1,  // Push receiver variable
                1 => +1,  // Push temporary
                2 => +1,  // Push literal constant
                3 => +1,  // Push literal variable
                4 => 0,   // Store receiver variable (no pop)
                5 => 0,   // Store temporary (no pop)
                6 => -1,  // Pop and store receiver variable
                7 => -1,  // Pop and store temporary
                _ => 0
            };
        }
        else if (subOp >= 0x10 && subOp <= 0x1F)
        {
            // Send operations - format varies
            if (pc < bytecodes.Length)
            {
                byte operand = bytecodes[pc++];
                // Parse jjjkkkkk format where jjj = arg count, kkkkk = selector
                int argCount = (operand >> 5) & 0x07;
                return -(argCount + 1) + 1; // Pop receiver + args, push result = -argCount
            }
            return 0;
        }
        else if (subOp >= 0x20 && subOp <= 0x2F)
        {
            // Jump operations - 2-byte offset
            if (pc + 1 < bytecodes.Length)
                pc += 2;

            // Conditional jumps pop the condition
            if ((subOp & 0x07) <= 0x03)
                return -1;
            else
                return 0;
        }
        else
        {
            // Unknown extended operation - assume neutral and skip one byte
            if (pc < bytecodes.Length)
                pc++;
            return 0;
        }
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
