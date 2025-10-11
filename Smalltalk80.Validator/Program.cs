using System.Text.Json;
using Smalltalk80.Validator.Core;
using Smalltalk80.Validator.Engine;
using Spectre.Console;

if (args.Length == 0)
{
    AnsiConsole.MarkupLine("[red]Usage: validator <path-to-coldstart.json>[/]");
    return 1;
}

var jsonPath = args[0];
if (!File.Exists(jsonPath))
{
    AnsiConsole.MarkupLine($"[red]File not found: {jsonPath}[/]");
    return 1;
}

try
{
    AnsiConsole.MarkupLine("[bold]Smalltalk-80 Validator[/]");
    AnsiConsole.MarkupLine($"Input: {jsonPath}\n");

    using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
    var ctx = ValidationContext.Build(doc);

    AnsiConsole.MarkupLine($"Loaded: {ctx.Methods.Count} methods, {ctx.Classes.Count} classes\n");

    var pipeline = new ValidatorPipeline()
        // Phase 1: Structural Integrity (fast, fail-fast)
        .AddValidator(new Smalltalk80.Validator.Core.Validators.MetadataCountsValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.LocalIdUniquenessValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.SymbolUtf8AndUniquenessValidator())

        // Phase 2: Semantic Validation
        .AddValidator(new Smalltalk80.Validator.Core.Validators.MethodHeaderValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.BytecodeValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.LiteralFrameValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.PatchValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.PatchTargetExistenceValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.InstallOrderValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.InstallOrderCoverageValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.ObjectFormatValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.BlockContextClosureGraphValidator())

        // Phase 3: Deep Semantic Validation
        .AddValidator(new Smalltalk80.Validator.Core.Validators.ClassHierarchySanityValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.MethodPrimitiveValidityValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.MethodSelectorPresenceValidator())
        .AddValidator(new Smalltalk80.Validator.Core.Validators.PoolDictionaryUsageValidator())

        // Phase 4: Static Analysis (heavy validators)
        .AddValidator(new Smalltalk80.Validator.Core.Validators.DeadCodeDetectionValidator());

    var runner = pipeline.Build();
    var diagnostics = await runner.RunAllAsync(ctx, includeHeavy: true);

    if (diagnostics.Count == 0)
    {
        AnsiConsole.MarkupLine("[green]✓ Validation PASSED[/]");
        return 0;
    }

    var errors = diagnostics.Where(d => d.Severity == Severity.Error).ToList();
    var warnings = diagnostics.Where(d => d.Severity == Severity.Warning).ToList();

    if (errors.Count > 0)
    {
        AnsiConsole.MarkupLine($"\n[red]Errors ({errors.Count}):[/]");
        foreach (var diag in errors)
        {
            var loc = diag.Locations.FirstOrDefault();
            var locStr = loc != null ? $" at {loc.Section}:{loc.LocalId}" : "";
            AnsiConsole.MarkupLine($"[red]  {diag.Code}: {diag.Message}{locStr}[/]");
        }
    }

    if (warnings.Count > 0)
    {
        AnsiConsole.MarkupLine($"\n[yellow]Warnings ({warnings.Count}):[/]");
        foreach (var diag in warnings.Take(10))
        {
            var loc = diag.Locations.FirstOrDefault();
            var locStr = loc != null ? $" at {loc.Section}:{loc.LocalId}" : "";
            AnsiConsole.MarkupLine($"[yellow]  {diag.Code}: {diag.Message}{locStr}[/]");
        }
        if (warnings.Count > 10)
        {
            AnsiConsole.MarkupLine($"[yellow]  ... and {warnings.Count - 10} more warnings[/]");
        }
    }

    return errors.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
    return 1;
}
