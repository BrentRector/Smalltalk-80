namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Object table entry structure for the Smalltalk-80 heap.
/// Each object in the heap has an entry containing metadata and data.
/// This is the lowest-level representation from the heap manager's perspective.
/// </summary>
public sealed class ObjectTableEntry
{
    /// <summary>OOP of this object's class (determines behavior and format).</summary>
    public int ClassOOP { get; set; }
    
    /// <summary>Storage format determining how IndexedData is interpreted.</summary>
    public ObjectFormat Format { get; set; }
    
    /// <summary>Named instance variables stored as OOPs.</summary>
    public int[] InstanceVariables { get; set; }
    
    /// <summary>Indexed data: byte[], ushort[], int[], or null depending on Format.</summary>
    public object? IndexedData { get; set; }
    
    /// <summary>GC mark bit for mark-and-sweep collection.</summary>
    public bool IsMarked { get; set; }
    
    /// <summary>Dynamic named slots for bootstrap and class metadata (legacy compatibility).</summary>
    public Dictionary<string, int>? NamedSlots { get; set; }

    /// <summary>Creates a new object table entry.</summary>
    public ObjectTableEntry(int classOOP, ObjectFormat format, int instanceVarCount)
    {
        ClassOOP = classOOP;
        Format = format;
        InstanceVariables = new int[instanceVarCount];
        IndexedData = null;
        IsMarked = false;
    }

    /// <summary>
    /// Initializes the indexed data storage based on the object format.
    /// </summary>
    public void InitializeIndexedData(int indexedCount)
    {
        IndexedData = Format switch
        {
            ObjectFormat.Pointer => null, // No indexed data
            ObjectFormat.PointerIndexed => new int[indexedCount],
            ObjectFormat.ByteIndexed => new byte[indexedCount],
            ObjectFormat.WordIndexed => new ushort[indexedCount],
            _ => throw new ArgumentException($"Unknown object format: {Format}")
        };
    }

    /// <summary>
    /// Gets the total size of this object in "slots" (for GC accounting).
    /// Instance variables + indexed data size.
    /// </summary>
    public int GetTotalSlotCount()
    {
        var baseSize = InstanceVariables.Length;
        return IndexedData switch
        {
            null => baseSize,
            int[] ints => baseSize + ints.Length,
            byte[] bytes => baseSize + ((bytes.Length + 3) / 4), // Round up to int alignment
            ushort[] words => baseSize + ((words.Length + 1) / 2), // Round up to int alignment
            _ => baseSize
        };
    }
}