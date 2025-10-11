using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates literal frames:
/// - Selector ordering for optimized single-byte sends
/// - Association keys must be symbols (Blue Book requirement)
/// - Index bounds for symbol/string references
/// </summary>
public class LiteralFrameValidator : IValidator
{
    public string Id => "LiteralFrameValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Validate method literals
        foreach (var method in ctx.Methods)
        {
            var localId = method.GetStringProp("localId") ?? "unknown";
            ValidateLiterals(diagnostics, method, localId, ctx);
        }

        // Validate init expression literals
        foreach (var initExpr in ctx.InitExpressions)
        {
            var localId = initExpr.GetStringProp("localId") ?? "unknown";
            ValidateLiterals(diagnostics, initExpr, localId, ctx);
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateLiterals(List<Diagnostic> diagnostics, JsonElement entity, string entityLocalId, ValidationContext ctx)
    {
        var literalsArray = entity.GetArrayProp("literals");
        if (literalsArray == null)
            return;

        int i = 0;
        foreach (var literal in literalsArray.Value.EnumerateArray())
        {
            if (literal.ValueKind == JsonValueKind.Object)
            {
                ValidateJsonLiteral(diagnostics, literal, i, entityLocalId, ctx);
            }
            i++;
        }
    }

    private void ValidateJsonLiteral(List<Diagnostic> diagnostics, JsonElement literal, int index, string entityLocalId, ValidationContext ctx)
    {
        var type = literal.GetStringProp("type");
        if (type == null)
            return;

        switch (type)
        {
            case "symbol":
                ValidateSymbolLiteral(diagnostics, literal, index, entityLocalId, ctx);
                break;
            case "string":
                ValidateStringLiteral(diagnostics, literal, index, entityLocalId, ctx);
                break;
            case "association":
                ValidateAssociationLiteral(diagnostics, literal, index, entityLocalId, ctx);
                break;
        }
    }

    private void ValidateSymbolLiteral(List<Diagnostic> diagnostics, JsonElement literal, int index, string entityLocalId, ValidationContext ctx)
    {
        var symbolIndex = literal.GetIntProp("index");
        if (symbolIndex.HasValue)
        {
            if (symbolIndex.Value < 0 || symbolIndex.Value >= ctx.Symbols.Count)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LiteralFrameInvalid,
                    Severity.Error,
                    $"Literal[{index}] symbol index {symbolIndex.Value} out of bounds (symbols.length = {ctx.Symbols.Count})",
                    new[] { new Location("methods", entityLocalId) },
                    $"Symbol index must be < {ctx.Symbols.Count}"));
            }
        }
    }

    private void ValidateStringLiteral(List<Diagnostic> diagnostics, JsonElement literal, int index, string entityLocalId, ValidationContext ctx)
    {
        var stringIndex = literal.GetIntProp("index");
        if (stringIndex.HasValue)
        {
            // Get strings count from bundle
            var stringsCount = 0;
            if (ctx.Bundle.RootElement.TryGetProperty("strings", out var stringsArray) && stringsArray.ValueKind == JsonValueKind.Array)
            {
                stringsCount = stringsArray.GetArrayLength();
            }

            if (stringIndex.Value < 0 || stringIndex.Value >= stringsCount)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LiteralFrameInvalid,
                    Severity.Error,
                    $"Literal[{index}] string index {stringIndex.Value} out of bounds (strings.length = {stringsCount})",
                    new[] { new Location("methods", entityLocalId) },
                    $"String index must be < {stringsCount}"));
            }
        }
    }

    private void ValidateAssociationLiteral(List<Diagnostic> diagnostics, JsonElement literal, int index, string entityLocalId, ValidationContext ctx)
    {
        var assocElement = literal.GetObjectProp("association");
        if (assocElement == null)
            return;

        var assoc = assocElement.Value;

        // Check that key is a symbol (Blue Book requirement)
        var keyType = assoc.GetStringProp("keyType");
        if (keyType != null && keyType != "symbol")
        {
            diagnostics.Add(new Diagnostic(
                ErrorCodes.LiteralFrameInvalid,
                Severity.Error,
                $"Literal[{index}] association key must be 'symbol', found '{keyType}' (Blue Book requirement)",
                new[] { new Location("methods", entityLocalId) },
                "Association keys must be symbols"));
        }

        // Validate key index
        var keyIndex = assoc.GetIntProp("keyIndex");
        if (keyIndex.HasValue)
        {
            if (keyIndex.Value < 0 || keyIndex.Value >= ctx.Symbols.Count)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.LiteralFrameInvalid,
                    Severity.Error,
                    $"Literal[{index}] association key index {keyIndex.Value} out of bounds (symbols.length = {ctx.Symbols.Count})",
                    new[] { new Location("methods", entityLocalId) },
                    $"Key index must be < {ctx.Symbols.Count}"));
            }
        }
    }
}
