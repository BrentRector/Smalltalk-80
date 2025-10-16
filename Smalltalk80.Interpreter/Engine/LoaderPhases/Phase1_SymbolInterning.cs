using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using System.Text;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 1: Symbol Interning
/// Interns all symbols from the bundle into the heap and populates the SymbolRegistry.
/// Uses the new heap manager with ByteIndexed objects for symbol storage.
/// Symbol class will be set later in Phase 2 when classes are created.
/// </summary>
public class Phase1_SymbolInterning
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _symbolTable = new();

    public Phase1_SymbolInterning(ColdStartBundle bundle, SemanticAllocator allocator)
    {
        _bundle = bundle;
        _allocator = allocator;
    }

    public void Run()
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            string symbolName = _bundle.Symbols[i];
            var symbolOOP = InternSymbol(symbolName);
            SymbolRegistry.Register(i, symbolOOP);
        }
    }

    /// <summary>
    /// Interns a symbol, ensuring only one object exists per symbol name.
    /// Creates ByteIndexed objects with UTF-8 encoded symbol text.
    /// Symbol class will be set later when Symbol class is created.
    /// </summary>
    private SmalltalkOOP InternSymbol(string name)
    {
        // Check if symbol already exists (deduplication)
        if (_symbolTable.TryGetValue(name, out var existingOOP))
        {
            return existingOOP;
        }

        // Encode symbol name as UTF-8 bytes
        byte[] symbolBytes = Encoding.UTF8.GetBytes(name);
        
        // Allocate ByteIndexed object to hold the symbol text
        var symbolOOP = _allocator.AllocateString("Symbol", symbolBytes.Length);

        // Store the symbol text as bytes
        for (int i = 0; i < symbolBytes.Length; i++)
        {
            _allocator.SetIndexedByte(symbolOOP, i, symbolBytes[i]);
        }

        // Cache in symbol table for deduplication
        _symbolTable[name] = symbolOOP;
        
        return symbolOOP;
    }

}
