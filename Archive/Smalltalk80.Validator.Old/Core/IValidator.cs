namespace Smalltalk80.Validator.Core;

public interface IValidator
{
    string Id { get; }
    bool IsHeavy { get; }
    Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default);
}
