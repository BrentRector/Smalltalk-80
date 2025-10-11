using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates class hierarchy sanity:
/// - Superclass references are valid
/// - No cycles in inheritance chain
/// - Root classes properly marked (nil superclass)
/// - Metaclass hierarchy mirrors instance hierarchy
/// </summary>
public class ClassHierarchySanityValidator
{
    // Known root classes in Smalltalk-80 that should have nil superclass
    private static readonly HashSet<string> KnownRootClasses = new()
    {
        "Object",
        "UndefinedObject",  // nil's class
        "ProtoObject"       // In some implementations
    };
    
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Build class hierarchy maps
        var superclassMap = new Dictionary<string, string>(); // class name -> superclass name
        var classNameToLocalId = new Dictionary<string, string>(); // class name -> localId
        
        // First pass: build maps
        foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
        {
            var className = context.GetSymbol(cls.NameIndex);
            if (className != null)
            {
                classNameToLocalId[className] = cls.LocalId;
                
                if (cls.SuperNameIndex.HasValue && cls.SuperNameIndex.Value >= 0)
                {
                    var superName = context.GetSymbol(cls.SuperNameIndex.Value);
                    if (superName != null)
                    {
                        superclassMap[className] = superName;
                    }
                }
            }
        }
        
        // Validate each class
        foreach (var cls in bundle.Classes)
        {
            if (cls.IsMetaclass)
            {
                ValidateMetaclass(result, context, cls, bundle);
            }
            else
            {
                ValidateInstanceClass(result, context, cls, superclassMap, classNameToLocalId);
            }
        }
        
        // Check for inheritance cycles
        DetectInheritanceCycles(result, superclassMap);
        
        // Validate root classes
        ValidateRootClasses(result, context, bundle, superclassMap);
        
        // Add summary
        var classCount = bundle.Classes.Count(c => !c.IsMetaclass);
        var metaclassCount = bundle.Classes.Count(c => c.IsMetaclass);
        
        if (result.IsValid)
        {
            result.AddInfo($"✓ Class hierarchy validated: {classCount} classes, {metaclassCount} metaclasses, no cycles");
        }
        else
        {
            result.AddInfo($"Validated {classCount} classes and {metaclassCount} metaclasses");
        }
        
        return result;
    }
    
    private void ValidateInstanceClass(ValidationResult result, ValidationContext context,
        ClassDefinition cls, Dictionary<string, string> superclassMap, 
        Dictionary<string, string> classNameToLocalId)
    {
        var className = context.GetSymbol(cls.NameIndex);
        if (className == null) return;
        
        // Check superclass reference
        if (cls.SuperNameIndex.HasValue && cls.SuperNameIndex.Value >= 0)
        {
            var superName = context.GetSymbol(cls.SuperNameIndex.Value);
            if (superName == null)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.ClassSuperMissing,
                    Severity.Error,
                    $"Class '{className}' has invalid superclass index {cls.SuperNameIndex.Value}",
                    new DiagnosticLocation("classes", cls.LocalId),
                    "Superclass index must reference a valid symbol");
                return;
            }
            
            // Check if superclass exists in the bundle
            if (!classNameToLocalId.ContainsKey(superName) && !KnownRootClasses.Contains(superName))
            {
                // Check if it might be a system class not in the bundle
                if (!IsSystemClass(superName))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.ClassSuperMissing,
                        Severity.Error,
                        $"Class '{className}' references non-existent superclass '{superName}'",
                        new DiagnosticLocation("classes", cls.LocalId),
                        "Superclass must exist in the bundle or be a known system class");
                }
            }
            
            // Warn about unusual inheritance patterns
            if (className == superName)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.ClassSuperCycle,
                    Severity.Error,
                    $"Class '{className}' cannot be its own superclass",
                    new DiagnosticLocation("classes", cls.LocalId),
                    "Remove self-reference in superclass");
            }
        }
        else if (!KnownRootClasses.Contains(className))
        {
            // Class has no superclass but isn't a known root
            result.AddDiagnostic(
                DiagnosticCodes.ClassSuperMissing,
                Severity.Warning,
                $"Class '{className}' has no superclass (only Object should be a root class)",
                new DiagnosticLocation("classes", cls.LocalId),
                "Most classes should inherit from Object or another class");
        }
    }
    
    private void ValidateMetaclass(ValidationResult result, ValidationContext context,
        ClassDefinition metaclass, ColdStartBundle bundle)
    {
        var metaclassName = context.GetSymbol(metaclass.NameIndex);
        if (metaclassName == null) return;
        
        // Metaclass should have a corresponding instance class
        var expectedInstanceClass = bundle.Classes.FirstOrDefault(c => 
            !c.IsMetaclass && c.NameIndex == metaclass.NameIndex);
        
        if (expectedInstanceClass == null)
        {
            result.AddDiagnostic(
                DiagnosticCodes.ClassSuperMissing,
                Severity.Error,
                $"Metaclass '{metaclass.LocalId}' has no corresponding instance class",
                new DiagnosticLocation("classes", metaclass.LocalId),
                "Every metaclass must have a corresponding instance class");
        }
        
        // Validate metaclass superclass follows instance hierarchy
        if (metaclass.SuperNameIndex.HasValue && expectedInstanceClass != null)
        {
            var metaSuperName = context.GetSymbol(metaclass.SuperNameIndex.Value);
            if (expectedInstanceClass.SuperNameIndex.HasValue)
            {
                var instanceSuperName = context.GetSymbol(expectedInstanceClass.SuperNameIndex.Value);
                
                // Metaclass hierarchy should mirror instance hierarchy
                // MetaA's super should be MetaB if A's super is B
                if (metaSuperName != null && instanceSuperName != null &&
                    !metaSuperName.Contains(instanceSuperName))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.ClassSuperMissing,
                        Severity.Warning,
                        $"Metaclass hierarchy inconsistent: {metaclassName} class super is {metaSuperName} " +
                        $"but {metaclassName} super is {instanceSuperName}",
                        new DiagnosticLocation("classes", metaclass.LocalId),
                        "Metaclass hierarchy should mirror instance hierarchy");
                }
            }
        }
    }
    
    private void DetectInheritanceCycles(ValidationResult result, Dictionary<string, string> superclassMap)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        
        foreach (var className in superclassMap.Keys)
        {
            if (!visited.Contains(className))
            {
                if (DetectCycleHelper(className, superclassMap, visited, recursionStack, result))
                {
                    // Cycle detected and reported
                }
            }
        }
    }
    
    private bool DetectCycleHelper(string className, Dictionary<string, string> superclassMap,
        HashSet<string> visited, HashSet<string> recursionStack, ValidationResult result)
    {
        visited.Add(className);
        recursionStack.Add(className);
        
        if (superclassMap.TryGetValue(className, out var superName))
        {
            if (recursionStack.Contains(superName))
            {
                // Found a cycle
                result.AddDiagnostic(
                    DiagnosticCodes.ClassSuperCycle,
                    Severity.Error,
                    $"Inheritance cycle detected: '{className}' -> '{superName}' forms a cycle",
                    new DiagnosticLocation("classes"),
                    "Remove cycle from inheritance chain");
                return true;
            }
            
            if (!visited.Contains(superName))
            {
                if (DetectCycleHelper(superName, superclassMap, visited, recursionStack, result))
                {
                    return true;
                }
            }
        }
        
        recursionStack.Remove(className);
        return false;
    }
    
    private void ValidateRootClasses(ValidationResult result, ValidationContext context,
        ColdStartBundle bundle, Dictionary<string, string> superclassMap)
    {
        // Find classes with no superclass
        var rootClasses = new List<string>();
        
        foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
        {
            var className = context.GetSymbol(cls.NameIndex);
            if (className != null && !superclassMap.ContainsKey(className))
            {
                rootClasses.Add(className);
            }
        }
        
        // Check if we have the expected root class
        if (!rootClasses.Any(r => KnownRootClasses.Contains(r)))
        {
            result.AddDiagnostic(
                DiagnosticCodes.ClassSuperMissing,
                Severity.Warning,
                $"No standard root class (Object) found among root classes: {string.Join(", ", rootClasses)}",
                new DiagnosticLocation("classes"),
                "System should have Object as root class");
        }
        
        // Warn about multiple root classes (unusual but not necessarily wrong)
        if (rootClasses.Count > 1)
        {
            var unexpectedRoots = rootClasses.Where(r => !KnownRootClasses.Contains(r)).ToList();
            if (unexpectedRoots.Any())
            {
                result.AddDiagnostic(
                    DiagnosticCodes.ClassSuperMissing,
                    Severity.Warning,
                    $"Multiple root classes found: {string.Join(", ", rootClasses)}",
                    new DiagnosticLocation("classes"),
                    "Usually only Object should be a root class");
            }
        }
    }
    
    private bool IsSystemClass(string className)
    {
        // Common system classes that might not be in the bundle
        var systemClasses = new HashSet<string>
        {
            "Object", "Class", "Metaclass", "Behavior", 
            "ClassDescription", "MethodDictionary", "CompiledMethod",
            "ProtoObject", "nil", "true", "false"
        };
        
        return systemClasses.Contains(className);
    }
}