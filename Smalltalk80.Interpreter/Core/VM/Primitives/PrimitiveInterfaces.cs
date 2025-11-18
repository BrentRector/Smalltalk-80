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
        RuntimeHeapAccessor? runtimeHeapAccess = null)
    {
        Receiver = receiver;
        Arguments = arguments;
        HeapManager = heapManager;
        StackManager = stackManager;
        ContextManager = contextManager;
        LayoutResolver = layoutResolver;
        Platform = platform;
        RuntimeHeapAccess = runtimeHeapAccess;
    }

    /// <summary>
    /// Gets a field value from an object using the layout resolver.
    /// </summary>
    public SmalltalkOOP GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
    {
        if (LayoutResolver == null)
            throw new InvalidOperationException("LayoutResolver not available");

        int fieldIndex = LayoutResolver.GetFieldIndex(className, fieldName);
        return HeapManager.GetInstanceVariable(oop, fieldIndex);
    }

    /// <summary>
    /// Sets a field value in an object using the layout resolver.
    /// </summary>
    public void SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
    {
        if (LayoutResolver == null)
            throw new InvalidOperationException("LayoutResolver not available");

        int fieldIndex = LayoutResolver.GetFieldIndex(className, fieldName);
        HeapManager.SetInstanceVariable(oop, fieldIndex, value);
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
    /// Registers a legacy primitive function (for backward compatibility).
    /// </summary>
    /// <param name="primitiveNumber">Blue Book primitive number.</param>
    /// <param name="function">Legacy primitive function delegate.</param>
    void RegisterLegacy(int primitiveNumber, PrimitiveFunction function);

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
