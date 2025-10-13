using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 12: Universal Symbol Table Population
/// Creates and populates the USTable with all interned symbols using the canonical Smalltalk-80 hash algorithm.
/// </summary>
public class Phase12_USTablePopulation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;

    public Phase12_USTablePopulation(ColdStartBundle bundle, SemanticAllocator allocator)
    {
        _bundle = bundle;
        _allocator = allocator;
    }

    public void Run()
    {
        var symbolObjects = SymbolRegistry.AllSymbols(); // index → objectId
        int ustableSize = ComputeUSTableSize(symbolObjects.Count());
        Console.WriteLine($"DEBUG: Creating single USTable with {ustableSize} slots for {symbolObjects.Count()} symbols");
        
        int ustableId = CreateUSTable(ustableSize);

        foreach (var (index, symbolId) in symbolObjects)
        {
            string symbolName = _bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            InsertIntoUSTable(ustableId, symbolId, hash);
        }

        GlobalRegistry.Register("USTable", ustableId);
        
        // Note: USTable will be installed as a global variable in Phase 11 (Global Variable Installation)
    }

    private int ComputeUSTableSize(int count) => count * 2; // Load factor heuristic

    /// <summary>
    /// Creates a USTable (Universal Symbol Table) as an Array.
    /// </summary>
    private int CreateUSTable(int size)
    {
        Console.WriteLine($"DEBUG: Creating USTable with {size} slots");
        
        // Look up Array class layout
        var arrayClassDefinition = FindClassDefinition("Array");
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        var ustableOOP = _allocator.AllocateArray("Array", size);

        // Initialize all slots to nil
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);
        for (int i = 0; i < size; i++)
        {
            _allocator.SetIndexedOOP(ustableOOP, i, nilOOP);
        }

        Console.WriteLine($"DEBUG: USTable chunk created with id {ustableOOP.AsObjectTableIndex()}");
        return ustableOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Inserts a symbol into the USTable using open addressing.
    /// </summary>
    private void InsertIntoUSTable(int ustableId, int symbolId, int hash)
    {
        var ustableOOP = SmalltalkOOP.FromObjectTableIndex(ustableId);
        int size = _allocator.GetIndexedSize(ustableOOP);
        int index = hash % size;
        
        // Find first empty slot using open addressing
        while (true)
        {
            var slotOOP = _allocator.GetIndexedOOP(ustableOOP, index);
            if (slotOOP.AsObjectTableIndex() == GlobalRegistry.NilObjectId)
            {
                // Found empty slot
                var symbolOOP = SmalltalkOOP.FromObjectTableIndex(symbolId);
                _allocator.SetIndexedOOP(ustableOOP, index, symbolOOP);
                break;
            }
            
            // Move to next slot (wrap around)
            index = (index + 1) % size;
        }
    }

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// </summary>
    private ClassDescriptor? FindClassDefinition(string className)
    {
        return _bundle.Classes.FirstOrDefault(c => 
            !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Calculates the total instance variable count for a class including inherited ones.
    /// </summary>
    private int CalculateTotalInstanceVariableCount(ClassDescriptor classDefinition)
    {
        int totalCount = 0;
        var currentClass = classDefinition;
        
        while (currentClass != null)
        {
            totalCount += currentClass.InstVarNames.Length;
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = _bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = _bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        return totalCount;
    }

}
