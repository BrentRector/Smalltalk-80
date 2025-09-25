using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// High-performance object pool for MethodContext instances.
/// Reduces GC pressure by reusing context objects.
/// </summary>
public sealed class MethodContextPool
{
    /// <summary>
    /// Thread-local storage for fast, contention-free pooling.
    /// Each thread gets its own small pool to avoid synchronization overhead.
    /// </summary>
    [ThreadStatic]
    private static MethodContext[]? threadLocalPool;
    
    /// <summary>
    /// Current count of items in thread-local pool.
    /// </summary>
    [ThreadStatic]
    private static int threadLocalCount;
    
    /// <summary>
    /// Maximum size of thread-local pool.
    /// </summary>
    private const int ThreadLocalPoolSize = 32;
    
    /// <summary>
    /// Shared pool for overflow and cross-thread sharing.
    /// Uses lock-free concurrent bag for thread safety.
    /// </summary>
    private readonly ConcurrentBag<MethodContext> sharedPool = new();
    
    /// <summary>
    /// Maximum number of contexts to keep in the shared pool.
    /// </summary>
    private readonly int maxSharedPoolSize;
    
    /// <summary>
    /// Current approximate size of the shared pool.
    /// </summary>
    private int sharedPoolSize;
    
    /// <summary>
    /// Statistics for monitoring pool effectiveness.
    /// </summary>
    private long totalRented;
    private long totalReturned;
    private long totalCreated;
    
    /// <summary>
    /// Singleton instance for global access.
    /// </summary>
    private static readonly Lazy<MethodContextPool> instance = new(() => new MethodContextPool());
    
    /// <summary>
    /// Gets the singleton instance of the pool.
    /// </summary>
    public static MethodContextPool Instance => instance.Value;
    
    /// <summary>
    /// Creates a new method context pool.
    /// </summary>
    /// <param name="maxSharedPoolSize">Maximum contexts to keep in shared pool. Default is 1024.</param>
    private MethodContextPool(int maxSharedPoolSize = 1024)
    {
        this.maxSharedPoolSize = maxSharedPoolSize;
    }
    
    /// <summary>
    /// Rents a MethodContext from the pool or creates a new one if necessary.
    /// </summary>
    /// <param name="sender">The sender context.</param>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="method">The method to execute.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <param name="tempCount">Number of temporary variables.</param>
    /// <param name="stackSize">Size of the execution stack.</param>
    /// <returns>A MethodContext ready for use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodContext Rent(
        MethodContext? sender,
        OOP receiver,
        OOP method,
        OOP[] arguments,
        int tempCount,
        int stackSize)
    {
        Interlocked.Increment(ref totalRented);
        
        // Try thread-local pool first (fastest path)
        var context = TryGetFromThreadLocal();
        
        // Fall back to shared pool
        context ??= TryGetFromShared();
        
        // Create new if necessary
        if (context == null)
        {
            Interlocked.Increment(ref totalCreated);
            context = new PooledMethodContext();
        }
        
        // Initialize the context with new values
        if (context is PooledMethodContext pooledContext)
        {
            pooledContext.Initialize(sender, receiver, method, arguments, tempCount, stackSize);
        }
        
        return context;
    }
    
    /// <summary>
    /// Returns a MethodContext to the pool for reuse.
    /// </summary>
    /// <param name="context">The context to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(MethodContext context)
    {
        if (context == null || context is not PooledMethodContext pooledContext)
            return;
            
        Interlocked.Increment(ref totalReturned);
        
        // Reset the context for reuse
        pooledContext.Reset();
        
        // Try to add to thread-local pool first
        if (!TryReturnToThreadLocal(pooledContext))
        {
            // Fall back to shared pool if thread-local is full
            TryReturnToShared(pooledContext);
        }
    }
    
    /// <summary>
    /// Tries to get a context from thread-local pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodContext? TryGetFromThreadLocal()
    {
        threadLocalPool ??= new MethodContext[ThreadLocalPoolSize];
        
        if (threadLocalCount > 0)
        {
            var context = threadLocalPool[--threadLocalCount];
            threadLocalPool[threadLocalCount] = null!;
            return context;
        }
        
        return null;
    }
    
    /// <summary>
    /// Tries to return a context to thread-local pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReturnToThreadLocal(MethodContext context)
    {
        threadLocalPool ??= new MethodContext[ThreadLocalPoolSize];
        
        if (threadLocalCount < ThreadLocalPoolSize)
        {
            threadLocalPool[threadLocalCount++] = context;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Tries to get a context from the shared pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MethodContext? TryGetFromShared()
    {
        if (sharedPool.TryTake(out var context))
        {
            Interlocked.Decrement(ref sharedPoolSize);
            return context;
        }
        
        return null;
    }
    
    /// <summary>
    /// Tries to return a context to the shared pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TryReturnToShared(MethodContext context)
    {
        if (Interlocked.Increment(ref sharedPoolSize) <= maxSharedPoolSize)
        {
            sharedPool.Add(context);
        }
        else
        {
            // Pool is full, let GC collect this instance
            Interlocked.Decrement(ref sharedPoolSize);
        }
    }
    
    /// <summary>
    /// Gets pool statistics for monitoring.
    /// </summary>
    public (long rented, long returned, long created, int currentSharedSize) GetStatistics()
    {
        return (totalRented, totalReturned, totalCreated, sharedPoolSize);
    }
    
    /// <summary>
    /// Clears all pooled instances.
    /// Useful for testing or when memory needs to be reclaimed.
    /// </summary>
    public void Clear()
    {
        // Clear thread-local pool
        if (threadLocalPool != null)
        {
            Array.Clear(threadLocalPool, 0, threadLocalCount);
            threadLocalCount = 0;
        }
        
        // Clear shared pool
        while (sharedPool.TryTake(out _))
        {
            Interlocked.Decrement(ref sharedPoolSize);
        }
    }
}

/// <summary>
/// Pooled version of MethodContext with reset capability.
/// </summary>
internal sealed class PooledMethodContext : MethodContext
{
    private OOP[]? pooledArguments;
    private OOP[]? pooledTemporaries;
    private Stack? pooledStack;
    
    /// <summary>
    /// Creates a new pooled method context.
    /// </summary>
    public PooledMethodContext() : base(null, OOP.Nil, OOP.Nil, [], 0, 16)
    {
        // Base constructor will be called, but we'll override in Initialize
    }
    
    /// <summary>
    /// Initializes the context with new values for reuse.
    /// </summary>
    internal void Initialize(
        MethodContext? sender,
        OOP receiver,
        OOP method,
        OOP[] arguments,
        int tempCount,
        int stackSize)
    {
        Sender = sender;
        Receiver = receiver;
        Method = method;
        PC = 0;
        
        // Reuse or allocate arguments array
        var argCount = arguments?.Length ?? 0;
        if (pooledArguments == null || pooledArguments.Length != argCount)
        {
            pooledArguments = new OOP[argCount];
        }
        if (arguments != null)
        {
            Array.Copy(arguments, pooledArguments, argCount);
        }
        Arguments = pooledArguments;
        
        // Reuse or allocate temporaries array
        var totalTemps = argCount + tempCount;
        if (pooledTemporaries == null || pooledTemporaries.Length != totalTemps)
        {
            pooledTemporaries = new OOP[totalTemps];
        }
        
        // Copy arguments to temporaries
        for (int i = 0; i < argCount; i++)
        {
            pooledTemporaries[i] = pooledArguments[i];
        }
        
        // Initialize remaining temporaries to nil
        for (int i = argCount; i < totalTemps; i++)
        {
            pooledTemporaries[i] = OOP.Nil;
        }
        Temporaries = pooledTemporaries;
        
        // Reuse or create stack
        if (pooledStack == null || pooledStack.Capacity != stackSize)
        {
            pooledStack = new Stack(Math.Max(stackSize, 16));
        }
        else
        {
            pooledStack.Clear();
        }
        Stack = pooledStack;
    }
    
    /// <summary>
    /// Resets the context for return to pool.
    /// </summary>
    internal void Reset()
    {
        Sender = null;
        Receiver = OOP.Nil;
        Method = OOP.Nil;
        PC = 0;
        
        // Clear arrays but keep allocated memory
        if (pooledArguments != null)
        {
            Array.Clear(pooledArguments);
        }
        
        if (pooledTemporaries != null)
        {
            Array.Clear(pooledTemporaries);
        }
        
        pooledStack?.Clear();
    }
}