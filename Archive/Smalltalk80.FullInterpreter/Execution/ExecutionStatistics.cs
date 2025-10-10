namespace Smalltalk80.Interpreter.Execution;

using System.Diagnostics;

/// <summary>
/// Tracks execution statistics for the Smalltalk-80 Virtual Machine.
/// </summary>
public sealed class ExecutionStatistics
{
    private readonly Stopwatch executionTimer = new();
    private long totalMessageSends;
    private long totalPrimitiveCalls;
    private long totalBlockActivations;
    private long totalMethodActivations;
    private long totalBytecodesExecuted;
    private int currentStackDepth;
    private int maxStackDepth;
    
    /// <summary>
    /// Gets the total number of message sends performed.
    /// </summary>
    public long TotalMessageSends => totalMessageSends;
    
    /// <summary>
    /// Gets the total number of primitive calls.
    /// </summary>
    public long TotalPrimitiveCalls => totalPrimitiveCalls;
    
    /// <summary>
    /// Gets the total number of block activations.
    /// </summary>
    public long TotalBlockActivations => totalBlockActivations;
    
    /// <summary>
    /// Gets the total number of method activations.
    /// </summary>
    public long TotalMethodActivations => totalMethodActivations;
    
    /// <summary>
    /// Gets the total number of bytecodes executed.
    /// </summary>
    public long TotalBytecodesExecuted => totalBytecodesExecuted;
    
    /// <summary>
    /// Gets the total execution time in milliseconds.
    /// </summary>
    public long TotalExecutionTimeMs => executionTimer.ElapsedMilliseconds;
    
    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    public int CurrentStackDepth => currentStackDepth;
    
    /// <summary>
    /// Gets the maximum stack depth reached.
    /// </summary>
    public int MaxStackDepth => maxStackDepth;
    
    /// <summary>
    /// Records a message send.
    /// </summary>
    public void RecordMessageSend()
    {
        Interlocked.Increment(ref totalMessageSends);
    }
    
    /// <summary>
    /// Records a primitive call.
    /// </summary>
    public void RecordPrimitiveCall()
    {
        Interlocked.Increment(ref totalPrimitiveCalls);
    }
    
    /// <summary>
    /// Records a block activation.
    /// </summary>
    public void RecordBlockActivation()
    {
        Interlocked.Increment(ref totalBlockActivations);
    }
    
    /// <summary>
    /// Records a method activation.
    /// </summary>
    public void RecordMethodActivation()
    {
        Interlocked.Increment(ref totalMethodActivations);
    }
    
    /// <summary>
    /// Records bytecode execution.
    /// </summary>
    /// <param name="count">The number of bytecodes executed.</param>
    public void RecordBytecodesExecuted(int count = 1)
    {
        Interlocked.Add(ref totalBytecodesExecuted, count);
    }
    
    /// <summary>
    /// Updates the stack depth.
    /// </summary>
    /// <param name="delta">The change in stack depth (positive for push, negative for pop).</param>
    public void UpdateStackDepth(int delta)
    {
        var newDepth = Interlocked.Add(ref currentStackDepth, delta);
        
        // Update max depth if needed
        var currentMax = maxStackDepth;
        while (newDepth > currentMax)
        {
            Interlocked.CompareExchange(ref maxStackDepth, newDepth, currentMax);
            currentMax = maxStackDepth;
        }
    }
    
    /// <summary>
    /// Starts the execution timer.
    /// </summary>
    public void StartTimer()
    {
        executionTimer.Start();
    }
    
    /// <summary>
    /// Stops the execution timer.
    /// </summary>
    public void StopTimer()
    {
        executionTimer.Stop();
    }
    
    /// <summary>
    /// Resets all statistics to zero.
    /// </summary>
    public void Reset()
    {
        totalMessageSends = 0;
        totalPrimitiveCalls = 0;
        totalBlockActivations = 0;
        totalMethodActivations = 0;
        totalBytecodesExecuted = 0;
        currentStackDepth = 0;
        maxStackDepth = 0;
        executionTimer.Reset();
    }
    
    /// <summary>
    /// Gets a snapshot of the current statistics.
    /// </summary>
    /// <returns>A snapshot of the current execution statistics.</returns>
    public ExecutionStatisticsSnapshot GetSnapshot() => new(
        TotalMessageSends,
        TotalPrimitiveCalls,
        TotalBlockActivations,
        TotalMethodActivations,
        TotalBytecodesExecuted,
        TotalExecutionTimeMs,
        CurrentStackDepth,
        MaxStackDepth);
    
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString()
    {
        var snapshot = GetSnapshot();
        return $"""
            Execution Statistics:
              Message Sends:      {snapshot.TotalMessageSends:N0}
              Primitive Calls:    {snapshot.TotalPrimitiveCalls:N0}
              Method Activations: {snapshot.TotalMethodActivations:N0}
              Block Activations:  {snapshot.TotalBlockActivations:N0}
              Bytecodes Executed: {snapshot.TotalBytecodesExecuted:N0}
              Execution Time:     {snapshot.TotalExecutionTimeMs:N0} ms
              Current Stack:      {snapshot.CurrentStackDepth}
              Max Stack Depth:    {snapshot.MaxStackDepth}
              Throughput:         {snapshot.MessageSendsPerSecond:N0} msg/sec
            """;
    }
}

/// <summary>
/// Immutable snapshot of execution statistics.
/// </summary>
public readonly record struct ExecutionStatisticsSnapshot(
    long TotalMessageSends,
    long TotalPrimitiveCalls,
    long TotalBlockActivations,
    long TotalMethodActivations,
    long TotalBytecodesExecuted,
    long TotalExecutionTimeMs,
    int CurrentStackDepth,
    int MaxStackDepth)
{
    /// <summary>
    /// Gets the average message sends per second.
    /// </summary>
    public double MessageSendsPerSecond =>
        TotalExecutionTimeMs > 0 ? TotalMessageSends * 1000.0 / TotalExecutionTimeMs : 0;
    
    /// <summary>
    /// Gets the average bytecodes executed per second.
    /// </summary>
    public double BytecodesPerSecond =>
        TotalExecutionTimeMs > 0 ? TotalBytecodesExecuted * 1000.0 / TotalExecutionTimeMs : 0;
}