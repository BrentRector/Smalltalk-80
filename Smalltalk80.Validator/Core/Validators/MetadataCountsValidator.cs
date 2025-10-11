using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;

namespace Smalltalk80.Validator.Core.Validators;

public class MetadataCountsValidator : IValidator
{
    public string Id => "MetadataCountsValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();
        var root = ctx.Bundle.RootElement;

        if (!root.TryGetProperty("metadata", out var metadata))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MetaMissing,
                Severity.Error,
                "Metadata section is missing from bundle",
                new[] { new Location("metadata") },
                "Ensure compiler emits metadata section"));
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }

        ValidateCount(diagnostics, metadata, "symbolCount", ctx.Symbols.Count);
        ValidateCount(diagnostics, metadata, "stringCount", GetStringCount(root));
        ValidateCount(diagnostics, metadata, "classCount", ctx.Classes.Count);
        ValidateCount(diagnostics, metadata, "methodCount", ctx.Methods.Count);
        ValidateCount(diagnostics, metadata, "objectCount", ctx.Objects.Count);
        ValidateCount(diagnostics, metadata, "initExpressionCount", ctx.InitExpressions.Count);

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateCount(List<Diagnostic> diagnostics, JsonElement metadata, string fieldName, int actualCount)
    {
        if (!metadata.TryGetProperty(fieldName, out var countProp))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MetaCountMismatch,
                Severity.Warning,
                $"metadata.{fieldName} is missing (expected {actualCount})",
                new[] { new Location("metadata", fieldName) },
                $"Add metadata.{fieldName} = {actualCount}"));
            return;
        }

        var declaredCount = countProp.GetInt32();
        if (declaredCount != actualCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MetaCountMismatch,
                Severity.Error,
                $"metadata.{fieldName}={declaredCount} but actual array length={actualCount}",
                new[] { new Location("metadata", fieldName) },
                $"Update metadata.{fieldName} to {actualCount} or fix compiler emitter"));
        }
    }

    private int GetStringCount(JsonElement root)
    {
        if (root.TryGetProperty("strings", out var strings) && strings.ValueKind == JsonValueKind.Array)
        {
            return strings.GetArrayLength();
        }
        return 0;
    }
}
