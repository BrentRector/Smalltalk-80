using System.Text;
using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates that symbols and strings are valid UTF-8 and that symbols are unique.
/// Duplicate symbols defeat interning assumptions and selector packing.
/// </summary>
public class SymbolUtf8AndUniquenessValidator : IValidator
{
    public string Id => "SymbolUtf8AndUniquenessValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Validate symbols
        ValidateSymbols(diagnostics, ctx);

        // Validate strings
        ValidateStrings(diagnostics, ctx);

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateSymbols(List<Diagnostic> diagnostics, ValidationContext ctx)
    {
        var seenSymbols = new Dictionary<string, int>();

        for (int i = 0; i < ctx.Symbols.Count; i++)
        {
            var symbol = ctx.Symbols[i];

            // Check for null or empty
            if (string.IsNullOrEmpty(symbol))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.SymbolInvalidUtf8,
                    Severity.Error,
                    $"symbols[{i}] is null or empty",
                    new[] { new Location("symbols", Index: i) },
                    "Symbols cannot be null or empty"));
                continue;
            }

            // Check UTF-8 validity
            if (!IsValidUtf8(symbol))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.SymbolInvalidUtf8,
                    Severity.Error,
                    $"symbols[{i}] contains invalid UTF-8 encoding",
                    new[] { new Location("symbols", Index: i) },
                    "Ensure compiler emits valid UTF-8 encoded symbols"));
            }

            // Check for control characters
            if (ContainsProblematicControlCharacters(symbol))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.SymbolInvalidUtf8,
                    Severity.Warning,
                    $"symbols[{i}] contains control characters: '{EscapeString(symbol)}'",
                    new[] { new Location("symbols", Index: i) },
                    "Avoid control characters in symbols"));
            }

            // Check for duplicates
            if (seenSymbols.TryGetValue(symbol, out var previousIndex))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.SymbolDuplicate,
                    Severity.Error,
                    $"Duplicate symbol '{symbol}' at indices {previousIndex} and {i}",
                    new[] { new Location("symbols", Index: previousIndex), new Location("symbols", Index: i) },
                    "Deduplicate symbols in the compiler emitter"));
            }
            else
            {
                seenSymbols[symbol] = i;
            }
        }
    }

    private void ValidateStrings(List<Diagnostic> diagnostics, ValidationContext ctx)
    {
        var root = ctx.Bundle.RootElement;
        if (!root.TryGetProperty("strings", out var stringsArray))
            return;

        int i = 0;
        foreach (var stringElement in stringsArray.EnumerateArray())
        {
            // Strings can be null (representing nil)
            if (stringElement.ValueKind == JsonValueKind.Null)
            {
                i++;
                continue;
            }

            if (stringElement.ValueKind != JsonValueKind.String)
            {
                i++;
                continue;
            }

            var str = stringElement.GetString();
            if (str == null)
            {
                i++;
                continue;
            }

            // Check UTF-8 validity
            if (!IsValidUtf8(str))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.StringInvalidUtf8,
                    Severity.Error,
                    $"strings[{i}] contains invalid UTF-8 encoding",
                    new[] { new Location("strings", Index: i) },
                    "Ensure compiler emits valid UTF-8 encoded strings"));
            }

            // Check for null characters
            if (ContainsNullCharacter(str))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.StringInvalidUtf8,
                    Severity.Warning,
                    $"strings[{i}] contains null character (\\0)",
                    new[] { new Location("strings", Index: i) },
                    "Null characters can cause string processing issues"));
            }

            i++;
        }
    }

    private bool IsValidUtf8(string s)
    {
        try
        {
            // Try to encode and decode - will throw if invalid UTF-8
            var bytes = Encoding.UTF8.GetBytes(s);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded == s;
        }
        catch
        {
            return false;
        }
    }

    private bool ContainsProblematicControlCharacters(string s)
    {
        foreach (char c in s)
        {
            // Allow common whitespace
            if (c == '\t' || c == '\n' || c == '\r' || c == ' ')
                continue;

            // Flag other control characters
            if (char.IsControl(c))
                return true;
        }
        return false;
    }

    private bool ContainsNullCharacter(string s)
    {
        return s.Contains('\0');
    }

    private string EscapeString(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsControl(c))
            {
                sb.Append($"\\x{(int)c:X2}");
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
