namespace Smalltalk80.Interpreter.Core.Memory;

using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Segmented Object Table managing Smalltalk object pointers with O(1) allocation.
/// Uses a linked list of fixed-size segments to avoid reallocation and copying during growth.
///
/// Design:
/// - Each segment contains 4096 OOP entries (fixed size)
/// - Segments are allocated on demand and never moved
/// - OOP = absolute index shifted left 2 bits (4-byte alignment, LSB=0 for object pointers)
/// - Free list (Stack of OOPs) provides O(1) allocation of recycled slots
/// - No copying on growth - just allocate new segment and link it
/// - GC mark bit stored in ObjectData.Marked field
///
/// OOP Calculation:
/// - segment = oop_index / SEGMENT_SIZE
/// - offset = oop_index % SEGMENT_SIZE
/// </summary>
public sealed class ObjectTable
{
    // Constants
    private const int SEGMENT_SIZE = 4096;  // 4K entries per segment
    private const int INITIAL_SEGMENTS = 4; // Start with 16K entries (4 * 4096)

    // Segmented storage
    private readonly List<ObjectTableEntry[]> _segments;
    private int _nextIndex;  // Next index to allocate from (if free list is empty)
    private readonly Stack<int> _freeList;
    private readonly object _tableLock = new();

    // Statistics
    private long _totalAllocations;
    private long _totalFrees;
    private int _segmentCount;

    /// <summary>
    /// Entry in the object table.
    /// </summary>
    internal sealed class ObjectTableEntry
    {
        public ObjectData? Data { get; set; }
        public bool IsFree { get; set; }

        public ObjectTableEntry()
        {
            IsFree = true;
            Data = null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectTable"/> class.
    /// </summary>
    public ObjectTable()
    {
        _segments = new List<ObjectTableEntry[]>(INITIAL_SEGMENTS);

        // Allocate initial segments
        for (int i = 0; i < INITIAL_SEGMENTS; i++)
        {
            AllocateSegment();
        }

        // Start at index 1 to avoid OOP 0, which is reserved for nil
        _nextIndex = 1;
        _freeList = new Stack<int>(1024);
        _totalAllocations = 0;
        _totalFrees = 0;
        _segmentCount = INITIAL_SEGMENTS;
    }

    /// <summary>
    /// Allocates a new segment of 4096 entries.
    /// </summary>
    private void AllocateSegment()
    {
        var segment = new ObjectTableEntry[SEGMENT_SIZE];
        for (int i = 0; i < SEGMENT_SIZE; i++)
        {
            segment[i] = new ObjectTableEntry();
        }
        _segments.Add(segment);
        _segmentCount++;
    }

    /// <summary>
    /// Gets the entry at the specified absolute index using segmented lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ObjectTableEntry GetEntry(int index)
    {
        int segmentIndex = index / SEGMENT_SIZE;
        int offset = index % SEGMENT_SIZE;
        return _segments[segmentIndex][offset];
    }

    /// <summary>
    /// Allocates a slot in the object table and returns the index.
    /// O(1) operation using free list or next available index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Allocate(ObjectData data)
    {
        lock (_tableLock)
        {
            int index;

            // Try to reuse a freed slot first (O(1))
            if (_freeList.Count > 0)
            {
                index = _freeList.Pop();
            }
            else
            {
                // Use next available index
                index = _nextIndex++;

                // Check if we need to grow by allocating a new segment
                int requiredSegments = (index / SEGMENT_SIZE) + 1;
                while (_segments.Count < requiredSegments)
                {
                    AllocateSegment();
                }
            }

            // Store the data
            var entry = GetEntry(index);
            entry.Data = data;
            entry.IsFree = false;

            _totalAllocations++;
            return index;
        }
    }

    /// <summary>
    /// Frees a slot in the object table, adding it to the free list.
    /// O(1) operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free(int index)
    {
        lock (_tableLock)
        {
            if (index < 0 || index >= _nextIndex)
                throw new ArgumentOutOfRangeException(nameof(index));

            var entry = GetEntry(index);

            if (entry.IsFree)
                return; // Already freed

            // Clear the entry (allows .NET GC to collect the ObjectData)
            entry.Data = null;
            entry.IsFree = true;

            // Add to free list for reuse
            _freeList.Push(index);

            _totalFrees++;
        }
    }

    /// <summary>
    /// Gets the ObjectData at the specified index.
    /// O(1) lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ObjectData? Get(int index)
    {
        if (index < 0 || index >= _nextIndex)
            return null;

        var entry = GetEntry(index);
        return entry.IsFree ? null : entry.Data;
    }

    /// <summary>
    /// Sets the ObjectData at the specified index.
    /// O(1) operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, ObjectData data)
    {
        lock (_tableLock)
        {
            if (index < 0 || index >= _nextIndex)
                throw new ArgumentOutOfRangeException(nameof(index));

            var entry = GetEntry(index);
            entry.Data = data;
            entry.IsFree = false;
        }
    }

    /// <summary>
    /// Converts an OOP (object-oriented pointer) to a table index.
    /// OOP encoding: index shifted left 1 bit with LSB=0 (31-bit index in 32-bit OOP).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexFromOop(SmalltalkOOP oop)
    {
        return oop.RawValue >> 1;
    }

    /// <summary>
    /// Converts a table index to an OOP (object-oriented pointer).
    /// OOP encoding: index shifted left 1 bit with LSB=0 (31-bit index in 32-bit OOP).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OopFromIndex(int index)
    {
        return index << 1;
    }

    /// <summary>
    /// Mark phase of garbage collection.
    /// Clears all mark bits, then marks reachable objects from roots.
    /// </summary>
    public void MarkPhase(IEnumerable<SmalltalkOOP> roots)
    {
        lock (_tableLock)
        {
            // Phase 1: Clear all mark bits
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (entry.Data != null)
                {
                    entry.Data.Marked = false;
                }
            }

            // Phase 2: Mark from roots using worklist algorithm
            var workList = new Queue<SmalltalkOOP>();
            var marked = new HashSet<SmalltalkOOP>();

            foreach (var rootOOP in roots)
            {
                if (rootOOP != SmalltalkOOP.Null && rootOOP.IsObjectPointer)
                {
                    workList.Enqueue(rootOOP);
                }
            }

            int class9count = 0;
            int class7count = 0;
            int class9withInstVars = 0;
            int class7withInstVars = 0;

            while (workList.Count > 0)
            {
                var oop = workList.Dequeue();

                if (marked.Contains(oop))
                    continue;

                marked.Add(oop);

                int index = IndexFromOop(oop);
                var data = Get(index);

                if (data == null)
                    continue;

                // Count Class objects (9 inst vars) and Metaclass objects (7 inst vars)
                if (data.InstanceVariables != null)
                {
                    if (data.InstanceVariables.Length == 9)
                    {
                        class9count++;

                        // Count non-nil InstanceVariables[4]
                        var instVarNamesRef = data.InstanceVariables[4];
                        if (instVarNamesRef != SmalltalkOOP.Null && instVarNamesRef.IsObjectPointer)
                        {
                            class9withInstVars++;
                        }
                    }
                    else if (data.InstanceVariables.Length == 7)
                    {
                        class7count++;

                        // Count non-nil InstanceVariables[4]
                        var instVarNamesRef = data.InstanceVariables[4];
                        if (instVarNamesRef != SmalltalkOOP.Null && instVarNamesRef.IsObjectPointer)
                        {
                            class7withInstVars++;
                        }
                    }
                }

                // Mark this object
                data.Marked = true;

                // Trace references
                TraceReferences(data, workList);
            }

        }
    }

    /// <summary>
    /// Traces all object references in the given ObjectData.
    /// </summary>
    private void TraceReferences(ObjectData data, Queue<SmalltalkOOP> workList)
    {
        bool enableDetailedLogging = false; // TODO: Re-enable when RuntimeOptions is added

        // Trace class reference
        if (data.ClassRef != SmalltalkOOP.Null && data.ClassRef.IsObjectPointer)
        {
            workList.Enqueue(data.ClassRef);
        }

        // Trace instance variables
        // NOTE: For CompiledMethod, literals are stored as instance variables, so this traces them!
        if (data.InstanceVariables != null && data.InstanceVariables.Length > 0)
        {
            // Detailed tracing disabled due to errors
            // if (enableDetailedLogging)
            // {
            //     ...
            // }

            foreach (var varRef in data.InstanceVariables)
            {
                if (varRef != SmalltalkOOP.Null && varRef.IsObjectPointer)
                {
                    workList.Enqueue(varRef);
                }
            }
        }

        // Trace indexed data if it contains pointers
        if (data.Format == ObjectFormat.PointerIndexed)
        {
            if (data.IndexedData is SmalltalkOOP[] indexedRefs)
            {
                foreach (var indexedRef in indexedRefs)
                {
                    if (indexedRef != SmalltalkOOP.Null && indexedRef.IsObjectPointer)
                    {
                        workList.Enqueue(indexedRef);
                    }
                }
            }
            else if (enableDetailedLogging)
            {
                var actualType = data.IndexedData?.GetType().Name ?? "null";
                Console.WriteLine($"[GC] WARNING: PointerIndexed object has IndexedData type={actualType} instead of SmalltalkOOP[]");
            }
        }
    }

    /// <summary>
    /// Sweep phase of garbage collection.
    /// Frees all unmarked objects and returns the count of freed objects.
    /// </summary>
    public int SweepPhase()
    {
        lock (_tableLock)
        {
            int freedCount = 0;

            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);

                if (entry.IsFree || entry.Data == null)
                    continue;

                // If not marked, free it
                if (!entry.Data.Marked)
                {
                    Free(i);
                    freedCount++;
                }
            }

            return freedCount;
        }
    }

    /// <summary>
    /// Gets statistics about the object table.
    /// </summary>
    public ObjectTableStatistics GetStatistics()
    {
        lock (_tableLock)
        {
            int liveObjects = 0;
            int freeSlots = _freeList.Count;

            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree)
                    liveObjects++;
            }

            int totalCapacity = _segments.Count * SEGMENT_SIZE;
            double utilization = _nextIndex > 0 ? (double)liveObjects / _nextIndex : 0.0;
            double fragmentation = liveObjects > 0 ? (double)freeSlots / liveObjects : 0.0;

            return new ObjectTableStatistics(
                Capacity: totalCapacity,
                LiveObjects: liveObjects,
                FreeListSize: freeSlots,
                NextIndex: _nextIndex,
                Utilization: utilization,
                FragmentationRatio: fragmentation
            );
        }
    }

    /// <summary>
    /// Gets all live object OOPs (for debugging/testing).
    /// </summary>
    public IEnumerable<nint> GetLiveObjects()
    {
        lock (_tableLock)
        {
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree && entry.Data != null)
                {
                    yield return OopFromIndex(i);
                }
            }
        }
    }

    /// <summary>
    /// Gets count of live objects in the table.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_tableLock)
            {
                int count = 0;
                for (int i = 0; i < _nextIndex; i++)
                {
                    var entry = GetEntry(i);
                    if (!entry.IsFree)
                        count++;
                }
                return count;
            }
        }
    }

    /// <summary>
    /// Gets all values (ObjectData) in the table.
    /// </summary>
    public IEnumerable<ObjectData> Values
    {
        get
        {
            lock (_tableLock)
            {
                for (int i = 0; i < _nextIndex; i++)
                {
                    var entry = GetEntry(i);
                    if (!entry.IsFree && entry.Data != null)
                    {
                        yield return entry.Data!;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enumerates live objects with their index and data.
    /// </summary>
    public IEnumerable<(int index, ObjectData data)> EnumerateLiveObjects()
    {
        lock (_tableLock)
        {
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree && entry.Data != null)
                {
                    yield return (i, entry.Data!);
                }
            }
        }
    }

    /// <summary>
    /// Provides enumeration support for foreach.
    /// </summary>
    public IEnumerator<KeyValuePair<int, ObjectData>> GetEnumerator()
    {
        lock (_tableLock)
        {
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree && entry.Data != null)
                {
                    yield return new KeyValuePair<int, ObjectData>(i, entry.Data!);
                }
            }
        }
    }

    /// <summary>
    /// Indexer to access ObjectData by index.
    /// </summary>
    public ObjectData? this[int index]
    {
        get => Get(index);
        set
        {
            if (value != null)
                Set(index, value);
        }
    }

    /// <summary>
    /// Tries to remove an object at the specified index.
    /// </summary>
    public bool TryRemove(int index, out ObjectData? data)
    {
        lock (_tableLock)
        {
            data = Get(index);
            if (data != null)
            {
                Free(index);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all entries in the table.
    /// </summary>
    public void Clear()
    {
        lock (_tableLock)
        {
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                entry.Data = null;
                entry.IsFree = true;
            }
            _freeList.Clear();
            _nextIndex = 0;
        }
    }

    /// <summary>
    /// Validates consistency of the object table.
    /// </summary>
    public void ValidateConsistency()
    {
        lock (_tableLock)
        {
            for (int i = 0; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree && entry.Data == null)
                {
                    throw new InvalidOperationException($"Entry {i} is marked as used but has null data");
                }
                if (entry.IsFree && entry.Data != null)
                {
                    throw new InvalidOperationException($"Entry {i} is marked as free but has non-null data");
                }
            }
        }
    }

    /// <summary>
    /// Gets information about the segmented structure.
    /// </summary>
    public (int SegmentCount, int SegmentSize, int TotalCapacity) GetSegmentInfo()
    {
        lock (_tableLock)
        {
            return (_segments.Count, SEGMENT_SIZE, _segments.Count * SEGMENT_SIZE);
        }
    }

    /// <summary>
    /// Gets all allocated object indices for GC traversal.
    /// </summary>
    public IEnumerable<int> GetAllocatedIndices()
    {
        lock (_tableLock)
        {
            for (int i = 1; i < _nextIndex; i++)
            {
                var entry = GetEntry(i);
                if (!entry.IsFree && entry.Data != null)
                {
                    yield return i;
                }
            }
        }
    }

    /// <summary>
    /// Gets the total count of allocated objects.
    /// </summary>
    public int GetAllocatedCount()
    {
        lock (_tableLock)
        {
            return GetAllocatedIndices().Count();
        }
    }
}
