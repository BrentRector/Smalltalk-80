using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.VM.Primitives;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// The Smalltalk-80 Virtual Machine - handles bytecode execution, message sending, and method dispatch.
/// Uses SmalltalkHeap for object storage and LayoutResolver for dynamic field access.
/// </summary>
public class VirtualMachine : IPrimitiveContext
{
    private readonly SmalltalkHeap _heap;
    private readonly LayoutResolver _layoutResolver;

    // Active context (current MethodContext being executed)
    private int _activeContextId;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    public VirtualMachine(SmalltalkHeap heap, LayoutResolver layoutResolver)
    {
        _heap = heap;
        _layoutResolver = layoutResolver;
    }

    /// <summary>
    /// Executes a method with the given receiver and arguments.
    /// Returns the result of the execution.
    /// </summary>
    public int Execute(int methodId, int receiverId, int[] arguments)
    {
        // Parse method header to check for quick forms
        var header = ParseMethodHeader(methodId);

        // Quick-return-self optimization (flags=5)
        if (header.IsQuickReturnSelf)
        {
            return receiverId;
        }

        // Quick-return-field optimization (flags=6)
        if (header.IsQuickReturnField)
        {
            return _heap.GetField(receiverId, header.QuickReturnFieldIndex);
        }

        // Create a MethodContext for this execution
        _activeContextId = CreateMethodContext(methodId, receiverId, arguments);

        // Execute bytecodes until we return
        return ExecuteBytecodeLoop();
    }

    /// <summary>
    /// Creates a MethodContext object for executing a method.
    /// </summary>
    private int CreateMethodContext(int methodId, int receiverId, int[] arguments)
    {
        // Parse method header using correct Smalltalk-80 format
        var header = ParseMethodHeader(methodId);

        // Calculate stack size (args + temps + execution stack)
        // Use large frame (48) or small frame (16) based on needsLargeFrame flag
        int executionStackSize = header.NeedsLargeFrame ? 48 : 16;
        int stackSize = header.NumArgs + header.NumTemps + executionStackSize;

        // Allocate MethodContext with indexed fields for stack
        int contextId = _heap.AllocateRawObject(
            classId: 0, // TODO: Get MethodContext class ID
            format: SmalltalkHeap.ObjectFormat.PointerIndexed,
            fixedSize: 7, // sender, instructionPointer, stackPointer, method, receiver, closure, (reserved)
            indexedSize: stackSize
        );

        // Get field indices using LayoutResolver
        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");
        int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackPointer");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        // Initialize MethodContext fields
        _heap.SetField(contextId, senderIndex, GlobalRegistry.NilObjectId);
        _heap.SetField(contextId, ipIndex, EncodeSmallInteger(0)); // Start at PC 0
        _heap.SetField(contextId, spIndex, EncodeSmallInteger(header.NumArgs + header.NumTemps)); // Stack starts after args+temps
        _heap.SetField(contextId, methodIndex, methodId);
        _heap.SetField(contextId, receiverIndex, receiverId);

        // Store arguments in indexed fields
        for (int i = 0; i < arguments.Length && i < header.NumArgs; i++)
        {
            _heap.SetIndexedField(contextId, i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heap.SetIndexedField(contextId, i, GlobalRegistry.NilObjectId);
        }

        return contextId;
    }

    /// <summary>
    /// Main bytecode execution loop.
    /// </summary>
    private int ExecuteBytecodeLoop()
    {
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");

        int methodId = _heap.GetField(_activeContextId, methodIndex);
        byte[] bytecodes = _heap.GetMethodBytecodes(methodId);

        int ip = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

            // Update IP
            _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

            // Execute the bytecode
            bool continueExecution = ExecuteBytecode(bytecode, methodId);

            if (!continueExecution)
            {
                // Method returned, pop the result and return it
                return Pop();
            }

            // Re-read IP in case bytecode modified it (jumps)
            ip = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));
        }

        // If we fall off the end, return self
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        return _heap.GetField(_activeContextId, receiverIndex);
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, int methodId)
    {
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        int receiverId = _heap.GetField(_activeContextId, receiverIndex);

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            int value = _heap.GetField(receiverId, index);
            Push(value);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);
            int value = _heap.GetIndexedField(_activeContextId, index);
            Push(value);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            int literal = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header
            Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            int associationId = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header

            // Association has layout: [key, value]
            int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
            int value = _heap.GetField(associationId, valueIndex);
            Push(value);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            int value = Pop();
            _heap.SetField(receiverId, index, value);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            int value = Pop();
            _heap.SetIndexedField(_activeContextId, index, value);
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                Push(receiverId);
                return true;

            case Bytecode.PUSH_TRUE:
                Push(GlobalRegistry.TrueObjectId);
                return true;

            case Bytecode.PUSH_FALSE:
                Push(GlobalRegistry.FalseObjectId);
                return true;

            case Bytecode.PUSH_NIL:
                Push(GlobalRegistry.NilObjectId);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                Push(EncodeSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                Push(EncodeSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                Push(EncodeSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                Push(EncodeSmallInteger(2));
                return true;

            case Bytecode.RETURN_SELF:
                Push(receiverId);
                return false; // Return from method

            case Bytecode.RETURN_TRUE:
                Push(GlobalRegistry.TrueObjectId);
                return false;

            case Bytecode.RETURN_FALSE:
                Push(GlobalRegistry.FalseObjectId);
                return false;

            case Bytecode.RETURN_NIL:
                Push(GlobalRegistry.NilObjectId);
                return false;

            case Bytecode.RETURN_TOP:
                // Value already on stack
                return false;

            case Bytecode.RETURN_SELF_POP:
                // Return self from block context to caller's caller (0x7D)
                // Variant of RETURN_SELF for block contexts
                Push(receiverId);
                return false; // TODO: Implement proper block return (non-local return)

            case Bytecode.RETURN_TOP_POP:
                // Pop and return from block context to caller's caller (0x7E)
                // Variant of RETURN_TOP for block contexts
                // Value already on stack
                return false; // TODO: Implement proper block return (non-local return)

            case Bytecode.RESERVED_RETURN:
                // No operation / reserved (0x7F)
                return true;
        }

        // Extended bytecodes (0x80)
        if (bytecode == 0x80)
        {
            return ExecuteExtendedBytecode(methodId);
        }

        // Push active context (0x89) - used for block creation
        if (bytecode == 0x89)
        {
            Push(_activeContextId);
            return true;
        }

        // Short jump forward (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            int distance = (bytecode - 0x90) + 1; // Jump 1-8 bytes
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");
            int currentIp = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));
            _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(currentIp + distance));
            return true;
        }

        // Medium jump forward (0xA0)
        if (bytecode == 0xA0)
        {
            byte[] bytecodes = _heap.GetMethodBytecodes(methodId);
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");
            int currentIp = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));

            byte distanceByte = bytecodes[currentIp++];
            int distance = distanceByte + 1; // Distance is encoded as (distance - 1)

            _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(currentIp + distance));
            return true;
        }

        // Long jump forward (0xA1-0xA7)
        if (bytecode >= 0xA1 && bytecode <= 0xA7)
        {
            byte[] bytecodes = _heap.GetMethodBytecodes(methodId);
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");
            int currentIp = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));

            int numBytes = bytecode - 0x9F; // 0xA1 => 2 bytes, 0xA2 => 3 bytes, etc.
            int distance = 0;
            for (int i = 0; i < numBytes; i++)
            {
                distance = (distance << 8) | bytecodes[currentIp++];
            }
            distance += 1; // Distance is encoded as (distance - 1)

            _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(currentIp + distance));
            return true;
        }

        // Send arithmetic messages (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            ExecuteSendArithmetic(bytecode);
            return true;
        }

        // Send special messages (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            ExecuteSendSpecial(bytecode);
            return true;
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend0(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send0Index(bytecode);
            int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header

            int receiver = Pop();
            int result = SendMessage(receiver, selectorId, Array.Empty<int>());
            Push(result);
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header

            int arg = Pop();
            int receiver = Pop();
            int result = SendMessage(receiver, selectorId, new[] { arg });
            Push(result);
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header

            int arg2 = Pop();
            int arg1 = Pop();
            int receiver = Pop();
            int result = SendMessage(receiver, selectorId, new[] { arg1, arg2 });
            Push(result);
            return true;
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    /// <summary>
    /// Executes extended bytecode operations (0x80 + sub-operation byte).
    /// </summary>
    private bool ExecuteExtendedBytecode(int methodId)
    {
        byte[] bytecodes = _heap.GetMethodBytecodes(methodId);
        int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "instructionPointer");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        int ip = DecodeSmallInteger(_heap.GetField(_activeContextId, ipIndex));
        byte subOp = bytecodes[ip++];
        _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

        int receiverId = _heap.GetField(_activeContextId, receiverIndex);

        switch (subOp)
        {
            case 0x00: // PUSH_IVAR - Push instance variable at any index
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = _heap.GetField(receiverId, index);
                    Push(value);
                    return true;
                }

            case 0x01: // STORE_IVAR - Store to instance variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    _heap.SetField(receiverId, index, value);
                    Push(value); // Leave value on stack
                    return true;
                }

            case 0x02: // POP_STORE_IVAR - Pop and store to instance variable
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    _heap.SetField(receiverId, index, value);
                    return true;
                }

            case 0x03: // PUSH_TEMP - Push temporary at any index
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = _heap.GetIndexedField(_activeContextId, index);
                    Push(value);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    _heap.SetIndexedField(_activeContextId, index, value);
                    Push(value); // Leave value on stack
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    _heap.SetIndexedField(_activeContextId, index, value);
                    return true;
                }

            case 0x06: // PUSH_LITERAL - Push literal constant at any index
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int literal = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header
                    Push(literal);
                    return true;
                }

            case 0x07: // PUSH_LITERAL_VAR - Push literal variable (association value)
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int associationId = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header
                    int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
                    int value = _heap.GetField(associationId, valueIndex);
                    Push(value);
                    return true;
                }

            case 0x08: // STORE_LITERAL_VAR - Store to literal variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    int associationId = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header
                    int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
                    _heap.SetField(associationId, valueIndex, value);
                    Push(value); // Leave value on stack
                    return true;
                }

            case 0x09: // POP_STORE_LITERAL_VAR - Pop and store to literal variable
                {
                    byte index = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int value = Pop();
                    int associationId = _heap.GetMethodLiteral(methodId, index + 1); // +1 to skip header
                    int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
                    _heap.SetField(associationId, valueIndex, value);
                    return true;
                }

            case 0x0A: // SEND_0 - Send with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int receiver = Pop();
                    int result = SendMessage(receiver, selectorId, Array.Empty<int>());
                    Push(result);
                    return true;
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int arg = Pop();
                    int receiver = Pop();
                    int result = SendMessage(receiver, selectorId, new[] { arg });
                    Push(result);
                    return true;
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int arg2 = Pop();
                    int arg1 = Pop();
                    int receiver = Pop();
                    int result = SendMessage(receiver, selectorId, new[] { arg1, arg2 });
                    Push(result);
                    return true;
                }

            case 0x0D: // SEND_N - Send with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header

                    int[] args = new int[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    int receiver = Pop();
                    int result = SendMessage(receiver, selectorId, args);
                    Push(result);
                    return true;
                }

            case 0x0E: // SEND_SUPER_0 - Send to super with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    int result = SendMessage(receiver, selectorId, Array.Empty<int>());
                    Push(result);
                    return true;
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int arg = Pop();
                    int receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    int result = SendMessage(receiver, selectorId, new[] { arg });
                    Push(result);
                    return true;
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header
                    int arg2 = Pop();
                    int arg1 = Pop();
                    int receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    int result = SendMessage(receiver, selectorId, new[] { arg1, arg2 });
                    Push(result);
                    return true;
                }

            case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));
                    int selectorId = _heap.GetMethodLiteral(methodId, selectorIndex + 1); // +1 to skip header

                    int[] args = new int[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    int receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    int result = SendMessage(receiver, selectorId, args);
                    Push(result);
                    return true;
                }

            case 0x12: // POP - Pop and discard top of stack
                {
                    Pop();
                    return true;
                }

            case 0x13: // DUP - Duplicate top of stack
                {
                    int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackPointer");
                    int sp = DecodeSmallInteger(_heap.GetField(_activeContextId, spIndex));
                    if (sp <= 0)
                    {
                        throw new InvalidOperationException("Stack underflow on DUP");
                    }
                    int value = _heap.GetIndexedField(_activeContextId, sp - 1);
                    Push(value);
                    return true;
                }

            case 0x14: // JUMP_TRUE - Pop and jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    int value = Pop();

                    if (value == GlobalRegistry.TrueObjectId)
                    {
                        _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x15: // JUMP_FALSE - Pop and jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    int value = Pop();

                    if (value == GlobalRegistry.FalseObjectId)
                    {
                        _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x16: // JUMP - Unconditional jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    return true;
                }

            case 0x17: // JUMP_LONG_TRUE - Pop and long jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    int value = Pop();

                    if (value == GlobalRegistry.TrueObjectId)
                    {
                        _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x18: // JUMP_LONG_FALSE - Pop and long jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    int value = Pop();

                    if (value == GlobalRegistry.FalseObjectId)
                    {
                        _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x19: // JUMP_LONG - Unconditional long jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heap.SetField(_activeContextId, ipIndex, EncodeSmallInteger(ip + offset));
                    return true;
                }

            default:
                throw new NotImplementedException($"Extended bytecode 0x80 0x{subOp:X2} not implemented");
        }
    }

    /// <summary>
    /// Executes arithmetic message send (0xB0-0xBF).
    /// </summary>
    private void ExecuteSendArithmetic(byte bytecode)
    {
        string selector = (bytecode & 0x0F) switch
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

        int arg = Pop();
        int receiver = Pop();

        // Try primitive arithmetic for SmallIntegers
        if (IsSmallInteger(receiver) && IsSmallInteger(arg))
        {
            int result = ExecuteIntegerArithmetic(receiver, selector, arg);
            if (result != GlobalRegistry.NilObjectId ||
                selector == "=" || selector == "~=" ||
                selector == "<" || selector == ">" ||
                selector == "<=" || selector == ">=")
            {
                Push(result);
                return;
            }
        }

        // Fall back to message send
        int selectorId = _heap.InternSymbol(selector);
        int sendResult = SendMessage(receiver, selectorId, new[] { arg });
        Push(sendResult);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
    private void ExecuteSendSpecial(byte bytecode)
    {
        string[] specialSelectors = new[]
        {
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        };

        int selectorIndex = bytecode & 0x0F;
        if (selectorIndex >= specialSelectors.Length)
        {
            throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}");
        }

        string selector = specialSelectors[selectorIndex];
        int argCount = selector.Count(c => c == ':');

        // Pop arguments
        int[] args = new int[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        int receiver = Pop();

        // Handle special cases
        if (selector == "==")
        {
            bool isIdentical = receiver == args[0];
            Push(isIdentical ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
            return;
        }

        if (selector == "class")
        {
            int classId = _heap.GetClass(receiver);
            Push(classId);
            return;
        }

        // Fall back to message send
        int selectorId = _heap.InternSymbol(selector);
        int result = SendMessage(receiver, selectorId, args);
        Push(result);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil if operation fails or is not supported.
    /// </summary>
    private int ExecuteIntegerArithmetic(int receiverId, string selector, int argId)
    {
        if (!IsSmallInteger(receiverId) || !IsSmallInteger(argId))
            return GlobalRegistry.NilObjectId;

        int a = DecodeSmallInteger(receiverId);
        int b = DecodeSmallInteger(argId);

        try
        {
            return selector switch
            {
                "+" => EncodeSmallInteger(a + b),
                "-" => EncodeSmallInteger(a - b),
                "*" => EncodeSmallInteger(a * b),
                "/" => b != 0 ? EncodeSmallInteger(a / b) : GlobalRegistry.NilObjectId,
                "\\\\" => b != 0 ? EncodeSmallInteger(a % b) : GlobalRegistry.NilObjectId,
                "//" => b != 0 ? EncodeSmallInteger(a / b) : GlobalRegistry.NilObjectId,
                "<" => a < b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                ">" => a > b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                "<=" => a <= b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                ">=" => a >= b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                "=" => a == b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                "~=" => a != b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId,
                "bitAnd:" => EncodeSmallInteger(a & b),
                "bitOr:" => EncodeSmallInteger(a | b),
                "bitShift:" => b >= 0 ? EncodeSmallInteger(a << b) : EncodeSmallInteger(a >> (-b)),
                _ => GlobalRegistry.NilObjectId
            };
        }
        catch
        {
            return GlobalRegistry.NilObjectId;
        }
    }

    /// <summary>
    /// Sends a message to a receiver.
    /// Looks up the method in the receiver's class and executes it.
    /// </summary>
    private int SendMessage(int receiverId, int selectorId, int[] args)
    {
        // Look up the method in the receiver's class
        int classId = _heap.GetClass(receiverId);
        var methodInfo = _heap.GetMethod(classId, _heap.GetSymbolName(selectorId)!);

        if (methodInfo == null)
        {
            throw new InvalidOperationException(
                $"Method not found: {_heap.GetSymbolName(selectorId)} in class {_heap.GetClass(receiverId)}");
        }

        // Check if the method has a primitive
        int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodId);
        if (primitiveNumber > 0)
        {
            // Convert receiver and args to SObjectRef for primitive execution
            SObjectRef receiver = SObjectRef.FromObjectPointer(receiverId);
            SObjectRef[] sobjectArgs = new SObjectRef[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                sobjectArgs[i] = SObjectRef.FromObjectPointer(args[i]);
            }

            // Try to execute the primitive
            SObjectRef primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiver, sobjectArgs);

            if (_primitiveSuccess)
            {
                // Primitive succeeded - convert result back to int
                return primitiveResult.IsSmallInteger
                    ? (int)primitiveResult.AsSmallInteger()
                    : (int)primitiveResult.AsObjectPointer();
            }

            // Primitive failed - execute method bytecodes as fallback
        }

        // Execute the method
        return Execute(methodInfo.MethodId, receiverId, args);
    }

    /// <summary>
    /// Parses a CompiledMethod header word according to the Smalltalk-80 specification.
    ///
    /// Header format (16 bits):
    ///   Bits 0-5   (6 bits): numLiterals (0-63)
    ///   Bit  6     (1 bit):  needsLargeFrame flag
    ///   Bits 7-11  (5 bits): numTemps (0-31)
    ///   Bits 12-14 (3 bits): flags field
    ///
    /// Flags field interpretation:
    ///   0-4: Simple method, flags = numArgs (0-4 args, no primitive)
    ///   5:   Quick-return-self (returns receiver without bytecode execution)
    ///   6:   Quick-return-field (returns instance variable at index numTemps)
    ///   7:   Extended form (numArgs and primitive in last literal)
    /// </summary>
    private record MethodHeaderInfo(
        int NumLiterals,
        bool NeedsLargeFrame,
        int NumTemps,
        int Flags,
        int NumArgs,
        int Primitive,
        bool IsQuickReturnSelf,
        bool IsQuickReturnField,
        int QuickReturnFieldIndex
    );

    private MethodHeaderInfo ParseMethodHeader(int methodId)
    {
        int headerOop = _heap.GetMethodLiteral(methodId, 0);
        int header = DecodeSmallInteger(headerOop);

        // Extract fields from header word
        int numLiterals = header & 0x3F;              // Bits 0-5
        bool needsLargeFrame = (header & 0x40) != 0;  // Bit 6
        int numTempsField = (header >> 7) & 0x1F;     // Bits 7-11
        int flags = (header >> 12) & 0x7;             // Bits 12-14

        int numArgs;
        int primitive = 0;
        bool isQuickReturnSelf = false;
        bool isQuickReturnField = false;
        int quickReturnFieldIndex = 0;
        int numTemps = numTempsField;

        // Interpret flags field
        if (flags < 5)
        {
            // Simple method: flags 0-4 encode numArgs directly
            numArgs = flags;
            primitive = 0;
        }
        else if (flags == 5)
        {
            // Quick-return-self: return receiver immediately
            numArgs = 0;
            primitive = 0;
            isQuickReturnSelf = true;
        }
        else if (flags == 6)
        {
            // Quick-return-field: return instance variable
            numArgs = 0;
            primitive = 0;
            isQuickReturnField = true;
            quickReturnFieldIndex = numTempsField;  // numTemps field repurposed as ivar index
            numTemps = 0;  // No temps for quick-return-field
        }
        else // flags >= 7
        {
            // Extended form: numArgs and primitive in last literal
            if (numLiterals > 0)
            {
                int lastLiteralOop = _heap.GetMethodLiteral(methodId, numLiterals);  // No +1, last literal
                int lastLiteral = DecodeSmallInteger(lastLiteralOop);

                primitive = lastLiteral & 0xFF;           // Low byte
                numArgs = (lastLiteral >> 8) & 0x1F;     // High bits
            }
            else
            {
                // Extended form but no literals - shouldn't happen in valid code
                numArgs = 0;
                primitive = 0;
            }
        }

        return new MethodHeaderInfo(
            NumLiterals: numLiterals,
            NeedsLargeFrame: needsLargeFrame,
            NumTemps: numTemps,
            Flags: flags,
            NumArgs: numArgs,
            Primitive: primitive,
            IsQuickReturnSelf: isQuickReturnSelf,
            IsQuickReturnField: isQuickReturnField,
            QuickReturnFieldIndex: quickReturnFieldIndex
        );
    }

    /// <summary>
    /// Extracts the primitive number from a method's header.
    /// Returns 0 if the method has no primitive.
    /// </summary>
    private int GetPrimitiveNumber(int methodId)
    {
        var headerInfo = ParseMethodHeader(methodId);
        return headerInfo.Primitive;
    }

    /// <summary>
    /// Pushes a value onto the operand stack.
    /// </summary>
    private void Push(int value)
    {
        int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackPointer");
        int sp = DecodeSmallInteger(_heap.GetField(_activeContextId, spIndex));

        _heap.SetIndexedField(_activeContextId, sp, value);
        _heap.SetField(_activeContextId, spIndex, EncodeSmallInteger(sp + 1));
    }

    /// <summary>
    /// Pops a value from the operand stack.
    /// </summary>
    private int Pop()
    {
        int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackPointer");
        int sp = DecodeSmallInteger(_heap.GetField(_activeContextId, spIndex));

        if (sp <= 0)
        {
            throw new InvalidOperationException("Stack underflow");
        }

        sp--;
        _heap.SetField(_activeContextId, spIndex, EncodeSmallInteger(sp));
        return _heap.GetIndexedField(_activeContextId, sp);
    }

    // SmallInteger encoding/decoding (LSB = 1 for SmallInteger)
    private static bool IsSmallInteger(int objectId) => (objectId & 1) == 1;
    private static int EncodeSmallInteger(int value) => (value << 1) | 1;
    private static int DecodeSmallInteger(int encoded) => encoded >> 1;

    // IPrimitiveContext implementation
    SmalltalkHeap IPrimitiveContext.Heap => _heap;
    LayoutResolver IPrimitiveContext.LayoutResolver => _layoutResolver;
    bool IPrimitiveContext.PrimitiveSuccess { get => _primitiveSuccess; set => _primitiveSuccess = value; }

    int IPrimitiveContext.GetFieldValue(int oop, string className, string fieldName)
    {
        int fieldIndex = _layoutResolver.GetFieldIndex(className, fieldName);
        return _heap.GetField(oop, fieldIndex);
    }

    void IPrimitiveContext.SetFieldValue(int oop, string className, string fieldName, int value)
    {
        int fieldIndex = _layoutResolver.GetFieldIndex(className, fieldName);
        _heap.SetField(oop, fieldIndex, value);
    }

    Memory<byte> IPrimitiveContext.GetByteArray(int oop)
    {
        // Get indexed size to verify it's a byte array
        int size = _heap.GetIndexedSize(oop);
        if (size == 0)
            return Memory<byte>.Empty;

        // For now, return empty - need to implement byte array access in heap
        // TODO: Add GetIndexedBytes method to SmalltalkHeap
        return Memory<byte>.Empty;
    }

    int IPrimitiveContext.AllocateObject(string className, int indexedSize)
    {
        // Look up class ID using layout resolver
        // For now, use a simple approach - this needs proper class lookup
        // TODO: Add class name to class ID mapping in heap or layout resolver

        // Get format from class name
        var format = indexedSize > 0
            ? SmalltalkHeap.ObjectFormat.ByteIndexed  // Default for indexed objects (bytes)
            : SmalltalkHeap.ObjectFormat.FixedPointer;

        // Count fields in the class layout
        int fixedSize = 0;
        try
        {
            // Try to count fields - this will fail if class not in layout
            var fieldNames = new System.Collections.Generic.List<string>();
            int index = 0;
            while (true)
            {
                try
                {
                    _layoutResolver.GetFieldIndex(className, $"field{index}");
                    index++;
                }
                catch
                {
                    break;
                }
            }
            fixedSize = index;
        }
        catch
        {
            fixedSize = 0;  // No fixed fields
        }

        // Allocate using heap
        return _heap.AllocateRawObject(
            classId: 0, // TODO: Proper class ID lookup
            format: format,
            fixedSize: fixedSize,
            indexedSize: indexedSize
        );
    }

    int IPrimitiveContext.SendMessage(int receiverOop, int selectorOop, int argumentsArray)
    {
        // Extract arguments from the array
        int argCount = _heap.GetIndexedSize(argumentsArray);
        int[] args = new int[argCount];
        for (int i = 0; i < argCount; i++)
        {
            args[i] = _heap.GetIndexedField(argumentsArray, i);
        }

        // Call the private SendMessage method
        return SendMessage(receiverOop, selectorOop, args);
    }

    int IPrimitiveContext.GetActiveContext()
    {
        return _activeContextId;
    }

    int IPrimitiveContext.GetInstructionPointer()
    {
        // Get the instruction pointer from the active context
        int ipIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "pc");
        int ipOop = _heap.GetField(_activeContextId, ipIndex);
        return DecodeSmallInteger(ipOop);
    }

    int IPrimitiveContext.CreateBlockContext(int homeContextOop, int startPC, int argCount)
    {
        // Determine the home context (if current context is already a BlockContext, use its home)
        int actualHome = homeContextOop;

        // Check if homeContextOop is a BlockContext - if so, get its home field
        // For simplicity, we'll use the passed homeContextOop directly for now
        // TODO: Implement proper home context resolution for nested blocks

        // Calculate stack size (args + some execution stack space)
        int stackSize = argCount + 16; // 16 slots for execution stack

        // Allocate BlockContext with indexed fields for stack
        int blockContextOop = _heap.AllocateRawObject(
            classId: 0, // TODO: Get BlockContext class ID
            format: SmalltalkHeap.ObjectFormat.PointerIndexed,
            fixedSize: 3, // home, startpc, nargs (inherited fields from ContextPart are handled by superclass)
            indexedSize: stackSize
        );

        // Get field indices using LayoutResolver
        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
        int startpcIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "startpc");
        int nargsIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "nargs");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "stackp");

        // Initialize BlockContext fields
        _heap.SetField(blockContextOop, homeIndex, actualHome);
        _heap.SetField(blockContextOop, startpcIndex, EncodeSmallInteger(startPC));
        _heap.SetField(blockContextOop, nargsIndex, EncodeSmallInteger(argCount));
        _heap.SetField(blockContextOop, stackpIndex, EncodeSmallInteger(0)); // Stack starts empty

        // Initialize indexed fields to nil
        for (int i = 0; i < stackSize; i++)
        {
            _heap.SetIndexedField(blockContextOop, i, GlobalRegistry.NilObjectId);
        }

        return blockContextOop;
    }

    int IPrimitiveContext.ActivateBlockContext(int blockContextOop, int argumentsArray)
    {
        // Get block context fields
        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
        int startpcIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "startpc");
        int nargsIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "nargs");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "stackp");
        int senderIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "pc");

        int homeOop = _heap.GetField(blockContextOop, homeIndex);
        int startPcOop = _heap.GetField(blockContextOop, startpcIndex);
        int nargsOop = _heap.GetField(blockContextOop, nargsIndex);

        int startPC = DecodeSmallInteger(startPcOop);
        int expectedArgCount = DecodeSmallInteger(nargsOop);

        // Verify argument count matches
        int providedArgCount = _heap.GetIndexedSize(argumentsArray);
        if (providedArgCount != expectedArgCount)
        {
            // Argument count mismatch - this will cause primitive to fail
            return GlobalRegistry.NilObjectId;
        }

        // Copy arguments into the block context's indexed fields
        for (int i = 0; i < providedArgCount; i++)
        {
            int argOop = _heap.GetIndexedField(argumentsArray, i);
            _heap.SetIndexedField(blockContextOop, i, argOop);
        }

        // Set up the block context for execution
        _heap.SetField(blockContextOop, senderIndex, _activeContextId); // Current context becomes sender
        _heap.SetField(blockContextOop, pcIndex, EncodeSmallInteger(startPC)); // Set PC to start of block
        _heap.SetField(blockContextOop, stackpIndex, EncodeSmallInteger(expectedArgCount)); // Stack pointer after args

        // Save current context and switch to block context
        int previousContext = _activeContextId;
        _activeContextId = blockContextOop;

        // Execute the block
        int result = ExecuteBytecodeLoop();

        // Restore previous context
        _activeContextId = previousContext;

        return result;
    }

    IEnumerable<int> IPrimitiveContext.GetAllObjects()
    {
        return _heap.GetAllObjects();
    }

    void IPrimitiveContext.BecomeOneWay(int fromOop, int toOop)
    {
        _heap.BecomeOneWay(fromOop, toOop);
    }

    void IPrimitiveContext.BecomeTwoWay(int oop1, int oop2)
    {
        _heap.BecomeTwoWay(oop1, oop2);
    }
}
