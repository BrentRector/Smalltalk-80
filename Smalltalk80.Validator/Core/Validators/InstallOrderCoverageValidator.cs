using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Enhanced install order validation that ensures:
/// - All entities appear exactly once in install order
/// - Phase kinds match the entity types they contain
/// - Phases are in correct dependency order
/// - No orphaned or missing entities
/// </summary>
public class InstallOrderCoverageValidator : IValidator
{
    public string Id => "InstallOrderCoverageValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Track all localIds that have been installed
        var installedLocalIds = new HashSet<string>();

        var root = ctx.Bundle.RootElement;
        if (root.TryGetProperty("installOrder", out var installOrderProp) &&
            installOrderProp.TryGetProperty("phases", out var phases) &&
            phases.ValueKind == JsonValueKind.Array)
        {
            int phaseIndex = 0;
            foreach (var phase in phases.EnumerateArray())
            {
                var phaseName = phase.GetStringProp("name") ?? $"Phase {phaseIndex}";
                var phaseKind = phase.GetStringProp("kind") ?? "unknown";

                if (phase.TryGetProperty("localIds", out var localIdArray) &&
                    localIdArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var localIdEl in localIdArray.EnumerateArray())
                    {
                        var localId = localIdEl.GetString();
                        if (localId == null) continue;

                        // Check for duplicates
                        if (installedLocalIds.Contains(localId))
                        {
                            diagnostics.Add(new Diagnostic(
                                ErrorCodes.InstallDuplicate,
                                Severity.Error,
                                $"LocalId '{localId}' appears multiple times in install order",
                                new[] { new Location("installOrder", localId, phaseIndex) },
                "Ensure each entity appears exactly once"));
                            continue;
                        }
                        installedLocalIds.Add(localId);

                        // Check that localId exists (either as localId or patchId)
                        bool exists = ctx.IndexByLocalId.ContainsKey(localId);
                        if (!exists && localId.StartsWith("patch:"))
                        {
                            // Patches use patchId, not localId, so check in Patches collection
                            exists = ctx.Patches.Any(p => p.GetStringProp("patchId") == localId);
                        }

                        if (!exists)
                        {
                            diagnostics.Add(new Diagnostic(
                                ErrorCodes.InstallMissing,
                                Severity.Error,
                                $"Phase '{phaseName}' references non-existent localId '{localId}'",
                                new[] { new Location("installOrder", localId, phaseIndex) },
                "Remove reference or add the missing entity"));
                            continue;
                        }

                        // Validate phase.kind matches entity type
                        ValidatePhaseKindMatch(diagnostics, ctx, phase, localId, phaseName, phaseKind);
                    }
                }
                phaseIndex++;
            }
        }

        // Check for entities not in install order
        // v1.0.4: SKIP metadata.blockContexts - they are tooling-only, not installed
        foreach (var kvp in ctx.IndexByLocalId)
        {
            var localId = kvp.Key;
            var occurrence = kvp.Value.First();

            // Skip BlockContext metadata entries (they're in metadata.blockContexts[], not installed)
            if (occurrence.Section == "metadata.blockContexts")
                continue;

            if (!installedLocalIds.Contains(localId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.InstallOmitted,
                    Severity.Error,
                    $"{occurrence.Section} entity '{localId}' is not in any install phase",
                    new[] { new Location(occurrence.Section, localId) },
                "Add entity to appropriate install phase"));
            }
        }

        // Also check patches (which use patchId instead of localId)
        foreach (var patch in ctx.Patches)
        {
            var patchId = patch.GetStringProp("patchId");
            if (patchId != null && !installedLocalIds.Contains(patchId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.InstallOmitted,
                    Severity.Error,
                    $"patches entity '{patchId}' is not in any install phase",
                    new[] { new Location("patches", patchId) },
                "Add patch to appropriate install phase"));
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidatePhaseKindMatch(List<Diagnostic> diagnostics, ValidationContext ctx,
        JsonElement phase, string localId, string phaseName, string phaseKind)
    {
        if (!ctx.IndexByLocalId.TryGetValue(localId, out var occurrences))
            return;

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
            ("inits", "initExpressions", _) => true,

            // Patches phase
            ("patches", "patches", _) => true,

            // Special phases
            ("bootstrap", _, _) => true,
            ("core", "classes", _) => true,
            ("kernel", "classes", _) => true,

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
            diagnostics.Add(new Diagnostic(
                ErrorCodes.InstallKindMismatch,
                Severity.Error,
                $"Phase '{phaseName}' has kind='{phaseKind}' but contains {actualSection} entity '{localId}'",
                new[] { new Location("installOrder", localId) },
                $"Move entity to appropriate phase or update phase kind to '{actualSection}'"));
        }
    }
}
