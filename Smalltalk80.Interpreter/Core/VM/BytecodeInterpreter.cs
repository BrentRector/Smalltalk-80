using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Bytecode execution handlers for the Smalltalk-80 VM.
/// Extracted from VirtualMachine.Bytecodes.cs for cleaner architecture.
/// </summary>
/// <param name="heapManager">The heap manager for memory operations.</param>
/// <param name="context">The bytecode context for accessing execution state.</param>
/// <param name="runtimeHeapAccess">Runtime heap access for class lookups.</param>
/// <param name="metadata">VM metadata containing class OOPs.</param>
internal class BytecodeInterpreter(
    ISmalltalkHeapManager heapManager,
    IBytecodeContext context,
    RuntimeHeapAccessor runtimeHeapAccess,
    VMMetadata metadata) : IBytecodeInterpreter
{
    // Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _arithmeticSelectorOOPs;

    // Cached selector OOPs for special send bytecodes (0xC0-0xCF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _specialSelectorOOPs;

    /// <inheritdoc />
    public bool VerboseDebug { get; set; }

    /// <inheritdoc />
    public void InitializeSelectorCaches(SmalltalkOOP[] arithmeticSelectorOOPs, SmalltalkOOP[] specialSelectorOOPs)
    {
        _arithmeticSelectorOOPs = arithmeticSelectorOOPs;
        _specialSelectorOOPs = specialSelectorOOPs;
    }

    /// <inheritdoc />
    public bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        // Check if this is a BlockContext - they retrieve receiver from home MethodContext
        var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);
        bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;

        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: receiver from home MethodContext (slot 5 in BlockContext is HOME, not receiver!)
            var methodContextOOP = context.FindMethodContext(context.ActiveContext);
            receiverOOP = heapManager.GetInstanceVariable(methodContextOOP, receiverIndex);
        }
        else
        {
            // MethodContext: receiver in instance variable
            receiverOOP = heapManager.GetInstanceVariable(context.ActiveContext, receiverIndex);
        }

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            var valueOOP = heapManager.GetInstanceVariable(receiverOOP, index);
            context.Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            if (VerboseDebug)
            {
                var contextClass = heapManager.GetClassOOP(context.ActiveContext);
                string contextType = contextClass == metadata.BlockContextClassOOP ? "BlockContext" : "MethodContext";
                Console.WriteLine($"[PUSH_TEMP] index={index}, activeContext={context.ActiveContext} ({contextType})");
            }

            var valueOOP = context.GetTemporaryVariable(context.ActiveContext, index);

            if (VerboseDebug)
            {
                string valueDesc = valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" :
                                   valueOOP == SpecialObjectsArray.Instance.Nil ? "nil" :
                                   $"{heapManager.GetClassName(valueOOP)} OOP({valueOOP})";
                Console.WriteLine($"[PUSH_TEMP] Retrieved value: {valueDesc}");
                context.DumpStack("After PUSH_TEMP");
            }

            context.Push(valueOOP);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        // NOTE: Blocks are NOT stored as literals anymore (Blue Book inline bytecode approach)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
            context.Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        //
        // CRITICAL: This bytecode is used for BOTH pool variables AND global variables.
        // The literal frame entry type determines which:
        // - 2-element Array: Pool variable binding metadata (NOT a user array)
        // - Association object: Global variable
        //
        // User-level array literals use pushLiteral: bytecode, NOT pushLiteralVariable:,
        // so there is no risk of accidental dereferencing.
        //
        // Pool Variable Resolution Algorithm:
        // 1. Check if literal is 2-element Array
        // 2. Check element[0] type:
        //    - Symbol: First execution -> resolve dictionary, lookup value, patch
        //    - Dictionary: Subsequent -> direct lookup (fast path)
        // 3. Return value from Association inside dictionary
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var literalOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            if (literalOOP == SpecialObjectsArray.Instance.Nil)
            {
                context.Push(SpecialObjectsArray.Instance.Nil);
                return true;
            }

            // POOL VARIABLES: Check if this is a 2-element Array (binding metadata)
            var classOOP = heapManager.GetClassOOP(literalOOP);
            var arrayClassOOP = runtimeHeapAccess.GetClass("Array");

            if (classOOP == arrayClassOOP)
            {
                int arraySize = heapManager.GetIndexedSize(literalOOP);
                if (arraySize == 2)
                {
                    // This is a pool variable literal: (#PoolName, #Key)
                    var element0 = heapManager.GetIndexedOOP(literalOOP, 0);
                    var element1 = heapManager.GetIndexedOOP(literalOOP, 1);

                    // Check if element 0 is a Symbol (pool name) or an OOP (already patched to dictionary)
                    var element0Class = heapManager.GetClassOOP(element0);
                    var symbolClass = runtimeHeapAccess.GetClass("Symbol");

                    if (element0Class == symbolClass)
                    {
                        // First execution: element0 is pool dictionary name symbol
                        // Resolve pool dictionary and patch
                        var poolValueOOP = context.ResolveAndPatchPoolVariable(literalOOP, methodOOP, index + 1, element0, element1);
                        context.Push(poolValueOOP);
                        return true;
                    }
                    else
                    {
                        // Already patched: element0 is pool dictionary OOP (fast path)
                        // Verify element0 is a Dictionary (should always be true after patching)
                        var dictionaryClassOOP = runtimeHeapAccess.GetClass("Dictionary");
                        if (element0Class != dictionaryClassOOP)
                        {
                            throw new InvalidOperationException(
                                $"Pool variable literal has invalid patched entry. " +
                                $"Expected Dictionary OOP, got class {element0Class}");
                        }

                        // Look up element1 (key symbol) in the dictionary
                        var keySymbol = element1;
                        var patchedValueOOP = context.LookupInDictionary(element0, keySymbol);
                        context.Push(patchedValueOOP);
                        return true;
                    }
                }
            }

            // GLOBAL VARIABLES: Standard Association handling
            // Association has layout: [key, value]
            int instVarCount = heapManager.GetInstanceVariableCount(literalOOP);

            if (instVarCount < 2)
            {
                Console.WriteLine($"[PUSH_LITERAL_VAR] ERROR: Expected Association or pool variable array, got object with {instVarCount} instance variables");
                Console.WriteLine($"  literalOOP: {literalOOP}");
                Console.WriteLine($"  classOOP: {classOOP}");
                Console.WriteLine($"  methodOOP: {methodOOP}");
                Console.WriteLine($"  literal index: {index}");

                // This might be a Symbol or other object that should be pushed directly
                context.Push(literalOOP);
                return true;
            }

            // Association has a fixed layout per Smalltalk-80 specification:
            // slot 0 = key, slot 1 = value
            var keyOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            var valueOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            context.Push(valueOOP);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var valueOOP = context.Pop();
            heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var valueOOP = context.Pop();
            context.SetTemporaryVariable(context.ActiveContext, index, valueOOP);
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                context.Push(receiverOOP);
                return true;

            case Bytecode.PUSH_TRUE:
                context.Push(SpecialObjectsArray.Instance.True);
                return true;

            case Bytecode.PUSH_FALSE:
                context.Push(SpecialObjectsArray.Instance.False);
                return true;

            case Bytecode.PUSH_NIL:
                context.Push(SpecialObjectsArray.Instance.Nil);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                context.Push(SmalltalkOOP.FromSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                context.Push(SmalltalkOOP.FromSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                context.Push(SmalltalkOOP.FromSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                context.Push(SmalltalkOOP.FromSmallInteger(2));
                return true;

            case Bytecode.RETURN_SELF:
                context.Push(receiverOOP);
                return false; // Return from method

            case Bytecode.RETURN_TRUE:
                context.Push(SpecialObjectsArray.Instance.True);
                return false;

            case Bytecode.RETURN_FALSE:
                context.Push(SpecialObjectsArray.Instance.False);
                return false;

            case Bytecode.RETURN_NIL:
                context.Push(SpecialObjectsArray.Instance.Nil);
                return false;

            case Bytecode.RETURN_TOP:
                // Value already on stack
                return false;

            case Bytecode.RETURN_SELF_POP:
                // Blue Book: Return self from block's home MethodContext (non-local return)
                {
                    // Properly check if we're in a BlockContext by checking the class
                    var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);

                    if (contextClassOOP == metadata.BlockContextClassOOP)
                    {
                        // We're in a BlockContext - get home and perform non-local return
                        var homeOOP = heapManager.GetInstanceVariable(
                            context.ActiveContext,
                            VMConstants.BLOCKCONTEXT_HOME_INDEX
                        );

                        // Non-local return from BlockContext to home MethodContext
                        var targetContextOOP = homeOOP;

                        // Validate home is still in sender chain (not already returned)
                        if (!context.IsInSenderChain(targetContextOOP, context.ActiveContext))
                        {
                            throw new InvalidOperationException(
                                "Block return failed: home context has already returned");
                        }

                        // Initiate non-local return to home's sender
                        context.NonLocalReturnPending = true;
                        context.NLRTargetContext = targetContextOOP;
                        context.NLRValue = receiverOOP;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal return self
                        context.Push(receiverOOP);
                        return false;
                    }
                }

            case Bytecode.RETURN_TOP_POP:
                // Blue Book: Return top of stack from block's home MethodContext (non-local return)
                {
                    var returnValue = context.Pop();

                    // Properly check if we're in a BlockContext by checking the class
                    var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);

                    if (contextClassOOP == metadata.BlockContextClassOOP)
                    {
                        // We're in a BlockContext - get home and perform non-local return
                        var homeOOP = heapManager.GetInstanceVariable(
                            context.ActiveContext,
                            VMConstants.BLOCKCONTEXT_HOME_INDEX
                        );

                        var targetContextOOP = homeOOP;

                        // Validate target is in sender chain (safety check)
                        if (!context.IsInSenderChain(targetContextOOP, context.ActiveContext))
                        {
                            throw new InvalidOperationException(
                                "Non-local return target not in sender chain - block's defining method has already returned");
                        }

                        // Initiate non-local return
                        context.NonLocalReturnPending = true;
                        context.NLRTargetContext = targetContextOOP;
                        context.NLRValue = returnValue;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal method return
                        context.Push(returnValue);
                        return false;
                    }
                }

            case Bytecode.RESERVED_RETURN:
                // No operation / reserved (0x7F)
                return true;
        }

        // Extended bytecodes (0x80)
        if (bytecode == 0x80)
        {
            return ExecuteExtendedBytecode(methodOOP);
        }

        // Push active context (0x89) - used for blockCopy: primitive
        if (bytecode == (byte)Bytecode.PUSH_ACTIVE_CONTEXT)
        {
            context.Push(context.ActiveContext);
            return true;
        }

        // Short jump forward (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            int distance = (bytecode - 0x90) + 1; // Jump 1-8 bytes
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();
            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Medium jump forward (0xA0)
        if (bytecode == 0xA0)
        {
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP2 = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP2.AsSmallInteger();

            byte distanceByte = bytecodes[currentIp++];
            int distance = distanceByte + 1; // Distance is encoded as (distance - 1)

            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Long jump forward (0xA1-0xA7)
        if (bytecode >= 0xA1 && bytecode <= 0xA7)
        {
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP3 = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP3.AsSmallInteger();

            int numBytes = bytecode - 0x9F; // 0xA1 => 2 bytes, 0xA2 => 3 bytes, etc.
            int distance = 0;
            for (int i = 0; i < numBytes; i++)
            {
                distance = (distance << 8) | bytecodes[currentIp++];
            }

            distance += 1; // Distance is encoded as (distance - 1)

            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Backward jumps (0xA8-0xAF) - for tail recursion optimization
        if (bytecode >= 0xA8 && bytecode <= 0xAF)
        {
            int offset = bytecode - 0xA8;  // 0xA8 => jump back 0 (nop), 0xA9 => back 1, ..., 0xAF => back 7
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP4 = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP4.AsSmallInteger();

            // Jump backward by offset bytes
            // For tail recursion, compiler emits STORE_TEMP for each argument, then JUMP_BACKWARD
            // The offset should point back to the method start (after argument setup)
            int newIp = currentIp - offset;

            if (newIp < 0)
            {
                throw new InvalidOperationException(
                    $"Backward jump offset {offset} would jump before method start " +
                    $"(currentIp={currentIp}, newIp={newIp})");
            }

            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(newIp));
            return true;
        }

        // Send arithmetic messages (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            return ExecuteSendArithmetic(bytecode);
        }

        // Send special messages (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            return ExecuteSendSpecial(bytecode);
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend0(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send0Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var receiverOOP2 = context.Pop();
            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, []);
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var argOOP = context.Pop();
            var receiverOOP2 = context.Pop();

            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, [argOOP]);
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var arg2OOP = context.Pop();
            var arg1OOP = context.Pop();
            var receiverOOP2 = context.Pop();

            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, [arg1OOP, arg2OOP]);
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    /// <summary>
    /// Executes extended bytecode operations (0x80 + sub-operation byte).
    /// </summary>
    /// <param name="methodOOP">The method OOP containing the bytecodes.</param>
    /// <returns>True to continue execution, false to exit the bytecode loop.</returns>
    private bool ExecuteExtendedBytecode(SmalltalkOOP methodOOP)
    {
        byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte subOp = bytecodes[ip++];
        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var receiverOOP = heapManager.GetInstanceVariable(context.ActiveContext, receiverIndex);

        switch (subOp)
        {
            case 0x00: // PUSH_IVAR - Push instance variable at any index
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = heapManager.GetInstanceVariable(receiverOOP, index);
                    context.Push(valueOOP);
                    return true;
                }

            case 0x01: // STORE_IVAR - Store to instance variable without popping
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Pop();
                    heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    context.Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x02: // POP_STORE_IVAR - Pop and store to instance variable
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Pop();
                    heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    return true;
                }

            case 0x03: // PUSH_TEMP - Push temporary at any index
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.GetTemporaryVariable(context.ActiveContext, index);
                    context.Push(valueOOP);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Peek(); // Don't pop - STORE keeps value on stack
                    context.SetTemporaryVariable(context.ActiveContext, index, valueOOP);
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Pop();
                    context.SetTemporaryVariable(context.ActiveContext, index, valueOOP);
                    return true;
                }

            case 0x06: // PUSH_LITERAL - Push literal constant at any index
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literal = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    context.Push(literal);
                    return true;
                }

            case 0x07: // PUSH_LITERAL_VAR - Push literal variable (association value or qualified pool var)
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literalOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    // Check if this is an Association (unqualified variable) or Array (qualified pool variable)
                    var literalClass = context.GetClassName(context.GetClass(literalOOP));

                    if (literalClass == "Association")
                    {
                        // Unqualified variable reference: push Association value directly
                        var valueOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

                        context.Push(valueOOP);
                        return true;
                    }
                    else if (literalClass == "Array")
                    {
                        // Qualified pool variable reference: #(poolDictName variableName)
                        int size = heapManager.GetIndexedSize(literalOOP);
                        if (size == 2)
                        {
                            var poolDictNameOOP = heapManager.GetIndexedOOP(literalOOP, 0);
                            var varNameOOP = heapManager.GetIndexedOOP(literalOOP, 1);
                            var poolDictName = context.GetSymbolName(poolDictNameOOP);
                            var varName = context.GetSymbolName(varNameOOP);

                            if (poolDictName == null || varName == null)
                            {
                                throw new InvalidOperationException($"PUSH_LITERAL_VAR: Qualified pool variable Array must contain Symbols");
                            }

                            // Look up pool dictionary in Smalltalk SystemDictionary
                            // NOTE: LookupGlobalByName returns the VALUE, not the Association
                            var systemDictOOP = SpecialObjectsArray.Instance.Smalltalk;
                            var poolDictOOP = runtimeHeapAccess.LookupGlobalByName(systemDictOOP, poolDictName);

                            if (poolDictOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                throw new InvalidOperationException($"Pool dictionary not found: {poolDictName}");
                            }

                            // Verify it's a Dictionary
                            var poolDictClass2 = context.GetClassName(context.GetClass(poolDictOOP));
                            if (poolDictClass2 != "Dictionary")
                            {
                                throw new InvalidOperationException(
                                    $"PUSH_LITERAL_VAR: Expected Dictionary for pool '{poolDictName}', got {poolDictClass2} (OOP={poolDictOOP})");
                            }

                            // Look up variable value in pool dictionary by variable name
                            // NOTE: LookupGlobalByName returns the VALUE, not the Association
                            var valueOOP = runtimeHeapAccess.LookupGlobalByName(poolDictOOP, varName);

                            if (valueOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                throw new InvalidOperationException($"Variable not found in pool dictionary {poolDictName}: {varName}");
                            }

                            // Push the variable value
                            context.Push(valueOOP);
                            return true;
                        }
                        else
                        {
                            throw new InvalidOperationException($"PUSH_LITERAL_VAR: Array literal has size {size}, expected 2 for qualified pool variable");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"PUSH_LITERAL_VAR: Expected Association or Array, got {literalClass}");
                    }
                }

            case 0x08: // STORE_LITERAL_VAR - Store to literal variable without popping
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Pop();
                    var associationOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    heapManager.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
                    context.Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x09: // POP_STORE_LITERAL_VAR - Pop and store to literal variable (class/global variable)
                {
                    byte index = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = context.Pop();
                    var associationOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    // DIAGNOSTIC: Validate this is actually an Association
                    if (associationOOP.IsSmallInteger)
                    {
                        string? methodLocalId = context.GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is SmallInteger({associationOOP.AsSmallInteger()}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}");
                    }

                    var literalClass = heapManager.GetSlot(associationOOP, 0);
                    string? literalClassName = context.GetClassName(literalClass);

                    // Allow "Association" or "class:Association" as valid class names
                    bool isAssociation = literalClassName == "class:Association" || literalClassName == "Association";

                    if (!isAssociation)
                    {
                        string? methodLocalId = context.GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is {literalClassName} (oop={associationOOP}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}. " +
                            $"This likely means the compiler generated wrong bytecode (should be POP_STORE_TEMP for temp variable).");
                    }

                    // Store the value in the Association
                    heapManager.SetInstanceVariableWithBarrier(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);

                    // Add Association to classPool if it's a class variable (lazy initialization)
                    // For class-side methods, receiver IS the class object which has the classPool
                    context.AddAssociationToClassPoolIfNeeded(receiverOOP, associationOOP);

                    return true;
                }

            case 0x0A: // SEND_0 - Send with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = context.Pop();
                    return context.SendMessageAndContinue(receiver, selectorOOP, []);
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = context.Pop();
                    var receiver = context.Pop();
                    return context.SendMessageAndContinue(receiver, selectorOOP, [arg]);
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = context.Pop();
                    var arg1 = context.Pop();
                    var receiver = context.Pop();
                    return context.SendMessageAndContinue(receiver, selectorOOP, [arg1, arg2]);
                }

            case 0x0D: // SEND_N - Send with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = context.Pop();
                    }

                    var receiver = context.Pop();
                    return context.SendMessageAndContinue(receiver, selectorOOP, args);
                }

            case 0x0E: // SEND_SUPER_0 - Send to super with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = context.Pop();
                    return context.SendSuperMessageAndContinue(receiver, selectorOOP, []);
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = context.Pop();
                    var receiver = context.Pop();
                    return context.SendSuperMessageAndContinue(receiver, selectorOOP, [arg]);
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = context.Pop();
                    var arg1 = context.Pop();
                    var receiver = context.Pop();
                    return context.SendSuperMessageAndContinue(receiver, selectorOOP, [arg1, arg2]);
                }

            case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = context.Pop();
                    }

                    var receiver = context.Pop();
                    return context.SendSuperMessageAndContinue(receiver, selectorOOP, args);
                }

            case 0x12: // POP - Pop and discard top of stack
                {
                    context.Pop();
                    return true;
                }

            case 0x13: // DUP - Duplicate top of stack
                {
                    int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
                    var spOOP = heapManager.GetInstanceVariable(context.ActiveContext, spIndex);
                    int sp = spOOP.AsSmallInteger();
                    if (sp <= 0)
                    {
                        throw new InvalidOperationException("Stack underflow on DUP");
                    }

                    var valueOOP = heapManager.GetIndexedOOP(context.ActiveContext, sp - 1);
                    context.Push(valueOOP);
                    return true;
                }

            case 0x14: // JUMP_TRUE - Pop and jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = context.Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }

                    return true;
                }

            case 0x15: // JUMP_FALSE - Pop and jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = context.Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }

                    return true;
                }

            case 0x16: // JUMP - Unconditional jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    int newIP = ip + offset;
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(newIP));
                    return true;
                }

            case 0x17: // JUMP_LONG_TRUE - Pop and long jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = context.Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }

                    return true;
                }

            case 0x18: // JUMP_LONG_FALSE - Pop and long jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = context.Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }

                    return true;
                }

            case 0x19: // JUMP_LONG - Unconditional long jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    return true;
                }

            default:
                throw new NotImplementedException($"Extended bytecode 0x80 0x{subOp:X2} not implemented");
        }
    }

    /// <summary>
    /// Executes arithmetic message send (0xB0-0xBF).
    /// Returns true to continue execution, false if context switched or method returned.
    /// </summary>
    /// <param name="bytecode">The arithmetic bytecode to execute.</param>
    /// <returns>True to continue execution, false if context switched.</returns>
    private bool ExecuteSendArithmetic(byte bytecode)
    {
        if (_arithmeticSelectorOOPs == null)
        {
            throw new InvalidOperationException("Arithmetic selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _arithmeticSelectorOOPs[selectorIndex];

        var argOOP = context.Pop();
        var receiverOOP = context.Pop();

        // DEBUG: Only log when @ is sent to non-SmallInteger (error case)
        if (bytecode == 187 && (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger))
        {
            string recClass = receiverOOP.IsSmallInteger ? "SmallInteger" : heapManager.GetClassName(receiverOOP);
            string argClass = argOOP.IsSmallInteger ? "SmallInteger" : heapManager.GetClassName(argOOP);
            string? recSymbol = recClass == "Symbol" ? context.GetSymbolName(receiverOOP) : null;
            Console.WriteLine($"[@ ERROR] receiver={receiverOOP} ({recClass}{(recSymbol != null ? $" #{recSymbol}" : "")}), arg={argOOP} ({argClass})");
        }

        // Try primitive arithmetic for SmallIntegers
        if (receiverOOP.IsSmallInteger && argOOP.IsSmallInteger)
        {
            // For primitive arithmetic, we need the selector name for ExecuteIntegerArithmetic
            string selector = selectorIndex switch
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
                _ => throw new InvalidOperationException($"Unknown arithmetic bytecode: 0x{bytecode:X2}")
            };

            var resultOOP = ExecuteIntegerArithmetic(receiverOOP, selector, argOOP);
            if (resultOOP != SpecialObjectsArray.Instance.Nil ||
                selector is "=" or "~=" or "<" or ">" or "<=" or ">=")
            {
                context.Push(resultOOP);
                return true;
            }
        }

        // Fall back to message send using cached selector OOP
        return context.SendMessageAndContinue(receiverOOP, selectorOOP, [argOOP]);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
    /// <param name="bytecode">The special bytecode to execute.</param>
    /// <returns>True to continue execution, false if context switched.</returns>
    private bool ExecuteSendSpecial(byte bytecode)
    {
        if (_specialSelectorOOPs == null)
        {
            throw new InvalidOperationException("Special selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _specialSelectorOOPs[selectorIndex];

        // Determine argument count from selector index
        int argCount = selectorIndex switch
        {
            0 => 1,  // at:
            1 => 2,  // at:put:
            2 => 0,  // size
            3 => 0,  // next
            4 => 1,  // nextPut:
            5 => 0,  // atEnd
            6 => 1,  // ==
            7 => 0,  // class
            8 => 1,  // blockCopy: (Blue Book standard - 1 arg: nargs only)
            9 => 0,  // value
            10 => 1, // value:
            11 => 1, // do:
            12 => 0, // new
            13 => 1, // new:
            14 => 0, // x
            15 => 0, // y
            _ => throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}")
        };

        // Handle blockCopy: BEFORE popping arguments - it's a context primitive, not a message send
        if (selectorIndex == 8) // blockCopy:
        {
            // blockCopy: is unique - it operates on the active context, not a receiver on the stack
            // VM EXTENSION: Now accepts 2 arguments (nargs, argStartIndex) for unified temp frame
            // Stack layout (top to bottom): argStartIndex, nargs
            var argStartIndexOOP = context.Pop(); // Second arg (top of stack)
            var nargsOOP = context.Pop(); // First arg

            if (!nargsOOP.IsSmallInteger || !argStartIndexOOP.IsSmallInteger)
            {
                throw new InvalidOperationException($"blockCopy: args must be SmallIntegers, got nargs={nargsOOP}, argStartIndex={argStartIndexOOP}");
            }

            // Call primitive 80 (blockCopy:) using new IPrimitive static helper
            // Need: heapManager, activeContext, blockContextClassOOP, nargs, argStartIndex, currentPC
            int currentPC = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_PC_INDEX).AsSmallInteger();
            var result = ControlPrimitives.BlockCopy.CreateBlockContext(
                heapManager,
                context.ActiveContext,
                metadata.BlockContextClassOOP,
                nargsOOP,
                argStartIndexOOP,
                currentPC);

            if (!result.Success)
            {
                throw new InvalidOperationException("blockCopy: primitive failed unexpectedly");
            }

            // Push the new BlockContext onto the stack
            context.Push(result.Value);
            return true;
        }

        // Pop arguments
        var args = new SmalltalkOOP[argCount];

        // Pop arguments
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }

        var receiverOOP = context.Pop();

        // Handle special cases
        if (selectorIndex == 6) // ==
        {
            bool isIdentical = receiverOOP == args[0];
            context.Push(isIdentical ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False);
            return true;
        }

        if (selectorIndex == 7) // class
        {
            try
            {
                var classOOP = context.GetClass(receiverOOP);
                context.Push(classOOP);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] in 'class' special selector");
                Console.WriteLine($"   receiverOOP: {receiverOOP}");
                Console.WriteLine($"   IsSmallInteger: {receiverOOP.IsSmallInteger}");
                Console.WriteLine($"   Exception: {ex.Message}");
                throw;
            }
        }

        // Fall back to message send using cached selector OOP
        return context.SendMessageAndContinue(receiverOOP, selectorOOP, args);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil OOP if operation fails or is not supported.
    /// </summary>
    /// <param name="receiverOOP">The receiver SmallInteger OOP.</param>
    /// <param name="selector">The arithmetic selector name.</param>
    /// <param name="argOOP">The argument SmallInteger OOP.</param>
    /// <returns>The result OOP, or nil if the operation failed.</returns>
    private static SmalltalkOOP ExecuteIntegerArithmetic(SmalltalkOOP receiverOOP, string selector, SmalltalkOOP argOOP)
    {
        if (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger)
        {
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiverOOP.AsSmallInteger();
        int b = argOOP.AsSmallInteger();

        try
        {
            return selector switch
            {
                "+" => SmalltalkOOP.FromSmallInteger(a + b),
                "-" => SmalltalkOOP.FromSmallInteger(a - b),
                "*" => SmalltalkOOP.FromSmallInteger(a * b),
                "/" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : SpecialObjectsArray.Instance.Nil,
                "\\" => b != 0 ? SmalltalkOOP.FromSmallInteger(a % b) : SpecialObjectsArray.Instance.Nil,
                "//" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : SpecialObjectsArray.Instance.Nil,
                "<" => a < b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                ">" => a > b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "<=" => a <= b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                ">=" => a >= b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "=" => a == b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "~=" => a != b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "bitAnd:" => SmalltalkOOP.FromSmallInteger(a & b),
                "bitOr:" => SmalltalkOOP.FromSmallInteger(a | b),
                "bitShift:" => b >= 0 ? SmalltalkOOP.FromSmallInteger(a << b) : SmalltalkOOP.FromSmallInteger(a >> (-b)),
                _ => SpecialObjectsArray.Instance.Nil
            };
        }
        catch
        {
            return SpecialObjectsArray.Instance.Nil;
        }
    }
}
