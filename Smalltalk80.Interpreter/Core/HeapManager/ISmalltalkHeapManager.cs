using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Minimal heap manager interface for Smalltalk-80 object allocation and access.
/// Provides low-level memory management without semantic knowledge of Smalltalk classes.
/// </summary>
public interface ISmalltalkHeapManager
{

    /// <summary>
    /// Allocates object with specified memory layout.
    /// Memory is zero-initialized by .NET guarantee.
    /// </summary>
    /// <param name="format">Object format for GC traversal</param>
    /// <param name="fixedSlots">Number of fixed slots (always treated as OOPs)</param>
    /// <param name="indexedSize">Size of indexed portion (format-specific)</param>
    /// <returns>Tagged OOP referencing new object</returns>
    SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize);


    /// <summary>
    /// Raw slot access - gets OOP from any slot (semantic-agnostic).
    /// </summary>
    /// <param name="objectOOP">Object to access</param>
    /// <param name="slotIndex">Zero-based slot index</param>
    /// <returns>OOP stored at slot</returns>
    SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex);

    /// <summary>
    /// Raw slot access - sets OOP in any slot (semantic-agnostic).
    /// </summary>
    /// <param name="objectOOP">Object to modify</param>
    /// <param name="slotIndex">Zero-based slot index</param>
    /// <param name="value">OOP to store at slot</param>
    void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value);

    // Instance Variable Access (always OOPs)
    
    /// <summary>
    /// Gets an instance variable by index. Instance variables are always OOPs.
    /// Validates index is within range [0, instanceVarCount-1].
    /// </summary>
    SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Sets an instance variable by index. Instance variables are always OOPs.
    /// Validates index is within range [0, instanceVarCount-1].
    /// </summary>
    void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    // Format-Specific Indexed Access (Type-Safe with Validation)

    /// <summary>
    /// Gets indexed OOP from PointerIndexed objects (Arrays, OrderedCollections).
    /// Validates format is PointerIndexed and index is within range.
    /// </summary>
    SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Sets indexed OOP in PointerIndexed objects (Arrays, OrderedCollections).
    /// Validates format is PointerIndexed and index is within range.
    /// </summary>
    void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    /// <summary>
    /// Gets indexed byte from ByteIndexed objects (Strings, ByteArrays, Floats).
    /// Validates format is ByteIndexed and index is within range.
    /// </summary>
    byte GetIndexedByte(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Sets indexed byte in ByteIndexed objects (Strings, ByteArrays, Floats).
    /// Validates format is ByteIndexed and index is within range.
    /// </summary>
    void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value);

    /// <summary>
    /// Gets indexed word from WordIndexed objects (WordArrays, Bitmaps).
    /// Validates format is WordIndexed and index is within range.
    /// </summary>
    ushort GetIndexedWord(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Sets indexed word in WordIndexed objects (WordArrays, Bitmaps).
    /// Validates format is WordIndexed and index is within range.
    /// </summary>
    void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value);


    // Object Metadata Access

    /// <summary>
    /// Gets the OOP stored in slot 0 (header).
    /// DEPRECATED: Use GetSlot(objectOOP, 0) instead for semantic-agnostic access.
    /// </summary>
    SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the object format from ObjectTableEntry.
    /// </summary>
    ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the total size of an object in words.
    /// </summary>
    int GetObjectSizeInWords(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the number of instance variables for an object.
    /// </summary>
    int GetInstanceVariableCount(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the size of the indexed portion of an object.
    /// Returns 0 if the object has no indexed portion.
    /// </summary>
    int GetIndexedSize(SmalltalkOOP objectOOP);

    // Garbage Collection and Root Management

    /// <summary>
    /// Sets the GC root objects array. The GC will start its reachability walk from these objects.
    /// This replaces any previously set root objects.
    /// </summary>
    /// <param name="rootObjects">Array of OOPs to use as GC roots</param>
    void SetGCRoots(SmalltalkOOP[] rootObjects);

    /// <summary>
    /// Adds additional OOPs to the current GC roots array.
    /// Useful for temporarily adding objects during specific operations.
    /// </summary>
    /// <param name="additionalRoots">Additional OOPs to add as roots</param>
    void AddGCRoots(SmalltalkOOP[] additionalRoots);

    /// <summary>
    /// Gets the current GC roots array (defensive copy).
    /// </summary>
    SmalltalkOOP[] GetGCRoots();

    /// <summary>
    /// Performs mark-and-sweep garbage collection using the configured root objects.
    /// Returns detailed statistics about the collection.
    /// </summary>
    GCStatistics GarbageCollect();

    /// <summary>
    /// DEBUG: Performs GC marking phase only and returns unreachable objects without collecting them.
    /// Returns array of OOPs that would be collected during a full GC.
    /// </summary>
    SmalltalkOOP[] GetUnreachableObjects();

    /// <summary>
    /// Gets current GC statistics without performing collection.
    /// </summary>
    GCStatistics GetGCStatistics();

    // ObjectTable Management

    /// <summary>
    /// Gets the total number of ObjectTable entries allocated.
    /// </summary>
    int GetObjectTableSize();

    /// <summary>
    /// Gets the number of active (non-null) objects in the ObjectTable.
    /// </summary>
    int GetActiveObjectCount();

    /// <summary>
    /// Validates that an OOP refers to a valid object.
    /// </summary>
    bool IsValidObjectOOP(SmalltalkOOP objectOOP);
}