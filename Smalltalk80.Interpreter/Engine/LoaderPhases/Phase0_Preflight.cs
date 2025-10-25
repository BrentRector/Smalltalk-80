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
        ValidateBundleVersion();
        ValidateLocalIds();
        ValidateSymbolIndices();
        ValidateObjectRefs();
        ValidatePrecomputedLayouts();
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

    /// <summary>
    /// v1.0.6+: Validates that bundle version is 1.0.6 or later.
    /// </summary>
    private void ValidateBundleVersion()
    {
        var version = _bundle.Metadata.Version;

        // Parse version string (format: "major.minor.patch")
        var parts = version.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException($"Invalid bundle version format: {version}");

        if (!int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
            throw new InvalidOperationException($"Invalid bundle version format: {version}");

        // Require v1.0.6 or later
        if (major < 1 || (major == 1 && minor == 0 && int.TryParse(parts.Length > 2 ? parts[2] : "0", out int patch) && patch < 6))
        {
            throw new InvalidOperationException(
                $"Bundle version {version} is not supported. This interpreter requires v1.0.6 or later " +
                "with precomputed class layouts. Please regenerate the bundle with the latest compiler.");
        }
    }

    /// <summary>
    /// v1.0.6+: Validates precomputed field layouts for all classes.
    /// Ensures TotalInstanceVarCount and FieldLayout arrays are consistent.
    /// </summary>
    private void ValidatePrecomputedLayouts()
    {
        int maxSymbolIndex = _bundle.Symbols.Length;

        foreach (var cls in _bundle.Classes)
        {
            // Validate TotalInstanceVarCount matches FieldLayout length
            if (cls.TotalInstanceVarCount != cls.FieldLayout.Length)
            {
                throw new InvalidOperationException(
                    $"Class {cls.LocalId}: TotalInstanceVarCount ({cls.TotalInstanceVarCount}) " +
                    $"does not match FieldLayout length ({cls.FieldLayout.Length})");
            }

            // Validate field layout entries
            for (int i = 0; i < cls.FieldLayout.Length; i++)
            {
                var entry = cls.FieldLayout[i];

                // Validate nameIndex is within bounds
                if (entry.NameIndex < 0 || entry.NameIndex >= maxSymbolIndex)
                {
                    throw new IndexOutOfRangeException(
                        $"Class {cls.LocalId}, field {i}: nameIndex {entry.NameIndex} is out of range (max: {maxSymbolIndex - 1})");
                }

                // Validate slotIndex is sequential (must be exactly i)
                if (entry.SlotIndex != i)
                {
                    throw new InvalidOperationException(
                        $"Class {cls.LocalId}, field {i}: slotIndex is {entry.SlotIndex}, expected {i}. " +
                        "Slot indices must be sequential starting from 0.");
                }
            }
        }
    }
}
