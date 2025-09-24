using System.Collections.Concurrent;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Core.Execution;

/// <summary>
/// Manages method and block contexts according to the Smalltalk-80 Blue Book specification.
/// </summary>
public sealed class ContextManager : IContextManager
{
    private readonly IObjectMemory _objectMemory;
    private readonly ILogger<ContextManager> _logger;
    
    // Context pools for performance
    private readonly ConcurrentBag<MethodContext> _methodContextPool;
    private readonly ConcurrentBag<BlockContext> _blockContextPool;
    
    // Active contexts tracking
    private readonly ConcurrentDictionary<int, IExecutionContext> _activeContexts;
    private int _nextContextId;
    private MethodContext? _activeContext;
    private MethodContext? _homeContext;
    
    // Statistics
    private long _methodContextsCreated;
    private long _blockContextsCreated;
    private long _contextsReused;

    public ContextManager(IObjectMemory objectMemory, ILogger<ContextManager> logger)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _methodContextPool = new ConcurrentBag<MethodContext>();
        _blockContextPool = new ConcurrentBag<BlockContext>();
        _activeContexts = new ConcurrentDictionary<int, IExecutionContext>();
        _nextContextId = 1;
    }
    
    /// <inheritdoc/>
    public MethodContext? ActiveContext => _activeContext;
    
    /// <inheritdoc/>
    public MethodContext? HomeContext => _homeContext;

    /// <inheritdoc/>
    public MethodContext CreateMethodContext(
        OOP receiver,
        OOP method,
        OOP[] arguments,
        MethodContext? sender = null)
    {
        // Try to reuse a pooled context
        MethodContext? context = null;
        if (_methodContextPool.TryTake(out var pooledContext))
        {
            // Reset and reuse the pooled context
            context = ReinitializeMethodContext(pooledContext, receiver, method, arguments, sender);
            Interlocked.Increment(ref _contextsReused);
            _logger.LogTrace("Reused method context from pool");
        }
        else
        {
            // Need to fetch the CompiledMethod from object memory
            var compiledMethod = GetCompiledMethod(method);
            
            // Create new context
            context = new MethodContext(receiver, compiledMethod, arguments, sender);
            Interlocked.Increment(ref _methodContextsCreated);
            _logger.LogDebug("Created new method context for method {Method}", method);
        }
        
        // Track the active context
        var contextId = Interlocked.Increment(ref _nextContextId);
        _activeContexts[contextId] = context;
        
        return context;
    }

    /// <inheritdoc/>
    public BlockContext CreateBlockContext(
        MethodContext home,
        int argumentCount)
    {
        if (home == null)
            throw new ArgumentNullException(nameof(home));
        
        // Blocks start at the current PC of the home context
        var initialPc = home.ProgramCounter;
        
        // Try to reuse a pooled context
        BlockContext? context = null;
        if (_blockContextPool.TryTake(out var pooledContext))
        {
            // Reset and reuse - note: BlockContext constructor is not easily reusable
            // For now, always create new
            context = new BlockContext(home, argumentCount, initialPc);
            // Return unused pooled context
            _blockContextPool.Add(pooledContext);
        }
        else
        {
            context = new BlockContext(home, argumentCount, initialPc);
        }
        
        Interlocked.Increment(ref _blockContextsCreated);
        _logger.LogDebug("Created block context with {ArgCount} arguments", argumentCount);
        
        // Track the active context
        var contextId = Interlocked.Increment(ref _nextContextId);
        _activeContexts[contextId] = context;
        
        return context;
    }

    /// <inheritdoc/>
    public void ActivateContext(MethodContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        
        _logger.LogTrace("Activating context: {Context}", context);
        
        // Save the current context state and switch to new context
        _activeContext = context;
        
        // Update home context if this is a method context (not from a block)
        if (context.Sender == null)
        {
            _homeContext = context;
        }
    }

    /// <inheritdoc/>
    public void ReturnFromContext(MethodContext context, OOP value)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        
        _logger.LogDebug("Returning from context with value {Value}", value);
        
        // Push return value to sender's stack
        if (context.Sender is MethodContext sender)
        {
            sender.Push(value);
            
            // Return context to pool for reuse
            ReturnContextToPool(context);
            
            // Activate the sender
            ActivateContext(sender);
        }
        else
        {
            _logger.LogDebug("Top-level return with value {Value}", value);
        }
    }
    
    /// <summary>
    /// Returns a context to the pool for reuse.
    /// </summary>
    private void ReturnContextToPool(IExecutionContext context)
    {
        // Remove from active contexts
        var entry = _activeContexts.FirstOrDefault(kvp => kvp.Value == context);
        if (entry.Value != null)
        {
            _activeContexts.TryRemove(entry.Key, out _);
        }
        
        // Add to appropriate pool
        if (context is MethodContext methodContext && _methodContextPool.Count < 100)
        {
            // Clear sensitive data before pooling
            ClearMethodContext(methodContext);
            _methodContextPool.Add(methodContext);
            _logger.LogTrace("Returned method context to pool");
        }
        else if (context is BlockContext blockContext && _blockContextPool.Count < 50)
        {
            // Block contexts are harder to reuse due to home context dependency
            // For now, don't pool them
            _logger.LogTrace("Block context not pooled");
        }
    }
    
    /// <summary>
    /// Clears a method context for reuse.
    /// </summary>
    private void ClearMethodContext(MethodContext context)
    {
        // Reset stack
        while (context.StackPointer > 0)
        {
            context.Pop();
        }
        
        // Clear temporaries
        for (int i = 0; i < context.Method.TemporaryCount; i++)
        {
            context.SetTemporary(i, OOP.Nil);
        }
        
        // Reset PC
        context.ProgramCounter = 0;
        
        // Clear sender
        context.Sender = null;
    }
    
    /// <summary>
    /// Reinitializes a pooled method context.
    /// </summary>
    private MethodContext ReinitializeMethodContext(
        MethodContext context,
        OOP receiver,
        OOP method,
        OOP[] arguments,
        MethodContext? sender)
    {
        // This would need to properly reinitialize the context
        // For now, just create a new one
        var compiledMethod = GetCompiledMethod(method);
        return new MethodContext(receiver, compiledMethod, arguments, sender);
    }
    
    /// <summary>
    /// Gets a CompiledMethod from an OOP.
    /// </summary>
    private CompiledMethod GetCompiledMethod(OOP methodOop)
    {
        // In a full implementation, this would fetch from object memory
        // For now, create a minimal compiled method
        return CompiledMethod.Create(
            argumentCount: 0,
            temporaryCount: 0,
            primitiveIndex: 0,
            literals: [],
            bytecodes: [120]); // returnReceiver
    }
    
    /// <summary>
    /// Gets statistics about context management.
    /// </summary>
    public (long MethodContexts, long BlockContexts, long Reused, int Active) GetStatistics()
    {
        return (_methodContextsCreated, _blockContextsCreated, _contextsReused, _activeContexts.Count);
    }
    
    /// <summary>
    /// Clears all context pools.
    /// </summary>
    public void ClearPools()
    {
        while (_methodContextPool.TryTake(out _)) { }
        while (_blockContextPool.TryTake(out _)) { }
        _activeContexts.Clear();
        _logger.LogInformation("Cleared all context pools");
    }
}