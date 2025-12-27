using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for tracking symbol OOPs during cold start.
/// Maps symbol indices to their allocated SmalltalkOOPs.
///
/// LIFECYCLE:
/// 1. During Phase 1: Register() populates the registry
/// 2. After Phase 1: MakeImmutable() - reads allowed, writes throw
/// 3. After Phase 6: Dispose() - all access throws, data cleared
///
/// Runtime code must access symbols via USTable in the Smalltalk heap.
/// </summary>
public static class SymbolRegistry
{
    private static Dictionary<int, SmalltalkOOP>? _symbolIndexToOOP = new();
    private static bool _isImmutable = false;
    private static bool _isDisposed = false;

    /// <summary>
    /// Makes the registry immutable (read-only).
    /// Call this at the end of Phase 1 (Symbol Interning).
    /// After this, Register() throws but reads are still allowed.
    /// </summary>
    public static void MakeImmutable()
    {
        _isImmutable = true;
    }

    /// <summary>
    /// Disposes the registry, blocking all access and releasing memory.
    /// Call this at the end of cold start (after Phase 6).
    /// After this, ALL operations throw ObjectDisposedException.
    /// </summary>
    public static void Dispose()
    {
        _isDisposed = true;
        _symbolIndexToOOP?.Clear();
        _symbolIndexToOOP = null;
    }

    /// <summary>
    /// Registers a symbol OOP for a given symbol index.
    /// THROWS after MakeImmutable() - only for Phase 1 use.
    /// </summary>
    public static void Register(int symbolIndex, SmalltalkOOP symbolOOP)
    {
        ThrowIfDisposed();

        if (_isImmutable)
        {
            throw new InvalidOperationException(
                $"SymbolRegistry.Register() called after MakeImmutable(). " +
                $"Symbol registration only allowed during Phase 1. Attempted to register symbol index: {symbolIndex}");
        }

        _symbolIndexToOOP![symbolIndex] = symbolOOP;
    }

    /// <summary>
    /// Gets the SmalltalkOOP for a symbol by its index.
    /// Allowed during cold start phases 2-6.
    /// THROWS after Dispose() - runtime code must use USTable in heap.
    /// </summary>
    public static SmalltalkOOP GetSymbolOOP(int symbolIndex)
    {
        ThrowIfDisposed();

        if (!_symbolIndexToOOP!.TryGetValue(symbolIndex, out var symbolOOP))
        {
            throw new KeyNotFoundException(
                $"SymbolRegistry.GetSymbolOOP: Symbol index {symbolIndex} not found. " +
                $"Registry contains {_symbolIndexToOOP.Count} symbols.");
        }
        return symbolOOP;
    }

    /// <summary>
    /// Returns all registered symbols as (index, symbolOOP) pairs.
    /// Allowed during cold start phases 2-6.
    /// THROWS after Dispose() - cold start structure not available at runtime.
    /// </summary>
    public static IEnumerable<(int Index, SmalltalkOOP SymbolOOP)> AllSymbols()
    {
        ThrowIfDisposed();
        return _symbolIndexToOOP!.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// Clears all registered symbols. Used for testing or reinitialization.
    /// </summary>
    public static void Clear()
    {
        _symbolIndexToOOP?.Clear();
        _symbolIndexToOOP = new();
        _isImmutable = false;
        _isDisposed = false;
    }

    private static void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SymbolRegistry),
                "SymbolRegistry has been disposed. Runtime code must access symbols via USTable in the Smalltalk heap.");
        }
    }
}
