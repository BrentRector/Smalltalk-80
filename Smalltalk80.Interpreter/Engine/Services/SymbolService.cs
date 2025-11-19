using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides centralized symbol interning and lookup services for cold start phases.
/// Consolidates duplicated symbol resolution logic from Phase3/FieldValueResolver,
/// Phase4/LiteralResolver, and Phase4/MethodDictionaryInstaller.
/// </summary>
/// <remarks>
/// Symbols are interned during Phase 1 and stored in the SymbolRegistry.
/// This service provides a unified interface for looking up symbols by name or index.
/// </remarks>
public class SymbolService
{
    private readonly ColdStartBundle _bundle;

    /// <summary>
    /// Initializes a new instance of the SymbolService class.
    /// </summary>
    /// <param name="bundle">The cold start bundle containing symbol definitions.</param>
    /// <exception cref="ArgumentNullException">Thrown when bundle is null.</exception>
    public SymbolService(ColdStartBundle bundle)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
    }

    /// <summary>
    /// Gets or creates a symbol OOP by name, looking it up in the SymbolRegistry.
    /// </summary>
    /// <param name="name">The symbol name to intern.</param>
    /// <returns>The symbol OOP from the SymbolRegistry.</returns>
    /// <exception cref="InvalidOperationException">Thrown when symbol is not found in symbol table.</exception>
    public SmalltalkOOP InternSymbol(string name)
    {
        // Find the symbol index in the bundle's symbol table
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException(
            $"Symbol '{name}' not found in symbol table. " +
            "Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Gets an existing symbol OOP by name, throwing if not found.
    /// </summary>
    /// <param name="name">The symbol name to look up.</param>
    /// <returns>The symbol OOP from the SymbolRegistry.</returns>
    /// <exception cref="InvalidOperationException">Thrown when symbol is not found.</exception>
    public SmalltalkOOP GetSymbolOOP(string name) => InternSymbol(name);

    /// <summary>
    /// Attempts to get a symbol OOP by name.
    /// </summary>
    /// <param name="name">The symbol name to look up.</param>
    /// <param name="oop">When this method returns, contains the symbol OOP if found; otherwise, default.</param>
    /// <returns>True if the symbol was found; otherwise, false.</returns>
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
    /// Gets a symbol OOP by its index in the symbol table.
    /// </summary>
    /// <param name="index">The zero-based index of the symbol in the bundle's symbol table.</param>
    /// <returns>The symbol OOP from the SymbolRegistry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public SmalltalkOOP GetSymbolByIndex(int index)
    {
        if (index < 0 || index >= _bundle.Symbols.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Symbol index {index} is out of range [0, {_bundle.Symbols.Length})");
        }

        return SymbolRegistry.GetSymbolOOP(index);
    }

    /// <summary>
    /// Gets the name of a symbol by its index in the symbol table.
    /// </summary>
    /// <param name="index">The zero-based index of the symbol in the bundle's symbol table.</param>
    /// <returns>The symbol name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public string GetSymbolName(int index)
    {
        if (index < 0 || index >= _bundle.Symbols.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Symbol index {index} is out of range [0, {_bundle.Symbols.Length})");
        }

        return _bundle.Symbols[index];
    }

    /// <summary>
    /// Gets the total number of symbols in the symbol table.
    /// </summary>
    public int SymbolCount => _bundle.Symbols.Length;
}
