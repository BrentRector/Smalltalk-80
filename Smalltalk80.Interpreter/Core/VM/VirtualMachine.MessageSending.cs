using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.VM.Interpreter;
using Smalltalk80.Interpreter.Core.Utilities;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing message sending and call outcome handling methods.
///
/// This partial class contains:
/// - SendMessage() / SendSuperMessage() - Method dispatch and invocation
/// - ConsumeCallOutcome() - Handling return values and NLR propagation
/// - DumpMethodStateOnError() - Detailed error diagnostics
///
/// These methods remain in VirtualMachine because they coordinate multiple components
/// and access VM-internal state (instruction count, tracing, primitive execution).
/// </summary>
public partial class VirtualMachine
{
    /// <summary>
    /// Gets the current method's local ID from the active context.
    /// Properly handles both MethodContexts and BlockContexts.
    /// For BlockContexts, traverses to the home MethodContext to get the method.
    /// </summary>
    private string GetCurrentMethodLocalId()
    {
        var contextOOP = _contextManager.ActiveContext;

        // Check if this is a BlockContext
        var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
        if (contextClassOOP == _metadata.BlockContextClassOOP)
        {
            // Get home MethodContext
            contextOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
        }

        // Get method from the (home) MethodContext
        var methodOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.CONTEXT_METHOD_INDEX);
        return GetLocalId(methodOOP) ?? "unknown";
    }

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
        var contextClass = GetClassName(_heapManager.GetClassOOP(_contextManager.ActiveContext));
        SmalltalkOOP methodContextOOP;

        if (contextClass == "BlockContext")
        {
            // We're in a block - walk to the home MethodContext
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            methodContextOOP = _contextManager.FindMethodContext(_contextManager.ActiveContext);
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

        // Now read the method from the MethodContext (instance variable 3)
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

        // Defer selectorName until needed for diagnostics - use OOP comparison for lookup
        string? selectorName = null;  // Lazy - only populated when needed

        // Get receiver class for diagnostic event
        var recvClass = GetClassName(GetClass(receiverOOP));

        // Emit diagnostic event for super method entry (conditional on listener subscription)
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.MethodEntry) == true)
        {
            selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for diagnostics
            _diagnostics.Emit(new MethodEntryEvent
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                InstructionCount = _tracer.InstructionCount,
                ReceiverOOP = receiverOOP,
                ReceiverClass = recvClass,
                Selector = selectorName ?? "unknown",
                Arguments = args,
                CallDepth = _tracer.SelectorTraceDepth,
                IsSuper = true
            });
        }

        // Use OOP-based lookup - no string allocation or comparison!
        var methodInfo = _messageDispatcher.LookupMethod(superclassOOP, selectorOOP);

        if (methodInfo == null)
        {
            selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for error message
            string? superclassName = GetClassName(superclassOOP);
            throw new InvalidOperationException(
                $"Super method not found: {selectorName ?? "<unknown>"} in superclass {superclassName} (superclassOOP={superclassOOP})");
        }

        // Check if the method has a primitive
        int primitiveNumber = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);

        if (primitiveNumber > 0)
        {
            if (_tracer.ShouldTracePrimitive())
            {
                _tracer.TracePrimitiveAttempt(primitiveNumber, GetLocalId(methodInfo.MethodOOP) ?? "unknown");
            }

            // Get current instruction pointer for primitives that need it
            int currentPC = ((IPrimitiveContext)this).GetInstructionPointer();

            var primitiveResult = _primitiveRegistry.ExecuteLegacyExtended(
                primitiveNumber, this, receiverOOP, args, _runtimeHeapAccess, _layoutResolver,
                _contextManager, currentPC, this, this, _metadata, _stackManager, _timerManager, _lowMemoryManager, this, vm: this);

            // Emit diagnostic event for primitive execution in super send
            _diagnostics?.Emit(new PrimitiveEvent
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                InstructionCount = _tracer.InstructionCount,
                PrimitiveIndex = primitiveNumber,
                PrimitiveName = $"Primitive{primitiveNumber}",
                ReceiverOOP = receiverOOP,
                Arguments = args,
                Success = _primitiveSuccess,
                Result = _primitiveSuccess ? primitiveResult : null,
                FailureReason = _primitiveSuccess ? null : "Primitive failed"
            });

            if (_primitiveSuccess)
            {
                if (_tracer.ShouldTracePrimitive())
                {
                    _tracer.TracePrimitiveSuccess(primitiveNumber);
                }

                return CallOutcome.Normal(primitiveResult);
            }
            else if (_tracer.ShouldTracePrimitiveFailure())
            {
                // Log primitive failures during tracing
                selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for tracing
                _tracer.TracePrimitiveFailure(primitiveNumber, selectorName ?? "unknown");
            }
        }

        // Log method sends during tracing
        if (_tracer.ShouldTraceSend())
        {
            selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for tracing
            var currentMethod = GetCurrentMethodLocalId();
            _tracer.TraceSendEntry(recvClass, selectorName ?? "unknown", currentMethod);

            // Log error: sends during tracing
            if (selectorName == "error:")
            {
                Console.WriteLine($"  *** ERROR BEING RAISED ***");
                // Let Smalltalk's error handling proceed - Object>>error: will open NotifierView
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
        // Validate receiver is a valid OOP (SmallIntegers are always valid)
        if (!receiverOOP.IsSmallInteger && !_heapManager.IsValidOOP(receiverOOP))
        {
            string? selectorName2 = GetSymbolName(selectorOOP);
            throw new InvalidOperationException(
                $"Invalid receiver OOP {receiverOOP.RawValue} when sending '{selectorName2 ?? "unknown"}'");
        }

        // Look up the method in the receiver's class
        var classOOP = GetClass(receiverOOP);

        // Validate the class OOP
        if (!_heapManager.IsValidOOP(classOOP))
        {
            string? selectorName2 = GetSymbolName(selectorOOP);
            throw new InvalidOperationException(
                $"Invalid class OOP {classOOP.RawValue} for receiver OOP {receiverOOP.RawValue} when sending '{selectorName2 ?? "unknown"}'");
        }

        // Defer GetSymbolName until needed for diagnostics - use OOP comparison for lookup
        string? selectorName = null;  // Lazy - only populated when needed




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
            // Use OOP comparison instead of string comparison for block value messages
            if (selectorOOP == _valueSelectorOOP || selectorOOP == _valueColonSelectorOOP || selectorOOP == _valueColonValueColonSelectorOOP)
            {
                // Execute the block with proper BlockContext wrapping
                return CallOutcome.Normal(InvokeBlock(receiverOOP, args));
            }
        }

        // Debug: check for classId 0 on "comment:" send
        if (classOOP == _heapManager.GetNil())
        {
            selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for error message
            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "unknown"} in class 0 (receiver OOP={receiverOOP}, receiverId has classId 0!)");
        }

        // Use OOP-based lookup - no string allocation or comparison!
        var methodInfo = _messageDispatcher.LookupMethod(classOOP, selectorOOP);

        // DIAGNOSTIC: Show what method was found for hash/stringhash (disabled for quiet mode)
        // if ((selectorName == "hash" || selectorName == "stringhash") && methodInfo != null)
        // {
        //     int primNum = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);
        //     string methodLocalId = GetLocalId(methodInfo.MethodOOP) ?? "unknown";
        //     Console.WriteLine($"  Found method: {methodLocalId}, primitiveNumber={primNum}");
        // }

        if (methodInfo == null)
        {
            selectorName ??= GetSymbolName(selectorOOP);  // Lazy get for error message
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

        // Record method call for infinite loop debugging
        string recvClassName = GetClassName(classOOP) ?? "?";
        selectorName ??= GetSymbolName(selectorOOP);  // Lazy get - needed for tracing
        _tracer.RecordMethodCall(recvClassName, selectorName ?? "?");

        // Log error: sends but let Smalltalk handle them gracefully via NotifierView
        if (selectorName == "error:")
        {
            Console.WriteLine($"\n*** SMALLTALK ERROR BEING RAISED ***");
            Console.WriteLine($"  Receiver class: {recvClassName}");
            Console.WriteLine($"  Instruction count: {_tracer.InstructionCount}");
            Console.WriteLine("\nSmalltalk Context Chain:");
            _debugger.DumpSenderChain(_contextManager.ActiveContext, 20);
            // Let Smalltalk's error handling proceed - Object>>error: will open NotifierView
        }

        // DIAGNOSTIC: Catch notify:at: to see parsing errors before GUI is accessed
        if (selectorName == "notify:at:")
        {
            Console.WriteLine($"\n*** SMALLTALK NOTIFY:AT: BEING RAISED ***");
            Console.WriteLine($"  Receiver class: {recvClassName}");

            // Extract the error message if it's a String
            if (args.Length >= 1)
            {
                var messageOOP = args[0];
                if (!messageOOP.IsSmallInteger && _heapManager.GetObjectFormat(messageOOP) == ObjectFormat.ByteIndexed)
                {
                    var errorMsg = StringHelper.ExtractString(_heapManager, messageOOP);
                    Console.WriteLine($"  Error message: \"{errorMsg}\"");
                }
            }

            // Extract the position if it's a SmallInteger
            if (args.Length >= 2 && args[1].IsSmallInteger)
            {
                Console.WriteLine($"  Position: {args[1].AsSmallInteger()}");
            }

            // DIAGNOSTIC: If receiver is Parser, dump Scanner state
            if (recvClassName == "Parser")
            {
                DumpScannerState(receiverOOP);
            }

            Console.WriteLine($"  Instruction count: {_tracer.InstructionCount}");
            Console.WriteLine("\nSmalltalk Context Chain:");
            _debugger.DumpSenderChain(_contextManager.ActiveContext, 20);
            // Let Smalltalk's error handling proceed - CodeController>>notify:at: should highlight the error
            Console.WriteLine("\n[Continuing with Smalltalk error handling...]\n");
        }

        // Get receiver class name once for all uses
        var recvClass = GetClassName(classOOP);

        // Emit diagnostic event for method entry (conditional on listener subscription)
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.MethodEntry) == true)
        {
            _diagnostics.Emit(new MethodEntryEvent
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                InstructionCount = _tracer.InstructionCount,
                ReceiverOOP = receiverOOP,
                ReceiverClass = recvClass,
                Selector = selectorName ?? "unknown",
                Arguments = args,
                CallDepth = _tracer.SelectorTraceDepth,
                IsSuper = false
            });
        }

        // Log ALL message sends during tracing (before primitive check)
        if (_tracer.ShouldTraceSend())
        {
            var currentMethod = GetCurrentMethodLocalId();
            _tracer.TraceSendEntry(recvClass, selectorName ?? "unknown", currentMethod);
        }

        // Check if the method has a primitive
        int primitiveNumber = _messageDispatcher.GetPrimitiveNumber(methodInfo.MethodOOP);

        if (primitiveNumber > 0)
        {
            // Count primitive execution as one instruction
            if (!_tracer.IncrementInstructionCount())
            {
                _tracer.ThrowIfInfiniteLoop();
            }

            // Get current instruction pointer for primitives that need it
            int currentPC = ((IPrimitiveContext)this).GetInstructionPointer();

            // Try to execute the primitive
            var primitiveResult = _primitiveRegistry.ExecuteLegacyExtended(
                primitiveNumber, this, receiverOOP, args, _runtimeHeapAccess, _layoutResolver,
                _contextManager, currentPC, this, this, _metadata, _stackManager, _timerManager, _lowMemoryManager, this, vm: this);

            // Emit diagnostic event for primitive execution
            _diagnostics?.Emit(new PrimitiveEvent
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                InstructionCount = _tracer.InstructionCount,
                PrimitiveIndex = primitiveNumber,
                PrimitiveName = $"Primitive{primitiveNumber}",
                ReceiverOOP = receiverOOP,
                Arguments = args,
                Success = _primitiveSuccess,
                Result = _primitiveSuccess ? primitiveResult : null,
                FailureReason = _primitiveSuccess ? null : "Primitive failed"
            });

            if (_primitiveSuccess)
            {
                // Check if primitive created a new context (e.g., block invocation via Primitive 207)
                // This includes process-switching primitives that push result before switching
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
        if (_tracer.SelectorTraceEnabled)
        {
            string className = GetClassName(classOOP) ?? "?";
            TraceMessage($"-> {className}>>{selectorName}");
            _tracer.PushSelectorTrace(className, selectorName!, _tracer.SelectorTraceDepth);
            _tracer.IncrementTraceDepth();
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
            throw outcome.VMException!;
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
            // Console.WriteLine($"[CONSUME NLR] target={outcome.TargetContext}, activeContext={_contextManager.ActiveContext}, match={outcome.TargetContext == _contextManager.ActiveContext}");

            // If NLR targets this context, this context must return immediately with the provided value.
            if (outcome.TargetContext == _contextManager.ActiveContext)
            {
                // Console.WriteLine($"[NLR TARGET MATCH] Stopping execution, returning value={outcome.Value}");
                // push the return value and signal method-return
                Push(outcome.Value);
                return false;
            }

            // Otherwise propagate as exception for callers to handle / continue unwinding
            // Console.WriteLine($"[NLR PROPAGATE] Rethrowing exception to continue unwinding to {outcome.TargetContext}");
            throw new NonLocalReturnException(outcome.Value, outcome.TargetContext);
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
        if (_contextManager.ActiveContext != _heapManager.GetNil())
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
        if (_contextManager.ActiveContext != _heapManager.GetNil())
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
    /// Dumps Scanner state from Parser object for diagnostic purposes.
    /// Parser inherits from Scanner, so it has Scanner's instance variables.
    /// </summary>
    private void DumpScannerState(SmalltalkOOP parserOOP)
    {
        Console.WriteLine("\n=== Scanner State (from Parser) ===");

        try
        {
            // Scanner instance variables (Parser inherits from Scanner):
            // 0: source (ReadStream)
            // 1: mark
            // 2: hereChar (Character)
            // 3: aheadChar (Character)
            // 4: token
            // 5: tokenType (Symbol)
            // 6: hereType (Symbol)
            // 7: hereMark
            // 8: prevMark
            // Plus others from Scanner/Parser

            var hereCharOOP = _heapManager.GetInstanceVariable(parserOOP, 2);
            var tokenOOP = _heapManager.GetInstanceVariable(parserOOP, 4);
            var tokenTypeOOP = _heapManager.GetInstanceVariable(parserOOP, 5);
            var hereTypeOOP = _heapManager.GetInstanceVariable(parserOOP, 6);

            Console.WriteLine($"  hereChar: {_debugger.DescribeValue(hereCharOOP)}");
            Console.WriteLine($"  token: {_debugger.DescribeValue(tokenOOP)}");
            Console.WriteLine($"  tokenType: {_debugger.DescribeValue(tokenTypeOOP)}");
            Console.WriteLine($"  hereType: {_debugger.DescribeValue(hereTypeOOP)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error dumping Scanner state: {ex.Message}");
        }

        Console.WriteLine("===================================\n");
    }
}
