using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Interface for mark-and-sweep garbage collection.
/// Manages GC roots and performs collection cycles.
/// </summary>
public interface IGarbageCollector
{
    /// <summary>
    /// Sets the GC root objects array. The GC will start its reachability walk from these objects.
    /// This replaces any previously set root objects.
    /// </summary>
    void SetGCRoots(SmalltalkOOP[] rootObjects);

    /// <summary>
    /// Adds additional OOPs to the current GC roots array.
    /// Useful for temporarily adding objects during specific operations.
    /// </summary>
    void AddGCRoots(SmalltalkOOP[] additionalRoots);

    /// <summary>
    /// Gets the current GC roots array (defensive copy).
    /// </summary>
    SmalltalkOOP[] GetGCRoots();

    /// <summary>
    /// Marks an object as reachable during GC mark phase.
    /// </summary>
    void MarkObject(SmalltalkOOP objectOOP);

    /// <summary>
    /// DEBUG: Performs GC marking phase only and returns unreachable objects without collecting them.
    /// Returns array of OOPs that would be collected during a full GC.
    /// </summary>
    SmalltalkOOP[] GetUnreachableObjects();

    /// <summary>
    /// Performs mark-and-sweep garbage collection using the configured root objects.
    /// Returns detailed statistics about the collection.
    /// </summary>
    GCStatistics GarbageCollect();

    /// <summary>
    /// Gets current GC statistics without performing collection.
    /// </summary>
    GCStatistics GetGCStatistics();
}
