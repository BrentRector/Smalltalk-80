namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;
using System.Collections.Generic;

/// <summary>
/// Represents a Smalltalk Semaphore for process synchronization.
/// </summary>
/// <remarks>
/// From the Blue Book page 258:
/// A Semaphore is used to synchronize processes. It maintains a count of
/// excess signals and a list of processes waiting on the semaphore.
/// 
/// Instance variables:
/// - excessSignals: Number of signals sent but not yet received
/// - waitingProcesses: LinkedList of processes waiting on this semaphore
/// </remarks>
public sealed class SSemaphore : SInstance
{
    // Instance variable indices
    private const int ExcessSignalsIndex = 0;
    private const int WaitingProcessesIndex = 1;
    
    private readonly Queue<SProcess> waitingProcesses;
    
    /// <summary>
    /// Initializes a new instance of the SSemaphore class.
    /// </summary>
    /// <param name="semaphoreClass">The Semaphore class.</param>
    /// <param name="initialSignals">Initial number of excess signals (default 0).</param>
    public SSemaphore(SClass semaphoreClass, int initialSignals = 0)
        : base(semaphoreClass, 2) // 2 instance variables
    {
        waitingProcesses = new Queue<SProcess>();
        SetExcessSignals(SmallInteger.From(initialSignals));
        
        // Create a LinkedList object for waiting processes
        var listClass = SystemDictionary.LookupClass("LinkedList");
        if (listClass != null)
        {
            SetInstanceVariable(WaitingProcessesIndex, new SInstance(listClass));
        }
        else
        {
            SetInstanceVariable(WaitingProcessesIndex, SNil.Instance);
        }
    }
    
    /// <summary>
    /// Gets or sets the number of excess signals.
    /// </summary>
    public SmallInteger ExcessSignals
    {
        get => (SmallInteger)(GetInstanceVariable(ExcessSignalsIndex) ?? SmallInteger.From(0));
        set => SetInstanceVariable(ExcessSignalsIndex, value);
    }
    
    /// <summary>
    /// Sets the excess signals count.
    /// </summary>
    private void SetExcessSignals(SmallInteger count)
    {
        SetInstanceVariable(ExcessSignalsIndex, count);
    }
    
    /// <summary>
    /// Adds a process to the waiting list.
    /// </summary>
    public void AddWaitingProcess(SProcess process)
    {
        waitingProcesses.Enqueue(process);
    }
    
    /// <summary>
    /// Removes and returns the first waiting process.
    /// </summary>
    public SProcess? RemoveWaitingProcess()
    {
        if (waitingProcesses.Count > 0)
        {
            return waitingProcesses.Dequeue();
        }
        return null;
    }
    
    /// <summary>
    /// Increments the excess signals count.
    /// </summary>
    public void IncrementExcessSignals()
    {
        var current = ExcessSignals.Value;
        SetExcessSignals(SmallInteger.From(current + 1));
    }
    
    /// <summary>
    /// Decrements the excess signals count.
    /// </summary>
    public bool DecrementExcessSignals()
    {
        var current = ExcessSignals.Value;
        if (current > 0)
        {
            SetExcessSignals(SmallInteger.From(current - 1));
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Performs a wait operation on this semaphore.
    /// </summary>
    /// <returns>True if the process should continue, false if it should be suspended.</returns>
    public bool Wait(SProcess process)
    {
        if (DecrementExcessSignals())
        {
            // There was an excess signal, process can continue
            return true;
        }
        
        // No excess signals, add process to waiting list
        AddWaitingProcess(process);
        return false;
    }
    
    /// <summary>
    /// Performs a signal operation on this semaphore.
    /// </summary>
    /// <returns>The process to resume, or null if none.</returns>
    public SProcess? Signal()
    {
        var waitingProcess = RemoveWaitingProcess();
        if (waitingProcess != null)
        {
            // Resume the waiting process
            return waitingProcess;
        }
        
        // No waiting processes, increment excess signals
        IncrementExcessSignals();
        return null;
    }
    
    /// <summary>
    /// Gets the number of processes waiting on this semaphore.
    /// </summary>
    public int WaitingCount => waitingProcesses.Count;
    
    /// <summary>
    /// Returns a string representation of this semaphore.
    /// </summary>
    public override string ToDisplayString()
    {
        var excess = ExcessSignals.Value;
        var waiting = WaitingCount;
        return $"Semaphore(excess={excess}, waiting={waiting})";
    }
    
    /// <summary>
    /// Creates a new binary semaphore (mutex).
    /// </summary>
    public static SSemaphore ForMutualExclusion()
    {
        var semaphoreClass = SystemDictionary.LookupClass("Semaphore")
            ?? throw new InvalidOperationException("Semaphore class not found");
            
        return new SSemaphore(semaphoreClass, 1);
    }
    
    /// <summary>
    /// Creates a new semaphore with no initial signals.
    /// </summary>
    public static SSemaphore New()
    {
        var semaphoreClass = SystemDictionary.LookupClass("Semaphore")
            ?? throw new InvalidOperationException("Semaphore class not found");
            
        return new SSemaphore(semaphoreClass, 0);
    }
}