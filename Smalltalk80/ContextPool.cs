using System;
using System.Collections.Generic;

/// <summary>
/// Provides centralized Context creation and statistics tracking for the Smalltalk-80 interpreter.
/// Note: Due to C# init-only properties in Context classes, actual object pooling is not currently possible.
/// This class provides a foundation for future optimization when Context design allows pooling.
/// Extracted as part of Phase 1 refactoring to improve performance monitoring and centralized creation.
/// </summary>
public static class ContextPool
{
    // Statistics for monitoring and debugging
    private static long methodContextCreated = 0;
    private static long blockContextCreated = 0;
    
    /// <summary>
    /// Gets statistics about context creation for debugging and monitoring.
    /// </summary>
    public static PoolStatistics Statistics => new()
    {
        MethodContextCreated = methodContextCreated,
        BlockContextCreated = blockContextCreated
    };
    
    /// <summary>
    /// Creates a MethodContext instance with centralized creation and statistics tracking.
    /// Note: Due to Context using init-only properties, actual pooling is not possible with current design.
    /// This provides a centralized creation point and statistics tracking for future optimization.
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
        // Track creation statistics
        System.Threading.Interlocked.Increment(ref methodContextCreated);
        
        return new MethodContext
        {
            Sender = sender,
            Method = method,
            Receiver = receiver,
            Arguments = arguments,
            Temporaries = temporaries,
            Stack = new Stack<object?>(),
            PC = 0
        };
    }
    
    /// <summary>
    /// Creates a BlockContext instance with centralized creation and statistics tracking.
    /// Note: Due to Context using init-only properties, actual pooling is not possible with current design.
    /// This provides a centralized creation point and statistics tracking for future optimization.
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
        // Track creation statistics
        System.Threading.Interlocked.Increment(ref blockContextCreated);
        
        return new BlockContext
        {
            HomeContext = homeContext,
            StartPC = startPC,
            ArgumentCount = argumentCount,
            Sender = sender,
            Method = homeContext.Method,
            Receiver = homeContext.Receiver,
            Arguments = arguments,
            Temporaries = homeContext.Temporaries, // Shared with home context
            Stack = new Stack<object?>(),
            PC = startPC
        };
    }
    
    /// <summary>
    /// Placeholder for returning a MethodContext to the pool.
    /// Currently does nothing due to init-only property constraints.
    /// Kept for API compatibility and future optimization when Context design allows pooling.
    /// </summary>
    /// <param name="context">The MethodContext to return (currently ignored).</param>
    public static void ReturnMethodContext(MethodContext context)
    {
        // Cannot actually pool due to init-only properties
        // This method is kept for API compatibility and future optimization
    }
    
    /// <summary>
    /// Placeholder for returning a BlockContext to the pool.
    /// Currently does nothing due to init-only property constraints.
    /// Kept for API compatibility and future optimization when Context design allows pooling.
    /// </summary>
    /// <param name="context">The BlockContext to return (currently ignored).</param>
    public static void ReturnBlockContext(BlockContext context)
    {
        // Cannot actually pool due to init-only properties
        // This method is kept for API compatibility and future optimization
    }
    
    /// <summary>
    /// Resets creation statistics. Used for testing and monitoring.
    /// </summary>
    public static void ResetStatistics()
    {
        System.Threading.Interlocked.Exchange(ref methodContextCreated, 0);
        System.Threading.Interlocked.Exchange(ref blockContextCreated, 0);
    }
}

/// <summary>
/// Statistics about Context creation for monitoring and debugging.
/// </summary>
public record PoolStatistics
{
    /// <summary>Total number of MethodContext instances created.</summary>
    public long MethodContextCreated { get; init; }
    
    /// <summary>Total number of BlockContext instances created.</summary>
    public long BlockContextCreated { get; init; }
    
    /// <summary>Total number of context instances created.</summary>
    public long TotalContextsCreated => MethodContextCreated + BlockContextCreated;
}