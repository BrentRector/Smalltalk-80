using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Generic garbage collector interface with zero semantic knowledge of Smalltalk.
/// Manages opaque OOP roots and performs mark-sweep collection.
///
/// GC Operations:
/// 1. Clear all marks
/// 2. Mark reachable objects (from roots)
/// 3. Find unreachable objects
/// 4. Collect unreachable objects
///
/// GetUnreachableObjects() performs steps 1-3 without collection (for testing).
/// GarbageCollect() performs all 4 steps (full collection).
/// </summary>
public interface IGarbageCollector
{
    // ========================================
    // Root Management
    // ========================================

    /// <summary>
    /// Adds an OOP to the GC roots.
    /// The GC will start its reachability walk from all registered roots.
    /// </summary>
    void AddGCRoot(SmalltalkOOP oop);

    /// <summary>
    /// Removes an OOP from the GC roots.
    /// </summary>
    void RemoveGCRoot(SmalltalkOOP oop);

    /// <summary>
    /// Gets the current GC roots array (defensive copy).
    /// </summary>
    SmalltalkOOP[] GetGCRoots();

    /// <summary>
    /// Clears all GC roots.
    /// Use with caution - without roots, all objects become unreachable.
    /// </summary>
    void ClearGCRoots();

    // ========================================
    // Collection
    // ========================================

    /// <summary>
    /// Performs full mark-and-sweep garbage collection using registered roots.
    /// Steps: Clear marks → Mark reachable → Find unreachable → Collect.
    /// Returns detailed statistics about the collection.
    /// </summary>
    GCStatistics GarbageCollect();

    /// <summary>
    /// Gets current GC statistics without performing collection.
    /// </summary>
    GCStatistics GetStatistics();

    /// <summary>
    /// Gets cumulative statistics accumulated over multiple GC cycles and resets the counters.
    /// Statistics are accumulated safely (capped at max values to prevent overflow).
    /// </summary>
    GCCumulativeStatistics GetAndResetCumulativeStatistics();

    // ========================================
    // Debug and Testing
    // ========================================

    /// <summary>
    /// DEBUG: Performs GC marking phase only and returns unreachable objects without collecting them.
    /// Steps: Clear marks → Mark reachable → Find unreachable (no collection).
    /// Returns array of OOPs that would be collected during a full GC.
    /// </summary>
    SmalltalkOOP[] GetUnreachableObjects();
}
