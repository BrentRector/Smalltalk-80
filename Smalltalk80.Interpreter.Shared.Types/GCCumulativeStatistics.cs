namespace Smalltalk80.Interpreter.Shared.Types;

/// <summary>
/// Cumulative garbage collection statistics accumulated over multiple GC cycles.
/// Uses long values to handle overflow gracefully (capped at long.MaxValue).
/// </summary>
public sealed class GCCumulativeStatistics
{
    /// <summary>Number of GC cycles performed.</summary>
    public long GCCount { get; init; }

    /// <summary>Total objects collected across all GC cycles.</summary>
    public long TotalCollectedObjects { get; init; }

    /// <summary>Total bytes freed across all GC cycles.</summary>
    public long TotalBytesFreed { get; init; }

    /// <summary>Total time spent in mark phase across all GC cycles (milliseconds).</summary>
    public double TotalMarkTimeMs { get; init; }

    /// <summary>Total time spent in sweep phase across all GC cycles (milliseconds).</summary>
    public double TotalSweepTimeMs { get; init; }

    /// <summary>Total GC time across all GC cycles (milliseconds).</summary>
    public double TotalGCTimeMs { get; init; }

    /// <summary>Total objects examined (before collection) across all GC cycles.</summary>
    public long TotalObjectsExamined { get; init; }

    /// <summary>Average objects collected per GC cycle.</summary>
    public double AverageCollectedPerCycle => GCCount > 0
        ? (double)TotalCollectedObjects / GCCount
        : 0;

    /// <summary>Average GC time per cycle (milliseconds).</summary>
    public double AverageGCTimeMs => GCCount > 0
        ? TotalGCTimeMs / GCCount
        : 0;

    /// <summary>Overall collection efficiency as percentage.</summary>
    public double OverallEfficiency => TotalObjectsExamined > 0
        ? (double)TotalCollectedObjects / TotalObjectsExamined * 100
        : 0;

    /// <summary>Returns a formatted summary of cumulative GC statistics.</summary>
    public string GetSummary()
    {
        return $"Cumulative GC: {GCCount} cycles, {TotalCollectedObjects:N0} objects collected, " +
               $"{TotalBytesFreed:N0} bytes freed, {TotalGCTimeMs:F2}ms total " +
               $"(avg {AverageGCTimeMs:F2}ms/cycle, {AverageCollectedPerCycle:F1} objects/cycle)";
    }
}
