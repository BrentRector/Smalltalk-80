using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 1: Symbol Interning
/// Interns all symbols from the bundle into the heap and populates the SymbolRegistry.
/// </summary>
public class Phase1_SymbolInterning
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;

    public Phase1_SymbolInterning(ColdStartBundle bundle, ISmalltalkHeap heap)
    {
        _bundle = bundle;
        _heap = heap;
    }

    public void Run()
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            string symbolName = _bundle.Symbols[i];
            int symbolId = _heap.InternSymbol(symbolName);
            SymbolRegistry.Register(i, symbolId);
        }
    }
}
