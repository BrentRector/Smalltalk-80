namespace Smalltalk80.Validator;

/// <summary>
/// Severity levels for validation diagnostics
/// </summary>
public enum Severity
{
    /// <summary>
    /// Fatal error that will cause loader to fail
    /// </summary>
    Error,
    
    /// <summary>
    /// Likely bug but recoverable
    /// </summary>
    Warning,
    
    /// <summary>
    /// Informational or optimization suggestion
    /// </summary>
    Info
}

/// <summary>
/// Location information for a diagnostic
/// </summary>
public record DiagnosticLocation(
    string Section,
    string? LocalId = null,
    int? Index = null,
    int? Pc = null,
    string? Field = null
);

/// <summary>
/// A validation diagnostic with structured information
/// </summary>
public record Diagnostic(
    string Code,
    Severity Severity,
    string Message,
    IReadOnlyList<DiagnosticLocation> Locations,
    string? Suggestion = null
)
{
    public override string ToString()
    {
        var severityStr = Severity switch
        {
            Severity.Error => "ERROR",
            Severity.Warning => "WARNING",
            Severity.Info => "INFO",
            _ => "UNKNOWN"
        };
        
        var locationStr = Locations.Count > 0 
            ? $" ({string.Join(", ", Locations.Select(FormatLocation))})"
            : "";
        
        var suggestionStr = !string.IsNullOrEmpty(Suggestion) 
            ? $" — Suggestion: {Suggestion}"
            : "";
        
        return $"{severityStr} {Code}: {Message}{locationStr}{suggestionStr}";
    }
    
    private static string FormatLocation(DiagnosticLocation loc)
    {
        var parts = new List<string> { loc.Section };
        if (loc.LocalId != null) parts.Add(loc.LocalId);
        if (loc.Index.HasValue) parts.Add($"[{loc.Index}]");
        if (loc.Pc.HasValue) parts.Add($"pc={loc.Pc}");
        if (loc.Field != null) parts.Add($".{loc.Field}");
        return string.Join(":", parts);
    }
}

/// <summary>
/// Standard diagnostic codes for validators
/// </summary>
public static class DiagnosticCodes
{
    // Metadata validation
    public const string MetaCountMismatch = "STV-META-COUNT-MISMATCH";
    public const string MetaMissing = "STV-META-MISSING";
    
    // LocalId validation
    public const string LocalIdDuplicate = "STV-LOCALID-DUP";
    public const string LocalIdMalformed = "STV-LOCALID-PREFIX";
    public const string LocalIdMissing = "STV-LOCALID-MISSING";
    
    // Install order validation
    public const string InstallMissing = "STV-INSTALL-MISSING";
    public const string InstallOmitted = "STV-INSTALL-OMITTED";
    public const string InstallKindMismatch = "STV-INSTALL-KIND-MISMATCH";
    public const string InstallDuplicate = "STV-INSTALL-DUP";
    
    // Block/closure validation
    public const string BlockHomeMissing = "STV-BLOCK-HOME-MISSING";
    public const string BlockOuterMissing = "STV-BLOCK-OUTER-MISSING";
    public const string BlockOuterNotMethod = "STV-BLOCK-OUTER-NOT-METHOD";
    public const string BlockCycle = "STV-BLOCK-CYCLE";
    
    // Method validation
    public const string MethodMissingInDict = "STV-METHOD-MISSING-IN-DICT";
    public const string MethodSelectorInvalid = "STV-METHOD-SELECTOR-INVALID";
    
    // Symbol/string validation
    public const string SymbolInvalidUtf8 = "STV-SYMBOL-INVALID-UTF8";
    public const string SymbolDuplicate = "STV-SYMBOL-DUP";
    public const string StringInvalidUtf8 = "STV-STRING-INVALID-UTF8";
    
    // Class hierarchy validation
    public const string ClassSuperMissing = "STV-CLASS-SUPER-MISSING";
    public const string ClassSuperCycle = "STV-CLASS-SUPER-CYCLE";
    
    // Patch validation
    public const string PatchTargetMissing = "STV-PATCH-TARGET-MISSING";
    public const string PatchInvalid = "STV-PATCH-INVALID";
}