using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions.Memory;
using Core.Memory;
using Core.Symbols;

namespace Core.Abstractions;

/// <summary>
/// Main interface for the Smalltalk-80 Virtual Machine.
/// Coordinates all subsystems and provides the primary execution interface.
/// </summary>
public interface IVirtualMachine
{
    /// <summary>
    /// Gets the object memory subsystem.
    /// </summary>
    IObjectMemory ObjectMemory { get; }
    
    /// <summary>
    /// Gets the special objects registry.
    /// </summary>
    ISpecialObjectsRegistry SpecialObjects { get; }
    
    /// <summary>
    /// Gets the symbol manager.
    /// </summary>
    ISymbolManager SymbolManager { get; }
    
    /// <summary>
    /// Initializes the virtual machine with a system image.
    /// </summary>
    /// <param name="imagePath">Path to the system image file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if initialization succeeded.</returns>
    Task<bool> InitializeAsync(string imagePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a Smalltalk expression.
    /// </summary>
    /// <param name="expression">The Smalltalk expression to evaluate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of evaluating the expression.</returns>
    Task<OOP> EvaluateAsync(string expression, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a message to an object.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    OOP Send(OOP receiver, OOP selector, params OOP[] arguments);
    
    /// <summary>
    /// Performs a full garbage collection.
    /// </summary>
    /// <returns>The number of objects collected.</returns>
    int PerformGarbageCollection();
    
    /// <summary>
    /// Gets a class by name.
    /// </summary>
    /// <param name="className">The name of the class.</param>
    /// <returns>The class OOP, or Nil if not found.</returns>
    OOP GetClass(string className);
    
    /// <summary>
    /// Creates a new instance of a class.
    /// </summary>
    /// <param name="classOop">The class to instantiate.</param>
    /// <returns>The new instance.</returns>
    OOP NewInstance(OOP classOop);
    
    /// <summary>
    /// Creates a new instance with indexed fields.
    /// </summary>
    /// <param name="classOop">The class to instantiate.</param>
    /// <param name="size">The number of indexed fields.</param>
    /// <returns>The new instance.</returns>
    OOP NewInstance(OOP classOop, int size);
    
    /// <summary>
    /// Shuts down the virtual machine cleanly.
    /// </summary>
    Task ShutdownAsync();
    
    /// <summary>
    /// Gets the current execution statistics.
    /// </summary>
    VMStatistics GetStatistics();
}

/// <summary>
/// Virtual machine execution statistics.
/// </summary>
public record VMStatistics(
    long MethodsExecuted,
    long BytecodesFetched,
    long MessagesSent,
    long ObjectsAllocated,
    long GarbageCollections,
    long MemoryUsed,
    long MemoryAvailable);