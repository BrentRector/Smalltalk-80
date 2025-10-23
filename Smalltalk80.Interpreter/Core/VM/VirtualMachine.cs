using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// The Smalltalk-80 Virtual Machine - handles bytecode execution, message sending, and method dispatch.
/// Uses ISmalltalkHeapManager for object storage and LayoutResolver for dynamic field access.
/// Consistently uses SmalltalkOOP for ALL object references to avoid OOP↔int conversions.
/// </summary>
public class VirtualMachine : IPrimitiveContext
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly VMMetadata _metadata;
    private readonly string[] _symbols;

    // Active context (current MethodContext being executed) - now using SmalltalkOOP
    private SmalltalkOOP _activeContextOOP;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    // Track block contexts: contextOOP -> blockContextOOP mapping - now using SmalltalkOOP
    private readonly Dictionary<SmalltalkOOP, SmalltalkOOP> _blockContextMapping = new();

    // Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _arithmeticSelectorOOPs;

    // Cached selector OOPs for special send bytecodes (0xC0-0xCF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _specialSelectorOOPs;

    // Verbose debugging support
    public bool VerboseDebug { get; set; } = false;
    private int _callDepth = 0;
    private readonly Dictionary<SmalltalkOOP, string> _methodSources = new();

    public VirtualMachine(ISmalltalkHeapManager heapManager, LayoutResolver layoutResolver, VMMetadata metadata, string[] symbols)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _metadata = metadata;
        _symbols = symbols;
    }

    /// <summary>
    /// Initializes the selector OOP caches by looking up selectors in the Smalltalk heap.
    /// Must be called after cold start completes and all symbols are interned.
    /// </summary>
    public void InitializeSelectorCaches()
    {
        // Initialize arithmetic selector OOPs (bytecodes 0xB0-0xBF)
        string[] arithmeticSelectors = new[]
        {
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
        };

        _arithmeticSelectorOOPs = new SmalltalkOOP[16];
        for (int i = 0; i < arithmeticSelectors.Length; i++)
        {
            _arithmeticSelectorOOPs[i] = LookupSymbolInHeap(arithmeticSelectors[i]);
        }

        // Initialize special selector OOPs (bytecodes 0xC0-0xCF)
        string[] specialSelectors = new[]
        {
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        };

        _specialSelectorOOPs = new SmalltalkOOP[16];
        for (int i = 0; i < specialSelectors.Length; i++)
        {
            _specialSelectorOOPs[i] = LookupSymbolInHeap(specialSelectors[i]);
        }
    }

    /// <summary>
    /// Looks up a symbol in the Smalltalk heap by searching the symbol table.
    /// This accesses Smalltalk heap structures (SymbolRegistry) not C# objects.
    /// </summary>
    private SmalltalkOOP LookupSymbolInHeap(string symbolName)
    {
        // Search the symbol table to find the symbol by name
        for (int i = 0; i < _symbols.Length; i++)
        {
            if (_symbols[i] == symbolName)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }
        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table during selector cache initialization");
    }

    /// <summary>
    /// Executes a method with the given receiver and arguments.
    /// Returns the result of the execution.
    /// </summary>
    public SmalltalkOOP Execute(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments)
    {
        // Parse method header to check for quick forms
        var header = ParseMethodHeader(methodOOP);

        // Quick-return-self optimization (flags=5)
        if (header.IsQuickReturnSelf)
        {
            return receiverOOP;
        }

        // Quick-return-field optimization (flags=6)
        if (header.IsQuickReturnField)
        {
            var fieldOOP = _heapManager.GetInstanceVariable(receiverOOP, header.QuickReturnFieldIndex);
            return fieldOOP;
        }

        // Create a MethodContext for this execution
        _activeContextOOP = CreateMethodContext(methodOOP, receiverOOP, arguments);

        // Execute bytecodes until we return
        return ExecuteBytecodeLoop();
    }

    public SmalltalkOOP ExecuteExistingContext(SmalltalkOOP contextOOP)
    {
        _activeContextOOP = contextOOP;
        
        return ExecuteBytecodeLoop();
    }

    /// <summary>
    /// Creates a MethodContext object for executing a method.
    /// Implements proper Blue Book MethodContext initialization with all required fields.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod OOP to execute</param>
    /// <param name="receiverOOP">Receiver OOP for this activation</param>
    /// <param name="arguments">Method arguments as OOPs</param>
    /// <param name="callerContextOOP">Optional caller context (sender), default for top-level DoIts</param>
    /// <returns>Allocated and initialized MethodContext OOP</returns>
    private SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
    {
        // Step 1: Validate CompiledMethod exists and parse header
        var header = ParseMethodHeader(methodOOP);

        // Validate argument count matches
        if (arguments.Length != header.NumArgs)
        {
            throw new InvalidOperationException(
                $"Argument count mismatch: method expects {header.NumArgs} args, got {arguments.Length}");
        }

        // Step 2: Calculate stack size (args + temps + execution stack)
        // Use large frame (48) or small frame (16) based on needsLargeFrame flag
        int executionStackSize = header.NeedsLargeFrame ? 48 : 16;
        int stackSize = header.NumArgs + header.NumTemps + executionStackSize;

        // Step 3: Get MethodContext class from GlobalRegistry
        if (!GlobalRegistry.TryGet("class:MethodContext", out SmalltalkOOP methodContextClassOOP))
        {
            throw new InvalidOperationException(
                "MethodContext class not found - ensure cold start completed Phase 2");
        }

        // Step 4: Allocate MethodContext using AllocateObject
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 6, stackSize);
        _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP); // Set class reference

        // Step 5: Get field indices using LayoutResolver
        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverMapIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiverMap");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        // Step 6: Initialize MethodContext fixed fields

        // sender: caller context or nil for top-level DoIts
        var callerOOP = callerContextOOP == default ? GlobalRegistry.NilOOP : callerContextOOP;
        _heapManager.SetInstanceVariable(contextOOP, senderIndex, callerOOP);

        // pc: start at bytecode offset 0
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));

        // stackp: points after args+temps (start of execution stack)
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

        // method: the CompiledMethod being executed
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);

        // receiverMap: used for multiple inheritance (not implemented yet, set to nil)
        _heapManager.SetInstanceVariable(contextOOP, receiverMapIndex, GlobalRegistry.NilOOP);

        // receiver: the object receiving this message
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);

        // Step 7: Initialize indexed fields (args + temps + stack)

        // Copy arguments into arg slots
        for (int i = 0; i < header.NumArgs; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, GlobalRegistry.NilOOP);
        }

        // Initialize execution stack slots to nil
        for (int i = header.NumArgs + header.NumTemps; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, GlobalRegistry.NilOOP);
        }

        // Note: GC root registration is implicit - the VM's _activeContextOOP field keeps the context alive
        // Note: home field is not applicable for MethodContext (only for BlockContext)
        // Note: literalFrame is accessed via method.literals, not stored in context

        return contextOOP;
    }

    /// <summary>
    /// Main bytecode execution loop.
    /// </summary>
    private SmalltalkOOP ExecuteBytecodeLoop()
    {
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");

        var methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
        // TODO: Implement GetMethodBytecodes in heap manager or add helper method
        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

            // Update IP
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Execute the bytecode
            bool continueExecution = ExecuteBytecode(bytecode, methodOOP);

            if (!continueExecution)
            {
                // Method returned, pop the result and return it
                return Pop();
            }

            // Re-read IP in case bytecode modified it (jumps)
            ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);
        return receiverOOP;
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            
            // For blocks, receiver should be the home context receiver
            var actualReceiverOOP = receiverOOP;
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var ivarBlockContextOOP))
            {
                var homeContextOOP = _heapManager.GetInstanceVariable(ivarBlockContextOOP, 3); // homeContext field
                actualReceiverOOP = GetHomeContextReceiver(homeContextOOP);
            }
            
            var valueOOP = _heapManager.GetInstanceVariable(actualReceiverOOP, index);
            Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);
            SmalltalkOOP valueOOP;
            
            // For blocks, read from BlockContext indexed fields
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockContextOOP))
            {
                valueOOP = _heapManager.GetIndexedOOP(blockContextOOP, index);
            }
            else
            {
                // Normal method - read from MethodContext indexed fields
                valueOOP = _heapManager.GetIndexedOOP(_activeContextOOP, index);
            }
            
            Push(valueOOP);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
            Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            // Handle blocks with nil literals - resolve from home context
            if (associationOOP == GlobalRegistry.NilOOP) // nil
            {
                
                // Check if this is a block execution context using our mapping
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockClosureOOP))
                {
                    
                    // Get the home context from the block
                    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    var homeContextOOP = _heapManager.GetInstanceVariable(blockClosureOOP, homeContextIndex);
                    
                    if (homeContextOOP != GlobalRegistry.NilOOP)
                    {
                        // Get the method from home context
                        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var homeMethodOOP = _heapManager.GetInstanceVariable(homeContextOOP, methodIndex);
                        
                        // Try to get the association from home method's literals
                        var homeAssociationOOP = GetMethodLiteral(homeMethodOOP, index + 1);
                        
                        
                        if (homeAssociationOOP != GlobalRegistry.NilOOP)
                        {
                            associationOOP = homeAssociationOOP;
                        }
                        else
                        {
                            Push(GlobalRegistry.NilOOP);
                            return true;
                        }
                    }
                    else
                    {
                        Push(GlobalRegistry.NilOOP);
                        return true;
                    }
                }
                else
                {
                    Push(GlobalRegistry.NilOOP);
                    return true;
                }
            }

            // Association has layout: [key, value]
            int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
            var valueOOP = _heapManager.GetInstanceVariable(associationOOP, valueIndex);

            Push(valueOOP);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var valueOOP = Pop();
            
            // For blocks, store to home context receiver
            var actualReceiverOOP = receiverOOP;
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var storeIvarBlockContextOOP))
            {
                var homeContextOOP = _heapManager.GetInstanceVariable(storeIvarBlockContextOOP, 3); // homeContext field
                actualReceiverOOP = GetHomeContextReceiver(homeContextOOP);
            }
            
            _heapManager.SetInstanceVariable(actualReceiverOOP, index, valueOOP);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var valueOOP = Pop();
            
            // For blocks, store in BlockContext indexed fields
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockContextOOP))
            {
                _heapManager.SetIndexedOOP(blockContextOOP, index, valueOOP);
            }
            else
            {
                // Normal method - store in MethodContext indexed fields
                _heapManager.SetIndexedOOP(_activeContextOOP, index, valueOOP);
            }
            
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                // For blocks, self should be the home context receiver, not the block itself
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockContextOOP2))
                {
                    try
                    {
                        // This is a block execution - get self from home context
                        var homeContextOOP = _heapManager.GetInstanceVariable(blockContextOOP2, 3); // homeContext field
                        var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                        Push(homeReceiverOOP);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                else
                {
                    // Normal method execution
                    Push(receiverOOP);
                }
                return true;

            case Bytecode.PUSH_TRUE:
                Push(GlobalRegistry.TrueOOP);
                return true;

            case Bytecode.PUSH_FALSE:
                Push(GlobalRegistry.FalseOOP);
                return true;

            case Bytecode.PUSH_NIL:
                Push(GlobalRegistry.NilOOP);
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
                // For blocks, return home context receiver as self
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var returnBlockContextOOP))
                {
                    var homeContextOOP = _heapManager.GetInstanceVariable(returnBlockContextOOP, 3); // homeContext field
                    var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                    Push(homeReceiverOOP);
                }
                else
                {
                    Push(receiverOOP);
                }
                return false; // Return from method

            case Bytecode.RETURN_TRUE:
                Push(GlobalRegistry.TrueOOP);
                return false;

            case Bytecode.RETURN_FALSE:
                Push(GlobalRegistry.FalseOOP);
                return false;

            case Bytecode.RETURN_NIL:
                Push(GlobalRegistry.NilOOP);
                return false;

            case Bytecode.RETURN_TOP:
                // Value already on stack
                return false;

            case Bytecode.RETURN_SELF_POP:
                // Return self from block context to caller's caller (0x7D)
                // Variant of RETURN_SELF for block contexts
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var returnSelfPopBlockContextOOP))
                {
                    var homeContextOOP = _heapManager.GetInstanceVariable(returnSelfPopBlockContextOOP, 3); // homeContext field
                    var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                    Push(homeReceiverOOP);
                }
                else
                {
                    Push(receiverOOP);
                }
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
            return ExecuteExtendedBytecode(methodOOP);
        }

        // Push active context (0x89) - used for block creation
        if (bytecode == 0x89)
        {
            Push(_activeContextOOP);
            return true;
        }

        // Short jump forward (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            int distance = (bytecode - 0x90) + 1; // Jump 1-8 bytes
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
            var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Medium jump forward (0xA0)
        if (bytecode == 0xA0)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
            var ipOOP2 = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP2.AsSmallInteger();

            byte distanceByte = bytecodes[currentIp++];
            int distance = distanceByte + 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Long jump forward (0xA1-0xA7)
        if (bytecode >= 0xA1 && bytecode <= 0xA7)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
            var ipOOP3 = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP3.AsSmallInteger();

            int numBytes = bytecode - 0x9F; // 0xA1 => 2 bytes, 0xA2 => 3 bytes, etc.
            int distance = 0;
            for (int i = 0; i < numBytes; i++)
            {
                distance = (distance << 8) | bytecodes[currentIp++];
            }
            distance += 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
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
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var receiverOOP2 = Pop();
            var resultOOP = SendMessage(receiverOOP2, selectorOOP, Array.Empty<SmalltalkOOP>());
            Push(resultOOP);
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var argOOP = Pop();
            var receiverOOP2 = Pop();
            var resultOOP = SendMessage(receiverOOP2, selectorOOP, new[] { argOOP });
            Push(resultOOP);
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var arg2OOP = Pop();
            var arg1OOP = Pop();
            var receiverOOP2 = Pop();
            var resultOOP = SendMessage(receiverOOP2, selectorOOP, new[] { arg1OOP, arg2OOP });
            Push(resultOOP);
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
        int ipIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte subOp = bytecodes[ip++];
        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);

        switch (subOp)
        {
            case 0x00: // PUSH_IVAR - Push instance variable at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = _heapManager.GetInstanceVariable(receiverOOP, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x01: // STORE_IVAR - Store to instance variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x02: // POP_STORE_IVAR - Pop and store to instance variable
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    return true;
                }

            case 0x03: // PUSH_TEMP - Push temporary at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = _heapManager.GetIndexedOOP(_activeContextOOP, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetIndexedOOP(_activeContextOOP, index, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetIndexedOOP(_activeContextOOP, index, valueOOP);
                    return true;
                }

            case 0x06: // PUSH_LITERAL - Push literal constant at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    Push(literal);
                    return true;
                }

            case 0x07: // PUSH_LITERAL_VAR - Push literal variable (association value)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
                    var valueOOP = _heapManager.GetInstanceVariable(associationOOP, valueIndex);
                    Push(valueOOP);
                    return true;
                }

            case 0x08: // STORE_LITERAL_VAR - Store to literal variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    int valueIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
                    _heapManager.SetInstanceVariable(associationOOP, valueIndex, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x09: // POP_STORE_LITERAL_VAR - Pop and store to literal variable (class/global variable)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    
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
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var result = SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    Push(result);
                    return true;
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var result = SendMessage(receiver, selectorOOP, new[] { arg });
                    Push(result);
                    return true;
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    var result = SendMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    Push(result);
                    return true;
                }

            case 0x0D: // SEND_N - Send with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    var result = SendMessage(receiver, selectorOOP, args);
                    Push(result);
                    return true;
                }

            case 0x0E: // SEND_SUPER_0 - Send to super with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    var result = SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    Push(result);
                    return true;
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    var result = SendMessage(receiver, selectorOOP, new[] { arg });
                    Push(result);
                    return true;
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    var result = SendMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    Push(result);
                    return true;
                }

            case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    // TODO: Implement super send (lookup in superclass)
                    var result = SendMessage(receiver, selectorOOP, args);
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
                    int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
                    var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
                    int sp = spOOP.AsSmallInteger();
                    if (sp <= 0)
                    {
                        throw new InvalidOperationException("Stack underflow on DUP");
                    }
                    var valueOOP = _heapManager.GetIndexedOOP(_activeContextOOP, sp - 1);
                    Push(valueOOP);
                    return true;
                }

            case 0x14: // JUMP_TRUE - Pop and jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == GlobalRegistry.TrueOOP)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x15: // JUMP_FALSE - Pop and jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == GlobalRegistry.FalseOOP)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x16: // JUMP - Unconditional jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    return true;
                }

            case 0x17: // JUMP_LONG_TRUE - Pop and long jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == GlobalRegistry.TrueOOP)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x18: // JUMP_LONG_FALSE - Pop and long jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == GlobalRegistry.FalseOOP)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x19: // JUMP_LONG - Unconditional long jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
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
            if (resultOOP != GlobalRegistry.NilOOP ||
                selector == "=" || selector == "~=" ||
                selector == "<" || selector == ">" ||
                selector == "<=" || selector == ">=")
            {
                Push(resultOOP);
                return;
            }
        }

        // Fall back to message send using cached selector OOP
        var sendResult = SendMessage(receiverOOP, selectorOOP, new[] { argOOP });
        Push(sendResult);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
    private void ExecuteSendSpecial(byte bytecode)
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
            8 => 1,  // blockCopy:
            9 => 0,  // value
            10 => 1, // value:
            11 => 1, // do:
            12 => 0, // new
            13 => 1, // new:
            14 => 0, // x
            15 => 0, // y
            _ => throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}")
        };

        // Pop arguments
        var args = new SmalltalkOOP[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        var receiverOOP = Pop();

        // Handle special cases
        if (selectorIndex == 6) // ==
        {
            bool isIdentical = receiverOOP == args[0];
            Push(isIdentical ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP);
            return;
        }

        if (selectorIndex == 7) // class
        {
            var classOOP = GetClass(receiverOOP);
            Push(classOOP);
            return;
        }

        // Fall back to message send using cached selector OOP
        var result = SendMessage(receiverOOP, selectorOOP, args);
        Push(result);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil OOP if operation fails or is not supported.
    /// </summary>
    private SmalltalkOOP ExecuteIntegerArithmetic(SmalltalkOOP receiverOOP, string selector, SmalltalkOOP argOOP)
    {
        if (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger)
            return GlobalRegistry.NilOOP;

        int a = receiverOOP.AsSmallInteger();
        int b = argOOP.AsSmallInteger();

        try
        {
            return selector switch
            {
                "+" => SmalltalkOOP.FromSmallInteger(a + b),
                "-" => SmalltalkOOP.FromSmallInteger(a - b),
                "*" => SmalltalkOOP.FromSmallInteger(a * b),
                "/" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : GlobalRegistry.NilOOP,
                "\\" => b != 0 ? SmalltalkOOP.FromSmallInteger(a % b) : GlobalRegistry.NilOOP,
                "//" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : GlobalRegistry.NilOOP,
                "<" => a < b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                ">" => a > b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                "<=" => a <= b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                ">=" => a >= b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                "=" => a == b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                "~=" => a != b ? GlobalRegistry.TrueOOP : GlobalRegistry.FalseOOP,
                "bitAnd:" => SmalltalkOOP.FromSmallInteger(a & b),
                "bitOr:" => SmalltalkOOP.FromSmallInteger(a | b),
                "bitShift:" => b >= 0 ? SmalltalkOOP.FromSmallInteger(a << b) : SmalltalkOOP.FromSmallInteger(a >> (-b)),
                _ => GlobalRegistry.NilOOP
            };
        }
        catch
        {
            return GlobalRegistry.NilOOP;
        }
    }

    /// <summary>
    /// Sends a message to a receiver.
    /// Looks up the method in the receiver's class and executes it.
    /// </summary>
    private SmalltalkOOP SendMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // Look up the method in the receiver's class
        var classOOP = GetClass(receiverOOP);

        // Get selector name once for all uses
        string? selectorName = GetSymbolName(selectorOOP);

        // Verbose debug: log method entry
        if (VerboseDebug)
        {
            string className = GetClassName(classOOP) ?? "?";
            string indent = new string(' ', _callDepth * 2);
            Console.WriteLine($"{indent}>> {className}>>{selectorName} (args={args.Length})");
            _callDepth++;
        }
        // SPECIAL CASE: Block CompiledMethods responding to value/value:/value:value:
        // In v1.0.3, blocks are separate CompiledMethod objects that respond to value messages
        // by creating proper BlockContext and executing with closure semantics
        if (GlobalRegistry.TryGet("class:CompiledMethod", out SmalltalkOOP compiledMethodClassId) &&
            classOOP == compiledMethodClassId)
        {
            if (selectorName == "value" || selectorName == "value:" || selectorName == "value:value:")
            {
                // Execute the block with proper BlockContext wrapping
                return InvokeBlock(receiverOOP, args);
            }
        }

        // Debug: check for classId 0 on "comment:" send
        if (classOOP == SmalltalkOOP.Null)
        {
            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "unknown"} in class 0 (receiver OOP={receiverOOP}, receiverId has classId 0!)");
        }

        var methodInfo = GetMethod(classOOP, selectorName!);

        if (methodInfo == null)
        {
            string? className = GetClassName(classOOP);
            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "<unknown>"} in class {className} (classOOP={classOOP})");
        }

        // Check if the method has a primitive
        int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodOOP);
        if (primitiveNumber > 0)
        {
            // Try to execute the primitive
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                // Primitive succeeded - return result directly
                return primitiveResult;
            }

            // Primitive failed - execute method bytecodes as fallback
        }

        // Execute the method with proper context chaining
        // Save current active context as the sender
        var previousContextOOP = _activeContextOOP;

        // Create new context with current context as sender
        _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, previousContextOOP);

        try
        {
            // Execute the new context
            var result = ExecuteBytecodeLoop();

            // Verbose debug: log method exit
            if (VerboseDebug)
            {
                _callDepth--;
                string indent = new string(' ', _callDepth * 2);
                string resultStr = result.IsSmallInteger ? $"SmallInteger({result.AsSmallInteger()})" : $"OOP({result})";
                Console.WriteLine($"{indent}<< {GetClassName(classOOP)}>>{selectorName} => {resultStr}");
            }

            // Restore previous context
            _activeContextOOP = previousContextOOP;

            return result;
        }
        catch (Exception ex)
        {
            if (VerboseDebug)
            {
                _callDepth--;
            }

            // On error, dump detailed method state
            DumpMethodStateOnError(methodInfo.MethodOOP, receiverOOP, args, classOOP, selectorName!, ex);

            // Restore previous context
            _activeContextOOP = previousContextOOP;

            throw;
        }
    }

    /// <summary>
    /// Dumps detailed method state when an error occurs during execution.
    /// Shows bytecode disassembly, source code, literals, and local variables.
    /// </summary>
    private void DumpMethodStateOnError(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] args, SmalltalkOOP classOOP, string selectorName, Exception ex)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine($"ERROR IN: {GetClassName(classOOP)}>>{selectorName}");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"Exception: {ex.Message}\n");

        // Get method header using correct custom format
        var headerInfo = ParseMethodHeader(methodOOP);
        int literalCount = headerInfo.NumLiterals;
        int tempCount = headerInfo.NumTemps;
        int argCount = headerInfo.NumArgs;

        // Get bytecodes
        int literalFrameSize = literalCount + 1; // +1 for header
        var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, literalFrameSize);
        int bytecodeCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
        byte[] bytecodes = new byte[bytecodeCount];
        for (int i = 0; i < bytecodeCount; i++)
        {
            bytecodes[i] = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
        }

        // Get literals
        SmalltalkOOP[] literalOOPs = new SmalltalkOOP[literalCount];
        for (int i = 0; i < literalCount; i++)
        {
            literalOOPs[i] = _heapManager.GetIndexedOOP(methodOOP, i + 1);
        }

        // Display source code if available
        if (_methodSources.TryGetValue(methodOOP, out string? source))
        {
            Console.WriteLine("SOURCE CODE:");
            Console.WriteLine(source);
            Console.WriteLine();
        }

        // Display bytecode disassembly
        Console.WriteLine("BYTECODE DISASSEMBLY:");
        string disassembly = BytecodeDisassembler.Disassemble(bytecodes, literalOOPs,
            (litOOP) => DescribeLiteral(litOOP),
            Array.Empty<string>());
        Console.WriteLine(disassembly);

        // Display literals
        Console.WriteLine("LITERALS:");
        for (int i = 0; i < literalCount; i++)
        {
            var litOOP = _heapManager.GetIndexedOOP(methodOOP, i + 1);
            Console.WriteLine($"  [{i}] = {DescribeLiteral(litOOP)}");
        }
        Console.WriteLine();

        // Display arguments
        Console.WriteLine($"ARGUMENTS ({args.Length}):");
        for (int i = 0; i < args.Length; i++)
        {
            Console.WriteLine($"  arg{i} = {DescribeValue(args[i])}");
        }
        Console.WriteLine();

        // Display receiver
        Console.WriteLine("RECEIVER:");
        Console.WriteLine($"  self = {DescribeValue(receiverOOP)}");
        Console.WriteLine();

        // Display temps (if in active context)
        if (_activeContextOOP != SmalltalkOOP.Null)
        {
            Console.WriteLine($"TEMPORARIES ({tempCount}):");
            for (int i = 0; i < tempCount; i++)
            {
                var tempOOP = _heapManager.GetIndexedOOP(_activeContextOOP, i);
                Console.WriteLine($"  temp{i} = {DescribeValue(tempOOP)}");
            }
            Console.WriteLine();
        }

        // Display stack
        if (_activeContextOOP != SmalltalkOOP.Null)
        {
            int stackpIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
            var stackpOOP = _heapManager.GetInstanceVariable(_activeContextOOP, stackpIndex);
            int stackp = stackpOOP.AsSmallInteger();

            Console.WriteLine($"STACK (sp={stackp}):");
            int stackStart = tempCount;
            for (int i = stackStart; i < stackp; i++)
            {
                var stackOOP = _heapManager.GetIndexedOOP(_activeContextOOP, i);
                Console.WriteLine($"  [{i - stackStart}] = {DescribeValue(stackOOP)}");
            }
        }

        Console.WriteLine(new string('=', 80) + "\n");
    }

    private string DescribeLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"Symbol(#{name})";
        }

        if (className == "Association")
        {
            int keyIndex = _layoutResolver.GetFieldIndex("class:Association", "key");
            var keyOOP = _heapManager.GetInstanceVariable(oop, keyIndex);
            string? keyName = GetSymbolName(keyOOP);
            return $"Association(#{keyName})";
        }

        if (className == "CompiledMethod")
        {
            return $"CompiledMethod(block)";
        }

        return $"{className}({oop})";
    }

    private string DescribeValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        if (oop == GlobalRegistry.NilOOP)
        {
            return "nil";
        }

        if (oop == GlobalRegistry.TrueOOP)
        {
            return "true";
        }

        if (oop == GlobalRegistry.FalseOOP)
        {
            return "false";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"#{name}";
        }

        return $"{className}({oop})";
    }

    /// <summary>
    /// Invokes a block CompiledMethod with proper BlockContext wrapping for v1.0.3 semantics.
    /// Creates BlockContext, resolves closure scope, and executes with proper self resolution.
    /// </summary>
    /// <param name="blockMethodOOP">The block CompiledMethod to invoke</param>
    /// <param name="args">Arguments to pass to the block</param>
    /// <returns>The result of block execution</returns>
    private SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args)
    {
        try
        {
            return InvokeBlockInternal(blockMethodOOP, args);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private SmalltalkOOP InvokeBlockInternal(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args)
    {
        // Step 1: Resolve block ownership from blockOwnerMap
        var blockOwnerMap = GlobalRegistry.GetObject<Dictionary<string, string>>("blockOwnerMap");
        if (blockOwnerMap == null)
        {
            throw new InvalidOperationException("blockOwnerMap not found in GlobalRegistry - ensure Phase6 completed");
        }

        // Get local ID for the block method
        string? blockLocalId = GetLocalId(blockMethodOOP);
        if (blockLocalId == null || !blockOwnerMap.TryGetValue(blockLocalId, out string? ownerLocalId))
        {
            throw new InvalidOperationException($"Block method {blockMethodOOP} not found in blockOwnerMap");
        }

        // Step 2: Determine contexts
        // homeContext = find the actual method context (not a block context) by walking the sender chain
        var homeContextOOP = FindMethodContext(_activeContextOOP);
        if (homeContextOOP == default)
        {
            throw new InvalidOperationException("No method context found - blocks must be invoked within method execution");
        }

        // outerContext = resolve to root method by walking blockOwnerMap
        var outerContextOOP = ResolveOuterContext(ownerLocalId, blockOwnerMap);

        // Step 3: Parse block method header for metadata
        var blockHeader = ParseMethodHeader(blockMethodOOP);
        int argCount = blockHeader.NumArgs;
        int tempCount = blockHeader.NumTemps;

        // Validate argument count
        if (args.Length != argCount)
        {
            throw new InvalidOperationException(
                $"Block argument count mismatch: expected {argCount}, got {args.Length}");
        }

        // Step 4: Create BlockContext
        // Get field indices using LayoutResolver (VirtualMachine responsibility)
        int nargsIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "nargs");
        int startpcIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "startpc");
        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
        
        // Call heap manager to create BlockContext with resolved field indices (proper architectural boundaries)
        // Get BlockContext class from GlobalRegistry
        if (!GlobalRegistry.TryGet("class:BlockContext", out SmalltalkOOP blockContextClassOOP))
        {
            throw new InvalidOperationException("BlockContext class not found");
        }
        
        // Allocate BlockContext
        var blockContextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 4, argCount + tempCount);
        _heapManager.SetSlot(blockContextOOP, 0, blockContextClassOOP); // Set class reference
        
        // Initialize BlockContext fields
        _heapManager.SetInstanceVariable(blockContextOOP, nargsIndex, SmalltalkOOP.FromSmallInteger(argCount));
        _heapManager.SetInstanceVariable(blockContextOOP, startpcIndex, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(blockContextOOP, homeIndex, homeContextOOP);

        // Step 5: Bind arguments into BlockContext indexed fields
        for (int i = 0; i < args.Length; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, args[i]);
        }

        // Step 6: Get home context receiver for proper self resolution
        // For blocks, we need to find the method where the block was originally defined
        var homeContextReceiverOOP = GetBlockHomeReceiver(blockMethodOOP, ownerLocalId);

        // Step 7: Create execution MethodContext backed by BlockContext
        var executionContextOOP = CreateBlockExecutionContext(
            blockMethodOOP, homeContextReceiverOOP, blockContextOOP, homeContextOOP);

        // Step 8: Execute block with GC protection
        // Root the BlockContext during execution to prevent GC
        // Note: In a real implementation, we'd use proper GC rooting
        
        // Save current active context
        var previousContextOOP = _activeContextOOP;
        
        // Set the block execution context as active
        _activeContextOOP = executionContextOOP;

        // Execute the block bytecodes
        var result = ExecuteBytecodeLoop();

        // Clean up block context mapping
        _blockContextMapping.Remove(executionContextOOP);

        // Restore previous context
        _activeContextOOP = previousContextOOP;

        return result;
    }

    /// <summary>
    /// Creates a MethodContext for executing a block, backed by a BlockContext.
    /// This implements Approach B from the documentation.
    /// </summary>
    private SmalltalkOOP CreateBlockExecutionContext(SmalltalkOOP blockMethodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP blockContextOOP, SmalltalkOOP senderContextOOP)
    {
        var header = ParseMethodHeader(blockMethodOOP);

        // Calculate stack size for execution
        int executionStackSize = header.NeedsLargeFrame ? 48 : 16;
        int stackSize = header.NumArgs + header.NumTemps + executionStackSize;

        // Get MethodContext class ID
        if (!GlobalRegistry.TryGet("class:MethodContext", out SmalltalkOOP methodContextClassOOP))
        {
            throw new InvalidOperationException("MethodContext class not found");
        }

        // Allocate MethodContext
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 6, stackSize);
        _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP); // Set class reference

        // Get field indices
        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverMapIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiverMap");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        // Initialize MethodContext fields
        _heapManager.SetInstanceVariable(contextOOP, senderIndex, senderContextOOP);
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, blockMethodOOP);
        _heapManager.SetInstanceVariable(contextOOP, receiverMapIndex, GlobalRegistry.NilOOP);
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP); // self = home context receiver

        // Store blockContext backreference in our mapping
        _blockContextMapping[contextOOP] = blockContextOOP;

        return contextOOP;
    }

    /// <summary>
    /// Resolves the outer context by walking the blockOwnerMap to find the root method.
    /// </summary>
    private SmalltalkOOP ResolveOuterContext(string ownerLocalId, Dictionary<string, string> blockOwnerMap)
    {
        string currentLocalId = ownerLocalId;
        
        // Walk the ownership chain to find the root method
        while (blockOwnerMap.TryGetValue(currentLocalId, out string? parentLocalId))
        {
            currentLocalId = parentLocalId;
        }

        // currentLocalId is now the root method's localId
        // For now, return the home context (we'd need to implement proper outer context tracking)
        return _activeContextOOP;
    }

    /// <summary>
    /// Gets the receiver from the home context for proper self resolution in blocks.
    /// </summary>
    private SmalltalkOOP GetHomeContextReceiver(SmalltalkOOP homeContextOOP)
    {
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        var result = _heapManager.GetInstanceVariable(homeContextOOP, receiverIndex);
        return result;
    }

    /// <summary>
    /// Gets the receiver from the home method context for a block.
    /// Uses the blockOwnerMap to find the ultimate method where the block was defined.
    /// </summary>
    private SmalltalkOOP GetBlockHomeReceiver(SmalltalkOOP blockMethodOOP, string ownerLocalId)
    {
        
        // Walk the blockOwnerMap to find the root method (not another block)
        var blockOwnerMap = GlobalRegistry.GetObject<Dictionary<string, string>>("blockOwnerMap");
        if (blockOwnerMap == null)
        {
            throw new InvalidOperationException("blockOwnerMap not found");
        }
        
        string currentLocalId = ownerLocalId;
        while (blockOwnerMap.TryGetValue(currentLocalId, out string? parentLocalId))
        {
            currentLocalId = parentLocalId;
        }
        
        
        // Now find the MethodContext for this root method
        // Look through all active contexts to find one executing this method
        var methodContextOOP = FindMethodContextByLocalId(currentLocalId);
        if (methodContextOOP == default)
        {
            throw new InvalidOperationException($"No active context found for method {currentLocalId}");
        }
        
        
        // Get the receiver from this method context
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        var receiver = _heapManager.GetInstanceVariable(methodContextOOP, receiverIndex);
        
        return receiver;
    }

    /// <summary>
    /// Finds a MethodContext that is executing the given method localId.
    /// </summary>
    private SmalltalkOOP FindMethodContextByLocalId(string methodLocalId)
    {
        // For now, we'll use a simple approach: find the first context in the sender chain
        // that has the correct method. In a real implementation, we'd maintain a proper
        // context stack or use the blockOwnerMap more sophisticatedly.
        
        var currentContextOOP = _activeContextOOP;
        
        while (currentContextOOP != default && currentContextOOP != GlobalRegistry.NilOOP)
        {
            // Get the method from this context
            int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
            var methodOOP = _heapManager.GetInstanceVariable(currentContextOOP, methodIndex);
            
            // Check if this matches our target method
            string? contextMethodLocalId = GetLocalId(methodOOP);
            
            if (contextMethodLocalId == methodLocalId)
            {
                return currentContextOOP;
            }
            
            // Move to sender
            int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
            var senderContextOOP = _heapManager.GetInstanceVariable(currentContextOOP, senderIndex);
            currentContextOOP = senderContextOOP;
        }
        
        return default;
    }

    /// <summary>
    /// Finds the actual method context by walking up the sender chain.
    /// Skips block execution contexts to find the underlying method context.
    /// </summary>
    private SmalltalkOOP FindMethodContext(SmalltalkOOP startContextOOP)
    {
        var currentContextOOP = startContextOOP;
        
        while (currentContextOOP != default && currentContextOOP != GlobalRegistry.NilOOP)
        {
            
            // Check if this context is a block execution context
            if (_blockContextMapping.ContainsKey(currentContextOOP))
            {
                // This is a block execution context, get its sender
                int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
                var senderContextOOP = _heapManager.GetInstanceVariable(currentContextOOP, senderIndex);
                currentContextOOP = senderContextOOP;
                continue;
            }
            
            // This should be a regular method context
            return currentContextOOP;
        }
        
        return default;
    }

    /// <summary>
    /// Gets the local ID for an object (used for blockOwnerMap lookup).
    /// Uses the reverse mapping built during Phase8.
    /// </summary>
    private string? GetLocalId(SmalltalkOOP objectOOP)
    {
        var objectIdToLocalId = GlobalRegistry.GetObject<Dictionary<SmalltalkOOP, string>>("objectIdToLocalId");
        if (objectIdToLocalId?.TryGetValue(objectOOP, out string? localId) == true)
        {
            return localId;
        }
        return null;
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

    private MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP)
    {
        var headerOop = GetMethodLiteral(methodOOP, 0);
        int header = headerOop.AsSmallInteger();

        // Method header word format per docs/Interpreter-Architecture.md lines 5015-5020:
        // Bits 0-5:   numLiterals (6 bits, 0-63)
        // Bit 6:      needsLargeFrame flag
        // Bits 7-11:  numTemps (5 bits, 0-31)
        // Bits 12-14: flags field (3 bits)
        // Bit 15:     Reserved/unused

        int numLiterals = header & 0x3F;              // Bits 0-5
        bool needsLargeFrame = (header & 0x40) != 0;  // Bit 6
        int numTemps = (header >> 7) & 0x1F;          // Bits 7-11
        int flags = (header >> 12) & 0x7;             // Bits 12-14

        int numArgs;
        int primitive = 0;

        // Interpret flags field
        if (flags < 5)
        {
            // Simple form: flags 0-4 encode numArgs directly
            numArgs = flags;
        }
        else if (flags == 7)
        {
            // Extended form: numArgs and primitive in last literal
            if (numLiterals > 0)
            {
                // Last literal is at indexed slot (numLiterals), since:
                // - Indexed slot 0 = header
                // - Indexed slots 1..N = literals (N = numLiterals)
                // - Indexed slot N+1 = bytecode array (hidden)
                // So last literal (literalN) is at indexed slot numLiterals
                var lastLiteralOop = GetMethodLiteral(methodOOP, numLiterals);
                int lastLiteral = lastLiteralOop.AsSmallInteger();

                primitive = lastLiteral & 0xFF;           // Low byte
                numArgs = (lastLiteral >> 8) & 0x1F;     // High bits
            }
            else
            {
                numArgs = 0;
                primitive = 0;
            }
        }
        else
        {
            // Flags 5-6: Quick-return forms (not supported yet)
            numArgs = 0;
        }

        return new MethodHeaderInfo(
            NumLiterals: numLiterals,
            NeedsLargeFrame: needsLargeFrame,
            NumTemps: numTemps,
            Flags: flags,
            NumArgs: numArgs,
            Primitive: primitive,
            IsQuickReturnSelf: flags == 5,
            IsQuickReturnField: flags == 6,
            QuickReturnFieldIndex: flags == 6 ? numTemps : 0  // numTemps repurposed for flags=6
        );
    }

    /// <summary>
    /// Extracts the primitive number from a method's header.
    /// Returns 0 if the method has no primitive.
    /// </summary>
    private int GetPrimitiveNumber(SmalltalkOOP methodOOP)
    {
        var headerInfo = ParseMethodHeader(methodOOP);
        return headerInfo.Primitive;
    }

    /// <summary>
    /// Pushes a value onto the operand stack.
    /// </summary>
    private void Push(SmalltalkOOP valueOOP)
    {
        int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        _heapManager.SetIndexedOOP(_activeContextOOP, sp, valueOOP);
        _heapManager.SetInstanceVariable(_activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp + 1));
    }

    /// <summary>
    /// Pops a value from the operand stack.
    /// </summary>
    private SmalltalkOOP Pop()
    {
        int spIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        if (sp <= 0)
        {
            throw new InvalidOperationException("Stack underflow");
        }

        sp--;
        _heapManager.SetInstanceVariable(_activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp));
        return _heapManager.GetIndexedOOP(_activeContextOOP, sp);
    }



    // Helper methods for heap access during refactoring
    private byte[] GetMethodBytecodes(SmalltalkOOP methodOOP)
    {
        // TODO: Implement GetMethodBytecodes - need to access bytecode array from CompiledMethod
        // CompiledMethod structure: [header, literal1, ..., literalN, bytecodeArrayOOP]
        // The last indexed slot contains OOP to ByteArray with bytecodes
        int indexedSize = _heapManager.GetIndexedSize(methodOOP);
        if (indexedSize > 0)
        {
            // Get the last indexed slot which should be the bytecode array OOP
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, indexedSize - 1);
            int byteCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            byte[] bytecodes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                bytecodes[i] = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
            }
            return bytecodes;
        }
        return Array.Empty<byte>();
    }

    private SmalltalkOOP GetMethodLiteral(SmalltalkOOP methodOOP, int index)
    {
        // TODO: Implement GetMethodLiteral - access literal from CompiledMethod
        var literalOOP = _heapManager.GetIndexedOOP(methodOOP, index);
        return literalOOP;
    }

    private SmalltalkOOP GetClass(SmalltalkOOP objectOOP)
    {
        // SmallIntegers are immediate values - return their class directly
        if (objectOOP.IsSmallInteger)
            return _metadata.SmallIntegerClassOOP;

        // For heap objects, class is in slot 0
        var classOOP = _heapManager.GetSlot(objectOOP, 0);
        return classOOP;
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        // Check if this is a metaclass by trying to read thisClass field
        // All metaclass instances (meta:X) have thisClass pointing to their instance class (class:X)
        try
        {
            var thisClassOOP = _heapManager.GetInstanceVariable(classOOP, _metadata.MetaclassThisClassFieldIndex);
            if (thisClassOOP != GlobalRegistry.NilOOP && thisClassOOP != SmalltalkOOP.Null)
            {
                // This is a metaclass - get the name from its thisClass field
                var nameSymbolOOP = _heapManager.GetInstanceVariable(thisClassOOP, _metadata.ClassNameFieldIndex);
                var baseName = GetSymbolName(nameSymbolOOP);
                if (baseName != null)
                    return baseName + " class";
            }
        }
        catch
        {
            // Not a metaclass or field doesn't exist - fall through to regular class handling
        }

        // Regular class - get name directly
        var nameSymbolOOP2 = _heapManager.GetInstanceVariable(classOOP, _metadata.ClassNameFieldIndex);
        return GetSymbolName(nameSymbolOOP2) ?? $"<unnamed class {classOOP}>";
    }

    private string? GetSymbolName(SmalltalkOOP symbolOOP)
    {
        var symbolClass = GetClass(symbolOOP);
        if (symbolClass != _metadata.SymbolClassOOP)
            return null;
        
        int byteCount = _heapManager.GetIndexedSize(symbolOOP);
        if (byteCount == 0)
            return null;
            
        var bytes = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }
        
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    private MethodInfo? GetMethod(SmalltalkOOP classOOP, string selectorName)
    {
        var currentClassOOP = classOOP;
        int depth = 0;
        const int maxDepth = 20; // Prevent infinite loops


        // Walk up the inheritance chain until we find the method or reach nil
        while (currentClassOOP != GlobalRegistry.NilOOP && depth < maxDepth)
        {
            string currentClassName = GetClassName(currentClassOOP);
            Console.WriteLine($"  [GetMethod] depth={depth}, {currentClassName}, looking for '{selectorName}'");

            // Get the method dictionary from the class
            int methodDictIndex = _layoutResolver.GetFieldIndex("class:Class", "methodDict");
            var methodDictOOP = _heapManager.GetInstanceVariable(currentClassOOP, methodDictIndex);

            if (methodDictOOP == GlobalRegistry.NilOOP)
            {
                // No method dictionary, try superclass
                int superclassIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass");
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, superclassIndex);

                if (superclassOOP == GlobalRegistry.NilOOP)
                {
                    Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root of hierarchy)");
                    break; // Reached root of class hierarchy
                }

                string superclassName = GetClassName(superclassOOP);
                Console.WriteLine($"  '{selectorName}' not found, trying superclass={superclassName}");
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // MethodDictionary structure (IdentityDictionary):
            // - Instance variable [0] = valuesArray (Array of methods)
            // - Instance variable [1] = tally (SmallInteger)
            // - Indexed fields = keys (selectors)
            // To find a method: search indexed fields for selector, then get method from valuesArray at same index

            var valuesArrayOOP = _heapManager.GetInstanceVariable(methodDictOOP, 0);
            if (valuesArrayOOP == GlobalRegistry.NilOOP)
            {
                // No values array, try superclass
                int superclassIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass");
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, superclassIndex);
                string superclassName = GetClassName(superclassOOP);
                Console.WriteLine($"  '{selectorName}' not found, trying superclass={superclassName}");
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            int dictSize = _heapManager.GetIndexedSize(methodDictOOP);

            for (int i = 0; i < dictSize; i++)
            {
                var selectorOOP = _heapManager.GetIndexedOOP(methodDictOOP, i);

                // Skip nil entries (empty slots in the hash table)
                if (selectorOOP == GlobalRegistry.NilOOP)
                    continue;

                // Check if this is the selector we're looking for
                string? currentSelectorName = GetSymbolName(selectorOOP);

                if (currentSelectorName == selectorName)
                {
                    // Found it! Get the method from the valuesArray at the same index
                    var methodOOP = _heapManager.GetIndexedOOP(valuesArrayOOP, i);
                    return new MethodInfo { MethodOOP = methodOOP };
                }
            }

            // Not found in this class, try superclass
            int superclassIndex2 = _layoutResolver.GetFieldIndex("class:Class", "superclass");
            var nextSuperclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, superclassIndex2);

            if (nextSuperclassOOP == GlobalRegistry.NilOOP)
            {
                Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root)");
            }
            else
            {
                string nextSuperclassName = GetClassName(nextSuperclassOOP);
                Console.WriteLine($"  '{selectorName}' not found, trying superclass={nextSuperclassName}");
            }

            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        Console.WriteLine($"  [GetMethod] FAILED: '{selectorName}' not found after {depth} levels");
        return null;
    }

    private class MethodInfo
    {
        public SmalltalkOOP MethodOOP { get; set; }
    }

    // IPrimitiveContext implementation - using new heap manager API with SmalltalkOOP types
    ISmalltalkHeapManager IPrimitiveContext.HeapManager => _heapManager;
    LayoutResolver IPrimitiveContext.LayoutResolver => _layoutResolver;
    bool IPrimitiveContext.PrimitiveSuccess { get => _primitiveSuccess; set => _primitiveSuccess = value; }

    SmalltalkOOP IPrimitiveContext.GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
    {
        int fieldIndex = _layoutResolver.GetFieldIndex(className, fieldName);
        return _heapManager.GetInstanceVariable(oop, fieldIndex);
    }

    void IPrimitiveContext.SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
    {
        int fieldIndex = _layoutResolver.GetFieldIndex(className, fieldName);
        _heapManager.SetInstanceVariable(oop, fieldIndex, value);
    }

    Memory<byte> IPrimitiveContext.GetByteArray(SmalltalkOOP oop)
    {
        int size = _heapManager.GetIndexedSize(oop);
        if (size == 0)
            return Memory<byte>.Empty;

        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(oop, i);
        }
        return bytes;
    }

    SmalltalkOOP IPrimitiveContext.AllocateObject(string className, int indexedSize)
    {
        // Look up class ID from GlobalRegistry - className might already have "class:" prefix
        string classKey = className.StartsWith("class:") ? className : $"class:{className}";
        if (!GlobalRegistry.TryGet(classKey, out SmalltalkOOP classOOP))
        {
            throw new InvalidOperationException($"Class {classKey} not found in GlobalRegistry");
        }

        // Get format based on whether object has indexed fields
        var format = indexedSize > 0
            ? ObjectFormat.ByteIndexed  // Default for indexed objects (bytes)
            : ObjectFormat.Pointer;

        // For simplicity, use a basic fixed size - can be refined later
        int fixedSize = 2; // Basic assumption for most objects

        // Allocate the object
        var objectOOP = _heapManager.AllocateObject(format, fixedSize, indexedSize);
        
        // Set the class reference
        _heapManager.SetSlot(objectOOP, 0, classOOP);
        
        return objectOOP;
    }

    SmalltalkOOP IPrimitiveContext.SendMessage(SmalltalkOOP receiverOop, SmalltalkOOP selectorOop, SmalltalkOOP argumentsArray)
    {
        // Extract arguments from the array
        int argCount = _heapManager.GetIndexedSize(argumentsArray);
        var args = new SmalltalkOOP[argCount];
        for (int i = 0; i < argCount; i++)
        {
            args[i] = _heapManager.GetIndexedOOP(argumentsArray, i);
        }

        // Call the private SendMessage method
        return SendMessage(receiverOop, selectorOop, args);
    }

    SmalltalkOOP IPrimitiveContext.GetActiveContext()
    {
        return _activeContextOOP;
    }

    int IPrimitiveContext.GetInstructionPointer()
    {
        // Get the instruction pointer from the active context
        int ipIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "pc");
        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        return ipOOP.AsSmallInteger();
    }

    SmalltalkOOP IPrimitiveContext.CreateBlockContext(SmalltalkOOP homeContextOop, int startPC, int argCount)
    {
        // Determine the home context (if current context is already a BlockContext, use its home)
        var actualHomeOOP = homeContextOop;

        // Check if homeContextOop is a BlockContext - if so, get its home field
        // For simplicity, we'll use the passed homeContextOop directly for now
        // TODO: Implement proper home context resolution for nested blocks

        // Calculate stack size (args + some execution stack space)
        int stackSize = argCount + 16; // 16 slots for execution stack

        // Get BlockContext class
        if (!GlobalRegistry.TryGet("class:BlockContext", out SmalltalkOOP blockContextClassOOP))
        {
            throw new InvalidOperationException("BlockContext class not found");
        }

        // Allocate BlockContext with indexed fields for stack
        var blockContextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 3, stackSize);
        _heapManager.SetSlot(blockContextOOP, 0, blockContextClassOOP); // Set class reference

        // Get field indices using LayoutResolver
        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
        int startpcIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "startpc");
        int nargsIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "nargs");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "stackp");

        // Initialize BlockContext fields
        _heapManager.SetInstanceVariable(blockContextOOP, homeIndex, actualHomeOOP);
        _heapManager.SetInstanceVariable(blockContextOOP, startpcIndex, SmalltalkOOP.FromSmallInteger(startPC));
        _heapManager.SetInstanceVariable(blockContextOOP, nargsIndex, SmalltalkOOP.FromSmallInteger(argCount));
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(0)); // Stack starts empty

        // Initialize indexed fields to nil
        for (int i = 0; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, GlobalRegistry.NilOOP);
        }

        return blockContextOOP;
    }

    SmalltalkOOP IPrimitiveContext.ActivateBlockContext(SmalltalkOOP blockContextOop, SmalltalkOOP argumentsArray)
    {
        // Get block context fields
        var blockContextOOP = blockContextOop;
        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
        int startpcIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "startpc");
        int nargsIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "nargs");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "stackp");
        int senderIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:ContextPart", "pc");

        var homeOOP = _heapManager.GetInstanceVariable(blockContextOOP, homeIndex);
        var startPcOOP = _heapManager.GetInstanceVariable(blockContextOOP, startpcIndex);
        var nargsOOP = _heapManager.GetInstanceVariable(blockContextOOP, nargsIndex);

        int startPC = startPcOOP.AsSmallInteger();
        int expectedArgCount = nargsOOP.AsSmallInteger();

        // Verify argument count matches
        var argumentsOOP = argumentsArray;
        int providedArgCount = _heapManager.GetIndexedSize(argumentsOOP);
        if (providedArgCount != expectedArgCount)
        {
            // Argument count mismatch - this will cause primitive to fail
            return GlobalRegistry.NilOOP;
        }

        // Copy arguments into the block context's indexed fields
        for (int i = 0; i < providedArgCount; i++)
        {
            var argOOP = _heapManager.GetIndexedOOP(argumentsOOP, i);
            _heapManager.SetIndexedOOP(blockContextOOP, i, argOOP);
        }

        // Set up the block context for execution
        _heapManager.SetInstanceVariable(blockContextOOP, senderIndex, _activeContextOOP); // Current context becomes sender
        _heapManager.SetInstanceVariable(blockContextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(startPC)); // Set PC to start of block
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(expectedArgCount)); // Stack pointer after args

        // Save current context and switch to block context
        var previousContext = _activeContextOOP;
        _activeContextOOP = blockContextOOP;

        // Execute the block
        var result = ExecuteBytecodeLoop();

        // Restore previous context
        _activeContextOOP = previousContext;

        return result;
    }

    /// <summary>
    /// Adds an Association to a class's classPool Dictionary if it doesn't already exist.
    /// Implements lazy class variable initialization: on first store, add the Association to classPool.
    /// </summary>
    private void AddAssociationToClassPoolIfNeeded(SmalltalkOOP classOOP, SmalltalkOOP associationOOP)
    {
        // For metaclasses, class variables are stored in the non-meta class's classPool
        // e.g., Float class (metaclass) shares class variables with Float (regular class)
        var actualClassOOP = classOOP;
        
        // Check if this is a metaclass by checking if it's an instance of Metaclass
        var classOfClassOOP = _heapManager.GetClassOOP(classOOP);
        
        if (GlobalRegistry.TryGet("class:Metaclass", out SmalltalkOOP metaclassOOP))
        {
            if (classOfClassOOP == metaclassOOP)
            {
                // This is a metaclass - get the regular class it's a metaclass of
                // Metaclass has instance variable 'thisClass' pointing to the regular class
                try
                {
                    int thisClassIndex = _layoutResolver.GetFieldIndex("class:Metaclass", "thisClass");
                    actualClassOOP = _heapManager.GetInstanceVariable(classOOP, thisClassIndex);
                    
                    // Check actualClassOOP's instance variable count
                    var actualFormat = _heapManager.GetObjectFormat(actualClassOOP);
                }
                catch (Exception)
                {
                    return;
                }
            }
        }
        
        // Now try to find classPool instance variable (only Class objects have it)
        // Try to get the classPool - if it fails, this might be a global variable access
        int classPoolIndex;
        try
        {
            classPoolIndex = _layoutResolver.GetFieldIndex("class:Class", "classPool");
        }
        catch (Exception)
        {
            // Not a Class object - skip (likely a global variable)
            return;
        }
        
        // Verify actualClassOOP has enough instance variables for classPool
        var format = _heapManager.GetObjectFormat(actualClassOOP);
        if (format != ObjectFormat.Pointer && format != ObjectFormat.PointerIndexed)
        {
            // Not an object with instance variables - skip
            return;
        }
        
        // Try to get classPool - catch any out of range errors
        SmalltalkOOP classPoolOOP;
        try
        {
            classPoolOOP = _heapManager.GetInstanceVariable(actualClassOOP, classPoolIndex);
        }
        catch (Exception)
        {
            // classPool index out of range - not a Class object (likely global variable access)
            return;
        }
        
        // Check if classPool is nil (shouldn't happen - Phase4 should have created it)
        var nilOOP = GlobalRegistry.NilOOP;
        if (classPoolOOP == nilOOP)
        {
            return;
        }
        
        // Get the Association's key (variable name Symbol)
        int keyIndex = _layoutResolver.GetFieldIndex("class:Association", "key");
        var keyOOP = _heapManager.GetInstanceVariable(associationOOP, keyIndex);
        
        // Use atomic insert-if-absent to add Association to classPool
        // This handles both first-time insertion and idempotent re-insertion
        var winnerAssocOOP = DictionaryAtomicInsertIfAbsent(classPoolOOP, keyOOP, associationOOP);
        
        // Validate canonicalization: winner should be same as our Association
        // With Phase8 canonicalization, all methods share the same Association OOP
        if (winnerAssocOOP != associationOOP)
        {
            // CANONICALIZATION VIOLATION DETECTED
            // This means either:
            // 1. Phase8 canonicalization failed (bug in loader)
            // 2. Someone created a non-canonical Association at runtime (illegal)
            // 
            // We keep the existing winner (first-writer-wins) but this indicates a serious bug
        }
    }
    
    /// <summary>
    /// Sets an Association's value field with proper write-barrier discipline.
    /// 
    /// loaderMode controls write-barrier usage:
    /// - true: Bypass write-barrier (loader-only, single-threaded, before GC enabled)
    /// - false: Use write-barrier (runtime, GC may be active)
    /// 
    /// CRITICAL: Runtime code MUST use loaderMode=false to ensure GC correctness.
    /// </summary>
    private void SetAssociationValue(SmalltalkOOP associationOOP, SmalltalkOOP valueOOP, bool loaderMode)
    {
        int valueSlotIndex = _layoutResolver.GetFieldIndex("class:Association", "value");
        
        if (loaderMode)
        {
            // Loader-only: single-threaded, no GC active, can skip barrier
            _heapManager.SetInstanceVariableNoBarrier(associationOOP, valueSlotIndex, valueOOP);
        }
        else
        {
            // Runtime: GC may be active, must use write barrier
            _heapManager.SetInstanceVariableWithBarrier(associationOOP, valueSlotIndex, valueOOP);
        }
    }
    
    /// <summary>
    /// Allocates an empty Dictionary for lazy classPool initialization.
    /// Dictionary has instance variables: tally (SmallInteger 0), plus indexed array for associations.
    /// Initial size chosen to accommodate typical class variable counts with reasonable load factor.
    /// </summary>
    private SmalltalkOOP AllocateEmptyClassPoolDictionary()
    {
        const int initialSize = 8;
        
        if (!GlobalRegistry.TryGet("class:Dictionary", out SmalltalkOOP dictClassOOP))
        {
            throw new InvalidOperationException("Dictionary class not found - needed for classPool lazy initialization");
        }
        
        int instVarCount = _layoutResolver.GetInstanceVariableCount("class:Dictionary");
        
        var dictOOP = _heapManager.AllocateObject(
            Smalltalk80.Interpreter.Core.Objects.ObjectFormat.PointerIndexed,
            instVarCount,
            initialSize
        );
        
        _heapManager.SetSlot(dictOOP, 0, dictClassOOP);
        
        int tallyIndex = _layoutResolver.GetFieldIndex("class:Dictionary", "tally");
        if (tallyIndex >= 0)
        {
            _heapManager.SetInstanceVariable(dictOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(0));
        }
        
        var nilOOP = GlobalRegistry.NilOOP;
        for (int i = 0; i < initialSize; i++)
        {
            _heapManager.SetIndexedOOP(dictOOP, i, nilOOP);
        }
        
        return dictOOP;
    }
    
    /// <summary>
    /// Atomically inserts Association into Dictionary if key doesn't exist.
    /// Returns the winning Association OOP (either newly inserted or existing).
    /// 
    /// Provides insert-if-absent semantics with proper synchronization:
    /// - If key exists, returns existing Association OOP (no modification)
    /// - If key doesn't exist, inserts Association and returns it
    /// - Thread-safe for Smalltalk-80 single-threaded VM (synchronization ready for future)
    /// </summary>
    private SmalltalkOOP DictionaryAtomicInsertIfAbsent(SmalltalkOOP dictionaryOOP, SmalltalkOOP keyOOP, SmalltalkOOP associationOOP)
    {
        int keyIndex = _layoutResolver.GetFieldIndex("class:Association", "key");
        int size = _heapManager.GetIndexedSize(dictionaryOOP);
        
        // NOTE: Smalltalk-80 VM is single-threaded, so we don't need actual atomics.
        // However, this API provides the correct semantics for future concurrent VMs.
        
        // PHASE 1: Check if key already exists
        for (int i = 0; i < size; i++)
        {
            var existingAssocOOP = _heapManager.GetIndexedOOP(dictionaryOOP, i);
            if (existingAssocOOP == GlobalRegistry.NilOOP)
                continue;
                
            var existingKeyOOP = _heapManager.GetInstanceVariable(existingAssocOOP, keyIndex);
            if (existingKeyOOP == keyOOP)
            {
                // Key exists - return existing Association (winner)
                return existingAssocOOP;
            }
        }
        
        // PHASE 2: Key doesn't exist - insert new Association
        for (int i = 0; i < size; i++)
        {
            var slotOOP = _heapManager.GetIndexedOOP(dictionaryOOP, i);
            if (slotOOP == GlobalRegistry.NilOOP)
            {
                // Found empty slot - insert here
                _heapManager.SetIndexedOOP(dictionaryOOP, i, associationOOP);
                
                // Increment tally
                int tallyIndex = _layoutResolver.GetFieldIndex("class:Dictionary", "tally");
                var tallyOOP = _heapManager.GetInstanceVariable(dictionaryOOP, tallyIndex);
                int tally = tallyOOP.AsSmallInteger();
                _heapManager.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(tally + 1));
                
                // Return the newly inserted Association (winner)
                return associationOOP;
            }
        }
        
        // No space - Dictionary needs rehashing
        throw new InvalidOperationException(
            $"Dictionary {dictionaryOOP} is full (size={size}). Rehashing not yet implemented.");
    }

    IEnumerable<SmalltalkOOP> IPrimitiveContext.GetAllObjects()
    {
        // TODO: Implement proper enumeration of all objects in the object table
        // For now, return empty enumerable
        return Enumerable.Empty<SmalltalkOOP>();
    }

    void IPrimitiveContext.BecomeOneWay(SmalltalkOOP fromOop, SmalltalkOOP toOop)
    {
        // TODO: Implement BecomeOneWay in heap manager
        // This needs to replace all references to fromOop with toOop
    }

    void IPrimitiveContext.BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2)
    {
        // TODO: Implement BecomeTwoWay in heap manager
        // This needs to swap all references between oop1 and oop2
    }
}