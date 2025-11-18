using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Handles become operations (identity swapping) per Blue Book specification.
/// </summary>
public class BecomeHandler : IBecomeHandler
{
    private readonly IObjectTableManager _tableManager;

    public BecomeHandler(IObjectTableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2)
    {
        // Validate parameters
        if (oop1 == oop2)
            return; // No-op if swapping object with itself

        if (oop1.IsSmallInteger || oop2.IsSmallInteger)
            throw new ArgumentException("Cannot become: with SmallIntegers - they are immediate values");

        if (!_tableManager.IsValidObjectOOP(oop1))
            throw new ArgumentException($"Invalid OOP: {oop1}");

        if (!_tableManager.IsValidObjectOOP(oop2))
            throw new ArgumentException($"Invalid OOP: {oop2}");

        // Blue Book become: semantics: Swap the object table entry contents
        // This makes oop1 point to what oop2 pointed to, and vice versa
        // All existing OOP references remain unchanged - they just now point to swapped data
        SwapObjectTableEntries(oop1, oop2);
    }

    /// <summary>
    /// Swaps the ObjectTableEntry contents for two OOPs.
    /// After this operation, oop1 will have the entry that oop2 had and vice versa.
    /// </summary>
    private void SwapObjectTableEntries(SmalltalkOOP oop1, SmalltalkOOP oop2)
    {
        var entry1 = _tableManager.GetObjectTableEntry(oop1);
        var entry2 = _tableManager.GetObjectTableEntry(oop2);

        // Create temporary copies of the entries to avoid aliasing issues
        var temp1 = new ObjectTableEntry(
            managedStorage: entry1.ManagedStorage,
            format: entry1.FormatExplicit,
            sizeInWords: entry1.SizeInWordsExplicit,
            instanceVariableCount: entry1.InstanceVariableCountExplicit,
            originalIndexedSize: entry1.OriginalIndexedSize)
        {
            PackedMetadata = entry1.PackedMetadata,
            ExtendedSize = entry1.ExtendedSize,
            IsMarked = entry1.IsMarked,
            NamedSlots = entry1.NamedSlots != null ? new Dictionary<string, int>(entry1.NamedSlots) : null,
            ClassOOP = entry1.ClassOOP
        };

        // Copy entry2 to entry1's position
        SetObjectTableEntryContents(entry1, entry2);

        // Copy temp1 (original entry1) to entry2's position
        SetObjectTableEntryContents(entry2, temp1);
    }

    /// <summary>
    /// Sets the contents of a target ObjectTableEntry from a source entry.
    /// </summary>
    private void SetObjectTableEntryContents(ObjectTableEntry targetEntry, ObjectTableEntry sourceEntry)
    {
        // Copy all fields from source to target
        targetEntry.ManagedStorage = sourceEntry.ManagedStorage;
        targetEntry.PackedMetadata = sourceEntry.PackedMetadata;
        targetEntry.ExtendedSize = sourceEntry.ExtendedSize;
        targetEntry.OriginalIndexedSize = sourceEntry.OriginalIndexedSize;
        targetEntry.IsMarked = sourceEntry.IsMarked;
        targetEntry.NamedSlots = sourceEntry.NamedSlots != null ? new Dictionary<string, int>(sourceEntry.NamedSlots) : null;
        targetEntry.FormatExplicit = sourceEntry.FormatExplicit;
        targetEntry.SizeInWordsExplicit = sourceEntry.SizeInWordsExplicit;
        targetEntry.InstanceVariableCountExplicit = sourceEntry.InstanceVariableCountExplicit;
        targetEntry.ClassOOP = sourceEntry.ClassOOP;
    }
}
