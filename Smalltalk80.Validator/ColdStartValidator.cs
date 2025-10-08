using Smalltalk80.Validator.JsonModels;
using Smalltalk80.Validator.Validators;

namespace Smalltalk80.Validator;

/// <summary>
/// Main coordinator for cold-start JSON validation.
/// Runs all validation modules and aggregates results.
/// </summary>
public class ColdStartValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        Console.WriteLine("Running validators...\n");

        // 1. Method Header Validator
        Console.Write("  MethodHeaderValidator... ");
        var headerResult = new MethodHeaderValidator().Validate(bundle);
        result.Merge(headerResult);
        Console.WriteLine(headerResult.IsValid ? "✓" : $"✗ ({headerResult.Errors.Count} errors)");

        // 2. Bytecode Validator
        Console.Write("  BytecodeValidator... ");
        var bytecodeResult = new BytecodeValidator().Validate(bundle);
        result.Merge(bytecodeResult);
        Console.WriteLine(bytecodeResult.IsValid ? "✓" : $"✗ ({bytecodeResult.Errors.Count} errors)");

        // 3. Literal Frame Validator
        Console.Write("  LiteralFrameValidator... ");
        var literalResult = new LiteralFrameValidator().Validate(bundle);
        result.Merge(literalResult);
        Console.WriteLine(literalResult.IsValid ? "✓" : $"✗ ({literalResult.Errors.Count} errors)");

        // 4. Patch Validator
        Console.Write("  PatchValidator... ");
        var patchResult = new PatchValidator().Validate(bundle);
        result.Merge(patchResult);
        Console.WriteLine(patchResult.IsValid ? "✓" : $"✗ ({patchResult.Errors.Count} errors)");

        // 5. Install Order Validator
        Console.Write("  InstallOrderValidator... ");
        var installOrderResult = new InstallOrderValidator().Validate(bundle);
        result.Merge(installOrderResult);
        Console.WriteLine(installOrderResult.IsValid ? "✓" : $"✗ ({installOrderResult.Errors.Count} errors)");

        // 6. Object Format Validator
        Console.Write("  ObjectFormatValidator... ");
        var formatResult = new ObjectFormatValidator().Validate(bundle);
        result.Merge(formatResult);
        Console.WriteLine(formatResult.IsValid ? "✓" : $"✗ ({formatResult.Errors.Count} errors)");

        return result;
    }
}
