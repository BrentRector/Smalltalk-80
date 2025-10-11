using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates that method selectors are present and valid.
/// Ensures methods have valid selectors (except blocks which have selector=-1).
/// </summary>
public class MethodSelectorPresenceValidator : IValidator
{
    public string Id => "MethodSelectorPresenceValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var method in ctx.Methods)
        {
            var localId = method.GetStringProp("localId") ?? "unknown";
            var selectorIndex = method.GetIntProp("selectorIndex") ?? -1;

            // Skip blocks (they don't have selectors in dictionaries)
            if (selectorIndex == -1 || localId.StartsWith("block:"))
                continue;

            var selector = selectorIndex >= 0 && selectorIndex < ctx.Symbols.Count
                ? ctx.Symbols[selectorIndex]
                : null;

            if (selector == null)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.MethodSelectorInvalid,
                    Severity.Error,
                    $"Method '{localId}' has invalid selector index {selectorIndex}",
                    new[] { new Location("methods", localId) },
                "Method must have valid selector"));
            }
            else if (string.IsNullOrWhiteSpace(selector))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.MethodSelectorInvalid,
                    Severity.Error,
                    $"Method '{localId}' has empty selector",
                    new[] { new Location("methods", localId) },
                "Method selector cannot be empty"));
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
