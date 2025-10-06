namespace Smalltalk80.Interpreter.Memory;

/// <summary>
/// Statistics about the object table.
/// </summary>
public record ObjectTableStatistics(
    int Capacity,
    int LiveObjects,
    int FreeListSize,
    int NextIndex,
    double Utilization,
    double FragmentationRatio
);
