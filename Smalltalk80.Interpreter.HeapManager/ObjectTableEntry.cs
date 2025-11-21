using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Object table entry structure for the Smalltalk-80 heap.
/// Each object in the heap has an entry containing metadata and managed storage.
/// This is the lowest-level representation from the heap manager's perspective.
/// </summary>
public sealed class ObjectTableEntry
{
    /// <summary>Managed storage for object data (byte[], SmalltalkOOP[], ushort[], or int[]).</summary>
    public object? ManagedStorage { get; set; }

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

    // Explicit metadata fields (cached for hot-path access, bypassing PackedMetadata)

    /// <summary>Object format (cached for hot-path access).</summary>
    public ObjectFormat FormatExplicit { get; set; }

    /// <summary>Total size in words (cached for hot-path access).</summary>
    public int SizeInWordsExplicit { get; set; }

    /// <summary>Instance variable count (cached for hot-path access).</summary>
    public int InstanceVariableCountExplicit { get; set; }

    /// <summary>
    /// Class reference (stored separately for ByteIndexed/WordIndexed objects).
    /// For Pointer/PointerIndexed, this is stored in slot 0 of the OOP array.
    /// </summary>
    public SmalltalkOOP ClassOOP { get; set; }

    /// <summary>
    /// Instance variable storage for ByteIndexed/WordIndexed objects.
    /// These formats store indexed data in ManagedStorage (byte[] or ushort[])
    /// but also need to store OOP instance variables (e.g., Form has bits, width, height, offset).
    /// For Pointer/PointerIndexed, instance variables are in the OOP array so this is null.
    /// </summary>
    public SmalltalkOOP[]? InstanceVariableStorage { get; set; }

    // Typed accessor methods for managed storage

    /// <summary>Gets storage as byte array (for ByteIndexed format).</summary>
    public byte[]? AsByteArray() => ManagedStorage as byte[];

    /// <summary>Gets storage as OOP array (for PointerIndexed/Pointer formats).</summary>
    public SmalltalkOOP[]? AsOopArray() => ManagedStorage as SmalltalkOOP[];

    /// <summary>Gets storage as word array (for WordIndexed format).</summary>
    public ushort[]? AsWordArray() => ManagedStorage as ushort[];

    /// <summary>Gets storage as int array (for specialized formats).</summary>
    public int[]? AsIntArray() => ManagedStorage as int[];

    // Constructors

    /// <summary>Creates a new object table entry with managed storage (preferred constructor).</summary>
    public ObjectTableEntry(
        object? managedStorage,
        ObjectFormat format,
        int sizeInWords,
        int instanceVariableCount = 0,
        int? originalIndexedSize = null)
    {
        ManagedStorage = managedStorage;
        FormatExplicit = format;
        SizeInWordsExplicit = sizeInWords;
        InstanceVariableCountExplicit = instanceVariableCount;
        OriginalIndexedSize = (format == ObjectFormat.ByteIndexed) ? originalIndexedSize : null;

        // Still encode PackedMetadata for cold-start compatibility
        if (sizeInWords > 65535)
        {
            PackedMetadata = EncodePackedMetadata(format, 65535, instanceVariableCount, 0, 0);
            ExtendedSize = (uint)sizeInWords;
        }
        else
        {
            PackedMetadata = EncodePackedMetadata(format, sizeInWords, instanceVariableCount, 0, 0);
            ExtendedSize = null;
        }

        IsMarked = false;
        NamedSlots = null;
    }


    // PackedMetadata bit layout (32-bit word):
    // Bits 31-16: Size in words (16 bits) - 0-65535 words = 0-256KB objects
    // Bits 15-12: Format Type (4 bits) - ObjectFormat enumeration
    // Bits 11-4:  Instance Variable Count (8 bits) - 0-255 instance variables
    // Bits 3-0:   GC Flags (4 bits) - mark bits, generation, pin status
    // Note: Moved GC flags to lower bits and expanded size field for large objects

    /// <summary>Gets object format from explicit field (hot-path optimized).</summary>
    public ObjectFormat Format => FormatExplicit;

    /// <summary>Gets size in words from explicit field (hot-path optimized).</summary>
    public int SizeInWords => SizeInWordsExplicit;

    /// <summary>Gets size in bytes (computed from explicit field).</summary>
    public int SizeInBytes => SizeInWordsExplicit * 4;

    /// <summary>Gets instance variable count from explicit field (hot-path optimized).</summary>
    public int InstanceVariableCount => InstanceVariableCountExplicit;

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
        string storageInfo = ManagedStorage switch
        {
            byte[] ba => $"byte[{ba.Length}]",
            SmalltalkOOP[] oa => $"SmalltalkOOP[{oa.Length}]",
            ushort[] wa => $"ushort[{wa.Length}]",
            int[] ia => $"int[{ia.Length}]",
            null => "null",
            _ => ManagedStorage.GetType().Name
        };

        return $"ObjectTableEntry {{ " +
               $"Storage: {storageInfo}, " +
               $"Format: {FormatExplicit}, " +
               $"Size: {SizeInWordsExplicit} words, " +
               $"InstanceVars: {InstanceVariableCountExplicit}, " +
               $"IsMarked: {IsMarked} }}";
    }
}
