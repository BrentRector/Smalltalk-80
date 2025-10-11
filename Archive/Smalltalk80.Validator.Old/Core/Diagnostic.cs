namespace Smalltalk80.Validator.Core;

public enum Severity { Error, Warning, Info }

public record Location(string Section, string? LocalId = null, int? Index = null, int? Pc = null);

public record Diagnostic(
    string Code,
    Severity Severity,
    string Message,
    IReadOnlyList<Location> Locations,
    string Suggestion,
    IReadOnlyDictionary<string,string>? Metadata = null
);
