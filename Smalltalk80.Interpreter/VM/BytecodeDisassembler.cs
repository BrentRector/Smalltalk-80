using Smalltalk80.Interpreter.Core.Bytecodes;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Disassembles Smalltalk-80 bytecodes into symbolic format following the Blue Book specification.
/// </summary>
public static class BytecodeDisassembler
{
    /// <summary>
    /// Disassembles a bytecode array into human-readable symbolic format.
    /// </summary>
    public static string Disassemble(byte[] bytecodes, int[] literalIds, Func<int, string> getLiteralDisplay, string[] optimizedSelectors)
    {
        var output = new System.Text.StringBuilder();
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            int startPc = pc;
            byte b = bytecodes[pc];
            output.Append($"  {pc,3}: ");

            string instruction = DisassembleInstruction(b, bytecodes, ref pc, literalIds, getLiteralDisplay, optimizedSelectors, out int bytesConsumed);

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

    private static string DisassembleInstruction(byte b, byte[] bytecodes, ref int pc, int[] literalIds, Func<int, string> getLiteralDisplay, string[] optimizedSelectors, out int bytesConsumed)
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
        // Note: Bytecode literal indices skip the header at literalIds[0]
        else if (BytecodeUtil.IsPushLiteralConst(b))
        {
            int litIndex = BytecodeUtil.PushLiteralConstIndex(b) + 1; // +1 to skip header at index 0
            string litValue = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : "???";
            result = $"pushLiteral {litIndex - 1} ({litValue})";
        }
        // Push literal variable (0x40-0x5F)
        else if (BytecodeUtil.IsPushLiteralVar(b))
        {
            int litIndex = BytecodeUtil.PushLiteralVarIndex(b) + 1; // +1 to skip header at index 0
            string litValue = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : "???";
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
            result = DisassembleExtended(b, bytecodes, ref pc, literalIds, getLiteralDisplay, optimizedSelectors, ref bytesConsumed);
        }

        pc++;
        return result;
    }

    private static string DisassembleExtended(byte b, byte[] bytecodes, ref int pc, int[] literalIds, Func<int, string> getLiteralDisplay, string[] optimizedSelectors, ref int bytesConsumed)
    {
        // Extended prefix (0x80)
        if (BytecodeUtil.IsExtended(b))
        {
            return DisassembleExtendedPrefix(bytecodes, ref pc, literalIds, getLiteralDisplay, ref bytesConsumed);
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
            int litIndex = BytecodeUtil.Send0Index(b) + 1; // +1 to skip header at literalIds[0]
            string selector = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        // Send literal selector with 1 arg (0xE0-0xEF)
        else if (BytecodeUtil.IsSend1(b))
        {
            int litIndex = BytecodeUtil.Send1Index(b) + 1; // +1 to skip header at literalIds[0]
            string selector = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        // Send literal selector with 2 args (0xF0-0xFF)
        else if (BytecodeUtil.IsSend2(b))
        {
            int litIndex = BytecodeUtil.Send2Index(b) + 1; // +1 to skip header at literalIds[0]
            string selector = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }
        else
        {
            return $"unknown extended bytecode 0x{b:X2}";
        }
    }

    private static string DisassembleExtendedPrefix(byte[] bytecodes, ref int pc, int[] literalIds, Func<int, string> getLiteralDisplay, ref int bytesConsumed)
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
}
