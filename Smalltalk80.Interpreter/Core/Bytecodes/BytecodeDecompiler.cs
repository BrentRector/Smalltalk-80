using System.Text;
using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.Bytecodes;

/// <summary>
/// Decompiles Smalltalk-80 bytecode back into high-level Smalltalk source code.
/// Used for validation - decompiled output should match original source.
/// </summary>
public class BytecodeDecompiler
{
    private readonly Dictionary<int, string> _symbolNameLookup;
    private readonly string[] _symbols;
    private readonly int[] _literalIds;
    private readonly string[] _tempNames;
    private readonly string[] _instVarNames;

    /// <summary>
    /// Initializes a new instance of the BytecodeDecompiler class.
    /// </summary>
    /// <param name="symbolNameLookup">Dictionary mapping OOP values to symbol names.</param>
    /// <param name="symbols">Array of symbol names for the method.</param>
    /// <param name="literalIds">Array of literal OOP values for the method.</param>
    /// <param name="tempNames">Array of temporary variable names.</param>
    /// <param name="instVarNames">Array of instance variable names.</param>
    public BytecodeDecompiler(
        Dictionary<int, string> symbolNameLookup,
        string[] symbols,
        int[] literalIds,
        string[] tempNames,
        string[] instVarNames)
    {
        _symbolNameLookup = symbolNameLookup;
        _symbols = symbols;
        _literalIds = literalIds;
        _tempNames = tempNames;
        _instVarNames = instVarNames;
    }

    /// <summary>
    /// Decompiles bytecode into Smalltalk source code.
    /// Returns the decompiled method body (without selector and temps declaration).
    /// </summary>
    public string Decompile(byte[] bytecodes)
    {
        var stack = new Stack<string>();
        var statements = new List<string>();
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            byte b = bytecodes[pc++];

            // Try each bytecode category in order
            if (TryDecompilePushBytecodes(b, stack)) continue;
            if (TryDecompilePopStoreBytecodes(b, stack, statements)) continue;
            if (TryDecompileSpecialConstants(b, stack)) continue;
            if (TryDecompileReturns(b, stack, statements)) continue;
            if (TryDecompileSendArithmetic(b, stack)) continue;
            if (TryDecompileSendSpecial(b, stack)) continue;
            if (TryDecompileSendLiteralSelector(b, stack)) continue;
            if (TryDecompileBlockAndMiscBytecodes(b)) continue;
            if (TryDecompileExtendedBytecodes(b, bytecodes, ref pc, stack, statements)) continue;
            if (TryDecompilePushActiveContext(b, stack)) continue;
            if (TryDecompileJumpBytecodes(b, bytecodes, ref pc, stack, statements)) continue;

            // If we get here, it's an unhandled bytecode
            statements.Add($"<< bytecode 0x{b:X2} not yet decompiled >>");
        }

        // If there's anything left on the stack at the end, it's an implicit return
        if (stack.Count > 0)
        {
            statements.Add($"^ {stack.Pop()}");
        }

        return string.Join(".\n\t", statements);
    }

    #region Push Bytecodes (0x00-0x5F)

    /// <summary>
    /// Handles push bytecodes: instance vars (0x00-0x0F), temps (0x10-0x1F),
    /// literal constants (0x20-0x3F), and literal variables (0x40-0x5F).
    /// </summary>
    private bool TryDecompilePushBytecodes(byte b, Stack<string> stack)
    {
        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(b))
        {
            int index = BytecodeUtil.PushIvarIndex(b);
            string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
            stack.Push(varName);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(b))
        {
            int index = BytecodeUtil.PushTempIndex(b);
            string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
            stack.Push(varName);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        if (BytecodeUtil.IsPushLiteralConst(b))
        {
            int litIndex = BytecodeUtil.PushLiteralConstIndex(b);
            if (litIndex + 1 >= _literalIds.Length)
            {
                stack.Push($"<lit[{litIndex}]?>");
                return true;
            }
            int litOop = _literalIds[litIndex + 1]; // +1 to skip header
            string litValue = FormatLiteral(litOop);
            stack.Push(litValue);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        if (BytecodeUtil.IsPushLiteralVar(b))
        {
            int litIndex = BytecodeUtil.PushLiteralVarIndex(b);
            if (litIndex + 1 >= _literalIds.Length)
            {
                stack.Push($"<litVar[{litIndex}]?>");
                return true;
            }
            int litOop = _literalIds[litIndex + 1]; // +1 to skip header
            string varName = FormatLiteral(litOop);
            stack.Push(varName);
            return true;
        }

        return false;
    }

    #endregion

    #region Pop and Store Bytecodes (0x60-0x6F)

    /// <summary>
    /// Handles pop and store bytecodes: instance vars (0x60-0x67), temps (0x68-0x6F).
    /// </summary>
    private bool TryDecompilePopStoreBytecodes(byte b, Stack<string> stack, List<string> statements)
    {
        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(b))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(b);
            string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
            if (stack.Count < 1)
                throw new InvalidOperationException(DecompilerStackError(-1, b, 1, stack.Count));
            string value = stack.Pop();
            statements.Add($"{varName} := {value}");
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(b))
        {
            int index = BytecodeUtil.PopStoreTempIndex(b);
            string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
            if (stack.Count < 1)
                throw new InvalidOperationException(DecompilerStackError(-1, b, 1, stack.Count));
            string value = stack.Pop();
            statements.Add($"{varName} := {value}");
            return true;
        }

        return false;
    }

    #endregion

    #region Special Constants (0x70-0x77)

    /// <summary>
    /// Handles special constant push bytecodes: self, true, false, nil, -1, 0, 1, 2.
    /// </summary>
    private bool TryDecompileSpecialConstants(byte b, Stack<string> stack)
    {
        switch ((Bytecode)b)
        {
            case Bytecode.PUSH_SELF:
                stack.Push("self");
                return true;
            case Bytecode.PUSH_TRUE:
                stack.Push("true");
                return true;
            case Bytecode.PUSH_FALSE:
                stack.Push("false");
                return true;
            case Bytecode.PUSH_NIL:
                stack.Push("nil");
                return true;
            case Bytecode.PUSH_MINUS_ONE:
                stack.Push("-1");
                return true;
            case Bytecode.PUSH_ZERO:
                stack.Push("0");
                return true;
            case Bytecode.PUSH_ONE:
                stack.Push("1");
                return true;
            case Bytecode.PUSH_TWO:
                stack.Push("2");
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Return Bytecodes (0x78-0x7C)

    /// <summary>
    /// Handles return bytecodes: return self, true, false, nil, and top of stack.
    /// </summary>
    private bool TryDecompileReturns(byte b, Stack<string> stack, List<string> statements)
    {
        switch ((Bytecode)b)
        {
            case Bytecode.RETURN_SELF:
                statements.Add("^ self");
                return true;
            case Bytecode.RETURN_TRUE:
                statements.Add("^ true");
                return true;
            case Bytecode.RETURN_FALSE:
                statements.Add("^ false");
                return true;
            case Bytecode.RETURN_NIL:
                statements.Add("^ nil");
                return true;
            case Bytecode.RETURN_TOP:
                if (stack.Count < 1)
                    throw new InvalidOperationException(DecompilerStackError(-1, b, 1, stack.Count));
                string returnValue = stack.Pop();
                statements.Add($"^ {returnValue}");
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Send Arithmetic (0xB0-0xBF)

    /// <summary>
    /// Handles arithmetic send bytecodes: +, -, *, /, comparisons, etc.
    /// </summary>
    private bool TryDecompileSendArithmetic(byte b, Stack<string> stack)
    {
        if (!BytecodeUtil.IsSendArithmetic(b))
            return false;

        string selector = (b & 0x0F) switch
        {
            0x00 => "+",
            0x01 => "-",
            0x02 => "<",
            0x03 => ">",
            0x04 => "<=",
            0x05 => ">=",
            0x06 => "=",
            0x07 => "~=",
            0x08 => "*",
            0x09 => "/",
            0x0A => "\\",
            0x0B => "@",
            0x0C => "bitShift:",
            0x0D => "//",
            0x0E => "bitAnd:",
            0x0F => "bitOr:",
            _ => "???"
        };

        if (stack.Count < 2)
            throw new InvalidOperationException(DecompilerStackError(-1, b, 2, stack.Count));
        string arg = stack.Pop();
        string receiver = stack.Pop();

        // Binary selectors can be written infix
        if (selector.Length <= 2 && !selector.Contains(':'))
        {
            stack.Push($"{receiver} {selector} {arg}");
        }
        else
        {
            stack.Push($"{receiver} {selector} {arg}");
        }
        return true;
    }

    #endregion

    #region Send Special (0xC0-0xCF)

    /// <summary>
    /// Handles special send bytecodes: at:, at:put:, size, value, etc.
    /// </summary>
    private bool TryDecompileSendSpecial(byte b, Stack<string> stack)
    {
        if (!BytecodeUtil.IsSendSpecial(b))
            return false;

        string[] specialSelectors =
        [
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        ];

        int selectorIndex = b & 0x0F;
        string selector = specialSelectors[selectorIndex];
        int argCount = selector.Count(c => c == ':');
        int totalPops = argCount + 1; // args + receiver

        if (stack.Count < totalPops)
            throw new InvalidOperationException(DecompilerStackError(-1, b, totalPops, stack.Count));

        var args = new List<string>();
        for (int i = 0; i < argCount; i++)
        {
            args.Insert(0, stack.Pop());
        }
        string receiver = stack.Pop();

        string send = BuildMessageSend(receiver, selector, args.ToArray());
        stack.Push(send);
        return true;
    }

    #endregion

    #region Send Literal Selector (0xD0-0xFF)

    /// <summary>
    /// Handles send literal selector bytecodes: Send0 (0xD0-0xDF), Send1 (0xE0-0xEF), Send2 (0xF0-0xFF).
    /// </summary>
    private bool TryDecompileSendLiteralSelector(byte b, Stack<string> stack)
    {
        // Send literal selector with 0 args (0xD0-0xDF)
        if (BytecodeUtil.IsSend0(b))
        {
            int selectorIndex = BytecodeUtil.Send0Index(b);
            if (selectorIndex + 1 >= _literalIds.Length)
            {
                stack.Push($"<send0[{selectorIndex}]?>");
                return true;
            }
            int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
            string selector = FormatLiteral(selectorOop);
            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
            stack.Push($"{receiver} {selector}");
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        if (BytecodeUtil.IsSend1(b))
        {
            int selectorIndex = BytecodeUtil.Send1Index(b);
            if (selectorIndex + 1 >= _literalIds.Length)
            {
                stack.Push($"<send1[{selectorIndex}]?>");
                return true;
            }
            int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
            string selector = FormatLiteral(selectorOop);
            string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
            string send = BuildMessageSend(receiver, selector, [arg]);
            stack.Push(send);
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        if (BytecodeUtil.IsSend2(b))
        {
            int selectorIndex = BytecodeUtil.Send2Index(b);
            if (selectorIndex + 1 >= _literalIds.Length)
            {
                stack.Push($"<send2[{selectorIndex}]?>");
                return true;
            }
            int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
            string selector = FormatLiteral(selectorOop);
            string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
            string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
            string send = BuildMessageSend(receiver, selector, [arg1, arg2]);
            stack.Push(send);
            return true;
        }

        return false;
    }

    #endregion

    #region Block and Misc Bytecodes (0x7D-0x7F)

    /// <summary>
    /// Handles block-related and miscellaneous bytecodes: RETURN_TOP_FROM_BLOCK, reserved, NOP.
    /// </summary>
    private bool TryDecompileBlockAndMiscBytecodes(byte b)
    {
        // Blue Book 0x7D: RETURN_TOP_FROM_BLOCK - local return from block (implicit)
        if (b == 0x7D)
        {
            // Implicit block return - the value is just the last expression's value
            return true;
        }

        // 0x7E: Reserved/unused per Blue Book
        if (b == 0x7E)
        {
            return true;
        }

        // NOP (0x7F)
        if (b == 0x7F)
        {
            return true;
        }

        return false;
    }

    #endregion

    #region Push Active Context (0x89)

    /// <summary>
    /// Handles push active context bytecode (thisContext).
    /// </summary>
    private bool TryDecompilePushActiveContext(byte b, Stack<string> stack)
    {
        if (b == 0x89)
        {
            stack.Push("thisContext");
            return true;
        }
        return false;
    }

    #endregion

    #region Extended Bytecodes (0x80)

    /// <summary>
    /// Handles extended bytecodes (0x80 prefix) with sub-operation byte.
    /// </summary>
    private bool TryDecompileExtendedBytecodes(byte b, byte[] bytecodes, ref int pc,
        Stack<string> stack, List<string> statements)
    {
        if (b != 0x80)
            return false;

        if (pc >= bytecodes.Length)
        {
            statements.Add("<< extended bytecode missing operand >>");
            return true;
        }

        byte subOp = bytecodes[pc++];

        // Dispatch to sub-handlers
        if (TryDecompileExtendedPushStore(subOp, bytecodes, ref pc, stack, statements)) return true;
        if (TryDecompileExtendedSend(subOp, bytecodes, ref pc, stack, statements)) return true;
        if (TryDecompileExtendedStackOps(subOp, stack, statements)) return true;
        if (TryDecompileExtendedJumps(subOp, bytecodes, ref pc, stack, statements)) return true;

        statements.Add($"<< extended bytecode 0x80 0x{subOp:X2} not yet decompiled >>");
        return true;
    }

    /// <summary>
    /// Handles extended push and store operations (0x00-0x09).
    /// </summary>
    private bool TryDecompileExtendedPushStore(byte subOp, byte[] bytecodes, ref int pc,
        Stack<string> stack, List<string> statements)
    {
        switch (subOp)
        {
            case 0x00: // PUSH_IVAR (extended)
                {
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add($"<< PUSH_IVAR missing index >>");
                        return true;
                    }
                    byte index = bytecodes[pc++];
                    string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                    stack.Push(varName);
                    return true;
                }
            case 0x01: // STORE_IVAR (extended)
                {
                    byte index = bytecodes[pc++];
                    string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                    if (stack.Count > 0)
                    {
                        string value = stack.Peek(); // Store doesn't pop
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            case 0x02: // POP_STORE_IVAR (extended)
                {
                    byte index = bytecodes[pc++];
                    string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                    if (stack.Count > 0)
                    {
                        string value = stack.Pop();
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            case 0x03: // PUSH_TEMP (extended)
                {
                    byte index = bytecodes[pc++];
                    string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                    stack.Push(varName);
                    return true;
                }
            case 0x04: // STORE_TEMP (extended)
                {
                    byte index = bytecodes[pc++];
                    string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                    if (stack.Count > 0)
                    {
                        string value = stack.Peek(); // Store doesn't pop
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            case 0x05: // POP_STORE_TEMP (extended)
                {
                    byte index = bytecodes[pc++];
                    string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                    if (stack.Count > 0)
                    {
                        string value = stack.Pop();
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            case 0x06: // PUSH_LITERAL (extended)
                {
                    byte litIndex = bytecodes[pc++];
                    if (litIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<lit[{litIndex}]?>");
                        return true;
                    }
                    int litOop = _literalIds[litIndex + 1];
                    string litValue = FormatLiteral(litOop);
                    stack.Push(litValue);
                    return true;
                }
            case 0x07: // PUSH_LITERAL_VAR (extended)
                {
                    byte litIndex = bytecodes[pc++];
                    if (litIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<litVar[{litIndex}]?>");
                        return true;
                    }
                    int litOop = _literalIds[litIndex + 1];
                    string varName = FormatLiteral(litOop);
                    stack.Push(varName);
                    return true;
                }
            case 0x08: // STORE_LITERAL_VAR (extended)
                {
                    byte litIndex = bytecodes[pc++];
                    if (litIndex + 1 >= _literalIds.Length)
                    {
                        statements.Add($"<storeLitVar[{litIndex}]?>");
                        return true;
                    }
                    int litOop = _literalIds[litIndex + 1];
                    string varName = FormatLiteral(litOop);
                    if (stack.Count > 0)
                    {
                        string value = stack.Peek();
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            case 0x09: // POP_STORE_LITERAL_VAR (extended)
                {
                    byte litIndex = bytecodes[pc++];
                    if (litIndex + 1 >= _literalIds.Length)
                    {
                        statements.Add($"<popStoreLitVar[{litIndex}]?>");
                        return true;
                    }
                    int litOop = _literalIds[litIndex + 1];
                    string varName = FormatLiteral(litOop);
                    if (stack.Count > 0)
                    {
                        string value = stack.Pop();
                        statements.Add($"{varName} := {value}");
                    }
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Handles extended send operations (0x0A-0x11): regular and super sends.
    /// </summary>
    private bool TryDecompileExtendedSend(byte subOp, byte[] bytecodes, ref int pc,
        Stack<string> stack, List<string> statements)
    {
        switch (subOp)
        {
            case 0x0A: // SEND_0 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<send0[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                    stack.Push($"{receiver} {selector}");
                    return true;
                }
            case 0x0B: // SEND_1 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<send1[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
                    string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                    string send = BuildMessageSend(receiver, selector, [arg]);
                    stack.Push(send);
                    return true;
                }
            case 0x0C: // SEND_2 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<send2[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
                    string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
                    string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                    string send = BuildMessageSend(receiver, selector, [arg1, arg2]);
                    stack.Push(send);
                    return true;
                }
            case 0x0D: // SEND_N (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add("<< SEND_N missing arg count >>");
                        return true;
                    }
                    byte argCount = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<sendN[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);

                    var args = new List<string>();
                    for (int i = 0; i < argCount; i++)
                    {
                        args.Insert(0, stack.Count > 0 ? stack.Pop() : $"<arg{i}?>");
                    }
                    string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                    string send = BuildMessageSend(receiver, selector, args.ToArray());
                    stack.Push(send);
                    return true;
                }
            case 0x0E: // SEND_SUPER_0 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<super send0[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    stack.Pop(); // Pop receiver (self)
                    stack.Push($"super {selector}");
                    return true;
                }
            case 0x0F: // SEND_SUPER_1 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<super send1[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
                    stack.Pop(); // Pop receiver (self)
                    string send = BuildMessageSend("super", selector, [arg]);
                    stack.Push(send);
                    return true;
                }
            case 0x10: // SEND_SUPER_2 (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<super send2[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);
                    string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
                    string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
                    stack.Pop(); // Pop receiver (self)
                    string send = BuildMessageSend("super", selector, [arg1, arg2]);
                    stack.Push(send);
                    return true;
                }
            case 0x11: // SEND_SUPER_N (extended)
                {
                    byte selectorIndex = bytecodes[pc++];
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add("<< SEND_SUPER_N missing arg count >>");
                        return true;
                    }
                    byte argCount = bytecodes[pc++];
                    if (selectorIndex + 1 >= _literalIds.Length)
                    {
                        stack.Push($"<super sendN[{selectorIndex}]?>");
                        return true;
                    }
                    int selectorOop = _literalIds[selectorIndex + 1];
                    string selector = FormatLiteral(selectorOop);

                    var args = new List<string>();
                    for (int i = 0; i < argCount; i++)
                    {
                        args.Insert(0, stack.Count > 0 ? stack.Pop() : $"<arg{i}?>");
                    }
                    stack.Pop(); // Pop receiver (self)
                    string send = BuildMessageSend("super", selector, args.ToArray());
                    stack.Push(send);
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Handles extended stack operations (0x12-0x13): POP, DUP.
    /// </summary>
    private bool TryDecompileExtendedStackOps(byte subOp, Stack<string> stack, List<string> statements)
    {
        switch (subOp)
        {
            case 0x12: // POP
                if (stack.Count > 0)
                {
                    string value = stack.Pop();
                    statements.Add(value); // Side-effect expression
                }
                return true;
            case 0x13: // DUP
                if (stack.Count > 0)
                {
                    string value = stack.Peek();
                    stack.Push(value);
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Handles extended jump operations (0x14-0x19): conditional and unconditional jumps.
    /// </summary>
    private bool TryDecompileExtendedJumps(byte subOp, byte[] bytecodes, ref int pc,
        Stack<string> stack, List<string> statements)
    {
        switch (subOp)
        {
            case 0x14: // JUMP_TRUE (extended short)
                {
                    byte offset = bytecodes[pc++];
                    if (stack.Count > 0)
                    {
                        string condition = stack.Pop();
                        statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
                    }
                    return true;
                }
            case 0x15: // JUMP_FALSE (extended short)
                {
                    byte offset = bytecodes[pc++];
                    if (stack.Count > 0)
                    {
                        string condition = stack.Pop();
                        statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                    }
                    return true;
                }
            case 0x16: // JUMP (extended short)
                {
                    byte offset = bytecodes[pc++];
                    statements.Add($"<jump +{offset}>");
                    return true;
                }
            case 0x17: // JUMP_LONG_TRUE (extended)
                {
                    byte offsetHigh = bytecodes[pc++];
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add("<< JUMP_LONG_TRUE missing low byte >>");
                        return true;
                    }
                    byte offsetLow = bytecodes[pc++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    if (stack.Count > 0)
                    {
                        string condition = stack.Pop();
                        statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
                    }
                    return true;
                }
            case 0x18: // JUMP_LONG_FALSE (extended)
                {
                    byte offsetHigh = bytecodes[pc++];
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add("<< JUMP_LONG_FALSE missing low byte >>");
                        return true;
                    }
                    byte offsetLow = bytecodes[pc++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    if (stack.Count > 0)
                    {
                        string condition = stack.Pop();
                        statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                    }
                    return true;
                }
            case 0x19: // JUMP_LONG (extended)
                {
                    byte offsetHigh = bytecodes[pc++];
                    if (pc >= bytecodes.Length)
                    {
                        statements.Add("<< JUMP_LONG missing low byte >>");
                        return true;
                    }
                    byte offsetLow = bytecodes[pc++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    statements.Add($"<jump +{offset}>");
                    return true;
                }
            default:
                return false;
        }
    }

    #endregion

    #region Jump Bytecodes (0x90-0xAF)

    /// <summary>
    /// Handles non-extended jump bytecodes: short/long, conditional/unconditional.
    /// </summary>
    private bool TryDecompileJumpBytecodes(byte b, byte[] bytecodes, ref int pc,
        Stack<string> stack, List<string> statements)
    {
        // Short unconditional forward jump (0x90-0x97)
        if (b >= 0x90 && b <= 0x97)
        {
            int offset = (b & 0x07) + 1;
            statements.Add($"<jump +{offset}>");
            return true;
        }

        // Short conditional forward jump if false (0x98-0x9F)
        if (b >= 0x98 && b <= 0x9F)
        {
            int offset = (b & 0x07) + 1;
            if (stack.Count > 0)
            {
                string condition = stack.Pop();
                statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
            }
            return true;
        }

        // Blue Book Long unconditional jump (0xA0-0xA7)
        if (b >= 0xA0 && b <= 0xA7)
        {
            if (pc >= bytecodes.Length)
            {
                statements.Add("<< long jump missing offset byte >>");
                return true;
            }
            // Blue Book: offset = ((opcode & 0x07) - 4) * 256 + extensionByte
            // Range: -1024 to +1023 (can jump backward or forward)
            int baseOffset = ((b & 0x07) - 4) * 256;
            int extensionByte = bytecodes[pc++];
            int offset = baseOffset + extensionByte;
            if (offset >= 0)
                statements.Add($"<jump +{offset}>");
            else
                statements.Add($"<jump {offset}>");
            return true;
        }

        // Long conditional forward jump if true (0xA8-0xAB)
        if (b >= 0xA8 && b <= 0xAB)
        {
            if (pc >= bytecodes.Length)
            {
                statements.Add("<< long jump true missing offset byte >>");
                return true;
            }
            int offsetHigh = (b & 0x03);
            int offsetLow = bytecodes[pc++];
            int offset = (offsetHigh << 8) | offsetLow;
            if (stack.Count > 0)
            {
                string condition = stack.Pop();
                statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
            }
            return true;
        }

        // Long conditional forward jump if false (0xAC-0xAF)
        if (b >= 0xAC && b <= 0xAF)
        {
            if (pc >= bytecodes.Length)
            {
                statements.Add("<< long jump false missing offset byte >>");
                return true;
            }
            int offsetHigh = (b & 0x03);
            int offsetLow = bytecodes[pc++];
            int offset = (offsetHigh << 8) | offsetLow;
            if (stack.Count > 0)
            {
                string condition = stack.Pop();
                statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
            }
            return true;
        }

        return false;
    }

    #endregion

    #region Helper Methods

    private string BuildMessageSend(string receiver, string selector, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{receiver} {selector}";
        }

        // Keyword message
        string[] parts = selector.Split(':', StringSplitOptions.RemoveEmptyEntries);

        // Validate that parts count matches args count
        if (parts.Length != args.Length)
        {
            // Fallback: just append selector and args
            return $"{receiver} {selector} {string.Join(" ", args)}";
        }

        var result = new StringBuilder(receiver);
        result.Append(' ');

        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) result.Append(' ');
            result.Append(parts[i]);
            result.Append(": ");
            result.Append(args[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Formats a literal OOP value for display in decompiled source.
    /// </summary>
    /// <param name="oop">The OOP value to format.</param>
    /// <returns>A string representation of the literal.</returns>
    private string FormatLiteral(int oop)
    {
        // Try to get symbol name from lookup
        if (_symbolNameLookup.TryGetValue(oop, out string? symbolName))
        {
            return $"#{symbolName}";
        }

        // Try SmallInteger
        var smallIntOOP = new SmalltalkOOP(oop);
        if (smallIntOOP.IsSmallInteger)
        {
            return smallIntOOP.AsSmallInteger().ToString();
        }

        // For other objects, show OOP
        return $"<oop:{oop}>";
    }

    /// <summary>
    /// Generates a detailed error message for stack underflow during decompilation.
    /// </summary>
    private static string DecompilerStackError(int pc, byte bytecode, int required, int actual)
    {
        return $"Decompiler stack underflow at PC {pc}: bytecode 0x{bytecode:X2} requires {required} stack item(s), " +
               $"but only {actual} available. This likely indicates unreachable code after an unconditional jump, " +
               $"or incorrect control flow in the bytecode. The decompiler uses linear scanning and does not " +
               $"follow jump targets, which can cause stack simulation to diverge from actual execution. " +
               $"See docs/Decompiler-Jump-Handling-Bug.md for details.";
    }

    #endregion
}
