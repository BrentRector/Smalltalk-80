using System.Collections.Concurrent;
using Core.Abstractions.Memory;

namespace Core.Memory;

/// <summary>
/// Implements the Smalltalk-80 object memory with allocation, field access, and garbage collection.
/// Follows the Blue Book specification for object memory management.
/// </summary>
public sealed class ObjectMemory : IObjectMemory
{
    private readonly List<ObjectEntry> _objectTable = [];
    private readonly Stack<uint> _freeList = new();
    private readonly ConcurrentDictionary<OOP, WeakReference> _weakReferences = new();
    
    // Memory segments
    private uint[] _heap;
    private uint _heapPointer;
    private const uint InitialHeapSize = 65536; // 64K words initially
    private const uint MaxHeapSize = 16777216;  // 16M words maximum

    // Special object indices
    private const uint NilIndex = 0;
    private const uint TrueIndex = 1;
    private const uint FalseIndex = 2;
    
    /// <summary>
    /// Initializes a new instance of the ObjectMemory class.
    /// </summary>
    public ObjectMemory()
    {
        _heap = new uint[InitialHeapSize];
        _heapPointer = 0;
        
        InitializeSpecialObjects();
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
            _heap[heapIndex + i] = OOP.Nil.Value;
        }
        
        // Update object table entry
        _objectTable[(int)objectIndex] = new ObjectEntry
        {
            Location = heapIndex,
            Size = (uint)(size + 1),
            ReferenceCount = 1
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
            ReferenceCount = 1,
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
        
        if (oop.IsNil)
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
        
        if (oop.IsNil)
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
        
        if (oop.IsNil)
        {
            // Return UndefinedObject class
            return UndefinedObjectClass;
        }
        
        var entry = GetObjectEntry(oop);
        var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
        return header.ClassOop;
    }

    /// <inheritdoc/>
    public int FetchWordLengthOf(OOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return 0; // SmallIntegers have no indexed fields
        }
        
        if (oop.IsNil)
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
        // Simplified mark-and-sweep garbage collection
        MarkPhase();
        SweepPhase();
    }

    /// <inheritdoc/>
    public void IncrementReference(OOP oop)
    {
        if (oop.IsSmallInteger || oop.IsNil)
        {
            return; // No reference counting for immediates
        }
        
        var index = oop.ToPointer();
        if (index < _objectTable.Count)
        {
            var entry = _objectTable[(int)index];
            entry.ReferenceCount++;
            _objectTable[(int)index] = entry;
        }
    }

    /// <inheritdoc/>
    public void DecrementReference(OOP oop)
    {
        if (oop.IsSmallInteger || oop.IsNil)
        {
            return; // No reference counting for immediates
        }
        
        var index = oop.ToPointer();
        if (index < _objectTable.Count)
        {
            var entry = _objectTable[(int)index];
            if (entry.ReferenceCount > 0)
            {
                entry.ReferenceCount--;
                _objectTable[(int)index] = entry;
            }
        }
    }

    private void InitializeSpecialObjects()
    {
        // Pre-allocate special object entries
        _objectTable.Add(new ObjectEntry { Location = 0, Size = 0 }); // nil
        _objectTable.Add(new ObjectEntry { Location = 0, Size = 0 }); // true
        _objectTable.Add(new ObjectEntry { Location = 0, Size = 0 }); // false
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
        
        return _objectTable[(int)index];
    }

    private void MarkPhase()
    {
        // Mark all reachable objects starting from roots
        // This is simplified - in full implementation would trace from all contexts
        for (var i = 0; i < _objectTable.Count; i++)
        {
            if (_objectTable[i].ReferenceCount > 0)
            {
                MarkObject(OOP.FromPointer((uint)i));
            }
        }
    }

    private void MarkObject(OOP oop)
    {
        if (oop.IsSmallInteger || oop.IsNil)
        {
            return;
        }
        
        var entry = GetObjectEntry(oop);
        var headerAddr = entry.Location;
        var header = new ObjectHeader { HeaderWord = _heap[headerAddr] };
        
        if (header.IsMarked)
        {
            return; // Already marked
        }
        
        // Mark the object
        _heap[headerAddr] = header.WithMark().HeaderWord;
        
        // Recursively mark referenced objects
        if (header.HasPointers)
        {
            for (var i = 0; i < header.Size; i++)
            {
                var fieldOop = new OOP(_heap[entry.Location + 1 + (uint)i]);
                MarkObject(fieldOop);
            }
        }
    }

    private void SweepPhase()
    {
        // Sweep unmarked objects and add to free list
        for (var i = 3; i < _objectTable.Count; i++) // Skip special objects
        {
            var entry = _objectTable[i];
            if (entry.Location == 0)
            {
                continue; // Already free
            }
            
            var header = new ObjectHeader { HeaderWord = _heap[entry.Location] };
            
            if (!header.IsMarked)
            {
                // Free this object
                _objectTable[i] = new ObjectEntry();
                _freeList.Push((uint)i);
            }
            else
            {
                // Clear mark for next GC
                _heap[entry.Location] = header.WithoutMark().HeaderWord;
            }
        }
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

    private struct ObjectEntry
    {
        public uint Location;
        public uint Size;
        public uint ReferenceCount;
        public uint ByteSize; // For byte objects
    }
}