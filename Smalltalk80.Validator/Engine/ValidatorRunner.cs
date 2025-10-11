using Smalltalk80.Validator.Core;

namespace Smalltalk80.Validator.Engine;

public class ValidatorRunner
{
    private readonly IEnumerable<IValidator> _validators;

    public ValidatorRunner(IEnumerable<IValidator> validators)
    {
        _validators = validators;
    }

    public async Task<IReadOnlyList<Diagnostic>> RunAllAsync(ValidationContext ctx, bool includeHeavy = false, CancellationToken ct = default)
    {
        var tasks = new List<Task<IEnumerable<Diagnostic>>>();
        foreach (var v in _validators.Where(v => !v.IsHeavy || includeHeavy))
        {
            tasks.Add(RunValidatorSafe(v, ctx, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var combined = tasks.SelectMany(t => t.Result).ToList();
        var dedup = combined
            .GroupBy(d => (d.Code, LocKey(d.Locations)))
            .Select(g => g.First())
            .OrderBy(d => d.Severity)
            .ThenBy(d => d.Code)
            .ToList();

        return dedup;

        static string LocKey(IReadOnlyList<Location> locs) =>
            string.Join("|", locs.Select(l => $"{l.Section}:{l.LocalId ?? ""}:{l.Index?.ToString() ?? ""}:{l.Pc?.ToString() ?? ""}"));
    }

    private static async Task<IEnumerable<Diagnostic>> RunValidatorSafe(IValidator v, ValidationContext ctx, CancellationToken ct)
    {
        try
        {
            return await v.ValidateAsync(ctx, ct).ConfigureAwait(false) ?? Array.Empty<Diagnostic>();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new[] {
                new Diagnostic(
                    "STV-VALIDATOR-EXCEPTION",
                    Severity.Error,
                    $"Validator '{v.Id}' crashed: {ex.Message}",
                    new[] { new Location("validator", v.Id) },
                    "Check validator implementation",
                    new Dictionary<string,string>{{"stack", ex.ToString()}})
            };
        }
    }
}
