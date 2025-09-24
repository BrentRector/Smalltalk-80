using Core.Abstractions.Execution;
using Core.Memory;

namespace Core.Execution;

/// <summary>
/// Represents an execution context for a Smalltalk method.
/// Manages the method's activation record including receiver, arguments, temporaries, and stack.
/// </summary>
public sealed class MethodContext : IExecutionContext
{
    private readonly OOP[] _stack;
    private readonly OOP[] _temporaries;
    private int _stackPointer;

    /// <summary>
    /// Gets the receiver of this method activation.
    /// </summary>
    public OOP Receiver { get; }

    /// <summary>
    /// Gets the compiled method being executed.
    /// </summary>
    public CompiledMethod Method { get; }

    /// <summary>
    /// Gets the arguments passed to this method.
    /// </summary>
    public OOP[] Arguments { get; }

    /// <summary>
    /// Gets or sets the sender context (caller).
    /// </summary>
    public IExecutionContext? Sender { get; set; }

    /// <summary>
    /// Gets or sets the current program counter.
    /// </summary>
    public int ProgramCounter { get; set; }

    /// <summary>
    /// Gets the current stack pointer.
    /// </summary>
    public int StackPointer => _stackPointer;

    /// <summary>
    /// Gets the value at the top of the stack without removing it.
    /// </summary>
    public OOP StackTop => _stackPointer > 0 ? _stack[_stackPointer - 1] : OOP.Nil;

    /// <summary>
    /// Initializes a new instance of the MethodContext class.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <param name="sender">The sender context.</param>
    public MethodContext(OOP receiver, CompiledMethod method, OOP[] arguments, IExecutionContext? sender = null)
    {
        Receiver = receiver;
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        Sender = sender;

        // Initialize temporaries
        _temporaries = new OOP[method.TemporaryCount];
        for (var i = 0; i < _temporaries.Length; i++)
        {
            _temporaries[i] = OOP.Nil;
        }

        // Initialize stack
        var stackSize = method.UsesLargeContext ? 56 : 16; // Blue Book stack sizes
        _stack = new OOP[stackSize];
        _stackPointer = 0;
        ProgramCounter = 0;
    }

    /// <inheritdoc/>
    public void Push(OOP value)
    {
        if (_stackPointer >= _stack.Length)
        {
            throw new StackOverflowException("Method context stack overflow");
        }

        _stack[_stackPointer++] = value;
    }

    /// <inheritdoc/>
    public OOP Pop()
    {
        if (_stackPointer <= 0)
        {
            throw new InvalidOperationException("Method context stack underflow");
        }

        return _stack[--_stackPointer];
    }

    /// <inheritdoc/>
    public OOP Peek(int offset = 0)
    {
        var index = _stackPointer - 1 - offset;
        if (index < 0 || index >= _stackPointer)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid stack offset");
        }

        return _stack[index];
    }

    /// <inheritdoc/>
    public void Dup()
    {
        if (_stackPointer <= 0)
        {
            throw new InvalidOperationException("Cannot duplicate from empty stack");
        }

        Push(_stack[_stackPointer - 1]);
    }

    /// <inheritdoc/>
    public OOP GetArgument(int index)
    {
        if (index < 0 || index >= Arguments.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Argument index {index} out of range");
        }

        return Arguments[index];
    }

    /// <inheritdoc/>
    public OOP GetTemporary(int index)
    {
        if (index < 0 || index >= _temporaries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Temporary index {index} out of range");
        }

        return _temporaries[index];
    }

    /// <inheritdoc/>
    public void SetTemporary(int index, OOP value)
    {
        if (index < 0 || index >= _temporaries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Temporary index {index} out of range");
        }

        _temporaries[index] = value;
    }

    /// <inheritdoc/>
    public OOP GetLiteral(int index) => Method.GetLiteral(index);

    /// <inheritdoc/>
    public byte GetNextBytecode()
    {
        if (Method.IsAtEnd(ProgramCounter))
        {
            throw new InvalidOperationException("Program counter past end of bytecodes");
        }

        return Method.GetBytecode(ProgramCounter++);
    }

    /// <inheritdoc/>
    public byte PeekNextBytecode()
    {
        if (Method.IsAtEnd(ProgramCounter))
        {
            throw new InvalidOperationException("Program counter past end of bytecodes");
        }

        return Method.GetBytecode(ProgramCounter);
    }

    /// <inheritdoc/>
    public bool IsAtEnd() => Method.IsAtEnd(ProgramCounter);

    /// <inheritdoc/>
    public void Jump(int offset)
    {
        var newPc = ProgramCounter + offset;
        if (newPc < 0 || newPc > Method.Bytecodes.Length)
        {
            throw new InvalidOperationException($"Jump offset {offset} would result in invalid PC {newPc}");
        }

        ProgramCounter = newPc;
    }

    /// <inheritdoc/>
    public void JumpTo(int address)
    {
        if (address < 0 || address > Method.Bytecodes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"Invalid jump address {address}");
        }

        ProgramCounter = address;
    }

    /// <summary>
    /// Creates a block context from this method context.
    /// </summary>
    /// <param name="argumentCount">Number of block arguments.</param>
    /// <param name="initialPc">Initial program counter for the block.</param>
    /// <returns>A new block context.</returns>
    public BlockContext CreateBlockContext(int argumentCount, int initialPc)
    {
        return new BlockContext(this, argumentCount, initialPc);
    }

    /// <summary>
    /// Returns a string representation of this method context.
    /// </summary>
    public override string ToString() => 
        $"MethodContext[receiver={Receiver}, pc={ProgramCounter}, sp={_stackPointer}, temps={_temporaries.Length}]";
}

/// <summary>
/// Represents a block closure execution context.
/// </summary>
public sealed class BlockContext : IExecutionContext
{
    private readonly MethodContext _homeContext;
    private readonly OOP[] _stack;
    private readonly OOP[] _arguments;
    private int _stackPointer;

    /// <summary>
    /// Gets the receiver (from home context).
    /// </summary>
    public OOP Receiver => _homeContext.Receiver;

    /// <summary>
    /// Gets the sender context.
    /// </summary>
    public IExecutionContext? Sender { get; set; }

    /// <summary>
    /// Gets or sets the program counter.
    /// </summary>
    public int ProgramCounter { get; set; }

    /// <summary>
    /// Gets the current stack pointer.
    /// </summary>
    public int StackPointer => _stackPointer;

    /// <summary>
    /// Gets the value at the top of the stack.
    /// </summary>
    public OOP StackTop => _stackPointer > 0 ? _stack[_stackPointer - 1] : OOP.Nil;

    /// <summary>
    /// Gets the home context (method context that created this block).
    /// </summary>
    public MethodContext HomeContext => _homeContext;

    /// <summary>
    /// Initializes a new instance of the BlockContext class.
    /// </summary>
    /// <param name="homeContext">The method context that created this block.</param>
    /// <param name="argumentCount">Number of block arguments.</param>
    /// <param name="initialPc">Initial program counter.</param>
    public BlockContext(MethodContext homeContext, int argumentCount, int initialPc)
    {
        _homeContext = homeContext ?? throw new ArgumentNullException(nameof(homeContext));
        _arguments = new OOP[argumentCount];
        _stack = new OOP[16]; // Default block stack size
        _stackPointer = 0;
        ProgramCounter = initialPc;

        // Initialize arguments to nil
        for (var i = 0; i < _arguments.Length; i++)
        {
            _arguments[i] = OOP.Nil;
        }
    }

    /// <summary>
    /// Sets a block argument value.
    /// </summary>
    /// <param name="index">The argument index.</param>
    /// <param name="value">The argument value.</param>
    public void SetArgument(int index, OOP value)
    {
        if (index < 0 || index >= _arguments.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _arguments[index] = value;
    }

    /// <inheritdoc/>
    public void Push(OOP value)
    {
        if (_stackPointer >= _stack.Length)
        {
            throw new StackOverflowException("Block context stack overflow");
        }

        _stack[_stackPointer++] = value;
    }

    /// <inheritdoc/>
    public OOP Pop()
    {
        if (_stackPointer <= 0)
        {
            throw new InvalidOperationException("Block context stack underflow");
        }

        return _stack[--_stackPointer];
    }

    /// <inheritdoc/>
    public OOP Peek(int offset = 0)
    {
        var index = _stackPointer - 1 - offset;
        if (index < 0 || index >= _stackPointer)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        return _stack[index];
    }

    /// <inheritdoc/>
    public void Dup()
    {
        if (_stackPointer <= 0)
        {
            throw new InvalidOperationException("Cannot duplicate from empty stack");
        }

        Push(_stack[_stackPointer - 1]);
    }

    /// <inheritdoc/>
    public OOP GetArgument(int index)
    {
        if (index < 0 || index >= _arguments.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _arguments[index];
    }

    /// <inheritdoc/>
    public OOP GetTemporary(int index) => _homeContext.GetTemporary(index);

    /// <inheritdoc/>
    public void SetTemporary(int index, OOP value) => _homeContext.SetTemporary(index, value);

    /// <inheritdoc/>
    public OOP GetLiteral(int index) => _homeContext.GetLiteral(index);

    /// <inheritdoc/>
    public byte GetNextBytecode() => _homeContext.Method.GetBytecode(ProgramCounter++);

    /// <inheritdoc/>
    public byte PeekNextBytecode() => _homeContext.Method.GetBytecode(ProgramCounter);

    /// <inheritdoc/>
    public bool IsAtEnd() => _homeContext.Method.IsAtEnd(ProgramCounter);

    /// <inheritdoc/>
    public void Jump(int offset)
    {
        ProgramCounter += offset;
    }

    /// <inheritdoc/>
    public void JumpTo(int address)
    {
        ProgramCounter = address;
    }

    /// <summary>
    /// Returns a string representation of this block context.
    /// </summary>
    public override string ToString() => 
        $"BlockContext[pc={ProgramCounter}, sp={_stackPointer}, args={_arguments.Length}]";
}