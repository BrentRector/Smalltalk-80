using Core.Abstractions;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Abstractions.Primitives;
using Core.Execution;
using Core.Memory;
using Core.Primitives;
using Core.Process;
using Core.Symbols;
using Core.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.DependencyInjection;

/// <summary>
/// Extension methods for configuring Smalltalk VM services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Smalltalk Virtual Machine services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSmalltalkVirtualMachine(
        this IServiceCollection services,
        Action<VirtualMachineOptions>? configureOptions = null)
    {
        // Configure options
        var options = new VirtualMachineOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        
        // Memory subsystem
        services.AddSingleton<IObjectMemory, ObjectMemory>();
        
        // Symbol management
        services.AddSingleton<ISymbolManager, SymbolManager>();
        
        // Special objects registry
        services.AddSingleton<ISpecialObjectsRegistry, SpecialObjectsRegistry>();
        
        // Execution subsystem
        services.AddSingleton<IExecutionEngine, ExecutionEngine>();
        services.AddSingleton<IContextManager, ContextManager>();
        services.AddSingleton<IBytecodeDispatcher, BytecodeDispatcher>();
        services.AddSingleton<IMethodLookup, MethodLookup>();
        
        // Process scheduling
        services.AddSingleton<IProcessScheduler, ProcessScheduler>();
        
        // Primitives
        services.AddSingleton<IPrimitiveTable, PrimitiveTable>();
        
        // Main VM (composite root)
        services.AddSingleton<IVirtualMachine, VirtualMachine>();
        
        // Configure logging if not already configured
        if (!services.Any(x => x.ServiceType == typeof(ILoggerFactory)))
        {
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(options.LogLevel);
                builder.AddConsole();
                
                if (options.EnableDebugLogging)
                {
                    builder.AddDebug();
                }
            });
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds Smalltalk VM with minimal configuration for testing.
    /// </summary>
    public static IServiceCollection AddSmalltalkVirtualMachineForTesting(
        this IServiceCollection services)
    {
        return services.AddSmalltalkVirtualMachine(options =>
        {
            options.InitialMemorySize = 1024 * 1024; // 1MB for tests
            options.EnableGarbageCollection = false;
            options.LogLevel = LogLevel.Warning;
        });
    }
}

/// <summary>
/// Configuration options for the Smalltalk Virtual Machine.
/// </summary>
public class VirtualMachineOptions
{
    /// <summary>
    /// Gets or sets the initial memory size in bytes.
    /// </summary>
    public int InitialMemorySize { get; set; } = 16 * 1024 * 1024; // 16MB default
    
    /// <summary>
    /// Gets or sets the maximum memory size in bytes.
    /// </summary>
    public int MaxMemorySize { get; set; } = 256 * 1024 * 1024; // 256MB default
    
    /// <summary>
    /// Gets or sets whether garbage collection is enabled.
    /// </summary>
    public bool EnableGarbageCollection { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the garbage collection threshold (percentage of memory used).
    /// </summary>
    public double GarbageCollectionThreshold { get; set; } = 0.75;
    
    /// <summary>
    /// Gets or sets whether to enable method caching.
    /// </summary>
    public bool EnableMethodCache { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the method cache size.
    /// </summary>
    public int MethodCacheSize { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets whether to enable context pooling.
    /// </summary>
    public bool EnableContextPooling { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of pooled contexts.
    /// </summary>
    public int MaxPooledContexts { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets the logging level.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    
    /// <summary>
    /// Gets or sets whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to enable bytecode tracing.
    /// </summary>
    public bool EnableBytecodeTracing { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to enable method tracing.
    /// </summary>
    public bool EnableMethodTracing { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the execution timeout in milliseconds.
    /// </summary>
    public int ExecutionTimeoutMs { get; set; } = 30000; // 30 seconds default
}