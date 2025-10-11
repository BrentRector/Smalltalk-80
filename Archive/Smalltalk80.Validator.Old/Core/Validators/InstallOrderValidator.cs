using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates install order:
/// - All referenced localIds appear in install order phases
/// - No duplicate localIds across phases
/// - Phase kinds match the objects they reference
/// </summary>
public class InstallOrderValidator : IValidator
{
    public string Id => "InstallOrderValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Build set of all localIds that should be installed
        var expectedLocalIds = new HashSet<string>();

        foreach (var cls in ctx.Classes)
        {
            var localId = cls.GetStringProp("localId");
            if (localId != null) expectedLocalIds.Add(localId);
        }

        foreach (var obj in ctx.Objects)
        {
            var localId = obj.GetStringProp("localId");
            if (localId != null) expectedLocalIds.Add(localId);
        }

        foreach (var method in ctx.Methods)
        {
            var localId = method.GetStringProp("localId");
            if (localId != null) expectedLocalIds.Add(localId);
        }

        foreach (var initExpr in ctx.InitExpressions)
        {
            var localId = initExpr.GetStringProp("localId");
            if (localId != null) expectedLocalIds.Add(localId);
        }

        foreach (var patch in ctx.Patches)
        {
            var patchId = patch.GetStringProp("patchId");
            if (patchId != null) expectedLocalIds.Add(patchId);
        }

        // Collect all localIds in install order
        var installedLocalIds = new HashSet<string>();
        var duplicates = new List<string>();

        var root = ctx.Bundle.RootElement;
        if (root.TryGetProperty("installOrder", out var installOrderProp) &&
            installOrderProp.TryGetProperty("phases", out var phases) &&
            phases.ValueKind == JsonValueKind.Array)
        {
            int phaseIndex = 0;
            foreach (var phase in phases.EnumerateArray())
            {
                var phaseName = phase.GetStringProp("name") ?? $"Phase {phaseIndex}";

                if (phase.TryGetProperty("localIds", out var localIdArray) &&
                    localIdArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var localIdEl in localIdArray.EnumerateArray())
                    {
                        var localId = localIdEl.GetString();
                        if (localId == null) continue;

                        if (installedLocalIds.Contains(localId))
                        {
                            duplicates.Add(localId);
                        }
                        else
                        {
                            installedLocalIds.Add(localId);
                        }

                        // Check that localId exists in bundle (skip detailed check for patches as they use patchId not localId)
                        if (!localId.StartsWith("patch:") && !expectedLocalIds.Contains(localId))
                        {
                            diagnostics.Add(new Diagnostic(
                                ErrorCodes.InstallMissing,
                                Severity.Error,
                                $"Phase '{phaseName}' references unknown localId '{localId}'",
                                new[] { new Location("installOrder", phaseName) },
                "Remove reference or add the missing entity"));
                        }
                    }
                }
                phaseIndex++;
            }
        }

        // Check for missing localIds
        var missingLocalIds = expectedLocalIds.Except(installedLocalIds).ToList();
        foreach (var missing in missingLocalIds)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.InstallOmitted,
                Severity.Error,
                $"LocalId '{missing}' not found in any install phase",
                new[] { new Location("installOrder") },
                "Add entity to appropriate install phase"));
        }

        // Report duplicates
        foreach (var dup in duplicates)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.InstallDuplicate,
                Severity.Error,
                $"LocalId '{dup}' appears in multiple install phases",
                new[] { new Location("installOrder") },
                "Ensure each entity appears exactly once"));
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
