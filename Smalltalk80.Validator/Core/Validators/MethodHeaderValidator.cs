using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates that method headers are consistent with their declared properties.
/// Method header format per docs/Interpreter-Architecture.md lines 5015-5020:
/// - Bits 0-5:   Literal count (0-63)         [6 bits]
/// - Bit 6:      Large frame flag             [1 bit]
/// - Bits 7-11:  Temporary count (0-31)       [5 bits]
/// - Bits 12-14: Flags field (0-7)            [3 bits]
/// - Bit 15:     Reserved/unused              [1 bit]
///
/// Flags field interpretation:
/// - 0-4: Simple form - flags encode argCount directly (no primitive)
/// - 5-6: Quick-return forms (not yet supported)
/// - 7:   Extended form - argCount and primitiveIndex in last literal
/// </summary>
public class MethodHeaderValidator : IValidator
{
    public string Id => "MethodHeaderValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Validate all methods
        for (int i = 0; i < ctx.Methods.Count; i++)
        {
            var method = ctx.Methods[i];
            ValidateMethodHeader(method, ctx, diagnostics, i);
        }

        // Validate all init expressions
        for (int i = 0; i < ctx.InitExpressions.Count; i++)
        {
            var initExpr = ctx.InitExpressions[i];
            ValidateInitExpressionHeader(initExpr, diagnostics, i);
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateMethodHeader(JsonElement method, ValidationContext ctx, List<Diagnostic> diagnostics, int methodIndex)
    {
        var header = method.GetObjectProp("header");
        if (header == null)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                "Method missing header",
                new[] { new Location("methods", method.GetStringProp("localId"), methodIndex) },
                "Ensure compiler emits header for all methods"));
            return;
        }

        var localId = method.GetStringProp("localId") ?? $"method[{methodIndex}]";

        // Decode header word
        var word = header.Value.GetIntProp("word") ?? 0;
        var literalCount = word & 0x3F;                // Bits 0-5
        var largeContext = (word & 0x40) != 0;         // Bit 6
        var tempCount = (word >> 7) & 0x1F;            // Bits 7-11
        var flags = (word >> 12) & 0x07;               // Bits 12-14

        // Interpret flags field to get argCount and primitive
        int argCount;
        int primitiveIndex = 0;

        if (flags < 5)
        {
            // Simple form: flags 0-4 encode argCount directly
            argCount = flags;
        }
        else if (flags == 7)
        {
            // Extended form: argCount and primitive in last literal
            var literals = method.GetArrayProp("literals");
            int litCount = literals?.GetArrayLength() ?? 0;

            if (litCount > 0)
            {
                var lastLiteral = literals!.Value[litCount - 1];
                int extendedInfo = 0;
                bool validLiteral = false;

                if (lastLiteral.ValueKind == JsonValueKind.Number)
                {
                    extendedInfo = lastLiteral.GetInt32();
                    validLiteral = true;
                }
                else if (lastLiteral.ValueKind == JsonValueKind.Object)
                {
                    // Handle JSON structure: {"type": "smallInt", "value": <number>}
                    if (lastLiteral.TryGetProperty("type", out var typeProperty) &&
                        typeProperty.GetString() == "smallInt" &&
                        lastLiteral.TryGetProperty("value", out var valueProperty) &&
                        valueProperty.ValueKind == JsonValueKind.Number)
                    {
                        extendedInfo = valueProperty.GetInt32();
                        validLiteral = true;
                    }
                }

                if (validLiteral)
                {
                    primitiveIndex = extendedInfo & 0xFF;
                    argCount = (extendedInfo >> 8) & 0x1F;
                }
                else
                {
                    argCount = 0;
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.MethodHeaderInvalid,
                        Severity.Error,
                        $"Extended form (flags=7) requires last literal to be SmallInteger, got {lastLiteral.ValueKind}",
                        new[] { new Location("methods", localId, methodIndex) },
                        "Fix compiler to emit SmallInteger in last literal for extended form"));
                }
            }
            else
            {
                argCount = 0;
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.MethodHeaderInvalid,
                    Severity.Error,
                    $"Extended form (flags=7) requires at least one literal for argCount/primitive encoding",
                    new[] { new Location("methods", localId, methodIndex) },
                    "Fix compiler to emit literals for extended form"));
            }
        }
        else
        {
            // Flags 5-6: Quick-return forms (not yet supported)
            argCount = 0;
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Warning,
                $"Method uses quick-return form (flags={flags}) which is not yet supported",
                new[] { new Location("methods", localId, methodIndex) },
                "Quick-return forms are not yet implemented"));
        }

        // Check if header fields match the decoded word (if provided)
        var declaredArgCount = header.Value.GetIntProp("argCount");
        if (declaredArgCount.HasValue && declaredArgCount != argCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Header argCount ({declaredArgCount}) doesn't match decoded value ({argCount}) from flags={flags}",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update header.argCount to {argCount}"));
        }

        var declaredTempCount = header.Value.GetIntProp("tempCount");
        if (declaredTempCount.HasValue && declaredTempCount != tempCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Header tempCount ({declaredTempCount}) doesn't match decoded word value ({tempCount})",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update header.tempCount to {tempCount}"));
        }

        var declaredLiteralCount = header.Value.GetIntProp("literalCount");
        if (declaredLiteralCount.HasValue && declaredLiteralCount != literalCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Header literalCount ({declaredLiteralCount}) doesn't match decoded word value ({literalCount})",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update header.literalCount to {literalCount}"));
        }

        var declaredLargeContext = header.Value.GetBoolProp("largeContext");
        if (declaredLargeContext.HasValue && declaredLargeContext != largeContext)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Header largeContext ({declaredLargeContext}) doesn't match decoded word value ({largeContext})",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update header.largeContext to {largeContext}"));
        }

        // Check actual literal count vs header
        var literals2 = method.GetArrayProp("literals");
        var actualLiteralCount = literals2?.GetArrayLength() ?? 0;
        if (actualLiteralCount != literalCount)
        {
            // Extended literals: method can have >63 literals (header capped at 63)
            if (literalCount == 63 && actualLiteralCount > 63)
            {
                // This is normal for extended literal support, verbose-only warning
                // (not adding diagnostic for now)
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.MethodHeaderInvalid,
                    Severity.Error,
                    $"Method has {actualLiteralCount} literals but header declares {literalCount}",
                    new[] { new Location("methods", localId, methodIndex) },
                    $"Update header to match actual literal count"));
            }
        }

        // Check temp/arg names consistency
        var tempNames = method.GetStringArray("tempNames");
        var argNames = method.GetStringArray("argNames");

        var declaredTempNameCount = tempNames.Length;
        var declaredArgNameCount = argNames.Length;

        if (declaredArgNameCount != argCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Warning,
                $"Method has {declaredArgNameCount} arg names but header declares {argCount} args (flags={flags})",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update argNames to match header argCount={argCount}"));
        }

        if (declaredTempNameCount != tempCount)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Warning,
                $"Method has {declaredTempNameCount} temp names but header declares {tempCount} temps",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update tempNames to match header tempCount={tempCount}"));
        }

        // Validate primitive consistency
        var declaredPrimitiveIndex = header.Value.GetIntProp("primitiveIndex");
        if (primitiveIndex > 0 && (!declaredPrimitiveIndex.HasValue || declaredPrimitiveIndex != primitiveIndex))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Decoded primitive index ({primitiveIndex}) doesn't match header.primitiveIndex ({declaredPrimitiveIndex})",
                new[] { new Location("methods", localId, methodIndex) },
                $"Update header.primitiveIndex to {primitiveIndex}"));
        }

        if (declaredPrimitiveIndex.HasValue && declaredPrimitiveIndex > 0 && primitiveIndex == 0)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Header declares primitiveIndex={declaredPrimitiveIndex} but flags={flags} doesn't encode a primitive",
                new[] { new Location("methods", localId, methodIndex) },
                "Fix header flags to encode primitive or remove primitiveIndex"));
        }

        // Validate selector index
        var selectorIndex = method.GetIntProp("selectorIndex") ?? -1;
        if (selectorIndex < -1 || selectorIndex >= ctx.Symbols.Count)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                $"Selector index {selectorIndex} out of bounds (symbols.length = {ctx.Symbols.Count})",
                new[] { new Location("methods", localId, methodIndex) },
                "Fix selector index to reference valid symbol"));
        }
    }

    private void ValidateInitExpressionHeader(JsonElement initExpr, List<Diagnostic> diagnostics, int initIndex)
    {
        var header = initExpr.GetObjectProp("header");
        if (header == null)
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.MethodHeaderInvalid,
                Severity.Error,
                "InitExpression missing header",
                new[] { new Location("initExpressions", initExpr.GetStringProp("localId"), initIndex) },
                "Ensure compiler emits header for all init expressions"));
            return;
        }

        var localId = initExpr.GetStringProp("localId") ?? $"init[{initIndex}]";

        // Decode header word
        var word = header.Value.GetIntProp("word") ?? 0;
        var literalCount = word & 0x3F;  // Bits 0-5

        // Check actual literal count vs header
        var literals = initExpr.GetArrayProp("literals");
        var actualLiteralCount = literals?.GetArrayLength() ?? 0;
        if (actualLiteralCount != literalCount)
        {
            // Extended literals: init expression can have >63 literals (header capped at 63)
            if (literalCount == 63 && actualLiteralCount > 63)
            {
                // This is normal for extended literal support, verbose-only warning
                // (not adding diagnostic for now)
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.MethodHeaderInvalid,
                    Severity.Error,
                    $"Init expression has {actualLiteralCount} literals but header declares {literalCount}",
                    new[] { new Location("initExpressions", localId, initIndex) },
                    $"Update header to match actual literal count"));
            }
        }
    }
}
