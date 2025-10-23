using System.Diagnostics;
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Implementation of ISmalltalkHeapManager providing low-level Smalltalk object management.
/// Uses tagged OOPs for SmallIntegers and ObjectTable indirection for heap objects.
/// Implements non-moving garbage collection that leverages .NET memory management.
/// </summary>
public class SmalltalkHeapManager : ISmalltalkHeapManager
{
    private readonly List<ObjectTableBlock> _objectTableBlocks = new();
    private readonly Stack<int> _freeObjectTableIndices = new();
    private int _nextObjectTableIndex = 1; // Start at 1, reserve 0 for null/special cases
    private const int ObjectTableBlockSize = 1024;
    
    // GC root management
    private SmalltalkOOP[] _gcRoots = Array.Empty<SmalltalkOOP>();
    
    // Class name field index (determined from coldstart.json during initialization)
    private int _classNameFieldIndex = -1;

    /// <summary>Represents a block of ObjectTable entries for scalable growth.</summary>
    private sealed class ObjectTableBlock
    {
        public readonly ObjectTableEntry?[] Entries = new ObjectTableEntry[ObjectTableBlockSize];
        public int ActiveCount = 0;
    }

    public SmalltalkHeapManager()
    {
        // Allocate initial ObjectTable block
        _objectTableBlocks.Add(new ObjectTableBlock());
    }

    /// <summary>
    /// Configures the instance variable index for the class name field.
    /// This must be called after bundle is loaded to enable GetClassName() functionality.
    /// </summary>
    public void SetClassNameFieldIndex(int index)
    {
        _classNameFieldIndex = index;
    }

    /// <summary>
    /// Allocates object with specified memory layout.
    /// Memory is zero-initialized by .NET guarantee.
    /// </summary>
    public SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize)
    {
        // Validate parameters
        if (fixedSlots < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedSlots), "Fixed slots must be non-negative");
        if (indexedSize < 0)
            throw new ArgumentOutOfRangeException(nameof(indexedSize), "Indexed size must be non-negative");

        // Calculate total size: header (1 word) + fixed slots + indexed data
        int headerWords = 1; // Header slot
        int indexedWords = CalculateIndexedWords(format, indexedSize);
        int totalWords = headerWords + fixedSlots + indexedWords;

        // Allocate memory from .NET (zero-initialized)
        int totalBytes = totalWords * 4;
        byte[] memory = new byte[totalBytes];
        
        // Pin memory and get pointer
        GCHandle handle = GCHandle.Alloc(memory, GCHandleType.Pinned);
        IntPtr memoryPtr = handle.AddrOfPinnedObject();

        // Get ObjectTable index
        int tableIndex = AllocateObjectTableIndex();
        

        // Create ObjectTableEntry with format information for GC
        // For ByteIndexed objects, preserve the original indexed size before word alignment
        int? originalIndexedSize = (format == ObjectFormat.ByteIndexed) ? indexedSize : null;
        var entry = new ObjectTableEntry(memoryPtr, format, totalWords, fixedSlots, originalIndexedSize);
        
        // Store entry in ObjectTable
        SetObjectTableEntry(tableIndex, entry);

        // Store GCHandle reference for cleanup (using NamedSlots temporarily)
        entry.NamedSlots = new Dictionary<string, int> { ["GCHandle"] = handle.GetHashCode() };
        
        // TODO: Need to properly track GCHandles for cleanup during GC

        // Return tagged OOP
        return SmalltalkOOP.FromObjectTableIndex(tableIndex);
    }

    public SmalltalkOOP AllocateInstanceOf(SmalltalkOOP classOOP, int indexedSize)
    {
        // Receiver must be a valid class object (not SmallInteger)
        if (classOOP.IsSmallInteger)
            throw new ArgumentException("Cannot allocate instance of SmallInteger", nameof(classOOP));

        // Read the class's format field (instance variable 2 in Behavior hierarchy)
        // Behavior: superclass(0), methodDict(1), format(2), subclasses(3)
        SmalltalkOOP formatOOP = GetInstanceVariable(classOOP, 2);

        if (!formatOOP.IsSmallInteger)
            throw new InvalidOperationException($"Class format field must be a SmallInteger, got {formatOOP}");

        // Decode the format SmallInteger
        // Format encoding (from ColdStartJsonGenerator.cs CalculateFormatWord):
        // - Bits 12-13: instanceSpec (instance specification: 01=pointer, 10=word-indexable, 11=byte-indexable)
        // - Bits 6-11: fixedFieldCount (number of instance variables, 0-63)
        // - Bit 4: isIndexable flag
        // - Bit 3: hasPointers flag
        int formatValue = formatOOP.AsSmallInteger();
        int instanceSpec = (formatValue >> 12) & 0x03;
        int instanceVariableCount = (formatValue >> 6) & 0x3F; // Bits 6-11
        bool isIndexable = (formatValue & 0x10) != 0;
        bool hasPointers = (formatValue & 0x08) != 0;

        // Decode object format using the same logic as Phase2
        ObjectFormat format = instanceSpec switch
        {
            0x00 => ObjectFormat.Pointer, // Fixed pointer objects
            0x01 => ObjectFormat.Pointer, // Fixed pointer objects
            0x02 when hasPointers && isIndexable => ObjectFormat.PointerIndexed, // Variable pointer objects
            0x02 when hasPointers && !isIndexable => ObjectFormat.Pointer, // Fixed pointer objects
            0x02 when !hasPointers && isIndexable => ObjectFormat.ByteIndexed, // ByteIndexed objects
            0x02 when !hasPointers && !isIndexable => ObjectFormat.WordIndexed, // Word objects
            0x03 => ObjectFormat.ByteIndexed, // Byte objects
            _ => throw new InvalidOperationException($"Unknown format instanceSpec: {instanceSpec:X2}")
        };

        // Allocate the object with the decoded format
        SmalltalkOOP newInstanceOOP = AllocateObject(format, instanceVariableCount, indexedSize);

        // Set the new instance's class reference (slot 0) to the class
        SetSlot(newInstanceOOP, 0, classOOP);

        return newInstanceOOP;
    }


    /// <summary>
    /// Raw slot access - gets OOP from any slot (semantic-agnostic).
    /// </summary>
    public SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get slot from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Calculate total slots available
        int totalSlots = entry.SizeInWords; // Each word is one slot
        
        // Validate slot index
        if (slotIndex < 0 || slotIndex >= totalSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {totalSlots - 1}] for object {objectOOP}");

        // Read from memory at slot offset
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            return new SmalltalkOOP(ptr[slotIndex]);
        }
    }

    /// <summary>
    /// Raw slot access - sets OOP in any slot (semantic-agnostic).
    /// </summary>
    public void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set slot in SmallInteger");


        var entry = GetObjectTableEntry(objectOOP);
        
        // Calculate total slots available
        int totalSlots = entry.SizeInWords; // Each word is one slot
        
        // Validate slot index
        if (slotIndex < 0 || slotIndex >= totalSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {totalSlots - 1}] for object {objectOOP}");

        // Write to memory at slot offset
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            ptr[slotIndex] = value.RawValue;
        }
    }

    /// <summary>Calculates words needed for indexed data based on format.</summary>
    private static int CalculateIndexedWords(ObjectFormat format, int indexedSize)
    {
        return format switch
        {
            ObjectFormat.Pointer => 0, // No indexed data
            ObjectFormat.PointerIndexed => indexedSize, // 1 word per OOP
            ObjectFormat.ByteIndexed => (indexedSize + 3) / 4, // Round up to words
            ObjectFormat.WordIndexed => (indexedSize + 1) / 2, // Round up to words  
            _ => throw new ArgumentException($"Unknown object format: {format}")
        };
    }

    /// <summary>Allocates a new ObjectTable index, growing blocks as needed.</summary>
    private int AllocateObjectTableIndex()
    {
        // Reuse free index if available
        if (_freeObjectTableIndices.Count > 0)
            return _freeObjectTableIndices.Pop();

        // Check if we need a new block
        int blockIndex = _nextObjectTableIndex / ObjectTableBlockSize;
        if (blockIndex >= _objectTableBlocks.Count)
        {
            _objectTableBlocks.Add(new ObjectTableBlock());
        }

        return _nextObjectTableIndex++;
    }

    /// <summary>Gets ObjectTableEntry for a given OOP.</summary>
    private ObjectTableEntry GetObjectTableEntry(SmalltalkOOP objectOOP)
    {
        int tableIndex = objectOOP.ToObjectTableIndex();
        int blockIndex = tableIndex / ObjectTableBlockSize;
        int entryIndex = tableIndex % ObjectTableBlockSize;

        if (blockIndex >= _objectTableBlocks.Count)
            throw new ArgumentException($"Invalid ObjectTable index: {tableIndex}");

        var entry = _objectTableBlocks[blockIndex].Entries[entryIndex];
        if (entry == null)
            throw new ArgumentException($"ObjectTable entry {tableIndex} is null");

        return entry;
    }

    /// <summary>Sets ObjectTableEntry for a given index.</summary>
    private void SetObjectTableEntry(int tableIndex, ObjectTableEntry entry)
    {
        int blockIndex = tableIndex / ObjectTableBlockSize;
        int entryIndex = tableIndex % ObjectTableBlockSize;

        if (blockIndex >= _objectTableBlocks.Count)
            throw new ArgumentException($"Invalid ObjectTable index: {tableIndex}");

        var block = _objectTableBlocks[blockIndex];
        bool wasNull = block.Entries[entryIndex] == null;
        
        block.Entries[entryIndex] = entry;
        
        if (wasNull && entry != null)
            block.ActiveCount++;
        else if (!wasNull && entry == null)
            block.ActiveCount--;
    }

    /// <summary>
    /// Gets an instance variable by index with bounds checking.
    /// </summary>
    public SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get instance variable of SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        // Get instance variable count from ObjectTableEntry
        int instanceVarCount = entry.InstanceVariableCount;

        // DEBUG: Track calls to GetInstanceVariable on oop=8952 (true singleton)
        if (objectOOP.RawValue == 8952)
        {
            Console.WriteLine($"[DEBUG GetInstanceVariable] Called on oop=8952 (true), index={index}, instanceVarCount={instanceVarCount}");
            Console.WriteLine($"[DEBUG GetInstanceVariable] Stack trace:");
            Console.WriteLine(Environment.StackTrace);
        }

        // Validate index
        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        // Read from memory: skip header (4 bytes) + index * 4 bytes
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            return new SmalltalkOOP(ptr[1 + index]); // +1 to skip header
        }
    }

    /// <summary>
    /// Sets an instance variable by index with bounds checking.
    /// </summary>
    public void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        // Default behavior: use barrier for safety
        SetInstanceVariableWithBarrier(objectOOP, index, value);
    }
    
    public void SetInstanceVariableWithBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set instance variable of SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Get instance variable count from ObjectTableEntry
        int instanceVarCount = entry.InstanceVariableCount;

        // Validate index
        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        // Write to memory: skip header (4 bytes) + index * 4 bytes
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            ptr[1 + index] = value.RawValue; // +1 to skip header
        }
        
        // Write barrier: notify GC of reference update
        // For future incremental/concurrent GC: record that objectOOP[index] now points to value
        // Current implementation: no-op since we use .NET GC which handles reference tracking
        // In a true Smalltalk GC, this would mark the card or add to remembered set
    }
    
    public void SetInstanceVariableNoBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set instance variable of SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Get instance variable count from ObjectTableEntry
        int instanceVarCount = entry.InstanceVariableCount;

        // Validate index
        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        // Direct write without barrier - ONLY safe during loader
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            ptr[1 + index] = value.RawValue; // +1 to skip header
        }
    }

    /// <summary>Helper to calculate indexed size from entry metadata.</summary>
    private int GetIndexedSizeFromEntry(ObjectTableEntry entry)
    {
        int totalWords = entry.SizeInWords;
        int headerWords = 1;
        int instanceVarCount = entry.InstanceVariableCount;
        
        // Calculate indexed portion: total - header - instance variables
        int indexedWords = totalWords - headerWords - instanceVarCount;
        if (indexedWords < 0) indexedWords = 0; // Safety check
        
        return entry.Format switch
        {
            ObjectFormat.PointerIndexed => indexedWords,
            ObjectFormat.ByteIndexed => entry.OriginalIndexedSize ?? (indexedWords * 4), // Use original size if available, fallback to word-aligned calculation
            ObjectFormat.WordIndexed => indexedWords * 2,
            _ => 0
        };
    }

    /// <summary>
    /// Gets indexed OOP from PointerIndexed objects with format and bounds validation.
    /// </summary>
    public SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed OOP from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object {objectOOP} with format {entry.Format} does not support OOP indexing (attempted GetIndexedOOP at index {index})");

        // Calculate indexed portion offset and size
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Read from memory: header + instance vars + index
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            return new SmalltalkOOP(ptr[1 + instanceVarCount + index]);
        }
    }

    /// <summary>
    /// Sets indexed OOP in PointerIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed OOP in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object {objectOOP} with format {entry.Format} does not support OOP indexing (attempted SetIndexedOOP at index {index})");

        // Calculate indexed portion offset and size
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Write to memory: header + instance vars + index
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            ptr[1 + instanceVarCount + index] = value.RawValue;
        }
    }

    /// <summary>
    /// Gets indexed byte from ByteIndexed objects with format and bounds validation.
    /// </summary>
    public byte GetIndexedByte(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed byte from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.Format} does not support byte indexing");

        // Calculate byte offset
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int byteOffset = (1 + instanceVarCount) * 4; // Skip header + instance vars (in bytes)
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Read byte from memory
        unsafe
        {
            byte* ptr = (byte*)entry.MemoryPointer.ToPointer();
            return ptr[byteOffset + index];
        }
    }

    /// <summary>
    /// Sets indexed byte in ByteIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed byte in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.Format} does not support byte indexing");

        // Calculate byte offset
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int byteOffset = (1 + instanceVarCount) * 4; // Skip header + instance vars (in bytes)
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Write byte to memory
        unsafe
        {
            byte* ptr = (byte*)entry.MemoryPointer.ToPointer();
            ptr[byteOffset + index] = value;
        }
    }

    /// <summary>
    /// Gets indexed word from WordIndexed objects with format and bounds validation.
    /// </summary>
    public ushort GetIndexedWord(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed word from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.Format} does not support word indexing");

        // Calculate word offset
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int wordOffset = (1 + instanceVarCount) * 2; // Skip header + instance vars (in words)
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Read word from memory
        unsafe
        {
            ushort* ptr = (ushort*)entry.MemoryPointer.ToPointer();
            return ptr[wordOffset + index];
        }
    }

    /// <summary>
    /// Sets indexed word in WordIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed word in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        
        // Validate format
        if (entry.Format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.Format} does not support word indexing");

        // Calculate word offset
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        int wordOffset = (1 + instanceVarCount) * 2; // Skip header + instance vars (in words)
        int indexedSize = GetIndexedSizeFromEntry(entry);
        
        // Validate index range
        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {indexedSize - 1}] for object {objectOOP}");

        // Write word to memory
        unsafe
        {
            ushort* ptr = (ushort*)entry.MemoryPointer.ToPointer();
            ptr[wordOffset + index] = value;
        }
    }


    /// <summary>Helper to get instance variable count from entry.</summary>
    private int GetInstanceVariableCountFromEntry(ObjectTableEntry entry)
    {
        return entry.InstanceVariableCount;
    }

    /// <summary>
    /// Gets the OOP stored in slot 0 (header).
    /// DEPRECATED: Use GetSlot(objectOOP, 0) instead for semantic-agnostic access.
    /// </summary>
    public SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("SmallIntegers don't have header slot");

        var entry = GetObjectTableEntry(objectOOP);
        
        unsafe
        {
            int* ptr = (int*)entry.MemoryPointer.ToPointer();
            return new SmalltalkOOP(*ptr); // First word is header slot
        }
    }

    /// <summary>
    /// Gets the object format from ObjectTableEntry.
    /// </summary>
    public ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("SmallIntegers don't have object format");

        var entry = GetObjectTableEntry(objectOOP);
        return entry.Format;
    }

    /// <summary>
    /// Gets the total size of an object in words.
    /// </summary>
    public int GetObjectSizeInWords(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0; // SmallIntegers have no heap footprint

        var entry = GetObjectTableEntry(objectOOP);
        return entry.SizeInWords;
    }

    /// <summary>
    /// Gets the number of instance variables for an object.
    /// </summary>
    public int GetInstanceVariableCount(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0; // SmallIntegers have no instance variables

        var entry = GetObjectTableEntry(objectOOP);
        return GetInstanceVariableCountFromEntry(entry);
    }

    /// <summary>
    /// Gets the size of the indexed portion of an object.
    /// </summary>
    public int GetIndexedSize(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0; // SmallIntegers have no indexed data

        var entry = GetObjectTableEntry(objectOOP);
        return GetIndexedSizeFromEntry(entry);
    }

    /// <summary>
    /// Marks an object as reachable during GC mark phase.
    /// </summary>
    public void MarkObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return; // SmallIntegers don't need marking

        if (!IsValidObjectOOP(objectOOP))
            return; // Invalid OOPs are ignored

        var entry = GetObjectTableEntry(objectOOP);
        entry.IsMarked = true;
    }

    /// <summary>
    /// Sets the GC root objects array. The GC will start its reachability walk from these objects.
    /// This replaces any previously set root objects.
    /// </summary>
    public void SetGCRoots(SmalltalkOOP[] rootObjects)
    {
        _gcRoots = rootObjects?.ToArray() ?? Array.Empty<SmalltalkOOP>();
    }

    /// <summary>
    /// Adds additional OOPs to the current GC roots array.
    /// Useful for temporarily adding objects during specific operations.
    /// </summary>
    public void AddGCRoots(SmalltalkOOP[] additionalRoots)
    {
        if (additionalRoots == null || additionalRoots.Length == 0)
            return;
            
        var newRoots = new SmalltalkOOP[_gcRoots.Length + additionalRoots.Length];
        _gcRoots.CopyTo(newRoots, 0);
        additionalRoots.CopyTo(newRoots, _gcRoots.Length);
        _gcRoots = newRoots;
    }

    /// <summary>
    /// Gets the current GC roots array (defensive copy).
    /// </summary>
    public SmalltalkOOP[] GetGCRoots()
    {
        return _gcRoots.ToArray();
    }

    /// <summary>
    /// DEBUG: Performs GC marking phase only and returns unreachable objects without collecting them.
    /// Returns array of OOPs that would be collected during a full GC.
    /// </summary>
    public SmalltalkOOP[] GetUnreachableObjects()
    {
        var unreachableObjects = new List<SmalltalkOOP>();
        
        // Phase 1: Clear all mark bits
        foreach (var block in _objectTableBlocks)
        {
            for (int i = 0; i < block.Entries.Length; i++)
            {
                if (block.Entries[i] != null)
                    block.Entries[i]!.IsMarked = false;
            }
        }
        
        // Phase 2: Mark from configured roots
        foreach (var rootOOP in _gcRoots)
        {
            MarkObjectRecursively(rootOOP);
        }
        
        // Phase 3: Find unmarked objects (would be collected)
        for (int blockIndex = 0; blockIndex < _objectTableBlocks.Count; blockIndex++)
        {
            var block = _objectTableBlocks[blockIndex];
            for (int i = 0; i < block.Entries.Length; i++)
            {
                var entry = block.Entries[i];
                if (entry != null && !entry.IsMarked)
                {
                    int objectId = blockIndex * ObjectTableBlockSize + i;
                    unreachableObjects.Add(objectId);
                }
            }
        }
        
        return unreachableObjects.ToArray();
    }

    /// <summary>
    /// Performs mark-and-sweep garbage collection using the configured root objects.
    /// Returns detailed statistics about the collection.
    /// </summary>
    public GCStatistics GarbageCollect()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Phase 1: Count objects before collection
        int objectsBeforeCollection = GetActiveObjectCount();
        
        // Phase 2: Clear all mark bits
        foreach (var block in _objectTableBlocks)
        {
            for (int i = 0; i < block.Entries.Length; i++)
            {
                if (block.Entries[i] != null)
                    block.Entries[i]!.IsMarked = false;
            }
        }
        
        double markStartTime = stopwatch.Elapsed.TotalMilliseconds;
        
        // Phase 3: Mark from configured roots
        foreach (var rootOOP in _gcRoots)
        {
            MarkObjectRecursively(rootOOP);
        }
        
        double markEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double markTime = markEndTime - markStartTime;
        
        // Phase 4: Sweep unmarked objects
        var collectedByFormat = new Dictionary<ObjectFormat, int>();
        long estimatedBytesFreed = 0;
        int objectsCollected = 0;
        
        var firstCollectedIds = new List<int>();
        
        for (int blockIndex = 0; blockIndex < _objectTableBlocks.Count; blockIndex++)
        {
            var block = _objectTableBlocks[blockIndex];
            for (int i = 0; i < block.Entries.Length; i++)
            {
                var entry = block.Entries[i];
                if (entry != null && !entry.IsMarked)
                {
                    // Track collection statistics
                    collectedByFormat[entry.Format] = collectedByFormat.GetValueOrDefault(entry.Format) + 1;
                    estimatedBytesFreed += entry.SizeInBytes;
                    objectsCollected++;
                    
                    // Track collected object IDs for debugging
                    int objectId = blockIndex * ObjectTableBlockSize + i;
                    if (firstCollectedIds.Count < 20)
                    {
                        firstCollectedIds.Add(objectId);
                    }
                    
                    // Free the object (set entry to null)
                    block.Entries[i] = null;
                    block.ActiveCount--;
                    
                    // TODO: Free GCHandle if we were tracking them properly
                }
            }
        }
        
        
        double sweepEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double sweepTime = sweepEndTime - markEndTime;
        
        int objectsAfterCollection = GetActiveObjectCount();
        int freeEntries = GetObjectTableSize() - objectsAfterCollection;
        
        
        return new GCStatistics
        {
            TotalObjects = objectsBeforeCollection,
            LiveObjects = objectsAfterCollection,
            CollectedObjects = objectsCollected,
            MarkTimeMs = markTime,
            SweepTimeMs = sweepTime,
            TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            EstimatedBytesFreed = estimatedBytesFreed,
            CollectedByFormat = collectedByFormat,
            FreeObjectTableEntries = freeEntries
        };
    }

    /// <summary>Recursively marks an object and all objects it references.</summary>
    private void MarkObjectRecursively(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger || !IsValidObjectOOP(objectOOP))
            return;

        var entry = GetObjectTableEntry(objectOOP);
        if (entry.IsMarked)
            return; // Already marked


        entry.IsMarked = true;

        // Mark header slot OOP
        var headerOOP = GetSlot(objectOOP, 0);
        MarkObjectRecursively(headerOOP);

        // Mark instance variables
        int instanceVarCount = GetInstanceVariableCountFromEntry(entry);
        for (int i = 0; i < instanceVarCount; i++)
        {
            var instVarOOP = GetInstanceVariable(objectOOP, i);
            MarkObjectRecursively(instVarOOP);
        }

        // Mark indexed data (if it contains OOPs)
        if (entry.Format == ObjectFormat.PointerIndexed)
        {
            int indexedSize = GetIndexedSizeFromEntry(entry);
            if (indexedSize < 0)
            {
                return;
            }
            
            // Debug CompiledMethod traversal - they should have ByteArray OOP in last slot
            var classOOP = GetSlot(objectOOP, 0);
            bool isCompiledMethod = IsObjectOfClass(objectOOP, "CompiledMethod");
            
            
            for (int i = 0; i < indexedSize; i++)
            {
                var indexedOOP = GetIndexedOOP(objectOOP, i);
                MarkObjectRecursively(indexedOOP);
            }
        }
    }


    /// <summary>
    /// Gets current GC statistics without performing collection.
    /// </summary>
    public GCStatistics GetGCStatistics()
    {
        var objectsByFormat = new Dictionary<ObjectFormat, int>();
        long estimatedMemoryUsage = 0;
        int activeObjects = 0;

        foreach (var block in _objectTableBlocks)
        {
            for (int i = 0; i < block.Entries.Length; i++)
            {
                var entry = block.Entries[i];
                if (entry != null)
                {
                    activeObjects++;
                    objectsByFormat[entry.Format] = objectsByFormat.GetValueOrDefault(entry.Format) + 1;
                    estimatedMemoryUsage += entry.SizeInBytes;
                }
            }
        }

        return new GCStatistics
        {
            TotalObjects = activeObjects,
            LiveObjects = activeObjects,
            CollectedObjects = 0, // No collection performed
            MarkTimeMs = 0,
            SweepTimeMs = 0,
            TotalTimeMs = 0,
            EstimatedBytesFreed = 0,
            CollectedByFormat = new Dictionary<ObjectFormat, int>(),
            FreeObjectTableEntries = GetObjectTableSize() - activeObjects
        };
    }

    public int GetObjectTableSize()
    {
        return _nextObjectTableIndex - 1;
    }

    public int GetActiveObjectCount()
    {
        return _objectTableBlocks.Sum(block => block.ActiveCount);
    }

    public bool IsValidObjectOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return true; // SmallIntegers are always valid

        int tableIndex = objectOOP.ToObjectTableIndex();
        if (tableIndex <= 0 || tableIndex >= _nextObjectTableIndex)
            return false;

        int blockIndex = tableIndex / ObjectTableBlockSize;
        int entryIndex = tableIndex % ObjectTableBlockSize;

        return blockIndex < _objectTableBlocks.Count && 
               _objectTableBlocks[blockIndex].Entries[entryIndex] != null;
    }

    /// <summary>
    /// Helper method to get the class name of an object.
    /// Traverses: object -> class -> class name (symbol) -> extract string
    /// </summary>
    private string GetObjectClassName(SmalltalkOOP objectOOP)
    {
        try
        {
            if (objectOOP.IsSmallInteger || !IsValidObjectOOP(objectOOP))
                return "SmallInteger";

            // Get class OOP from object header (slot 0)
            var classOOP = GetSlot(objectOOP, 0);
            if (classOOP.IsSmallInteger || !IsValidObjectOOP(classOOP))
                return "InvalidClass";

            // Get class name symbol from class object
            var classNameSymbolOOP = GetInstanceVariable(classOOP, 0);
            if (classNameSymbolOOP.IsSmallInteger || !IsValidObjectOOP(classNameSymbolOOP))
                return "InvalidSymbol";

            // Extract the string from the Symbol's byte data
            var symbolEntry = GetObjectTableEntry(classNameSymbolOOP);
            if (symbolEntry.Format != ObjectFormat.ByteIndexed)
                return "NotSymbol";

            int symbolSize = GetIndexedSizeFromEntry(symbolEntry);
            var symbolBytes = new byte[symbolSize];
            for (int i = 0; i < symbolSize; i++)
            {
                symbolBytes[i] = GetIndexedByte(classNameSymbolOOP, i);
            }
            
            return System.Text.Encoding.UTF8.GetString(symbolBytes);
        }
        catch
        {
            return "Error";
        }
    }

    /// <summary>
    /// Helper method to check if an object is of a specific class by name.
    /// Traverses: object -> class -> class name (symbol) -> compare string
    /// </summary>
    private bool IsObjectOfClass(SmalltalkOOP objectOOP, string className)
    {
        try
        {
            if (objectOOP.IsSmallInteger || !IsValidObjectOOP(objectOOP))
                return false;

            // Get class OOP from object header (slot 0)
            var classOOP = GetSlot(objectOOP, 0);
            if (classOOP.IsSmallInteger || !IsValidObjectOOP(classOOP))
                return false;

            // Get class name symbol from class object
            // Assuming class has name in instance variable 0 (typical Smalltalk layout)
            var classNameSymbolOOP = GetInstanceVariable(classOOP, 0);
            if (classNameSymbolOOP.IsSmallInteger || !IsValidObjectOOP(classNameSymbolOOP))
                return false;

            // Check if this is a Symbol object with ByteIndexed format containing the class name
            var symbolEntry = GetObjectTableEntry(classNameSymbolOOP);
            if (symbolEntry.Format != ObjectFormat.ByteIndexed)
                return false;

            // Extract the string from the Symbol's byte data
            int symbolSize = GetIndexedSizeFromEntry(symbolEntry);
            var symbolBytes = new byte[symbolSize];
            for (int i = 0; i < symbolSize; i++)
            {
                symbolBytes[i] = GetIndexedByte(classNameSymbolOOP, i);
            }
            
            string actualClassName = System.Text.Encoding.UTF8.GetString(symbolBytes);
            return actualClassName == className;
        }
        catch
        {
            return false; // Any error means it's not the expected class
        }
    }

    public string GetClassName(SmalltalkOOP objectOOP)
    {
        try
        {
            if (objectOOP.IsSmallInteger)
                return "SmallInteger";
            
            if (!IsValidObjectOOP(objectOOP))
                return "Invalid";

            // Get class OOP from object header (slot 0)
            var classOOP = GetSlot(objectOOP, 0);
            if (classOOP.IsSmallInteger)
                return "InvalidClass(SmallInt)";
            
            if (!IsValidObjectOOP(classOOP))
                return $"InvalidClass(OOP={classOOP.RawValue})";

            var classEntry = GetObjectTableEntry(classOOP);
            int instVarCount = GetInstanceVariableCountFromEntry(classEntry);
            
            // If class name field index is configured, try to extract the class name
            // The name field is an instance variable of the class object that points to a Symbol
            if (_classNameFieldIndex >= 0 && instVarCount > _classNameFieldIndex)
            {
                // Get name Symbol from the configured instance variable index
                var classNameSymbolOOP = GetInstanceVariable(classOOP, _classNameFieldIndex);
                
                if (!classNameSymbolOOP.IsSmallInteger && IsValidObjectOOP(classNameSymbolOOP))
                {
                    var symbolEntry = GetObjectTableEntry(classNameSymbolOOP);
                    
                    if (symbolEntry.Format == ObjectFormat.ByteIndexed)
                    {
                        int symbolSize = GetIndexedSizeFromEntry(symbolEntry);
                        if (symbolSize > 0)
                        {
                            var symbolBytes = new byte[symbolSize];
                            for (int i = 0; i < symbolSize; i++)
                            {
                                symbolBytes[i] = GetIndexedByte(classNameSymbolOOP, i);
                            }
                            return System.Text.Encoding.UTF8.GetString(symbolBytes);
                        }
                    }
                }
            }

            return $"UnknownClass(classOOP={classOOP},fmt={classEntry.Format},ivars={instVarCount})";
        }
        catch (Exception ex)
        {
            return $"Error({ex.Message})";
        }
    }

    public void ValidateAllObjectClasses(Dictionary<string, int> localIdToObjectId)
    {
        int invalidCount = 0;
        var invalidObjects = new List<(int objectId, string className)>();

        // Iterate through all object table blocks
        for (int blockIdx = 0; blockIdx < _objectTableBlocks.Count; blockIdx++)
        {
            var block = _objectTableBlocks[blockIdx];
            for (int i = 0; i < ObjectTableBlockSize; i++)
            {
                var entry = block.Entries[i];
                if (entry == null)
                    continue;

                int objectId = blockIdx * ObjectTableBlockSize + i;
                var objectOOP = objectId;
                string className = GetClassName(objectOOP);
                
                if (className.StartsWith("Invalid") || className.StartsWith("NoClassName") || 
                    className.StartsWith("BadClassNameFormat") || className.StartsWith("Error"))
                {
                    invalidCount++;
                    if (invalidObjects.Count < 10)
                    {
                        invalidObjects.Add((objectId, className));
                    }
                }
            }
        }

        if (invalidCount > 0)
        {
            Console.WriteLine($"⚠️  VALIDATION WARNING: Found {invalidCount} objects with invalid class references:");
            foreach (var (objectId, className) in invalidObjects)
            {
                var entry = GetObjectTableEntry(SmalltalkOOP.FromObjectTableIndex(objectId));
                var classOOP = GetSlot(objectId, 0);
                
                // Try to find localId for this object
                string localId = localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectId).Key ?? "unknown";
                
                // Get some info about the object's contents
                int instVarCount = GetInstanceVariableCountFromEntry(entry);
                int indexedSize = GetIndexedSizeFromEntry(entry);
                
                // Show instance variable values to help identify the object
                var objectOOP = objectId;
                string instVarInfo = "";
                if (instVarCount > 0 && instVarCount <= 3)
                {
                    var instVars = new List<string>();
                    for (int i = 0; i < instVarCount; i++)
                    {
                        var varOOP = GetInstanceVariable(objectOOP, i);
                        instVars.Add(varOOP.IsSmallInteger ? $"SmallInt({varOOP.AsSmallInteger()})" : $"OOP({varOOP})");
                    }
                    instVarInfo = $", instVarValues=[{string.Join(", ", instVars)}]";
                }
                
                Console.WriteLine($"  Object {objectId} (localId={localId}): class='{className}', classOOP={classOOP}, format={entry.Format}, instVars={instVarCount}, indexed={indexedSize}{instVarInfo}");
            }
            if (invalidCount > 10)
            {
                Console.WriteLine($"  ... and {invalidCount - 10} more");
            }
        }
    }
}