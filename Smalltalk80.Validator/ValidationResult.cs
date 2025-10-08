namespace Smalltalk80.Validator;

/// <summary>
/// Represents the result of a validation check.
/// </summary>
public class ValidationResult
{
    public List<ValidationError> Errors { get; } = new();
    public List<ValidationWarning> Warnings { get; } = new();
    public List<string> Info { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void AddError(string category, string message, string? location = null)
    {
        Errors.Add(new ValidationError(category, message, location));
    }

    public void AddWarning(string category, string message, string? location = null)
    {
        Warnings.Add(new ValidationWarning(category, message, location));
    }

    public void AddInfo(string message)
    {
        Info.Add(message);
    }

    public void Merge(ValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
        Info.AddRange(other.Info);
    }
}

public record ValidationError(string Category, string Message, string? Location = null);

public record ValidationWarning(string Category, string Message, string? Location = null);
