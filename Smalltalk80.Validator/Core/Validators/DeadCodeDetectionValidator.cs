using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Detects dead/unreachable code in bytecode:
/// - Unreachable bytecode not targeted by any jumps
/// - Code after unconditional returns
/// - Large unreachable regions indicating compiler bugs
/// </summary>
public class DeadCodeDetectionValidator : IValidator
{
    public string Id => "DeadCodeDetectionValidator";
    public bool IsHeavy => true;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Check all methods
        foreach (var method in ctx.Methods)
        {
            var localId = method.GetStringProp("localId") ?? "unknown";
            var bytecodes = method.GetIntArray("bytecodes");

            if (bytecodes.Length == 0) continue;

            var analysis = AnalyzeBytecode(bytecodes);

            if (analysis.DeadRegions.Count > 0)
            {
                foreach (var region in analysis.DeadRegions)
                {
                    int deadBytes = region.End - region.Start + 1;

                    if (deadBytes >= 10)
                    {
                        // Large dead region - likely a compiler bug
                        diagnostics.Add(new Diagnostic(
                            ErrorCodes.BytecodeUnreachable,
                            Severity.Warning,
                            $"Method '{localId}' has {deadBytes} bytes of unreachable code at PC {region.Start}-{region.End}",
                            new[] { new Location("methods", localId, Pc: region.Start) },
                "Large unreachable regions indicate compiler bugs"));
                    }
                    else if (deadBytes >= 3)
                    {
                        diagnostics.Add(new Diagnostic(
                            ErrorCodes.BytecodeUnreachable,
                            Severity.Info,
                            $"Method '{localId}' has {deadBytes} bytes of unreachable code at PC {region.Start}",
                            new[] { new Location("methods", localId, Pc: region.Start) },
                "Consider removing unreachable code"));
                    }
                }
            }
        }

        // Check init expressions
        foreach (var initExpr in ctx.InitExpressions)
        {
            var localId = initExpr.GetStringProp("localId") ?? "unknown";
            var bytecodes = initExpr.GetIntArray("bytecodes");

            if (bytecodes.Length == 0) continue;

            var analysis = AnalyzeBytecode(bytecodes);

            if (analysis.DeadRegions.Count > 0)
            {
                foreach (var region in analysis.DeadRegions)
                {
                    int deadBytes = region.End - region.Start + 1;

                    if (deadBytes >= 5)
                    {
                        diagnostics.Add(new Diagnostic(
                            ErrorCodes.BytecodeUnreachable,
                            Severity.Warning,
                            $"Init expression '{localId}' has {deadBytes} bytes of unreachable code",
                            new[] { new Location("initExpressions", localId, Pc: region.Start) },
                "Unreachable code in initialization"));
                    }
                }
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private class DeadCodeAnalysis
    {
        public List<(int Start, int End)> DeadRegions { get; } = new();
    }

    private DeadCodeAnalysis AnalyzeBytecode(int[] bytecodeInts)
    {
        var analysis = new DeadCodeAnalysis();

        if (bytecodeInts.Length == 0)
            return analysis;

        // Convert to byte array
        var bytecodes = new byte[bytecodeInts.Length];
        for (int i = 0; i < bytecodeInts.Length; i++)
        {
            bytecodes[i] = (byte)bytecodeInts[i];
        }

        // Build control flow graph
        var reachable = new HashSet<int>();
        var jumpTargets = new HashSet<int>();

        // First pass: identify all jump targets
        int pc = 0;
        while (pc < bytecodes.Length)
        {
            byte opcode = bytecodes[pc];

            if (IsJump(opcode))
            {
                int target = GetJumpTarget(bytecodes, pc);
                if (target >= 0 && target < bytecodes.Length)
                {
                    jumpTargets.Add(target);
                }
            }

            pc = AdvancePC(bytecodes, pc);
        }

        // Second pass: mark reachable code using flood fill
        var worklist = new Queue<int>();
        worklist.Enqueue(0); // Start from PC 0

        // Also start from all jump targets (exception handlers)
        foreach (var target in jumpTargets)
        {
            worklist.Enqueue(target);
        }

        while (worklist.Count > 0)
        {
            pc = worklist.Dequeue();

            if (pc < 0 || pc >= bytecodes.Length || reachable.Contains(pc))
                continue;

            reachable.Add(pc);

            byte opcode = bytecodes[pc];

            // If this is a return, don't continue
            if (IsReturn(opcode))
            {
                continue;
            }

            // If this is an unconditional jump, only follow the jump
            if (IsUnconditionalJump(opcode))
            {
                int target = GetJumpTarget(bytecodes, pc);
                if (target >= 0 && target < bytecodes.Length && !reachable.Contains(target))
                {
                    worklist.Enqueue(target);
                }
            }
            else if (IsConditionalJump(opcode))
            {
                // Conditional jump: follow both paths
                int target = GetJumpTarget(bytecodes, pc);
                if (target >= 0 && target < bytecodes.Length && !reachable.Contains(target))
                {
                    worklist.Enqueue(target);
                }

                int nextPc = AdvancePC(bytecodes, pc);
                if (nextPc < bytecodes.Length && !reachable.Contains(nextPc))
                {
                    worklist.Enqueue(nextPc);
                }
            }
            else
            {
                // Normal instruction: continue to next
                int nextPc = AdvancePC(bytecodes, pc);
                if (nextPc < bytecodes.Length && !reachable.Contains(nextPc))
                {
                    worklist.Enqueue(nextPc);
                }
            }
        }

        // Third pass: identify dead regions
        int deadStart = -1;

        for (pc = 0; pc < bytecodes.Length; pc++)
        {
            if (!reachable.Contains(pc))
            {
                if (deadStart == -1)
                {
                    deadStart = pc;
                }
            }
            else if (deadStart != -1)
            {
                analysis.DeadRegions.Add((deadStart, pc - 1));
                deadStart = -1;
            }
        }

        if (deadStart != -1)
        {
            analysis.DeadRegions.Add((deadStart, bytecodes.Length - 1));
        }

        return analysis;
    }

    private bool IsReturn(byte opcode)
    {
        return opcode >= 0x78 && opcode <= 0x7D;
    }

    private bool IsJump(byte opcode)
    {
        return IsUnconditionalJump(opcode) || IsConditionalJump(opcode);
    }

    private bool IsUnconditionalJump(byte opcode)
    {
        return (opcode >= 0x90 && opcode <= 0x97) || (opcode >= 0xA0 && opcode <= 0xA7);
    }

    private bool IsConditionalJump(byte opcode)
    {
        return (opcode >= 0x98 && opcode <= 0x9F) || (opcode >= 0xA8 && opcode <= 0xAF);
    }

    private int GetJumpTarget(byte[] bytecodes, int pc)
    {
        if (pc >= bytecodes.Length)
            return -1;

        byte opcode = bytecodes[pc];

        // Short jumps
        if (opcode >= 0x90 && opcode <= 0x9F)
        {
            int offset = (opcode & 0x07) + 1;
            return pc + offset + 1;
        }
        // Long jumps
        else if (opcode == 0xA0 && pc + 1 < bytecodes.Length)
        {
            int offset = bytecodes[pc + 1];
            return pc + offset + 2;
        }

        return -1;
    }

    private int AdvancePC(byte[] bytecodes, int pc)
    {
        if (pc >= bytecodes.Length)
            return pc;

        byte opcode = bytecodes[pc];
        pc++; // Past opcode

        // Handle extended bytecodes
        if (opcode == 0x80 && pc < bytecodes.Length)
        {
            pc++; // Skip sub-opcode
            if (pc < bytecodes.Length) pc++; // Most extended ops have 1 operand
        }
        // Handle jumps with operands
        else if (opcode == 0xA0 && pc < bytecodes.Length)
        {
            pc++; // 1 byte offset
        }

        return pc;
    }
}
