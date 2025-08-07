namespace Smalltalk80.Core;

/// <summary>
/// Defines the contract for a Smalltalk-80 virtual machine implementation.
/// The VM manages object memory, class registry, primitive operations, and bytecode execution.
/// </summary>
public interface IVirtualMachine
{
    /// <summary>
    /// Gets the object memory manager responsible for object allocation and garbage collection.
    /// </summary>
    IObjectMemory ObjectMemory { get; }

    /// <summary>
    /// Gets the class registry that manages all classes and metaclasses in the system.
    /// </summary>
    IClassRegistry ClassRegistry { get; }

    /// <summary>
    /// Gets the primitive operation dispatcher for handling primitive method calls.
    /// </summary>
    IPrimitiveDispatcher PrimitiveDispatcher { get; }

    /// <summary>
    /// Gets the bytecode interpreter for executing compiled methods.
    /// </summary>
    IInterpreter Interpreter { get; }

    /// <summary>
    /// Bootstraps the VM with the minimal class hierarchy and essential methods.
    /// </summary>
    /// <param name="provider">The bootstrap data provider (e.g., JSON file reader).</param>
    /// <returns>A task that completes when bootstrap is finished.</returns>
    Task BootstrapAsync(IBootstrapProvider provider);

    /// <summary>
    /// Executes a compiled method with the given receiver and arguments.
    /// </summary>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="receiver">The object receiving the message.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The result of method execution.</returns>
    ISmalltalkObject? Execute(ICompiledMethod method, ISmalltalkObject receiver, params ISmalltalkObject[] arguments);

    /// <summary>
    /// Sends a message to an object and returns the result.
    /// </summary>
    /// <param name="selector">The message selector (method name).</param>
    /// <param name="receiver">The object receiving the message.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    ISmalltalkObject? SendMessage(ISymbol selector, ISmalltalkObject receiver, params ISmalltalkObject[] arguments);

    /// <summary>
    /// Loads and executes a Smalltalk source file.
    /// </summary>
    /// <param name="filePath">Path to the Smalltalk source file.</param>
    /// <returns>A task that completes when the file is loaded.</returns>
    Task LoadFileAsync(string filePath);

    /// <summary>
    /// Gets a global variable by name.
    /// </summary>
    /// <param name="name">The name of the global variable.</param>
    /// <returns>The global variable value, or null if not found.</returns>
    ISmalltalkObject? GetGlobal(string name);

    /// <summary>
    /// Sets a global variable.
    /// </summary>
    /// <param name="name">The name of the global variable.</param>
    /// <param name="value">The value to set.</param>
    void SetGlobal(string name, ISmalltalkObject value);

    /// <summary>
    /// Gets the current VM configuration.
    /// </summary>
    IVMConfiguration Configuration { get; }

    /// <summary>
    /// Event raised when the VM state changes (e.g., during bootstrap phases).
    /// </summary>
    event EventHandler<VMStateChangedEventArgs>? StateChanged;
}

/// <summary>
/// Represents the VM configuration options.
/// </summary>
public interface IVMConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable tracing for debugging.
    /// </summary>
    bool EnableTracing { get; set; }

    /// <summary>
    /// Gets or sets the maximum stack depth for method calls.
    /// </summary>
    int MaxStackDepth { get; set; }

    /// <summary>
    /// Gets or sets the initial heap size in bytes.
    /// </summary>
    int InitialHeapSize { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the method cache for performance.
    /// </summary>
    bool EnableMethodCache { get; set; }

    /// <summary>
    /// Gets or sets the bootstrap mode (Phase1Only, Phase1And2, Full).
    /// </summary>
    BootstrapMode BootstrapMode { get; set; }
}

/// <summary>
/// Defines the bootstrap modes available for VM initialization.
/// </summary>
public enum BootstrapMode
{
    /// <summary>
    /// Only execute Phase 1 (VM primitives) bootstrap.
    /// </summary>
    Phase1Only,

    /// <summary>
    /// Execute Phase 1 and Phase 2 (messaging) bootstrap.
    /// </summary>
    Phase1And2,

    /// <summary>
    /// Execute all three bootstrap phases including file loading.
    /// </summary>
    Full
}

/// <summary>
/// Event arguments for VM state change notifications.
/// </summary>
public class VMStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous VM state.
    /// </summary>
    public VMState OldState { get; }

    /// <summary>
    /// Gets the new VM state.
    /// </summary>
    public VMState NewState { get; }

    /// <summary>
    /// Gets an optional message describing the state change.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Initializes a new instance of the VMStateChangedEventArgs class.
    /// </summary>
    public VMStateChangedEventArgs(VMState oldState, VMState newState, string? message = null)
    {
        OldState = oldState;
        NewState = newState;
        Message = message;
    }
}

/// <summary>
/// Represents the current state of the virtual machine.
/// </summary>
public enum VMState
{
    /// <summary>
    /// VM has not been initialized.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// VM is executing Phase 1 bootstrap (VM primitives).
    /// </summary>
    BootstrappingPhase1,

    /// <summary>
    /// VM is executing Phase 2 bootstrap (messaging).
    /// </summary>
    BootstrappingPhase2,

    /// <summary>
    /// VM is executing Phase 3 bootstrap (file loading).
    /// </summary>
    BootstrappingPhase3,

    /// <summary>
    /// VM is ready for normal operation.
    /// </summary>
    Ready,

    /// <summary>
    /// VM is executing code.
    /// </summary>
    Executing,

    /// <summary>
    /// VM has encountered an error.
    /// </summary>
    Error
}