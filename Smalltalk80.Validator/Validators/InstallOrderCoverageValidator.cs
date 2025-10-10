using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Enhanced install order validation that ensures:
/// - All entities appear exactly once in install order
/// - Phase kinds match the entity types they contain
/// - Phases are in correct dependency order
/// - No orphaned or missing entities
/// </summary>
public class InstallOrderCoverageValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Track all localIds that have been installed
        var installedLocalIds = new HashSet<string>();
        var phaseIndex = 0;
        
        foreach (var phase in bundle.InstallOrder.Phases)
        {
            phaseIndex++;
            var phaseName = phase.Name ?? $"Phase {phaseIndex}";
            var phaseKind = phase.Kind ?? "unknown";
            
            foreach (var localId in phase.LocalIds)
            {
                // Check for duplicates
                if (installedLocalIds.Contains(localId))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.InstallDuplicate,
                        Severity.Error,
                        $"LocalId '{localId}' appears multiple times in install order",
                        new DiagnosticLocation("installOrder", localId, phaseIndex),
                        "Ensure each entity appears exactly once");
                    continue;
                }
                installedLocalIds.Add(localId);
                
                // Check that localId exists
                if (!context.LocalIdExists(localId))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.InstallMissing,
                        Severity.Error,
                        $"Phase '{phaseName}' references non-existent localId '{localId}'",
                        new DiagnosticLocation("installOrder", localId, phaseIndex),
                        "Remove reference or add the missing entity");
                    continue;
                }
                
                // Validate phase.kind matches entity type
                ValidatePhaseKindMatch(result, context, phase, localId, phaseName);
            }
        }
        
        // Check for entities not in install order
        CheckMissingEntities(result, context, installedLocalIds);
        
        // Validate phase ordering
        ValidatePhaseOrdering(result, bundle.InstallOrder.Phases);
        
        // Add summary info
        var totalEntities = context.LocalIdIndex.Count;
        var installedCount = installedLocalIds.Count;
        
        if (installedCount == totalEntities && result.IsValid)
        {
            result.AddInfo($"✓ Install order covers all {totalEntities} entities with correct phase kinds");
        }
        else
        {
            result.AddInfo($"Install order covers {installedCount}/{totalEntities} entities");
        }
        
        return result;
    }
    
    private void ValidatePhaseKindMatch(ValidationResult result, ValidationContext context, 
        InstallPhase phase, string localId, string phaseName)
    {
        var phaseKind = phase.Kind ?? "";
        var occurrences = context.LocalIdIndex[localId];
        var actualSection = occurrences.First().Section;
        
        // Determine expected kind based on entity type
        bool isValid = (phaseKind, actualSection, localId) switch
        {
            // Classes phase should contain classes and metaclasses
            ("classes", "classes", _) => true,
            
            // Methods phase should contain methods and blocks
            ("methods", "methods", _) => true,
            
            // Objects phase should contain objects
            ("objects", "objects", _) => true,
            
            // Init expressions phase
            ("initExpressions", "initExpressions", _) => true,
            ("inits", "initExpressions", _) => true, // Allow alternate naming
            
            // Patches phase
            ("patches", "patches", _) => true,
            
            // Special phases
            ("bootstrap", _, _) => true, // Bootstrap can contain anything
            ("core", "classes", _) => true, // Core usually has fundamental classes
            ("kernel", "classes", _) => true, // Kernel classes
            
            // Check localId prefix as fallback
            (_, "classes", var id) when id.StartsWith("class:") || id.StartsWith("meta:") => 
                phaseKind == "classes" || phaseKind == "bootstrap" || phaseKind == "core",
            
            (_, "methods", var id) when id.StartsWith("method:") || id.StartsWith("block:") => 
                phaseKind == "methods",
            
            (_, "objects", var id) when id.StartsWith("object:") || id.StartsWith("assoc:") || 
                                        id.StartsWith("pool:") || id.StartsWith("blockctx:") =>
                phaseKind == "objects",
                
            (_, "initExpressions", var id) when id.StartsWith("init:") =>
                phaseKind == "initExpressions" || phaseKind == "inits",
                
            (_, "patches", var id) when id.StartsWith("patch:") =>
                phaseKind == "patches",
            
            _ => false
        };
        
        if (!isValid && !string.IsNullOrEmpty(phaseKind))
        {
            result.AddDiagnostic(
                DiagnosticCodes.InstallKindMismatch,
                Severity.Error,
                $"Phase '{phaseName}' has kind='{phaseKind}' but contains {actualSection} entity '{localId}'",
                new DiagnosticLocation("installOrder", localId, Field: "kind"),
                $"Change phase kind to '{actualSection}' or move entity to correct phase");
        }
    }
    
    private void CheckMissingEntities(ValidationResult result, ValidationContext context, 
        HashSet<string> installedLocalIds)
    {
        foreach (var kvp in context.LocalIdIndex)
        {
            var localId = kvp.Key;
            if (!installedLocalIds.Contains(localId))
            {
                var occurrence = kvp.Value.First();
                result.AddDiagnostic(
                    DiagnosticCodes.InstallOmitted,
                    Severity.Error,
                    $"{occurrence.Section} entity '{localId}' is not in any install phase",
                    new DiagnosticLocation(occurrence.Section, localId, occurrence.Index),
                    "Add entity to appropriate install phase");
            }
        }
    }
    
    private void ValidatePhaseOrdering(ValidationResult result, InstallPhase[] phases)
    {
        // Typical expected order: classes -> methods -> objects -> initExpressions -> patches
        // But allow flexibility for bootstrap and other special phases
        
        var classesIndex = -1;
        var methodsIndex = -1;
        var objectsIndex = -1;
        var initsIndex = -1;
        var patchesIndex = -1;
        
        for (int i = 0; i < phases.Length; i++)
        {
            var kind = phases[i].Kind ?? "";
            switch (kind)
            {
                case "classes":
                    if (classesIndex == -1) classesIndex = i;
                    break;
                case "methods":
                    if (methodsIndex == -1) methodsIndex = i;
                    break;
                case "objects":
                    if (objectsIndex == -1) objectsIndex = i;
                    break;
                case "initExpressions":
                case "inits":
                    if (initsIndex == -1) initsIndex = i;
                    break;
                case "patches":
                    if (patchesIndex == -1) patchesIndex = i;
                    break;
            }
        }
        
        // Check ordering constraints
        if (methodsIndex != -1 && classesIndex != -1 && methodsIndex < classesIndex)
        {
            result.AddDiagnostic(
                DiagnosticCodes.InstallKindMismatch,
                Severity.Warning,
                "Methods phase appears before classes phase",
                new DiagnosticLocation("installOrder", Index: methodsIndex),
                "Classes should be installed before their methods");
        }
        
        if (objectsIndex != -1 && classesIndex != -1 && objectsIndex < classesIndex)
        {
            result.AddDiagnostic(
                DiagnosticCodes.InstallKindMismatch,
                Severity.Warning,
                "Objects phase appears before classes phase",
                new DiagnosticLocation("installOrder", Index: objectsIndex),
                "Classes should be installed before their instances");
        }
        
        if (initsIndex != -1 && objectsIndex != -1 && initsIndex < objectsIndex)
        {
            result.AddDiagnostic(
                DiagnosticCodes.InstallKindMismatch,
                Severity.Warning,
                "Init expressions phase appears before objects phase",
                new DiagnosticLocation("installOrder", Index: initsIndex),
                "Objects should exist before initialization code runs");
        }
    }
}