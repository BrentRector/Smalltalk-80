using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates that patch targets exist and operations are valid:
/// - Target localIds must exist
/// - Target field indices must be in range
/// - Operations must be valid for target types
/// - Values must be appropriate for operations
/// </summary>
public class PatchTargetExistenceValidator : IValidator
{
    public string Id => "PatchTargetExistenceValidator";
    public bool IsHeavy => false;

    private static readonly HashSet<string> ValidOperations = new()
    {
        "wireMetaclass", "setValue", "setAssociationValue", "addMethod",
        "setSuperclass", "setField", "setLiteral", "setInstVar",
        "setClassVar", "addToMethodDictionary", "initializeClass", "custom"
    };

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var patch in ctx.Patches)
        {
            ValidatePatch(diagnostics, ctx, patch);
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidatePatch(List<Diagnostic> diagnostics, ValidationContext ctx, JsonElement patch)
    {
        var patchId = patch.GetStringProp("patchId");
        if (string.IsNullOrEmpty(patchId))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchInvalid,
                Severity.Error,
                "Patch missing patchId",
                new[] { new Location("patches") },
                "All patches must have a patchId"));
            return;
        }

        var operation = patch.GetStringProp("operation");
        if (string.IsNullOrEmpty(operation))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patchId}' missing operation",
                new[] { new Location("patches", patchId) },
                "All patches must specify an operation"));
            return;
        }

        // Validate operation is standard
        if (!ValidOperations.Contains(operation))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchInvalid,
                Severity.Warning,
                $"Patch '{patchId}' uses non-standard operation '{operation}'",
                new[] { new Location("patches", patchId) },
                $"Consider using standard operations: {string.Join(", ", ValidOperations)}"));
        }

        // Validate target exists
        var targetElement = patch.GetObjectProp("target");
        if (targetElement == null)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchTargetMissing,
                Severity.Error,
                $"Patch '{patchId}' has missing or invalid target",
                new[] { new Location("patches", patchId) },
                "Patch must specify a target"));
            return;
        }

        ValidatePatchTarget(diagnostics, ctx, patch, patchId, targetElement.Value, operation);
    }

    private void ValidatePatchTarget(List<Diagnostic> diagnostics, ValidationContext ctx,
        JsonElement patch, string patchId, JsonElement target, string operation)
    {
        var targetKind = target.GetStringProp("kind");
        var targetLocalId = target.GetStringProp("localId")
            ?? target.GetStringProp("classLocalId")
            ?? target.GetStringProp("methodLocalId")
            ?? target.GetStringProp("objectLocalId");

        if (!string.IsNullOrEmpty(targetLocalId))
        {
            if (!ctx.IndexByLocalId.ContainsKey(targetLocalId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.PatchTargetMissing,
                    Severity.Error,
                    $"Patch '{patchId}' targets non-existent localId '{targetLocalId}'",
                    new[] { new Location("patches", patchId) },
                    "Target localId must exist"));
                return;
            }

            // Validate metaclass wiring
            if (targetKind == "metaclassWiring" && !targetLocalId.StartsWith("meta:"))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.PatchInvalid,
                    Severity.Warning,
                    $"Patch '{patchId}' metaclass wiring targets non-metaclass '{targetLocalId}'",
                    new[] { new Location("patches", patchId) },
                    "Metaclass wiring should target meta: prefixed classes"));
            }

            // Validate field/literal indices
            var fieldIndex = target.GetIntProp("fieldIndex");
            var literalIndex = target.GetIntProp("literalIndex");

            if (literalIndex.HasValue && targetKind == "methodLiteral")
            {
                ValidateMethodLiteralIndex(diagnostics, ctx, patchId, targetLocalId, literalIndex.Value);
            }

            if (fieldIndex.HasValue && targetKind == "objectField")
            {
                ValidateObjectFieldIndex(diagnostics, ctx, patchId, targetLocalId, fieldIndex.Value);
            }
        }

        // Validate operation-specific requirements
        ValidateOperationRequirements(diagnostics, patch, patchId, operation);
    }

    private void ValidateMethodLiteralIndex(List<Diagnostic> diagnostics, ValidationContext ctx,
        string patchId, string methodLocalId, int literalIndex)
    {
        var methodRefs = ctx.IndexByLocalId.GetValueOrDefault(methodLocalId);
        if (methodRefs == null || !methodRefs.Any())
            return;

        var method = methodRefs.First(r => r.Section == "methods").Item;
        var literals = method.GetArrayProp("literals");
        if (literals != null)
        {
            var literalCount = literals.Value.GetArrayLength();
            if (literalIndex >= literalCount)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.PatchInvalid,
                    Severity.Error,
                    $"Patch '{patchId}' literal index {literalIndex} out of range (method has {literalCount} literals)",
                    new[] { new Location("patches", patchId) },
                    $"Literal index must be < {literalCount}"));
            }
        }
    }

    private void ValidateObjectFieldIndex(List<Diagnostic> diagnostics, ValidationContext ctx,
        string patchId, string objectLocalId, int fieldIndex)
    {
        var objectRefs = ctx.IndexByLocalId.GetValueOrDefault(objectLocalId);
        if (objectRefs == null || !objectRefs.Any())
            return;

        var obj = objectRefs.First(r => r.Section == "objects").Item;
        var fields = obj.GetArrayProp("fields");
        if (fields != null)
        {
            var fieldCount = fields.Value.GetArrayLength();
            if (fieldIndex >= fieldCount)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.PatchInvalid,
                    Severity.Error,
                    $"Patch '{patchId}' field index {fieldIndex} out of range (object has {fieldCount} fields)",
                    new[] { new Location("patches", patchId) },
                    $"Field index must be < {fieldCount}"));
            }
        }
    }

    private void ValidateOperationRequirements(List<Diagnostic> diagnostics, JsonElement patch,
        string patchId, string operation)
    {
        var hasValue = patch.TryGetProperty("value", out var valueProp) && valueProp.ValueKind != JsonValueKind.Null;

        if (operation == "setAssociationValue" && !hasValue)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patchId}' operation 'setAssociationValue' requires a value",
                new[] { new Location("patches", patchId) },
                "Provide a value for the association"));
        }

        if (operation == "addMethod" && !hasValue)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.PatchInvalid,
                Severity.Error,
                $"Patch '{patchId}' operation 'addMethod' requires a method localId as value",
                new[] { new Location("patches", patchId) },
                "Provide the method localId to add"));
        }
    }
}
