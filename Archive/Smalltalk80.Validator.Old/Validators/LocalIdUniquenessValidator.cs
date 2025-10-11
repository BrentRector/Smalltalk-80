using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates that all localIds are unique across the bundle and follow naming conventions.
/// Duplicate localIds break allocation mapping and patch resolution.
/// </summary>
public class LocalIdUniquenessValidator
{
    private static readonly string[] AllowedPrefixes = new[]
    {
        "class:",
        "meta:",
        "method:",
        "object:",
        "assoc:",
        "pool:",
        "blockctx:",
        "init:",
        "patch:",
        "block:"  // For block compiled methods
    };
    
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Check for duplicates
        foreach (var kvp in context.LocalIdIndex)
        {
            var localId = kvp.Key;
            var occurrences = kvp.Value;
            
            if (occurrences.Count > 1)
            {
                var locations = occurrences
                    .Select(o => new DiagnosticLocation(o.Section, localId, o.Index))
                    .ToList();
                
                result.AddDiagnostic(new Diagnostic(
                    DiagnosticCodes.LocalIdDuplicate,
                    Severity.Error,
                    $"LocalId '{localId}' appears {occurrences.Count} times across sections",
                    locations,
                    "Ensure compiler emits unique localIds"));
            }
        }
        
        // Check naming conventions
        foreach (var localId in context.LocalIdIndex.Keys)
        {
            if (!HasValidPrefix(localId))
            {
                var occurrence = context.LocalIdIndex[localId].First();
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMalformed,
                    Severity.Warning,
                    $"LocalId '{localId}' uses non-standard prefix",
                    new DiagnosticLocation(occurrence.Section, localId, occurrence.Index),
                    $"Use canonical prefixes: {string.Join(", ", AllowedPrefixes)}");
            }
            
            // Additional validation: check prefix matches section
            ValidatePrefixMatchesSection(result, context, localId);
        }
        
        // Check for empty localIds
        CheckForEmptyLocalIds(result, bundle);
        
        if (result.IsValid)
        {
            result.AddInfo($"✓ All {context.LocalIdIndex.Count} localIds are unique and well-formed");
        }
        
        return result;
    }
    
    private bool HasValidPrefix(string localId)
    {
        return AllowedPrefixes.Any(prefix => localId.StartsWith(prefix, StringComparison.Ordinal));
    }
    
    private void ValidatePrefixMatchesSection(ValidationResult result, ValidationContext context, string localId)
    {
        var occurrence = context.LocalIdIndex[localId].First();
        var section = occurrence.Section;
        
        // Check if prefix is consistent with section
        bool isValid = (section, localId) switch
        {
            ("classes", var id) when id.StartsWith("class:") || id.StartsWith("meta:") => true,
            ("methods", var id) when id.StartsWith("method:") || id.StartsWith("block:") => true,
            ("objects", var id) when id.StartsWith("object:") || id.StartsWith("assoc:") || 
                                     id.StartsWith("pool:") || id.StartsWith("blockctx:") => true,
            ("initExpressions", var id) when id.StartsWith("init:") => true,
            ("patches", var id) when id.StartsWith("patch:") => true,
            _ => false
        };
        
        if (!isValid)
        {
            // Extract prefix from localId
            var colonIndex = localId.IndexOf(':');
            var prefix = colonIndex >= 0 ? localId.Substring(0, colonIndex + 1) : "<no-prefix>";
            
            result.AddDiagnostic(
                DiagnosticCodes.LocalIdMalformed,
                Severity.Warning,
                $"LocalId prefix '{prefix}' unexpected in section '{section}'",
                new DiagnosticLocation(section, localId, occurrence.Index),
                $"LocalId '{localId}' in section '{section}' has unexpected prefix");
        }
    }
    
    private void CheckForEmptyLocalIds(ValidationResult result, ColdStartBundle bundle)
    {
        // Check classes
        for (int i = 0; i < bundle.Classes.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(bundle.Classes[i].LocalId))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMissing,
                    Severity.Error,
                    $"Class at index {i} has empty or missing localId",
                    new DiagnosticLocation("classes", Index: i),
                    "All classes must have non-empty localIds");
            }
        }
        
        // Check methods
        for (int i = 0; i < bundle.Methods.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(bundle.Methods[i].LocalId))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMissing,
                    Severity.Error,
                    $"Method at index {i} has empty or missing localId",
                    new DiagnosticLocation("methods", Index: i),
                    "All methods must have non-empty localIds");
            }
        }
        
        // Check objects
        for (int i = 0; i < bundle.Objects.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(bundle.Objects[i].LocalId))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMissing,
                    Severity.Error,
                    $"Object at index {i} has empty or missing localId",
                    new DiagnosticLocation("objects", Index: i),
                    "All objects must have non-empty localIds");
            }
        }
        
        // Check init expressions
        for (int i = 0; i < bundle.InitExpressions.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(bundle.InitExpressions[i].LocalId))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMissing,
                    Severity.Error,
                    $"InitExpression at index {i} has empty or missing localId",
                    new DiagnosticLocation("initExpressions", Index: i),
                    "All init expressions must have non-empty localIds");
            }
        }
        
        // Check patches
        for (int i = 0; i < bundle.Patches.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(bundle.Patches[i].PatchId))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.LocalIdMissing,
                    Severity.Error,
                    $"Patch at index {i} has empty or missing patchId",
                    new DiagnosticLocation("patches", Index: i),
                    "All patches must have non-empty patchIds");
            }
        }
    }
}