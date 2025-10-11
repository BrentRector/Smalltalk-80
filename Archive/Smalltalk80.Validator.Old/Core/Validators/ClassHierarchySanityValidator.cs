using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates class hierarchy sanity:
/// - Superclass references are valid
/// - No cycles in inheritance chain
/// - Root classes properly marked (nil superclass)
/// - Metaclass hierarchy mirrors instance hierarchy
/// </summary>
public class ClassHierarchySanityValidator : IValidator
{
    private static readonly HashSet<string> KnownRootClasses = new() { "Object", "UndefinedObject", "ProtoObject" };

    public string Id => "ClassHierarchySanityValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Build class hierarchy maps
        var superclassMap = new Dictionary<string, string>(); // class name -> superclass name
        var classNameToLocalId = new Dictionary<string, string>(); // class name -> localId

        // First pass: build maps
        foreach (var cls in ctx.Classes)
        {
            var isMetaclass = cls.GetBoolProp("isMetaclass") ?? false;
            if (isMetaclass) continue;

            var nameIndex = cls.GetIntProp("nameIndex");
            if (!nameIndex.HasValue) continue;

            var className = ctx.Symbols.ElementAtOrDefault(nameIndex.Value);
            if (className == null) continue;

            var localId = cls.GetStringProp("localId") ?? "unknown";
            classNameToLocalId[className] = localId;

            var superNameIndex = cls.GetIntProp("superNameIndex");
            if (superNameIndex.HasValue && superNameIndex.Value >= 0)
            {
                var superName = ctx.Symbols.ElementAtOrDefault(superNameIndex.Value);
                if (superName != null)
                {
                    superclassMap[className] = superName;
                }
            }
        }

        // Validate each class
        foreach (var cls in ctx.Classes)
        {
            var isMetaclass = cls.GetBoolProp("isMetaclass") ?? false;
            var localId = cls.GetStringProp("localId") ?? "unknown";
            var nameIndex = cls.GetIntProp("nameIndex");
            if (!nameIndex.HasValue) continue;

            var className = ctx.Symbols.ElementAtOrDefault(nameIndex.Value);
            if (className == null) continue;

            if (isMetaclass)
            {
                ValidateMetaclass(ctx, cls, diagnostics);
            }
            else
            {
                ValidateInstanceClass(ctx, cls, superclassMap, classNameToLocalId, diagnostics);
            }
        }

        // Check for inheritance cycles
        DetectInheritanceCycles(superclassMap, diagnostics);

        // Validate root classes
        ValidateRootClasses(ctx, superclassMap, diagnostics);

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateInstanceClass(ValidationContext ctx, JsonElement cls,
        Dictionary<string, string> superclassMap, Dictionary<string, string> classNameToLocalId,
        List<Diagnostic> diagnostics)
    {
        var localId = cls.GetStringProp("localId") ?? "unknown";
        var nameIndex = cls.GetIntProp("nameIndex");
        if (!nameIndex.HasValue) return;

        var className = ctx.Symbols.ElementAtOrDefault(nameIndex.Value);
        if (className == null) return;

        // Check superclass reference
        var superNameIndex = cls.GetIntProp("superNameIndex");
        if (superNameIndex.HasValue && superNameIndex.Value >= 0)
        {
            var superName = ctx.Symbols.ElementAtOrDefault(superNameIndex.Value);
            if (superName == null)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ClassSuperMissing,
                    Severity.Error,
                    $"Class '{className}' has invalid superclass index {superNameIndex.Value}",
                    new[] { new Location("classes", localId) },
                "Superclass index must reference a valid symbol"));
                return;
            }

            // Check if superclass exists in the bundle
            if (!classNameToLocalId.ContainsKey(superName) && !IsSystemClass(superName))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ClassSuperMissing,
                    Severity.Error,
                    $"Class '{className}' references non-existent superclass '{superName}'",
                    new[] { new Location("classes", localId) },
                "Superclass must exist in the bundle or be a known system class"));
            }

            // Warn about unusual inheritance patterns
            if (className == superName)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ClassSuperCycle,
                    Severity.Error,
                    $"Class '{className}' cannot be its own superclass",
                    new[] { new Location("classes", localId) },
                "Remove self-reference in superclass"));
            }
        }
        else if (!KnownRootClasses.Contains(className))
        {
            // Class has no superclass but isn't a known root
            diagnostics.Add(new Diagnostic(
                ErrorCodes.ClassSuperMissing,
                Severity.Warning,
                $"Class '{className}' has no superclass (only Object should be a root class)",
                new[] { new Location("classes", localId) },
                "Most classes should inherit from Object or another class"));
        }
    }

    private void ValidateMetaclass(ValidationContext ctx, JsonElement metaclass, List<Diagnostic> diagnostics)
    {
        var localId = metaclass.GetStringProp("localId") ?? "unknown";
        var nameIndex = metaclass.GetIntProp("nameIndex");
        if (!nameIndex.HasValue) return;

        var metaclassName = ctx.Symbols.ElementAtOrDefault(nameIndex.Value);
        if (metaclassName == null) return;

        // Metaclass should have a corresponding instance class
        var expectedInstanceClass = ctx.Classes.FirstOrDefault(c =>
        {
            var isMetaclass = c.GetBoolProp("isMetaclass") ?? false;
            var cNameIndex = c.GetIntProp("nameIndex");
            return !isMetaclass && cNameIndex.HasValue && cNameIndex.Value == nameIndex.Value;
        });

        if (expectedInstanceClass.ValueKind == JsonValueKind.Undefined)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.ClassSuperMissing,
                Severity.Error,
                $"Metaclass '{localId}' has no corresponding instance class",
                new[] { new Location("classes", localId) },
                "Every metaclass must have a corresponding instance class"));
        }
    }

    private void DetectInheritanceCycles(Dictionary<string, string> superclassMap, List<Diagnostic> diagnostics)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var className in superclassMap.Keys)
        {
            if (!visited.Contains(className))
            {
                DetectCycleHelper(className, superclassMap, visited, recursionStack, diagnostics);
            }
        }
    }

    private bool DetectCycleHelper(string className, Dictionary<string, string> superclassMap,
        HashSet<string> visited, HashSet<string> recursionStack, List<Diagnostic> diagnostics)
    {
        visited.Add(className);
        recursionStack.Add(className);

        if (superclassMap.TryGetValue(className, out var superName))
        {
            if (recursionStack.Contains(superName))
            {
                // Found a cycle
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ClassSuperCycle,
                    Severity.Error,
                    $"Inheritance cycle detected: '{className}' -> '{superName}' forms a cycle",
                    new[] { new Location("classes") },
                "Remove cycle from inheritance chain"));
                recursionStack.Remove(className);
                return true;
            }

            if (!visited.Contains(superName))
            {
                if (DetectCycleHelper(superName, superclassMap, visited, recursionStack, diagnostics))
                {
                    recursionStack.Remove(className);
                    return true;
                }
            }
        }

        recursionStack.Remove(className);
        return false;
    }

    private void ValidateRootClasses(ValidationContext ctx, Dictionary<string, string> superclassMap,
        List<Diagnostic> diagnostics)
    {
        // Find classes with no superclass
        var rootClasses = new List<string>();

        foreach (var cls in ctx.Classes)
        {
            var isMetaclass = cls.GetBoolProp("isMetaclass") ?? false;
            if (isMetaclass) continue;

            var nameIndex = cls.GetIntProp("nameIndex");
            if (!nameIndex.HasValue) continue;

            var className = ctx.Symbols.ElementAtOrDefault(nameIndex.Value);
            if (className != null && !superclassMap.ContainsKey(className))
            {
                rootClasses.Add(className);
            }
        }

        // Check if we have the expected root class
        if (!rootClasses.Any(r => KnownRootClasses.Contains(r)))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.ClassSuperMissing,
                Severity.Warning,
                $"No standard root class (Object) found among root classes: {string.Join(", ", rootClasses)}",
                new[] { new Location("classes") },
                "System should have Object as root class"));
        }

        // Warn about multiple root classes
        if (rootClasses.Count > 1)
        {
            var unexpectedRoots = rootClasses.Where(r => !KnownRootClasses.Contains(r)).ToList();
            if (unexpectedRoots.Any())
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ClassSuperMissing,
                    Severity.Warning,
                    $"Multiple root classes found: {string.Join(", ", rootClasses)}",
                    new[] { new Location("classes") },
                "Usually only Object should be a root class"));
            }
        }
    }

    private bool IsSystemClass(string className)
    {
        var systemClasses = new HashSet<string>
        {
            "Object", "Class", "Metaclass", "Behavior",
            "ClassDescription", "MethodDictionary", "CompiledMethod",
            "ProtoObject", "nil", "true", "false"
        };

        return systemClasses.Contains(className);
    }
}
