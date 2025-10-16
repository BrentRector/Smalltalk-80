using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for tracking symbol OOPs during cold start.
/// Maps symbol indices to their allocated SmalltalkOOPs.
/// </summary>
public static class SymbolRegistry
{
    private static readonly Dictionary<int, SmalltalkOOP> _symbolIndexToOOP = new();

    /// <summary>
    /// Registers a symbol OOP for a given symbol index.
    /// </summary>
    public static void Register(int symbolIndex, SmalltalkOOP symbolOOP)
    {
        _symbolIndexToOOP[symbolIndex] = symbolOOP;
    }

    /// <summary>
    /// Gets the SmalltalkOOP for a symbol by its index.
    /// </summary>
    public static SmalltalkOOP GetSymbolOOP(int symbolIndex)
    {
        return _symbolIndexToOOP[symbolIndex];
    }

    /// <summary>
    /// Returns all registered symbols as (index, symbolOOP) pairs.
    /// </summary>
    public static IEnumerable<(int Index, SmalltalkOOP SymbolOOP)> AllSymbols()
    {
        return _symbolIndexToOOP.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// Clears all registered symbols. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _symbolIndexToOOP.Clear();
    }
}
