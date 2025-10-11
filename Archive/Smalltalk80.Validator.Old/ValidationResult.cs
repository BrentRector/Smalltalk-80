namespace Smalltalk80.Validator;

/// <summary>
/// Represents the result of a validation check.
/// </summary>
public class ValidationResult
{
    // Legacy properties for backward compatibility
    public List<ValidationError> Errors { get; } = new();
    public List<ValidationWarning> Warnings { get; } = new();
    public List<string> Info { get; } = new();
    
    // New diagnostic-based storage
    public List<Diagnostic> Diagnostics { get; } = new();

    public bool IsValid => Errors.Count == 0 && !Diagnostics.Any(d => d.Severity == Severity.Error);

    // Legacy methods
    public void AddError(string category, string message, string? location = null)
    {
        Errors.Add(new ValidationError(category, message, location));
    }

    public void AddWarning(string category, string message, string? location = null, bool verboseOnly = false)
    {
        Warnings.Add(new ValidationWarning(category, message, location, verboseOnly));
    }

    public void AddInfo(string message)
    {
        Info.Add(message);
    }

    // New diagnostic methods
    public void AddDiagnostic(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
        
        // Also add to legacy collections for backward compatibility
        switch (diagnostic.Severity)
        {
            case Severity.Error:
                var location = diagnostic.Locations.FirstOrDefault();
                Errors.Add(new ValidationError(
                    diagnostic.Code,
                    diagnostic.Message,
                    location?.LocalId ?? location?.Section));
                break;
            case Severity.Warning:
                location = diagnostic.Locations.FirstOrDefault();
                Warnings.Add(new ValidationWarning(
                    diagnostic.Code,
                    diagnostic.Message,
                    location?.LocalId ?? location?.Section,
                    false));
                break;
            case Severity.Info:
                Info.Add($"{diagnostic.Code}: {diagnostic.Message}");
                break;
        }
    }
    
    public void AddDiagnostic(string code, Severity severity, string message, 
        DiagnosticLocation location, string? suggestion = null)
    {
        AddDiagnostic(new Diagnostic(code, severity, message, new[] { location }, suggestion));
    }

    public void Merge(ValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
        Info.AddRange(other.Info);
        Diagnostics.AddRange(other.Diagnostics);
    }

    /// <summary>
    /// Gets warnings filtered by verbosity level.
    /// </summary>
    public IEnumerable<ValidationWarning> GetWarnings(bool verbose)
    {
        return verbose
            ? Warnings
            : Warnings.Where(w => !w.VerboseOnly);
    }
    
    /// <summary>
    /// Gets diagnostics filtered by severity.
    /// </summary>
    public IEnumerable<Diagnostic> GetDiagnostics(Severity minSeverity = Severity.Info)
    {
        return Diagnostics.Where(d => d.Severity <= minSeverity);
    }
    
    /// <summary>
    /// Gets error count from both legacy and diagnostic systems.
    /// </summary>
    public int GetTotalErrorCount()
    {
        // Count unique errors (avoid double-counting when diagnostic is also in Errors)
        var diagnosticErrors = Diagnostics.Count(d => d.Severity == Severity.Error);
        return Math.Max(Errors.Count, diagnosticErrors);
    }
}

public record ValidationError(string Category, string Message, string? Location = null);

public record ValidationWarning(string Category, string Message, string? Location = null, bool VerboseOnly = false);
