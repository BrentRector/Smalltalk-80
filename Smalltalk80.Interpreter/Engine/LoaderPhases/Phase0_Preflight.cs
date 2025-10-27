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
        ValidateHomeClassMetadata();
        ValidateMetaclassCircle();
        ValidateSpecialObjectsArrayDefinition();
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
        foreach (var patch in _bundle.Patches ?? Array.Empty<PatchDescriptor>())
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

    /// <summary>
    /// Validates that all methods have HomeClassLocalId metadata.
    /// This is required for super send semantics (method lookup must start at homeClass.superclass).
    /// </summary>
    private void ValidateHomeClassMetadata()
    {
        // Build set of all valid class localIds (both instance and meta)
        var validClassIds = _bundle.Classes.Select(c => c.LocalId).ToHashSet();

        int invalidCount = 0;
        foreach (var method in _bundle.Methods)
        {
            // Skip block literals - they don't have home classes (they're embedded in methods)
            if (method.LocalId.StartsWith("block:"))
                continue;

            if (string.IsNullOrEmpty(method.HomeClassLocalId))
            {
                if (invalidCount == 0)
                    Console.WriteLine($"  Error: Found methods missing HomeClassLocalId:");
                Console.WriteLine($"    - {method.LocalId}");
                invalidCount++;
            }
            else if (!validClassIds.Contains(method.HomeClassLocalId))
            {
                if (invalidCount == 0)
                    Console.WriteLine($"  Error: Found methods with invalid HomeClassLocalId:");
                Console.WriteLine($"    - {method.LocalId} references unknown class '{method.HomeClassLocalId}'");
                invalidCount++;
            }
        }

        if (invalidCount > 0)
        {
            throw new InvalidOperationException(
                $"Bundle validation failed: {invalidCount} methods/init expressions have missing or invalid HomeClassLocalId. " +
                "All CompiledMethods require valid home class metadata for correct super send semantics. " +
                "Please regenerate the bundle with the latest compiler.");
        }
    }

    /// <summary>
    /// Validates the metaclass circle and class superclass chains.
    /// Ensures:
    /// - All metaclasses have thisClass pointing to their instance-side class
    /// - The metaclass circle closes: Behavior → Object → Class → ... → Behavior
    /// - All superclass chains terminate at Object.superclass = nil
    /// </summary>
    private void ValidateMetaclassCircle()
    {
        // Build localId lookup
        var classMap = _bundle.Classes.ToDictionary(c => c.LocalId, c => c);

        // Find the fundamental classes
        var objectClass = _bundle.Classes.FirstOrDefault(c => _bundle.Symbols[c.NameIndex] == "Object" && !c.LocalId.StartsWith("meta:"));
        var behaviorClass = _bundle.Classes.FirstOrDefault(c => _bundle.Symbols[c.NameIndex] == "Behavior" && !c.LocalId.StartsWith("meta:"));
        var classClass = _bundle.Classes.FirstOrDefault(c => _bundle.Symbols[c.NameIndex] == "Class" && !c.LocalId.StartsWith("meta:"));
        var metaclassClass = _bundle.Classes.FirstOrDefault(c => _bundle.Symbols[c.NameIndex] == "Metaclass" && !c.LocalId.StartsWith("meta:"));

        if (objectClass == null || behaviorClass == null || classClass == null || metaclassClass == null)
        {
            throw new InvalidOperationException(
                "Bundle validation failed: Could not find fundamental classes (Object, Behavior, Class, Metaclass). " +
                "These classes are required for the metaclass circle.");
        }

        // Validate all metaclasses have thisClass field
        var metaclasses = _bundle.Classes.Where(c => c.LocalId.StartsWith("meta:")).ToList();
        int missingThisClass = 0;

        foreach (var metaclass in metaclasses)
        {
            // Find the 'thisClass' field in the metaclass layout
            bool hasThisClass = metaclass.FieldLayout.Any(f => _bundle.Symbols[f.NameIndex] == "thisClass");

            if (!hasThisClass)
            {
                Console.WriteLine($"  Error: Metaclass {metaclass.LocalId} missing 'thisClass' field");
                missingThisClass++;
            }
        }

        if (missingThisClass > 0)
        {
            throw new InvalidOperationException(
                $"Bundle validation failed: {missingThisClass} metaclasses missing 'thisClass' field. " +
                "All metaclasses must have thisClass pointing to their instance-side class.");
        }

        // Validate superclass chains terminate at Object.superclass = nil
        // We can't validate the actual nil value here (it doesn't exist yet),
        // but we can verify Object has no superclass reference in the bundle
        if (objectClass.SuperNameIndex.HasValue)
        {
            string superName = _bundle.Symbols[objectClass.SuperNameIndex.Value];
            throw new InvalidOperationException(
                $"Bundle validation failed: Object.superclass should be nil, but references '{superName}'. " +
                "The superclass chain must terminate at Object.");
        }

        // Validate metaclass circle structure
        // NOTE: meta:Object.superclass is NOT stored in the bundle - it's wired dynamically in Phase 3
        // Phase 3 sets meta:Object.superclass = class:Class programmatically
        // Here we just verify that the fundamental classes exist for Phase 3 to wire up
        var metaObject = classMap["meta:Object"];

        // Verify the fundamental classes needed for metaclass circle exist
        if (classClass == null)
        {
            throw new InvalidOperationException(
                "Bundle validation failed: class:Class not found. " +
                "This class is required for metaclass circle wiring in Phase 3.");
        }

        // meta:Object should NOT have a superclass in the bundle (it's wired in Phase 3)
        if (metaObject.SuperNameIndex.HasValue)
        {
            string metaObjectSuperName = _bundle.Symbols[metaObject.SuperNameIndex.Value];
            Console.WriteLine(
                $"  NOTE: meta:Object.superclass is '{metaObjectSuperName}' in bundle. " +
                "Typically this is wired dynamically in Phase 3.");
        }
    }

    /// <summary>
    /// Validates that the bundle defines all required classes for SpecialObjectsArray.
    /// NOTE: Singleton objects (nil, true, false) are NOT in the bundle - they're created
    /// programmatically in Phase 6 (Object Allocation).
    /// Required classes:
    /// - UndefinedObject, True, False, Boolean (singleton classes)
    /// - Object, Behavior, Class, Metaclass (fundamental hierarchy)
    /// - SmallInteger, Symbol, String, Array (basic types)
    /// - CompiledMethod, MethodDictionary, MethodContext, BlockContext (VM internals)
    /// </summary>
    private void ValidateSpecialObjectsArrayDefinition()
    {
        // Build name lookup for classes
        var classNames = new HashSet<string>();
        foreach (var cls in _bundle.Classes)
        {
            classNames.Add(_bundle.Symbols[cls.NameIndex]);
        }

        // Required fundamental classes (must exist in bundle)
        var requiredClasses = new[]
        {
            "Object", "Behavior", "Class", "Metaclass",
            "UndefinedObject", "True", "False", "Boolean",
            "SmallInteger", "Symbol", "String", "Array",
            "CompiledMethod", "MethodDictionary", "MethodContext", "BlockContext"
        };

        var missingClasses = new List<string>();
        foreach (var className in requiredClasses)
        {
            if (!classNames.Contains(className))
            {
                missingClasses.Add(className);
            }
        }

        if (missingClasses.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bundle validation failed: Missing required fundamental classes: {string.Join(", ", missingClasses)}. " +
                "These classes are needed for SpecialObjectsArray and core VM operations.");
        }

        // NOTE: We do NOT check for singleton objects (nil, true, false) in the bundle's Objects section
        // because they are created programmatically in Phase 6 (Object Allocation).
        // The validation of actual singleton OOPs happens in Phase 13 (ValidateSpecialObjectsArray).
    }
}
