using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates that all localIds are unique across the bundle and follow naming conventions.
/// Duplicate localIds break allocation mapping and patch resolution.
/// </summary>
public class LocalIdUniquenessValidator : IValidator
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

    public string Id => "LocalIdUniquenessValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Check for duplicates using the pre-built index
        foreach (var kvp in ctx.IndexByLocalId)
        {
            var localId = kvp.Key;
            var occurrences = kvp.Value;

            if (occurrences.Count > 1)
            {
                var locations = occurrences
                    .Select((o, idx) => new Location(o.Section, localId, idx))
                    .ToList();

                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdDuplicate,
                    Severity.Error,
                    $"LocalId '{localId}' appears {occurrences.Count} times across sections",
                    locations,
                    "Ensure compiler emits unique localIds"));
            }
        }

        // Check naming conventions
        foreach (var localId in ctx.IndexByLocalId.Keys)
        {
            if (!HasValidPrefix(localId))
            {
                var occurrence = ctx.IndexByLocalId[localId].First();
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMalformed,
                    Severity.Warning,
                    $"LocalId '{localId}' uses non-standard prefix",
                    new[] { new Location(occurrence.Section, localId) },
                    $"Use canonical prefixes: {string.Join(", ", AllowedPrefixes)}"));
            }

            // Additional validation: check prefix matches section
            ValidatePrefixMatchesSection(diagnostics, localId, ctx);
        }

        // Check for empty localIds
        CheckForEmptyLocalIds(diagnostics, ctx);

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private bool HasValidPrefix(string localId)
    {
        return AllowedPrefixes.Any(prefix => localId.StartsWith(prefix, StringComparison.Ordinal));
    }

    private void ValidatePrefixMatchesSection(List<Diagnostic> diagnostics, string localId, ValidationContext ctx)
    {
        var occurrence = ctx.IndexByLocalId[localId].First();
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

            diagnostics.Add(new Diagnostic(
                ErrorCodes.LocalIdMalformed,
                Severity.Warning,
                $"LocalId prefix '{prefix}' unexpected in section '{section}'",
                new[] { new Location(section, localId) },
                $"LocalId '{localId}' in section '{section}' has unexpected prefix"));
        }
    }

    private void CheckForEmptyLocalIds(List<Diagnostic> diagnostics, ValidationContext ctx)
    {
        // Check classes
        for (int i = 0; i < ctx.Classes.Count; i++)
        {
            var cls = ctx.Classes[i];
            var localId = cls.GetStringProp("localId");
            if (string.IsNullOrWhiteSpace(localId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMissing,
                    Severity.Error,
                    $"Class at index {i} has empty or missing localId",
                    new[] { new Location("classes", null, i) },
                    "All classes must have non-empty localIds"));
            }
        }

        // Check methods
        for (int i = 0; i < ctx.Methods.Count; i++)
        {
            var method = ctx.Methods[i];
            var localId = method.GetStringProp("localId");
            if (string.IsNullOrWhiteSpace(localId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMissing,
                    Severity.Error,
                    $"Method at index {i} has empty or missing localId",
                    new[] { new Location("methods", null, i) },
                    "All methods must have non-empty localIds"));
            }
        }

        // Check objects
        for (int i = 0; i < ctx.Objects.Count; i++)
        {
            var obj = ctx.Objects[i];
            var localId = obj.GetStringProp("localId");
            if (string.IsNullOrWhiteSpace(localId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMissing,
                    Severity.Error,
                    $"Object at index {i} has empty or missing localId",
                    new[] { new Location("objects", null, i) },
                    "All objects must have non-empty localIds"));
            }
        }

        // Check init expressions
        for (int i = 0; i < ctx.InitExpressions.Count; i++)
        {
            var initExpr = ctx.InitExpressions[i];
            var localId = initExpr.GetStringProp("localId");
            if (string.IsNullOrWhiteSpace(localId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMissing,
                    Severity.Error,
                    $"InitExpression at index {i} has empty or missing localId",
                    new[] { new Location("initExpressions", null, i) },
                    "All init expressions must have non-empty localIds"));
            }
        }

        // Check patches
        for (int i = 0; i < ctx.Patches.Count; i++)
        {
            var patch = ctx.Patches[i];
            // Patches use "patchId" not "localId"
            var patchId = patch.GetStringProp("patchId");
            if (string.IsNullOrWhiteSpace(patchId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LocalIdMissing,
                    Severity.Error,
                    $"Patch at index {i} has empty or missing patchId",
                    new[] { new Location("patches", null, i) },
                    "All patches must have non-empty patchIds"));
            }
        }
    }
}
