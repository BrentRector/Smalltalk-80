using System.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Implementation of ISmalltalkHeap providing low-level Smalltalk object management.
/// Uses tagged OOPs for SmallIntegers and ObjectTable indirection for heap objects.
/// Delegates to specialized components for GC, table management, and become operations.
/// </summary>
public class SmalltalkHeapManager : ISmalltalkHeap, ISmalltalkHeapManager
{
    private readonly IObjectTableManager _tableManager;
    private readonly IGarbageCollector _garbageCollector;
    private readonly IBecomeHandler _becomeHandler;
    private readonly SmalltalkOOP _nilOOP;

    // Class name field index (determined from coldstart.json during initialization)
    private int _classNameFieldIndex = -1;

    // DEBUG: Runtime allocation counter
    private static int _runtimeAllocationCount = 0;

    // Verbose debug mode control
    public bool VerboseDebugMode { get; set; } = false;

    public SmalltalkHeapManager()
    {
        _tableManager = new ObjectTableManager();
        _garbageCollector = new GarbageCollector(_tableManager, this);
        _becomeHandler = new BecomeHandler(_tableManager);

        // Allocate Nil as first object (reserves first ObjectTable slot for Nil)
        _nilOOP = AllocateObject(
            format: ObjectFormat.Pointer,
            fixedSlots: 0,
            indexedSize: 0
        );

        // Set class pointer to self-reference (fixed later by become)
        var nilEntry = _tableManager.GetObjectTableEntry(_nilOOP);
        WriteSlot(nilEntry, 0, _nilOOP);
    }

    /// <summary>
    /// Gets the OOP for the Nil singleton.
    /// </summary>
    public SmalltalkOOP GetNil() => _nilOOP;

    /// <summary>
    /// Gets the garbage collector subsystem for root management and collection.
    /// </summary>
    public IGarbageCollector GC => _garbageCollector;

    /// <summary>
    /// Gets the become handler subsystem for object identity swapping.
    /// </summary>
    public IBecomeHandler Become => _becomeHandler;

    /// <summary>
    /// Configures the instance variable index for the class name field.
    /// This must be called after bundle is loaded to enable GetClassName() functionality.
    /// </summary>
    public void SetClassNameFieldIndex(int index)
    {
        _classNameFieldIndex = index;
    }

    #region Object Allocation

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
        int tableIndex = _tableManager.AllocateObjectTableIndex();

        // CRITICAL: Initialize all OOP slots to nil (Blue Book requirement)
        // Skip initialization for the first allocation (Nil itself)
        if (tableIndex > 0 && managedStorage is SmalltalkOOP[] oopArray)
        {
            for (int i = 1; i < oopArray.Length; i++) // Skip slot 0 (class pointer)
            {
                oopArray[i] = _nilOOP;
            }
        }

        // Create ObjectTableEntry with managed storage and explicit metadata
        int? originalIndexedSize = (format == ObjectFormat.ByteIndexed) ? indexedSize : null;
        var entry = new ObjectTableEntry(
            managedStorage: managedStorage,
            format: format,
            sizeInWords: totalWords,
            instanceVariableCount: fixedSlots,
            originalIndexedSize: originalIndexedSize
        );

        // For ByteIndexed/WordIndexed objects with instance variables, allocate separate storage
        // These formats store indexed data in ManagedStorage (byte[]/ushort[]) but also need
        // OOP storage for instance variables (e.g., Form has bits, width, height, offset)
        if ((format == ObjectFormat.ByteIndexed || format == ObjectFormat.WordIndexed) && fixedSlots > 0)
        {
            entry.InstanceVariableStorage = new SmalltalkOOP[fixedSlots];
            // Initialize to nil (skip for first allocation - Nil itself)
            if (tableIndex > 0)
            {
                for (int i = 0; i < fixedSlots; i++)
                {
                    entry.InstanceVariableStorage[i] = _nilOOP;
                }
            }
        }

        // Store entry in ObjectTable
        _tableManager.SetObjectTableEntry(tableIndex, entry);

        // Return tagged OOP
        return SmalltalkOOP.FromObjectTableIndex(tableIndex);
    }

    public SmalltalkOOP AllocateInstanceOf(SmalltalkOOP classOOP, int indexedSize)
    {
        // Receiver must be a valid class object (not SmallInteger)
        if (classOOP.IsSmallInteger)
            throw new ArgumentException("Cannot allocate instance of SmallInteger", nameof(classOOP));

        // Read the class's format field (instance variable 2 in Behavior hierarchy)
        SmalltalkOOP formatOOP = GetInstanceVariable(classOOP, 2);

        if (!formatOOP.IsSmallInteger)
            throw new InvalidOperationException($"Class format field must be a SmallInteger, got {formatOOP}");

        // Decode format using the FormatWord semantic wrapper
        var formatWord = new FormatWord(formatOOP.AsSmallInteger());
        int instanceVariableCount = formatWord.FixedFields;

        // Convert to ObjectFormat enum
        ObjectFormat format;
        if (formatWord.IsPointers)
        {
            format = formatWord.IsIndexable ? ObjectFormat.PointerIndexed : ObjectFormat.Pointer;
        }
        else if (formatWord.IsIndexable)
        {
            format = formatWord.IsWords ? ObjectFormat.WordIndexed : ObjectFormat.ByteIndexed;
        }
        else
        {
            // Non-pointer, non-indexable (shouldn't occur in practice)
            format = ObjectFormat.Pointer;
        }

        // Allocate the object with the decoded format
        SmalltalkOOP newInstanceOOP = AllocateObject(format, instanceVariableCount, indexedSize);

        // Set the new instance's class reference (slot 0) to the class
        SetClassOOP(newInstanceOOP, classOOP);

        // Track runtime allocations for debugging
        System.Threading.Interlocked.Increment(ref _runtimeAllocationCount);

        // Note: AllocateObject already initializes all instance variables and indexed slots to nil

        return newInstanceOOP;
    }

    /// <summary>Calculates words needed for indexed data based on format.</summary>
    private static int CalculateIndexedWords(ObjectFormat format, int indexedSize)
    {
        return format switch
        {
            ObjectFormat.Pointer => 0,
            ObjectFormat.PointerIndexed => indexedSize,
            ObjectFormat.ByteIndexed => (indexedSize + 3) / 4,
            ObjectFormat.WordIndexed => (indexedSize + 1) / 2,
            _ => throw new ArgumentException($"Unknown object format: {format}")
        };
    }

    #endregion

    #region Slot Access (Internal Use Only)

    /// <summary>
    /// INTERNAL: Raw slot access - gets OOP from any slot (semantic-agnostic).
    /// External code should use GetClassOOP/GetInstanceVariable/GetIndexedOOP instead.
    /// </summary>
    private SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get slot from SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return ReadSlot(entry, slotIndex);
    }

    /// <summary>
    /// INTERNAL: Raw slot access - sets OOP in any slot (semantic-agnostic).
    /// External code should use SetClassOOP/SetInstanceVariable/SetIndexedOOP instead.
    /// </summary>
    private void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set slot in SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        WriteSlot(entry, slotIndex, value);
    }

    public SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get instance variable of SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        return ReadSlot(entry, 1 + index);
    }

    public void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        SetInstanceVariableWithBarrier(objectOOP, index, value);
    }

    public void SetInstanceVariableWithBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set instance variable of SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        WriteSlot(entry, 1 + index, value);
        // Write barrier: Future implementation for incremental/concurrent GC
    }

    public void SetInstanceVariableNoBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set instance variable of SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        int instanceVarCount = entry.InstanceVariableCountExplicit;

        if (index < 0 || index >= instanceVarCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range [0, {instanceVarCount - 1}] for object {objectOOP}");

        WriteSlot(entry, 1 + index, value);
    }

    #endregion

    #region Indexed Access

    public SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed OOP from SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.PointerIndexed)
        {
            var classOOP = GetClassOOP(objectOOP);
            throw new InvalidOperationException(
                $"GetIndexedOOP called on non-PointerIndexed object!\n" +
                $"  objectOOP: {objectOOP}, format: {entry.FormatExplicit}, class={classOOP}, index: {index}");
        }

        return ReadIndexedOop(entry, index);
    }

    public void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed OOP in SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.PointerIndexed)
        {
            var classOOP = GetClassOOP(objectOOP);
            throw new InvalidOperationException(
                $"SetIndexedOOP called on non-PointerIndexed object!\n" +
                $"  objectOOP: {objectOOP}, format: {entry.FormatExplicit}, class={classOOP}, index: {index}");
        }

        WriteIndexedOop(entry, index, value);
    }

    public byte GetIndexedByte(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed byte from SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support byte indexing");

        return ReadIndexedByte(entry, index);
    }

    public void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed byte in SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.ByteIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support byte indexing");

        WriteIndexedByte(entry, index, value);
    }

    public ushort GetIndexedWord(SmalltalkOOP objectOOP, int index)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot get indexed word from SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support word indexing");

        return ReadIndexedWord(entry, index);
    }

    public void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set indexed word in SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);

        if (entry.FormatExplicit != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support word indexing");

        WriteIndexedWord(entry, index, value);
    }

    #endregion

    #region Managed Storage Access Helpers

    private SmalltalkOOP ReadSlot(ObjectTableEntry entry, int slotIndex)
    {
        // For ByteIndexed/WordIndexed objects, handle specially
        if (entry.FormatExplicit == ObjectFormat.ByteIndexed || entry.FormatExplicit == ObjectFormat.WordIndexed)
        {
            if (slotIndex == 0)
            {
                return entry.ClassOOP;
            }

            // Instance variables stored in InstanceVariableStorage
            if (entry.InstanceVariableStorage != null)
            {
                int instVarIndex = slotIndex - 1;
                if (instVarIndex < 0 || instVarIndex >= entry.InstanceVariableStorage.Length)
                    throw new ArgumentOutOfRangeException(nameof(slotIndex),
                        $"Instance variable index {instVarIndex} out of range [0, {entry.InstanceVariableStorage.Length - 1}]");
                return entry.InstanceVariableStorage[instVarIndex];
            }
            else
            {
                throw new InvalidOperationException(
                    $"ByteIndexed/WordIndexed object has no instance variable storage for slot {slotIndex}");
            }
        }

        // For Pointer/PointerIndexed, use OOP array
        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support OOP slot access");

        if (slotIndex < 0 || slotIndex >= oopArray.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {oopArray.Length - 1}]");

        return oopArray[slotIndex];
    }

    private void WriteSlot(ObjectTableEntry entry, int slotIndex, SmalltalkOOP value)
    {
        // For ByteIndexed/WordIndexed objects, handle specially
        if (entry.FormatExplicit == ObjectFormat.ByteIndexed || entry.FormatExplicit == ObjectFormat.WordIndexed)
        {
            if (slotIndex == 0)
            {
                entry.ClassOOP = value;
                return;
            }

            // Instance variables stored in InstanceVariableStorage
            if (entry.InstanceVariableStorage != null)
            {
                int instVarIndex = slotIndex - 1;
                if (instVarIndex < 0 || instVarIndex >= entry.InstanceVariableStorage.Length)
                    throw new ArgumentOutOfRangeException(nameof(slotIndex),
                        $"Instance variable index {instVarIndex} out of range [0, {entry.InstanceVariableStorage.Length - 1}]");
                entry.InstanceVariableStorage[instVarIndex] = value;
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"ByteIndexed/WordIndexed object has no instance variable storage for slot {slotIndex}");
            }
        }

        // For Pointer/PointerIndexed, use OOP array
        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"Object format {entry.FormatExplicit} does not support OOP slot access");

        if (slotIndex < 0 || slotIndex >= oopArray.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} out of range [0, {oopArray.Length - 1}]");

        oopArray[slotIndex] = value;
    }

    private SmalltalkOOP ReadIndexedOop(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.PointerIndexed);

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"PointerIndexed object has no OOP array storage");

        int offset = 1 + entry.InstanceVariableCountExplicit;
        int indexedSize = oopArray.Length - offset;

        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}]");

        return oopArray[offset + index];
    }

    private void WriteIndexedOop(ObjectTableEntry entry, int index, SmalltalkOOP value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.PointerIndexed);

        var oopArray = entry.AsOopArray();
        if (oopArray == null)
            throw new InvalidOperationException($"PointerIndexed object has no OOP array storage");

        int offset = 1 + entry.InstanceVariableCountExplicit;
        int indexedSize = oopArray.Length - offset;

        if (index < 0 || index >= indexedSize)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed OOP index {index} out of range [0, {indexedSize - 1}]");

        oopArray[offset + index] = value;
    }

    private byte ReadIndexedByte(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.ByteIndexed);

        var byteArray = entry.AsByteArray();
        if (byteArray == null)
            throw new InvalidOperationException($"ByteIndexed object has no byte array storage");

        if (index < 0 || index >= byteArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {byteArray.Length - 1}]");

        return byteArray[index];
    }

    private void WriteIndexedByte(ObjectTableEntry entry, int index, byte value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.ByteIndexed);

        var byteArray = entry.AsByteArray();
        if (byteArray == null)
            throw new InvalidOperationException($"ByteIndexed object has no byte array storage");

        if (index < 0 || index >= byteArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed byte index {index} out of range [0, {byteArray.Length - 1}]");

        byteArray[index] = value;
    }

    private ushort ReadIndexedWord(ObjectTableEntry entry, int index)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.WordIndexed);

        var wordArray = entry.AsWordArray();
        if (wordArray == null)
            throw new InvalidOperationException($"WordIndexed object has no word array storage");

        if (index < 0 || index >= wordArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {wordArray.Length - 1}]");

        return wordArray[index];
    }

    private void WriteIndexedWord(ObjectTableEntry entry, int index, ushort value)
    {
        Debug.Assert(entry.FormatExplicit == ObjectFormat.WordIndexed);

        var wordArray = entry.AsWordArray();
        if (wordArray == null)
            throw new InvalidOperationException($"WordIndexed object has no word array storage");

        if (index < 0 || index >= wordArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Indexed word index {index} out of range [0, {wordArray.Length - 1}]");

        wordArray[index] = value;
    }

    #endregion

    #region Object Metadata

    public SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("SmallIntegers don't have header slot");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return ReadSlot(entry, 0);
    }

    public void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("Cannot set class of SmallInteger");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        WriteSlot(entry, 0, classOOP);
    }

    public ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            throw new ArgumentException("SmallIntegers don't have object format");

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return entry.Format;
    }

    public int GetObjectSizeInWords(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0;

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return entry.SizeInWords;
    }

    public int GetInstanceVariableCount(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0;

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return entry.InstanceVariableCount;
    }

    public int GetIndexedSize(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0;

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        return GetIndexedSizeFromEntry(entry);
    }

    private int GetIndexedSizeFromEntry(ObjectTableEntry entry)
    {
        int totalWords = entry.SizeInWords;
        int headerWords = 1;
        int instanceVarCount = entry.InstanceVariableCount;
        int indexedWords = totalWords - headerWords - instanceVarCount;
        if (indexedWords < 0) indexedWords = 0;

        return entry.Format switch
        {
            ObjectFormat.PointerIndexed => indexedWords,
            ObjectFormat.ByteIndexed => entry.OriginalIndexedSize ?? (indexedWords * 4),
            // For WordIndexed, return actual array length to avoid rounding errors
            ObjectFormat.WordIndexed => entry.AsWordArray()?.Length ?? (indexedWords * 2),
            _ => 0
        };
    }

    #endregion

    #region GC Delegation (ISmalltalkHeapManager compatibility)

    // Legacy methods for ISmalltalkHeapManager compatibility
    // New code should use heap.GC.* instead
    public void SetGCRoots(SmalltalkOOP[] rootObjects)
    {
        _garbageCollector.ClearGCRoots();
        if (rootObjects != null)
        {
            foreach (var oop in rootObjects)
                _garbageCollector.AddGCRoot(oop);
        }
    }

    public void AddGCRoots(SmalltalkOOP[] additionalRoots)
    {
        if (additionalRoots != null)
        {
            foreach (var oop in additionalRoots)
                _garbageCollector.AddGCRoot(oop);
        }
    }

    public SmalltalkOOP[] GetGCRoots() => _garbageCollector.GetGCRoots();
    public SmalltalkOOP[] GetUnreachableObjects() => _garbageCollector.GetUnreachableObjects();
    public GCStatistics GarbageCollect() => _garbageCollector.GarbageCollect();
    public GCStatistics GetGCStatistics() => _garbageCollector.GetStatistics();

    #endregion

    #region Table Management Delegation

    public int GetObjectTableSize() => _tableManager.GetObjectTableSize();
    public int GetActiveObjectCount() => _tableManager.GetActiveObjectCount();
    public bool IsValidObjectOOP(SmalltalkOOP objectOOP) => _tableManager.IsValidObjectOOP(objectOOP);
    public bool IsValidOOP(SmalltalkOOP oop) => _tableManager.IsValidOOP(oop);
    public IEnumerable<SmalltalkOOP> EnumerateObjectTable() => _tableManager.EnumerateObjectTable();

    #endregion

    #region Become Delegation

    public void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2) => _becomeHandler.BecomeTwoWay(oop1, oop2);

    #endregion

    #region Instance Finding

    public SmalltalkOOP FindInstanceOf(SmalltalkOOP classOOP)
    {
        foreach (var oop in EnumerateObjectTable())
        {
            if (oop.IsSmallInteger)
                continue;

            var objectClass = GetClassOOP(oop);
            if (objectClass == classOOP)
                return oop;
        }

        return _nilOOP;
    }

    public SmalltalkOOP FindNextInstanceOf(SmalltalkOOP classOOP, SmalltalkOOP afterOOP)
    {
        bool foundCurrent = false;

        foreach (var oop in EnumerateObjectTable())
        {
            if (oop.IsSmallInteger)
                continue;

            if (!foundCurrent)
            {
                if (oop == afterOOP)
                    foundCurrent = true;
                continue;
            }

            var objectClass = GetClassOOP(oop);
            if (objectClass == classOOP)
                return oop;
        }

        return _nilOOP;
    }

    #endregion

    #region Class Name Utilities

    public string GetClassName(SmalltalkOOP objectOOP)
    {
        try
        {
            if (objectOOP.IsSmallInteger)
                return "SmallInteger";

            if (!IsValidObjectOOP(objectOOP))
                return "Invalid";

            var classOOP = GetClassOOP(objectOOP);
            if (classOOP.IsSmallInteger)
                return "InvalidClass(SmallInt)";

            if (!IsValidObjectOOP(classOOP))
                return $"InvalidClass(OOP={classOOP.RawValue})";

            var classEntry = _tableManager.GetObjectTableEntry(classOOP);
            int instVarCount = classEntry.InstanceVariableCount;

            // If class name field index is configured, try to extract the class name
            if (_classNameFieldIndex >= 0 && instVarCount > _classNameFieldIndex)
            {
                var classNameSymbolOOP = GetInstanceVariable(classOOP, _classNameFieldIndex);

                if (!classNameSymbolOOP.IsSmallInteger && IsValidObjectOOP(classNameSymbolOOP))
                {
                    var symbolEntry = _tableManager.GetObjectTableEntry(classNameSymbolOOP);

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

            // For metaclass instances, follow thisClass pointer
            const int METACLASS_THISCLASS_INDEX = 6;
            if (instVarCount > METACLASS_THISCLASS_INDEX)
            {
                var thisClassOOP = GetInstanceVariable(classOOP, METACLASS_THISCLASS_INDEX);
                if (!thisClassOOP.IsSmallInteger && IsValidObjectOOP(thisClassOOP))
                {
                    var thisClassEntry = _tableManager.GetObjectTableEntry(thisClassOOP);
                    int thisClassInstVarCount = thisClassEntry.InstanceVariableCount;
                    if (_classNameFieldIndex >= 0 && thisClassInstVarCount > _classNameFieldIndex)
                    {
                        var nameSymbolOOP = GetInstanceVariable(thisClassOOP, _classNameFieldIndex);
                        if (!nameSymbolOOP.IsSmallInteger && IsValidObjectOOP(nameSymbolOOP))
                        {
                            var symbolEntry = _tableManager.GetObjectTableEntry(nameSymbolOOP);
                            if (symbolEntry.Format == ObjectFormat.ByteIndexed)
                            {
                                int symbolSize = GetIndexedSizeFromEntry(symbolEntry);
                                if (symbolSize > 0)
                                {
                                    var symbolBytes = new byte[symbolSize];
                                    for (int i = 0; i < symbolSize; i++)
                                    {
                                        symbolBytes[i] = GetIndexedByte(nameSymbolOOP, i);
                                    }
                                    string baseName = System.Text.Encoding.UTF8.GetString(symbolBytes);
                                    return $"{baseName} class";
                                }
                            }
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

        foreach (var objectOOP in EnumerateObjectTable())
        {
            int objectId = objectOOP.ToObjectTableIndex();
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

        if (invalidCount > 0)
        {
            Console.WriteLine($"⚠️  VALIDATION WARNING: Found {invalidCount} objects with invalid class references:");
            foreach (var (objectId, className) in invalidObjects)
            {
                var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
                var entry = _tableManager.GetObjectTableEntry(objectOOP);
                var classOOP = GetClassOOP(objectOOP);

                string localId = localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectId).Key ?? "unknown";

                int instVarCount = entry.InstanceVariableCount;
                int indexedSize = GetIndexedSizeFromEntry(entry);

                Console.WriteLine($"  Object {objectId} (localId={localId}): class='{className}', " +
                    $"classOOP={classOOP}, format={entry.Format}, instVars={instVarCount}, indexed={indexedSize}");
            }
            if (invalidCount > 10)
            {
                Console.WriteLine($"  ... and {invalidCount - 10} more");
            }
        }
    }

    #endregion
}
