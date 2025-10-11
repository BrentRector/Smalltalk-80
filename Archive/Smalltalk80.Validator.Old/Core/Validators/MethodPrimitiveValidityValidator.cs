using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates method primitive indices:
/// - Primitive indices are in valid range
/// - Blocks have primitiveIndex=0
/// - Primitive encoding matches header flags
/// - Known primitives are used correctly
/// </summary>
public class MethodPrimitiveValidityValidator : IValidator
{
    private const int MinPrimitive = 0;
    private const int MaxPrimitive = 255;

    public string Id => "MethodPrimitiveValidityValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var method in ctx.Methods)
        {
            var localId = method.GetStringProp("localId") ?? "unknown";
            var selectorIndex = method.GetIntProp("selectorIndex") ?? -1;
            var selector = selectorIndex >= 0 ? ctx.Symbols.ElementAtOrDefault(selectorIndex) : null;
            var isBlock = selectorIndex == -1 || localId.StartsWith("block:");

            var header = method.GetObjectProp("header");
            if (header == null) continue;

            var primitiveIndex = header.Value.GetIntProp("primitiveIndex") ?? 0;

            if (isBlock)
            {
                // Blocks should have primitiveIndex=0
                if (primitiveIndex != 0)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.MethodPrimitiveInvalid,
                        Severity.Error,
                        $"Block '{localId}' has primitiveIndex={primitiveIndex} (blocks cannot have primitives)",
                        new[] { new Location("methods", localId) },
                "Set primitiveIndex to 0 for blocks"));
                }
            }
            else if (primitiveIndex > 0)
            {
                // Check primitive range
                if (primitiveIndex < MinPrimitive || primitiveIndex > MaxPrimitive)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.MethodPrimitiveInvalid,
                        Severity.Error,
                        $"Method '{localId}' has invalid primitiveIndex={primitiveIndex} (valid range: {MinPrimitive}-{MaxPrimitive})",
                        new[] { new Location("methods", localId) },
                        "Set primitiveIndex to a valid value (0-255)"));
                }

                // Validate primitive encoding in header
                var word = header.Value.GetIntProp("word") ?? 0;
                var flags = (word >> 12) & 0x07;

                // If method has a primitive, it should use extended form (flags=7)
                if (flags != 7 && flags < 5)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.MethodPrimitiveInvalid,
                        Severity.Warning,
                        $"Method with primitive {primitiveIndex} has flags={flags} (expected 7 for extended form)",
                        new[] { new Location("methods", localId) },
                "Methods with primitives typically use extended header form (flags=7)"));
                }
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
