namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Manages the Special Objects Array - the ONLY root for garbage collection.
/// The SOA is a VM-level structure that is never accessed by Smalltalk code.
/// It can be implemented as either a C# List or a Smalltalk Array.
/// </summary>
public static class SpecialObjectsArray
{
    /// <summary>
    /// Index of nil object in Special Objects Array.
    /// </summary>
    public const int NilIndex = 0;
    
    /// <summary>
    /// Index of false object in Special Objects Array.
    /// </summary>
    public const int FalseIndex = 1;
    
    /// <summary>
    /// Index of true object in Special Objects Array.
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
    /// Index of SystemDictionary (Smalltalk global) - contains all classes and globals.
    /// </summary>
    public const int SystemDictionaryIndex = 5;
    
    /// <summary>
    /// Index of SymbolTable - contains all interned symbols including special selectors.
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
    /// Total count of Special Objects Array entries (exactly 9).
    /// </summary>
    public const int Count = 9;
    
    private static readonly List<SObject?> array = new(32);
    
    /// <summary>
    /// Initializes the Special Objects Array with system objects.
    /// Called after cold start when all system objects exist.
    /// </summary>
    public static void Initialize()
    {
        // Clear and resize to minimal size
        array.Clear();
        for (int i = 0; i < Count; i++)  // Exactly 9 entries
            array.Add(null);
        
        // Core singleton objects (must be roots)
        array[NilIndex] = SNil.Instance;
        array[FalseIndex] = SFalse.Instance;
        array[TrueIndex] = STrue.Instance;
        
        // Metaclass hierarchy roots (needed for class structure)
        array[BehaviorClassIndex] = SystemDictionary.BehaviorClass;
        array[MetaclassClassIndex] = SystemDictionary.MetaclassClass;
        
        // System dictionary (contains ALL classes and globals - main root)
        // This is THE critical entry - all classes are reachable through it
        array[SystemDictionaryIndex] = SDictionary.CreateSystemDictionary();
        
        // Symbol table (contains all interned symbols including special selectors)
        // Special selectors like #doesNotUnderstand: are reachable through here
        array[SymbolTableIndex] = SDictionary.CreateSymbolTable();
        
        // Process list (TODO: create actual process list)
        array[ProcessListIndex] = null;  // Will be set when process system is initialized
        
        // Process scheduler (if created)
        var scheduler = SystemDictionary.LookupGlobal("Processor");
        if (scheduler != null)
        {
            array[ProcessorSchedulerIndex] = scheduler;
        }
    }
    
    /// <summary>
    /// Gets the Special Objects Array for GC use.
    /// </summary>
    public static List<SObject?> GetArray() => array;
    
    /// <summary>
    /// Gets a specific special object.
    /// </summary>
    public static SObject? Get(int index)
    {
        if (index >= 0 && index < array.Count)
            return array[index];
        return null;
    }
    
    /// <summary>
    /// Sets a specific special object.
    /// </summary>
    public static void Set(int index, SObject? obj)
    {
        while (array.Count <= index)
            array.Add(null);
        array[index] = obj;
    }
    
    /// <summary>
    /// Reports the contents of the SOA for debugging.
    /// </summary>
    public static void Report()
    {
        Console.WriteLine("\nSpecial Objects Array Contents:");
        Console.WriteLine("=" . PadRight(60, '='));
        
        var namedIndices = new (int index, string name)[]
        {
            (NilIndex, "nil"),
            (FalseIndex, "false"),
            (TrueIndex, "true"),
            (BehaviorClassIndex, "Behavior class"),
            (MetaclassClassIndex, "Metaclass class"),
            (SystemDictionaryIndex, "Smalltalk (SystemDict)"),
            (SymbolTableIndex, "SymbolTable"),
            (ProcessListIndex, "ProcessList"),
            (ProcessorSchedulerIndex, "ProcessorScheduler")
        };
        
        foreach (var (index, name) in namedIndices)
        {
            var obj = Get(index);
            var status = obj == null ? "NOT SET" : obj.ToDisplayString();
            Console.WriteLine($"  [{index,2}] {name,-25} = {status}");
        }
        
        Console.WriteLine();
    }
}