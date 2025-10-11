using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates pool dictionary usage:
/// - Pool localIds reference actual objects
/// - Classes that reference pools have them available
/// </summary>
public class PoolDictionaryUsageValidator : IValidator
{
    public string Id => "PoolDictionaryUsageValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Collect all pool dictionaries
        var poolDictionaries = new HashSet<string>();

        foreach (var obj in ctx.Objects)
        {
            var localId = obj.GetStringProp("localId");
            if (localId == null) continue;

            // Check if object is a pool dictionary
            var formatHint = obj.GetStringProp("formatHint");
            if (localId.StartsWith("pool:") || formatHint == "pool" || formatHint == "poolDictionary")
            {
                poolDictionaries.Add(localId);
            }
        }

        // Validate pool references from classes
        int invalidPoolReferences = 0;

        foreach (var cls in ctx.Classes)
        {
            var classLocalId = cls.GetStringProp("localId") ?? "unknown";
            var nameIndex = cls.GetIntProp("nameIndex");
            var className = nameIndex.HasValue && nameIndex.Value < ctx.Symbols.Count
                ? ctx.Symbols[nameIndex.Value]
                : "unknown";

            var poolLocalIds = cls.GetStringArray("poolLocalIds");
            if (poolLocalIds.Length == 0) continue;

            foreach (var poolLocalId in poolLocalIds)
            {
                if (string.IsNullOrEmpty(poolLocalId))
                    continue;

                if (!ctx.IndexByLocalId.ContainsKey(poolLocalId))
                {
                    invalidPoolReferences++;
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.PoolDictionaryMissing,
                        Severity.Error,
                        $"Class '{className}' references non-existent pool '{poolLocalId}'",
                        new[] { new Location("classes", classLocalId) },
                "Pool dictionary must exist"));
                }
                else if (!poolDictionaries.Contains(poolLocalId))
                {
                    // Object exists but isn't marked as a dictionary
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.PoolDictionaryInvalid,
                        Severity.Warning,
                        $"Class '{className}' pool reference '{poolLocalId}' is not marked as a pool dictionary",
                        new[] { new Location("classes", classLocalId) },
                "Pool must be a Dictionary object"));
                }
            }
        }

        // Check for orphaned pool dictionaries
        var referencedPools = new HashSet<string>();
        foreach (var cls in ctx.Classes)
        {
            var poolLocalIds = cls.GetStringArray("poolLocalIds");
            foreach (var poolId in poolLocalIds)
            {
                if (!string.IsNullOrEmpty(poolId))
                    referencedPools.Add(poolId);
            }
        }

        foreach (var poolId in poolDictionaries)
        {
            if (!referencedPools.Contains(poolId))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.PoolDictionaryInvalid,
                    Severity.Info,
                    $"Pool dictionary '{poolId}' is not referenced by any class",
                    new[] { new Location("objects", poolId) },
                "Consider removing unused pool dictionary"));
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
