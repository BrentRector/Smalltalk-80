using System.Text.Json;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates that patch targets exist and operations are valid:
/// - Target localIds must exist
/// - Target field indices must be in range
/// - Operations must be valid for target types
/// - Values must be appropriate for operations
/// </summary>
public class PatchTargetExistenceValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        foreach (var patch in bundle.Patches)
        {
            ValidatePatch(result, context, patch, bundle);
        }
        
        if (result.IsValid && bundle.Patches.Length > 0)
        {
            result.AddInfo($"✓ All {bundle.Patches.Length} patch targets validated successfully");
        }
        else
        {
            result.AddInfo($"Validated {bundle.Patches.Length} patches");
        }
        
        return result;
    }
    
    private void ValidatePatch(ValidationResult result, ValidationContext context, 
        Patch patch, ColdStartBundle bundle)
    {
        // Validate patch has required fields
        if (string.IsNullOrEmpty(patch.PatchId))
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                "Patch missing patchId",
                new DiagnosticLocation("patches"),
                "All patches must have a patchId");
            return;
        }
        
        if (string.IsNullOrEmpty(patch.Operation))
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patch.PatchId}' missing operation",
                new DiagnosticLocation("patches", patch.PatchId),
                "All patches must specify an operation");
            return;
        }
        
        // Parse target structure
        if (patch.Target == null)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchTargetMissing,
                Severity.Error,
                $"Patch '{patch.PatchId}' has null target",
                new DiagnosticLocation("patches", patch.PatchId),
                "Patch must specify a target");
            return;
        }
        
        // Target can be a complex object with different structures
        ValidatePatchTarget(result, context, patch, bundle);
        
        // Validate operation is appropriate for target
        ValidatePatchOperation(result, context, patch);
    }
    
    private void ValidatePatchTarget(ValidationResult result, ValidationContext context,
        Patch patch, ColdStartBundle bundle)
    {
        if (patch.Target is JsonElement targetJson)
        {
            if (targetJson.ValueKind == JsonValueKind.Object)
            {
                // Extract target information based on common patterns
                string? targetKind = null;
                string? targetLocalId = null;
                int? fieldIndex = null;
                int? literalIndex = null;
                
                if (targetJson.TryGetProperty("kind", out var kindProp))
                {
                    targetKind = kindProp.GetString();
                }
                
                if (targetJson.TryGetProperty("localId", out var localIdProp))
                {
                    targetLocalId = localIdProp.GetString();
                }
                else if (targetJson.TryGetProperty("classLocalId", out var classIdProp))
                {
                    targetLocalId = classIdProp.GetString();
                }
                else if (targetJson.TryGetProperty("methodLocalId", out var methodIdProp))
                {
                    targetLocalId = methodIdProp.GetString();
                }
                else if (targetJson.TryGetProperty("objectLocalId", out var objectIdProp))
                {
                    targetLocalId = objectIdProp.GetString();
                }
                
                if (targetJson.TryGetProperty("fieldIndex", out var fieldProp))
                {
                    fieldIndex = fieldProp.GetInt32();
                }
                
                if (targetJson.TryGetProperty("literalIndex", out var literalProp))
                {
                    literalIndex = literalProp.GetInt32();
                }
                
                // Validate based on target kind
                if (targetKind != null)
                {
                    ValidateByTargetKind(result, context, patch, targetKind, targetLocalId, 
                        fieldIndex, literalIndex, bundle);
                }
                else if (targetLocalId != null)
                {
                    // Generic localId validation
                    if (!context.LocalIdExists(targetLocalId))
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Patch '{patch.PatchId}' targets non-existent localId '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Target localId must exist");
                    }
                }
            }
            else if (targetJson.ValueKind == JsonValueKind.String)
            {
                // Simple string target (probably a localId)
                var targetLocalId = targetJson.GetString();
                if (!string.IsNullOrEmpty(targetLocalId) && !context.LocalIdExists(targetLocalId))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchTargetMissing,
                        Severity.Error,
                        $"Patch '{patch.PatchId}' targets non-existent localId '{targetLocalId}'",
                        new DiagnosticLocation("patches", patch.PatchId),
                        "Target localId must exist");
                }
            }
        }
    }
    
    private void ValidateByTargetKind(ValidationResult result, ValidationContext context,
        Patch patch, string targetKind, string? targetLocalId, int? fieldIndex, int? literalIndex,
        ColdStartBundle bundle)
    {
        switch (targetKind)
        {
            case "metaclassWiring":
                // Validate metaclass exists
                if (!string.IsNullOrEmpty(targetLocalId))
                {
                    if (!context.LocalIdExists(targetLocalId))
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Patch '{patch.PatchId}' targets non-existent metaclass '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Metaclass must exist");
                    }
                    else if (!targetLocalId.StartsWith("meta:"))
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchInvalid,
                            Severity.Warning,
                            $"Patch '{patch.PatchId}' metaclass wiring targets non-metaclass '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Metaclass wiring should target meta: prefixed classes");
                    }
                }
                break;
                
            case "methodLiteral":
                // Validate method exists and literal index is in range
                if (!string.IsNullOrEmpty(targetLocalId))
                {
                    if (context.MethodByLocalId.TryGetValue(targetLocalId, out var method))
                    {
                        if (literalIndex.HasValue && literalIndex.Value >= method.Literals.Count)
                        {
                            result.AddDiagnostic(
                                DiagnosticCodes.PatchInvalid,
                                Severity.Error,
                                $"Patch '{patch.PatchId}' literal index {literalIndex} out of range " +
                                $"(method has {method.Literals.Count} literals)",
                                new DiagnosticLocation("patches", patch.PatchId),
                                $"Literal index must be < {method.Literals.Count}");
                        }
                    }
                    else
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Patch '{patch.PatchId}' targets non-existent method '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Method must exist");
                    }
                }
                break;
                
            case "objectField":
                // Validate object exists and field index is in range
                if (!string.IsNullOrEmpty(targetLocalId))
                {
                    if (context.ObjectByLocalId.TryGetValue(targetLocalId, out var obj))
                    {
                        var fieldCount = obj.Fields?.Count ?? 0;
                        if (fieldIndex.HasValue && fieldIndex.Value >= fieldCount)
                        {
                            result.AddDiagnostic(
                                DiagnosticCodes.PatchInvalid,
                                Severity.Error,
                                $"Patch '{patch.PatchId}' field index {fieldIndex} out of range " +
                                $"(object has {fieldCount} fields)",
                                new DiagnosticLocation("patches", patch.PatchId),
                                $"Field index must be < {fieldCount}");
                        }
                    }
                    else
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Patch '{patch.PatchId}' targets non-existent object '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Object must exist");
                    }
                }
                break;
                
            case "classField":
                // Validate class exists
                if (!string.IsNullOrEmpty(targetLocalId))
                {
                    if (!context.ClassByLocalId.ContainsKey(targetLocalId))
                    {
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Patch '{patch.PatchId}' targets non-existent class '{targetLocalId}'",
                            new DiagnosticLocation("patches", patch.PatchId),
                            "Class must exist");
                    }
                }
                break;
                
            case "setValue":
            case "setAssociationValue":
            case "addMethod":
            case "setSuperclass":
                // These operations should have valid target localIds
                if (!string.IsNullOrEmpty(targetLocalId) && !context.LocalIdExists(targetLocalId))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchTargetMissing,
                        Severity.Error,
                        $"Patch '{patch.PatchId}' operation '{targetKind}' targets non-existent '{targetLocalId}'",
                        new DiagnosticLocation("patches", patch.PatchId),
                        "Target must exist");
                }
                break;
        }
    }
    
    private void ValidatePatchOperation(ValidationResult result, ValidationContext context, Patch patch)
    {
        var validOperations = new HashSet<string>
        {
            "wireMetaclass",
            "setValue",
            "setAssociationValue",
            "addMethod",
            "setSuperclass",
            "setField",
            "setLiteral",
            "setInstVar",
            "setClassVar",
            "addToMethodDictionary",
            "initializeClass",
            "custom"
        };
        
        if (!validOperations.Contains(patch.Operation))
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Warning,
                $"Patch '{patch.PatchId}' uses non-standard operation '{patch.Operation}'",
                new DiagnosticLocation("patches", patch.PatchId),
                $"Consider using standard operations: {string.Join(", ", validOperations)}");
        }
        
        // Validate value is appropriate for operation
        if (patch.Operation == "setAssociationValue" && patch.Value == null)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patch.PatchId}' operation 'setAssociationValue' requires a value",
                new DiagnosticLocation("patches", patch.PatchId),
                "Provide a value for the association");
        }
        
        if (patch.Operation == "addMethod" && patch.Value == null)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patch.PatchId}' operation 'addMethod' requires a method localId as value",
                new DiagnosticLocation("patches", patch.PatchId),
                "Provide the method localId to add");
        }
    }
}