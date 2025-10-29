using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Attributes;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// The Smalltalk-80 Virtual Machine - handles bytecode execution, message sending, and method dispatch.
/// Uses ISmalltalkHeapManager for object storage and LayoutResolver for dynamic field access.
/// Consistently uses SmalltalkOOP for ALL object references to avoid OOP↔int conversions.
///
/// RUNTIME ONLY: This class must ONLY access the Smalltalk heap via ISmalltalkHeapManager.
/// NO access to cold start proxy data (_symbols[], ColdStartBundle, unfrozen registries).
/// </summary>
[RuntimeOnly(Reason = "Executes Smalltalk bytecode at runtime, must use heap-only access")]
public partial class VirtualMachine : IPrimitiveContext
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly VMMetadata _metadata;
    private readonly IPlatformBridge? _platformBridge;
    private readonly DebugOptions _debugOptions;

    // Active context (current MethodContext being executed) - now using SmalltalkOOP
    private SmalltalkOOP _activeContextOOP;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    // Context depth tracking to prevent CLR stack overflow
    private int _activeContextCount = 0;
    private const int MAX_CONTEXT_DEPTH = 500;  // Lowered to capture diagnostics before CLR overflow

    // Cursor creation loop diagnostics
    private int _cursorCreationCount = 0;

    // Call outcome used for NLR propagation between SendMessage and bytecode loop
    private enum CallOutcomeKind { Normal, NonLocalReturn, VMException }

    private readonly struct CallOutcome
    {
        public readonly CallOutcomeKind Kind;
        public readonly SmalltalkOOP Value;
        public readonly SmalltalkOOP TargetContextOop;
        public readonly Exception? VMEx;

        private CallOutcome(CallOutcomeKind kind, SmalltalkOOP value, SmalltalkOOP target, Exception? vmEx)
        {
            Kind = kind;
            Value = value;
            TargetContextOop = target;
            VMEx = vmEx;
        }

        public static CallOutcome Normal(SmalltalkOOP value) => new(CallOutcomeKind.Normal, value, SmalltalkOOP.Null, null);
        public static CallOutcome NonLocalReturn(SmalltalkOOP target, SmalltalkOOP value) => new(CallOutcomeKind.NonLocalReturn, value, target, null);
        public static CallOutcome VMException(Exception ex) => new(CallOutcomeKind.VMException, SmalltalkOOP.Null, SmalltalkOOP.Null, ex);
    }

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

    public VirtualMachine(ISmalltalkHeapManager heapManager, LayoutResolver layoutResolver, VMMetadata metadata, IPlatformBridge? platformBridge = null, DebugOptions? debugOptions = null)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _metadata = metadata;
        _platformBridge = platformBridge;
        _debugOptions = debugOptions ?? DebugOptions.Default;
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
    /// Looks up a symbol in the Smalltalk heap by searching USTable.
    /// CRITICAL: Uses heap-only access, no C# proxy data.
    /// </summary>
    private SmalltalkOOP LookupSymbolInHeap(string symbolName)
    {
        // Get USTable from GlobalRegistry (points to Smalltalk heap)
        if (!GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableOOP))
        {
            throw new InvalidOperationException("USTable not found - cannot initialize selector caches");
        }

        // USTable is an Array in the Smalltalk heap containing Symbol OOPs
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Convert search string to bytes (Latin-1 encoding)
        byte[] searchBytes = System.Text.Encoding.Latin1.GetBytes(symbolName);

        // Search through USTable
        for (int i = 0; i < ustableSize; i++)
        {
            var symbolOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil slots
            if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                continue;

            // Compare Symbol bytes in heap
            int symbolLength = _heapManager.GetIndexedSize(symbolOOP);
            if (symbolLength == searchBytes.Length)
            {
                bool match = true;
                for (int j = 0; j < searchBytes.Length; j++)
                {
                    byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                    if (symbolByte != searchBytes[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return symbolOOP;
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in USTable during selector cache initialization");
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
        var result = ExecuteBytecodeLoop();
        return result;
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
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
        _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP); // Set class reference

        // Increment context depth and check limit
        _activeContextCount++;

        // Diagnostic snapshots at key thresholds
        if (_activeContextCount == 100 || _activeContextCount == 250 || _activeContextCount == 400)
        {
            Console.WriteLine($"\n[DEPTH SNAPSHOT] Context depth reached {_activeContextCount}");
            DumpSenderChain(callerContextOOP, 20);
        }

        if (_activeContextCount > MAX_CONTEXT_DEPTH)
        {
            Console.WriteLine($"\n[CONTEXT DEPTH EXCEEDED] Depth: {_activeContextCount}, Limit: {MAX_CONTEXT_DEPTH}");
            Console.WriteLine($"Current receiver: {receiverOOP}, method: {methodOOP}");
            DumpSenderChain(callerContextOOP, 30);

            throw new InvalidOperationException(
                $"Context depth limit exceeded: {_activeContextCount} contexts (limit: {MAX_CONTEXT_DEPTH}). " +
                $"This typically indicates infinite recursion or primitive failure causing excessive message sends.");
        }

        // Step 5: Get field indices using LayoutResolver
        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverMapIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiverMap");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        // Step 6: Initialize MethodContext fixed fields

        // sender: caller context or nil for top-level DoIts
        var callerOOP = callerContextOOP == default ? SpecialObjectsArray.Instance.Nil : callerContextOOP;
        _heapManager.SetInstanceVariable(contextOOP, senderIndex, callerOOP);

        // pc: start at bytecode offset 0
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));

        // stackp: points after args+temps (start of execution stack)
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

        // method: the CompiledMethod being executed
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);

        // receiverMap: used for multiple inheritance (not implemented yet, set to nil)
        _heapManager.SetInstanceVariable(contextOOP, receiverMapIndex, SpecialObjectsArray.Instance.Nil);

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
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Initialize execution stack slots to nil
        for (int i = header.NumArgs + header.NumTemps; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
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
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

        var methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);

        // Diagnostic: Show method name when recursion depth > 10
        if (_activeContextCount > 10 && _activeContextCount <= 15)  // Limit output
        {
            var selectorOOP = GetMethodLiteral(methodOOP, 0);
            var selectorName = GetSymbolName(selectorOOP);
            var receiverClass = GetClass(receiverOOP);
            var className = GetClassName(receiverClass);

            // Debug: show what literal[0] actually is
            string literalInfo = "";
            if (selectorOOP == SpecialObjectsArray.Instance.Nil)
            {
                literalInfo = " [literal[0]=nil]";
            }
            else if (selectorOOP.IsSmallInteger)
            {
                literalInfo = $" [literal[0]=SmallInteger({selectorOOP.AsSmallInteger()})]";
            }
            else
            {
                var litClass = GetClassName(GetClass(selectorOOP));
                literalInfo = $" [literal[0]={litClass}, name='{selectorName}']";
            }

            Console.WriteLine($"[DEPTH={_activeContextCount}] {className}>>{selectorName}{literalInfo}");
        }

        // Validate that methodOOP is a CompiledMethod (PointerIndexed), not a bytecode array (ByteIndexed)
        var methodFormat = _heapManager.GetObjectFormat(methodOOP);
        if (methodFormat != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            var contextClass = GetClassName(GetClass(_activeContextOOP));
            throw new InvalidOperationException(
                $"ExecuteBytecodeLoop: method field in context contains non-PointerIndexed object!\n" +
                $"  Context: {_activeContextOOP}, contextClass={contextClass}\n" +
                $"  Method field (index {methodIndex}): {methodOOP}\n" +
                $"  Method format: {methodFormat} (expected PointerIndexed)\n" +
                $"  Method class: {className}\n" +
                $"  Method localId: {localId}\n" +
                $"  This suggests the bytecode array was stored in the context instead of the CompiledMethod.");
        }
        // TODO: Implement GetMethodBytecodes in heap manager or add helper method
        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

            // For extended opcodes, peek ahead and show the full instruction
            string opcodeName;
            string opcodeBytes;
            if (bytecode == 0x80 && ip < bytecodes.Length)
            {
                byte subOp = bytecodes[ip];
                string[] extendedNames = new[] {
                    "PUSH_IVAR", "STORE_IVAR", "POP_STORE_IVAR", "PUSH_TEMP", "STORE_TEMP", "POP_STORE_TEMP",
                    "PUSH_LITERAL", "PUSH_LITERAL_VAR", "STORE_LITERAL_VAR", "POP_STORE_LITERAL_VAR",
                    "SEND_0", "SEND_1", "SEND_2", "SEND_N", "SEND_SUPER_0", "SEND_SUPER_1",
                    "SEND_SUPER_2", "SEND_SUPER_N", "POP", "DUP", "JUMP_TRUE", "JUMP_FALSE",
                    "JUMP", "JUMP_LONG_TRUE", "JUMP_LONG_FALSE", "JUMP_LONG"
                };
                opcodeName = subOp < extendedNames.Length ? extendedNames[subOp] : $"EXTENDED_UNKNOWN";
                opcodeBytes = $"0x{bytecode:X2} 0x{subOp:X2}";
            }
            else
            {
                opcodeName = BytecodeDisassembler.GetOpcodeName(bytecode);
                opcodeBytes = $"0x{bytecode:X2}";
            }

            if (_debugOptions.ShouldDebugBytecode)
            {
                Console.WriteLine($"[BYTECODE EXEC] context={_activeContextOOP}, ip={ip-1}, bytecode={opcodeName} ({opcodeBytes})");
            }

            // Update IP
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Execute the bytecode
            bool continueExecution = ExecuteBytecode(bytecode, methodOOP);

            if (!continueExecution)
            {
                if (_debugOptions.ShouldDebugBytecode)
                {
                    Console.WriteLine($"[BYTECODE RETURN] context={_activeContextOOP}, returning from bytecode loop");
                }
                // Method returned, pop the result and return it
                return Pop();
            }

            // Re-read IP in case bytecode modified it (jumps)
            ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self (receiverIndex and receiverOOP already declared at top)
        return receiverOOP;
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        // DEBUG: Track bytecode execution for debugging
        if (bytecode == 0x10)
        {
            if (_debugOptions.ShouldDebugBytecode)
            {
                Console.WriteLine($"[DEBUG ExecuteBytecode] bytecode=0x{bytecode:X2}, IsPushIvar={BytecodeUtil.IsPushIvar(bytecode)}, IsPushTemp={BytecodeUtil.IsPushTemp(bytecode)}");
            }
        }

        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);

            // Console.WriteLine($"[DEBUG IsPushIvar] bytecode=0x{bytecode:X2}, index={index}, receiverOOP={receiverOOP}");

            // For blocks, receiver should be the home context receiver
            var actualReceiverOOP = receiverOOP;
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOOP))
            {
                // Check if this is a BlockClosure or BlockContext
                var blockClass = GetClassName(GetClass(blockOOP));
                SmalltalkOOP homeContextOOP;

                if (blockClass == "BlockClosure")
                {
                    // New BlockClosure path - get homeContext from BlockClosure
                    int homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                    homeContextOOP = _heapManager.GetInstanceVariable(blockOOP, homeIndex);
                    // Console.WriteLine($"[DEBUG IsPushIvar] BlockClosure path - homeContext={homeContextOOP}");
                }
                else
                {
                    // Legacy BlockContext path
                    int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    homeContextOOP = _heapManager.GetInstanceVariable(blockOOP, homeIndex);
                    // Console.WriteLine($"[DEBUG IsPushIvar] BlockContext path - homeContext={homeContextOOP}");
                }

                actualReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                // Console.WriteLine($"[DEBUG IsPushIvar] Block - actualReceiverOOP={actualReceiverOOP}");
            }

            var valueOOP = _heapManager.GetInstanceVariable(actualReceiverOOP, index);
            // Console.WriteLine($"[DEBUG IsPushIvar] Retrieved value: {valueOOP}, class={GetClassName(GetClass(valueOOP))}");
            Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        // Temporaries are ALWAYS in the active MethodContext's indexed slots (args + temps)
        // They are NOT affected by BlockContext - blocks share the home method's temporaries
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            // For block execution contexts, check if we need to access home context temps
            SmalltalkOOP contextToReadFrom = _activeContextOOP;

            // Check if current context is a block execution context
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockContextOOP))
            {
                // This is a block - check if index is out of range for block's own temps
                // Get the method to check arg+temp count
                var methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                var blockMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
                var header = ParseMethodHeader(blockMethodOOP);
                int blockLocalCount = header.NumArgs + header.NumTemps;

                if (_debugOptions.ShouldDebugBytecode)
                {
                    Console.WriteLine($"[DEBUG pushTemp] index={index}, blockLocalCount={blockLocalCount}");
                }

                // If accessing beyond block's local scope, read from home context
                if (index >= blockLocalCount)
                {
                    // Check if blockContextOOP is a BlockClosure or BlockContext
                    var blockClass = GetClassName(GetClass(blockContextOOP));
                    if (blockClass == "BlockClosure")
                    {
                        var homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                        contextToReadFrom = _heapManager.GetInstanceVariable(blockContextOOP, homeIndex);
                        if (_debugOptions.ShouldDebugBytecode)
                        {
                            Console.WriteLine($"[DEBUG pushTemp] BlockClosure: Reading from home context: {contextToReadFrom}");
                        }
                    }
                    else
                    {
                        var homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                        contextToReadFrom = _heapManager.GetInstanceVariable(blockContextOOP, homeIndex);
                        if (_debugOptions.ShouldDebugBytecode)
                        {
                            Console.WriteLine($"[DEBUG pushTemp] BlockContext: Reading from home context: {contextToReadFrom}");
                        }
                    }
                }
                else
                {
                    if (_debugOptions.ShouldDebugBytecode)
                    {
                        Console.WriteLine($"[DEBUG pushTemp] Reading from block's own stack");
                    }
                }
            }

            var valueOOP = _heapManager.GetIndexedOOP(contextToReadFrom, index);
            if (_debugOptions.ShouldDebugBytecode)
            {
                Console.WriteLine($"[DEBUG pushTemp] Retrieved temp[{index}] = {valueOOP}, class={GetClassName(GetClass(valueOOP))}");
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

            // Check if this literal is a block - wrap in BlockClosure
            if (IsBlockLiteral(literal))
            {
                // Determine the home context for this block
                SmalltalkOOP homeContextOOP = _activeContextOOP;

                // If we're currently executing inside a block, use that block's homeContext
                // instead of the block's own execution context. This ensures nested blocks
                // share the same home context as their parent.
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var parentBlockClosureOOP))
                {
                    var parentClass = GetClassName(GetClass(parentBlockClosureOOP));
                    // Console.WriteLine($"[DEBUG CreateBlock] Found parent object: {parentBlockClosureOOP}, class={parentClass}");
                    int homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                    // Console.WriteLine($"[DEBUG CreateBlock] homeContext field index: {homeIndex}");
                    var parentHomeContextOOP = _heapManager.GetInstanceVariable(parentBlockClosureOOP, homeIndex);
                    // Console.WriteLine($"[DEBUG CreateBlock] Active context is block - using parent's home: {_activeContextOOP} -> {parentHomeContextOOP}");
                    homeContextOOP = parentHomeContextOOP;
                }
                else
                {
                    // Console.WriteLine($"[DEBUG CreateBlock] Active context is method - using directly: {homeContextOOP}");
                }

                var closureOOP = CreateBlockClosure(literal, homeContextOOP);
                Push(closureOOP);
            }
            else
            {
                Push(literal);
            }
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            // Handle blocks with nil literals - resolve from home context
            if (associationOOP == SpecialObjectsArray.Instance.Nil) // nil
            {
                
                // Check if this is a block execution context using our mapping
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockClosureOOP))
                {
                    
                    // Get the home context from the block
                    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    var homeContextOOP = _heapManager.GetInstanceVariable(blockClosureOOP, homeContextIndex);
                    
                    if (homeContextOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Get the method from home context
                        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var homeMethodOOP = _heapManager.GetInstanceVariable(homeContextOOP, methodIndex);
                        
                        // Try to get the association from home method's literals
                        var homeAssociationOOP = GetMethodLiteral(homeMethodOOP, index + 1);
                        
                        
                        if (homeAssociationOOP != SpecialObjectsArray.Instance.Nil)
                        {
                            associationOOP = homeAssociationOOP;
                        }
                        else
                        {
                            Push(SpecialObjectsArray.Instance.Nil);
                            return true;
                        }
                    }
                    else
                    {
                        Push(SpecialObjectsArray.Instance.Nil);
                        return true;
                    }
                }
                else
                {
                    Push(SpecialObjectsArray.Instance.Nil);
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
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOOP))
            {
                // Check if this is a BlockClosure or BlockContext
                var blockClass = GetClassName(GetClass(blockOOP));
                SmalltalkOOP homeContextOOP;

                if (blockClass == "BlockClosure")
                {
                    // New BlockClosure path - get homeContext from BlockClosure
                    int homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                    homeContextOOP = _heapManager.GetInstanceVariable(blockOOP, homeIndex);
                }
                else
                {
                    // Legacy BlockContext path
                    int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    homeContextOOP = _heapManager.GetInstanceVariable(blockOOP, homeIndex);
                }

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

            // For block execution contexts, check if we need to store to home context
            SmalltalkOOP contextToStoreTo = _activeContextOOP;

            // Check if current context is a block execution context
            if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOrClosureOOP))
            {
                // This is a block - check if index is out of range for block's own temps
                var methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                var blockMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
                var header = ParseMethodHeader(blockMethodOOP);
                int blockLocalCount = header.NumArgs + header.NumTemps;

                // If storing beyond block's local scope, store to home context
                if (index >= blockLocalCount)
                {
                    // Check if it's a BlockClosure or BlockContext
                    var blockClass = GetClassName(GetClass(blockOrClosureOOP));
                    if (blockClass == "BlockClosure")
                    {
                        var homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                        contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeIndex);
                    }
                    else
                    {
                        var homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                        contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeIndex);
                    }
                }
            }

            _heapManager.SetIndexedOOP(contextToStoreTo, index, valueOOP);

            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                // For blocks, self should be the home context receiver, not the block itself
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOOP2))
                {
                    var blockClass = GetClassName(GetClass(blockOOP2));
                    SmalltalkOOP homeContextOOP;

                    if (blockClass == "BlockClosure")
                    {
                        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                        homeContextOOP = _heapManager.GetInstanceVariable(blockOOP2, homeIndex);
                    }
                    else
                    {
                        // Legacy BlockContext path
                        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                        homeContextOOP = _heapManager.GetInstanceVariable(blockOOP2, homeIndex);
                    }

                    var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                    Push(homeReceiverOOP);
                }
                else
                {
                    // Normal method execution
                    Push(receiverOOP);
                }
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
                // For blocks, return home context receiver as self
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var returnBlockOOP))
                {
                    var blockClass = GetClassName(GetClass(returnBlockOOP));
                    SmalltalkOOP homeContextOOP;

                    if (blockClass == "BlockClosure")
                    {
                        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                        homeContextOOP = _heapManager.GetInstanceVariable(returnBlockOOP, homeIndex);
                    }
                    else
                    {
                        // Legacy BlockContext path
                        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                        homeContextOOP = _heapManager.GetInstanceVariable(returnBlockOOP, homeIndex);
                    }

                    var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                    Push(homeReceiverOOP);
                }
                else
                {
                    Push(receiverOOP);
                }
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
                // Return self from block context to caller's caller (0x7D)
                // Non-local return - unwind to block's return context
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var returnSelfPopBlockOOP))
                {
                    var blockClass = GetClassName(GetClass(returnSelfPopBlockOOP));
                    if (blockClass == "BlockClosure")
                    {
                        // Non-local return support for BlockClosure (return self)
                        // Get self from home context
                        int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                        var homeContextOOP = _heapManager.GetInstanceVariable(returnSelfPopBlockOOP, homeContextIndex);
                        var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);

                        // Get return context
                        int returnContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "returnContext");
                        var returnContextOOP = _heapManager.GetInstanceVariable(returnSelfPopBlockOOP, returnContextIndex);

                        // Console.WriteLine($"[NON-LOCAL RETURN] Throwing exception to unwind to context {returnContextOOP}, value=self ({homeReceiverOOP})");

                        // THROW NON-LOCAL RETURN EXCEPTION
                        // This will unwind the call stack back to the target context
                        throw new NonLocalReturnException(homeReceiverOOP, returnContextOOP);
                    }
                    else
                    {
                        // Legacy BlockContext path - treat as normal return
                        int homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                        var homeContextOOP = _heapManager.GetInstanceVariable(returnSelfPopBlockOOP, homeIndex);
                        var homeReceiverOOP = GetHomeContextReceiver(homeContextOOP);
                        Push(homeReceiverOOP);
                        return false;
                    }
                }
                else
                {
                    // Normal method - return self
                    Push(receiverOOP);
                }
                return false;

            case Bytecode.RETURN_TOP_POP:
                // Pop and return from block context to caller's caller (0x7E)
                // Non-local return - unwind to block's return context
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var returnTopPopBlockOOP))
                {
                    var blockClass = GetClassName(GetClass(returnTopPopBlockOOP));
                    if (blockClass == "BlockClosure")
                    {
                        // Non-local return support for BlockClosure
                        // Value already on stack
                        var returnValue = Pop();

                        // Get return context
                        int returnContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "returnContext");
                        var returnContextOOP = _heapManager.GetInstanceVariable(returnTopPopBlockOOP, returnContextIndex);

                        // Console.WriteLine($"[NON-LOCAL RETURN] Throwing exception to unwind to context {returnContextOOP}, value={returnValue}");

                        // THROW NON-LOCAL RETURN EXCEPTION
                        // This will unwind the call stack back to the target context
                        throw new NonLocalReturnException(returnValue, returnContextOOP);
                    }
                    else
                    {
                        // Legacy BlockContext path - treat as normal return
                        return false;
                    }
                }
                // Value already on stack, normal method return
                return false;

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

            // Handle blocks with nil literals - resolve from home context
            if (selectorOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Check if this is a block execution context using our mapping
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockClosureOOP))
                {
                    // Get the home context from the block
                    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    var homeContextOOP = _heapManager.GetInstanceVariable(blockClosureOOP, homeContextIndex);

                    if (homeContextOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Get the method from home context
                        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var homeMethodOOP = _heapManager.GetInstanceVariable(homeContextOOP, methodIndex);

                        // Try to get the selector from home method's literals
                        var homeSelectorOOP = GetMethodLiteral(homeMethodOOP, selectorIndex + 1);

                        if (homeSelectorOOP != SpecialObjectsArray.Instance.Nil)
                        {
                            selectorOOP = homeSelectorOOP;
                        }
                    }
                }
            }

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

            // Handle blocks with nil literals - resolve from home context
            if (selectorOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Check if this is a block execution context using our mapping
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockClosureOOP))
                {
                    // Get the home context from the block
                    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    var homeContextOOP = _heapManager.GetInstanceVariable(blockClosureOOP, homeContextIndex);

                    if (homeContextOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Get the method from home context
                        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var homeMethodOOP = _heapManager.GetInstanceVariable(homeContextOOP, methodIndex);

                        // Try to get the selector from home method's literals
                        var homeSelectorOOP = GetMethodLiteral(homeMethodOOP, selectorIndex + 1);

                        // Console.WriteLine($"[SEND1] Block literal fallback: blockMethod={GetLocalId(methodOOP)}, homeMethod={GetLocalId(homeMethodOOP)}");
                        Console.WriteLine($"  Accessing homeMethod literal[{selectorIndex + 1}] = {homeSelectorOOP}, symbolName={GetSymbolName(homeSelectorOOP)}");

                        if (homeSelectorOOP != SpecialObjectsArray.Instance.Nil)
                        {
                            selectorOOP = homeSelectorOOP;
                        }
                    }
                }
            }

            var argOOP = Pop();
            var receiverOOP2 = Pop();
            // Console.WriteLine($"[SEND1 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_activeContextOOP}");
            var outcome1 = SendMessage(receiverOOP2, selectorOOP, new[] { argOOP });
            // Console.WriteLine($"[SEND1 AFTER] Message outcome.Kind={outcome1.Kind}, context={_activeContextOOP}");
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

            // Handle blocks with nil literals - resolve from home context
            if (selectorOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Check if this is a block execution context using our mapping
                if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockClosureOOP))
                {
                    // Get the home context from the block
                    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                    var homeContextOOP = _heapManager.GetInstanceVariable(blockClosureOOP, homeContextIndex);

                    if (homeContextOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Get the method from home context
                        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var homeMethodOOP = _heapManager.GetInstanceVariable(homeContextOOP, methodIndex);

                        // Try to get the selector from home method's literals
                        var homeSelectorOOP = GetMethodLiteral(homeMethodOOP, selectorIndex + 1);

                        if (homeSelectorOOP != SpecialObjectsArray.Instance.Nil)
                        {
                            selectorOOP = homeSelectorOOP;
                        }
                    }
                }
            }

            var arg2OOP = Pop();
            var arg1OOP = Pop();
            var receiverOOP2 = Pop();
            // Console.WriteLine($"[SEND2 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_activeContextOOP}");
            var outcome2 = SendMessage(receiverOOP2, selectorOOP, new[] { arg1OOP, arg2OOP });
            // Console.WriteLine($"[SEND2 AFTER] Message outcome.Kind={outcome2.Kind}, context={_activeContextOOP}");
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

                    // For block execution contexts, check if we need to access home context temps
                    SmalltalkOOP contextToReadFrom = _activeContextOOP;
                    if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOrClosureOOP))
                    {
                        var methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var blockMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
                        var header = ParseMethodHeader(blockMethodOOP);
                        int blockLocalCount = header.NumArgs + header.NumTemps;

                        if (index >= blockLocalCount)
                        {
                            // Check if it's a BlockClosure or BlockContext
                            var objClass = _heapManager.GetSlot(blockOrClosureOOP, 0);
                            if (GlobalRegistry.TryGet("class:BlockClosure", out var blockClosureClassOOP) && objClass == blockClosureClassOOP)
                            {
                                // It's a BlockClosure - get homeContext field
                                var homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                                contextToReadFrom = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeContextIndex);
                            }
                            else
                            {
                                // It's a BlockContext - get home field
                                var homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                                contextToReadFrom = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeIndex);
                            }
                        }
                    }

                    var valueOOP = _heapManager.GetIndexedOOP(contextToReadFrom, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();

                    // For block execution contexts, check if we need to store to home context
                    SmalltalkOOP contextToStoreTo = _activeContextOOP;
                    if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOrClosureOOP))
                    {
                        var methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var blockMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
                        var header = ParseMethodHeader(blockMethodOOP);
                        int blockLocalCount = header.NumArgs + header.NumTemps;

                        if (index >= blockLocalCount)
                        {
                            // Check if it's a BlockClosure or BlockContext
                            var objClass = _heapManager.GetSlot(blockOrClosureOOP, 0);
                            if (GlobalRegistry.TryGet("class:BlockClosure", out var blockClosureClassOOP) && objClass == blockClosureClassOOP)
                            {
                                // It's a BlockClosure - get homeContext field
                                var homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                                contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeContextIndex);
                            }
                            else
                            {
                                // It's a BlockContext - get home field
                                var homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                                contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeIndex);
                            }
                        }
                    }

                    _heapManager.SetIndexedOOP(contextToStoreTo, index, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();

                    // For block execution contexts, check if we need to store to home context
                    SmalltalkOOP contextToStoreTo = _activeContextOOP;
                    if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOrClosureOOP))
                    {
                        var methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
                        var blockMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
                        var header = ParseMethodHeader(blockMethodOOP);
                        int blockLocalCount = header.NumArgs + header.NumTemps;

                        if (index >= blockLocalCount)
                        {
                            // Check if it's a BlockClosure or BlockContext
                            var objClass = _heapManager.GetSlot(blockOrClosureOOP, 0);
                            if (GlobalRegistry.TryGet("class:BlockClosure", out var blockClosureClassOOP) && objClass == blockClosureClassOOP)
                            {
                                // It's a BlockClosure - get homeContext field
                                var homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
                                contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeContextIndex);
                            }
                            else
                            {
                                // It's a BlockContext - get home field
                                var homeIndex = _layoutResolver.GetFieldIndex("class:BlockContext", "home");
                                contextToStoreTo = _heapManager.GetInstanceVariable(blockOrClosureOOP, homeIndex);
                            }
                        }
                    }

                    _heapManager.SetIndexedOOP(contextToStoreTo, index, valueOOP);
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
                    var outcome = SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
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
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
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
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
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
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
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

                    if (valueOOP == SpecialObjectsArray.Instance.True)
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

                    if (valueOOP == SpecialObjectsArray.Instance.False)
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

                    if (valueOOP == SpecialObjectsArray.Instance.True)
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

                    if (valueOOP == SpecialObjectsArray.Instance.False)
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
            if (resultOOP != SpecialObjectsArray.Instance.Nil ||
                selector == "=" || selector == "~=" ||
                selector == "<" || selector == ">" ||
                selector == "<=" || selector == ">=")
            {
                Push(resultOOP);
                return;
            }
        }

        // Fall back to message send using cached selector OOP
        var outcome = SendMessage(receiverOOP, selectorOOP, new[] { argOOP });
        if (!ConsumeCallOutcome(outcome))
            return; // method returned via NLR targeting this context
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
            Push(isIdentical ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False);
            return;
        }

        if (selectorIndex == 7) // class
        {
            var classOOP = GetClass(receiverOOP);
            Push(classOOP);
            return;
        }

        // Fall back to message send using cached selector OOP
        var outcome = SendMessage(receiverOOP, selectorOOP, args);
        if (!ConsumeCallOutcome(outcome))
            return; // method returned via NLR targeting this context
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

    /// <summary>
    /// Sends a super message to a receiver.
    /// Looks up the method starting from the superclass of the currently executing method's home class.
    /// Returns a CallOutcome that signals whether a non-local return occurred.
    /// </summary>
    private CallOutcome SendSuperMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // For super sends, we need to start method lookup from the superclass of the class
        // that contains the currently executing method, NOT from the receiver's class.

        // CRITICAL: When executing inside a BlockContext, _activeContextOOP points to the BlockContext,
        // not the enclosing MethodContext. We need to find the actual MethodContext to get the correct
        // method for super send lookup.
        var methodContextOOP = FindMethodContext(_activeContextOOP);
        if (methodContextOOP == default)
        {
            throw new InvalidOperationException($"Cannot find MethodContext for super send - active context may be invalid");
        }

        // Get the currently executing method from the method context
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        var currentMethodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);

        // Get the class that owns this method (the home class)
        // CompiledMethod structure: fixed slot 0 = homeClass, indexed slots = [header] [literals...] [bytecodeArray]
        // The home class is stored in fixed slot 0
        var homeClassOOP = _heapManager.GetInstanceVariable(currentMethodOOP, 0);

        // Get the superclass of the home class
        int superclassIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass");
        var superclassOOP = _heapManager.GetInstanceVariable(homeClassOOP, superclassIndex);

        // Get selector name for diagnostics
        string? selectorName = GetSymbolName(selectorOOP);

        // Console.WriteLine($"[SUPER SEND] selector={selectorName}, homeClass={GetClassName(homeClassOOP)}, superclass={GetClassName(superclassOOP)}");

        // Now look up the method starting from the superclass
        var methodInfo = GetMethod(superclassOOP, selectorName!);

        if (methodInfo == null)
        {
            string? superclassName = GetClassName(superclassOOP);
            throw new InvalidOperationException(
                $"Super method not found: {selectorName ?? "<unknown>"} in superclass {superclassName} (superclassOOP={superclassOOP})");
        }

        // Check if the method has a primitive
        int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodOOP);
        if (primitiveNumber > 0)
        {
            Console.WriteLine($"[SendSuperMessage] Executing primitive {primitiveNumber} for {selectorName}");
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                Console.WriteLine($"[SendSuperMessage] Primitive {primitiveNumber} succeeded");
                return CallOutcome.Normal(primitiveResult);
            }

            Console.WriteLine($"[SendSuperMessage] Primitive {primitiveNumber} failed, executing bytecode fallback");
        }

        // Execute the method with proper context chaining
        var previousContextOOP = _activeContextOOP;
        _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, previousContextOOP);

        try
        {
            var result = ExecuteBytecodeLoop();
            _activeContextOOP = previousContextOOP;
            _activeContextCount--;

            if (_activeContextCount % 50 == 0 && _activeContextCount > 0)
            {
                Console.WriteLine($"[CONTEXT EXIT] Normal return from super send, depth now={_activeContextCount}");
            }

            return CallOutcome.Normal(result);
        }
        catch (NonLocalReturnException ex)
        {
            // Console.WriteLine($"[NLR CAUGHT] super method={selectorName}, targetContext={ex.TargetContext}, value={ex.ReturnValue}");
            _activeContextOOP = previousContextOOP;
            _activeContextCount--;
            // Console.WriteLine($"[NLR EXIT] depth now={_activeContextCount}, restored to context={_activeContextOOP}");
            return CallOutcome.NonLocalReturn(ex.TargetContext, ex.ReturnValue);
        }
        catch (Exception ex)
        {
            DumpMethodStateOnError(methodInfo.MethodOOP, receiverOOP, args, superclassOOP, selectorName!, ex);
            _activeContextOOP = previousContextOOP;
            _activeContextCount--;
            throw;
        }
    }

    /// <summary>
    /// Sends a message to a receiver.
    /// Looks up the method in the receiver's class and executes it.
    /// Returns a CallOutcome that signals whether a non-local return occurred.
    /// </summary>
    private CallOutcome SendMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // Look up the method in the receiver's class
        var classOOP = GetClass(receiverOOP);

        // Get selector name once for all uses
        string? selectorName = GetSymbolName(selectorOOP);

        // DEBUG: Check if receiver is a class object
        if (selectorName == "new:")
        {
            var className = GetClassName(classOOP);
            var classFormat = _heapManager.GetObjectFormat(classOOP);
            var classOfClass = _heapManager.GetClassOOP(classOOP);
            var classOfClassName = GetClassName(classOfClass);
            Console.WriteLine($"\n[new:] receiver \"{className}\" {receiverOOP} | class \"{classOfClassName}\" {classOOP} | format={classFormat}, iVars={_heapManager.GetInstanceVariableCount(classOOP)}");
        }

        // DEBUG: Log when selector is <unknown>
        if (selectorName == null)
        {
            if (_debugOptions.ShouldDebugMessages)
            {
                Console.WriteLine($"[SendMessage] WARNING: selectorOOP={selectorOOP} does not resolve to a Symbol!");
            }
            Console.WriteLine($"  receiverOOP={receiverOOP}, classOOP={classOOP}, className={GetClassName(classOOP)}");
            Console.WriteLine($"  selectorOOP.IsSmallInteger={selectorOOP.IsSmallInteger}, selectorOOP.RawValue=0x{selectorOOP.RawValue:X}");
        }

        // DIAGNOSTIC: Track repeated Cursor creation to diagnose loop
        if (selectorName == "extent:fromArray:offset:")
        {
            _cursorCreationCount++;
            Console.WriteLine($"\n[CURSOR #{_cursorCreationCount}] depth={_activeContextCount}, receiver \"{GetClassName(classOOP)}\" {receiverOOP}");

            // Show sender chain for first 10 calls
            if (_cursorCreationCount <= 10)
            {
                DumpSenderChain(_activeContextOOP, 6);
            }
        }

        // DIAGNOSTIC: Track > and max: message sends
        if (selectorName == ">")
        {
            var argInfo = args.Length > 0 ? $"{args[0]} (SmallInt={args[0].IsSmallInteger})" : "none";
            Console.WriteLine($"\n[>] receiver {receiverOOP} (SmallInt={receiverOOP.IsSmallInteger}, class=\"{GetClassName(classOOP)}\") | arg={argInfo}");
        }
        if (selectorName == "max:")
        {
            var argInfo = args.Length > 0 ? $"{args[0]}" : "none";
            Console.WriteLine($"\n[max:] receiver {receiverOOP} | arg={argInfo} | class=\"{GetClassName(classOOP)}\"");
        }

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
                return CallOutcome.Normal(InvokeBlock(receiverOOP, args));
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
            // DEBUG: Always log primitive 4 (>) to diagnose why it's not being called
            if (primitiveNumber == 4 || _debugOptions.ShouldDebugMessages)
            {
                Console.WriteLine($"[SendMessage] Executing primitive {primitiveNumber} for {selectorName}");
            }
            // Try to execute the primitive
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                if (primitiveNumber == 4 || _debugOptions.ShouldDebugMessages)
                {
                    Console.WriteLine($"[SendMessage] Primitive {primitiveNumber} succeeded");
                }
                // Primitive succeeded - return result directly
                return CallOutcome.Normal(primitiveResult);
            }

            if (primitiveNumber == 4 || _debugOptions.ShouldDebugMessages)
            {
                Console.WriteLine($"[SendMessage] Primitive {primitiveNumber} failed, executing bytecode fallback");
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
            _activeContextCount--;

            if (_activeContextCount % 50 == 0 && _activeContextCount > 0)
            {
                Console.WriteLine($"[CONTEXT EXIT] Normal return, depth now={_activeContextCount}");
            }

            return CallOutcome.Normal(result);
        }
        catch (NonLocalReturnException ex)
        {
            // Non-local return from a block - convert to CallOutcome
            // DO NOT convert to normal return - let caller decide based on target
            // Console.WriteLine($"[NLR CAUGHT] method={selectorName}, targetContext={ex.TargetContext}, value={ex.ReturnValue}, currentContext={_activeContextOOP}");

            // Restore previous context BEFORE returning outcome
            _activeContextOOP = previousContextOOP;
            _activeContextCount--;

            // Console.WriteLine($"[NLR EXIT] depth now={_activeContextCount}, restored to context={_activeContextOOP}");

            if (VerboseDebug)
            {
                _callDepth--;
            }

            // Return NonLocalReturn outcome - caller will check if target matches its context
            return CallOutcome.NonLocalReturn(ex.TargetContext, ex.ReturnValue);
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
            _activeContextCount--;

            throw;
        }
    }

    /// <summary>
    /// Consume a CallOutcome in the bytecode loop / caller.
    /// Returns true to continue execution (normal), false to indicate that the current method/context has returned (stop loop).
    /// If the outcome is a VMException, throws it. If the outcome is a NonLocalReturn targeted at another context, rethrows as NonLocalReturnException for propagation.
    /// </summary>
    private bool ConsumeCallOutcome(CallOutcome outcome)
    {
        if (outcome.Kind == CallOutcomeKind.Normal)
        {
            Push(outcome.Value);
            return true;
        }

        if (outcome.Kind == CallOutcomeKind.VMException)
        {
            throw outcome.VMEx!;
        }

        // Non-local return
        if (outcome.Kind == CallOutcomeKind.NonLocalReturn)
        {
            // Console.WriteLine($"[CONSUME NLR] target={outcome.TargetContextOop}, activeContext={_activeContextOOP}, match={outcome.TargetContextOop == _activeContextOOP}");

            // If NLR targets this context, this context must return immediately with the provided value.
            if (outcome.TargetContextOop == _activeContextOOP)
            {
                // Console.WriteLine($"[NLR TARGET MATCH] Stopping execution, returning value={outcome.Value}");
                // push the return value and signal method-return
                Push(outcome.Value);
                return false;
            }

            // Otherwise propagate as exception for callers to handle / continue unwinding
            // Console.WriteLine($"[NLR PROPAGATE] Rethrowing exception to continue unwinding to {outcome.TargetContextOop}");
            throw new NonLocalReturnException(outcome.Value, outcome.TargetContextOop);
        }

        throw new InvalidOperationException("Unhandled CallOutcome kind");
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

    private void DumpSenderChain(SmalltalkOOP contextOOP, int maxDepth)
    {
        Console.WriteLine($"  SENDER CHAIN (showing up to {maxDepth} contexts):");

        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");

        var currentOOP = contextOOP;
        int depth = 0;

        while (currentOOP != SmalltalkOOP.Null && currentOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            if (currentOOP.IsSmallInteger)
            {
                Console.WriteLine($"    [{depth}] INVALID - SmallInteger {currentOOP.AsSmallInteger()}");
                break;
            }

            try
            {
                var methodOOP = _heapManager.GetInstanceVariable(currentOOP, methodIndex);

                // Get selector from method's second literal (index 1, after header at index 0)
                string selectorName = "<unknown>";
                try
                {
                    var selectorOOP = _heapManager.GetIndexedOOP(methodOOP, 1);
                    selectorName = GetSymbolName(selectorOOP) ?? "<unknown>";
                }
                catch
                {
                    // If we can't get the selector, just use unknown
                }

                var classOOP = _heapManager.GetClassOOP(currentOOP);
                var className = GetClassName(classOOP) ?? "?";

                Console.WriteLine($"    [{depth}] {className}>>{selectorName} (context={currentOOP}, method={methodOOP})");

                var senderOOP = _heapManager.GetInstanceVariable(currentOOP, senderIndex);
                currentOOP = senderOOP;
                depth++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [{depth}] ERROR reading context: {ex.Message}");
                break;
            }
        }

        if (depth >= maxDepth)
        {
            Console.WriteLine($"    ... (truncated, chain continues)");
        }
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

        if (oop == SpecialObjectsArray.Instance.Nil)
        {
            return "nil";
        }

        if (oop == SpecialObjectsArray.Instance.True)
        {
            return "true";
        }

        if (oop == SpecialObjectsArray.Instance.False)
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
    /// <param name="blockClosureOOP">Optional BlockClosure object for Primitive 207 support</param>
    /// <returns>The result of block execution</returns>
    private SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args, SmalltalkOOP blockClosureOOP = default)
    {
        try
        {
            return InvokeBlockInternal(blockMethodOOP, args, blockClosureOOP);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private SmalltalkOOP InvokeBlockInternal(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args, SmalltalkOOP blockClosureOOP = default)
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
        // homeContext = find the method context where the block was DEFINED (using blockOwnerMap)
        // This is the context that owns the block's lexical environment (receiver, instance variables)
        var homeContextOOP = FindMethodContextByLocalId(ownerLocalId);
        if (homeContextOOP == default)
        {
            throw new InvalidOperationException($"No method context found for block owner {ownerLocalId} - context may have already returned");
        }

        // Step 3: Parse block method header for metadata
        // NOTE: Argument count validation is done by Primitive 207 using BlockClosure.numArgs
        // We just need temp count from the block header
        var blockHeader = ParseMethodHeader(blockMethodOOP);
        int argCount = args.Length;  // Use actual args length, already validated by caller
        int tempCount = blockHeader.NumTemps;

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
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:BlockContext");
        var blockContextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, argCount + tempCount);
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

        // Initialize temps to nil (after arguments)
        for (int i = argCount; i < argCount + tempCount; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Step 6: Get home context receiver for proper self resolution
        // For blocks, we need to find the method where the block was originally defined
        var homeContextReceiverOOP = GetBlockHomeReceiver(blockMethodOOP, ownerLocalId);

        // Step 7: Create execution MethodContext backed by BlockContext
        var executionContextOOP = CreateBlockExecutionContext(
            blockMethodOOP, homeContextReceiverOOP, blockContextOOP, homeContextOOP, blockClosureOOP);

        // DIAGNOSTIC: Validate executionContextOOP format before using it
        var execContextFormat = _heapManager.GetObjectFormat(executionContextOOP);
        if (execContextFormat != ObjectFormat.PointerIndexed)
        {
            var execContextClass = GetClassName(GetClass(executionContextOOP));
            Console.WriteLine($"[ERROR] executionContextOOP has wrong format!");
            Console.WriteLine($"  executionContextOOP: {executionContextOOP}");
            Console.WriteLine($"  Expected: PointerIndexed");
            Console.WriteLine($"  Actual: {execContextFormat}");
            Console.WriteLine($"  Class: {execContextClass}");
            Console.WriteLine($"  blockClosureOOP: {blockClosureOOP}");
            Console.WriteLine($"  blockMethodOOP: {blockMethodOOP}");
            Console.WriteLine($"  About to copy {args.Length} arguments");
            throw new InvalidOperationException($"CreateBlockExecutionContext returned {execContextFormat} object instead of PointerIndexed");
        }

        // Step 7a: Copy arguments to MethodContext indexed slots
        // The MethodContext's indexed slots 0..(argCount-1) hold the arguments
        for (int i = 0; i < args.Length; i++)
        {
            _heapManager.SetIndexedOOP(executionContextOOP, i, args[i]);
        }

        // Step 7b: Initialize temps to nil (after arguments)
        for (int i = argCount; i < argCount + tempCount; i++)
        {
            _heapManager.SetIndexedOOP(executionContextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Step 8: Execute block with GC protection
        // Root the BlockContext during execution to prevent GC
        // Note: In a real implementation, we'd use proper GC rooting
        
        // Save current active context
        var previousContextOOP = _activeContextOOP;

        // Set the block execution context as active
        _activeContextOOP = executionContextOOP;
        // Console.WriteLine($"[DEBUG Primitive207] Set _activeContextOOP = {_activeContextOOP} (format={_heapManager.GetObjectFormat(_activeContextOOP)}, class={GetClassName(GetClass(_activeContextOOP))})");

        // Execute the block bytecodes
        var result = ExecuteBytecodeLoop();
        // Console.WriteLine($"[DEBUG Primitive207] After ExecuteBytecodeLoop, _activeContextOOP = {_activeContextOOP} (format={_heapManager.GetObjectFormat(_activeContextOOP)}, class={GetClassName(GetClass(_activeContextOOP))})");

        // Clean up block context mapping
        _blockContextMapping.Remove(executionContextOOP);

        // Restore previous context
        _activeContextOOP = previousContextOOP;
        // Console.WriteLine($"[DEBUG Primitive207] Restored _activeContextOOP = {_activeContextOOP}");

        return result;
    }

    /// <summary>
    /// Creates a MethodContext for executing a block, backed by a BlockContext.
    /// This implements Approach B from the documentation.
    /// </summary>
    private SmalltalkOOP CreateBlockExecutionContext(SmalltalkOOP blockMethodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP blockContextOOP, SmalltalkOOP senderContextOOP, SmalltalkOOP blockClosureOOP)
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
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
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
        _heapManager.SetInstanceVariable(contextOOP, receiverMapIndex, SpecialObjectsArray.Instance.Nil);
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP); // self = home context receiver

        // CRITICAL: Store BlockClosure (not BlockContext) in mapping for nested block support
        // This allows nested blocks to access the parent BlockClosure's homeContext
        if (blockClosureOOP != default)
        {
            _blockContextMapping[contextOOP] = blockClosureOOP;
            // Console.WriteLine($"[DEBUG CreateBlockExecutionContext] Mapped context {contextOOP} -> BlockClosure {blockClosureOOP}");
        }
        else
        {
            // Fallback for legacy code paths that don't provide BlockClosure
            _blockContextMapping[contextOOP] = blockContextOOP;
            // Console.WriteLine($"[DEBUG CreateBlockExecutionContext] Mapped context {contextOOP} -> BlockContext {blockContextOOP} (legacy)");
        }

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
        // Check if homeContextOOP is valid
        if (homeContextOOP.IsSmallInteger || homeContextOOP == SpecialObjectsArray.Instance.Nil || homeContextOOP == default)
        {
            throw new InvalidOperationException($"Invalid home context OOP: {homeContextOOP}");
        }

        // Debug: Check the class of homeContextOOP
        var homeContextClass = GetClass(homeContextOOP);
        var homeContextClassName = GetClassName(homeContextClass);
        if (homeContextClassName != "MethodContext")
        {
            throw new InvalidOperationException($"Home context is not a MethodContext: {homeContextClassName} (OOP={homeContextOOP})");
        }

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
        
        while (currentContextOOP != default && currentContextOOP != SpecialObjectsArray.Instance.Nil)
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

        // Console.WriteLine($"[DEBUG FindMethodContext] Starting from {startContextOOP}");

        while (currentContextOOP != default && currentContextOOP != SpecialObjectsArray.Instance.Nil)
        {

            // Check if this context is a block execution context
            if (_blockContextMapping.ContainsKey(currentContextOOP))
            {
                // Console.WriteLine($"[DEBUG FindMethodContext] {currentContextOOP} is block context, following sender");
                // This is a block execution context, get its sender
                int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
                var senderContextOOP = _heapManager.GetInstanceVariable(currentContextOOP, senderIndex);
                currentContextOOP = senderContextOOP;
                continue;
            }

            // Console.WriteLine($"[DEBUG FindMethodContext] Found method context: {currentContextOOP}");
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

        // DEBUG: Check if headerOop is SmallInteger
        if (!headerOop.IsSmallInteger)
        {
            Console.WriteLine($"[ParseMethodHeader] WARNING: Header is not SmallInteger: {headerOop}");
            // Return default values
            return new MethodHeaderInfo(0, false, 0, 0, 0, 0, false, false, 0);
        }

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

                if (!lastLiteralOop.IsSmallInteger)
                {
                    numArgs = 0;
                    primitive = 0;
                }
                else
                {
                    int lastLiteral = lastLiteralOop.AsSmallInteger();
                    primitive = lastLiteral & 0xFF;           // Low byte
                    numArgs = (lastLiteral >> 8) & 0x1F;     // High bits
                }
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
        // DIAGNOSTIC: Validate _activeContextOOP before using it
        var activeContextFormat = _heapManager.GetObjectFormat(_activeContextOOP);
        if (activeContextFormat != ObjectFormat.PointerIndexed)
        {
            var activeContextClass = GetClassName(GetClass(_activeContextOOP));
            var valueClass = valueOOP.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(valueOOP));
            Console.WriteLine($"[ERROR Push] _activeContextOOP has wrong format!");
            Console.WriteLine($"  _activeContextOOP: {_activeContextOOP}");
            Console.WriteLine($"  Expected format: PointerIndexed (MethodContext)");
            Console.WriteLine($"  Actual format: {activeContextFormat}");
            Console.WriteLine($"  Class: {activeContextClass}");
            Console.WriteLine($"  Trying to push value: {valueOOP}");
            Console.WriteLine($"  Value class: {valueClass}");
            Console.WriteLine($"  CORRUPTION DETECTED: _activeContextOOP was overwritten with non-context object!");
            throw new InvalidOperationException($"Push() called with corrupted _activeContextOOP={_activeContextOOP}, format={activeContextFormat}, class={activeContextClass}");
        }

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
        // Validate that methodOOP is a CompiledMethod (PointerIndexed)
        var format = _heapManager.GetObjectFormat(methodOOP);
        if (format != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            throw new InvalidOperationException(
                $"GetMethodLiteral called on non-PointerIndexed object: {methodOOP}, " +
                $"format={format}, class={className}, localId={localId}. " +
                $"Expected CompiledMethod (PointerIndexed), got {format}. " +
                $"Index={index}");
        }

        // Get the literal from indexed slots
        var literalOOP = _heapManager.GetIndexedOOP(methodOOP, index);

        // Note: Literals can be Symbols (ByteIndexed), Strings (ByteIndexed),
        // SmallIntegers, Arrays (PointerIndexed), CompiledMethods (PointerIndexed), etc.
        // ByteIndexed literals (Symbols/Strings) are normal and expected.

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
            if (thisClassOOP != SpecialObjectsArray.Instance.Nil && thisClassOOP != SmalltalkOOP.Null)
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
        while (currentClassOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            string currentClassName = GetClassName(currentClassOOP);
            if (_debugOptions.ShouldDebugMethodLookup)
            {
                Console.WriteLine($"  [GetMethod] depth={depth}, {currentClassName}, looking for '{selectorName}'");
            }

            // Get the method dictionary from the class
            int methodDictIndex = _layoutResolver.GetFieldIndex("class:Class", "methodDict");
            var methodDictOOP = _heapManager.GetInstanceVariable(currentClassOOP, methodDictIndex);

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                // No method dictionary, try superclass
                int superclassIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass");
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, superclassIndex);

                if (superclassOOP == SpecialObjectsArray.Instance.Nil)
                {
                    if (_debugOptions.ShouldDebugMethodLookup)
                    {
                        Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root of hierarchy)");
                    }
                    break; // Reached root of class hierarchy
                }

                if (_debugOptions.ShouldDebugMethodLookup)
                {
                    string superclassName = GetClassName(superclassOOP);
                    Console.WriteLine($"  '{selectorName}' not found, trying superclass={superclassName}");
                }
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
            if (valuesArrayOOP == SpecialObjectsArray.Instance.Nil)
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

            // DIAGNOSTIC: Get format once for both validation blocks
            var methodDictFormat = _heapManager.GetObjectFormat(methodDictOOP);

            // DIAGNOSTIC: Validate methodDictOOP format FIRST (before we try to access its indexed slots)
            if (methodDictFormat != ObjectFormat.PointerIndexed)
            {
                var methodDictClass = GetClassName(GetClass(methodDictOOP));
                Console.WriteLine($"[ERROR] methodDictOOP has wrong format!");
                Console.WriteLine($"  methodDictOOP: {methodDictOOP}");
                Console.WriteLine($"  Expected: PointerIndexed (MethodDictionary)");
                Console.WriteLine($"  Actual: {methodDictFormat}");
                Console.WriteLine($"  Class: {methodDictClass}");
                Console.WriteLine($"  currentClassOOP={currentClassOOP}, className={GetClassName(currentClassOOP)}");
                Console.WriteLine($"  Looking for selector: {selectorName}");

                // Check ALL instance variables in the class
                Console.WriteLine($"  Checking class instance variables:");
                try
                {
                    int instanceVarCount = _layoutResolver.GetInstanceVariableCount(GetClassName(currentClassOOP));
                    Console.WriteLine($"    Instance variable count: {instanceVarCount}");
                    for (int slot = 0; slot < instanceVarCount; slot++)
                    {
                        try
                        {
                            var slotValue = _heapManager.GetInstanceVariable(currentClassOOP, slot);
                            var slotFormat = slotValue.IsSmallInteger ? "SmallInteger" : _heapManager.GetObjectFormat(slotValue).ToString();
                            var slotClass = slotValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(slotValue));
                            Console.WriteLine($"    slot[{slot}]: {slotValue}, format={slotFormat}, class={slotClass}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    slot[{slot}]: ERROR - {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ERROR getting instance variables: {ex.Message}");
                }

                throw new InvalidOperationException($"Class methodDict field contains {methodDictFormat} object (class={methodDictClass}) instead of PointerIndexed MethodDictionary");
            }

            // DIAGNOSTIC: Validate valuesArrayOOP format BEFORE using it
            var valuesArrayFormat = _heapManager.GetObjectFormat(valuesArrayOOP);
            if (valuesArrayFormat != ObjectFormat.PointerIndexed)
            {
                var valuesArrayClass = GetClassName(GetClass(valuesArrayOOP));
                Console.WriteLine($"[ERROR] valuesArrayOOP has wrong format!");
                Console.WriteLine($"  valuesArrayOOP: {valuesArrayOOP}");
                Console.WriteLine($"  Expected: PointerIndexed (Array)");
                Console.WriteLine($"  Actual: {valuesArrayFormat}");
                Console.WriteLine($"  Class: {valuesArrayClass}");
                Console.WriteLine($"  methodDictOOP: {methodDictOOP}, format={methodDictFormat}");
                Console.WriteLine($"  currentClassOOP={currentClassOOP}, className={GetClassName(currentClassOOP)}");
                Console.WriteLine($"  Looking for selector: {selectorName}");

                // Check ALL instance variables in the MethodDictionary
                Console.WriteLine($"  Checking MethodDictionary instance variables:");
                for (int slot = 0; slot < 3; slot++)
                {
                    try
                    {
                        var slotValue = _heapManager.GetInstanceVariable(methodDictOOP, slot);
                        var slotFormat = slotValue.IsSmallInteger ? "SmallInteger" : _heapManager.GetObjectFormat(slotValue).ToString();
                        var slotClass = slotValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(slotValue));
                        Console.WriteLine($"    slot[{slot}]: {slotValue}, format={slotFormat}, class={slotClass}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    slot[{slot}]: ERROR - {ex.Message}");
                    }
                }

                throw new InvalidOperationException($"MethodDictionary slot[0] (valuesArray) contains {valuesArrayFormat} object (class={valuesArrayClass}) instead of PointerIndexed Array");
            }

            int dictSize = _heapManager.GetIndexedSize(methodDictOOP);

            // Diagnostic code already ran above, so we know methodDictFormat is PointerIndexed here
            if (_debugOptions.ShouldDebugMethodLookup && methodDictFormat != ObjectFormat.PointerIndexed)
            {
                var methodDictClass = GetClassName(GetClass(methodDictOOP));
                Console.WriteLine($"[ERROR] methodDictOOP={methodDictOOP} has wrong format!");
                Console.WriteLine($"  Expected: PointerIndexed (MethodDictionary)");
                Console.WriteLine($"  Actual: {methodDictFormat}");
                Console.WriteLine($"  Class: {methodDictClass}");
                Console.WriteLine($"  currentClassOOP={currentClassOOP}, className={GetClassName(currentClassOOP)}");
                Console.WriteLine($"  methodDictIndex={methodDictIndex} (from GetFieldIndex('class:Class', 'methodDict'))");
                Console.WriteLine($"  Looking for selector: {selectorName}");

                // Check what's in the other slots to diagnose field index issue
                Console.WriteLine($"  Checking class instance variables:");
                for (int slot = 0; slot < 7; slot++)
                {
                    try
                    {
                        var slotValue = _heapManager.GetInstanceVariable(currentClassOOP, slot);
                        var slotFormat = slotValue.IsSmallInteger ? "SmallInteger" : _heapManager.GetObjectFormat(slotValue).ToString();
                        var slotClass = slotValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(slotValue));
                        Console.WriteLine($"    slot[{slot}]: {slotValue}, format={slotFormat}, class={slotClass}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    slot[{slot}]: ERROR - {ex.Message}");
                    }
                }

                throw new InvalidOperationException($"methodDict field contains {methodDictFormat} object (class={methodDictClass}) instead of PointerIndexed MethodDictionary");
            }

            for (int i = 0; i < dictSize; i++)
            {
                var selectorOOP = _heapManager.GetIndexedOOP(methodDictOOP, i);

                // Skip nil entries (empty slots in the hash table)
                if (selectorOOP == SpecialObjectsArray.Instance.Nil)
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

            if (_debugOptions.ShouldDebugMethodLookup)
            {
                if (nextSuperclassOOP == SpecialObjectsArray.Instance.Nil)
                {
                    Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root)");
                }
                else
                {
                    string nextSuperclassName = GetClassName(nextSuperclassOOP);
                    Console.WriteLine($"  '{selectorName}' not found, trying superclass={nextSuperclassName}");
                }
            }

            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        if (_debugOptions.ShouldDebugMethodLookup)
        {
            Console.WriteLine($"  [GetMethod] FAILED: '{selectorName}' not found after {depth} levels");
        }
        return null;
    }

    private class MethodInfo
    {
        public SmalltalkOOP MethodOOP { get; set; }
    }

    // IPrimitiveContext implementation - using new heap manager API with SmalltalkOOP types
    ISmalltalkHeapManager IPrimitiveContext.HeapManager => _heapManager;
    LayoutResolver IPrimitiveContext.LayoutResolver => _layoutResolver;
    IPlatformBridge? IPrimitiveContext.PlatformBridge => _platformBridge;
    bool IPrimitiveContext.PrimitiveSuccess { get => _primitiveSuccess; set => _primitiveSuccess = value; }

    SmalltalkOOP IPrimitiveContext.GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
    {
        // Ensure className has "class:" prefix for LayoutResolver lookup
        string classKey = className.StartsWith("class:") || className.StartsWith("meta:")
            ? className
            : $"class:{className}";

        int fieldIndex = _layoutResolver.GetFieldIndex(classKey, fieldName);
        return _heapManager.GetInstanceVariable(oop, fieldIndex);
    }

    void IPrimitiveContext.SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
    {
        // Ensure className has "class:" prefix for LayoutResolver lookup
        string classKey = className.StartsWith("class:") || className.StartsWith("meta:")
            ? className
            : $"class:{className}";

        int fieldIndex = _layoutResolver.GetFieldIndex(classKey, fieldName);
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

        // Call the private SendMessage method and convert Normal outcome to OOP,
        // or throw/propagate a NonLocalReturnException for non-local returns so primitives that expect OOP behave.
        var outcome = SendMessage(receiverOop, selectorOop, args);
        switch (outcome.Kind)
        {
            case CallOutcomeKind.Normal:
                return outcome.Value;
            case CallOutcomeKind.NonLocalReturn:
                // TODO: Phase 3 - Implement NonLocalReturnException
                // For now, treat non-local returns as normal returns
                // throw new NonLocalReturnException(outcome.Value, outcome.TargetContextOop);
                return outcome.Value;
            case CallOutcomeKind.VMException:
                throw outcome.VMEx!;
            default:
                throw new InvalidOperationException("Unknown CallOutcome kind");
        }
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
            _heapManager.SetIndexedOOP(blockContextOOP, i, SpecialObjectsArray.Instance.Nil);
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
            return SpecialObjectsArray.Instance.Nil;
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
        var nilOOP = SpecialObjectsArray.Instance.Nil;
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
        
        var nilOOP = SpecialObjectsArray.Instance.Nil;
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
            if (existingAssocOOP == SpecialObjectsArray.Instance.Nil)
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
            if (slotOOP == SpecialObjectsArray.Instance.Nil)
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

    /// <summary>
    /// Checks if an OOP references a block literal (CompiledMethod with localId starting with "block:").
    /// </summary>
    private bool IsBlockLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return false;

        // Check if this OOP has a localId starting with "block:"
        string? localId = GetLocalId(oop);
        return localId != null && localId.StartsWith("block:");
    }

    /// <summary>
    /// Creates a BlockClosure instance wrapping a block CompiledMethod.
    /// Captures the current active context as the homeContext for lexical scoping.
    /// </summary>
    private SmalltalkOOP CreateBlockClosure(SmalltalkOOP blockMethodOOP, SmalltalkOOP homeContextOOP)
    {
        // Get BlockClosure class
        if (!GlobalRegistry.TryGet("class:BlockClosure", out SmalltalkOOP blockClosureClassOOP))
        {
            throw new InvalidOperationException("BlockClosure class not found in GlobalRegistry");
        }

        // Get argument count from block method header
        int numArgs = GetBlockArgCount(blockMethodOOP);

        // Allocate BlockClosure instance with instance variable count from layout
        // IMPORTANT: Do NOT hardcode instance variable count - query from LayoutResolver
        // to match the precomputed layout in the JSON bundle
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:BlockClosure");
        var closureOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, instanceVarCount, 0);
        _heapManager.SetSlot(closureOOP, 0, blockClosureClassOOP);

        // Get field indices for BlockClosure instance variables
        int methodIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "method");
        int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
        int returnContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "returnContext");
        int numArgsIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "numArgs");

        // Console.WriteLine($"[DEBUG CreateBlockClosure] BlockClosure instance var count={instanceVarCount}, field indices: method={methodIndex}, homeContext={homeContextIndex}, returnContext={returnContextIndex}, numArgs={numArgsIndex}");

        // Initialize instance variables
        _heapManager.SetInstanceVariable(closureOOP, methodIndex, blockMethodOOP);
        _heapManager.SetInstanceVariable(closureOOP, homeContextIndex, homeContextOOP);

        // Ensure the returnContext is the enclosing MethodContext (skip block execution contexts)
        var returnContextOOP = FindMethodContext(homeContextOOP);
        if (returnContextOOP == default)
        {
            // Fallback to the homeContextOOP if no method context can be found
            returnContextOOP = homeContextOOP;
        }
        _heapManager.SetInstanceVariable(closureOOP, returnContextIndex, returnContextOOP);
        _heapManager.SetInstanceVariable(closureOOP, numArgsIndex, SmalltalkOOP.FromSmallInteger(numArgs));

        // Console.WriteLine($"[DEBUG CreateBlockClosure] Created closure {closureOOP} with homeContext={homeContextOOP}, returnContext={returnContextOOP}");

        return closureOOP;
    }

    /// <summary>
    /// Extracts the argument count from a block CompiledMethod's header.
    /// </summary>
    private int GetBlockArgCount(SmalltalkOOP blockMethodOOP)
    {
        // Validate that blockMethodOOP is a CompiledMethod (PointerIndexed format)
        var format = _heapManager.GetObjectFormat(blockMethodOOP);
        var className = GetClassName(GetClass(blockMethodOOP));
        var localId = GetLocalId(blockMethodOOP);

        // Console.WriteLine($"[DEBUG GetBlockArgCount] blockMethodOOP={blockMethodOOP}, format={format}, class={className}, localId={localId}");

        if (format != ObjectFormat.PointerIndexed)
        {
            throw new InvalidOperationException(
                $"GetBlockArgCount called on non-PointerIndexed object: {blockMethodOOP}, " +
                $"format={format}, class={className}, localId={localId}. " +
                $"Expected CompiledMethod (PointerIndexed), got {format}.");
        }

        // Get the method header (first indexed slot)
        var headerOOP = _heapManager.GetIndexedOOP(blockMethodOOP, 0);
        if (!headerOOP.IsSmallInteger)
        {
            throw new InvalidOperationException($"Block method header is not a SmallInteger: {headerOOP}");
        }

        int header = headerOOP.AsSmallInteger();

        // Check header format - blocks use extended form with flags=7
        int flags = (header >> 12) & 0x7;

        if (flags == 7)
        {
            // Extended form - arg count is in last literal (high byte)
            int numLiterals = header & 0x3F;

            if (numLiterals > 0)
            {
                // Last literal contains: low byte = primitive, high byte = arg count
                var lastLiteralOOP = _heapManager.GetIndexedOOP(blockMethodOOP, numLiterals);

                if (lastLiteralOOP.IsSmallInteger)
                {
                    int lastLiteral = lastLiteralOOP.AsSmallInteger();
                    int argCount = (lastLiteral >> 8) & 0x1F;
                    return argCount;
                }
            }
        }

        // Simple form (flags 0-4): argCount is encoded directly in flags field (bits 12-14)
        // For blocks with 0-4 arguments and no primitive, flags = argCount
        return flags;
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

    SmalltalkOOP IPrimitiveContext.InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] arguments, SmalltalkOOP homeContextOOP, SmalltalkOOP blockClosureOOP)
    {
        // Canonical Primitive 207 implementation: all metadata is in the BlockClosure
        // The blockMethodOOP and homeContextOOP parameters are redundant but kept for compatibility
        return InvokeBlockInternal(blockMethodOOP, arguments, blockClosureOOP);
    }

    int IPrimitiveContext.GetFieldIndex(string className, string fieldName)
    {
        return _layoutResolver.GetFieldIndex(className, fieldName);
    }

    SmalltalkOOP IPrimitiveContext.GetInstanceVariable(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetInstanceVariable(objectOOP, index);
    }

    int IPrimitiveContext.GetIndexedSize(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetIndexedSize(objectOOP);
    }

    SmalltalkOOP IPrimitiveContext.GetIndexedOOP(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetIndexedOOP(objectOOP, index);
    }

    SmalltalkOOP IPrimitiveContext.InternSymbol(string symbolName)
    {
        // CRITICAL: Must access USTable from Smalltalk heap, NOT from C# proxy data
        // The _symbols array and SymbolRegistry are coldstart-time snapshots that become
        // stale as soon as the Smalltalk interpreter begins running and creates new symbols.

        // Get USTable (Universal Symbol Table) from GlobalRegistry
        if (!GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableOOP))
        {
            // USTable not yet created (shouldn't happen after cold start)
            return SpecialObjectsArray.Instance.Nil;
        }

        // USTable is an Array of Symbol OOPs (with nils for empty slots)
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Convert input string to bytes for comparison (Latin-1 encoding)
        byte[] searchBytes = System.Text.Encoding.Latin1.GetBytes(symbolName);

        // Iterate through all slots in USTable
        for (int i = 0; i < ustableSize; i++)
        {
            var symbolOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil slots (empty entries in hash table)
            if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                continue;

            // Get the Symbol's string length from its indexed fields in the Smalltalk heap
            int symbolLength = _heapManager.GetIndexedSize(symbolOOP);

            // Compare byte-by-byte
            if (symbolLength == searchBytes.Length)
            {
                bool match = true;
                for (int j = 0; j < searchBytes.Length; j++)
                {
                    byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                    if (symbolByte != searchBytes[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Found matching symbol in the Smalltalk heap
                    return symbolOOP;
                }
            }
        }

        // Symbol not found - return Nil to signal primitive failure
        // The Smalltalk fallback code will create a new Symbol if needed
        return SpecialObjectsArray.Instance.Nil;
    }

    SmalltalkOOP IPrimitiveContext.GetGlobalObjectByName(string globalName)
    {
        // HEAP-ONLY IMPLEMENTATION: Access Smalltalk global dictionary from heap,
        // NOT from GlobalRegistry (which is a cold start snapshot that becomes stale)

        // Get Smalltalk SystemDictionary from SpecialObjectsArray
        var smalltalkOOP = SpecialObjectsArray.Instance.Smalltalk;
        if (smalltalkOOP == SpecialObjectsArray.Instance.Nil)
        {
            // SystemDictionary not yet created (shouldn't happen after cold start)
            return SpecialObjectsArray.Instance.Nil;
        }

        // Determine what we're looking for based on the prefix
        string lookupKey;
        if (globalName.StartsWith("class:"))
        {
            // Strip "class:" prefix - the key in Smalltalk is just the class name
            lookupKey = globalName.Substring(6);
        }
        else if (globalName.StartsWith("global:"))
        {
            // Strip "global:" prefix
            lookupKey = globalName.Substring(7);
        }
        else
        {
            // No prefix - use as-is
            lookupKey = globalName;
        }

        // First, intern the key as a Symbol (required for dictionary lookup)
        var keySymbolOOP = ((IPrimitiveContext)this).InternSymbol(lookupKey);
        if (keySymbolOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Symbol not found - global doesn't exist
            return SpecialObjectsArray.Instance.Nil;
        }

        // SystemDictionary is a Dictionary with an Association array stored in instance variable
        // Dictionary instance variables (from Smalltalk-80 spec):
        //   0: tally (SmallInteger count of entries)
        //   1: array (PointerIndexed Array of Association objects)

        int arrayIndex = _layoutResolver.GetFieldIndex("class:Dictionary", "array");
        var associationArrayOOP = _heapManager.GetInstanceVariable(smalltalkOOP, arrayIndex);

        if (associationArrayOOP == SpecialObjectsArray.Instance.Nil)
        {
            return SpecialObjectsArray.Instance.Nil;
        }

        // Search the association array for matching key
        int arraySize = _heapManager.GetIndexedSize(associationArrayOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        for (int i = 0; i < arraySize; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(associationArrayOOP, i);

            // Skip nil slots (empty hash table slots)
            if (associationOOP == nilOOP || associationOOP.IsSmallInteger)
                continue;

            // Association has two instance variables: key and value
            // Instance variable 0: key (Symbol)
            // Instance variable 1: value (Object)
            var assocKeyOOP = _heapManager.GetInstanceVariable(associationOOP, 0);

            // Check if key matches
            if (assocKeyOOP == keySymbolOOP)
            {
                // Found it! Return the value
                var valueOOP = _heapManager.GetInstanceVariable(associationOOP, 1);
                return valueOOP;
            }
        }

        // Global not found
        return SpecialObjectsArray.Instance.Nil;
    }

    string? IPrimitiveContext.GetClassNameForObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
        {
            return "class:SmallInteger";
        }

        // Get the class OOP from slot 0 of the object
        var classOOP = _heapManager.GetSlot(objectOOP, 0);
        if (classOOP == SpecialObjectsArray.Instance.Nil || classOOP == SmalltalkOOP.Null)
        {
            return null;
        }

        // Use the private GetClassName method to get the class name
        return GetClassName(classOOP);
    }
}