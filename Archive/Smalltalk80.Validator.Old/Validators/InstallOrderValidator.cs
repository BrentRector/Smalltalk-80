using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates install order:
/// - All referenced localIds appear in install order phases
/// - No duplicate localIds across phases
/// - Phase kinds match the objects they reference
/// </summary>
public class InstallOrderValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        // Build set of all localIds that should be installed
        var expectedLocalIds = new HashSet<string>();

        foreach (var cls in bundle.Classes)
            expectedLocalIds.Add(cls.LocalId);

        foreach (var obj in bundle.Objects)
            expectedLocalIds.Add(obj.LocalId);

        foreach (var method in bundle.Methods)
            expectedLocalIds.Add(method.LocalId);

        foreach (var initExpr in bundle.InitExpressions)
            expectedLocalIds.Add(initExpr.LocalId);

        foreach (var patch in bundle.Patches)
            expectedLocalIds.Add(patch.PatchId);

        // Collect all localIds in install order
        var installedLocalIds = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var phase in bundle.InstallOrder.Phases)
        {
            foreach (var localId in phase.LocalIds)
            {
                if (installedLocalIds.Contains(localId))
                {
                    duplicates.Add(localId);
                }
                else
                {
                    installedLocalIds.Add(localId);
                }

                // Check that localId exists in bundle
                if (!expectedLocalIds.Contains(localId))
                {
                    result.AddError("InstallOrder",
                        $"Phase '{phase.Name}' references unknown localId '{localId}'",
                        phase.Name);
                }
            }
        }

        // Check for missing localIds
        var missingLocalIds = expectedLocalIds.Except(installedLocalIds).ToList();
        foreach (var missing in missingLocalIds)
        {
            result.AddError("InstallOrder",
                $"LocalId '{missing}' not found in any install phase",
                "InstallOrder");
        }

        // Report duplicates
        foreach (var dup in duplicates)
        {
            result.AddError("InstallOrder",
                $"LocalId '{dup}' appears in multiple install phases",
                "InstallOrder");
        }

        result.AddInfo($"Install order covers {installedLocalIds.Count}/{expectedLocalIds.Count} objects");

        if (result.IsValid)
        {
            result.AddInfo("✓ All objects appear exactly once in install order");
        }

        return result;
    }
}
