namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Registry for tracking symbol object IDs during cold start.
/// Maps symbol indices to their allocated object IDs.
/// </summary>
public static class SymbolRegistry
{
    private static readonly Dictionary<int, int> _symbolIndexToObjectId = new();

    /// <summary>
    /// Registers a symbol object ID for a given symbol index.
    /// </summary>
    public static void Register(int symbolIndex, int objectId)
    {
        _symbolIndexToObjectId[symbolIndex] = objectId;
    }

    /// <summary>
    /// Gets the object ID for a symbol by its index.
    /// </summary>
    public static int GetObjectId(int symbolIndex)
    {
        return _symbolIndexToObjectId[symbolIndex];
    }

    /// <summary>
    /// Returns all registered symbols as (index, objectId) pairs.
    /// </summary>
    public static IEnumerable<(int Index, int ObjectId)> AllSymbols()
    {
        return _symbolIndexToObjectId.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// Clears all registered symbols. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _symbolIndexToObjectId.Clear();
    }
}
