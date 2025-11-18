using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Bytecode execution handlers for the Smalltalk-80 VM.
/// This partial class contains all bytecode interpretation logic.
/// </summary>
public partial class VirtualMachine
{
    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        // DEBUG: Track bytecode execution for debugging
        // if (_activeContextCount <= 3)  // Only log first few contexts
        // {
        //     Console.WriteLine($"[BYTECODE] 0x{bytecode:X2} ({bytecode}) in method {GetLocalId(methodOOP)}");
        // }

        // Check if this is a BlockContext - they retrieve receiver from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(_contextManager.ActiveContext);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: receiver from home MethodContext (slot 5 in BlockContext is HOME, not receiver!)
            var methodContextOOP = _contextManager.FindMethodContext(_contextManager.ActiveContext);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, receiverIndex);
        }
        else
        {
            // MethodContext: receiver in instance variable
            receiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, receiverIndex);
        }

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            var valueOOP = _heapManager.GetInstanceVariable(receiverOOP, index);
            Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        // Single-Context Architecture: Uses lexicalParent chain traversal for variable access
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            if (VerboseDebug)
            {
                var contextClass = _heapManager.GetClassOOP(_contextManager.ActiveContext);
                string contextType = (contextClass == _metadata.BlockContextClassOOP) ? "BlockContext" : "MethodContext";
                Console.WriteLine($"[PUSH_TEMP] index={index}, activeContext={_contextManager.ActiveContext} ({contextType})");
            }

            var valueOOP = GetTemporaryVariable(_contextManager.ActiveContext, index);

            if (VerboseDebug)
            {
                string valueDesc = valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" :
                                   valueOOP == SpecialObjectsArray.Instance.Nil ? "nil" :
                                   $"{_heapManager.GetClassName(valueOOP)} OOP({valueOOP})";
                Console.WriteLine($"[PUSH_TEMP] Retrieved value: {valueDesc}");
                DumpStack("After PUSH_TEMP");
            }

            Push(valueOOP);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        // NOTE: Blocks are NOT stored as literals anymore (Blue Book inline bytecode approach)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
            Push(literal);
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
        //    - Symbol: First execution → resolve dictionary, lookup value, patch
        //    - Dictionary: Subsequent → direct lookup (fast path)
        // 3. Return value from Association inside dictionary
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var literalOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)
            if (literalOOP == SpecialObjectsArray.Instance.Nil)
            {
                Push(SpecialObjectsArray.Instance.Nil);
                return true;
            }

            // POOL VARIABLES: Check if this is a 2-element Array (binding metadata)
            var classOOP = _heapManager.GetClassOOP(literalOOP);
            var arrayClassOOP = _runtimeHeapAccess.GetClass("Array");

            if (classOOP == arrayClassOOP)
            {
                int arraySize = _heapManager.GetIndexedSize(literalOOP);
                if (arraySize == 2)
                {
                    // This is a pool variable literal: (#PoolName, #Key)
                    var element0 = _heapManager.GetIndexedOOP(literalOOP, 0);
                    var element1 = _heapManager.GetIndexedOOP(literalOOP, 1);

                    // Check if element 0 is a Symbol (pool name) or an OOP (already patched to dictionary)
                    var element0Class = _heapManager.GetClassOOP(element0);
                    var symbolClass = _runtimeHeapAccess.GetClass("Symbol");

                    if (element0Class == symbolClass)
                    {
                        // First execution: element0 is pool dictionary name symbol
                        // Resolve pool dictionary and patch
                        var poolValueOOP = ResolveAndPatchPoolVariable(literalOOP, methodOOP, index + 1, element0, element1);
                        Push(poolValueOOP);
                        return true;
                    }
                    else
                    {
                        // Already patched: element0 is pool dictionary OOP (fast path)
                        // Verify element0 is a Dictionary (should always be true after patching)
                        var dictionaryClassOOP = _runtimeHeapAccess.GetClass("Dictionary");
                        if (element0Class != dictionaryClassOOP)
                        {
                            throw new InvalidOperationException(
                                $"Pool variable literal has invalid patched entry. " +
                                $"Expected Dictionary OOP, got class {element0Class}");
                        }

                        // Look up element1 (key symbol) in the dictionary
                        var keySymbol = element1;
                        var patchedValueOOP = LookupInDictionary(element0, keySymbol);
                        Push(patchedValueOOP);
                        return true;
                    }
                }
            }

            // GLOBAL VARIABLES: Standard Association handling
            // Association has layout: [key, value]
            int instVarCount = _heapManager.GetInstanceVariableCount(literalOOP);

            if (instVarCount < 2)
            {
                Console.WriteLine($"[PUSH_LITERAL_VAR] ERROR: Expected Association or pool variable array, got object with {instVarCount} instance variables");
                Console.WriteLine($"  literalOOP: {literalOOP}");
                Console.WriteLine($"  classOOP: {classOOP}");
                Console.WriteLine($"  methodOOP: {methodOOP}");
                Console.WriteLine($"  literal index: {index}");

                // This might be a Symbol or other object that should be pushed directly
                Push(literalOOP);
                return true;
            }

            // Association has a fixed layout per Smalltalk-80 specification:
            // slot 0 = key, slot 1 = value
            var keyOOP = _heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            var valueOOP = _heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            Push(valueOOP);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var valueOOP = Pop();
            _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        // Single-Context Architecture: Uses lexicalParent chain traversal for variable access
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var valueOOP = Pop();
            SetTemporaryVariable(_contextManager.ActiveContext, index, valueOOP);
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
                Push(receiverOOP);
                return true;

            case Bytecode.PUSH_TRUE:
                Push(SpecialObjectsArray.Instance.True);
                return true;

            case Bytecode.PUSH_FALSE:
                Push(SpecialObjectsArray.Instance.False);
                return true;

            case Bytecode.PUSH_NIL:
                Push(SpecialObjectsArray.Instance.Nil);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                Push(SmalltalkOOP.FromSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                Push(SmalltalkOOP.FromSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                Push(SmalltalkOOP.FromSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                Push(SmalltalkOOP.FromSmallInteger(2));
                return true;

            case Bytecode.RETURN_SELF:
                // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
                Push(receiverOOP);
                return false; // Return from method

            case Bytecode.RETURN_TRUE:
                Push(SpecialObjectsArray.Instance.True);
                return false;

            case Bytecode.RETURN_FALSE:
                Push(SpecialObjectsArray.Instance.False);
                return false;

            case Bytecode.RETURN_NIL:
                Push(SpecialObjectsArray.Instance.Nil);
                return false;

            case Bytecode.RETURN_TOP:
                // Value already on stack
                return false;

            case Bytecode.RETURN_SELF_POP:
                // Blue Book: Return self from block's home MethodContext (non-local return)
                {
                    // Check if we're in a BlockContext by reading slot 5 (home field)
                    var homeOOP = _heapManager.GetInstanceVariable(
                        _contextManager.ActiveContext,
                        VMConstants.BLOCKCONTEXT_HOME_INDEX  // Slot 5
                    );

                    // If home is not nil, we're in a BlockContext
                    if (homeOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Non-local return from BlockContext to home MethodContext
                        var targetContextOOP = homeOOP;

                        // Validate home is still in sender chain (not already returned)
                        if (!_contextManager.IsInSenderChain(targetContextOOP, _contextManager.ActiveContext))
                        {
                            throw new InvalidOperationException(
                                "Block return failed: home context has already returned");
                        }

                        // Initiate non-local return to home's sender
                        _nonLocalReturnPending = true;
                        _nlrTargetContext = targetContextOOP;
                        _nlrValue = receiverOOP;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal return self
                        Push(receiverOOP);
                        return false;
                    }
                }

            case Bytecode.RETURN_TOP_POP:
                // Blue Book: Return top of stack from block's home MethodContext (non-local return)
                {
                    var returnValue = Pop();

                    // Check if we're in a BlockContext by reading slot 5 (home field)
                    var homeOOP = _heapManager.GetInstanceVariable(
                        _contextManager.ActiveContext,
                        VMConstants.BLOCKCONTEXT_HOME_INDEX  // Slot 5, NOT lexicalParent (slot 4)!
                    );

                    if (homeOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // We're in a BlockContext - perform non-local return
                        var targetContextOOP = homeOOP;

                        // Validate target is in sender chain (safety check)
                        if (!_contextManager.IsInSenderChain(targetContextOOP, _contextManager.ActiveContext))
                        {
                            throw new InvalidOperationException(
                                "Non-local return target not in sender chain - block's defining method has already returned");
                        }

                        // Initiate non-local return
                        _nonLocalReturnPending = true;
                        _nlrTargetContext = targetContextOOP;
                        _nlrValue = returnValue;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal method return
                        Push(returnValue);
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
            Push(_contextManager.ActiveContext);
            return true;
        }

        // Short jump forward (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            int distance = (bytecode - 0x90) + 1; // Jump 1-8 bytes
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();
            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Medium jump forward (0xA0)
        if (bytecode == 0xA0)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP2 = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
            int currentIp = ipOOP2.AsSmallInteger();

            byte distanceByte = bytecodes[currentIp++];
            int distance = distanceByte + 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Long jump forward (0xA1-0xA7)
        if (bytecode >= 0xA1 && bytecode <= 0xA7)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP3 = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
            int currentIp = ipOOP3.AsSmallInteger();

            int numBytes = bytecode - 0x9F; // 0xA1 => 2 bytes, 0xA2 => 3 bytes, etc.
            int distance = 0;
            for (int i = 0; i < numBytes; i++)
            {
                distance = (distance << 8) | bytecodes[currentIp++];
            }
            distance += 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Backward jumps (0xA8-0xAF) - for tail recursion optimization
        if (bytecode >= 0xA8 && bytecode <= 0xAF)
        {
            int offset = bytecode - 0xA8;  // 0xA8 => jump back 0 (nop), 0xA9 => back 1, ..., 0xAF => back 7
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP4 = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
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

            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(newIp));
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
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var receiverOOP2 = Pop();
            var outcome0 = SendMessage(receiverOOP2, selectorOOP, Array.Empty<SmalltalkOOP>());
            if (!ConsumeCallOutcome(outcome0)) return false;
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var argOOP = Pop();
            var receiverOOP2 = Pop();

            // Console.WriteLine($"[SEND1 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_contextManager.ActiveContext}");
            var outcome1 = SendMessage(receiverOOP2, selectorOOP, new[] { argOOP });
            // Console.WriteLine($"[SEND1 AFTER] Message outcome.Kind={outcome1.Kind}, context={_contextManager.ActiveContext}");
            if (!ConsumeCallOutcome(outcome1)) return false;
            // Console.WriteLine($"[SEND1 PUSHED] Pushed result onto stack");
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var arg2OOP = Pop();
            var arg1OOP = Pop();
            var receiverOOP2 = Pop();

            // Console.WriteLine($"[SEND2 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_contextManager.ActiveContext}");
            var outcome2 = SendMessage(receiverOOP2, selectorOOP, new[] { arg1OOP, arg2OOP });
            // Console.WriteLine($"[SEND2 AFTER] Message outcome.Kind={outcome2.Kind}, context={_contextManager.ActiveContext}");
            if (!ConsumeCallOutcome(outcome2)) return false;
            // Console.WriteLine($"[SEND2 PUSHED] Pushed result onto stack");
            return true;
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    /// <summary>
    /// Executes extended bytecode operations (0x80 + sub-operation byte).
    /// </summary>
    private bool ExecuteExtendedBytecode(SmalltalkOOP methodOOP)
    {
        byte[] bytecodes = GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        var ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte subOp = bytecodes[ip++];
        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var receiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, receiverIndex);

        switch (subOp)
        {
            case 0x00: // PUSH_IVAR - Push instance variable at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = _heapManager.GetInstanceVariable(receiverOOP, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x01: // STORE_IVAR - Store to instance variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x02: // POP_STORE_IVAR - Pop and store to instance variable
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    return true;
                }

            case 0x03: // PUSH_TEMP - Push temporary at any index
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = GetTemporaryVariable(_contextManager.ActiveContext, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Peek(); // Don't pop - STORE keeps value on stack
                    SetTemporaryVariable(_contextManager.ActiveContext, index, valueOOP);
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    SetTemporaryVariable(_contextManager.ActiveContext, index, valueOOP);
                    return true;
                }

            case 0x06: // PUSH_LITERAL - Push literal constant at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    Push(literal);
                    return true;
                }

            case 0x07: // PUSH_LITERAL_VAR - Push literal variable (association value or qualified pool var)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literalOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    // Check if this is an Association (unqualified variable) or Array (qualified pool variable)
                    var literalClass = GetClassName(GetClass(literalOOP));

                    if (literalClass == "Association")
                    {
                        // Unqualified variable reference: push Association value directly
                        var valueOOP = _heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

                        Push(valueOOP);
                        return true;
                    }
                    else if (literalClass == "Array")
                    {
                        // Qualified pool variable reference: #(poolDictName variableName)
                        int size = _heapManager.GetIndexedSize(literalOOP);
                        if (size == 2)
                        {
                            var poolDictNameOOP = _heapManager.GetIndexedOOP(literalOOP, 0);
                            var varNameOOP = _heapManager.GetIndexedOOP(literalOOP, 1);
                            var poolDictName = GetSymbolName(poolDictNameOOP);
                            var varName = GetSymbolName(varNameOOP);

                            if (poolDictName == null || varName == null)
                            {
                                throw new InvalidOperationException($"PUSH_LITERAL_VAR: Qualified pool variable Array must contain Symbols");
                            }

                            // Look up pool dictionary in Smalltalk SystemDictionary
                            // NOTE: LookupGlobalByName returns the VALUE, not the Association
                            var systemDictOOP = SpecialObjectsArray.Instance.Smalltalk;
                            var poolDictOOP = _runtimeHeapAccess.LookupGlobalByName(systemDictOOP, poolDictName);

                            if (poolDictOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                throw new InvalidOperationException($"Pool dictionary not found: {poolDictName}");
                            }

                            // Verify it's a Dictionary
                            var poolDictClass2 = GetClassName(GetClass(poolDictOOP));
                            if (poolDictClass2 != "Dictionary")
                            {
                                throw new InvalidOperationException(
                                    $"PUSH_LITERAL_VAR: Expected Dictionary for pool '{poolDictName}', got {poolDictClass2} (OOP={poolDictOOP})");
                            }

                            // Look up variable value in pool dictionary by variable name
                            // NOTE: LookupGlobalByName returns the VALUE, not the Association
                            var valueOOP = _runtimeHeapAccess.LookupGlobalByName(poolDictOOP, varName);

                            if (valueOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                throw new InvalidOperationException($"Variable not found in pool dictionary {poolDictName}: {varName}");
                            }

                            // Push the variable value
                            Push(valueOOP);
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
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    _heapManager.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x09: // POP_STORE_LITERAL_VAR - Pop and store to literal variable (class/global variable)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    // DIAGNOSTIC: Validate this is actually an Association
                    if (associationOOP.IsSmallInteger)
                    {
                        string? methodLocalId = GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is SmallInteger({associationOOP.AsSmallInteger()}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}");
                    }

                    var literalClass = _heapManager.GetSlot(associationOOP, 0);
                    string? literalClassName = GetClassName(literalClass);

                    // Allow "Association" or "class:Association" as valid class names
                    bool isAssociation = literalClassName == "class:Association" || literalClassName == "Association";

                    if (!isAssociation)
                    {
                        string? methodLocalId = GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is {literalClassName} (oop={associationOOP}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}. " +
                            $"This likely means the compiler generated wrong bytecode (should be POP_STORE_TEMP for temp variable).");
                    }

                    // Store the value in the Association using write-barrier (runtime mode)
                    SetAssociationValue(associationOOP, valueOOP, loaderMode: false);

                    // Add Association to classPool if it's a class variable (lazy initialization)
                    // For class-side methods, receiver IS the class object which has the classPool
                    AddAssociationToClassPoolIfNeeded(receiverOOP, associationOOP);

                    return true;
                }

            case 0x0A: // SEND_0 - Send with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0D: // SEND_N - Send with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, args);
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0E: // SEND_SUPER_0 - Send to super with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, args);
                    return ConsumeCallOutcome(outcome);
                }

            case 0x12: // POP - Pop and discard top of stack
                {
                    Pop();
                    return true;
                }

            case 0x13: // DUP - Duplicate top of stack
                {
                    int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
                    var spOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, spIndex);
                    int sp = spOOP.AsSmallInteger();
                    if (sp <= 0)
                    {
                        throw new InvalidOperationException("Stack underflow on DUP");
                    }
                    var valueOOP = _heapManager.GetIndexedOOP(_contextManager.ActiveContext, sp - 1);
                    Push(valueOOP);
                    return true;
                }

            case 0x14: // JUMP_TRUE - Pop and jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x15: // JUMP_FALSE - Pop and jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x16: // JUMP - Unconditional jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    int newIP = ip + offset;
                    // Console.WriteLine($"[JUMP] from IP={ip-2} offset={offset} to newIP={newIP}, method={GetLocalId(methodOOP)}");
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(newIP));
                    return true;
                }

            case 0x17: // JUMP_LONG_TRUE - Pop and long jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x18: // JUMP_LONG_FALSE - Pop and long jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x19: // JUMP_LONG - Unconditional long jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
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
    private bool ExecuteSendArithmetic(byte bytecode)
    {
        if (_arithmeticSelectorOOPs == null)
        {
            throw new InvalidOperationException("Arithmetic selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _arithmeticSelectorOOPs[selectorIndex];

        var argOOP = Pop();
        var receiverOOP = Pop();

        // DEBUG: Only log when @ is sent to non-SmallInteger (error case)
        if (bytecode == 187 && (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger))
        {
            string recClass = receiverOOP.IsSmallInteger ? "SmallInteger" : _heapManager.GetClassName(receiverOOP);
            string argClass = argOOP.IsSmallInteger ? "SmallInteger" : _heapManager.GetClassName(argOOP);
            string? recSymbol = recClass == "Symbol" ? GetSymbolName(receiverOOP) : null;
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
                selector == "=" || selector == "~=" ||
                selector == "<" || selector == ">" ||
                selector == "<=" || selector == ">=")
            {
                Push(resultOOP);
                return true;
            }
        }

        // Fall back to message send using cached selector OOP
        var outcome = SendMessage(receiverOOP, selectorOOP, new[] { argOOP });
        return ConsumeCallOutcome(outcome);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
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
            var argStartIndexOOP = Pop(); // Second arg (top of stack)
            var nargsOOP = Pop(); // First arg

            if (!nargsOOP.IsSmallInteger || !argStartIndexOOP.IsSmallInteger)
            {
                throw new InvalidOperationException($"blockCopy: args must be SmallIntegers, got nargs={nargsOOP}, argStartIndex={argStartIndexOOP}");
            }

            // Call primitive 80 (blockCopy:) - receiver is the active context
            var blockContextOOP = ControlPrimitives.BlockCopy(this, _contextManager.ActiveContext, new[] { nargsOOP, argStartIndexOOP });

            // Push the new BlockContext onto the stack
            Push(blockContextOOP);
            return true;
        }

        // Pop arguments
        var args = new SmalltalkOOP[argCount];

        // Pop arguments
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        var receiverOOP = Pop();

        // Handle special cases
        if (selectorIndex == 6) // ==
        {
            bool isIdentical = receiverOOP == args[0];
            Push(isIdentical ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False);
            return true;
        }

        if (selectorIndex == 7) // class
        {
            try
            {
                var classOOP = GetClass(receiverOOP);
                Push(classOOP);
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
        var outcome = SendMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil OOP if operation fails or is not supported.
    /// </summary>
    private SmalltalkOOP ExecuteIntegerArithmetic(SmalltalkOOP receiverOOP, string selector, SmalltalkOOP argOOP)
    {
        if (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger)
            return SpecialObjectsArray.Instance.Nil;

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
