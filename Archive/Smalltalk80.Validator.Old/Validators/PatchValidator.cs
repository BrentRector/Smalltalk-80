using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates patches:
/// - Basic patch structure
/// - Patch IDs are valid
/// </summary>
public class PatchValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        // Validate each patch (simplified - just check structure exists)
        foreach (var patch in bundle.Patches)
        {
            // Basic validation - check patch has required fields
            if (string.IsNullOrEmpty(patch.PatchId))
            {
                result.AddError("Patch",
                    $"Patch missing patchId",
                    "unknown");
            }

            if (string.IsNullOrEmpty(patch.Operation))
            {
                result.AddError("Patch",
                    $"Patch '{patch.PatchId}' missing operation",
                    patch.PatchId);
            }
        }

        result.AddInfo($"Validated {bundle.Patches.Length} patches");
        return result;
    }
}
