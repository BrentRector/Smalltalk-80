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
    /// Allocates object with specified memory layout using managed storage.
    /// Memory is zero-initialized by .NET guarantee.
    /// </summary>
    public SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize)
    {
        // Validate parameters
        if (fixedSlots < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedSlots), "Fixed slots must be non-negative");
        if (indexedSize < 0)
            throw new ArgumentOutOfRangeException(nameof(indexedSize), "Indexed size must be non-negative");

        // Calculate total size for metadata tracking
        int headerWords = 1; // Header slot
        int indexedWords = CalculateIndexedWords(format, indexedSize);
        int totalWords = headerWords + fixedSlots + indexedWords;

        // Allocate managed backing storage based on format
        object? managedStorage = format switch
        {
            ObjectFormat.Pointer => new SmalltalkOOP[1 + fixedSlots], // Header + fixed slots only
            ObjectFormat.PointerIndexed => new SmalltalkOOP[1 + fixedSlots + indexedSize], // Header + fixed + indexed
            ObjectFormat.ByteIndexed => new byte[indexedSize], // Raw byte storage (no header/fixed in array)
            ObjectFormat.WordIndexed => new ushort[indexedSize], // Raw word storage
            _ => throw new ArgumentException($"Unknown object format: {format}")
        };

        // Get ObjectTable index
        int tableIndex = AllocateObjectTableIndex();

        // Create ObjectTableEntry with managed storage and explicit metadata
        int? originalIndexedSize = (format == ObjectFormat.ByteIndexed) ? indexedSize : null;
        var entry = new ObjectTableEntry(
            managedStorage: managedStorage,
            format: format,
            sizeInWords: totalWords,
            instanceVariableCount: fixedSlots,
            originalIndexedSize: originalIndexedSize
        );

        // Store entry in ObjectTable
        SetObjectTableEntry(tableIndex, entry);

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
            0x02 when hasPointers && isIndexable => ObjectFormat.PointerIndexed, // Variable pointer objects (variableSubclass:)
            0x02 when hasPointers && !isIndexable => ObjectFormat.Pointer, // Fixed pointer objects
            0x02 when !hasPointers && isIndexable => ObjectFormat.WordIndexed, // WordIndexed objects (variableWordSubclass:)
            0x02 when !hasPointers && !isIndexable => ObjectFormat.Pointer, // Fixed non-pointer objects (rare)
            0x03 => ObjectFormat.ByteIndexed, // Byte objects (variableByteSubclass:)
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
        return ReadSlot(entry, slotIndex);
    }

    /// <summary>
    /// Raw slot access - sets OOP in any slot (semantic-agnostic).
    /// </summary>
    public void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set slot in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        WriteSlot(entry, slotIndex, value);
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

        // Canonical mapping: index >= 1, so (index - 1) gives 0-based block/entry
        int blockIndex = (_nextObjectTableIndex - 1) / ObjectTableBlockSize;
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

        // Canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlockSize;

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
        // Canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlockSize;

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
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        // Slot layout: [0]=header, [1..N]=instance variables
        return ReadSlot(entry, 1 + index);
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
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");
        // Slot layout: [0]=header, [1..N]=instance variables
        WriteSlot(entry, 1 + index, value);

        // Write barrier: Future implementation for incremental/concurrent GC
    }

    public void SetInstanceVariableNoBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set instance variable of SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        // Direct write without barrier - ONLY safe during loader
        WriteSlot(entry, 1 + index, value);
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

        if (entry.FormatExplicit != ObjectFormat.PointerIndexed)
        {
            // Enhanced error message with diagnostic info
            var classOOP = GetSlot(objectOOP, 0); // Get class from slot 0
            string className = "UnknownClass";
            try
            {
                if (!classOOP.IsSmallInteger && classOOP != default)
                {
                    // Try to get class name for better diagnostics
                    var classEntry = GetObjectTableEntry(classOOP);
                    if (classEntry.FormatExplicit == ObjectFormat.Pointer || classEntry.FormatExplicit == ObjectFormat.PointerIndexed)
                    {
                        // Classes have a 'name' field - but we need the field index
                        // For now, just show the classOOP
                        className = $"class={classOOP}";
                    }
                }
            }
            catch
            {
                // Ignore errors in diagnostic code
            }

            throw new InvalidOperationException(
                $"GetIndexedOOP called on non-PointerIndexed object!\n" +
                $"  objectOOP: {objectOOP}\n" +
                $"  format: {entry.FormatExplicit} (expected PointerIndexed)\n" +
                $"  {className}\n" +
                $"  index: {index}\n" +
                $"  This likely means a ByteIndexed Symbol/String or bytecode array is being used where a PointerIndexed object (like Array or MethodDictionary) was expected.");
        }

        return ReadIndexedOop(entry, index);
    }

    /// <summary>
    /// Sets indexed OOP in PointerIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed OOP in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.PointerIndexed)
        {
            // Enhanced error message with diagnostic info
            var classOOP = GetSlot(objectOOP, 0);
            string className = "UnknownClass";
            string valueClassName = "UnknownClass";
            try
            {
                if (!classOOP.IsSmallInteger && classOOP != default)
                {
                    var classEntry = GetObjectTableEntry(classOOP);
                    if (classEntry.FormatExplicit == ObjectFormat.Pointer || classEntry.FormatExplicit == ObjectFormat.PointerIndexed)
                    {
                        className = $"class={classOOP}";
                    }
                }

                // Get info about the value we're trying to write
                if (!value.IsSmallInteger && value != default)
                {
                    var valueClassOOP = GetSlot(value, 0);
                    valueClassName = $"class={valueClassOOP}";
                }
                else if (value.IsSmallInteger)
                {
                    valueClassName = "SmallInteger";
                }
            }
            catch
            {
                // Ignore errors in diagnostic code
            }

            throw new InvalidOperationException(
                $"SetIndexedOOP called on non-PointerIndexed object!\n" +
                $"  objectOOP: {objectOOP}\n" +
                $"  format: {entry.FormatExplicit} (expected PointerIndexed)\n" +
                $"  {className}\n" +
                $"  index: {index}\n" +
                $"  value: {value}\n" +
                $"  value class: {valueClassName}\n" +
                $"  This likely means we're trying to write to a ByteIndexed object (Symbol/String/ByteArray/WordArray) indexed slots, which is not allowed.\n" +
                $"  Possible causes:\n" +
                $"    - Context object has wrong format (should be PointerIndexed)\n" +
                $"    - Wrong object being used as a context or array\n" +
                $"    - Field layout error causing wrong object to be accessed");
        }

        WriteIndexedOop(entry, index, value);
    }

    /// <summary>
    /// Gets indexed byte from ByteIndexed objects with format and bounds validation.
    /// </summary>
    public byte GetIndexedByte(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed byte from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support byte indexing");

        return ReadIndexedByte(entry, index);
    }

    /// <summary>
    /// Sets indexed byte in ByteIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed byte in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support byte indexing");

        WriteIndexedByte(entry, index, value);
    }

    /// <summary>
    /// Gets indexed word from WordIndexed objects with format and bounds validation.
    /// </summary>
    public ushort GetIndexedWord(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed word from SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support word indexing");

        return ReadIndexedWord(entry, index);
    }

    /// <summary>
    /// Sets indexed word in WordIndexed objects with format and bounds validation.
    /// </summary>
    public void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed word in SmallInteger");

        var entry = GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support word indexing");

        WriteIndexedWord(entry, index, value);
    }


    /// <summary>Helper to get instance variable count from entry.</summary>
    private int GetInstanceVariableCountFromEntry(ObjectTableEntry entry)
    {
        return entry.InstanceVariableCount;
    }

    #region Managed Storage Access Helpers

    /// <summary>
    /// Reads a slot from PointerIndexed/Pointer object storage.
    /// Slot 0 = header, slots 1+ = instance variables or indexed elements.
    /// </summary>
    private SmalltalkOOP ReadSlot(ObjectTableEntry entry, int slotIndex)
    {
        // Slot 0 is always the class reference - handle specially for ByteIndexed/WordIndexed
        if (slotIndex == 0)
        {
            if (entry.FormatExplicit == ObjectFormat.ByteIndexed || entry.FormatExplicit == ObjectFormat.WordIndexed)
                return entry.ClassOOP;
            // Fall through to OOP array access for Pointer/PointerIndexed
        }

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support OOP slot access");

        if (slotIndex < 0 || slotIndex >= oopArray.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {oopArray.Length - 1}]");

        return oopArray[slotIndex];
    }

    /// <summary>
    /// Writes a slot in PointerIndexed/Pointer object storage.
    /// </summary>
    private void WriteSlot(ObjectTableEntry entry, int slotIndex, SmalltalkOOP value)
    {
        // Slot 0 is always the class reference - handle specially for ByteIndexed/WordIndexed
        if (slotIndex == 0)
        {
            if (entry.FormatExplicit == ObjectFormat.ByteIndexed || entry.FormatExplicit == ObjectFormat.WordIndexed)
            {
                entry.ClassOOP = value;
                return;
            }
            // Fall through to OOP array access for Pointer/PointerIndexed
        }

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support OOP slot access");

        if (slotIndex < 0 || slotIndex >= oopArray.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {oopArray.Length - 1}]");

        oopArray[slotIndex] = value;
    }

    /// <summary>
    /// Reads indexed OOP from PointerIndexed object (skips header + instance variables).
    /// </summary>
    private SmalltalkOOP ReadIndexedOop(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.PointerIndexed,
            $"Expected PointerIndexed format, got {entry.FormatExplicit}");

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"PointerIndexed object has no OOP array storage");

        // Calculate offset: header (1) + instance variables
        int offset = 1 + entry.InstanceVariableCountExplicit;
        int indexedSize = oopArray.Length - offset;

        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}]");

        return oopArray[offset + index];
    }

    /// <summary>
    /// Writes indexed OOP to PointerIndexed object.
    /// </summary>
    private void WriteIndexedOop(ObjectTableEntry entry, int index, SmalltalkOOP value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.PointerIndexed,
            $"Expected PointerIndexed format, got {entry.FormatExplicit}");

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"PointerIndexed object has no OOP array storage");

        // Calculate offset: header (1) + instance variables
        int offset = 1 + entry.InstanceVariableCountExplicit;
        int indexedSize = oopArray.Length - offset;

        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}]");

        oopArray[offset + index] = value;
    }

    /// <summary>
    /// Reads indexed byte from ByteIndexed object.
    /// </summary>
    private byte ReadIndexedByte(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.ByteIndexed,
            $"Expected ByteIndexed format, got {entry.FormatExplicit}");

        var byteArray = entry.AsByteArray();
        if (byteArray == null)
            throw new InvalidOperationException($"ByteIndexed object has no byte array storage");

        if (index < 0 || index >= byteArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {byteArray.Length - 1}]");

        return byteArray[index];
    }

    /// <summary>
    /// Writes indexed byte to ByteIndexed object.
    /// </summary>
    private void WriteIndexedByte(ObjectTableEntry entry, int index, byte value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.ByteIndexed,
            $"Expected ByteIndexed format, got {entry.FormatExplicit}");

        var byteArray = entry.AsByteArray();
        if (byteArray == null)
            throw new InvalidOperationException($"ByteIndexed object has no byte array storage");

        if (index < 0 || index >= byteArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {byteArray.Length - 1}]");

        byteArray[index] = value;
    }

    /// <summary>
    /// Reads indexed word from WordIndexed object.
    /// </summary>
    private ushort ReadIndexedWord(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.WordIndexed,
            $"Expected WordIndexed format, got {entry.FormatExplicit}");

        var wordArray = entry.AsWordArray();
        if (wordArray == null)
            throw new InvalidOperationException($"WordIndexed object has no word array storage");

        if (index < 0 || index >= wordArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {wordArray.Length - 1}]");

        return wordArray[index];
    }

    /// <summary>
    /// Writes indexed word to WordIndexed object.
    /// </summary>
    private void WriteIndexedWord(ObjectTableEntry entry, int index, ushort value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.WordIndexed,
            $"Expected WordIndexed format, got {entry.FormatExplicit}");

        var wordArray = entry.AsWordArray();
        if (wordArray == null)
            throw new InvalidOperationException($"WordIndexed object has no word array storage");

        if (index < 0 || index >= wordArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {wordArray.Length - 1}]");

        wordArray[index] = value;
    }

    #endregion

    /// <summary>
    /// Gets the OOP stored in slot 0 (header).
    /// DEPRECATED: Use GetSlot(objectOOP, 0) instead for semantic-agnostic access.
    /// </summary>
    public SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("SmallIntegers don't have header slot");

        var entry = GetObjectTableEntry(objectOOP);
        var classOOP = ReadSlot(entry, 0); // Slot 0 is always the class reference
        return classOOP;
    }

    /// <summary>
    /// Sets the class reference of an object (slot 0).
    /// Used by primitives that create instances of dynamically determined classes.
    /// </summary>
    public void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
    {
        SetSlot(objectOOP, 0, classOOP);
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
                    // Use canonical mapping to create OOP
                    int objectId = blockIndex * ObjectTableBlockSize + i + 1;
                    unreachableObjects.Add(SmalltalkOOP.FromObjectTableIndex(objectId));
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
                    collectedByFormat[entry.FormatExplicit] = collectedByFormat.GetValueOrDefault(entry.FormatExplicit) + 1;
                    estimatedBytesFreed += entry.SizeInWordsExplicit * 4;
                    objectsCollected++;

                    // Track collected object IDs for debugging (use canonical mapping)
                    int objectId = blockIndex * ObjectTableBlockSize + i + 1;
                    if (firstCollectedIds.Count < 20)
                    {
                        firstCollectedIds.Add(objectId);
                    }

                    // Clear managed storage (allows .NET GC to reclaim memory)
                    entry.ManagedStorage = null;

                    // Push freed index back to free list for reuse
                    _freeObjectTableIndices.Push(objectId);

                    // Free the ObjectTable entry
                    block.Entries[i] = null;
                    block.ActiveCount--;
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
        if (objectOOP.IsSmallInteger)
            return;

        if (!IsValidObjectOOP(objectOOP))
            return;

        var entry = GetObjectTableEntry(objectOOP);
        if (entry.IsMarked)
            return;

        entry.IsMarked = true;

        // Traverse pointer fields based on format
        if (entry.FormatExplicit == ObjectFormat.Pointer || entry.FormatExplicit == ObjectFormat.PointerIndexed)
        {
            var oopArray = entry.AsOopArray();
            if (oopArray != null)
            {
                // Mark all OOPs in the array (header, instance vars, indexed elements)
                foreach (var childOOP in oopArray)
                {
                    MarkObjectRecursively(childOOP);
                }
            }
        }

        // ByteIndexed and WordIndexed objects contain no OOPs, skip traversal
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

        // Use canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlockSize;

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

            bool isValid = IsValidObjectOOP(objectOOP);
            if (!isValid)
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

                // Use canonical mapping: objectId = blockIdx * BlockSize + i + 1
                int objectId = blockIdx * ObjectTableBlockSize + i + 1;
                var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
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
                var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
                var entry = GetObjectTableEntry(objectOOP);
                var classOOP = GetSlot(objectOOP, 0);

                // Try to find localId for this object
                string localId = localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectId).Key ?? "unknown";

                // Get some info about the object's contents
                int instVarCount = GetInstanceVariableCountFromEntry(entry);
                int indexedSize = GetIndexedSizeFromEntry(entry);
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

    /// <summary>
    /// Enumerates all valid objects in the object table in canonical order.
    /// Yields only non-null entries, skipping freed slots.
    /// </summary>
    public IEnumerable<SmalltalkOOP> EnumerateObjectTable()
    {
        for (int blockIndex = 0; blockIndex < _objectTableBlocks.Count; blockIndex++)
        {
            var block = _objectTableBlocks[blockIndex];
            for (int i = 0; i < ObjectTableBlockSize; i++)
            {
                if (block.Entries[i] != null)
                {
                    // Use canonical mapping: objectId = blockIndex * BlockSize + i + 1
                    int objectId = blockIndex * ObjectTableBlockSize + i + 1;
                    yield return SmalltalkOOP.FromObjectTableIndex(objectId);
                }
            }
        }
    }
}