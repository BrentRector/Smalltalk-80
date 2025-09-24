using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Abstractions.Primitives;
using Core.DependencyInjection;
using Core.Execution;
using Core.Memory;
using Core.Process;
using Core.Symbols;
using Core.System;
using Microsoft.Extensions.Logging;

namespace Core;

/// <summary>
/// The main Smalltalk-80 Virtual Machine implementation.
/// Combines all components to execute Smalltalk bytecode.
/// </summary>
public sealed class VirtualMachine : IVirtualMachine
{
    private readonly IObjectMemory _objectMemory;
    private readonly IMethodLookup _methodLookup;
    private readonly IPrimitiveTable _primitiveTable;
    private readonly IBytecodeDispatcher _bytecodeDispatcher;
    private readonly IExecutionEngine _executionEngine;
    private readonly IContextManager _contextManager;
    private readonly ISpecialObjectsRegistry _specialObjects;
    private readonly ISymbolManager _symbolManager;
    private readonly IProcessScheduler _processScheduler;
    private readonly ILogger<VirtualMachine> _logger;
    private readonly VirtualMachineOptions _options;
    private readonly Stack<IExecutionContext> _contextStack;
    
    private IExecutionContext? _activeContext;
    private bool _running;

    /// <inheritdoc/>
    public IObjectMemory ObjectMemory => _objectMemory;
    
    /// <inheritdoc/>
    public ISpecialObjectsRegistry SpecialObjects => _specialObjects;
    
    /// <inheritdoc/>
    public ISymbolManager SymbolManager => _symbolManager;

    /// <summary>
    /// Gets the current active execution context.
    /// </summary>
    public IExecutionContext? ActiveContext => _activeContext;

    /// <summary>
    /// Gets whether the VM is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Initializes a new instance of the VirtualMachine class.
    /// </summary>
    public VirtualMachine(
        IObjectMemory objectMemory,
        IMethodLookup methodLookup,
        IPrimitiveTable primitiveTable,
        IBytecodeDispatcher bytecodeDispatcher,
        IExecutionEngine executionEngine,
        IContextManager contextManager,
        ISpecialObjectsRegistry specialObjects,
        ISymbolManager symbolManager,
        IProcessScheduler processScheduler,
        VirtualMachineOptions options,
        ILogger<VirtualMachine> logger)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _methodLookup = methodLookup ?? throw new ArgumentNullException(nameof(methodLookup));
        _primitiveTable = primitiveTable ?? throw new ArgumentNullException(nameof(primitiveTable));
        _bytecodeDispatcher = bytecodeDispatcher ?? throw new ArgumentNullException(nameof(bytecodeDispatcher));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _specialObjects = specialObjects ?? throw new ArgumentNullException(nameof(specialObjects));
        _symbolManager = symbolManager ?? throw new ArgumentNullException(nameof(symbolManager));
        _processScheduler = processScheduler ?? throw new ArgumentNullException(nameof(processScheduler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextStack = new Stack<IExecutionContext>();
    }

    /// <summary>
    /// Starts the VM execution with an initial message send.
    /// </summary>
    /// <param name="receiver">The receiver of the initial message.</param>
    /// <param name="selector">The selector to send.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of execution.</returns>
    public OOP Execute(OOP receiver, string selector, params OOP[] arguments)
    {
        if (_running)
        {
            throw new InvalidOperationException("VM is already running");
        }

        try
        {
            _running = true;
            
            // Look up the method
            var receiverClass = _objectMemory.FetchClassOf(receiver);
            var methodOop = _methodLookup.LookupMethod(receiverClass, selector);
            
            if (methodOop == null || methodOop.Value.IsNil)
            {
                throw new InvalidOperationException($"Method not found: {selector}");
            }

            // Create initial context
            var method = LoadCompiledMethod(methodOop.Value);
            _activeContext = CreateMethodContext(receiver, method, arguments);
            
            // Main execution loop
            return ExecutionLoop();
        }
        finally
        {
            _running = false;
            _activeContext = null;
            _contextStack.Clear();
        }
    }

    /// <summary>
    /// Performs a message send.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <param name="isSuper">Whether this is a super send.</param>
    public void Send(OOP receiver, string selector, OOP[] arguments, bool isSuper = false)
    {
        // Look up method
        var lookupClass = isSuper
            ? _objectMemory.FetchClassOf(_activeContext!.Receiver)
            : _objectMemory.FetchClassOf(receiver);

        var methodOop = isSuper
            ? _methodLookup.LookupMethodInSuper(lookupClass, selector)
            : _methodLookup.LookupMethod(lookupClass, selector);

        if (methodOop == null || methodOop.Value.IsNil)
        {
            // Message not understood
            HandleMessageNotUnderstood(receiver, selector, arguments);
            return;
        }

        // Load and activate the method
        var method = LoadCompiledMethod(methodOop.Value);
        ActivateMethod(receiver, method, arguments);
    }

    /// <summary>
    /// Returns a value from the current context.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <param name="targetContext">Optional target context for non-local returns.</param>
    public void Return(OOP value, IExecutionContext? targetContext = null)
    {
        if (_activeContext == null)
        {
            throw new InvalidOperationException("No active context");
        }

        // Handle non-local return
        if (targetContext != null)
        {
            // Pop contexts until we reach the target
            while (_activeContext != targetContext && _contextStack.Count > 0)
            {
                _activeContext = _contextStack.Pop();
            }

            if (_activeContext != targetContext)
            {
                throw new InvalidOperationException("Target context not found in stack");
            }
        }

        // Pop to caller context
        if (_contextStack.Count > 0)
        {
            _activeContext = _contextStack.Pop();
            _activeContext.Push(value);
        }
        else
        {
            // Top-level return
            _activeContext = null;
        }
    }

    private OOP ExecutionLoop()
    {
        while (_activeContext != null)
        {
            try
            {
                // Check if we're at a primitive method
                if (_activeContext is MethodContext methodContext && 
                    methodContext.Method.HasPrimitive &&
                    methodContext.ProgramCounter == 0)
                {
                    // Try to execute primitive
                    if (_primitiveTable.ExecutePrimitive(methodContext.Method.PrimitiveIndex, _activeContext))
                    {
                        // Primitive succeeded, return its result
                        var result = _activeContext.Pop();
                        Return(result);
                        continue;
                    }
                    // Primitive failed, continue with bytecode execution
                }

                // Execute next bytecode
                if (!_bytecodeDispatcher.ExecuteNextBytecode(_activeContext))
                {
                    // Context completed
                    if (_contextStack.Count > 0)
                    {
                        var result = _activeContext.StackTop;
                        _activeContext = _contextStack.Pop();
                        _activeContext.Push(result);
                    }
                    else
                    {
                        // Execution complete
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleVMError(ex);
            }
        }

        // Return the final result
        return _activeContext?.StackTop ?? OOP.Nil;
    }

    private void ActivateMethod(OOP receiver, CompiledMethod method, OOP[] arguments)
    {
        // Validate argument count
        if (arguments.Length != method.ArgumentCount)
        {
            throw new ArgumentException($"Wrong number of arguments: expected {method.ArgumentCount}, got {arguments.Length}");
        }

        // Create new context
        var newContext = CreateMethodContext(receiver, method, arguments);
        
        // Push current context and activate new one
        if (_activeContext != null)
        {
            _contextStack.Push(_activeContext);
        }
        
        _activeContext = newContext;
    }

    private IExecutionContext CreateMethodContext(OOP receiver, CompiledMethod method, OOP[] arguments)
    {
        // Use the context manager to create contexts
        var methodOop = OOP.Nil; // Would need to get the actual method OOP
        var context = _contextManager.CreateMethodContext(
            receiver,
            methodOop, 
            arguments,
            _activeContext as MethodContext);

        // Set up the context with VM reference for sends
        context.SetVirtualMachine(this);
        
        return context;
    }

    private CompiledMethod LoadCompiledMethod(OOP methodOop)
    {
        // Load method from object memory
        // This is simplified - in full implementation would deserialize from object memory
        
        // For now, create a simple method
        return CompiledMethod.Create(
            argumentCount: 0,
            temporaryCount: 0,
            primitiveIndex: 0,
            literals: [],
            bytecodes: [0x78] // returnReceiver
        );
    }

    private void HandleMessageNotUnderstood(OOP receiver, string selector, OOP[] arguments)
    {
        // Send doesNotUnderstand: message
        // This is simplified - in full implementation would create Message object
        throw new InvalidOperationException($"Message not understood: {selector}");
    }

    private void HandleVMError(Exception ex)
    {
        // Log error and attempt recovery
        Console.Error.WriteLine($"VM Error: {ex.Message}");
        
        // In full implementation, would send error: message to context
        throw new InvalidOperationException("VM execution error", ex);
    }

    /// <summary>
    /// Performs garbage collection.
    /// </summary>
    public void CollectGarbage()
    {
        _objectMemory.CollectGarbage();
    }

    /// <summary>
    /// Flushes the method cache.
    /// </summary>
    public void FlushMethodCache()
    {
        _methodLookup.FlushCache();
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing VM with image: {ImagePath}", imagePath);
        
        // In a full implementation, this would load the system image
        // For now, just return success
        await Task.CompletedTask;
        return true;
    }
    
    /// <inheritdoc/>
    public async Task<OOP> EvaluateAsync(string expression, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Evaluating expression: {Expression}", expression);
        
        // In a full implementation, this would compile and execute the expression
        // For now, return nil
        await Task.CompletedTask;
        return OOP.Nil;
    }
    
    /// <inheritdoc/>
    public OOP Send(OOP receiver, OOP selector, params OOP[] arguments)
    {
        var selectorString = _symbolManager.GetString(selector);
        _logger.LogTrace("Send {Selector} to {Receiver}", selectorString, receiver);
        
        // Use existing Send implementation
        Send(receiver, selectorString, arguments);
        
        // Return result from stack
        return _activeContext?.StackTop ?? OOP.Nil;
    }
    
    /// <inheritdoc/>
    public int PerformGarbageCollection()
    {
        _logger.LogInformation("Performing garbage collection");
        CollectGarbage();
        return 0; // Would return actual collection count
    }
    
    /// <inheritdoc/>
    public OOP GetClass(string className)
    {
        return _specialObjects.GetClass(className);
    }
    
    /// <inheritdoc/>
    public OOP NewInstance(OOP classOop)
    {
        return _objectMemory.AllocateObject(classOop, 0);
    }
    
    /// <inheritdoc/>
    public OOP NewInstance(OOP classOop, int size)
    {
        return _objectMemory.AllocateObject(classOop, size);
    }
    
    /// <inheritdoc/>
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down VM");
        _running = false;
        _contextStack.Clear();
        _activeContext = null;
        await Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public VMStatistics GetStatistics() => new(
        MethodsExecuted: 0,
        BytecodesFetched: 0,
        MessagesSent: 0,
        ObjectsAllocated: 0,
        GarbageCollections: 0,
        MemoryUsed: 0,
        MemoryAvailable: _options.MaxMemorySize
    );

    /// <summary>
    /// Represents VM execution statistics.
    /// </summary>
    public sealed record VmStatistics
    {
        /// <summary>
        /// The current context stack depth.
        /// </summary>
        public required int ContextStackDepth { get; init; }

        /// <summary>
        /// Whether the VM is running.
        /// </summary>
        public required bool IsRunning { get; init; }

        /// <summary>
        /// Description of the active context.
        /// </summary>
        public required string ActiveContextDescription { get; init; }
    }
}

/// <summary>
/// Extension methods for MethodContext to integrate with VirtualMachine.
/// </summary>
internal static class MethodContextExtensions
{
    private static readonly ConditionalWeakTable<MethodContext, VirtualMachine> VmTable = new();

    /// <summary>
    /// Sets the VirtualMachine reference for a MethodContext.
    /// </summary>
    internal static void SetVirtualMachine(this MethodContext context, VirtualMachine vm)
    {
        VmTable.AddOrUpdate(context, vm);
    }

    /// <summary>
    /// Gets the VirtualMachine reference for a MethodContext.
    /// </summary>
    internal static VirtualMachine? GetVirtualMachine(this MethodContext context)
    {
        return VmTable.TryGetValue(context, out var vm) ? vm : null;
    }
}