using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Memory;
using Core.Symbols;
using Microsoft.Extensions.Logging;

namespace Core.Execution;

/// <summary>
/// Pure context-switching execution engine implementation.
/// Follows the Smalltalk-80 Blue Book specification for method execution.
/// </summary>
public sealed class ExecutionEngine : IExecutionEngine
{
    private readonly IBytecodeDispatcher _bytecodeDispatcher;
    private readonly IContextManager _contextManager;
    private readonly IMethodLookup _methodLookup;
    private readonly IObjectMemory _objectMemory;
    private readonly ISpecialObjectsRegistry _specialObjects;
    private readonly ISymbolManager _symbolManager;
    private readonly ILogger<ExecutionEngine> _logger;
    
    // Context management
    private MethodContext? _activeContext;
    private readonly ConcurrentStack<MethodContext> _contextStack;
    private readonly SemaphoreSlim _executionSemaphore;
    
    // Performance tracking
    private long _methodsExecuted;
    private long _bytecodesFetched;
    private long _contextsCreated;

    public ExecutionEngine(
        IBytecodeDispatcher bytecodeDispatcher,
        IContextManager contextManager,
        IMethodLookup methodLookup,
        IObjectMemory objectMemory,
        ISpecialObjectsRegistry specialObjects,
        ISymbolManager symbolManager,
        ILogger<ExecutionEngine> logger)
    {
        _bytecodeDispatcher = bytecodeDispatcher ?? throw new ArgumentNullException(nameof(bytecodeDispatcher));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _methodLookup = methodLookup ?? throw new ArgumentNullException(nameof(methodLookup));
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _specialObjects = specialObjects ?? throw new ArgumentNullException(nameof(specialObjects));
        _symbolManager = symbolManager ?? throw new ArgumentNullException(nameof(symbolManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _contextStack = new ConcurrentStack<MethodContext>();
        _executionSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc/>
    public async Task<ExecutionResult> ExecuteMethodAsync(
        OOP receiver,
        OOP method,
        OOP[] arguments,
        CancellationToken cancellationToken = default)
    {
        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Executing method {Method} on receiver {Receiver} with {ArgCount} arguments",
                method, receiver, arguments.Length);

            // Create method context
            var context = _contextManager.CreateMethodContext(
                receiver, method, arguments, _activeContext);
            
            Interlocked.Increment(ref _contextsCreated);
            
            // Execute the method
            return await ExecuteContextAsync(context, cancellationToken);
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public void HandleReturn(MethodContext context, OOP value)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        _logger.LogDebug("Handling return from context {Context} with value {Value}", 
            context, value);

        // Push return value to sender's stack
        if (context.Sender != null)
        {
            if (context.Sender is MethodContext senderContext)
            {
                senderContext.Push(value);
                
                // Restore sender as active context
                _activeContext = senderContext;
                _contextStack.TryPop(out _); // Remove returning context
            }
            
            _logger.LogDebug("Restored sender context as active: {Context}", _activeContext);
        }
        else
        {
            // Top-level return
            _logger.LogDebug("Top-level return with value {Value}", value);
            _activeContext = null;
        }
    }

    /// <inheritdoc/>
    public void HandleBlockReturn(BlockContext context, OOP value)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        _logger.LogDebug("Handling block return from context {Context} with value {Value}",
            context, value);

        // Block returns to its home context
        var home = context.HomeContext;
        if (home != null)
        {
            // Find and return to the home context's sender
            HandleReturn(home, value);
        }
        else
        {
            _logger.LogWarning("Block return with no home context");
            throw new InvalidOperationException("Cannot return from block with no home context");
        }
    }

    private async Task<ExecutionResult> ExecuteContextAsync(
        MethodContext context,
        CancellationToken cancellationToken)
    {
        // Activate the context
        _contextManager.ActivateContext(context);
        _activeContext = context;
        _contextStack.Push(context);
        
        try
        {
            // Check if method has a primitive
            var primitiveIndex = GetPrimitiveIndex(context.Method);
            if (primitiveIndex > 0)
            {
                _logger.LogDebug("Executing primitive {PrimitiveIndex}", primitiveIndex);
                
                // Try primitive execution first
                var primitiveResult = await ExecutePrimitiveAsync(
                    primitiveIndex, context, cancellationToken);
                
                if (primitiveResult.Status == ExecutionStatus.Success)
                {
                    // Primitive succeeded, return its result
                    HandleReturn(context, primitiveResult.Value);
                    return primitiveResult;
                }
                
                // Primitive failed, fall through to bytecode execution
                _logger.LogDebug("Primitive {PrimitiveIndex} failed, executing bytecode", 
                    primitiveIndex);
            }

            // Execute bytecodes
            return await ExecuteBytecodesAsync(context, cancellationToken);
        }
        catch (StackUnderflowException ex)
        {
            _logger.LogError(ex, "Stack underflow in context {Context}", context);
            return new ExecutionResult(
                OOP.Nil,
                ExecutionStatus.StackUnderflow,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution error in context {Context}", context);
            return new ExecutionResult(
                OOP.Nil,
                ExecutionStatus.Error,
                ex.Message);
        }
    }

    private Task<ExecutionResult> ExecuteBytecodesAsync(
        MethodContext context,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ExecuteBytecodes(context, cancellationToken), cancellationToken);
    }

    private ExecutionResult ExecuteBytecodes(
        MethodContext context,
        CancellationToken cancellationToken)
    {
        var maxIterations = 1_000_000; // Safety limit
        var iterations = 0;
        
        while (!cancellationToken.IsCancellationRequested && iterations < maxIterations)
        {
            // Check if we've returned
            if (_activeContext != context)
            {
                // Context has changed due to return
                break;
            }

            // Fetch and dispatch next bytecode
            var bytecode = FetchBytecode(context);
            Interlocked.Increment(ref _bytecodesFetched);
            
            _logger.LogTrace("PC={PC}, Bytecode={Bytecode:X2}, Stack depth={Depth}",
                context.ProgramCounter, bytecode, context.StackPointer);

            // Dispatch the bytecode
            var continueExecution = _bytecodeDispatcher.ExecuteBytecode(bytecode, context);
            
            if (!continueExecution)
            {
                // Bytecode indicated to stop (e.g., return bytecode)
                break;
            }
            
            iterations++;
        }
        
        if (iterations >= maxIterations)
        {
            _logger.LogWarning("Execution limit reached in context {Context}", context);
            return new ExecutionResult(
                OOP.Nil,
                ExecutionStatus.Error,
                "Execution limit exceeded");
        }
        
        // Get the result from the stack
        var result = context.StackPointer > 0 ? context.Pop() : OOP.Nil;
        
        Interlocked.Increment(ref _methodsExecuted);
        
        return new ExecutionResult(result, ExecutionStatus.Success);
    }

    private byte FetchBytecode(MethodContext context)
    {
        // Fetch bytecode at current PC from compiled method
        if (context.IsAtEnd())
        {
            throw new InvalidOperationException(
                $"PC {context.ProgramCounter} exceeds bytecode length");
        }
        
        var bytecode = context.GetNextBytecode();
        
        return bytecode;
    }

    private byte[] GetBytecodes(CompiledMethod method)
    {
        return method.Bytecodes;
    }

    private int GetPrimitiveIndex(CompiledMethod method)
    {
        return method.PrimitiveIndex;
    }

    private async Task<ExecutionResult> ExecutePrimitiveAsync(
        int primitiveIndex,
        MethodContext context,
        CancellationToken cancellationToken)
    {
        // Primitive execution would be delegated to IPrimitiveTable
        // For now, return failure to trigger bytecode execution
        return await Task.FromResult(new ExecutionResult(
            OOP.Nil,
            ExecutionStatus.PrimitiveFailed));
    }

    /// <summary>
    /// Gets execution statistics.
    /// </summary>
    public (long MethodsExecuted, long BytecodesFetched, long ContextsCreated) GetStatistics()
    {
        return (_methodsExecuted, _bytecodesFetched, _contextsCreated);
    }

    /// <summary>
    /// Resets execution statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _methodsExecuted, 0);
        Interlocked.Exchange(ref _bytecodesFetched, 0);
        Interlocked.Exchange(ref _contextsCreated, 0);
    }
}

/// <summary>
/// Exception thrown when stack underflow occurs.
/// </summary>
public class StackUnderflowException : Exception
{
    public StackUnderflowException(string message) : base(message) { }
    public StackUnderflowException(string message, Exception innerException) 
        : base(message, innerException) { }
}