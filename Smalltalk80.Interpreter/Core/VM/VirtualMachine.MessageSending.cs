using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing message sending and call outcome handling methods.
/// </summary>
public partial class VirtualMachine
{
    /// <summary>
    /// Sends a super message to the receiver.
    /// Looks up the method in the superclass of the class containing the currently executing method.
    /// Returns a CallOutcome that signals whether a non-local return occurred.
    /// </summary>
    private CallOutcome SendSuperMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // For super sends, we need to start method lookup from the superclass of the class
        // that contains the currently executing method, NOT from the receiver's class.

        // CRITICAL FIX: Read the method from the correct context
        // When executing inside a block, _contextManager.ActiveContext is a BlockContext, and we need to walk to home MethodContext.
        // When executing in a method, _contextManager.ActiveContext is already a MethodContext.

        // Check if we're in a BlockContext or MethodContext
        var contextClass = GetClassName(_heapManager.GetSlot(_contextManager.ActiveContext, 0));
        SmalltalkOOP methodContextOOP;

        if (contextClass == "BlockContext")
        {
            // We're in a block - walk to the home MethodContext
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            methodContextOOP = _contextManager.FindMethodContext(_contextManager.ActiveContext);

            // if (_debugOptions.ShouldDebugMessages)
            // {
            //     Console.WriteLine($"[Super Send in Block] Walking from BlockContext to home MethodContext (oop={methodContextOOP})");
            // }
        }
        else if (contextClass == "MethodContext")
        {
            // We're already in a MethodContext
            methodContextOOP = _contextManager.ActiveContext;
        }
        else
        {
            throw new InvalidOperationException(
                $"Super send requires MethodContext or BlockContext, but _contextManager.ActiveContext is {contextClass}.");
        }

        // Now read the method from the MethodContext (slot 3)
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        var currentMethodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);

        // Get the class that owns this method (the home class)
        // CompiledMethod structure: fixed slot 0 = homeClass, indexed slots = [header] [literals...] [bytecodeArray]
        // The home class is stored in fixed slot 0
        var homeClassOOP = _heapManager.GetInstanceVariable(currentMethodOOP, 0);

        // Get the superclass of the home class
        // Use Blue Book architectural constant: superclass is ALWAYS at index 0 in Behavior
        // This is guaranteed by the compiler which enforces this invariant
        var superclassOOP = _heapManager.GetInstanceVariable(homeClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

        // Get selector name for diagnostics
        string? selectorName = GetSymbolName(selectorOOP);

        // Now look up the method starting from the superclass
        var methodInfo = _messageDispatcher.LookupMethod(superclassOOP, selectorName!);

        if (methodInfo == null)
        {
            string? superclassName = GetClassName(superclassOOP);
            throw new InvalidOperationException(
                $"Super method not found: {selectorName ?? "<unknown>"} in superclass {superclassName} (superclassOOP={superclassOOP})");
        }

        // Check if the method has a primitive
        int primitiveNumber = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);
        if (primitiveNumber > 0)
        {
            if (_selectorTraceEnabled && _instructionCount >= 2000 && _instructionCount < 2100)
            {
                Console.WriteLine($"[PRIMITIVE #{_instructionCount}] Trying primitive {primitiveNumber} in {GetLocalId(methodInfo.MethodOOP)}");
            }

            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                if (_selectorTraceEnabled && _instructionCount >= 2000 && _instructionCount < 2100)
                {
                    Console.WriteLine($"[PRIMITIVE #{_instructionCount}] Primitive {primitiveNumber} succeeded");
                }
                return CallOutcome.Normal(primitiveResult);
            }
            else if (_selectorTraceEnabled && _instructionCount < 50000)
            {
                // Log primitive failures during tracing
                Console.WriteLine($"[PRIMITIVE FAILED] Primitive {primitiveNumber} failed in {GetLocalId(methodInfo.MethodOOP)}");
            }
        }

        // Log method sends during tracing
        if (_selectorTraceEnabled && _instructionCount < 5000)
        {
            var recvClass = GetClassName(GetClass(receiverOOP));
            var currentMethod = GetLocalId(_heapManager.GetInstanceVariable(_contextManager.ActiveContext, VMConstants.CONTEXT_METHOD_INDEX)) ?? "unknown";
            Console.WriteLine($"[SEND #{_instructionCount}] {recvClass}>>{selectorName} (from {currentMethod})");

            // Special alert for error: sends
            if (selectorName == "error:")
            {
                Console.WriteLine($"  *** ERROR BEING RAISED ***");
            }
        }

        // TRAMPOLINE MODE: Create context and signal context switch (don't recurse)
        // The InterpreterLoop will continue execution in the new context

        // Create new context with current context as sender
        _contextManager.SetActiveContext(_contextManager.CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _contextManager.ActiveContext));
        // Console.WriteLine($"[SendMessage AFTER CreateMethodContext] _contextManager.ActiveContext = {_contextManager.ActiveContext}");

        // Signal context switch - InterpreterLoop will continue in new context
        return CallOutcome.ContextSwitch();
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




        // DIAGNOSTIC: Track > and max: message sends (DISABLED - causes issues with SmallInteger diagnostic output)
        // if (selectorName == ">")
        // {
        //     var argInfo = args.Length > 0 ? $"{args[0]} (SmallInt={args[0].IsSmallInteger})" : "none";
        //     Console.WriteLine($"\n[>] receiver {receiverOOP} (SmallInt={receiverOOP.IsSmallInteger}, class=\"{GetClassName(classOOP)}\") | arg={argInfo}");
        // }
        // if (selectorName == "max:")
        // {
        //     var argInfo = args.Length > 0 ? $"{args[0]}" : "none";
        //     Console.WriteLine($"\n[max:] receiver {receiverOOP} | arg={argInfo} | class=\"{GetClassName(classOOP)}\"");
        // }

        // SPECIAL CASE: Block CompiledMethods responding to value/value:/value:value:
        // In v1.0.3, blocks are separate CompiledMethod objects that respond to value messages
        // by creating proper BlockContext and executing with closure semantics
        var compiledMethodClassId = RuntimeSpecialObjects.Instance.CompiledMethodClass;
        if (classOOP == compiledMethodClassId)
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

        var methodInfo = _messageDispatcher.LookupMethod(classOOP, selectorName!);

        // DIAGNOSTIC: Show what method was found for hash/stringhash (disabled for quiet mode)
        // if ((selectorName == "hash" || selectorName == "stringhash") && methodInfo != null)
        // {
        //     int primNum = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);
        //     string methodLocalId = GetLocalId(methodInfo.MethodOOP) ?? "unknown";
        //     Console.WriteLine($"  Found method: {methodLocalId}, primitiveNumber={primNum}");
        // }

        if (methodInfo == null)
        {
            string? className = GetClassName(classOOP);
            string receiverLocalId = GetLocalId(receiverOOP) ?? "";
            string receiverInfo = string.IsNullOrEmpty(receiverLocalId)
                ? $"OOP({receiverOOP.RawValue})"
                : $"{receiverLocalId} OOP({receiverOOP.RawValue})";

            Console.WriteLine($"\n[X] METHOD NOT FOUND ERROR");
            Console.WriteLine($"   Selector: {selectorName ?? "<unknown>"}");
            Console.WriteLine($"   Receiver: {receiverInfo}");
            Console.WriteLine($"   Receiver class: {className} OOP({classOOP.RawValue})");
            Console.WriteLine($"   Arguments: {args.Length}");
            Console.WriteLine("\nSmalltalk Context Chain:");
            _debugger.DumpSenderChain(_contextManager.ActiveContext, 10);

            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "<unknown>"} in class {className} OOP({classOOP.RawValue})");
        }

        // Check if the method has a primitive
        int primitiveNumber = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);

        if (primitiveNumber > 0)
        {
            // Count primitive execution as one instruction
            _instructionCount++;
            if (_instructionCount >= MAX_INSTRUCTIONS)
            {
                throw new InvalidOperationException($"Infinite loop detected: {_instructionCount} instructions exceeded threshold of {MAX_INSTRUCTIONS}");
            }

            // Try to execute the primitive
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                // Check if primitive created a new context (e.g., block invocation via Primitive 207)
                if (_contextSwitchedByPrimitive)
                {
                    _contextSwitchedByPrimitive = false;
                    // Primitive created new context - signal context switch
                    return CallOutcome.ContextSwitch();
                }

                // Normal primitive success - return result directly
                return CallOutcome.Normal(primitiveResult);
            }

            // Primitive failed - execute method bytecodes as fallback
        }

        // TRAMPOLINE MODE: Create context and signal context switch (don't recurse)
        // The InterpreterLoop will continue execution in the new context

        // TAIL CALL OPTIMIZATION: DISABLED - caused stack underflow bugs
        // TODO: Fix tail call optimization to properly handle stack management
        // if (IsAtTailCallPosition())
        // {
        //     Console.WriteLine($"[TAIL CALL] Reusing context for {selectorName}");
        //     ReuseContextForTailCall(methodInfo.MethodOOP, receiverOOP, args);
        //     return CallOutcome.ContextSwitch();
        // }

        // Create new context with current context as sender
        _contextManager.SetActiveContext(_contextManager.CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _contextManager.ActiveContext));

        // Selector trace: log method entry
        if (_selectorTraceEnabled)
        {
            string className = GetClassName(classOOP) ?? "?";
            TraceMessage($"-> {className}>>{selectorName}");
            _selectorTraceStack.Push((className, selectorName!, _selectorTraceDepth));
            _selectorTraceDepth++;
        }


        // Signal context switch - InterpreterLoop will continue in new context
        return CallOutcome.ContextSwitch();
    }

    /// <summary>
    /// Consume a CallOutcome in the bytecode loop / caller.
    /// Returns true to continue execution (normal), false to indicate that the current method/context has returned (stop loop).
    /// If the outcome is a VMException, throws it. If the outcome is a NonLocalReturn targeted at another context, rethrows as NonLocalReturnException for propagation.
    /// If the outcome is a ContextSwitch, sets _contextSwitchPending and returns false to exit the bytecode loop.
    /// </summary>
    private bool ConsumeCallOutcome(CallOutcome outcome)
    {
        // if (_verboseDebugMode)
        //     Console.WriteLine($"[ConsumeCallOutcome] outcome.Kind={outcome.Kind}");

        if (outcome.Kind == CallOutcomeKind.Normal)
        {
            Push(outcome.Value);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"  Normal: pushed {outcome.Value}");
            return true;
        }

        if (outcome.Kind == CallOutcomeKind.VMException)
        {
            throw outcome.VMEx!;
        }

        if (outcome.Kind == CallOutcomeKind.ContextSwitch)
        {
            // Context switch occurred - signal to exit bytecode loop and continue in new context
            _contextSwitchPending = true;
            return false;
        }

        // Non-local return
        if (outcome.Kind == CallOutcomeKind.NonLocalReturn)
        {
            // Console.WriteLine($"[CONSUME NLR] target={outcome.TargetContextOop}, activeContext={_contextManager.ActiveContext}, match={outcome.TargetContextOop == _contextManager.ActiveContext}");

            // If NLR targets this context, this context must return immediately with the provided value.
            if (outcome.TargetContextOop == _contextManager.ActiveContext)
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
            (litOOP) => _debugger.DescribeLiteral(litOOP),
            Array.Empty<string>());
        Console.WriteLine(disassembly);

        // Display literals
        Console.WriteLine("LITERALS:");
        for (int i = 0; i < literalCount; i++)
        {
            var litOOP = _heapManager.GetIndexedOOP(methodOOP, i + 1);
            Console.WriteLine($"  [{i}] = {_debugger.DescribeLiteral(litOOP)}");
        }
        Console.WriteLine();

        // Display arguments
        Console.WriteLine($"ARGUMENTS ({args.Length}):");
        for (int i = 0; i < args.Length; i++)
        {
            Console.WriteLine($"  arg{i} = {_debugger.DescribeValue(args[i])}");
        }
        Console.WriteLine();

        // Display receiver
        Console.WriteLine("RECEIVER:");
        Console.WriteLine($"  self = {_debugger.DescribeValue(receiverOOP)}");
        Console.WriteLine();

        // Display temps (if in active context)
        if (_contextManager.ActiveContext != SmalltalkOOP.Null)
        {
            Console.WriteLine($"TEMPORARIES ({tempCount}):");
            for (int i = 0; i < tempCount; i++)
            {
                var tempOOP = _heapManager.GetIndexedOOP(_contextManager.ActiveContext, i);
                Console.WriteLine($"  temp{i} = {_debugger.DescribeValue(tempOOP)}");
            }
            Console.WriteLine();
        }

        // Display stack
        if (_contextManager.ActiveContext != SmalltalkOOP.Null)
        {
            int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
            var stackpOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, stackpIndex);
            int stackp = stackpOOP.AsSmallInteger();

            Console.WriteLine($"STACK (sp={stackp}):");
            int stackStart = tempCount;
            for (int i = stackStart; i < stackp; i++)
            {
                var stackOOP = _heapManager.GetIndexedOOP(_contextManager.ActiveContext, i);
                Console.WriteLine($"  [{i - stackStart}] = {_debugger.DescribeValue(stackOOP)}");
            }
        }

        Console.WriteLine(new string('=', 80) + "\n");
    }

    /// <summary>
    /// Reuses the current context for a tail call by replacing method, receiver, and arguments.
    /// This prevents context accumulation in recursive patterns like whileTrue:.
    /// </summary>
    private void ReuseContextForTailCall(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments)
    {
        var header = ParseMethodHeader(methodOOP);

        // Validate argument count
        if (arguments.Length != header.NumArgs)
        {
            throw new InvalidOperationException(
                $"Tail call argument count mismatch: method expects {header.NumArgs} args, got {arguments.Length}");
        }

        // Get field indices
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Replace method and receiver
        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, methodIndex, methodOOP);
        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, receiverIndex, receiverOOP);

        // Reset PC to start of method
        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, pcIndex, SmalltalkOOP.FromSmallInteger(0));

        // Reset stack pointer to after args+temps
        _heapManager.SetInstanceVariable(_contextManager.ActiveContext, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

        // Replace arguments
        for (int i = 0; i < header.NumArgs; i++)
        {
            _heapManager.SetIndexedOOP(_contextManager.ActiveContext, i, arguments[i]);
        }

        // Clear temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heapManager.SetIndexedOOP(_contextManager.ActiveContext, i, SpecialObjectsArray.Instance.Nil);
        }

        // Note: sender stays the same - we return to the same place
        // Note: we don't increment _activeContextCount since we're reusing a context
    }

    /// <summary>
    /// Checks if the current instruction pointer is at a tail call position.
    /// A tail call occurs when the next bytecode after a message send is a return.
    /// </summary>
    private bool IsAtTailCallPosition()
    {
        try
        {
            // Get current PC
            int pcIndex = VMConstants.CONTEXT_PC_INDEX;
            var pcOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, pcIndex);
            if (!pcOOP.IsSmallInteger) return false;
            int currentPC = pcOOP.AsSmallInteger();

            // Get method
            int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
            var methodOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, methodIndex);

            // Get bytecodes from method (last indexed item is the bytecode array OOP)
            int literalCount = _heapManager.GetIndexedSize(methodOOP);
            if (literalCount == 0) return false;
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, literalCount - 1);

            // Get bytecode array bytes
            int bytecodeLength = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            if (bytecodeLength == 0) return false;

            // Check if next bytecode (after current send completes) is a return
            // The send bytecode we just executed consumed some bytes, so we check currentPC+1
            int nextPC = currentPC + 1;
            if (nextPC >= bytecodeLength) return false;

            byte nextBytecode = _heapManager.GetIndexedByte(bytecodeArrayOOP, nextPC);

            // Check if it's a return bytecode
            return BytecodeUtil.IsReturn(nextBytecode);
        }
        catch
        {
            return false;
        }
    }
}
