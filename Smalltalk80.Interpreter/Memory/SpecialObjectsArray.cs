namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Manages the GC root objects array - the minimal set of objects needed for garbage collection
/// to mark all reachable objects in the system (per Smalltalk-80 Blue Book specification).
///
/// BLUE BOOK SPECIFICATION (Goldberg & Robson, 1983):
///
/// All objects in the system must be reachable from these 5 minimal roots:
///
/// 1. nil, true, false - Singletons required for hardwired bytecode references
///    (pushNil, pushTrue, pushFalse bytecodes require direct object pointers)
///
/// 2. Smalltalk (SystemDictionary instance) - Root of ALL classes and globals
///    Reachability chain:
///    - Smalltalk at: #Symbol -&gt; Symbol class -&gt; classPool -&gt; #USTable -&gt; ALL symbols/selectors
///    - Smalltalk at: #ClassName -&gt; Class -&gt; methodDict -&gt; ALL methods
///    - Smalltalk at: #Character -&gt; Character class -&gt; classPool -&gt; #CharacterTable -&gt; 256 chars
///    - Each class -&gt; metaclass -&gt; class-side methodDict -&gt; class methods
///
/// 3. ProcessorScheduler - Active process queue
///    Reachability chain:
///    - activeProcess -&gt; Process -&gt; suspendedContext -&gt; MethodContext chain -&gt; execution state
///    - quiescentProcessLists -&gt; Array[100] -&gt; LinkedList -&gt; Process -&gt; contexts
///
/// IMPLEMENTATION NOTE:
/// All Smalltalk data structures (including the symbol table USTable) are maintained as
/// Smalltalk heap objects, not C# proxy structures. This ensures Smalltalk code can update
/// these structures directly, and they remain consistent with the GC reachability model.
/// </summary>
public static class SpecialObjectsArray
{
    /// <summary>
    /// Index of nil object (UndefinedObject singleton).
    /// Required for pushNil bytecode (113).
    /// </summary>
    public const int NilIndex = 0;

    /// <summary>
    /// Index of false object (False singleton).
    /// Required for pushFalse bytecode (114).
    /// </summary>
    public const int FalseIndex = 1;

    /// <summary>
    /// Index of true object (True singleton).
    /// Required for pushTrue bytecode (113).
    /// </summary>
    public const int TrueIndex = 2;

    /// <summary>
    /// Index of the "Smalltalk" global variable (SystemDictionary instance).
    /// This is the main GC root containing ALL classes and global variables.
    ///
    /// Reachability:
    /// - ALL classes (via Associations: #Object -&gt; Object class, #String -&gt; String class, etc.)
    /// - ALL methods (via class -&gt; methodDict -&gt; CompiledMethod instances)
    /// - ALL symbols (via Symbol class -&gt; classPool -&gt; #USTable -&gt; symbol table)
    /// - ALL global variables and pool dictionaries
    /// </summary>
    public const int SystemDictionaryIndex = 3;

    /// <summary>
    /// Index of ProcessorScheduler singleton instance.
    /// Contains all active and runnable processes, which in turn reference all live contexts.
    ///
    /// Reachability:
    /// - activeProcess -&gt; currently executing Process
    /// - quiescentProcessLists -&gt; Array of 100 LinkedLists (priority queues)
    /// - Each Process -&gt; suspendedContext -&gt; MethodContext/BlockContext chain
    /// - Each context -&gt; sender, receiver, method, temps, stack contents
    /// </summary>
    public const int ProcessorSchedulerIndex = 4;

    /// <summary>
    /// Total count of GC root objects (Blue Book minimal specification: 5 roots).
    /// </summary>
    public const int Count = 5;

    private static readonly List<SObject?> array = new(Count);

    /// <summary>
    /// Initializes the GC roots array with essential system objects.
    /// Called after cold start when all system objects exist.
    /// </summary>
    public static void Initialize()
    {
        // Clear and resize to exactly 5 entries (Blue Book minimal specification)
        array.Clear();
        for (int i = 0; i < Count; i++)
            array.Add(null);

        // Singletons (required for bytecode operations)
        array[NilIndex] = null;    // Will be set during bootstrap
        array[FalseIndex] = null;  // Will be set during bootstrap
        array[TrueIndex] = null;   // Will be set during bootstrap

        // SystemDictionary (main GC root for all classes, methods, symbols, globals)
        array[SystemDictionaryIndex] = null;  // Will be set during bootstrap

        // ProcessorScheduler (GC root for all processes and execution contexts)
        array[ProcessorSchedulerIndex] = null;  // Will be set during bootstrap
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
        Console.WriteLine("\nGC Roots Array (Blue Book Minimal Specification - 5 entries):");
        Console.WriteLine("=" . PadRight(70, '='));

        var namedIndices = new (int index, string name)[]
        {
            (NilIndex, "nil (UndefinedObject singleton)"),
            (FalseIndex, "false (False singleton)"),
            (TrueIndex, "true (True singleton)"),
            (SystemDictionaryIndex, "Smalltalk (SystemDictionary instance)"),
            (ProcessorSchedulerIndex, "ProcessorScheduler (process queue)")
        };

        foreach (var (index, name) in namedIndices)
        {
            var obj = Get(index);
            var status = obj == null ? "NULL" : obj.ToDisplayString();
            Console.WriteLine($"  [{index}] {name,-45} = {status}");
        }

        // Summary statistics
        var populatedCount = 0;
        for (int i = 0; i < Count; i++)
        {
            if (Get(i) != null) populatedCount++;
        }

        Console.WriteLine("\n" + "=" . PadRight(70, '='));
        Console.WriteLine($"  Total entries: {Count}, Populated: {populatedCount}, Empty: {Count - populatedCount}");
        Console.WriteLine("\nReachability from these 5 roots:");
        Console.WriteLine("  • nil, false, true → Singletons (hardwired bytecode references)");
        Console.WriteLine("  • Smalltalk → ALL classes → methodDict → ALL methods");
        Console.WriteLine("              → Symbol class → classPool → USTable → ALL symbols");
        Console.WriteLine("              → ALL global variables");
        Console.WriteLine("  • ProcessorScheduler → activeProcess + process queues");
        Console.WriteLine("                       → ALL processes → contexts → execution state");
        Console.WriteLine();
    }
}