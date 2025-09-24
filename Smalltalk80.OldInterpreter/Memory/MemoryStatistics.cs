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
    /// Number of free objects available for allocation.
    /// </summary>
    public int FreeObjects => TotalCapacity - TotalObjects;

    /// <summary>
    /// Percentage of capacity used.
    /// </summary>
    public double UsagePercentage => TotalCapacity > 0 ? (double)TotalObjects / TotalCapacity * 100 : 0;

    public override string ToString() =>
        $"Memory: {TotalObjects}/{TotalCapacity} objects ({UsagePercentage:F1}%), NextIndex={NextIndex}";
}