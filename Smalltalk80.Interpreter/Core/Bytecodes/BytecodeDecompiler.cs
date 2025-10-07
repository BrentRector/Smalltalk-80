using System.Text;
using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Core.Bytecodes;

/// <summary>
/// Decompiles Smalltalk-80 bytecode back into high-level Smalltalk source code.
/// Used for validation - decompiled output should match original source.
/// </summary>
public class BytecodeDecompiler
{
    private readonly SmalltalkHeap _heap;
    private readonly string[] _symbols;
    private readonly int[] _literalIds;
    private readonly string[] _tempNames;
    private readonly string[] _instVarNames;

    public BytecodeDecompiler(
        SmalltalkHeap heap,
        string[] symbols,
        int[] literalIds,
        string[] tempNames,
        string[] instVarNames)
    {
        _heap = heap;
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

            // Push receiver variable 0-15 (0x00-0x0F)
            if (BytecodeUtil.IsPushIvar(b))
            {
                int index = BytecodeUtil.PushIvarIndex(b);
                string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                stack.Push(varName);
                continue;
            }

            // Push temporary variable 0-15 (0x10-0x1F)
            if (BytecodeUtil.IsPushTemp(b))
            {
                int index = BytecodeUtil.PushTempIndex(b);
                string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                stack.Push(varName);
                continue;
            }

            // Push literal constant 0-31 (0x20-0x3F)
            if (BytecodeUtil.IsPushLiteralConst(b))
            {
                int litIndex = BytecodeUtil.PushLiteralConstIndex(b);
                if (litIndex + 1 >= _literalIds.Length)
                {
                    stack.Push($"<lit[{litIndex}]?>");
                    continue;
                }
                int litOop = _literalIds[litIndex + 1]; // +1 to skip header
                string litValue = FormatLiteral(litOop);
                stack.Push(litValue);
                continue;
            }

            // Push literal variable 0-31 (0x40-0x5F)
            if (BytecodeUtil.IsPushLiteralVar(b))
            {
                int litIndex = BytecodeUtil.PushLiteralVarIndex(b);
                if (litIndex + 1 >= _literalIds.Length)
                {
                    stack.Push($"<litVar[{litIndex}]?>");
                    continue;
                }
                int litOop = _literalIds[litIndex + 1]; // +1 to skip header
                string varName = FormatLiteral(litOop);
                stack.Push(varName);
                continue;
            }

            // Pop and store receiver variable 0-7 (0x60-0x67)
            if (BytecodeUtil.IsPopStoreIvar(b))
            {
                int index = BytecodeUtil.PopStoreIvarIndex(b);
                string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                string value = stack.Pop();
                statements.Add($"{varName} := {value}");
                continue;
            }

            // Pop and store temporary variable 0-68-0x6F)
            if (BytecodeUtil.IsPopStoreTemp(b))
            {
                int index = BytecodeUtil.PopStoreTempIndex(b);
                string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                string value = stack.Pop();
                statements.Add($"{varName} := {value}");
                continue;
            }

            // Special constants (0x70-0x77)
            switch ((Bytecode)b)
            {
                case Bytecode.PUSH_SELF:
                    stack.Push("self");
                    continue;
                case Bytecode.PUSH_TRUE:
                    stack.Push("true");
                    continue;
                case Bytecode.PUSH_FALSE:
                    stack.Push("false");
                    continue;
                case Bytecode.PUSH_NIL:
                    stack.Push("nil");
                    continue;
                case Bytecode.PUSH_MINUS_ONE:
                    stack.Push("-1");
                    continue;
                case Bytecode.PUSH_ZERO:
                    stack.Push("0");
                    continue;
                case Bytecode.PUSH_ONE:
                    stack.Push("1");
                    continue;
                case Bytecode.PUSH_TWO:
                    stack.Push("2");
                    continue;
            }

            // Returns (0x78-0x7C)
            switch ((Bytecode)b)
            {
                case Bytecode.RETURN_SELF:
                    statements.Add("^ self");
                    continue;
                case Bytecode.RETURN_TRUE:
                    statements.Add("^ true");
                    continue;
                case Bytecode.RETURN_FALSE:
                    statements.Add("^ false");
                    continue;
                case Bytecode.RETURN_NIL:
                    statements.Add("^ nil");
                    continue;
                case Bytecode.RETURN_TOP:
                    if (stack.Count > 0)
                    {
                        string value = stack.Pop();
                        statements.Add($"^ {value}");
                    }
                    continue;
            }

            // Send arithmetic (0xB0-0xBF)
            if (BytecodeUtil.IsSendArithmetic(b))
            {
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
                continue;
            }

            // Send special (0xC0-0xCF)
            if (BytecodeUtil.IsSendSpecial(b))
            {
                string[] specialSelectors = new[]
                {
                    "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
                    "==", "class", "blockCopy:", "value", "value:", "do:",
                    "new", "new:", "x", "y"
                };

                int selectorIndex = b & 0x0F;
                string selector = specialSelectors[selectorIndex];
                int argCount = selector.Count(c => c == ':');

                List<string> args = new List<string>();
                for (int i = 0; i < argCount; i++)
                {
                    args.Insert(0, stack.Pop());
                }
                string receiver = stack.Pop();

                string send = BuildMessageSend(receiver, selector, args.ToArray());
                stack.Push(send);
                continue;
            }

            // Send literal selector with 0 args (0xD0-0xDF)
            if (BytecodeUtil.IsSend0(b))
            {
                int selectorIndex = BytecodeUtil.Send0Index(b);
                if (selectorIndex + 1 >= _literalIds.Length)
                {
                    stack.Push($"<send0[{selectorIndex}]?>");
                    continue;
                }
                int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
                string selector = FormatLiteral(selectorOop);
                string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                stack.Push($"{receiver} {selector}");
                continue;
            }

            // Send literal selector with 1 arg (0xE0-0xEF)
            if (BytecodeUtil.IsSend1(b))
            {
                int selectorIndex = BytecodeUtil.Send1Index(b);
                if (selectorIndex + 1 >= _literalIds.Length)
                {
                    stack.Push($"<send1[{selectorIndex}]?>");
                    continue;
                }
                int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
                string selector = FormatLiteral(selectorOop);
                string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
                string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                string send = BuildMessageSend(receiver, selector, new[] { arg });
                stack.Push(send);
                continue;
            }

            // Send literal selector with 2 args (0xF0-0xFF)
            if (BytecodeUtil.IsSend2(b))
            {
                int selectorIndex = BytecodeUtil.Send2Index(b);
                if (selectorIndex + 1 >= _literalIds.Length)
                {
                    stack.Push($"<send2[{selectorIndex}]?>");
                    continue;
                }
                int selectorOop = _literalIds[selectorIndex + 1]; // +1 to skip header
                string selector = FormatLiteral(selectorOop);
                string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
                string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
                string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                string send = BuildMessageSend(receiver, selector, new[] { arg1, arg2 });
                stack.Push(send);
                continue;
            }

            // Block returns (0x7D-0x7E)
            if (b == 0x7D) // RETURN_SELF_POP
            {
                statements.Add("^ self");
                continue;
            }
            if (b == 0x7E) // RETURN_TOP_POP
            {
                if (stack.Count > 0)
                {
                    string value = stack.Pop();
                    statements.Add($"^ {value}");
                }
                continue;
            }

            // NOP (0x7F)
            if (b == 0x7F)
            {
                continue; // Skip NOP
            }

            // Extended bytecodes (0x80)
            if (b == 0x80)
            {
                if (pc >= bytecodes.Length)
                {
                    statements.Add("<< extended bytecode missing operand >>");
                    break;
                }

                byte subOp = bytecodes[pc++];

                switch (subOp)
                {
                    case 0x00: // PUSH_IVAR (extended)
                        {
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add($"<< PUSH_IVAR missing index >>");
                                break;
                            }
                            byte index = bytecodes[pc++];
                            string varName = index < _instVarNames.Length ? _instVarNames[index] : $"instVar{index}";
                            stack.Push(varName);
                            continue;
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
                            continue;
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
                            continue;
                        }
                    case 0x03: // PUSH_TEMP (extended)
                        {
                            byte index = bytecodes[pc++];
                            string varName = index < _tempNames.Length ? _tempNames[index] : $"temp{index}";
                            stack.Push(varName);
                            continue;
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
                            continue;
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
                            continue;
                        }
                    case 0x06: // PUSH_LITERAL (extended)
                        {
                            byte litIndex = bytecodes[pc++];
                            if (litIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<lit[{litIndex}]?>");
                                continue;
                            }
                            int litOop = _literalIds[litIndex + 1];
                            string litValue = FormatLiteral(litOop);
                            stack.Push(litValue);
                            continue;
                        }
                    case 0x07: // PUSH_LITERAL_VAR (extended)
                        {
                            byte litIndex = bytecodes[pc++];
                            if (litIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<litVar[{litIndex}]?>");
                                continue;
                            }
                            int litOop = _literalIds[litIndex + 1];
                            string varName = FormatLiteral(litOop);
                            stack.Push(varName);
                            continue;
                        }
                    case 0x08: // STORE_LITERAL_VAR (extended)
                        {
                            byte litIndex = bytecodes[pc++];
                            if (litIndex + 1 >= _literalIds.Length)
                            {
                                statements.Add($"<storeLitVar[{litIndex}]?>");
                                continue;
                            }
                            int litOop = _literalIds[litIndex + 1];
                            string varName = FormatLiteral(litOop);
                            if (stack.Count > 0)
                            {
                                string value = stack.Peek();
                                statements.Add($"{varName} := {value}");
                            }
                            continue;
                        }
                    case 0x09: // POP_STORE_LITERAL_VAR (extended)
                        {
                            byte litIndex = bytecodes[pc++];
                            if (litIndex + 1 >= _literalIds.Length)
                            {
                                statements.Add($"<popStoreLitVar[{litIndex}]?>");
                                continue;
                            }
                            int litOop = _literalIds[litIndex + 1];
                            string varName = FormatLiteral(litOop);
                            if (stack.Count > 0)
                            {
                                string value = stack.Pop();
                                statements.Add($"{varName} := {value}");
                            }
                            continue;
                        }
                    case 0x0A: // SEND_0 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<send0[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                            stack.Push($"{receiver} {selector}");
                            continue;
                        }
                    case 0x0B: // SEND_1 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<send1[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
                            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                            string send = BuildMessageSend(receiver, selector, new[] { arg });
                            stack.Push(send);
                            continue;
                        }
                    case 0x0C: // SEND_2 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<send2[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
                            string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
                            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                            string send = BuildMessageSend(receiver, selector, new[] { arg1, arg2 });
                            stack.Push(send);
                            continue;
                        }
                    case 0x0D: // SEND_N (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add("<< SEND_N missing arg count >>");
                                break;
                            }
                            byte argCount = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<sendN[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);

                            List<string> args = new List<string>();
                            for (int i = 0; i < argCount; i++)
                            {
                                args.Insert(0, stack.Count > 0 ? stack.Pop() : $"<arg{i}?>");
                            }
                            string receiver = stack.Count > 0 ? stack.Pop() : "<receiver?>";
                            string send = BuildMessageSend(receiver, selector, args.ToArray());
                            stack.Push(send);
                            continue;
                        }
                    case 0x0E: // SEND_SUPER_0 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<super send0[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            stack.Pop(); // Pop receiver (self)
                            stack.Push($"super {selector}");
                            continue;
                        }
                    case 0x0F: // SEND_SUPER_1 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<super send1[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            string arg = stack.Count > 0 ? stack.Pop() : "<arg?>";
                            stack.Pop(); // Pop receiver (self)
                            string send = BuildMessageSend("super", selector, new[] { arg });
                            stack.Push(send);
                            continue;
                        }
                    case 0x10: // SEND_SUPER_2 (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<super send2[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);
                            string arg2 = stack.Count > 0 ? stack.Pop() : "<arg2?>";
                            string arg1 = stack.Count > 0 ? stack.Pop() : "<arg1?>";
                            stack.Pop(); // Pop receiver (self)
                            string send = BuildMessageSend("super", selector, new[] { arg1, arg2 });
                            stack.Push(send);
                            continue;
                        }
                    case 0x11: // SEND_SUPER_N (extended)
                        {
                            byte selectorIndex = bytecodes[pc++];
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add("<< SEND_SUPER_N missing arg count >>");
                                break;
                            }
                            byte argCount = bytecodes[pc++];
                            if (selectorIndex + 1 >= _literalIds.Length)
                            {
                                stack.Push($"<super sendN[{selectorIndex}]?>");
                                continue;
                            }
                            int selectorOop = _literalIds[selectorIndex + 1];
                            string selector = FormatLiteral(selectorOop);

                            List<string> args = new List<string>();
                            for (int i = 0; i < argCount; i++)
                            {
                                args.Insert(0, stack.Count > 0 ? stack.Pop() : $"<arg{i}?>");
                            }
                            stack.Pop(); // Pop receiver (self)
                            string send = BuildMessageSend("super", selector, args.ToArray());
                            stack.Push(send);
                            continue;
                        }
                    case 0x12: // POP
                        {
                            if (stack.Count > 0)
                            {
                                string value = stack.Pop();
                                statements.Add(value); // Side-effect expression
                            }
                            continue;
                        }
                    case 0x13: // DUP
                        {
                            if (stack.Count > 0)
                            {
                                string value = stack.Peek();
                                stack.Push(value);
                            }
                            continue;
                        }
                    case 0x14: // JUMP_TRUE (extended short)
                        {
                            byte offset = bytecodes[pc++];
                            if (stack.Count > 0)
                            {
                                string condition = stack.Pop();
                                statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
                            }
                            continue;
                        }
                    case 0x15: // JUMP_FALSE (extended short)
                        {
                            byte offset = bytecodes[pc++];
                            if (stack.Count > 0)
                            {
                                string condition = stack.Pop();
                                statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                            }
                            continue;
                        }
                    case 0x16: // JUMP (extended short)
                        {
                            byte offset = bytecodes[pc++];
                            statements.Add($"<jump +{offset}>");
                            continue;
                        }
                    case 0x17: // JUMP_LONG_TRUE (extended)
                        {
                            byte offsetHigh = bytecodes[pc++];
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add("<< JUMP_LONG_TRUE missing low byte >>");
                                break;
                            }
                            byte offsetLow = bytecodes[pc++];
                            int offset = (offsetHigh << 8) | offsetLow;
                            if (stack.Count > 0)
                            {
                                string condition = stack.Pop();
                                statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
                            }
                            continue;
                        }
                    case 0x18: // JUMP_LONG_FALSE (extended)
                        {
                            byte offsetHigh = bytecodes[pc++];
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add("<< JUMP_LONG_FALSE missing low byte >>");
                                break;
                            }
                            byte offsetLow = bytecodes[pc++];
                            int offset = (offsetHigh << 8) | offsetLow;
                            if (stack.Count > 0)
                            {
                                string condition = stack.Pop();
                                statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                            }
                            continue;
                        }
                    case 0x19: // JUMP_LONG (extended)
                        {
                            byte offsetHigh = bytecodes[pc++];
                            if (pc >= bytecodes.Length)
                            {
                                statements.Add("<< JUMP_LONG missing low byte >>");
                                break;
                            }
                            byte offsetLow = bytecodes[pc++];
                            int offset = (offsetHigh << 8) | offsetLow;
                            statements.Add($"<jump +{offset}>");
                            continue;
                        }
                    default:
                        statements.Add($"<< extended bytecode 0x80 0x{subOp:X2} not yet decompiled >>");
                        continue;
                }
            }

            // Push active context (0x89)
            if (b == 0x89)
            {
                stack.Push("thisContext");
                continue;
            }

            // Jump bytecodes (0x90-0xA7)
            if (b >= 0x90 && b <= 0x97) // Short unconditional forward jump
            {
                int offset = (b & 0x07) + 1;
                statements.Add($"<jump +{offset}>");
                continue;
            }
            if (b >= 0x98 && b <= 0x9F) // Short conditional forward jump if false
            {
                int offset = (b & 0x07) + 1;
                if (stack.Count > 0)
                {
                    string condition = stack.Pop();
                    statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                }
                continue;
            }
            if (b >= 0xA0 && b <= 0xA7) // Long unconditional forward jump
            {
                if (pc >= bytecodes.Length)
                {
                    statements.Add("<< long jump missing offset byte >>");
                    break;
                }
                int offset = ((b & 0x07) << 8) | bytecodes[pc++];
                statements.Add($"<jump +{offset}>");
                continue;
            }
            if (b >= 0xA8 && b <= 0xAB) // Long conditional forward jump if true
            {
                if (pc >= bytecodes.Length)
                {
                    statements.Add("<< long jump true missing offset byte >>");
                    break;
                }
                int offsetHigh = (b & 0x03);
                int offsetLow = bytecodes[pc++];
                int offset = (offsetHigh << 8) | offsetLow;
                if (stack.Count > 0)
                {
                    string condition = stack.Pop();
                    statements.Add($"{condition} ifTrue: [ <jump +{offset}> ]");
                }
                continue;
            }
            if (b >= 0xAC && b <= 0xAF) // Long conditional forward jump if false
            {
                if (pc >= bytecodes.Length)
                {
                    statements.Add("<< long jump false missing offset byte >>");
                    break;
                }
                int offsetHigh = (b & 0x03);
                int offsetLow = bytecodes[pc++];
                int offset = (offsetHigh << 8) | offsetLow;
                if (stack.Count > 0)
                {
                    string condition = stack.Pop();
                    statements.Add($"{condition} ifFalse: [ <jump +{offset}> ]");
                }
                continue;
            }

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

    private string FormatLiteral(int oop)
    {
        // Try to get symbol name
        string? symbolName = _heap.GetSymbolName(oop);
        if (symbolName != null)
        {
            return $"#{symbolName}";
        }

        // Try SmallInteger
        if ((oop & 1) == 1)
        {
            return (oop >> 1).ToString();
        }

        // For other objects, show OOP
        return $"<oop:{oop}>";
    }
}
