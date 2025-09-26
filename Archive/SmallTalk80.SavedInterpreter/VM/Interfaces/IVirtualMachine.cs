using System;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.VM.Interfaces;

/// <summary>
/// Represents a pure execution engine for the Smalltalk-80 Virtual Machine.
/// This interface defines a VM that receives pre-initialized state and executes bytecode.
/// </summary>
public interface IVirtualMachine : IDisposable
{
    /// <summary>
    /// Executes a Smalltalk method on a receiver with arguments.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The result of the method execution.</returns>
    OOP Send(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Executes a block closure with the given arguments.
    /// </summary>
    /// <param name="blockClosure">The block closure to execute.</param>
    /// <param name="arguments">The block arguments.</param>
    /// <returns>The result of the block execution.</returns>
    OOP ExecuteBlock(OOP blockClosure, OOP[] arguments);
    
    /// <summary>
    /// Executes a compiled method directly (for bootstrap and testing).
    /// </summary>
    /// <param name="method">The compiled method.</param>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The result of the method execution.</returns>
    OOP ExecuteMethod(OOP method, OOP receiver, OOP[] arguments);
    
    /// <summary>
    /// Compiles and executes a Smalltalk expression string.
    /// </summary>
    /// <param name="expression">The Smalltalk expression to evaluate.</param>
    /// <param name="receiver">Optional receiver for the expression (defaults to nil).</param>
    /// <returns>The result of evaluating the expression.</returns>
    OOP EvaluateExpression(string expression, OOP receiver = default);
    
    /// <summary>
    /// Gets the current execution statistics.
    /// </summary>
    /// <returns>Execution statistics including message sends, allocations, etc.</returns>
    IExecutionStatistics GetStatistics();
    
    /// <summary>
    /// Triggers a garbage collection cycle.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    IGarbageCollectionStatistics CollectGarbage();
    
    /// <summary>
    /// Gets whether the VM is currently executing code.
    /// </summary>
    bool IsExecuting { get; }
}

/// <summary>
/// Provides statistics about VM execution.
/// </summary>
public interface IExecutionStatistics
{
    /// <summary>
    /// Gets the total number of message sends performed.
    /// </summary>
    long TotalMessageSends { get; }
    
    /// <summary>
    /// Gets the total number of primitive calls.
    /// </summary>
    long TotalPrimitiveCalls { get; }
    
    /// <summary>
    /// Gets the total number of block activations.
    /// </summary>
    long TotalBlockActivations { get; }
    
    /// <summary>
    /// Gets the total number of method activations.
    /// </summary>
    long TotalMethodActivations { get; }
    
    /// <summary>
    /// Gets the total execution time in milliseconds.
    /// </summary>
    long TotalExecutionTimeMs { get; }
    
    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    int CurrentStackDepth { get; }
    
    /// <summary>
    /// Gets the maximum stack depth reached.
    /// </summary>
    int MaxStackDepth { get; }
}

/// <summary>
/// Provides statistics about garbage collection.
/// </summary>
public interface IGarbageCollectionStatistics
{
    /// <summary>
    /// Gets the number of objects collected.
    /// </summary>
    int ObjectsCollected { get; }
    
    /// <summary>
    /// Gets the amount of memory freed in bytes.
    /// </summary>
    long BytesFreed { get; }
    
    /// <summary>
    /// Gets the collection duration in milliseconds.
    /// </summary>
    long DurationMs { get; }
    
    /// <summary>
    /// Gets the total number of collections performed.
    /// </summary>
    int TotalCollections { get; }
}