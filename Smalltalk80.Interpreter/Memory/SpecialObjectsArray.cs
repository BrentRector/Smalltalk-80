namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Manages the GC root objects array - the essential objects needed for garbage collection
/// to mark all reachable objects in the system.
///
/// This is NOT the Blue Book's broader "Special Objects Array" concept which includes
/// special selectors and other cached objects. This array contains ONLY the root objects
/// necessary for garbage collection to function correctly.
///
/// All objects in the system must be reachable from one of these roots:
/// - System singletons (nil, true, false)
/// - Metaclass hierarchy roots (Behavior, Metaclass)
/// - SystemDictionary (contains all classes and globals)
/// - SymbolTable (contains all interned symbols)
/// - Process management (ProcessList, ProcessorScheduler)
/// </summary>
public static class SpecialObjectsArray
{
    /// <summary>
    /// Index of nil object (UndefinedObject singleton).
    /// </summary>
    public const int NilIndex = 0;

    /// <summary>
    /// Index of false object (False singleton).
    /// </summary>
    public const int FalseIndex = 1;

    /// <summary>
    /// Index of true object (True singleton).
    /// </summary>
    public const int TrueIndex = 2;

    /// <summary>
    /// Index of Behavior class - root for metaclass hierarchy.
    /// </summary>
    public const int BehaviorClassIndex = 3;

    /// <summary>
    /// Index of Metaclass class - root for metaclass hierarchy.
    /// </summary>
    public const int MetaclassClassIndex = 4;

    /// <summary>
    /// Index of SystemDictionary (Smalltalk global) - main root containing all classes and globals.
    /// </summary>
    public const int SystemDictionaryIndex = 5;

    /// <summary>
    /// Index of SymbolTable - contains all interned symbols.
    /// </summary>
    public const int SymbolTableIndex = 6;

    /// <summary>
    /// Index of ProcessList - contains all active processes.
    /// </summary>
    public const int ProcessListIndex = 7;

    /// <summary>
    /// Index of ProcessorScheduler singleton instance.
    /// </summary>
    public const int ProcessorSchedulerIndex = 8;

    /// <summary>
    /// Total count of GC root objects.
    /// </summary>
    public const int Count = 9;

    private static readonly List<SObject?> array = new(Count);

    /// <summary>
    /// Initializes the GC roots array with essential system objects.
    /// Called after cold start when all system objects exist.
    /// </summary>
    public static void Initialize()
    {
        // Clear and resize to exactly 9 entries
        array.Clear();
        for (int i = 0; i < Count; i++)
            array.Add(null);

        // System singletons
        // These will be set later during bootstrap - for now just initialize to null
        array[NilIndex] = null;
        array[FalseIndex] = null;  // Will be set during bootstrap
        array[TrueIndex] = null;   // Will be set during bootstrap

        // Metaclass hierarchy roots (needed for class structure)
        // These will be set during bootstrap
        array[BehaviorClassIndex] = null;
        array[MetaclassClassIndex] = null;

        // System dictionary (contains ALL classes and globals - main root)
        // This will be set during bootstrap
        array[SystemDictionaryIndex] = null;

        // Symbol table (contains all interned symbols)
        // This will be set during bootstrap
        array[SymbolTableIndex] = null;

        // Process list (TODO: create actual process list)
        array[ProcessListIndex] = null;  // Will be set when process system is initialized

        // Process scheduler - will be set during bootstrap
        array[ProcessorSchedulerIndex] = null;
    }

    /// <summary>
    /// Gets the GC roots array for garbage collection use.
    /// </summary>
    public static List<SObject?> GetArray() => array;

    /// <summary>
    /// Gets a specific GC root object.
    /// </summary>
    public static SObject? Get(int index)
    {
        if (index >= 0 && index < array.Count)
            return array[index];
        return null;
    }

    /// <summary>
    /// Sets a specific GC root object.
    /// </summary>
    public static void Set(int index, SObject? obj)
    {
        while (array.Count <= index)
            array.Add(null);
        array[index] = obj;
    }

    /// <summary>
    /// Reports the contents of the GC roots array for debugging.
    /// </summary>
    public static void Report()
    {
        Console.WriteLine("\nGC Roots Array Contents:");
        Console.WriteLine("=" . PadRight(60, '='));

        var namedIndices = new (int index, string name)[]
        {
            (NilIndex, "nil (UndefinedObject singleton)"),
            (FalseIndex, "false (False singleton)"),
            (TrueIndex, "true (True singleton)"),
            (BehaviorClassIndex, "Behavior class"),
            (MetaclassClassIndex, "Metaclass class"),
            (SystemDictionaryIndex, "SystemDictionary (Smalltalk global)"),
            (SymbolTableIndex, "SymbolTable"),
            (ProcessListIndex, "ProcessList"),
            (ProcessorSchedulerIndex, "ProcessorScheduler")
        };

        foreach (var (index, name) in namedIndices)
        {
            var obj = Get(index);
            var status = obj == null ? "NULL" : obj.ToDisplayString();
            Console.WriteLine($"  [{index}] {name,-40} = {status}");
        }

        // Summary statistics
        var populatedCount = 0;
        for (int i = 0; i < Count; i++)
        {
            if (Get(i) != null) populatedCount++;
        }

        Console.WriteLine("\n" + "=" . PadRight(60, '='));
        Console.WriteLine($"  Total entries: {Count}, Populated: {populatedCount}, Empty: {Count - populatedCount}");
        Console.WriteLine();
    }
}