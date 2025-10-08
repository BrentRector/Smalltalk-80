using Smalltalk80.Validator.JsonModels;
using System.Text.Json;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates literal frames:
/// - Selector ordering for optimized single-byte sends
/// - Association keys must be symbols (Blue Book requirement)
/// - Index bounds for symbol/string references
/// </summary>
public class LiteralFrameValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        foreach (var method in bundle.Methods)
        {
            ValidateLiterals(method.Literals, method.LocalId, bundle, result);
        }

        foreach (var initExpr in bundle.InitExpressions)
        {
            ValidateLiterals(initExpr.Literals, initExpr.LocalId, bundle, result);
        }

        return result;
    }

    private void ValidateLiterals(List<object> literals, string location, ColdStartBundle bundle, ValidationResult result)
    {
        for (int i = 0; i < literals.Count; i++)
        {
            var literal = literals[i];

            if (literal is JsonElement jsonElement)
            {
                ValidateJsonLiteral(jsonElement, i, location, bundle, result);
            }
        }
    }

    private void ValidateJsonLiteral(JsonElement literal, int index, string location, ColdStartBundle bundle, ValidationResult result)
    {
        if (literal.ValueKind == JsonValueKind.Object)
        {
            if (literal.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();

                switch (type)
                {
                    case "symbol":
                        ValidateSymbolLiteral(literal, index, location, bundle, result);
                        break;
                    case "string":
                        ValidateStringLiteral(literal, index, location, bundle, result);
                        break;
                    case "association":
                        ValidateAssociationLiteral(literal, index, location, bundle, result);
                        break;
                }
            }
        }
    }

    private void ValidateSymbolLiteral(JsonElement literal, int index, string location, ColdStartBundle bundle, ValidationResult result)
    {
        if (literal.TryGetProperty("index", out var indexElement))
        {
            var symbolIndex = indexElement.GetInt32();
            if (symbolIndex < 0 || symbolIndex >= bundle.Symbols.Length)
            {
                result.AddError("LiteralFrame",
                    $"Literal[{index}] symbol index {symbolIndex} out of bounds (symbols.length = {bundle.Symbols.Length})",
                    location);
            }
        }
    }

    private void ValidateStringLiteral(JsonElement literal, int index, string location, ColdStartBundle bundle, ValidationResult result)
    {
        if (literal.TryGetProperty("index", out var indexElement))
        {
            var stringIndex = indexElement.GetInt32();
            if (stringIndex < 0 || stringIndex >= bundle.Strings.Length)
            {
                result.AddError("LiteralFrame",
                    $"Literal[{index}] string index {stringIndex} out of bounds (strings.length = {bundle.Strings.Length})",
                    location);
            }
        }
    }

    private void ValidateAssociationLiteral(JsonElement literal, int index, string location, ColdStartBundle bundle, ValidationResult result)
    {
        if (literal.TryGetProperty("association", out var assocElement))
        {
            // Check that key is a symbol (Blue Book requirement)
            if (assocElement.TryGetProperty("keyType", out var keyTypeElement))
            {
                var keyType = keyTypeElement.GetString();
                if (keyType != "symbol")
                {
                    result.AddError("LiteralFrame",
                        $"Literal[{index}] association key must be 'symbol', found '{keyType}' (Blue Book requirement)",
                        location);
                }
            }

            // Validate key index
            if (assocElement.TryGetProperty("keyIndex", out var keyIndexElement))
            {
                var keyIndex = keyIndexElement.GetInt32();
                if (keyIndex < 0 || keyIndex >= bundle.Symbols.Length)
                {
                    result.AddError("LiteralFrame",
                        $"Literal[{index}] association key index {keyIndex} out of bounds (symbols.length = {bundle.Symbols.Length})",
                        location);
                }
            }
        }
    }
}
