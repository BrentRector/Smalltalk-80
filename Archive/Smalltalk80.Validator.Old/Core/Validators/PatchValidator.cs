using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates patches:
/// - Basic patch structure
/// - Patch IDs are valid
/// </summary>
public class PatchValidator : IValidator
{
    public string Id => "PatchValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Validate each patch
        foreach (var patch in ctx.Patches)
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
                continue;
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
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
