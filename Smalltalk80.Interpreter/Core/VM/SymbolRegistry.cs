using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for tracking symbol OOPs during cold start.
/// Maps symbol indices to their allocated SmalltalkOOPs.
///
/// ARCHITECTURAL ENFORCEMENT: After Freeze() is called (after Phase 1),
/// Register() will throw exceptions. Runtime code must access the Smalltalk
/// heap directly via USTable, not this cold start proxy structure.
/// </summary>
public static class SymbolRegistry
{
    private static readonly Dictionary<int, SmalltalkOOP> _symbolIndexToOOP = new();
    private static bool _isFrozen = false;

    /// <summary>
    /// Freezes the registry to prevent runtime modifications.
    /// Call this at the end of Phase 1 (Symbol Interning).
    /// Once frozen, Register() will throw exceptions.
    /// Read-only operations (GetSymbolOOP, AllSymbols) remain allowed.
    /// </summary>
    public static void Freeze()
    {
        _isFrozen = true;
        // Console.WriteLine($"[SymbolRegistry] Frozen with {_symbolIndexToOOP.Count} symbols");
    }

    /// <summary>
    /// Registers a symbol OOP for a given symbol index.
    /// THROWS after Freeze() - only for Phase 1 use.
    /// </summary>
    public static void Register(int symbolIndex, SmalltalkOOP symbolOOP)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"SymbolRegistry.Register() called after freeze. " +
                $"Runtime code must access heap directly via USTable. Attempted to register symbol index: {symbolIndex}");
        }

        _symbolIndexToOOP[symbolIndex] = symbolOOP;
    }

    /// <summary>
    /// Gets the SmalltalkOOP for a symbol by its index.
    /// </summary>
    public static SmalltalkOOP GetSymbolOOP(int symbolIndex)
    {
        if (!_symbolIndexToOOP.TryGetValue(symbolIndex, out var symbolOOP))
        {
            throw new KeyNotFoundException(
                $"SymbolRegistry.GetSymbolOOP: Symbol index {symbolIndex} not found. " +
                $"Registry contains {_symbolIndexToOOP.Count} symbols.");
        }
        return symbolOOP;
    }

    /// <summary>
    /// Returns all registered symbols as (index, symbolOOP) pairs.
    /// </summary>
    public static IEnumerable<(int Index, SmalltalkOOP SymbolOOP)> AllSymbols()
    {
        return _symbolIndexToOOP.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// Clears all registered symbols. Used after cold start to release memory.
    /// Does NOT reset frozen state - registry remains unusable after freeze.
    /// </summary>
    public static void Clear()
    {
        _symbolIndexToOOP.Clear();
    }
}
