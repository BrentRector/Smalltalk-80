using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates that metadata counts match actual array lengths.
/// Ensures compiler bookkeeping is accurate for reproducible builds.
/// </summary>
public class MetadataCountsValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Check if metadata exists
        if (bundle.Metadata == null)
        {
            result.AddDiagnostic(
                DiagnosticCodes.MetaMissing,
                Severity.Error,
                "Metadata section is missing from bundle",
                new DiagnosticLocation("metadata"),
                "Ensure compiler emits metadata section");
            return result;
        }
        
        // Validate each count
        ValidateCount(result, "symbolCount", bundle.Metadata.SymbolCount, bundle.Symbols.Length);
        ValidateCount(result, "stringCount", bundle.Metadata.StringCount, bundle.Strings.Length);
        ValidateCount(result, "classCount", bundle.Metadata.ClassCount, bundle.Classes.Length);
        ValidateCount(result, "methodCount", bundle.Metadata.MethodCount, bundle.Methods.Length);
        ValidateCount(result, "objectCount", bundle.Metadata.ObjectCount, bundle.Objects.Length);
        ValidateCount(result, "initExpressionCount", bundle.Metadata.InitExpressionCount, bundle.InitExpressions.Length);
        
        // Add summary info if all counts match
        if (result.IsValid)
        {
            result.AddInfo($"✓ All metadata counts verified: {bundle.Metadata.MethodCount} methods, " +
                          $"{bundle.Metadata.ClassCount} classes, {bundle.Metadata.SymbolCount} symbols");
        }
        
        return result;
    }
    
    private void ValidateCount(ValidationResult result, string fieldName, int? declaredCount, int actualCount)
    {
        if (!declaredCount.HasValue)
        {
            // Count field is missing - warning only as it might be optional
            result.AddDiagnostic(
                DiagnosticCodes.MetaCountMismatch,
                Severity.Warning,
                $"metadata.{fieldName} is missing (expected {actualCount})",
                new DiagnosticLocation("metadata", Field: fieldName),
                $"Add metadata.{fieldName} = {actualCount}");
            return;
        }
        
        if (declaredCount.Value != actualCount)
        {
            result.AddDiagnostic(
                DiagnosticCodes.MetaCountMismatch,
                Severity.Error,
                $"metadata.{fieldName}={declaredCount.Value} but actual array length={actualCount}",
                new DiagnosticLocation("metadata", Field: fieldName),
                $"Update metadata.{fieldName} to {actualCount} or fix compiler emitter");
        }
    }
}