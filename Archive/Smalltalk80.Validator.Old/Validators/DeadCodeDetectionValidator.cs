using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Detects dead/unreachable code in bytecode:
/// - Unreachable bytecode not targeted by any jumps
/// - Code after unconditional returns
/// - Orphaned jump targets
/// - Large unreachable regions indicating compiler bugs
/// </summary>
public class DeadCodeDetectionValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        
        int methodsWithDeadCode = 0;
        int totalDeadBytes = 0;
        int methodsWithReturnDeadCode = 0;
        
        // Check all methods
        foreach (var method in bundle.Methods)
        {
            var deadCodeResult = AnalyzeMethod(method);
            
            if (deadCodeResult.HasDeadCode)
            {
                methodsWithDeadCode++;
                totalDeadBytes += deadCodeResult.DeadByteCount;
                
                ReportDeadCode(result, method, deadCodeResult);
            }
            
            if (deadCodeResult.HasCodeAfterReturn)
            {
                methodsWithReturnDeadCode++;
            }
        }
        
        // Check init expressions
        foreach (var initExpr in bundle.InitExpressions)
        {
            var deadCodeResult = AnalyzeInitExpression(initExpr);
            
            if (deadCodeResult.HasDeadCode)
            {
                totalDeadBytes += deadCodeResult.DeadByteCount;
                ReportDeadCodeInit(result, initExpr, deadCodeResult);
            }
        }
        
        // Add summary
        if (methodsWithDeadCode == 0)
        {
            result.AddInfo($"✓ No dead code detected in {bundle.Methods.Length} methods");
        }
        else
        {
            result.AddInfo($"Dead code analysis: {methodsWithDeadCode} methods with dead code, " +
                          $"{totalDeadBytes} total dead bytes, {methodsWithReturnDeadCode} with code after return");
        }
        
        return result;
    }
    
    private class DeadCodeAnalysis
    {
        public bool HasDeadCode { get; set; }
        public bool HasCodeAfterReturn { get; set; }
        public int DeadByteCount { get; set; }
        public List<(int Start, int End)> DeadRegions { get; } = new();
        public List<int> UnconditionalReturns { get; } = new();
    }
    
    private DeadCodeAnalysis AnalyzeMethod(MethodDefinition method)
    {
        return AnalyzeBytecode(method.Bytecodes);
    }
    
    private DeadCodeAnalysis AnalyzeInitExpression(InitExpression initExpr)
    {
        return AnalyzeBytecode(initExpr.Bytecodes);
    }
    
    private DeadCodeAnalysis AnalyzeBytecode(int[] bytecodeInts)
    {
        var analysis = new DeadCodeAnalysis();
        
        if (bytecodeInts == null || bytecodeInts.Length == 0)
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
        var unconditionalReturns = new List<int>();
        
        // First pass: identify all jump targets and returns
        int pc = 0;
        while (pc < bytecodes.Length)
        {
            byte opcode = bytecodes[pc];
            int currentPc = pc;
            
            // Check for returns
            if (IsReturn(opcode))
            {
                unconditionalReturns.Add(currentPc);
            }
            
            // Check for jumps and record targets
            if (IsJump(opcode))
            {
                int target = GetJumpTarget(bytecodes, currentPc);
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
        
        // Also start from all jump targets (they might be exception handlers)
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
        var deadRegions = new List<(int Start, int End)>();
        int deadStart = -1;
        
        for (pc = 0; pc < bytecodes.Length; pc++)
        {
            if (!reachable.Contains(pc))
            {
                if (deadStart == -1)
                {
                    deadStart = pc;
                }
                analysis.DeadByteCount++;
            }
            else if (deadStart != -1)
            {
                deadRegions.Add((deadStart, pc - 1));
                deadStart = -1;
            }
        }
        
        // Handle dead code at end
        if (deadStart != -1)
        {
            deadRegions.Add((deadStart, bytecodes.Length - 1));
        }
        
        // Check for code after unconditional returns
        foreach (var returnPc in unconditionalReturns)
        {
            int nextPc = AdvancePC(bytecodes, returnPc);
            if (nextPc < bytecodes.Length)
            {
                // Check if there's reachable code after this return
                bool hasCodeAfter = false;
                for (int i = nextPc; i < bytecodes.Length; i++)
                {
                    if (!jumpTargets.Contains(i))
                    {
                        // This is code after return that's not a jump target
                        hasCodeAfter = true;
                        break;
                    }
                }
                
                if (hasCodeAfter)
                {
                    analysis.HasCodeAfterReturn = true;
                }
            }
        }
        
        analysis.HasDeadCode = deadRegions.Count > 0;
        analysis.DeadRegions.AddRange(deadRegions);
        analysis.UnconditionalReturns.AddRange(unconditionalReturns);
        
        return analysis;
    }
    
    private bool IsReturn(byte opcode)
    {
        // Return opcodes from Blue Book
        return opcode >= 0x78 && opcode <= 0x7D; // All return variants
    }
    
    private bool IsJump(byte opcode)
    {
        return IsUnconditionalJump(opcode) || IsConditionalJump(opcode);
    }
    
    private bool IsUnconditionalJump(byte opcode)
    {
        // Unconditional jumps
        return (opcode >= 0x90 && opcode <= 0x97) ||  // Short jump forward
               (opcode >= 0xA0 && opcode <= 0xA7);     // Long jump forward
    }
    
    private bool IsConditionalJump(byte opcode)
    {
        // Conditional jumps
        return (opcode >= 0x98 && opcode <= 0x9F) ||  // Jump on false
               (opcode >= 0xA8 && opcode <= 0xAF);     // Jump on true
    }
    
    private int GetJumpTarget(byte[] bytecodes, int pc)
    {
        if (pc >= bytecodes.Length)
            return -1;
        
        byte opcode = bytecodes[pc];
        
        // Short jumps (offset in opcode)
        if (opcode >= 0x90 && opcode <= 0x97)
        {
            int offset = (opcode & 0x07) + 1;
            return pc + offset + 1; // +1 for opcode
        }
        else if (opcode >= 0x98 && opcode <= 0x9F)
        {
            int offset = (opcode & 0x07) + 1;
            return pc + offset + 1;
        }
        // Medium/long jumps (offset in following bytes)
        else if (opcode == 0xA0)
        {
            if (pc + 1 < bytecodes.Length)
            {
                int offset = bytecodes[pc + 1];
                return pc + offset + 2; // +2 for opcode and offset byte
            }
        }
        else if (opcode >= 0xA1 && opcode <= 0xA7)
        {
            int numBytes = (opcode - 0x9F);
            if (pc + numBytes < bytecodes.Length)
            {
                int offset = 0;
                for (int i = 1; i <= numBytes; i++)
                {
                    offset = (offset << 8) | bytecodes[pc + i];
                }
                return pc + offset + numBytes + 1;
            }
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
        if (opcode == 0x80)
        {
            if (pc >= bytecodes.Length)
                return pc;
            
            byte subOp = bytecodes[pc++];
            
            // Extended operations with operands
            if (subOp <= 0x09)
            {
                // Single operand byte
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x0A && subOp <= 0x0C)
            {
                // Single operand byte
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp == 0x0D || subOp == 0x11)
            {
                // Two operand bytes
                if (pc < bytecodes.Length) pc++;
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x0E && subOp <= 0x10)
            {
                // Single operand byte
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x14 && subOp <= 0x19)
            {
                // Two operand bytes (jump offsets)
                if (pc < bytecodes.Length) pc++;
                if (pc < bytecodes.Length) pc++;
            }
        }
        // Handle jumps with operands
        else if (opcode == 0xA0)
        {
            // Medium jump: 1 byte offset
            if (pc < bytecodes.Length) pc++;
        }
        else if (opcode >= 0xA1 && opcode <= 0xA7)
        {
            // Long jump: N byte offset
            int numBytes = (opcode - 0x9F);
            for (int i = 0; i < numBytes && pc < bytecodes.Length; i++)
            {
                pc++;
            }
        }
        
        return pc;
    }
    
    private void ReportDeadCode(ValidationResult result, MethodDefinition method, DeadCodeAnalysis analysis)
    {
        if (analysis.DeadRegions.Count > 0)
        {
            // Report significant dead code regions
            foreach (var region in analysis.DeadRegions)
            {
                int deadBytes = region.End - region.Start + 1;
                
                if (deadBytes >= 10)
                {
                    // Large dead region - likely a compiler bug
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Method '{method.LocalId}' has {deadBytes} bytes of unreachable code at PC {region.Start}-{region.End}",
                        new DiagnosticLocation("methods", method.LocalId, Pc: region.Start),
                        "Large unreachable regions indicate compiler bugs");
                }
                else if (deadBytes >= 3)
                {
                    // Small dead region - might be intentional padding
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Info,
                        $"Method '{method.LocalId}' has {deadBytes} bytes of unreachable code at PC {region.Start}",
                        new DiagnosticLocation("methods", method.LocalId, Pc: region.Start),
                        "Consider removing unreachable code");
                }
            }
        }
        
        if (analysis.HasCodeAfterReturn)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Warning,
                $"Method '{method.LocalId}' has code after unconditional return",
                new DiagnosticLocation("methods", method.LocalId),
                "Code after return is unreachable");
        }
    }
    
    private void ReportDeadCodeInit(ValidationResult result, InitExpression initExpr, DeadCodeAnalysis analysis)
    {
        if (analysis.DeadRegions.Count > 0)
        {
            foreach (var region in analysis.DeadRegions)
            {
                int deadBytes = region.End - region.Start + 1;
                
                if (deadBytes >= 5)
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Init expression '{initExpr.LocalId}' has {deadBytes} bytes of unreachable code",
                        new DiagnosticLocation("initExpressions", initExpr.LocalId, Pc: region.Start),
                        "Unreachable code in initialization");
                }
            }
        }
    }
}