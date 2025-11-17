using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Comprehensive garbage collection results with timing and object counts.
/// </summary>
public sealed class GCStatistics
{
    /// <summary>Total objects in ObjectTable before collection.</summary>
    public int TotalObjects { get; init; }
    
    /// <summary>Objects marked as reachable during mark phase.</summary>
    public int LiveObjects { get; init; }
    
    /// <summary>Objects collected (unreachable) during sweep phase.</summary>
    public int CollectedObjects { get; init; }
    
    /// <summary>Time spent in mark phase (milliseconds).</summary>
    public double MarkTimeMs { get; init; }
    
    /// <summary>Time spent in sweep phase (milliseconds).</summary>
    public double SweepTimeMs { get; init; }
    
    /// <summary>Total garbage collection time (milliseconds).</summary>
    public double TotalTimeMs { get; init; }
    
    /// <summary>Memory reclaimed estimate (bytes freed by setting pointers to null).</summary>
    public long EstimatedBytesFreed { get; init; }
    
    /// <summary>Breakdown of collected objects by format type.</summary>
    public Dictionary<ObjectFormat, int> CollectedByFormat { get; init; } = new();

    /// <summary>Number of ObjectTable entries freed after collection.</summary>
    public int FreeObjectTableEntries { get; init; }
    
    /// <summary>Detailed info about collected objects (OOP, format, size, className).</summary>
    public List<(int oop, ObjectFormat format, int size, string? className)> CollectedObjectDetails { get; init; } = new();

    /// <summary>Collection efficiency as percentage of total objects collected.</summary>
    public double CollectionEfficiency => TotalObjects > 0 
        ? (double)CollectedObjects / TotalObjects * 100 
        : 0;

    /// <summary>Returns a formatted summary of the GC results.</summary>
    public string GetSummary()
    {
        return $"GC Results: {CollectedObjects}/{TotalObjects} objects collected " +
               $"({CollectionEfficiency:F1}% efficiency) in {TotalTimeMs:F2}ms " +
               $"(Mark: {MarkTimeMs:F2}ms, Sweep: {SweepTimeMs:F2}ms), " +
               $"{EstimatedBytesFreed:N0} bytes freed";
    }

    /// <summary>Returns detailed breakdown by object format.</summary>
    public string GetFormatBreakdown()
    {
        if (CollectedByFormat.Count == 0)
            return "No objects collected";

        var breakdown = CollectedByFormat
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => $"{kvp.Key}: {kvp.Value}")
            .ToArray();

        return $"Collected by format: {string.Join(", ", breakdown)}";
    }
}