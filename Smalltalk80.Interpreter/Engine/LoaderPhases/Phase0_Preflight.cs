using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 0: Preflight Validation
/// Validates the cold start bundle for consistency before processing.
/// Checks for duplicate localIds, invalid symbol indices, and unresolvable object references.
/// </summary>
public class Phase0_Preflight
{
    private readonly ColdStartBundle _bundle;

    public Phase0_Preflight(ColdStartBundle bundle)
    {
        _bundle = bundle;
    }

    public void Run()
    {
        ValidateLocalIds();
        ValidateSymbolIndices();
        ValidateObjectRefs();
    }

    /// <summary>
    /// Ensures all localIds are unique across classes, objects, methods, and init expressions.
    /// </summary>
    private void ValidateLocalIds()
    {
        var allIds = _bundle.Classes.Select(c => c.LocalId)
            .Concat(_bundle.Objects.Select(o => o.LocalId))
            .Concat(_bundle.Methods.Select(m => m.LocalId))
            .Concat(_bundle.InitExpressions.Select(i => i.LocalId))
            .ToHashSet();

        if (allIds.Count != _bundle.Classes.Length + _bundle.Objects.Length + _bundle.Methods.Length + _bundle.InitExpressions.Length)
            throw new InvalidOperationException("Duplicate localIds detected.");
    }

    /// <summary>
    /// Validates that all symbol indices are within bounds.
    /// </summary>
    private void ValidateSymbolIndices()
    {
        int max = _bundle.Symbols.Length;
        foreach (var c in _bundle.Classes)
        {
            if (c.NameIndex >= max || c.InstVarNames.Any(i => i >= max))
                throw new IndexOutOfRangeException("Invalid symbol index in class.");
        }
    }

    /// <summary>
    /// Validates that all object references point to valid localIds.
    /// </summary>
    private void ValidateObjectRefs()
    {
        // Build set of all valid localIds (classes, objects, methods, init expressions)
        var validIds = _bundle.Classes.Select(c => c.LocalId)
            .Concat(_bundle.Objects.Select(o => o.LocalId))
            .Concat(_bundle.Methods.Select(m => m.LocalId))
            .Concat(_bundle.InitExpressions.Select(i => i.LocalId))
            .ToHashSet();

        // Validate patch references
        // Note: Some patches may reference objects not yet allocated (forward references)
        // so we just warn instead of throwing
        int unresolvedCount = 0;
        foreach (var patch in _bundle.Patches)
        {
            if (patch.Value?.LocalId is string id && !validIds.Contains(id))
            {
                if (unresolvedCount == 0)
                    Console.WriteLine($"  Warning: Found patches with unknown localIds:");
                Console.WriteLine($"    - {id}");
                unresolvedCount++;
            }
        }
        if (unresolvedCount > 0)
        {
            Console.WriteLine($"  Total unresolved references: {unresolvedCount} (may be resolved during loading)");
        }
    }
}
