using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Control-Flow-Graph-based stack depth validator using worklist propagation.
/// Validates bytecode arrays for:
/// - All bytes in range [0, 255]
/// - Stack discipline (no underflow/overflow) using CFG analysis
/// - Operand structure (extended bytecodes have required operands)
/// </summary>
public class BytecodeValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        // Validate all methods
        foreach (var method in bundle.Methods)
        {
            ValidateMethodBytecode(method, bundle, result);
        }

        // Validate all init expressions
        foreach (var initExpr in bundle.InitExpressions)
        {
            ValidateInitExpressionBytecode(initExpr, bundle, result);
        }

        return result;
    }

    private void ValidateMethodBytecode(MethodDefinition method, ColdStartBundle bundle, ValidationResult result)
    {
        var location = method.LocalId;
        var bytecodes = ConvertToByteArray(method.Bytecodes);

        // Check all bytecodes are valid
        for (int i = 0; i < bytecodes.Length; i++)
        {
            if (bytecodes[i] < 0 || bytecodes[i] > 255)
            {
                result.AddError("Bytecode",
                    $"Invalid bytecode at index {i}: {bytecodes[i]} (must be 0-255)",
                    location);
            }
        }

        // Track stack errors to avoid cascading operand errors
        int errorCountBeforeStack = result.Errors.Count;

        // Validate stack discipline using CFG
        ValidateStackDisciplineCFG(bytecodes, location, result);

        bool stackIsValid = (result.Errors.Count == errorCountBeforeStack);

        // Validate operand indices (only if stack is valid - avoids cascading errors)
        if (stackIsValid)
        {
            ValidateOperandIndices(bytecodes, method, bundle, location, result);
        }

        // Validate operand structure
        ValidateOperandStructure(bytecodes, method.Literals.Count, location, result);
    }

    private void ValidateInitExpressionBytecode(InitExpression initExpr, ColdStartBundle bundle, ValidationResult result)
    {
        var location = initExpr.LocalId;
        var bytecodes = ConvertToByteArray(initExpr.Bytecodes);

        // Check all bytecodes are valid
        for (int i = 0; i < bytecodes.Length; i++)
        {
            if (bytecodes[i] < 0 || bytecodes[i] > 255)
            {
                result.AddError("Bytecode",
                    $"Invalid bytecode at index {i}: {bytecodes[i]} (must be 0-255)",
                    location);
            }
        }

        // Validate stack discipline using CFG
        ValidateStackDisciplineCFG(bytecodes, location, result);

        // Validate operand structure
        ValidateOperandStructure(bytecodes, initExpr.Literals.Count, location, result);
    }

    private byte[] ConvertToByteArray(int[] intArray)
    {
        byte[] result = new byte[intArray.Length];
        for (int i = 0; i < intArray.Length; i++)
        {
            result[i] = (byte)intArray[i];
        }
        return result;
    }

    /// <summary>
    /// CFG-based stack validation using worklist propagation.
    /// Based on Smalltalk80.Interpreter.Core.Bytecodes.BytecodeStackValidator
    /// </summary>
    private void ValidateStackDisciplineCFG(byte[] bytecodes, string location, ValidationResult result)
    {
        if (bytecodes == null || bytecodes.Length == 0)
            return;

        var errors = new List<string>();

        try
        {
            // Step 1-2: Build CFG
            var cfg = BuildCFG(bytecodes);

            // Step 3-4: Worklist propagation (collects errors)
            var stackDepthMap = PropagateStackDepths(bytecodes, cfg, errors);

            // Step 6: Final checks - validate return instructions
            if (errors.Count == 0) // Only validate returns if no earlier errors
            {
                ValidateReturnInstructions(bytecodes, stackDepthMap, errors);
            }

            // Report all errors
            foreach (var error in errors)
            {
                result.AddError("Bytecode", error, location);
            }
        }
        catch (Exception ex)
        {
            result.AddError("Bytecode", $"Internal validation error: {ex.Message}", location);
        }
    }

    /// <summary>
    /// Step 1-2: Parse bytecode and build control flow graph.
    /// Returns: CFG as adjacency list (PC -> list of successor PCs)
    /// </summary>
    private Dictionary<int, List<int>> BuildCFG(byte[] bytecodes)
    {
        var cfg = new Dictionary<int, List<int>>();
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            int currentPC = pc;
            byte opcode = bytecodes[pc];

            // Advance PC past current instruction
            int nextPC = AdvancePC(bytecodes, pc);

            var successors = new List<int>();

            // Identify control flow
            if (IsReturn(opcode))
            {
                // Return: no successors (end of path)
                cfg[currentPC] = successors;
            }
            else if (IsUnconditionalJump(opcode, bytecodes, currentPC, out int target))
            {
                // Unconditional jump: one successor (target)
                successors.Add(target);
                cfg[currentPC] = successors;
            }
            else if (IsConditionalJump(opcode, bytecodes, currentPC, out target))
            {
                // Conditional jump: two successors (target and fall-through)
                successors.Add(target);
                if (nextPC < bytecodes.Length)
                    successors.Add(nextPC);
                cfg[currentPC] = successors;
            }
            else
            {
                // Normal instruction: fall through
                if (nextPC < bytecodes.Length)
                    successors.Add(nextPC);
                cfg[currentPC] = successors;
            }

            pc = nextPC;
        }

        return cfg;
    }

    /// <summary>
    /// Step 3-4: Worklist propagation to compute stack depth at each PC.
    /// Collects errors instead of throwing exceptions.
    /// </summary>
    private Dictionary<int, int?> PropagateStackDepths(byte[] bytecodes, Dictionary<int, List<int>> cfg, List<string> errors)
    {
        // stackDepth[pc] = depth at start of instruction at pc (null = unvisited)
        var stackDepth = new Dictionary<int, int?>();
        foreach (int pc in cfg.Keys)
            stackDepth[pc] = null;

        // Track predecessors for error reporting
        var predecessors = new Dictionary<int, int>();

        // Worklist: (PC, stack depth at that PC)
        var worklist = new Queue<(int pc, int depth)>();

        // Initialize: entry point at PC 0 has depth 0
        worklist.Enqueue((0, 0));
        stackDepth[0] = 0;

        while (worklist.Count > 0)
        {
            var (pc, depthAtPC) = worklist.Dequeue();

            if (pc >= bytecodes.Length)
                continue;

            byte opcode = bytecodes[pc];

            // Compute stack effect of this instruction
            int effect = ComputeStackEffect(opcode, bytecodes, pc);
            int depthAfter = depthAtPC + effect;

            // Check for underflow
            if (depthAfter < 0)
            {
                string predInfo = predecessors.ContainsKey(pc) ? $" (reached from PC {predecessors[pc]})" : "";
                errors.Add($"Stack underflow at pc={pc}, opcode=0x{opcode:X2} ({(Bytecode)opcode}): " +
                          $"depth before={depthAtPC}, effect={effect}, depth after={depthAfter}{predInfo}");
                continue; // Skip this path, continue with other paths
            }

            // Propagate to successors
            if (cfg.TryGetValue(pc, out var successors))
            {
                foreach (int succ in successors)
                {
                    if (succ >= bytecodes.Length)
                        continue;

                    if (stackDepth.TryGetValue(succ, out int? existingDepth))
                    {
                        if (existingDepth == null)
                        {
                            // First visit to this PC
                            stackDepth[succ] = depthAfter;
                            predecessors[succ] = pc;  // Track where we came from
                            worklist.Enqueue((succ, depthAfter));
                        }
                        else if (existingDepth.Value != depthAfter)
                        {
                            // Join point with inconsistent depth!
                            errors.Add($"Stack depth mismatch at join point PC {succ}: " +
                                      $"incoming depth {depthAfter} from PC {pc}, " +
                                      $"but previously saw depth {existingDepth.Value}");
                            // Continue anyway - don't add to worklist again
                        }
                        // else: already visited with same depth, skip
                    }
                    else
                    {
                        // Successor not in CFG (shouldn't happen if CFG is complete)
                        stackDepth[succ] = depthAfter;
                        worklist.Enqueue((succ, depthAfter));
                    }
                }
            }
        }

        return stackDepth;
    }

    /// <summary>
    /// Advances PC past the current instruction (including operands).
    /// </summary>
    private int AdvancePC(byte[] bytecodes, int pc)
    {
        if (pc >= bytecodes.Length)
            return pc;

        byte opcode = bytecodes[pc];
        pc++; // Past opcode

        // Extended bytecodes (0x80)
        if (opcode == 0x80)
        {
            if (pc >= bytecodes.Length)
                return pc;

            byte subOp = bytecodes[pc++];

            // Sub-operations with operands
            if (subOp <= 0x09)
            {
                // Push/Store operations: 1 operand byte
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x0A && subOp <= 0x0C)
            {
                // Send 0/1/2: 1 operand byte (selector index)
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp == 0x0D || subOp == 0x11)
            {
                // Send N, Send Super N: 2 operand bytes (selector, argcount)
                if (pc < bytecodes.Length) pc++;
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x0E && subOp <= 0x10)
            {
                // Send Super 0/1/2: 1 operand byte
                if (pc < bytecodes.Length) pc++;
            }
            else if (subOp >= 0x14 && subOp <= 0x19)
            {
                // Jump operations: 2 operand bytes (offset)
                if (pc < bytecodes.Length) pc++;
                if (pc < bytecodes.Length) pc++;
            }
            // POP (0x12), DUP (0x13): no operands

            return pc;
        }

        // Long jumps (0xA0-0xA7): 1 operand byte
        if (opcode >= 0xA0 && opcode <= 0xA7)
        {
            if (pc < bytecodes.Length) pc++; // Offset byte
            return pc;
        }

        // All other bytecodes: no operands
        return pc;
    }

    /// <summary>
    /// Returns true if opcode is a return instruction.
    /// </summary>
    private bool IsReturn(byte opcode)
    {
        return opcode >= 0x78 && opcode <= 0x7F; // RETURN_SELF through RETURN_FALSE_FROM_METHOD
    }

    /// <summary>
    /// Returns true and outputs target if opcode is an unconditional jump.
    /// </summary>
    private bool IsUnconditionalJump(byte opcode, byte[] bytecodes, int pc, out int target)
    {
        target = -1;

        // Short unconditional forward jump (0x90-0x97)
        if (opcode >= 0x90 && opcode <= 0x97)
        {
            int offset = (opcode & 0x07) + 1;
            target = pc + 1 + offset;
            return true;
        }

        // Long unconditional forward jump (0xA0-0xA3)
        if (opcode >= 0xA0 && opcode <= 0xA3)
        {
            if (pc + 1 >= bytecodes.Length)
                return false;

            int highBits = (opcode & 0x07) << 8;
            int lowBits = bytecodes[pc + 1];
            int offset = highBits | lowBits;
            target = pc + 2 + offset + 1; // Blue Book: jump distance = offset + 1
            return true;
        }

        // Extended unconditional jump (0x80 0x16 or 0x80 0x19)
        if (opcode == 0x80 && pc + 1 < bytecodes.Length)
        {
            byte subOp = bytecodes[pc + 1];
            if (subOp == 0x16 || subOp == 0x19)
            {
                if (pc + 3 >= bytecodes.Length)
                    return false;

                int highByte = bytecodes[pc + 2];
                int lowByte = bytecodes[pc + 3];
                int offset = (highByte << 8) | lowByte;
                target = pc + 4 + offset + 1; // Blue Book: jump distance = offset + 1
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true and outputs target if opcode is a conditional jump.
    /// </summary>
    private bool IsConditionalJump(byte opcode, byte[] bytecodes, int pc, out int target)
    {
        target = -1;

        // Short conditional forward jump if false (0x98-0x9F)
        if (opcode >= 0x98 && opcode <= 0x9F)
        {
            int offset = (opcode & 0x07) + 1;
            target = pc + 1 + offset;
            return true;
        }

        // Long conditional forward jump if false (0xA4-0xA7)
        if (opcode >= 0xA4 && opcode <= 0xA7)
        {
            if (pc + 1 >= bytecodes.Length)
                return false;

            int highBits = (opcode & 0x03) << 8;
            int lowBits = bytecodes[pc + 1];
            int offset = highBits | lowBits;
            target = pc + 2 + offset + 1; // Blue Book: jump distance = offset + 1
            return true;
        }

        // Extended conditional jumps (0x80 0x14/0x15/0x17/0x18)
        if (opcode == 0x80 && pc + 1 < bytecodes.Length)
        {
            byte subOp = bytecodes[pc + 1];
            if (subOp == 0x14 || subOp == 0x15 || subOp == 0x17 || subOp == 0x18)
            {
                if (pc + 3 >= bytecodes.Length)
                    return false;

                int highByte = bytecodes[pc + 2];
                int lowByte = bytecodes[pc + 3];
                int offset = (highByte << 8) | lowByte;
                target = pc + 4 + offset + 1; // Blue Book: jump distance = offset + 1
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Computes stack effect: positive = push, negative = pop, zero = neutral.
    /// </summary>
    private int ComputeStackEffect(byte opcode, byte[] bytecodes, int pc)
    {
        // Push operations: +1
        if ((opcode >= 0x00 && opcode <= 0x0F) ||  // Push ivar
            (opcode >= 0x10 && opcode <= 0x1F) ||  // Push temp
            (opcode >= 0x20 && opcode <= 0x3F) ||  // Push literal const
            (opcode >= 0x40 && opcode <= 0x5F) ||  // Push literal var
            (opcode >= 0x70 && opcode <= 0x77))    // Push special
            return +1;

        // Pop and store: -1
        if ((opcode >= 0x60 && opcode <= 0x67) ||  // Pop store ivar
            (opcode >= 0x68 && opcode <= 0x6F))    // Pop store temp
            return -1;

        // Returns: 0 (internal stack effect, not caller's stack)
        if (opcode >= 0x78 && opcode <= 0x7F)
            return 0;

        // Extended bytecodes
        if (opcode == 0x80)
        {
            if (pc + 1 >= bytecodes.Length)
                return 0;

            byte subOp = bytecodes[pc + 1];
            return subOp switch
            {
                0x00 or 0x03 or 0x06 or 0x07 => +1,  // Push (ivar/temp/literal/litvar)
                0x01 or 0x04 or 0x08 => 0,           // Store without pop
                0x02 or 0x05 or 0x09 => -1,          // Pop and store
                0x0A or 0x0E => 0,                   // Send 0 args: -1 rcv, +1 result
                0x0B or 0x0F => -1,                  // Send 1 arg: -2, +1 result
                0x0C or 0x10 => -2,                  // Send 2 args: -3, +1 result
                0x0D => GetSendNEffect(bytecodes, pc + 3),  // Send N: argCount at pc+3 (FIXED!)
                0x11 => GetSendNEffect(bytecodes, pc + 3),  // Send Super N: argCount at pc+3 (FIXED!)
                0x12 => -1,                          // POP
                0x13 => +1,                          // DUP
                0x14 or 0x15 or 0x17 or 0x18 => -1,  // Conditional jump (pops condition)
                0x16 or 0x19 => 0,                   // Unconditional jump
                _ => 0
            };
        }

        // Push active context (for block creation)
        if (opcode == 0x89)
            return +1;

        // Jumps
        if (opcode >= 0x90 && opcode <= 0x97)  // Unconditional short jump
            return 0;
        if (opcode >= 0x98 && opcode <= 0x9F)  // Conditional short jump
            return -1;
        if (opcode >= 0xA0 && opcode <= 0xA3)  // Unconditional long jump
            return 0;
        if (opcode >= 0xA4 && opcode <= 0xA7)  // Conditional long jump
            return -1;

        // Send arithmetic (0xB0-0xBF): binary operator
        if (opcode >= 0xB0 && opcode <= 0xBF)
            return -1; // -2 (rcv+arg), +1 result

        // Send special (0xC0-0xCF)
        if (opcode >= 0xC0 && opcode <= 0xCF)
        {
            int specialIndex = opcode & 0x0F;
            int argCount = GetSpecialArgCount(specialIndex);
            return -(argCount + 1) + 1; // -(rcv+args) + result = -argCount
        }

        // Send literal 0/1/2 args
        if (opcode >= 0xD0 && opcode <= 0xDF) return 0;   // 0 args: -1 rcv, +1 result = 0
        if (opcode >= 0xE0 && opcode <= 0xEF) return -1;  // 1 arg: -2, +1 result = -1
        if (opcode >= 0xF0 && opcode <= 0xFF) return -2;  // 2 args: -3, +1 result = -2

        return 0; // Unknown/unhandled
    }

    private int GetSendNEffect(byte[] bytecodes, int pcAtArgCount)
    {
        if (pcAtArgCount >= bytecodes.Length)
            return 0;

        int argCount = bytecodes[pcAtArgCount];
        return -argCount; // -(rcv + args) + result = -(1 + argCount) + 1
    }

    private int GetSpecialArgCount(int index)
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
    /// Step 6: Validates return instructions have correct stack depth.
    /// RETURN_TOP should have exactly 1 item on stack (the return value).
    /// RETURN_SELF, RETURN_NIL, etc. don't use the stack (implicit return value).
    /// </summary>
    private void ValidateReturnInstructions(byte[] bytecodes, Dictionary<int, int?> stackDepth, List<string> errors)
    {
        for (int pc = 0; pc < bytecodes.Length; pc++)
        {
            byte opcode = bytecodes[pc];

            // RETURN_TOP (0x7C) - pops 1 value from stack and returns it
            if (opcode == 0x7C)
            {
                if (stackDepth.TryGetValue(pc, out int? depth) && depth.HasValue)
                {
                    if (depth.Value != 1)
                    {
                        errors.Add($"RETURN_TOP at PC {pc} with invalid stack depth {depth.Value}, expected 1");
                    }
                }
            }

            // RETURN_TOP_FROM_BLOCK (0x7D) - block return
            if (opcode == 0x7D)
            {
                if (stackDepth.TryGetValue(pc, out int? depth) && depth.HasValue)
                {
                    if (depth.Value != 1)
                    {
                        errors.Add($"RETURN_TOP_FROM_BLOCK at PC {pc} with invalid stack depth {depth.Value}, expected 1");
                    }
                }
            }

            // RETURN_TOP_FROM_METHOD (0x7E) - non-local return
            if (opcode == 0x7E)
            {
                if (stackDepth.TryGetValue(pc, out int? depth) && depth.HasValue)
                {
                    if (depth.Value != 1)
                    {
                        errors.Add($"RETURN_TOP_FROM_METHOD at PC {pc} with invalid stack depth {depth.Value}, expected 1");
                    }
                }
            }

            // RETURN_SELF (0x78), RETURN_TRUE (0x79), RETURN_FALSE (0x7A), RETURN_NIL (0x7B)
            // These don't consume stack - they return implicit values
            // No validation needed for these
        }
    }

    private void ValidateOperandStructure(byte[] bytecodes, int literalCount, string location, ValidationResult result)
    {
        int pc = 0;

        while (pc < bytecodes.Length)
        {
            byte opcode = bytecodes[pc++];

            if (opcode == 0x80) // Extended bytecode
            {
                if (pc >= bytecodes.Length)
                {
                    result.AddError("Bytecode",
                        $"Extended bytecode 0x80 at pc={pc - 1} missing sub-operation byte",
                        location);
                    return;
                }

                byte subOp = bytecodes[pc++];

                if (RequiresOperandByte(subOp))
                {
                    if (pc >= bytecodes.Length)
                    {
                        result.AddError("Bytecode",
                            $"Extended bytecode 0x80 0x{subOp:X2} at pc={pc - 2} missing operand byte",
                            location);
                        return;
                    }
                    pc++; // Skip operand
                }
            }
            else if (opcode >= 0xA0 && opcode <= 0xAF) // Long jumps need offset byte
            {
                if (pc >= bytecodes.Length)
                {
                    result.AddError("Bytecode",
                        $"Long jump 0x{opcode:X2} at pc={pc - 1} missing offset byte",
                        location);
                    return;
                }
                pc++; // Skip offset
            }
        }
    }

    private bool RequiresOperandByte(byte subOp)
    {
        return subOp <= 0x19; // Most extended ops through 0x19 have operand bytes
    }

    /// <summary>
    /// Validates that all bytecode operands reference valid indices.
    /// </summary>
    private void ValidateOperandIndices(byte[] bytecodes, MethodDefinition method, ColdStartBundle bundle, string location, ValidationResult result)
    {
        // Get instance variable count for the method's home class
        // NOTE: For blocks (selectorIndex == -1), we skip instance variable validation
        // because blocks access instance variables through their home context at runtime,
        // not through their own class. Set instVarCount to Int32.MaxValue to disable validation.
        int instVarCount;
        if (method.SelectorIndex == -1)
        {
            // Block: disable instance variable index validation
            instVarCount = Int32.MaxValue;
        }
        else
        {
            // Regular method: validate against class instance variables
            instVarCount = GetInstanceVariableCount(method.HomeClassLocalId, bundle);
        }

        int tempCount = method.Header.TempCount ?? 0;
        int argCount = method.Header.ArgCount ?? 0;
        int literalCount = method.Literals.Count;

        // Call the operand validator
        var operandResult = OperandValidator.Validate(
            bytecodes,
            literalCount,
            tempCount,
            argCount,
            instVarCount);

        if (!operandResult.IsValid && operandResult.ErrorMessage != null)
        {
            result.AddError("Bytecode", operandResult.ErrorMessage, location);
        }
    }

    /// <summary>
    /// Computes total instance variable count for a class (including inherited).
    /// </summary>
    private int GetInstanceVariableCount(string classLocalId, ColdStartBundle bundle)
    {
        // Find the class definition
        var classDef = bundle.Classes.FirstOrDefault(c => c.LocalId == classLocalId);
        if (classDef == null)
            return 0;

        int totalCount = classDef.InstVarNames.Length;

        // Walk up inheritance chain using superclass name
        int? currentSuperNameIndex = classDef.SuperNameIndex;
        var visited = new HashSet<string> { classLocalId }; // Prevent infinite loops

        while (currentSuperNameIndex.HasValue)
        {
            string superName = bundle.Symbols[currentSuperNameIndex.Value];

            // Find superclass by name
            var superclass = bundle.Classes.FirstOrDefault(c =>
                bundle.Symbols[c.NameIndex] == superName && !c.IsMetaclass);

            if (superclass == null || !visited.Add(superclass.LocalId))
                break;

            totalCount += superclass.InstVarNames.Length;
            currentSuperNameIndex = superclass.SuperNameIndex;
        }

        return totalCount;
    }
}
