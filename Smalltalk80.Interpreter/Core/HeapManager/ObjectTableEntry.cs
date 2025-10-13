using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Object table entry structure for the Smalltalk-80 heap.
/// Each object in the heap has an entry containing metadata and memory pointer.
/// This is the lowest-level representation from the heap manager's perspective.
/// </summary>
public sealed class ObjectTableEntry
{
    /// <summary>Pointer to allocated object memory in .NET heap.</summary>
    public IntPtr MemoryPointer { get; set; }
    
    /// <summary>Packed metadata word for space efficiency (size + format + GC flags + other metadata).</summary>
    public uint PackedMetadata { get; set; }
    
    /// <summary>Extended size field for objects larger than 65535 words. Only used when needed.</summary>
    public uint? ExtendedSize { get; set; }
    
    /// <summary>Original indexed size for ByteIndexed objects (before word alignment). Only used for ByteIndexed format.</summary>
    public int? OriginalIndexedSize { get; set; }
    
    /// <summary>GC mark bit for mark-and-sweep collection.</summary>
    public bool IsMarked { get; set; }
    
    /// <summary>Dynamic named slots for bootstrap and class metadata (legacy compatibility).</summary>
    public Dictionary<string, int>? NamedSlots { get; set; }

    /// <summary>Creates a new object table entry.</summary>
    public ObjectTableEntry(IntPtr memoryPointer, ObjectFormat format, int sizeInWords, int instanceVariableCount = 0, int? originalIndexedSize = null)
    {
        MemoryPointer = memoryPointer;
        
        // Handle very large objects with ExtendedSize
        if (sizeInWords > 65535)
        {
            PackedMetadata = EncodePackedMetadata(format, 65535, instanceVariableCount, 0, 0); // Use max value as indicator
            ExtendedSize = (uint)sizeInWords;
        }
        else
        {
            PackedMetadata = EncodePackedMetadata(format, sizeInWords, instanceVariableCount, 0, 0);
            ExtendedSize = null;
        }
        
        // Store original indexed size for ByteIndexed objects
        OriginalIndexedSize = (format == ObjectFormat.ByteIndexed) ? originalIndexedSize : null;
        
        IsMarked = false;
        NamedSlots = null;
    }

    /// <summary>Creates a new object table entry with full metadata control.</summary>
    public ObjectTableEntry(IntPtr memoryPointer, uint packedMetadata)
    {
        MemoryPointer = memoryPointer;
        PackedMetadata = packedMetadata;
        IsMarked = false;
        NamedSlots = null;
    }

    // PackedMetadata bit layout (32-bit word):
    // Bits 31-16: Size in words (16 bits) - 0-65535 words = 0-256KB objects  
    // Bits 15-12: Format Type (4 bits) - ObjectFormat enumeration
    // Bits 11-4:  Instance Variable Count (8 bits) - 0-255 instance variables
    // Bits 3-0:   GC Flags (4 bits) - mark bits, generation, pin status
    // Note: Moved GC flags to lower bits and expanded size field for large objects

    /// <summary>Extracts ObjectFormat from PackedMetadata.</summary>
    public ObjectFormat Format => (ObjectFormat)((PackedMetadata >> 12) & 0xF);

    /// <summary>Extracts size in words from PackedMetadata (with extended size support).</summary>
    public int SizeInWords => ExtendedSize.HasValue ? (int)ExtendedSize.Value : (int)((PackedMetadata >> 16) & 0xFFFF);

    /// <summary>Extracts size in bytes from PackedMetadata.</summary>
    public int SizeInBytes => SizeInWords * 4;

    /// <summary>Extracts instance variable count from PackedMetadata.</summary>
    public int InstanceVariableCount => (int)((PackedMetadata >> 4) & 0xFF);

    /// <summary>Extracts GC flags from PackedMetadata.</summary>
    public byte GCFlags => (byte)(PackedMetadata & 0xF);

    /// <summary>Extracts general flags from PackedMetadata (now part of GC flags).</summary>
    public byte Flags => (byte)(PackedMetadata & 0xF);

    /// <summary>Checks if identity hash is cached in GC flags.</summary>
    public bool HasCachedIdentityHash => ((PackedMetadata >> 31) & 1) == 1;

    /// <summary>Checks if object is marked as immutable.</summary>
    public bool IsImmutable => ((PackedMetadata >> 0) & 1) == 1;

    /// <summary>Encodes PackedMetadata from individual components.</summary>
    public static uint EncodePackedMetadata(ObjectFormat format, int sizeInWords, int instanceVariableCount, byte gcFlags, byte flags)
    {
        if (sizeInWords < 0 || sizeInWords > 65535)
            throw new ArgumentOutOfRangeException(nameof(sizeInWords), "Size must be 0-65535 words for PackedMetadata (use ExtendedSize for larger objects)");
        if (instanceVariableCount < 0 || instanceVariableCount > 255)
            throw new ArgumentOutOfRangeException(nameof(instanceVariableCount), "Instance variable count must be 0-255");

        return ((uint)sizeInWords << 16) |
               ((uint)format << 12) |
               ((uint)instanceVariableCount << 4) |
               (uint)(gcFlags & 0xF);
    }

    /// <summary>Encodes PackedMetadata from individual components (legacy overload).</summary>
    public static uint EncodePackedMetadata(ObjectFormat format, int sizeInWords, byte gcFlags, byte flags)
    {
        return EncodePackedMetadata(format, sizeInWords, 0, gcFlags, flags);
    }

    /// <summary>Updates the format in PackedMetadata.</summary>
    public void SetFormat(ObjectFormat format)
    {
        PackedMetadata = (PackedMetadata & ~(0xFu << 12)) | ((uint)format << 12);
    }

    /// <summary>Updates the size in PackedMetadata.</summary>
    public void SetSizeInWords(int sizeInWords)
    {
        if (sizeInWords < 0 || sizeInWords > 65535)
            throw new ArgumentOutOfRangeException(nameof(sizeInWords), "Size must be 0-65535 words");
        
        PackedMetadata = (PackedMetadata & ~(0xFFFFu << 16)) | ((uint)sizeInWords << 16);
    }

    /// <summary>Updates GC flags in PackedMetadata.</summary>
    public void SetGCFlags(byte gcFlags)
    {
        PackedMetadata = (PackedMetadata & ~(0xFFu << 24)) | ((uint)gcFlags << 24);
    }

    /// <summary>Updates general flags in PackedMetadata.</summary>
    public void SetFlags(byte flags)
    {
        PackedMetadata = (PackedMetadata & ~0xFu) | (uint)(flags & 0xF);
    }

    /// <summary>Gets debugging information about this entry.</summary>
    public string GetDebugInfo()
    {
        return $"ObjectTableEntry {{ " +
               $"MemoryPointer: 0x{MemoryPointer:X8}, " +
               $"Format: {Format}, " +
               $"Size: {SizeInWords} words ({SizeInBytes} bytes), " +
               $"IsMarked: {IsMarked}, " +
               $"GCFlags: 0x{GCFlags:X2}, " +
               $"Flags: 0x{Flags:X}, " +
               $"HasNamedSlots: {NamedSlots != null} }}";
    }
}