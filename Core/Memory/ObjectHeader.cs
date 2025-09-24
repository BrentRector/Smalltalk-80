namespace Core.Memory;

/// <summary>
/// Represents an object header in the Smalltalk-80 object memory.
/// Contains class reference, size information, and various flags.
/// </summary>
public readonly record struct ObjectHeader
{
    /// <summary>
    /// The raw header value containing all encoded information.
    /// </summary>
    public uint HeaderWord { get; init; }

    /// <summary>
    /// Creates a new object header with the specified class and size.
    /// </summary>
    /// <param name="classOop">The OOP of the object's class.</param>
    /// <param name="size">The size of the object in words.</param>
    /// <param name="isPointers">Whether the object contains pointers.</param>
    /// <param name="isWords">Whether the object contains words (non-pointer data).</param>
    /// <param name="isFree">Whether this object is free.</param>
    public ObjectHeader(OOP classOop, ushort size, bool isPointers = true, bool isWords = false, bool isFree = false)
    {
        // Encode header according to Blue Book specification
        // Format: [Class OOP: 16 bits][Size: 12 bits][Flags: 4 bits]
        var flags = 0u;
        if (isPointers) flags |= PointersFlag;
        if (isWords) flags |= WordsFlag;
        if (isFree) flags |= FreeFlag;

        HeaderWord = ((uint)(classOop.Value & 0xFFFF) << 16) | ((uint)(size & 0xFFF) << 4) | flags;
    }

    /// <summary>
    /// Gets the class OOP from the header.
    /// </summary>
    public OOP ClassOop => new((HeaderWord >> 16) & 0xFFFF);

    /// <summary>
    /// Gets the size in words from the header.
    /// </summary>
    public ushort Size => (ushort)((HeaderWord >> 4) & 0xFFF);

    /// <summary>
    /// Indicates whether this object contains pointers.
    /// </summary>
    public bool HasPointers => (HeaderWord & PointersFlag) != 0;

    /// <summary>
    /// Indicates whether this object contains words (non-pointer data).
    /// </summary>
    public bool HasWords => (HeaderWord & WordsFlag) != 0;

    /// <summary>
    /// Indicates whether this object is free (available for allocation).
    /// </summary>
    public bool IsFree => (HeaderWord & FreeFlag) != 0;

    /// <summary>
    /// Indicates whether this object is marked (for garbage collection).
    /// </summary>
    public bool IsMarked => (HeaderWord & MarkFlag) != 0;

    /// <summary>
    /// Creates a copy of this header with the mark flag set.
    /// </summary>
    /// <returns>A new header with the mark flag set.</returns>
    public ObjectHeader WithMark() => this with { HeaderWord = HeaderWord | MarkFlag };

    /// <summary>
    /// Creates a copy of this header with the mark flag cleared.
    /// </summary>
    /// <returns>A new header with the mark flag cleared.</returns>
    public ObjectHeader WithoutMark() => this with { HeaderWord = HeaderWord & ~MarkFlag };

    /// <summary>
    /// Creates a copy of this header marked as free.
    /// </summary>
    /// <returns>A new header marked as free.</returns>
    public ObjectHeader AsFree() => this with { HeaderWord = HeaderWord | FreeFlag };

    /// <summary>
    /// Creates a copy of this header marked as allocated.
    /// </summary>
    /// <returns>A new header marked as allocated.</returns>
    public ObjectHeader AsAllocated() => this with { HeaderWord = HeaderWord & ~FreeFlag };

    /// <summary>
    /// Updates the size in the header.
    /// </summary>
    /// <param name="newSize">The new size in words.</param>
    /// <returns>A new header with the updated size.</returns>
    public ObjectHeader WithSize(ushort newSize)
    {
        var newHeader = (HeaderWord & ~(0xFFFu << 4)) | ((uint)(newSize & 0xFFF) << 4);
        return this with { HeaderWord = newHeader };
    }

    /// <summary>
    /// Updates the class OOP in the header.
    /// </summary>
    /// <param name="newClassOop">The new class OOP.</param>
    /// <returns>A new header with the updated class.</returns>
    public ObjectHeader WithClass(OOP newClassOop)
    {
        var newHeader = (HeaderWord & 0xFFFF) | ((uint)(newClassOop.Value & 0xFFFF) << 16);
        return this with { HeaderWord = newHeader };
    }

    // Flag bit positions
    private const uint PointersFlag = 0x1;
    private const uint WordsFlag = 0x2;
    private const uint FreeFlag = 0x4;
    private const uint MarkFlag = 0x8;

    /// <summary>
    /// Returns a string representation of this object header.
    /// </summary>
    public override string ToString() => 
        $"Header[Class={ClassOop}, Size={Size}, Ptrs={HasPointers}, Words={HasWords}, Free={IsFree}, Marked={IsMarked}]";
}