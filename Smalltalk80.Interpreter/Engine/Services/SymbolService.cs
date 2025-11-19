using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides symbol lookup services for cold start phases.
/// Wraps SymbolRegistry to provide a consistent interface for symbol resolution.
/// </summary>
/// <remarks>
/// Symbols are interned during Phase 1 and stored in the SymbolRegistry.
/// This service provides lookup by name using the bundle's symbol table.
/// </remarks>
public class SymbolService
{
    private readonly ColdStartBundle _bundle;

    /// <summary>
    /// Initializes a new instance of the SymbolService class.
    /// </summary>
    /// <param name="bundle">The cold start bundle containing symbol definitions.</param>
    public SymbolService(ColdStartBundle bundle)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
    }

    /// <summary>
    /// Gets a symbol OOP by name. Throws if not found.
    /// </summary>
    /// <param name="name">The symbol name to look up.</param>
    /// <returns>The symbol OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown if symbol not found.</exception>
    public SmalltalkOOP GetSymbolOOP(string name)
    {
        // Find the symbol in the bundle's symbol table
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Gets a symbol OOP by index in the bundle's symbol table.
    /// </summary>
    /// <param name="index">The index in the symbol table.</param>
    /// <returns>The symbol OOP.</returns>
    public SmalltalkOOP GetSymbolOOPByIndex(int index)
    {
        return SymbolRegistry.GetSymbolOOP(index);
    }

    /// <summary>
    /// Tries to get a symbol OOP by name.
    /// </summary>
    /// <param name="name">The symbol name to look up.</param>
    /// <param name="oop">The symbol OOP if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool TryGetSymbolOOP(string name, out SmalltalkOOP oop)
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                oop = SymbolRegistry.GetSymbolOOP(i);
                return true;
            }
        }

        oop = default;
        return false;
    }

    /// <summary>
    /// Gets the total number of symbols in the bundle.
    /// </summary>
    public int SymbolCount => _bundle.Symbols.Length;
}
