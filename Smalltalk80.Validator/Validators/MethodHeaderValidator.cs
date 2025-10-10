using System.Text.Json;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

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
public class MethodHeaderValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        // Validate all methods
        foreach (var method in bundle.Methods)
        {
            ValidateMethodHeader(method, bundle, result);
        }

        // Validate all init expressions
        foreach (var initExpr in bundle.InitExpressions)
        {
            ValidateInitExpressionHeader(initExpr, result);
        }

        return result;
    }

    private void ValidateMethodHeader(MethodDefinition method, ColdStartBundle coldStartBundle, ValidationResult result)
    {
        var header = method.Header;
        var location = method.LocalId;

        // Decode header word per docs/Interpreter-Architecture.md lines 5015-5020
        // Bits 0-5:   Literal count (6 bits, 0-63)
        // Bit 6:      Large frame flag
        // Bits 7-11:  Temporary count (5 bits, 0-31)
        // Bits 12-14: Flags field (3 bits, 0-7)
        // Bit 15:     Reserved/unused
        var word = header.Word;
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
            if (method.Literals.Count > 0)
            {
                var lastLiteral = method.Literals[method.Literals.Count - 1];
                int extendedInfo;
                bool validLiteral = false;

                if (lastLiteral is int intValue)
                {
                    extendedInfo = intValue;
                    validLiteral = true;
                }
                else if (lastLiteral is JsonElement jsonElement)
                {
                    // Handle JSON structure: {"type": "smallInt", "value": <number>}
                    if (jsonElement.ValueKind == JsonValueKind.Object &&
                        jsonElement.TryGetProperty("type", out var typeProperty) &&
                        typeProperty.GetString() == "smallInt" &&
                        jsonElement.TryGetProperty("value", out var valueProperty) &&
                        valueProperty.ValueKind == JsonValueKind.Number)
                    {
                        extendedInfo = valueProperty.GetInt32();
                        validLiteral = true;
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        // Fallback: plain number
                        extendedInfo = jsonElement.GetInt32();
                        validLiteral = true;
                    }
                    else
                    {
                        extendedInfo = 0;
                    }
                }
                else
                {
                    extendedInfo = 0;
                }

                if (validLiteral)
                {
                    primitiveIndex = extendedInfo & 0xFF;
                    argCount = (extendedInfo >> 8) & 0x1F;
                }
                else
                {
                    argCount = 0;
                    result.AddError("MethodHeader",
                        $"Extended form (flags=7) requires last literal to be SmallInteger, got {lastLiteral?.GetType().Name ?? "null"}",
                        location);
                }
            }
            else
            {
                argCount = 0;
                result.AddError("MethodHeader",
                    $"Extended form (flags=7) requires at least one literal for argCount/primitive encoding",
                    location);
            }
        }
        else
        {
            // Flags 5-6: Quick-return forms (not yet supported)
            argCount = 0;
            result.AddWarning("MethodHeader",
                $"Method uses quick-return form (flags={flags}) which is not yet supported",
                location);
        }

        // Check if header fields match the decoded word (if provided)
        if (header.ArgCount.HasValue && header.ArgCount != argCount)
        {
            result.AddError("MethodHeader",
                $"Header argCount ({header.ArgCount}) doesn't match decoded value ({argCount}) from flags={flags}",
                location);
        }

        if (header.TempCount.HasValue && header.TempCount != tempCount)
        {
            result.AddError("MethodHeader",
                $"Header tempCount ({header.TempCount}) doesn't match decoded word value ({tempCount})",
                location);
        }

        if (header.LiteralCount.HasValue && header.LiteralCount != literalCount)
        {
            result.AddError("MethodHeader",
                $"Header literalCount ({header.LiteralCount}) doesn't match decoded word value ({literalCount})",
                location);
        }

        if (header.LargeContext.HasValue && header.LargeContext != largeContext)
        {
            result.AddError("MethodHeader",
                $"Header largeContext ({header.LargeContext}) doesn't match decoded word value ({largeContext})",
                location);
        }

        // Check actual literal count vs header
        var actualLiteralCount = method.Literals.Count;
        if (actualLiteralCount != literalCount)
        {
            // Extended literals: method can have >63 literals (header capped at 63)
            // This is a verbose-only warning since it's a normal Blue Book feature
            if (literalCount == 63 && actualLiteralCount > 63)
            {
                result.AddWarning("MethodHeader",
                    $"Method has {actualLiteralCount} literals (extended literal support, header capped at 63)",
                    location,
                    verboseOnly: true);
            }
            else
            {
                result.AddError("MethodHeader",
                    $"Method has {actualLiteralCount} literals but header declares {literalCount}",
                    location);
            }
        }

        // Check temp/arg names consistency
        var declaredTempCount = method.TempNames?.Length ?? 0;
        var declaredArgCount = method.ArgNames?.Length ?? 0;

        if (declaredArgCount != argCount)
        {
            result.AddWarning("MethodHeader",
                $"Method has {declaredArgCount} arg names but header declares {argCount} args (flags={flags})",
                location);
        }

        if (declaredTempCount != tempCount)
        {
            result.AddWarning("MethodHeader",
                $"Method has {declaredTempCount} temp names but header declares {tempCount} temps",
                location);
        }

        // Validate primitive consistency
        if (primitiveIndex > 0 && (!header.PrimitiveIndex.HasValue || header.PrimitiveIndex != primitiveIndex))
        {
            result.AddError("MethodHeader",
                $"Decoded primitive index ({primitiveIndex}) doesn't match header.primitiveIndex ({header.PrimitiveIndex})",
                location);
        }

        if (header.PrimitiveIndex.HasValue && header.PrimitiveIndex > 0 && primitiveIndex == 0)
        {
            result.AddError("MethodHeader",
                $"Header declares primitiveIndex={header.PrimitiveIndex} but flags={flags} doesn't encode a primitive",
                location);
        }

        // Validate selector index
        // Note: -1 is valid for blocks (blocks have no selector)
        if (method.SelectorIndex < -1 || method.SelectorIndex >= coldStartBundle.Symbols.Length)
        {
            result.AddError("MethodHeader",
                $"Selector index {method.SelectorIndex} out of bounds (symbols.length = {coldStartBundle.Symbols.Length})",
                location);
        }
    }

    private void ValidateInitExpressionHeader(InitExpression initExpr, ValidationResult result)
    {
        var header = initExpr.Header;
        var location = initExpr.LocalId;

        // Decode header word per docs/Interpreter-Architecture.md lines 5015-5020
        // Bits 0-5:   Literal count (6 bits, 0-63)
        // Bit 6:      Large frame flag
        // Bits 7-11:  Temporary count (5 bits, 0-31)
        // Bits 12-14: Flags field (3 bits, 0-7)
        var word = header.Word;
        var literalCount = word & 0x3F;  // Bits 0-5

        // Check actual literal count vs header
        var actualLiteralCount = initExpr.Literals.Count;
        if (actualLiteralCount != literalCount)
        {
            // Extended literals: init expression can have >63 literals (header capped at 63)
            // This is a verbose-only warning since it's a normal Blue Book feature
            if (literalCount == 63 && actualLiteralCount > 63)
            {
                result.AddWarning("MethodHeader",
                    $"Init expression has {actualLiteralCount} literals (extended literal support, header capped at 63)",
                    location,
                    verboseOnly: true);
            }
            else
            {
                result.AddError("MethodHeader",
                    $"Init expression has {actualLiteralCount} literals but header declares {literalCount}",
                    location);
            }
        }
    }
}
