using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Disassembles Smalltalk-80 bytecodes into symbolic format following the Blue Book specification.
/// </summary>
public static class BytecodeDisassembler
{
    /// <summary>
    /// Disassembles a bytecode array into human-readable symbolic format.
    /// </summary>
    public static string Disassemble(byte[] bytecodes, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, string[] optimizedSelectors)
    {
        var output = new System.Text.StringBuilder();
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            int startPc = pc;
            byte b = bytecodes[pc];
            output.Append($"  {pc,3}: ");

            string instruction = DisassembleInstruction(b, bytecodes, ref pc, literalOOPs, getLiteralDisplay, optimizedSelectors, out int bytesConsumed);

            // Append hex bytes for this instruction
            var hexBytes = new System.Text.StringBuilder();
            hexBytes.Append("; 0x");
            hexBytes.Append(bytecodes[startPc].ToString("X2"));
            for (int i = 1; i < bytesConsumed; i++)
            {
                hexBytes.Append(" 0x");
                hexBytes.Append(bytecodes[startPc + i].ToString("X2"));
            }

            output.AppendLine($"{instruction,-45} {hexBytes}");
        }

        return output.ToString();
    }

    // Overload for backward compatibility (will be removed later)
    public static string Disassemble(byte[] bytecodes, int[] literalIds, Func<int, string> getLiteralDisplay, string[] optimizedSelectors)
    {
        // Convert int[] to SmalltalkOOP[]
        var literalOOPs = new SmalltalkOOP[literalIds.Length];
        for (int i = 0; i < literalIds.Length; i++)
        {
            literalOOPs[i] = new SmalltalkOOP(literalIds[i]);
        }

        return Disassemble(bytecodes, literalOOPs, oop => getLiteralDisplay(oop.RawValue), optimizedSelectors);
    }

    private static string DisassembleInstruction(byte b, byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, string[] optimizedSelectors, out int bytesConsumed)
    {
        bytesConsumed = 1; // Default: single byte instruction
        string result;

        // Push receiver instance variable (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(b))
        {
            result = $"pushReceiver {BytecodeUtil.PushIvarIndex(b)}";
        }
        // Push temporary variable (0x10-0x1F)
        else if (BytecodeUtil.IsPushTemp(b))
        {
            result = $"pushTemporary {BytecodeUtil.PushTempIndex(b)}";
        }
        // Push literal constant (0x20-0x3F)
        // Note: Bytecode literal indices skip the header at literalOOPs[0]
        else if (BytecodeUtil.IsPushLiteralConst(b))
        {
            int litIndex = BytecodeUtil.PushLiteralConstIndex(b) + 1; // +1 to skip header at index 0
            string litValue = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : "???";
            result = $"pushLiteral {litIndex - 1} ({litValue})";
        }
        // Push literal variable (0x40-0x5F)
        else if (BytecodeUtil.IsPushLiteralVar(b))
        {
            int litIndex = BytecodeUtil.PushLiteralVarIndex(b) + 1; // +1 to skip header at index 0
            string litValue = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : "???";
            result = $"pushLiteralVariable {litIndex - 1} ({litValue})";
        }
        // Pop and store receiver variable (0x60-0x67)
        else if (BytecodeUtil.IsPopStoreIvar(b))
        {
            result = $"popStoreReceiver {BytecodeUtil.PopStoreIvarIndex(b)}";
        }
        // Pop and store temporary variable (0x68-0x6F)
        else if (BytecodeUtil.IsPopStoreTemp(b))
        {
            result = $"popStoreTemporary {BytecodeUtil.PopStoreTempIndex(b)}";
        }
        // Push specials (0x70-0x77)
        else if (BytecodeUtil.IsPushSpecial(b))
        {
            result = ((Bytecode)b) switch
            {
                Bytecode.PUSH_SELF => "pushSelf",
                Bytecode.PUSH_TRUE => "pushTrue",
                Bytecode.PUSH_FALSE => "pushFalse",
                Bytecode.PUSH_NIL => "pushNil",
                Bytecode.PUSH_MINUS_ONE => "pushMinusOne",
                Bytecode.PUSH_ZERO => "pushZero",
                Bytecode.PUSH_ONE => "pushOne",
                Bytecode.PUSH_TWO => "pushTwo",
                _ => $"push??? {b}"
            };
        }
        // Return variants (0x78-0x7F)
        else if (BytecodeUtil.IsReturn(b))
        {
            result = ((Bytecode)b) switch
            {
                Bytecode.RETURN_TOP => "returnTop",
                Bytecode.RETURN_SELF => "returnSelf",
                Bytecode.RETURN_FALSE => "returnFalse",
                Bytecode.RETURN_TRUE => "returnTrue",
                Bytecode.RETURN_NIL => "returnNil",
                Bytecode.RETURN_SELF_POP => "blockReturnSelf",
                Bytecode.RETURN_TOP_POP => "blockReturnTop",
                Bytecode.RESERVED_RETURN => "nop",
                _ => $"return??? {b}"
            };
        }
        // Extended bytecodes (0x80-0xFF)
        else
        {
            result = DisassembleExtended(b, bytecodes, ref pc, literalOOPs, getLiteralDisplay, optimizedSelectors, ref bytesConsumed);
        }

        pc++;
        return result;
    }

    private static string DisassembleExtended(byte b, byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, string[] optimizedSelectors, ref int bytesConsumed)
    {
        // Extended prefix (0x80)
        if (BytecodeUtil.IsExtended(b))
        {
            return DisassembleExtendedPrefix(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed);
        }
        // Send arithmetic (0xB0-0xBF)
        else if (BytecodeUtil.IsSendArithmetic(b))
        {
            int opIndex = BytecodeUtil.SendArithmeticIndex(b);
            string[] arithmeticOps = [
                "+", "-", "<", ">", "<=", ">=", "=", "~=",
                "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
            ];
            string selector = opIndex < arithmeticOps.Length ? arithmeticOps[opIndex] : $"arith[{opIndex}]";
            return $"send #{selector}";
        }
        // Send special selector (0xC0-0xCF)
        else if (BytecodeUtil.IsSendSpecial(b))
        {
            int specialIndex = BytecodeUtil.SendSpecialIndex(b);
            string[] specialSelectors = [
                "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "==", "class",
                "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y"
            ];
            string selector = specialIndex < specialSelectors.Length ? specialSelectors[specialIndex] : $"special[{specialIndex}]";
            return $"send #{selector}";
        }
        // Send literal selector with 0 args (0xD0-0xDF)
        else if (BytecodeUtil.IsSend0(b))
        {
            int litIndex = BytecodeUtil.Send0Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        // Send literal selector with 1 arg (0xE0-0xEF)
        else if (BytecodeUtil.IsSend1(b))
        {
            int litIndex = BytecodeUtil.Send1Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        // Send literal selector with 2 args (0xF0-0xFF)
        else if (BytecodeUtil.IsSend2(b))
        {
            int litIndex = BytecodeUtil.Send2Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        else
        {
            return $"unknown extended bytecode 0x{b:X2}";
        }
    }

    private static string DisassembleExtendedPrefix(byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, ref int bytesConsumed)
    {
        // Extended prefix (0x80) followed by sub-operation byte
        if (pc + 1 >= bytecodes.Length)
        {
            return "extended (incomplete)";
        }

        byte subOp = bytecodes[pc + 1];
        pc++; // Consume sub-operation byte
        bytesConsumed = 2;

        return ((ExtendedSubOp)subOp) switch
        {
            ExtendedSubOp.PUSH_IVAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("pushReceiver", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.STORE_IVAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("storeReceiver", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.POP_STORE_IVAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("popStoreReceiver", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.PUSH_TEMP when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("pushTemporary", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.STORE_TEMP when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("storeTemporary", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.POP_STORE_TEMP when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("popStoreTemporary", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.PUSH_LITERAL when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("pushLiteral", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.PUSH_LITERAL_VAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("pushLiteralVariable", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.STORE_LITERAL_VAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("storeLiteralVariable", bytecodes, ref pc, ref bytesConsumed),
            ExtendedSubOp.POP_STORE_LITERAL_VAR when pc + 1 < bytecodes.Length =>
                GetExtendedOperandString("popStoreLiteralVariable", bytecodes, ref pc, ref bytesConsumed),

            // Send operations with single operand (selector index)
            ExtendedSubOp.SEND_0 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("send", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 0),
            ExtendedSubOp.SEND_1 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("send", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 1),
            ExtendedSubOp.SEND_2 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("send", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 2),
            ExtendedSubOp.SEND_SUPER_0 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("sendSuper", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 0),
            ExtendedSubOp.SEND_SUPER_1 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("sendSuper", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 1),
            ExtendedSubOp.SEND_SUPER_2 when pc + 1 < bytecodes.Length =>
                GetExtendedSendString("sendSuper", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay, 2),

            // Send operations with two operands (selector index + arg count)
            ExtendedSubOp.SEND_N when pc + 2 < bytecodes.Length =>
                GetExtendedSendNString("send", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay),
            ExtendedSubOp.SEND_SUPER_N when pc + 2 < bytecodes.Length =>
                GetExtendedSendNString("sendSuper", bytecodes, ref pc, ref bytesConsumed, literalOOPs, getLiteralDisplay),

            ExtendedSubOp.POP => "pop",
            ExtendedSubOp.DUP => "dup",
            _ => $"extended subOp 0x{subOp:X2}"
        };
    }

    private static string GetExtendedOperandString(string mnemonic, byte[] bytecodes, ref int pc, ref int bytesConsumed)
    {
        byte operand = bytecodes[pc + 1];
        pc++;
        bytesConsumed = 3; // 0x80 + subOp + operand
        return $"{mnemonic} {operand}";
    }

    private static string GetExtendedSendString(string mnemonic, byte[] bytecodes, ref int pc, ref int bytesConsumed, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, int argCount)
    {
        byte selectorIndex = bytecodes[pc + 1];
        pc++;
        bytesConsumed = 3; // 0x80 + subOp + selectorIndex

        int litIndex = selectorIndex + 1; // +1 to skip header at literalOOPs[0]
        string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{selectorIndex}]";
        return $"{mnemonic} #{selector} ({argCount} args)";
    }

    private static string GetExtendedSendNString(string mnemonic, byte[] bytecodes, ref int pc, ref int bytesConsumed, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay)
    {
        byte selectorIndex = bytecodes[pc + 1];
        byte argCount = bytecodes[pc + 2];
        pc += 2;
        bytesConsumed = 4; // 0x80 + subOp + selectorIndex + argCount

        int litIndex = selectorIndex + 1; // +1 to skip header at literalOOPs[0]
        string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{selectorIndex}]";
        return $"{mnemonic} #{selector} ({argCount} args)";
    }

    /// <summary>
    /// Gets a short symbolic name for a single bytecode (for debug logging).
    /// Returns just the opcode name without operands or hex formatting.
    /// </summary>
    public static string GetOpcodeName(byte bytecode)
    {
        // Push receiver instance variable (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
            return "PUSH_IVAR";

        // Push temporary variable (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
            return "PUSH_TEMP";

        // Push literal constant (0x20-0x3F)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
            return "PUSH_LITERAL";

        // Push literal variable (0x40-0x5F)
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
            return "PUSH_LITERAL_VAR";

        // Pop and store receiver variable (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
            return "POP_STORE_IVAR";

        // Pop and store temporary variable (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
            return "POP_STORE_TEMP";

        // Push specials (0x70-0x77)
        if (BytecodeUtil.IsPushSpecial(bytecode))
        {
            return ((Bytecode)bytecode) switch
            {
                Bytecode.PUSH_SELF => "PUSH_SELF",
                Bytecode.PUSH_TRUE => "PUSH_TRUE",
                Bytecode.PUSH_FALSE => "PUSH_FALSE",
                Bytecode.PUSH_NIL => "PUSH_NIL",
                Bytecode.PUSH_MINUS_ONE => "PUSH_MINUS_ONE",
                Bytecode.PUSH_ZERO => "PUSH_ZERO",
                Bytecode.PUSH_ONE => "PUSH_ONE",
                Bytecode.PUSH_TWO => "PUSH_TWO",
                _ => "PUSH_SPECIAL"
            };
        }

        // Return variants (0x78-0x7F)
        if (BytecodeUtil.IsReturn(bytecode))
        {
            return ((Bytecode)bytecode) switch
            {
                Bytecode.RETURN_TOP => "RETURN_TOP",
                Bytecode.RETURN_SELF => "RETURN_SELF",
                Bytecode.RETURN_FALSE => "RETURN_FALSE",
                Bytecode.RETURN_TRUE => "RETURN_TRUE",
                Bytecode.RETURN_NIL => "RETURN_NIL",
                Bytecode.RETURN_SELF_POP => "BLOCK_RETURN_SELF",
                Bytecode.RETURN_TOP_POP => "BLOCK_RETURN_TOP",
                Bytecode.RESERVED_RETURN => "NOP",
                _ => "RETURN"
            };
        }

        // Extended prefix (0x80)
        if (BytecodeUtil.IsExtended(bytecode))
            return "EXTENDED";

        // Send arithmetic (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
            return "SEND_ARITH";

        // Send special selector (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
            return "SEND_SPECIAL";

        // Send literal selector with 0 args (0xD0-0xDF)
        if (BytecodeUtil.IsSend0(bytecode))
            return "SEND_0";

        // Send literal selector with 1 arg (0xE0-0xEF)
        if (BytecodeUtil.IsSend1(bytecode))
            return "SEND_1";

        // Send literal selector with 2 args (0xF0-0xFF)
        if (BytecodeUtil.IsSend2(bytecode))
            return "SEND_2";

        return "UNKNOWN";
    }
}
