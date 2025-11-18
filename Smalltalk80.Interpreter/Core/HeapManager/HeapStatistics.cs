using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Current heap statistics (snapshot without collection).
/// </summary>
public sealed class HeapStatistics
{
    /// <summary>Total ObjectTable entries allocated.</summary>
    public int TotalObjectTableEntries { get; init; }

    /// <summary>ObjectTable entries with valid objects.</summary>
    public int ActiveObjects { get; init; }

    /// <summary>ObjectTable entries available for reuse.</summary>
    public int FreeObjectTableEntries { get; init; }
    
    /// <summary>Breakdown of active objects by format type.</summary>
    public Dictionary<ObjectFormat, int> ObjectsByFormat { get; init; } = new();
    
    /// <summary>Total estimated memory used by all objects (bytes).</summary>
    public long EstimatedMemoryUsage { get; init; }
    
    /// <summary>Number of ObjectTable blocks allocated.</summary>
    public int ObjectTableBlocks { get; init; }
    
    /// <summary>Current ObjectTable capacity (entries across all blocks).</summary>
    public int ObjectTableCapacity { get; init; }
    
    /// <summary>ObjectTable utilization percentage.</summary>
    public double ObjectTableUtilization => TotalObjectTableEntries > 0 
        ? (double)ActiveObjects / TotalObjectTableEntries * 100 
        : 0;

    /// <summary>Average object size in bytes.</summary>
    public double AverageObjectSize => ActiveObjects > 0 
        ? (double)EstimatedMemoryUsage / ActiveObjects 
        : 0;

    /// <summary>Memory utilization efficiency (allocated vs capacity).</summary>
    public double MemoryEfficiency => ObjectTableCapacity > 0 
        ? (double)ActiveObjects / ObjectTableCapacity * 100 
        : 0;

    /// <summary>Returns a formatted summary of heap statistics.</summary>
    public string GetSummary()
    {
        return $"Heap Statistics: {ActiveObjects:N0}/{TotalObjectTableEntries:N0} active objects " +
               $"({ObjectTableUtilization:F1}% utilization), " +
               $"{EstimatedMemoryUsage:N0} bytes " +
               $"({AverageObjectSize:F1} bytes/object), " +
               $"{ObjectTableBlocks} blocks, " +
               $"{FreeObjectTableEntries:N0} free entries";
    }

    /// <summary>Returns breakdown of objects by format type.</summary>
    public string GetFormatBreakdown()
    {
        if (ObjectsByFormat.Count == 0)
            return "No active objects";

        var breakdown = ObjectsByFormat
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => $"{kvp.Key}: {kvp.Value:N0}")
            .ToArray();

        return $"Objects by format: {string.Join(", ", breakdown)}";
    }

    /// <summary>Returns memory usage analysis.</summary>
    public string GetMemoryAnalysis()
    {
        return $"Memory Analysis: " +
               $"Total: {EstimatedMemoryUsage:N0} bytes, " +
               $"Average: {AverageObjectSize:F1} bytes/object, " +
               $"Efficiency: {MemoryEfficiency:F1}% " +
               $"({ActiveObjects:N0}/{ObjectTableCapacity:N0} slots used)";
    }
}