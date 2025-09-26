using System.Collections.Concurrent;
using Core.Abstractions.Memory;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Memory;

/// <summary>
/// Implements the Smalltalk-80 object memory with allocation, field access, and mark-and-sweep garbage collection.
/// Uses a pure mark-and-sweep algorithm without reference counting.
/// </summary>
public sealed class ObjectMemory : IObjectMemory
{
    private readonly ILogger<ObjectMemory> _logger;
    private readonly List<ObjectEntry?> _objectTable = [];
    private readonly Stack<uint> _freeList = new();
    
    // Memory segments
    private uint[] _heap;
    private uint _heapPointer;
    private const uint InitialHeapSize = 65536; // 64K words initially
    private const uint MaxHeapSize = 16777216;  // 16M words maximum

    // No special indices - all objects are treated equally
    
    /// <summary>
    /// Controls whether [GC] logging messages are enabled.
    /// </summary>
    public bool EnableGCLogging { get; set; }
    
    /// <summary>
    /// Logs a message with the [GC] prefix if the flag is enabled.
    /// </summary>
    private void LogGC(LogLevel level, string message, params object[] args)
    {
        if (EnableGCLogging)
        {
            var formattedMessage = "[GC] " + message;
            _logger.Log(level, formattedMessage, args);
        }
    }
    
    /// <summary>
    /// Initializes a new instance of the ObjectMemory class.
    /// </summary>
    public ObjectMemory() : this(NullLogger<ObjectMemory>.Instance)
    {
    }
    
    public ObjectMemory(ILogger<ObjectMemory> logger)
    {
        _logger = logger;
        _heap = new uint[InitialHeapSize];
        _heapPointer = 0;
        EnableGCLogging = false; // Default to disabled
    }
    
    public ObjectMemory(ILogger<ObjectMemory> logger, object runtimeOptions) : this(logger)
    {
        // Use reflection to check for EnableGCLogging property
        if (runtimeOptions != null)
        {
            var gcLoggingProp = runtimeOptions.GetType().GetProperty("EnableGCLogging");
            if (gcLoggingProp != null && gcLoggingProp.PropertyType == typeof(bool))
            {
                var value = gcLoggingProp.GetValue(runtimeOptions);
                if (value is bool boolValue)
                {
                    EnableGCLogging = boolValue;
                }
            }
        }
    }

    /// <inheritdoc/>
    public OOP AllocateObject(OOP classOop, int size)
    {
        if (size < 0 || size > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Object size must be between 0 and 65535");
        }

        // Allocate object table entry
        var objectIndex = AllocateObjectTableEntry();
        
        // Allocate heap space
        var heapIndex = AllocateHeapSpace((uint)(size + 1)); // +1 for header
        
        // Create and store header
        var header = new ObjectHeader(classOop, (ushort)size, isPointers: true);
        _heap[heapIndex] = header.HeaderWord;
        
        // Initialize fields to nil
        for (var i = 1; i <= size; i++)
        {
            _heap[heapIndex + i] = _nilOop.Value;
        }
        
        // Update object table entry
        _objectTable[(int)objectIndex] = new ObjectEntry
        {
            Location = heapIndex,
            Size = (uint)(size + 1)
        };
        
        return OOP.FromPointer(objectIndex);
    }

    /// <inheritdoc/>
    public OOP AllocateByteObject(OOP classOop, int byteSize)
    {
        if (byteSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size cannot be negative");
        }

        // Calculate word size (round up)
        var wordSize = (byteSize + 3) / 4;
        
        // Allocate object table entry
        var objectIndex = AllocateObjectTableEntry();
        
        // Allocate heap space
        var heapIndex = AllocateHeapSpace((uint)(wordSize + 1)); // +1 for header
        
        // Create and store header
        var header = new ObjectHeader(classOop, (ushort)wordSize, isPointers: false, isWords: true);
        _heap[heapIndex] = header.HeaderWord;
        
        // Clear data area
        for (var i = 1; i <= wordSize; i++)
        {
            _heap[heapIndex + i] = 0;
        }
        
        // Update object table entry
        _objectTable[(int)objectIndex] = new ObjectEntry
        {
            Location = heapIndex,
            Size = (uint)(wordSize + 1),
            ByteSize = (uint)byteSize
        };
        
        return OOP.FromPointer(objectIndex);
    }

    /// <inheritdoc/>
    public OOP FetchPointer(OOP oop, int index)
    {
        if (oop.IsSmallInteger)
        {
            throw new InvalidOperationException("Cannot fetch pointer from SmallInteger");
        }
        
        if (IsNil(oop))
        {
            throw new InvalidOperationException("Cannot fetch pointer from nil");
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        
        if (!header.HasPointers)
        {
            throw new InvalidOperationException("Object does not contain pointers");
        }
        
        if (index < 0 || index >= header.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for object of size {header.Size}");
        }
        
        return new OOP(_heap[entry.Location + 1 + (uint)index]);
    }

    /// <inheritdoc/>
    public void StorePointer(OOP oop, int index, OOP value)
    {
        if (oop.IsSmallInteger)
        {
            throw new InvalidOperationException("Cannot store pointer in SmallInteger");
        }
        
        if (IsNil(oop))
        {
            throw new InvalidOperationException("Cannot store pointer in nil");
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        
        if (!header.HasPointers)
        {
            throw new InvalidOperationException("Object does not contain pointers");
        }
        
        if (index < 0 || index >= header.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for object of size {header.Size}");
        }
        
        _heap[entry.Location + 1 + (uint)index] = value.Value;
    }

    /// <inheritdoc/>
    public byte FetchByte(OOP oop, int index)
    {
        if (oop.IsSmallInteger)
        {
            throw new InvalidOperationException("Cannot fetch byte from SmallInteger");
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        
        if (!header.HasWords)
        {
            throw new InvalidOperationException("Object does not contain bytes");
        }
        
        if (index < 0 || index >= entry.ByteSize)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for byte object of size {entry.ByteSize}");
        }
        
        // Calculate word and byte offset
        var wordIndex = index / 4;
        var byteOffset = index % 4;
        
        var word = _heap[entry.Location + 1 + (uint)wordIndex];
        return (byte)((word >> (byteOffset * 8)) & 0xFF);
    }

    /// <inheritdoc/>
    public void StoreByte(OOP oop, int index, byte value)
    {
        if (oop.IsSmallInteger)
        {
            throw new InvalidOperationException("Cannot store byte in SmallInteger");
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        
        if (!header.HasWords)
        {
            throw new InvalidOperationException("Object does not contain bytes");
        }
        
        if (index < 0 || index >= entry.ByteSize)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for byte object of size {entry.ByteSize}");
        }
        
        // Calculate word and byte offset
        var wordIndex = index / 4;
        var byteOffset = index % 4;
        
        var wordAddr = entry.Location + 1 + (uint)wordIndex;
        var word = _heap[wordAddr];
        
        // Clear the byte and set new value
        var mask = ~(0xFFu << (byteOffset * 8));
        word = (word & mask) | ((uint)value << (byteOffset * 8));
        _heap[wordAddr] = word;
    }

    /// <inheritdoc/>
    public OOP FetchClassOf(OOP oop)
    {
        if (oop.IsSmallInteger)
        {
            // Return SmallInteger class (would be set during initialization)
            return SmallIntegerClass;
        }
        
        if (IsNil(oop))
        {
            // Return UndefinedObject class
            return UndefinedObjectClass;
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        return header.ClassOop;
    }

    /// <inheritdoc/>
    public void SetClassOf(OOP oop, OOP classOop)
    {
        if (oop.IsSmallInteger)
        {
            throw new InvalidOperationException("Cannot set class of SmallInteger");
        }
        
        // No special handling for nil - it's a regular object
        
        var entry = GetObjectEntry(oop);
        var currentHeader = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        
        // Create new header with updated class but preserving size and flags
        var newHeader = new ObjectHeader(
            classOop, 
            currentHeader.Size,
            currentHeader.HasPointers,
            currentHeader.HasWords,
            currentHeader.IsFree
        );
        
        _heap[entry.Location] = newHeader.HeaderWord;
    }

    /// <inheritdoc/>
    public int FetchWordLengthOf(OOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return 0; // SmallIntegers have no indexed fields
        }
        
        if (IsNil(oop))
        {
            return 0;
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        return header.Size;
    }

    /// <inheritdoc/>
    public int FetchByteLengthOf(OOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return 0;
        }
        
        var entry = GetObjectEntry(oop);
        return (int)entry.ByteSize;
    }

    /// <inheritdoc/>
    public void CollectGarbage()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Statistics tracking
        int objectsVisited = 0;
        int objectsMarked = 0;
        int objectsCollected = 0;
        long bytesFreed = 0;
        int totalObjectsBefore = _objectTable.Count(entry => entry.HasValue);
        long totalBytesBefore = CalculateTotalMemoryUsed();
        
        LogGC(LogLevel.Information, "============ Starting Garbage Collection ============");
        LogGC(LogLevel.Information, "Total objects before GC: {Count:N0}", totalObjectsBefore);
        LogGC(LogLevel.Information, "Memory used: {Bytes:N0} bytes", totalBytesBefore);
        LogGC(LogLevel.Information, "Heap usage: {Used:N0} bytes of {Total:N0} bytes", _heapPointer * 4, _heap.Length * 4);
        
        // MARK PHASE - Mark all reachable objects starting from roots
        var markStartTime = stopwatch.ElapsedMilliseconds;
        
        // Mark phase: Start from the Special Objects Array (the ONLY root)
        var visitedIndices = new HashSet<uint>();
        var workStack = new Stack<OOP>();
        
        // Add the Special Objects Array as the sole root
        if (!_specialObjectsArray.IsSmallInteger && _specialObjectsArray.Value != 0)
        {
            workStack.Push(_specialObjectsArray);
        }
        else
        {
            LogGC(LogLevel.Warning, "No Special Objects Array registered - nothing to mark!");
        }
        
        // Perform depth-first marking
        while (workStack.Count > 0)
        {
            var oop = workStack.Pop();
            
            // Skip immediates (SmallIntegers only - nil is a real object that needs marking!)
            if (oop.IsSmallInteger)
                continue;
                
            var index = oop.ToPointer();
            
            // Skip if already visited (this prevents counting the same object multiple times)
            if (visitedIndices.Contains(index))
                continue;
                
            visitedIndices.Add(index);
            objectsVisited++;  // Count only when we actually visit a new object
            
            // Get the object entry
            if (index >= _objectTable.Count || !_objectTable[(int)index].HasValue)
            {
                continue;
            }
                
            var entry = _objectTable[(int)index]!.Value;
            
            // No special handling for any objects - all are treated equally
            
            // Mark the object by setting the mark bit in its header
            var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
            
            // Objects should not already be marked if we're visiting them for the first time
            if (header.IsMarked)
            {
                LogGC(LogLevel.Warning, "Object at index {Index} already marked but not in visited set!", index);
                continue;
            }
            
            _heap[entry.Location] = header.WithMark().HeaderWord;
            objectsMarked++;
            
            // Also mark the object's class
            if (!header.ClassOop.IsSmallInteger)
            {
                var classIndex = header.ClassOop.ToPointer();
                if (!visitedIndices.Contains(classIndex))  // Only add if not already visited
                {
                    workStack.Push(header.ClassOop);
                }
            }
            
            // If the object contains pointers, add all referenced objects to work stack
            if (header.HasPointers)
            {
                for (var i = 0; i < header.Size; i++)
                {
                    var fieldOop = new OOP(_heap[entry.Location + 1 + (uint)i]);
                    
                    if (!fieldOop.IsSmallInteger)
                    {
                        var fieldIndex = fieldOop.ToPointer();
                        if (!visitedIndices.Contains(fieldIndex))  // Only add if not already visited
                        {
                            workStack.Push(fieldOop);
                        }
                    }
                }
            }
        }
        
        var markEndTime = stopwatch.ElapsedMilliseconds;
        var markTime = markEndTime - markStartTime;
        
        // SWEEP PHASE - Free all unmarked objects and clear marks on surviving objects
        var sweepStartTime = stopwatch.ElapsedMilliseconds;
        
        for (var i = 0; i < _objectTable.Count; i++)
        {
            if (!_objectTable[i].HasValue)
                continue;
                
            var entry = _objectTable[i]!.Value;
            
            // No special handling - all objects treated equally
            
            var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
            
            if (!header.IsMarked)
            {
                // This object is unreachable - free it
                bytesFreed += entry.Size * 4; // Size is in words, convert to bytes
                
                // Clear the heap memory (optional, helps with debugging)
                for (uint j = 0; j < entry.Size; j++)
                {
                    _heap[entry.Location + j] = 0xDEADBEEF; // Dead object marker
                }
                
                // Return the object table entry to the free list
                _objectTable[i] = null;
                _freeList.Push((uint)i);
                objectsCollected++;
            }
            else
            {
                // Object survives - clear the mark bit for the next GC
                _heap[entry.Location] = header.WithoutMark().HeaderWord;
            }
        }
        
        var sweepEndTime = stopwatch.ElapsedMilliseconds;
        var sweepTime = sweepEndTime - sweepStartTime;
        
        stopwatch.Stop();
        
        // Update statistics
        _lastObjectsCollected = objectsCollected;
        _lastBytesFreed = (int)bytesFreed;
        var totalObjectsAfter = _objectTable.Count(entry => entry.HasValue);
        long totalBytesAfter = CalculateTotalMemoryUsed();
        
        // Display comprehensive statistics with sanity checks
        LogGC(LogLevel.Information, "============ Garbage Collection Complete ============");
        LogGC(LogLevel.Information, "BEFORE: {ObjBefore} objects, {BytesBefore:N0} bytes", totalObjectsBefore, totalBytesBefore);
        LogGC(LogLevel.Information, "MARKED: {Marked} objects (visited {Visited} unique objects)", objectsMarked, objectsVisited);
        LogGC(LogLevel.Information, "COLLECTED: {Collected} objects, {Freed:N0} bytes freed", objectsCollected, bytesFreed);
        LogGC(LogLevel.Information, "AFTER: {ObjAfter} objects, {BytesAfter:N0} bytes", totalObjectsAfter, totalBytesAfter);
        LogGC(LogLevel.Information, "ROOT: Special Objects Array = {SOA}", _specialObjectsArray);
        LogGC(LogLevel.Information, "TIME: {TotalMs}ms (mark: {MarkMs}ms, sweep: {SweepMs}ms)", stopwatch.ElapsedMilliseconds, markTime, sweepTime);
        
        // Sanity checks to detect bugs
        if (objectsVisited != objectsMarked)
        {
            LogGC(LogLevel.Warning, "Visited {Visited} but marked {Marked} - this indicates a bug!", objectsVisited, objectsMarked);
        }
        
        var unaccounted = totalObjectsBefore - (objectsMarked + objectsCollected);
        if (unaccounted != 0)
        {
            LogGC(LogLevel.Warning, "{Unaccounted} objects unaccounted for!", unaccounted);
            LogGC(LogLevel.Warning, "{Marked} marked + {Collected} collected != {Total} total", objectsMarked, objectsCollected, totalObjectsBefore);
            
            // Let's find which objects weren't marked
            LogGC(LogLevel.Debug, "Examining first 10 objects:");
            for (var i = 0; i < Math.Min(_objectTable.Count, 10); i++)
            {
                if (_objectTable[i].HasValue)
                {
                    var entry = _objectTable[i]!.Value;
                    
                    // No special handling for display
                        
                    var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
                    var oop = OOP.FromPointer((uint)i);
                    LogGC(LogLevel.Debug, "  Index {Index} (OOP {Oop}): Class={Class}, Size={Size}, Marked={Marked}", 
                        i, oop, header.ClassOop, header.Size, header.IsMarked);
                }
            }
        }
        LogGC(LogLevel.Information, "======================================================");
    }

    /// <summary>
    /// Calculates the total memory used by all allocated objects.
    /// </summary>
    /// <returns>Total memory in bytes.</returns>
    private long CalculateTotalMemoryUsed()
    {
        long totalBytes = 0;
        for (var i = 0; i < _objectTable.Count; i++)
        {
            if (_objectTable[i].HasValue)
            {
                var entry = _objectTable[i]!.Value;
                totalBytes += entry.Size * 4; // Size is in words, convert to bytes
            }
        }
        return totalBytes;
    }

    private void InitializeSpecialObjects()
    {
        // No special initialization needed - all objects are created normally
    }

    private uint AllocateObjectTableEntry()
    {
        if (_freeList.Count > 0)
        {
            return _freeList.Pop();
        }
        
        var index = (uint)_objectTable.Count;
        _objectTable.Add(new ObjectEntry());
        return index;
    }

    private uint AllocateHeapSpace(uint size)
    {
        // Check if we need to grow the heap
        if (_heapPointer + size > _heap.Length)
        {
            GrowHeap(size);
        }
        
        var location = _heapPointer;
        _heapPointer += size;
        return location;
    }

    private void GrowHeap(uint minSize)
    {
        var newSize = Math.Max(_heap.Length * 2, _heap.Length + (int)minSize);
        if (newSize > MaxHeapSize)
        {
            throw new OutOfMemoryException("Heap size exceeded maximum limit");
        }
        
        Array.Resize(ref _heap, newSize);
    }

    private ObjectEntry GetObjectEntry(OOP oop)
    {
        var index = oop.ToPointer();
        if (index >= _objectTable.Count)
        {
            throw new InvalidOperationException($"Invalid OOP: {oop}");
        }
        
        var entry = _objectTable[(int)index];
        if (!entry.HasValue)
        {
            throw new InvalidOperationException($"Object at index {index} has been deallocated");
        }
        
        return entry.Value;
    }

    /// <summary>
    /// Forces a garbage collection cycle.
    /// Can be called when memory pressure is detected.
    /// </summary>
    public void ForceGarbageCollection()
    {
        LogGC(LogLevel.Information, "Forced garbage collection requested");
        CollectGarbage();
    }
    
    /// <summary>
    /// Sets the Special Objects Array as the sole GC root.
    /// This should be called during system initialization.
    /// </summary>
    /// <param name="specialObjectsArray">The OOP of the special objects array.</param>
    public void SetSpecialObjectsArray(OOP specialObjectsArray)
    {
        _specialObjectsArray = specialObjectsArray;
        LogGC(LogLevel.Information, "Set Special Objects Array as sole GC root: {SpecialObjectsArray}", specialObjectsArray);
    }

    /// <inheritdoc/>
    public bool IsSmallInteger(OOP oop)
    {
        return oop.IsSmallInteger;
    }

    /// <inheritdoc/>
    public int SmallIntegerValue(OOP oop)
    {
        return oop.SmallIntegerValue;
    }

    /// <inheritdoc/>
    public OOP SmallIntegerObjectOf(int value)
    {
        return OOP.FromSmallInteger(value);
    }

    // Well-known classes (would be set during system initialization)
    private OOP SmallIntegerClass { get; set; } = new(6);
    private OOP UndefinedObjectClass { get; set; } = new(8);
    
    // Track the nil object OOP (set during bootstrap)
    private OOP _nilOop = new(0); // Will be set properly during initialization
    
    // Garbage collection tracking
    private OOP _specialObjectsArray = new(0); // The ONLY GC root - set during bootstrap
    private int _lastObjectsCollected;
    private int _lastBytesFreed;

    /// <summary>
    /// Represents an entry in the object table.
    /// Contains location and size information for an allocated object.
    /// Note: No reference counting - we use pure mark-and-sweep GC.
    /// </summary>
    private struct ObjectEntry
    {
        /// <summary>
        /// Location of the object in the heap (index into _heap array).
        /// </summary>
        public uint Location;
        
        /// <summary>
        /// Size of the object in words (including header).
        /// </summary>
        public uint Size;
        
        /// <summary>
        /// For byte objects, the actual byte size (word Size may be larger due to alignment).
        /// </summary>
        public uint ByteSize;
    }
    
    
    /// <inheritdoc/>
    public (int objectsCollected, int bytesFreed) GetLastGCStats()
    {
        return (_lastObjectsCollected, _lastBytesFreed);
    }
    
    /// <summary>
    /// Sets the OOP value that represents nil.
    /// This should be called during bootstrap after nil object is created.
    /// </summary>
    public void SetNilOop(OOP nilOop)
    {
        _nilOop = nilOop;
    }
    
    /// <summary>
    /// Checks if the given OOP is nil.
    /// </summary>
    private bool IsNil(OOP oop)
    {
        return oop.Value == _nilOop.Value;
    }
}