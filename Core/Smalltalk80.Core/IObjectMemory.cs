namespace Smalltalk80.Core;

/// <summary>
/// Manages the allocation, storage, and garbage collection of Smalltalk objects in memory.
/// This interface abstracts the object memory management from the VM implementation.
/// </summary>
public interface IObjectMemory
{
    /// <summary>
    /// Allocates a new object with the specified class and field count.
    /// </summary>
    /// <param name="classObject">The class of the object to create.</param>
    /// <param name="fieldCount">The number of instance variable fields.</param>
    /// <returns>The newly allocated object.</returns>
    ISmalltalkObject AllocateObject(ISmalltalkClass classObject, int fieldCount);

    /// <summary>
    /// Allocates a new indexed object (like Array) with the specified size.
    /// </summary>
    /// <param name="classObject">The class of the indexed object.</param>
    /// <param name="size">The number of indexed slots.</param>
    /// <returns>The newly allocated indexed object.</returns>
    ISmalltalkObject AllocateIndexedObject(ISmalltalkClass classObject, int size);

    /// <summary>
    /// Allocates a new byte object (like String or Symbol) with the specified size.
    /// </summary>
    /// <param name="classObject">The class of the byte object.</param>
    /// <param name="size">The number of bytes.</param>
    /// <returns>The newly allocated byte object.</returns>
    ISmalltalkObject AllocateByteObject(ISmalltalkClass classObject, int size);

    /// <summary>
    /// Gets an object by its object identifier (OOP - Object-Oriented Pointer).
    /// </summary>
    /// <param name="oop">The object identifier.</param>
    /// <returns>The object, or null if not found.</returns>
    ISmalltalkObject? GetObject(int oop);

    /// <summary>
    /// Registers an object and returns its OOP.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    /// <returns>The object's OOP.</returns>
    int RegisterObject(ISmalltalkObject obj);

    /// <summary>
    /// Performs garbage collection to reclaim unused memory.
    /// </summary>
    /// <returns>The number of objects collected.</returns>
    int CollectGarbage();

    /// <summary>
    /// Gets the current memory statistics.
    /// </summary>
    MemoryStatistics GetStatistics();

    /// <summary>
    /// Creates a memory snapshot for debugging or persistence.
    /// </summary>
    /// <returns>A snapshot of the current object memory state.</returns>
    IMemorySnapshot CreateSnapshot();

    /// <summary>
    /// Restores object memory from a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore from.</param>
    void RestoreSnapshot(IMemorySnapshot snapshot);
}

/// <summary>
/// Represents memory usage statistics.
/// </summary>
public record MemoryStatistics
{
    /// <summary>
    /// Gets the total number of allocated objects.
    /// </summary>
    public int TotalObjects { get; init; }

    /// <summary>
    /// Gets the total memory used in bytes.
    /// </summary>
    public long UsedMemory { get; init; }

    /// <summary>
    /// Gets the total available memory in bytes.
    /// </summary>
    public long AvailableMemory { get; init; }

    /// <summary>
    /// Gets the number of garbage collections performed.
    /// </summary>
    public int GarbageCollectionCount { get; init; }

    /// <summary>
    /// Gets the breakdown of objects by class.
    /// </summary>
    public IReadOnlyDictionary<string, int> ObjectsByClass { get; init; } = new Dictionary<string, int>();
}

/// <summary>
/// Represents a snapshot of object memory state.
/// </summary>
public interface IMemorySnapshot
{
    /// <summary>
    /// Gets the timestamp when the snapshot was created.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the serialized object data.
    /// </summary>
    byte[] Data { get; }

    /// <summary>
    /// Gets metadata about the snapshot.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}