using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Result of a primitive operation.
/// Encapsulates success/failure status and return value together.
/// Replaces the pattern of returning nil and setting ctx.PrimitiveSuccess = false.
/// </summary>
public readonly struct PrimitiveResult
{
    /// <summary>Gets whether the primitive succeeded.</summary>
    public bool Success { get; }

    /// <summary>Gets the return value (valid only if Success is true).</summary>
    public SmalltalkOOP Value { get; }

    private PrimitiveResult(bool success, SmalltalkOOP value)
    {
        Success = success;
        Value = value;
    }

    /// <summary>Creates a successful result with the given value.</summary>
    public static PrimitiveResult Ok(SmalltalkOOP value) => new(true, value);

    /// <summary>Creates a failed result (value will be nil).</summary>
    public static PrimitiveResult Fail() => new(false, SpecialObjectsArray.Instance.Nil);

    /// <summary>Creates a result from a boolean condition.</summary>
    public static PrimitiveResult FromBool(bool condition) =>
        condition ? Ok(SpecialObjectsArray.Instance.True) : Ok(SpecialObjectsArray.Instance.False);
}

/// <summary>
/// Execution context provided to primitives via dependency injection.
/// Contains receiver, arguments, and all dependencies needed by primitives.
/// Replaces IPrimitiveContext to break the circular dependency with VirtualMachine.
/// </summary>
public readonly struct PrimitiveExecutionContext
{
    /// <summary>Gets the receiver object for this primitive invocation.</summary>
    public SmalltalkOOP Receiver { get; }

    /// <summary>Gets the arguments array for this primitive invocation.</summary>
    public SmalltalkOOP[] Arguments { get; }

    /// <summary>Gets the heap manager for memory operations.</summary>
    public ISmalltalkHeapManager HeapManager { get; }

    /// <summary>Gets the stack manager for stack operations (optional for simple primitives).</summary>
    public IStackManager? StackManager { get; }

    /// <summary>Gets the context manager for context operations (optional for simple primitives).</summary>
    public IContextManager? ContextManager { get; }

    /// <summary>Gets the layout resolver for field access (optional).</summary>
    public LayoutResolver? LayoutResolver { get; }

    /// <summary>Gets the platform bridge for platform operations (optional, may be null in headless mode).</summary>
    public IPlatformBridge? Platform { get; }

    /// <summary>Gets the runtime heap accessor for class/global lookups.</summary>
    public RuntimeHeapAccessor? RuntimeHeapAccess { get; }

    /// <summary>Gets the current instruction pointer when primitive was called (for block primitives).</summary>
    public int InstructionPointer { get; }

    /// <summary>Gets the cache manager for method/runtime cache operations.</summary>
    public ICacheManager? CacheManager { get; }

    /// <summary>Gets the message sender for perform primitives.</summary>
    public IPrimitiveMessageSender? MessageSender { get; }

    /// <summary>
    /// Creates a new primitive execution context.
    /// </summary>
    public PrimitiveExecutionContext(
        SmalltalkOOP receiver,
        SmalltalkOOP[] arguments,
        ISmalltalkHeapManager heapManager,
        IStackManager? stackManager = null,
        IContextManager? contextManager = null,
        LayoutResolver? layoutResolver = null,
        IPlatformBridge? platform = null,
        RuntimeHeapAccessor? runtimeHeapAccess = null,
        int instructionPointer = 0,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null)
    {
        Receiver = receiver;
        Arguments = arguments;
        HeapManager = heapManager;
        StackManager = stackManager;
        ContextManager = contextManager;
        LayoutResolver = layoutResolver;
        Platform = platform;
        RuntimeHeapAccess = runtimeHeapAccess;
        InstructionPointer = instructionPointer;
        CacheManager = cacheManager;
        MessageSender = messageSender;
    }

    /// <summary>
    /// Gets a field value from an object by querying the runtime heap for field layout.
    /// The Smalltalk heap is the authoritative source of class structure at runtime.
    /// </summary>
    public SmalltalkOOP GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
    {
        if (RuntimeHeapAccess == null)
            throw new InvalidOperationException("RuntimeHeapAccess not available");

        // Get the class OOP from the heap
        var classOOP = HeapManager.GetClassOOP(oop);

        // Query the heap for the field index
        int fieldIndex = RuntimeHeapAccess.GetFieldIndex(classOOP, fieldName);
        if (fieldIndex < 0)
            throw new InvalidOperationException($"Field '{fieldName}' not found in class '{className}'");

        return HeapManager.GetInstanceVariable(oop, fieldIndex);
    }

    /// <summary>
    /// Sets a field value in an object by querying the runtime heap for field layout.
    /// The Smalltalk heap is the authoritative source of class structure at runtime.
    /// </summary>
    public void SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
    {
        if (RuntimeHeapAccess == null)
            throw new InvalidOperationException("RuntimeHeapAccess not available");

        // Get the class OOP from the heap
        var classOOP = HeapManager.GetClassOOP(oop);

        // Query the heap for the field index
        int fieldIndex = RuntimeHeapAccess.GetFieldIndex(classOOP, fieldName);
        if (fieldIndex < 0)
            throw new InvalidOperationException($"Field '{fieldName}' not found in class '{className}'");

        HeapManager.SetInstanceVariable(oop, fieldIndex, value);
    }

    /// <summary>
    /// Gets the currently active context.
    /// </summary>
    public SmalltalkOOP GetActiveContext()
    {
        if (ContextManager == null)
            throw new InvalidOperationException("ContextManager not available");
        return ContextManager.ActiveContext;
    }

    /// <summary>
    /// Sets the active context (for context-switching primitives).
    /// </summary>
    public void SetActiveContext(SmalltalkOOP contextOOP)
    {
        if (ContextManager == null)
            throw new InvalidOperationException("ContextManager not available");
        ContextManager.SetActiveContext(contextOOP);
    }

    /// <summary>
    /// Gets the BlockContext class OOP.
    /// </summary>
    public SmalltalkOOP GetBlockContextClassOOP()
    {
        if (RuntimeHeapAccess == null)
            throw new InvalidOperationException("RuntimeHeapAccess not available");
        return RuntimeHeapAccess.GetClass("BlockContext");
    }

    /// <summary>
    /// Allocates a new object instance with optional indexed fields.
    /// </summary>
    public SmalltalkOOP AllocateObject(string className, int indexedSize)
    {
        if (RuntimeHeapAccess == null)
            throw new InvalidOperationException("RuntimeHeapAccess not available");

        var classOOP = RuntimeHeapAccess.GetClass(className);
        return HeapManager.AllocateInstanceOf(classOOP, indexedSize);
    }

    /// <summary>
    /// Gets the class name for an object.
    /// </summary>
    public string? GetClassNameForObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return "class:SmallInteger";

        var className = HeapManager.GetClassName(objectOOP);
        if (className == null)
            return null;

        return "class:" + className;
    }

    /// <summary>
    /// Gets a global object by name from SystemDictionary.
    /// </summary>
    public SmalltalkOOP GetGlobalObjectByName(string globalName)
    {
        if (RuntimeHeapAccess == null)
            throw new InvalidOperationException("RuntimeHeapAccess not available");

        // Remove "class:" or "global:" prefix if present
        string name = globalName;
        if (globalName.StartsWith("class:"))
            name = globalName.Substring(6);
        else if (globalName.StartsWith("global:"))
            name = globalName.Substring(7);

        // For classes, use GetClass
        if (globalName.StartsWith("class:"))
            return RuntimeHeapAccess.GetClass(name);

        // For globals, look up in SystemDictionary
        var systemDictOOP = SpecialObjectsArray.Instance.Smalltalk;
        return RuntimeHeapAccess.LookupGlobalByName(systemDictOOP, name);
    }
}

/// <summary>
/// Interface for individual primitive implementations.
/// Primitives receive dependencies via constructor injection and are stateless.
/// </summary>
public interface IPrimitive
{
    /// <summary>
    /// Executes the primitive operation.
    /// </summary>
    /// <param name="context">Execution context with receiver, args, and dependencies.</param>
    /// <returns>Result with success flag and return value.</returns>
    PrimitiveResult Execute(PrimitiveExecutionContext context);
}

/// <summary>
/// Interface for cache management operations used by primitives.
/// </summary>
public interface ICacheManager
{
    /// <summary>Clears the method lookup cache.</summary>
    void ClearMethodCache();

    /// <summary>Invalidates all runtime caches (method, class, field layout).</summary>
    void InvalidateAllRuntimeCaches();
}

/// <summary>
/// Interface for message sending operations used by perform primitives.
/// Named IPrimitiveMessageSender to avoid conflict with existing IMessageDispatcher.
/// </summary>
public interface IPrimitiveMessageSender
{
    /// <summary>
    /// Sends a message to a receiver object.
    /// </summary>
    /// <param name="receiver">Receiver object.</param>
    /// <param name="selector">Selector Symbol.</param>
    /// <param name="arguments">Arguments array.</param>
    /// <returns>Result of message send.</returns>
    SmalltalkOOP SendMessage(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] arguments);
}

/// <summary>
/// Registry and dispatch for primitive operations.
/// Replaces static PrimitiveTable with dependency-injectable design.
/// </summary>
public interface IPrimitiveRegistry
{
    /// <summary>
    /// Registers a primitive implementation.
    /// </summary>
    /// <param name="primitiveNumber">Blue Book primitive number.</param>
    /// <param name="primitive">Primitive implementation.</param>
    void Register(int primitiveNumber, IPrimitive primitive);


    /// <summary>
    /// Executes a primitive by number using new interface.
    /// </summary>
    /// <param name="primitiveNumber">Blue Book primitive number.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>Primitive result.</returns>
    PrimitiveResult Execute(int primitiveNumber, PrimitiveExecutionContext context);

    /// <summary>
    /// Executes a primitive by number using legacy interface (for backward compatibility).
    /// </summary>
    /// <param name="primitiveNumber">Blue Book primitive number.</param>
    /// <param name="ctx">Legacy primitive context.</param>
    /// <param name="receiver">Receiver object.</param>
    /// <param name="args">Arguments array.</param>
    /// <returns>Return value (check ctx.PrimitiveSuccess for success).</returns>
    SmalltalkOOP ExecuteLegacy(int primitiveNumber, IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args);

    /// <summary>
    /// Executes a primitive with extended dependencies for new IPrimitive implementations.
    /// </summary>
    SmalltalkOOP ExecuteLegacyExtended(
        int primitiveNumber,
        IPrimitiveContext ctx,
        SmalltalkOOP receiver,
        SmalltalkOOP[] args,
        RuntimeHeapAccessor? runtimeHeapAccess,
        LayoutResolver? layoutResolver,
        IContextManager? contextManager = null,
        int instructionPointer = 0,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null);

    /// <summary>
    /// Checks if a primitive is registered.
    /// </summary>
    /// <param name="primitiveNumber">Blue Book primitive number.</param>
    /// <returns>True if primitive is registered.</returns>
    bool IsRegistered(int primitiveNumber);

    /// <summary>
    /// Gets the total number of registered primitives.
    /// </summary>
    int Count { get; }
}
