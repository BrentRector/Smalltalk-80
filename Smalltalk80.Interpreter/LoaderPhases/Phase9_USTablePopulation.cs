using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 9: Universal Symbol Table Population
/// Creates and populates the USTable with all interned symbols using the canonical Smalltalk-80 hash algorithm.
/// </summary>
public class Phase9_USTablePopulation
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;

    public Phase9_USTablePopulation(ColdStartBundle bundle, ISmalltalkHeap heap)
    {
        _bundle = bundle;
        _heap = heap;
    }

    public void Run()
    {
        var symbolObjects = SymbolRegistry.AllSymbols(); // index → objectId
        int ustableSize = ComputeUSTableSize(symbolObjects.Count());
        int ustableId = _heap.CreateUSTable(ustableSize);

        foreach (var (index, symbolId) in symbolObjects)
        {
            string symbolName = _bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            _heap.InsertIntoUSTable(ustableId, symbolId, hash);
        }

        GlobalRegistry.Register("USTable", ustableId);
    }

    private int ComputeUSTableSize(int count) => count * 2; // Load factor heuristic
}
