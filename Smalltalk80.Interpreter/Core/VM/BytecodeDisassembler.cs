using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

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
                Bytecode.RETURN_TOP_FROM_MESSAGE => "returnTop",     // 0x7C - also NLR in blocks
                Bytecode.RETURN_SELF => "returnSelf",
                Bytecode.RETURN_FALSE => "returnFalse",
                Bytecode.RETURN_TRUE => "returnTrue",
                Bytecode.RETURN_NIL => "returnNil",
                Bytecode.RETURN_TOP_FROM_BLOCK => "blockReturnValue", // 0x7D - local block return
                Bytecode.RESERVED_126 => "reserved126",              // 0x7E - unused
                Bytecode.RESERVED_127 => "reserved127",              // 0x7F - unused
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
        // Blue Book extended bytecodes (0x80-0x88)
        switch (b)
        {
            // Extended push (0x80) - type:index format
            case 0x80:
                return DisassembleExtendedPush(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed);

            // Extended store no pop (0x81) - type:index format
            case 0x81:
                return DisassembleExtendedStore(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "store");

            // Extended pop and store (0x82) - type:index format
            case 0x82:
                return DisassembleExtendedStore(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "popStore");

            // Single extended send (0x83) - nnnlllll format
            case 0x83:
                return DisassembleSingleExtendedSend(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "send");

            // Double extended send (0x84)
            case 0x84:
                return DisassembleDoubleExtendedSend(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "send");

            // Single extended super send (0x85) - nnnlllll format
            case 0x85:
                return DisassembleSingleExtendedSend(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "sendSuper");

            // Double extended super send (0x86)
            case 0x86:
                return DisassembleDoubleExtendedSend(bytecodes, ref pc, literalOOPs, getLiteralDisplay, ref bytesConsumed, "sendSuper");

            // Pop (0x87)
            case 0x87:
                return "pop";

            // Duplicate (0x88)
            case 0x88:
                return "dup";

            // Push active context (0x89)
            case 0x89:
                return "pushActiveContext";
        }

        // Short jump forward 1-8 (0x90-0x97)
        if (BytecodeUtil.IsShortJump(b))
        {
            int offset = (b & 0x07) + 1; // 1-8 bytes forward
            return $"jump +{offset}";
        }

        // Short jump on false 1-8 (0x98-0x9F)
        if (BytecodeUtil.IsShortJumpFalse(b))
        {
            int offset = (b & 0x07) + 1; // 1-8 bytes forward
            return $"jumpFalse +{offset}";
        }

        // Long jump (0xA0-0xA7)
        if (BytecodeUtil.IsLongJump(b))
        {
            if (pc + 1 >= bytecodes.Length) return "longJump (incomplete)";
            byte nextByte = bytecodes[pc + 1];
            pc++;
            bytesConsumed = 2;
            // Offset formula: ((b & 7) - 4) * 256 + nextByte
            int offset = ((b & 0x07) - 4) * 256 + nextByte;
            string direction = offset >= 0 ? $"+{offset}" : offset.ToString();
            return $"jump {direction}";
        }

        // Long jump on true (0xA8-0xAB)
        if (BytecodeUtil.IsLongJumpTrue(b))
        {
            if (pc + 1 >= bytecodes.Length) return "longJumpTrue (incomplete)";
            byte nextByte = bytecodes[pc + 1];
            pc++;
            bytesConsumed = 2;
            int offset = (b & 0x03) * 256 + nextByte;
            return $"jumpTrue +{offset}";
        }

        // Long jump on false (0xAC-0xAF)
        if (BytecodeUtil.IsLongJumpFalse(b))
        {
            if (pc + 1 >= bytecodes.Length) return "longJumpFalse (incomplete)";
            byte nextByte = bytecodes[pc + 1];
            pc++;
            bytesConsumed = 2;
            int offset = (b & 0x03) * 256 + nextByte;
            return $"jumpFalse +{offset}";
        }

        // Send arithmetic (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(b))
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
        if (BytecodeUtil.IsSendSpecial(b))
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
        if (BytecodeUtil.IsSend0(b))
        {
            int litIndex = BytecodeUtil.Send0Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        if (BytecodeUtil.IsSend1(b))
        {
            int litIndex = BytecodeUtil.Send1Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        if (BytecodeUtil.IsSend2(b))
        {
            int litIndex = BytecodeUtil.Send2Index(b) + 1; // +1 to skip header at literalOOPs[0]
            string selector = litIndex < literalOOPs.Length ? getLiteralDisplay(literalOOPs[litIndex]) : $"lit[{litIndex - 1}]";
            return $"send {selector}";
        }

        return $"unknown extended bytecode 0x{b:X2}";
    }

    /// <summary>
    /// Disassemble Blue Book extended push (0x80) with type:index encoding.
    /// </summary>
    private static string DisassembleExtendedPush(byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, ref int bytesConsumed)
    {
        if (pc + 1 >= bytecodes.Length) return "extPush (incomplete)";

        byte typeIndex = bytecodes[pc + 1];
        pc++;
        bytesConsumed = 2;

        int type = (typeIndex >> 6) & 0x03;  // tt bits
        int index = typeIndex & 0x3F;         // iiiiii bits

        return type switch
        {
            0 => $"pushReceiver {index}",     // ReceiverVariable
            1 => $"pushTemporary {index}",    // TemporaryVariable
            2 => GetLiteralDisplay("pushLiteral", index, literalOOPs, getLiteralDisplay),
            3 => GetLiteralDisplay("pushLiteralVariable", index, literalOOPs, getLiteralDisplay),
            _ => $"extPush type={type} index={index}"
        };
    }

    /// <summary>
    /// Disassemble Blue Book extended store (0x81/0x82) with type:index encoding.
    /// </summary>
    private static string DisassembleExtendedStore(byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, ref int bytesConsumed, string prefix)
    {
        if (pc + 1 >= bytecodes.Length) return $"{prefix} (incomplete)";

        byte typeIndex = bytecodes[pc + 1];
        pc++;
        bytesConsumed = 2;

        int type = (typeIndex >> 6) & 0x03;  // tt bits
        int index = typeIndex & 0x3F;         // iiiiii bits

        return type switch
        {
            0 => $"{prefix}Receiver {index}",
            1 => $"{prefix}Temporary {index}",
            2 => GetLiteralDisplay($"{prefix}Literal", index, literalOOPs, getLiteralDisplay),
            3 => GetLiteralDisplay($"{prefix}LiteralVariable", index, literalOOPs, getLiteralDisplay),
            _ => $"{prefix} type={type} index={index}"
        };
    }

    /// <summary>
    /// Disassemble Blue Book single extended send (0x83/0x85) with nnnlllll encoding.
    /// </summary>
    private static string DisassembleSingleExtendedSend(byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, ref int bytesConsumed, string mnemonic)
    {
        if (pc + 1 >= bytecodes.Length) return $"{mnemonic} (incomplete)";

        byte descriptor = bytecodes[pc + 1];
        pc++;
        bytesConsumed = 2;

        int argCount = (descriptor >> 5) & 0x07;  // nnn bits (0-7)
        int litIndex = descriptor & 0x1F;          // lllll bits (0-31)

        int adjustedIndex = litIndex + 1; // +1 to skip header
        string selector = adjustedIndex < literalOOPs.Length
            ? getLiteralDisplay(literalOOPs[adjustedIndex])
            : $"lit[{litIndex}]";

        return $"{mnemonic} #{selector} ({argCount} args)";
    }

    /// <summary>
    /// Disassemble Blue Book double extended send (0x84/0x86).
    /// Format: opcode argCount litIndex
    /// </summary>
    private static string DisassembleDoubleExtendedSend(byte[] bytecodes, ref int pc, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay, ref int bytesConsumed, string mnemonic)
    {
        if (pc + 2 >= bytecodes.Length) return $"{mnemonic} (incomplete)";

        byte argCount = bytecodes[pc + 1];
        byte litIndex = bytecodes[pc + 2];
        pc += 2;
        bytesConsumed = 3;

        int adjustedIndex = litIndex + 1; // +1 to skip header
        string selector = adjustedIndex < literalOOPs.Length
            ? getLiteralDisplay(literalOOPs[adjustedIndex])
            : $"lit[{litIndex}]";

        return $"{mnemonic} #{selector} ({argCount} args)";
    }

    /// <summary>
    /// Helper to format literal display with index.
    /// </summary>
    private static string GetLiteralDisplay(string mnemonic, int index, SmalltalkOOP[] literalOOPs, Func<SmalltalkOOP, string> getLiteralDisplay)
    {
        int adjustedIndex = index + 1; // +1 to skip header
        string litValue = adjustedIndex < literalOOPs.Length
            ? getLiteralDisplay(literalOOPs[adjustedIndex])
            : "???";
        return $"{mnemonic} {index} ({litValue})";
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
                Bytecode.RETURN_TOP_FROM_MESSAGE => "RETURN_TOP",
                Bytecode.RETURN_SELF => "RETURN_SELF",
                Bytecode.RETURN_FALSE => "RETURN_FALSE",
                Bytecode.RETURN_TRUE => "RETURN_TRUE",
                Bytecode.RETURN_NIL => "RETURN_NIL",
                Bytecode.RETURN_TOP_FROM_BLOCK => "BLOCK_RETURN_VALUE",
                Bytecode.RESERVED_126 => "RESERVED_126",
                Bytecode.RESERVED_127 => "RESERVED_127",
                _ => "RETURN"
            };
        }

        // Blue Book extended bytecodes (0x80-0x88)
        switch (bytecode)
        {
            case 0x80: return "EXT_PUSH";
            case 0x81: return "EXT_STORE";
            case 0x82: return "EXT_POP_STORE";
            case 0x83: return "SINGLE_SEND";
            case 0x84: return "DOUBLE_SEND";
            case 0x85: return "SINGLE_SUPER";
            case 0x86: return "DOUBLE_SUPER";
            case 0x87: return "POP";
            case 0x88: return "DUP";
            case 0x89: return "PUSH_CONTEXT";
        }

        // Short jump forward (0x90-0x97)
        if (BytecodeUtil.IsShortJump(bytecode))
            return "SHORT_JUMP";

        // Short jump on false (0x98-0x9F)
        if (BytecodeUtil.IsShortJumpFalse(bytecode))
            return "SHORT_JUMP_FALSE";

        // Long jump (0xA0-0xA7)
        if (BytecodeUtil.IsLongJump(bytecode))
            return "LONG_JUMP";

        // Long jump on true (0xA8-0xAB)
        if (BytecodeUtil.IsLongJumpTrue(bytecode))
            return "LONG_JUMP_TRUE";

        // Long jump on false (0xAC-0xAF)
        if (BytecodeUtil.IsLongJumpFalse(bytecode))
            return "LONG_JUMP_FALSE";

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
