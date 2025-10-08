using System.Text.Json;
using Smalltalk80.Validator;
using Smalltalk80.Validator.JsonModels;

if (args.Length == 0)
{
    Console.WriteLine("Smalltalk-80 Cold Start Validator");
    Console.WriteLine("==================================");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run -- <coldstart.json>");
    Console.WriteLine();
    Console.WriteLine("Validates a cold-start JSON file against:");
    Console.WriteLine("  - Method header consistency");
    Console.WriteLine("  - Bytecode validity and stack discipline");
    Console.WriteLine("  - Literal frame structure");
    Console.WriteLine("  - Patch target resolution");
    Console.WriteLine("  - Install order completeness");
    Console.WriteLine("  - Object format hints");
    return 1;
}

var jsonPath = args[0];

if (!File.Exists(jsonPath))
{
    Console.WriteLine($"Error: File not found: {jsonPath}");
    return 1;
}

Console.WriteLine("Smalltalk-80 Cold Start Validator");
Console.WriteLine("==================================");
Console.WriteLine($"Input: {jsonPath}");
Console.WriteLine();

// Load JSON
Console.Write("Loading JSON... ");
ColdStartBundle? bundle;
try
{
    var jsonText = File.ReadAllText(jsonPath);
    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };
    bundle = JsonSerializer.Deserialize<ColdStartBundle>(jsonText, options);

    if (bundle == null)
    {
        Console.WriteLine("✗ Failed to deserialize JSON");
        return 1;
    }

    Console.WriteLine($"✓ ({bundle.Methods.Length} methods, {bundle.Classes.Length} classes)");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ {ex.Message}");
    return 1;
}

Console.WriteLine();

// Run validation
var validator = new ColdStartValidator();
var result = validator.Validate(bundle);

Console.WriteLine();
Console.WriteLine("=== Validation Results ===");
Console.WriteLine();

// Summary
if (result.IsValid)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ Validation PASSED");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ Validation FAILED ({result.Errors.Count} errors)");
    Console.ResetColor();
}

Console.WriteLine();

// Show errors
if (result.Errors.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Errors ({result.Errors.Count}):");
    Console.ResetColor();

    var errorLimit = 20;
    foreach (var error in result.Errors.Take(errorLimit))
    {
        Console.WriteLine($"  [{error.Category}] {error.Message}");
        if (error.Location != null)
        {
            Console.WriteLine($"    at: {error.Location}");
        }
    }

    if (result.Errors.Count > errorLimit)
    {
        Console.WriteLine($"  ... and {result.Errors.Count - errorLimit} more errors");
    }

    Console.WriteLine();
}

// Show warnings
if (result.Warnings.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Warnings ({result.Warnings.Count}):");
    Console.ResetColor();

    var warningLimit = 10;
    foreach (var warning in result.Warnings.Take(warningLimit))
    {
        Console.WriteLine($"  [{warning.Category}] {warning.Message}");
        if (warning.Location != null)
        {
            Console.WriteLine($"    at: {warning.Location}");
        }
    }

    if (result.Warnings.Count > warningLimit)
    {
        Console.WriteLine($"  ... and {result.Warnings.Count - warningLimit} more warnings");
    }

    Console.WriteLine();
}

// Show info
if (result.Info.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Info:");
    Console.ResetColor();

    foreach (var info in result.Info)
    {
        Console.WriteLine($"  {info}");
    }

    Console.WriteLine();
}

return result.IsValid ? 0 : 1;
