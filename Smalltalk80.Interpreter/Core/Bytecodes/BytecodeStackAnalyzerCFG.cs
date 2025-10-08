using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Interpreter.Core.Bytecodes;

/// <summary>
/// Control-flow-graph-aware bytecode stack analyzer.
/// Validates stack balance by following all possible execution paths through the bytecode.
/// </summary>
public static class BytecodeStackAnalyzerCFG
{
    /// <summary>
    /// Result of analyzing a bytecode sequence with CFG-based analysis.
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
    /// Analyzes bytecode using control flow graph to follow all possible execution paths.
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

        try
        {
            // Build control flow graph
            var cfg = BuildCFG(bytecodes);

            // Compute stack depth for all reachable instructions
            var stackDepths = ComputeStackDepths(bytecodes, cfg);

            // Find maximum stack depth across all paths
            int maxDepth = stackDepths.Values.Where(d => d >= 0).DefaultIfEmpty(0).Max();

            return new AnalysisResult
            {
                IsBalanced = true,
                MaxStackDepth = maxDepth,
                FinalStackEffect = 0, // Not meaningful with multiple exit points
                ErrorMessage = null,
                ErrorOffset = null
            };
        }
        catch (StackAnalysisException ex)
        {
            return new AnalysisResult
            {
                IsBalanced = false,
                MaxStackDepth = 0,
                FinalStackEffect = 0,
                ErrorMessage = ex.Message,
                ErrorOffset = ex.Offset
            };
        }
        catch (Exception ex)
        {
            return new AnalysisResult
            {
                IsBalanced = false,
                MaxStackDepth = 0,
                FinalStackEffect = 0,
                ErrorMessage = $"Analysis error: {ex.Message}",
                ErrorOffset = null
            };
        }
    }

    /// <summary>
    /// Builds a control flow graph mapping each PC to its possible successors.
    /// </summary>
    private static Dictionary<int, List<int>> BuildCFG(byte[] bytecodes)
    {
        var cfg = new Dictionary<int, List<int>>();
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            int currentPc = pc;
            byte b = bytecodes[pc++];

            var successors = new List<int>();

            // Check for return instructions (no successors)
            if (BytecodeUtil.IsReturn(b))
            {
                cfg[currentPc] = successors; // Empty list - end of path
                continue;
            }

            // Check for jump instructions
            int? jumpTarget = GetJumpTarget(b, bytecodes, ref pc, currentPc);
            if (jumpTarget.HasValue)
            {
                bool isConditional = IsConditionalJump(b, bytecodes, currentPc);

                if (isConditional)
                {
                    // Conditional jump: two successors (jump target and fall-through)
                    successors.Add(jumpTarget.Value);
                    successors.Add(pc); // Fall-through
                }
                else
                {
                    // Unconditional jump: one successor (jump target only)
                    successors.Add(jumpTarget.Value);
                }
            }
            else
            {
                // Normal instruction: fall through to next PC
                if (pc < bytecodes.Length)
                {
                    successors.Add(pc);
                }
            }

            cfg[currentPc] = successors;
        }

        return cfg;
    }

    /// <summary>
    /// Computes stack depth at each instruction using dataflow analysis.
    /// </summary>
    private static Dictionary<int, int> ComputeStackDepths(byte[] bytecodes, Dictionary<int, List<int>> cfg)
    {
        // Map: PC -> stack depth at that PC
        var stackDepths = new Dictionary<int, int>();

        // Worklist: PCs that need to be processed
        var worklist = new Queue<(int pc, int depth)>();

        // Start at PC 0 with depth 0
        worklist.Enqueue((0, 0));
        stackDepths[0] = 0;

        while (worklist.Count > 0)
        {
            var (pc, currentDepth) = worklist.Dequeue();

            if (pc >= bytecodes.Length)
                continue;

            // Get stack effect of current instruction
            int tempPc = pc + 1;
            byte b = bytecodes[pc];
            int effect = BytecodeStackAnalyzer_GetStackEffect(b, bytecodes, ref tempPc);

            int newDepth = currentDepth + effect;

            // Check for stack underflow
            if (newDepth < 0)
            {
                throw new StackAnalysisException(
                    $"Stack underflow at PC {pc}: bytecode 0x{b:X2} would cause stack depth {newDepth}",
                    pc);
            }

            // Process successors
            if (cfg.TryGetValue(pc, out var successors))
            {
                foreach (int successor in successors)
                {
                    // Check if we've seen this successor before
                    if (stackDepths.TryGetValue(successor, out int existingDepth))
                    {
                        // Verify stack depth consistency at join points
                        if (existingDepth != newDepth)
                        {
                            throw new StackAnalysisException(
                                $"Inconsistent stack depth at PC {successor}: " +
                                $"expected {existingDepth}, got {newDepth} from PC {pc}",
                                successor);
                        }
                        // Already processed with same depth - skip
                    }
                    else
                    {
                        // First time seeing this PC - add to worklist
                        stackDepths[successor] = newDepth;
                        worklist.Enqueue((successor, newDepth));
                    }
                }
            }
        }

        return stackDepths;
    }

    /// <summary>
    /// Gets the jump target for a jump instruction, or null if not a jump.
    /// </summary>
    private static int? GetJumpTarget(byte b, byte[] bytecodes, ref int pc, int currentPc)
    {
        // Short unconditional forward jump (0x90-0x97)
        if (b >= 0x90 && b <= 0x97)
        {
            int offset = (b & 0x07) + 1;
            return currentPc + 1 + offset; // Target is relative to next PC
        }

        // Short conditional forward jump if false (0x98-0x9F)
        if (b >= 0x98 && b <= 0x9F)
        {
            int offset = (b & 0x07) + 1;
            return currentPc + 1 + offset;
        }

        // Long jumps (0xA0-0xA7)
        if (b >= 0xA0 && b <= 0xA7)
        {
            if (pc >= bytecodes.Length)
                return null;

            int highBits = (b & 0x07) << 8;
            int lowBits = bytecodes[pc++];
            int offset = highBits | lowBits;
            return currentPc + 2 + offset; // Target is relative to PC after offset byte
        }

        // Extended prefix
        if (BytecodeUtil.IsExtended(b))
        {
            if (pc >= bytecodes.Length)
                return null;

            byte subOp = bytecodes[pc];

            // Extended jump instructions (0x14-0x19)
            if (subOp >= 0x14 && subOp <= 0x19)
            {
                pc++; // Skip subop
                if (pc + 1 >= bytecodes.Length)
                    return null;

                int highByte = bytecodes[pc++];
                int lowByte = bytecodes[pc++];
                int offset = (highByte << 8) | lowByte;
                return currentPc + 4 + offset; // Target is relative to PC after 2-byte offset
            }
        }

        return null; // Not a jump
    }

    /// <summary>
    /// Returns true if the instruction is a conditional jump.
    /// </summary>
    private static bool IsConditionalJump(byte b, byte[] bytecodes, int currentPc)
    {
        // Short conditional jump (0x98-0x9F)
        if (b >= 0x98 && b <= 0x9F)
            return true;

        // Extended conditional jumps
        if (BytecodeUtil.IsExtended(b))
        {
            int subOpPc = currentPc + 1;
            if (subOpPc < bytecodes.Length)
            {
                byte subOp = bytecodes[subOpPc];
                // JUMP_TRUE, JUMP_FALSE, JUMP_LONG_TRUE, JUMP_LONG_FALSE
                return subOp == 0x14 || subOp == 0x15 || subOp == 0x17 || subOp == 0x18;
            }
        }

        return false;
    }

    /// <summary>
    /// Re-use the stack effect computation from BytecodeStackAnalyzer.
    /// This is a copy to avoid circular dependencies.
    /// </summary>
    private static int BytecodeStackAnalyzer_GetStackEffect(byte b, byte[] bytecodes, ref int pc)
    {
        // Delegate to the existing BytecodeStackAnalyzer implementation
        // We need to create a temporary PC context
        int tempPc = pc - 1; // Back up to include the current byte
        var effect = BytecodeStackAnalyzer.Analyze(new[] { b });

        // Actually, we need to properly call the private method.
        // For now, use reflection or duplicate the logic.
        // Let's duplicate the core logic here:

        return GetStackEffectCore(b, bytecodes, ref pc);
    }

    private static int GetStackEffectCore(byte b, byte[] bytecodes, ref int pc)
    {
        // Push instructions: +1
        if (BytecodeUtil.IsPushIvar(b) || BytecodeUtil.IsPushTemp(b) ||
            BytecodeUtil.IsPushLiteralConst(b) || BytecodeUtil.IsPushLiteralVar(b) ||
            BytecodeUtil.IsPushSpecial(b) || b == 137)
            return +1;

        // Pop and store: -1
        if (BytecodeUtil.IsPopStoreIvar(b) || BytecodeUtil.IsPopStoreTemp(b))
            return -1;

        // Returns: 0 (they don't affect the method's internal stack)
        if (BytecodeUtil.IsReturn(b))
            return 0;

        // Jumps: depends on type
        if (b >= 0x90 && b <= 0x97) // Short unconditional jump
            return 0;

        if (b >= 0x98 && b <= 0x9F) // Short conditional jump (pops condition)
            return -1;

        if (b >= 0xA0 && b <= 0xA7) // Long jump
        {
            pc++; // Skip offset byte
            return (b >= 0xA0 && b <= 0xA3) ? 0 : -1; // A0-A3 unconditional, A4-A7 conditional
        }

        // Extended bytecodes
        if (BytecodeUtil.IsExtended(b))
        {
            if (pc >= bytecodes.Length)
                return 0;

            byte subOp = bytecodes[pc++];
            return subOp switch
            {
                0x00 or 0x03 or 0x06 or 0x07 => SkipOne(bytecodes, ref pc, +1), // Push
                0x01 or 0x04 or 0x08 => SkipOne(bytecodes, ref pc, 0),          // Store (no pop)
                0x02 or 0x05 or 0x09 => SkipOne(bytecodes, ref pc, -1),         // Pop-store
                0x0A or 0x0E => SkipOne(bytecodes, ref pc, 0),                  // Send 0 args
                0x0B or 0x0F => SkipOne(bytecodes, ref pc, -1),                 // Send 1 arg
                0x0C or 0x10 => SkipOne(bytecodes, ref pc, -2),                 // Send 2 args
                0x0D or 0x11 => SkipTwo(bytecodes, ref pc, GetSendNEffect(bytecodes, pc - 1)),
                0x12 => -1, // POP
                0x13 => +1, // DUP
                0x14 or 0x15 or 0x17 or 0x18 => SkipTwo(bytecodes, ref pc, -1), // Conditional jump
                0x16 or 0x19 => SkipTwo(bytecodes, ref pc, 0),                  // Unconditional jump
                _ => 0
            };
        }

        // Send arithmetic: -1 (pop receiver + arg, push result)
        if (BytecodeUtil.IsSendArithmetic(b))
            return -1;

        // Send special
        if (BytecodeUtil.IsSendSpecial(b))
        {
            int argCount = GetSpecialArgCount(b & 0x0F);
            return -argCount; // Pop receiver + args, push result
        }

        // Send literal 0/1/2 args
        if (BytecodeUtil.IsSend0(b)) return 0;   // -1 receiver, +1 result
        if (BytecodeUtil.IsSend1(b)) return -1;  // -2 (rcv+arg), +1 result
        if (BytecodeUtil.IsSend2(b)) return -2;  // -3 (rcv+2args), +1 result

        return 0;
    }

    private static int SkipOne(byte[] bytecodes, ref int pc, int effect)
    {
        if (pc < bytecodes.Length) pc++;
        return effect;
    }

    private static int SkipTwo(byte[] bytecodes, ref int pc, int effect)
    {
        if (pc < bytecodes.Length) pc++;
        if (pc < bytecodes.Length) pc++;
        return effect;
    }

    private static int GetSendNEffect(byte[] bytecodes, int pc)
    {
        if (pc + 1 < bytecodes.Length)
        {
            int argCount = bytecodes[pc + 1];
            return -argCount;
        }
        return 0;
    }

    private static int GetSpecialArgCount(int index)
    {
        return index switch
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

    /// <summary>
    /// Exception thrown when stack analysis detects an error.
    /// </summary>
    private class StackAnalysisException : Exception
    {
        public int Offset { get; }

        public StackAnalysisException(string message, int offset) : base(message)
        {
            Offset = offset;
        }
    }
}
