using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Core heap interface for Smalltalk-80 object allocation and access.
/// Provides low-level memory management without semantic knowledge of Smalltalk classes.
/// </summary>
public interface ISmalltalkHeap
{
    // ========================================
    // Nil Singleton
    // ========================================

    /// <summary>
    /// Gets the OOP for the Nil singleton.
    /// </summary>
    SmalltalkOOP GetNil();

    // ========================================
    // Object Allocation
    // ========================================

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
    /// Allocates a new instance of the given class by reading its format and layout from the heap.
    /// This is the proper way for primitives to allocate objects - uses only heap data.
    /// Reads the class's format field (instance variable 2 in Behavior) to determine object layout.
    /// Sets the new instance's class reference (slot 0) to classOOP.
    /// </summary>
    /// <param name="classOOP">Class object pointer (receiver of 'new' or 'new:' message)</param>
    /// <param name="indexedSize">Number of indexed elements (0 for 'new', n for 'new: n')</param>
    /// <returns>Tagged OOP referencing new instance</returns>
    SmalltalkOOP AllocateInstanceOf(SmalltalkOOP classOOP, int indexedSize);

    // ========================================
    // Class Pointer Access (Slot 0)
    // ========================================

    /// <summary>
    /// Gets the class OOP stored in slot 0 (header).
    /// </summary>
    SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP);

    /// <summary>
    /// Sets the class reference of an object (slot 0).
    /// Used by primitives that create instances of dynamically determined classes.
    /// </summary>
    /// <param name="objectOOP">Object to modify</param>
    /// <param name="classOOP">Class OOP to set</param>
    void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP);

    // ========================================
    // Instance Variable Access (Always OOPs)
    // ========================================

    /// <summary>
    /// Gets an instance variable by index. Instance variables are always OOPs.
    /// Validates index is within range [0, instanceVarCount-1].
    /// </summary>
    SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Sets an instance variable by index. Instance variables are always OOPs.
    /// Validates index is within range [0, instanceVarCount-1].
    /// Uses write barrier for runtime safety.
    /// </summary>
    void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    /// <summary>
    /// Sets an instance variable with write barrier (for runtime use).
    /// Use this for all heap mutations during runtime execution.
    /// Ensures GC sees the reference update for concurrent/incremental collection.
    /// </summary>
    void SetInstanceVariableWithBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    /// <summary>
    /// Sets an instance variable without write barrier (for loader-only use).
    /// DANGER: Only use during single-threaded loader phases before GC is enabled.
    /// Runtime code MUST use SetInstanceVariableWithBarrier.
    /// </summary>
    void SetInstanceVariableNoBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    // ========================================
    // Format-Specific Indexed Access
    // ========================================

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

    // ========================================
    // Float Optimization (Zero-Allocation IEEE 754)
    // ========================================

    /// <summary>
    /// Reads a Float object's 4 words directly as a C# double.
    /// OPTIMIZATION: Single method call instead of 4 GetIndexedWord calls.
    /// Reads words 0-3 as little-endian IEEE 754 double-precision value.
    /// </summary>
    /// <param name="floatOOP">Float object (must be WordIndexed with exactly 4 words)</param>
    /// <returns>IEEE 754 double-precision value</returns>
    double GetFloatAsDouble(SmalltalkOOP floatOOP);

    /// <summary>
    /// Writes a C# double directly into a Float object's 4 words.
    /// OPTIMIZATION: Single method call instead of 4 SetIndexedWord calls.
    /// Writes IEEE 754 double-precision value as words 0-3 in little-endian format.
    /// </summary>
    /// <param name="floatOOP">Float object (must be WordIndexed with exactly 4 words)</param>
    /// <param name="value">IEEE 754 double-precision value to write</param>
    void SetFloatFromDouble(SmalltalkOOP floatOOP, double value);

    /// <summary>
    /// Bulk copy indexed bytes from heap object to destination span.
    /// Much faster than element-by-element access for large bitmaps.
    /// </summary>
    /// <param name="objectOOP">ByteIndexed object to read from</param>
    /// <param name="destination">Destination span to copy bytes into</param>
    /// <param name="startIndex">Starting index in source object (default 0)</param>
    void CopyIndexedBytes(SmalltalkOOP objectOOP, Span<byte> destination, int startIndex = 0);

    /// <summary>
    /// Bulk copy indexed words from heap object to destination span as big-endian bytes.
    /// Each word (16-bit) is stored as 2 bytes in big-endian format.
    /// Much faster than element-by-element access for large bitmaps.
    /// </summary>
    /// <param name="objectOOP">WordIndexed object to read from</param>
    /// <param name="destination">Destination span to copy bytes into</param>
    /// <param name="startIndex">Starting index in source object (default 0)</param>
    void CopyIndexedWords(SmalltalkOOP objectOOP, Span<byte> destination, int startIndex = 0);

    /// <summary>
    /// Bulk copy bytes from source span to indexed bytes in heap object.
    /// Much faster than element-by-element access for large bitmaps.
    /// </summary>
    /// <param name="objectOOP">ByteIndexed object to write to</param>
    /// <param name="source">Source span to copy bytes from</param>
    /// <param name="startIndex">Starting index in destination object (default 0)</param>
    void SetIndexedBytes(SmalltalkOOP objectOOP, ReadOnlySpan<byte> source, int startIndex = 0);

    /// <summary>
    /// Bulk copy bytes to indexed words in heap object, converting from big-endian format.
    /// Each 2 bytes in source represents one 16-bit word in big-endian format.
    /// Much faster than element-by-element access for large bitmaps.
    /// </summary>
    /// <param name="objectOOP">WordIndexed object to write to</param>
    /// <param name="source">Source span containing big-endian bytes</param>
    /// <param name="startIndex">Starting index in destination object (default 0)</param>
    void SetIndexedWords(SmalltalkOOP objectOOP, ReadOnlySpan<byte> source, int startIndex = 0);

    // ========================================
    // Object Metadata Access
    // ========================================

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

    // ========================================
    // ObjectTable Management
    // ========================================

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

    /// <summary>
    /// Validates that an OOP refers to a valid object in the heap.
    /// Used by primitive 76 (SmallInteger asObject) to verify object existence.
    /// </summary>
    bool IsValidOOP(SmalltalkOOP oop);

    /// <summary>
    /// Enumerates all valid objects in the object table in canonical order.
    /// Yields only non-null entries, skipping freed slots.
    /// Useful for VM primitives like someInstance, nextObject, allInstances.
    /// </summary>
    IEnumerable<SmalltalkOOP> EnumerateObjectTable();

    // ========================================
    // Instance Finding (Primitives)
    // ========================================

    /// <summary>
    /// Finds the first instance of a given class.
    /// Used by primitive 77 (Behavior someInstance).
    /// Returns nil if no instances exist.
    /// </summary>
    SmalltalkOOP FindInstanceOf(SmalltalkOOP classOOP);

    /// <summary>
    /// Finds the next instance of the same class as the given object.
    /// Used by primitive 78 (Object nextInstance).
    /// Returns nil if the object is the last instance of its class.
    /// </summary>
    SmalltalkOOP FindNextInstanceOf(SmalltalkOOP classOOP, SmalltalkOOP afterOOP);

    // ========================================
    // Debugging and Diagnostics
    // ========================================

    /// <summary>
    /// Gets the class name of an object for diagnostic purposes.
    /// Returns descriptive error strings for invalid objects.
    /// </summary>
    string GetClassName(SmalltalkOOP objectOOP);

    /// <summary>
    /// Configures the instance variable index for the class name field.
    /// Must be called after bundle loading to enable GetClassName().
    /// </summary>
    void SetClassNameFieldIndex(int index);

    /// <summary>
    /// Validates all objects have valid class references.
    /// Reports objects with invalid class OOPs for diagnostic purposes.
    /// </summary>
    void ValidateAllObjectClasses(Dictionary<string, int> localIdToObjectId);

    /// <summary>
    /// Gets or sets verbose debug mode for allocation/primitive diagnostics.
    /// When false, suppresses noisy debug output from successful operations.
    /// </summary>
    bool VerboseDebugMode { get; set; }

    // ========================================
    // Subsystem Access
    // ========================================

    /// <summary>
    /// Gets the garbage collector subsystem for root management and collection.
    /// </summary>
    IGarbageCollector GC { get; }

    /// <summary>
    /// Gets the become handler subsystem for object identity swapping.
    /// </summary>
    IBecomeHandler Become { get; }

    /// <summary>
    /// Controls whether allocation-triggered GC is enabled.
    /// Should be disabled during cold start when objects are being built but not yet rooted.
    /// Default is false (disabled). Enable after cold start completes and GC roots are established.
    /// </summary>
    bool AutoGCEnabled { get; set; }
}
