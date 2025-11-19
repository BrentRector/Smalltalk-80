namespace Smalltalk80.Interpreter.Core.VM.Diagnostics;

/// <summary>
/// Manages execution tracing and debugging state for the VM.
/// Encapsulates trace counters, flags, and output methods.
/// </summary>
public class ExecutionTracer
{
    // Instruction execution counter for debugging infinite loops
    private long _instructionCount;
    private const long MAX_INSTRUCTIONS = 500000;  // Halt at 500K instructions (infinite loop threshold)

    // Verbose debug mode for current init expression
    private bool _verboseDebugMode;

    // Selector trace mechanism - shows nested method calls and returns
    private bool _selectorTraceEnabled;
    private int _selectorTraceDepth;
    private int _bytecodeInstructionCounter;
    private int _pushTraceCounter;
    private readonly Stack<(string className, string selector, int depth)> _selectorTraceStack = new();

    /// <summary>Gets the total number of instructions executed.</summary>
    public long InstructionCount => _instructionCount;

    /// <summary>Gets or sets whether verbose debug mode is enabled.</summary>
    public bool VerboseDebugMode
    {
        get => _verboseDebugMode;
        set => _verboseDebugMode = value;
    }

    /// <summary>Gets or sets whether selector tracing is enabled.</summary>
    public bool SelectorTraceEnabled
    {
        get => _selectorTraceEnabled;
        set
        {
            _selectorTraceEnabled = value;
            _bytecodeInstructionCounter = 0;
            _pushTraceCounter = 0;
        }
    }

    /// <summary>Gets current push trace counter for StackManager.</summary>
    public int PushTraceCounter => _pushTraceCounter;

    /// <summary>Increments and returns the push trace counter.</summary>
    public int IncrementPushTraceCounter() => _pushTraceCounter++;

    /// <summary>Gets current bytecode instruction counter.</summary>
    public int BytecodeInstructionCounter => _bytecodeInstructionCounter;

    /// <summary>Increments bytecode instruction counter.</summary>
    public void IncrementBytecodeCounter() => _bytecodeInstructionCounter++;

    /// <summary>Gets current selector trace depth (call nesting level).</summary>
    public int SelectorTraceDepth => _selectorTraceDepth;

    /// <summary>Increments trace depth when entering a method.</summary>
    public void IncrementTraceDepth() => _selectorTraceDepth++;

    /// <summary>Decrements trace depth when exiting a method.</summary>
    public void DecrementTraceDepth() => _selectorTraceDepth--;

    /// <summary>Resets the instruction counter to zero.</summary>
    public void ResetInstructionCount() => _instructionCount = 0;

    /// <summary>
    /// Increments instruction count and returns true if execution should continue.
    /// Returns false if max instructions exceeded (infinite loop detected).
    /// </summary>
    public bool IncrementInstructionCount()
    {
        _instructionCount++;
        return _instructionCount < MAX_INSTRUCTIONS;
    }

    /// <summary>
    /// Throws if instruction count has exceeded the maximum.
    /// </summary>
    public void ThrowIfInfiniteLoop()
    {
        if (_instructionCount >= MAX_INSTRUCTIONS)
        {
            throw new InvalidOperationException(
                $"Infinite loop detected: {_instructionCount} instructions exceeded threshold of {MAX_INSTRUCTIONS}");
        }
    }

    /// <summary>
    /// Checks if we should output periodic instruction trace.
    /// </summary>
    public bool ShouldTraceInstructionPeriodic()
        => _selectorTraceEnabled && _instructionCount % 10000 == 0;

    /// <summary>
    /// Checks if we should trace bytecode execution (narrow range for debugging).
    /// </summary>
    public bool ShouldTraceBytecodeRange()
        => _selectorTraceEnabled && _instructionCount >= 2020 && _instructionCount <= 2045;

    /// <summary>
    /// Checks if we should trace primitive execution.
    /// </summary>
    public bool ShouldTracePrimitive()
        => _selectorTraceEnabled && _instructionCount >= 2000 && _instructionCount < 2100;

    /// <summary>
    /// Checks if we should trace message sends.
    /// </summary>
    public bool ShouldTraceSend()
        => _selectorTraceEnabled && _instructionCount < 10000;

    /// <summary>
    /// Checks if we should trace primitive failures.
    /// </summary>
    public bool ShouldTracePrimitiveFailure()
        => _selectorTraceEnabled && _instructionCount < 50000;

    /// <summary>
    /// Traces a message send entry.
    /// </summary>
    public void TraceSendEntry(string receiverClass, string selectorName, string fromMethod)
    {
        if (!ShouldTraceSend()) return;
        Console.WriteLine($"[SEND #{_instructionCount}] {receiverClass}>>{selectorName} (from {fromMethod})");
    }

    /// <summary>
    /// Traces periodic instruction count.
    /// </summary>
    public void TraceInstructionPeriodic(string className, string methodId, int ip)
    {
        if (!ShouldTraceInstructionPeriodic()) return;
        Console.WriteLine($"[{_instructionCount / 1000}K instructions] In: {className}>>{methodId}, IP={ip}");
    }

    /// <summary>
    /// Traces bytecode execution in debug range.
    /// </summary>
    public void TraceBytecodeRange(int ip, byte opcode, string methodId)
    {
        if (!ShouldTraceBytecodeRange()) return;
        Console.WriteLine($"[BYTECODE #{_instructionCount}] IP={ip}, opcode=0x{opcode:X2}, method={methodId}");
    }

    /// <summary>
    /// Traces primitive attempt.
    /// </summary>
    public void TracePrimitiveAttempt(int primitiveNumber, string methodId)
    {
        if (!ShouldTracePrimitive()) return;
        Console.WriteLine($"[PRIMITIVE #{_instructionCount}] Trying primitive {primitiveNumber} in {methodId}");
    }

    /// <summary>
    /// Traces primitive success.
    /// </summary>
    public void TracePrimitiveSuccess(int primitiveNumber)
    {
        if (!ShouldTracePrimitive()) return;
        Console.WriteLine($"[PRIMITIVE #{_instructionCount}] Primitive {primitiveNumber} succeeded");
    }

    /// <summary>
    /// Traces primitive failure.
    /// </summary>
    public void TracePrimitiveFailure(int primitiveNumber, string selectorName)
    {
        if (!ShouldTracePrimitiveFailure()) return;
        Console.WriteLine($"[PRIMITIVE #{_instructionCount}] Primitive {primitiveNumber} ({selectorName}) failed, falling back to bytecode");
    }

    /// <summary>
    /// Starts selector tracing mode.
    /// </summary>
    public void StartSelectorTrace()
    {
        _selectorTraceEnabled = true;
        _selectorTraceDepth = 0;
        _selectorTraceStack.Clear();
    }

    /// <summary>
    /// Stops selector tracing mode.
    /// </summary>
    public void StopSelectorTrace()
    {
        _selectorTraceEnabled = false;
        _selectorTraceDepth = 0;
    }

    /// <summary>
    /// Pushes a selector trace entry onto the stack.
    /// </summary>
    public void PushSelectorTrace(string className, string selector, int depth)
    {
        _selectorTraceStack.Push((className, selector, depth));
    }

    /// <summary>
    /// Pops a selector trace entry from the stack.
    /// </summary>
    public (string className, string selector, int depth) PopSelectorTrace()
    {
        return _selectorTraceStack.Pop();
    }

    /// <summary>
    /// Gets whether the selector trace stack has entries.
    /// </summary>
    public bool HasSelectorTraceEntries => _selectorTraceStack.Count > 0;

    /// <summary>
    /// Sets the selector trace depth directly.
    /// </summary>
    public void SetSelectorTraceDepth(int depth) => _selectorTraceDepth = depth;
}
