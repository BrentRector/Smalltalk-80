using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Provides object pooling for Context instances to reduce allocation pressure during interpretation.
/// This implementation significantly reduces garbage collection overhead by reusing Context objects.
/// </summary>
public static class ContextPool
{
    private static readonly ConcurrentStack<MethodContext> methodContextPool = new();
    private static readonly ConcurrentStack<BlockContext> blockContextPool = new();
    private static readonly ConcurrentStack<Stack<object?>> stackPool = new();
    private static readonly ConcurrentStack<object?[]> temporariesPool = new();
    
    // Pool statistics for monitoring and debugging
    private static long methodContextCreated = 0;
    private static long methodContextReused = 0;
    private static long blockContextCreated = 0;
    private static long blockContextReused = 0;
    private static long stackReused = 0;
    private static long temporariesReused = 0;
    
    /// <summary>
    /// Maximum number of contexts to keep in each pool to prevent unbounded memory growth.
    /// </summary>
    private const int MaxPoolSize = 1000;
    
    /// <summary>
    /// Maximum size for temporary arrays to pool (prevents pooling very large arrays).
    /// </summary>
    private const int MaxTemporariesSize = 32;
    
    /// <summary>
    /// Gets statistics about pool usage for debugging and monitoring.
    /// </summary>
    public static PoolStatistics Statistics => new()
    {
        MethodContextCreated = methodContextCreated,
        MethodContextReused = methodContextReused,
        MethodContextPoolSize = methodContextPool.Count,
        BlockContextCreated = blockContextCreated,
        BlockContextReused = blockContextReused,
        BlockContextPoolSize = blockContextPool.Count,
        StackPoolSize = stackPool.Count,
        StackReused = stackReused,
        TemporariesPoolSize = temporariesPool.Count,
        TemporariesReused = temporariesReused
    };
    
    /// <summary>
    /// Gets or creates a MethodContext instance, preferring pooled instances for performance.
    /// </summary>
    /// <param name="sender">The sending context.</param>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <param name="temporaries">Temporary variables array.</param>
    /// <returns>A MethodContext instance ready for use.</returns>
    public static MethodContext GetMethodContext(
        Context? sender,
        CompiledMethod method,
        SmalltalkObject receiver,
        object?[] arguments,
        object?[] temporaries)
    {
        MethodContext context;
        
        if (methodContextPool.TryPop(out var pooledContext))
        {
            context = pooledContext;
            System.Threading.Interlocked.Increment(ref methodContextReused);
        }
        else
        {
            context = new MethodContext();
            System.Threading.Interlocked.Increment(ref methodContextCreated);
        }
        
        // Initialize/reset the context
        context.Sender = sender;
        context.Method = method;
        context.Receiver = receiver;
        context.Arguments = arguments;
        context.Temporaries = temporaries;
        context.Stack = GetStack();
        context.PC = 0;
        
        return context;
    }
    
    /// <summary>
    /// Gets or creates a BlockContext instance, preferring pooled instances for performance.
    /// </summary>
    /// <param name="homeContext">The home context for the block.</param>
    /// <param name="startPC">Starting program counter for the block.</param>
    /// <param name="argumentCount">Number of arguments the block accepts.</param>
    /// <param name="arguments">Block arguments.</param>
    /// <param name="sender">The sending context.</param>
    /// <returns>A BlockContext instance ready for use.</returns>
    public static BlockContext GetBlockContext(
        Context homeContext,
        int startPC,
        int argumentCount,
        object?[] arguments,
        Context? sender)
    {
        BlockContext context;
        
        if (blockContextPool.TryPop(out var pooledContext))
        {
            context = pooledContext;
            System.Threading.Interlocked.Increment(ref blockContextReused);
        }
        else
        {
            context = new BlockContext();
            System.Threading.Interlocked.Increment(ref blockContextCreated);
        }
        
        // Initialize/reset the context
        context.HomeContext = homeContext;
        context.StartPC = startPC;
        context.ArgumentCount = argumentCount;
        context.Sender = sender;
        context.Method = homeContext.Method;
        context.Receiver = homeContext.Receiver;
        context.Arguments = arguments;
        context.Temporaries = homeContext.Temporaries; // Shared with home context
        context.Stack = GetStack();
        context.PC = startPC;
        
        return context;
    }
    
    /// <summary>
    /// Gets a Stack instance from the pool or creates a new one.
    /// </summary>
    /// <returns>A clean Stack instance ready for use.</returns>
    private static Stack<object?> GetStack()
    {
        if (stackPool.TryPop(out var pooledStack))
        {
            System.Threading.Interlocked.Increment(ref stackReused);
            return pooledStack;
        }
        
        return new Stack<object?>();
    }
    
    /// <summary>
    /// Gets a temporary variables array from the pool or creates a new one.
    /// </summary>
    /// <param name="size">The required size of the temporaries array.</param>
    /// <returns>A temporaries array of the specified size.</returns>
    public static object?[] GetTemporaries(int size)
    {
        if (size <= MaxTemporariesSize && temporariesPool.TryPop(out var pooledTemps) && pooledTemps.Length >= size)
        {
            // Clear the array and resize if needed
            Array.Clear(pooledTemps, 0, pooledTemps.Length);
            if (pooledTemps.Length == size)
            {
                System.Threading.Interlocked.Increment(ref temporariesReused);
                return pooledTemps;
            }
        }
        
        return new object?[size];
    }
    
    /// <summary>
    /// Returns a MethodContext to the pool for reuse.
    /// The context will be cleaned up and made available for future allocation.
    /// </summary>
    /// <param name="context">The MethodContext to return to the pool.</param>
    public static void ReturnMethodContext(MethodContext context)
    {
        if (context == null) return;
        
        // Return the stack to the pool
        if (context.Stack != null)
        {
            ReturnStack(context.Stack);
        }
        
        // Return temporaries to pool if they're a reasonable size
        if (context.Temporaries != null && context.Temporaries.Length <= MaxTemporariesSize)
        {
            ReturnTemporaries(context.Temporaries);
        }
        
        // Clean up the context to prevent memory leaks
        CleanupMethodContext(context);
        
        // Only add to pool if we haven't exceeded the maximum size
        if (methodContextPool.Count < MaxPoolSize)
        {
            methodContextPool.Push(context);
        }
        // If pool is full, let the context be garbage collected
    }
    
    /// <summary>
    /// Returns a BlockContext to the pool for reuse.
    /// The context will be cleaned up and made available for future allocation.
    /// </summary>
    /// <param name="context">The BlockContext to return to the pool.</param>
    public static void ReturnBlockContext(BlockContext context)
    {
        if (context == null) return;
        
        // Return the stack to the pool
        if (context.Stack != null)
        {
            ReturnStack(context.Stack);
        }
        
        // Note: Don't return temporaries for blocks as they share with home context
        
        // Clean up the context to prevent memory leaks
        CleanupBlockContext(context);
        
        // Only add to pool if we haven't exceeded the maximum size
        if (blockContextPool.Count < MaxPoolSize)
        {
            blockContextPool.Push(context);
        }
        // If pool is full, let the context be garbage collected
    }
    
    /// <summary>
    /// Returns a Stack to the pool for reuse.
    /// </summary>
    /// <param name="stack">The stack to return to the pool.</param>
    private static void ReturnStack(Stack<object?> stack)
    {
        if (stack == null) return;
        
        stack.Clear();
        
        if (stackPool.Count < MaxPoolSize)
        {
            stackPool.Push(stack);
        }
    }
    
    /// <summary>
    /// Returns a temporaries array to the pool for reuse.
    /// </summary>
    /// <param name="temporaries">The temporaries array to return to the pool.</param>
    private static void ReturnTemporaries(object?[] temporaries)
    {
        if (temporaries == null || temporaries.Length > MaxTemporariesSize) return;
        
        Array.Clear(temporaries, 0, temporaries.Length);
        
        if (temporariesPool.Count < MaxPoolSize)
        {
            temporariesPool.Push(temporaries);
        }
    }
    
    /// <summary>
    /// Clears all pooled contexts. Used for testing or when memory pressure is high.
    /// </summary>
    public static void ClearPools()
    {
        methodContextPool.Clear();
        blockContextPool.Clear();
        stackPool.Clear();
        temporariesPool.Clear();
    }
    
    /// <summary>
    /// Resets all statistics. Used for testing and monitoring.
    /// </summary>
    public static void ResetStatistics()
    {
        System.Threading.Interlocked.Exchange(ref methodContextCreated, 0);
        System.Threading.Interlocked.Exchange(ref methodContextReused, 0);
        System.Threading.Interlocked.Exchange(ref blockContextCreated, 0);
        System.Threading.Interlocked.Exchange(ref blockContextReused, 0);
        System.Threading.Interlocked.Exchange(ref stackReused, 0);
        System.Threading.Interlocked.Exchange(ref temporariesReused, 0);
    }
    
    /// <summary>
    /// Cleans up a MethodContext to prepare it for pooling.
    /// Removes references to prevent memory leaks while keeping the object structure intact.
    /// </summary>
    /// <param name="context">The context to clean up.</param>
    private static void CleanupMethodContext(MethodContext context)
    {
        context.Sender = null;
        context.Method = null!; // Will be reset when reused
        context.Receiver = null!; // Will be reset when reused
        context.Arguments = Array.Empty<object?>();
        context.Temporaries = Array.Empty<object?>();
        context.Stack = null!; // Will be reset when reused
        context.PC = 0;
    }
    
    /// <summary>
    /// Cleans up a BlockContext to prepare it for pooling.
    /// Removes references to prevent memory leaks while keeping the object structure intact.
    /// </summary>
    /// <param name="context">The context to clean up.</param>
    private static void CleanupBlockContext(BlockContext context)
    {
        context.HomeContext = null!; // Will be reset when reused
        context.StartPC = 0;
        context.ArgumentCount = 0;
        context.Sender = null;
        context.Method = null!; // Will be reset when reused
        context.Receiver = null!; // Will be reset when reused
        context.Arguments = Array.Empty<object?>();
        context.Temporaries = Array.Empty<object?>();
        context.Stack = null!; // Will be reset when reused
        context.PC = 0;
    }
}

/// <summary>
/// Statistics about Context pool usage for monitoring and debugging.
/// </summary>
public record PoolStatistics
{
    /// <summary>Total number of MethodContext instances created (not from pool).</summary>
    public long MethodContextCreated { get; init; }
    
    /// <summary>Total number of MethodContext instances reused from pool.</summary>
    public long MethodContextReused { get; init; }
    
    /// <summary>Current number of MethodContext instances in the pool.</summary>
    public int MethodContextPoolSize { get; init; }
    
    /// <summary>Total number of BlockContext instances created (not from pool).</summary>
    public long BlockContextCreated { get; init; }
    
    /// <summary>Total number of BlockContext instances reused from pool.</summary>
    public long BlockContextReused { get; init; }
    
    /// <summary>Current number of BlockContext instances in the pool.</summary>
    public int BlockContextPoolSize { get; init; }
    
    /// <summary>Current number of Stack instances in the pool.</summary>
    public int StackPoolSize { get; init; }
    
    /// <summary>Total number of Stack instances reused from pool.</summary>
    public long StackReused { get; init; }
    
    /// <summary>Current number of temporaries arrays in the pool.</summary>
    public int TemporariesPoolSize { get; init; }
    
    /// <summary>Total number of temporaries arrays reused from pool.</summary>
    public long TemporariesReused { get; init; }
    
    /// <summary>Overall reuse efficiency for MethodContext (0.0 to 1.0).</summary>
    public double MethodContextReuseRate => 
        MethodContextCreated + MethodContextReused == 0 ? 0.0 : 
        (double)MethodContextReused / (MethodContextCreated + MethodContextReused);
    
    /// <summary>Overall reuse efficiency for BlockContext (0.0 to 1.0).</summary>
    public double BlockContextReuseRate => 
        BlockContextCreated + BlockContextReused == 0 ? 0.0 : 
        (double)BlockContextReused / (BlockContextCreated + BlockContextReused);
    
    /// <summary>Total number of context instances managed by the pool.</summary>
    public long TotalContextsManaged => 
        MethodContextCreated + MethodContextReused + BlockContextCreated + BlockContextReused;
        
    /// <summary>Overall memory efficiency score combining all pool reuse rates.</summary>
    public double OverallEfficiency
    {
        get
        {
            var total = TotalContextsManaged + StackReused + TemporariesReused;
            var reused = MethodContextReused + BlockContextReused + StackReused + TemporariesReused;
            return total == 0 ? 0.0 : (double)reused / total;
        }
    }
}