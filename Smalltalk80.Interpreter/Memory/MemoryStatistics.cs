namespace Smalltalk80.Interpreter.Memory;

/// <summary>
/// Memory usage statistics.
/// </summary>
public sealed record MemoryStatistics
{
    /// <summary>
    /// Total number of allocated objects.
    /// </summary>
    public required int TotalObjects { get; init; }

    /// <summary>
    /// Total capacity of the object table.
    /// </summary>
    public required int TotalCapacity { get; init; }

    /// <summary>
    /// Next object index to be allocated.
    /// </summary>
    public required uint NextIndex { get; init; }
    
    /// <summary>
    /// Total number of allocations made since startup.
    /// </summary>
    public long TotalAllocations { get; init; }
    
    /// <summary>
    /// Total bytes allocated since startup.
    /// </summary>
    public long TotalAllocatedBytes { get; init; }
    
    /// <summary>
    /// Number of explicit GC roots.
    /// </summary>
    public int GCRootsCount { get; init; }

    /// <summary>
    /// Number of free objects available for allocation.
    /// </summary>
    public int FreeObjects => TotalCapacity - TotalObjects;

    /// <summary>
    /// Percentage of capacity used.
    /// </summary>
    public double UsagePercentage => TotalCapacity > 0 ? (double)TotalObjects / TotalCapacity * 100 : 0;
    
    /// <summary>
    /// Average object size in bytes.
    /// </summary>
    public double AverageObjectSize => TotalAllocations > 0 ? (double)TotalAllocatedBytes / TotalAllocations : 0;

    public override string ToString() =>
        $"Memory: {TotalObjects:N0}/{TotalCapacity:N0} objects ({UsagePercentage:F1}%), " +
        $"Total Allocations: {TotalAllocations:N0}, " +
        $"Total Bytes: {TotalAllocatedBytes:N0}, " +
        $"Avg Size: {AverageObjectSize:F1} bytes, " +
        $"GC Roots: {GCRootsCount}";
}