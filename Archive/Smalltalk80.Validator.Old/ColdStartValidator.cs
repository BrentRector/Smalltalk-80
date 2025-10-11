using Smalltalk80.Validator.JsonModels;
using Smalltalk80.Validator.Validators;

namespace Smalltalk80.Validator;

/// <summary>
/// Main coordinator for cold-start JSON validation.
/// Runs all validation modules and aggregates results.
/// </summary>
public class ColdStartValidator
{
    public ValidationResult Validate(ColdStartBundle bundle, bool verbose = false)
    {
        var result = new ValidationResult();

        Console.WriteLine("Running validators...\n");
        
        // Phase 1: Structural integrity checks (fast, fail-fast)
        Console.WriteLine("Phase 1: Structural Integrity");
        
        // NEW: Metadata Counts Validator
        Console.Write("  MetadataCountsValidator... ");
        var metadataResult = new MetadataCountsValidator().Validate(bundle);
        result.Merge(metadataResult);
        Console.WriteLine(metadataResult.IsValid ? "✓" : $"✗ ({metadataResult.GetTotalErrorCount()} errors)");
        
        // NEW: LocalId Uniqueness Validator
        Console.Write("  LocalIdUniquenessValidator... ");
        var localIdResult = new LocalIdUniquenessValidator().Validate(bundle);
        result.Merge(localIdResult);
        Console.WriteLine(localIdResult.IsValid ? "✓" : $"✗ ({localIdResult.GetTotalErrorCount()} errors)");
        
        // NEW: Symbol UTF-8 and Uniqueness Validator
        Console.Write("  SymbolUtf8AndUniquenessValidator... ");
        var symbolResult = new SymbolUtf8AndUniquenessValidator().Validate(bundle);
        result.Merge(symbolResult);
        Console.WriteLine(symbolResult.IsValid ? "✓" : $"✗ ({symbolResult.GetTotalErrorCount()} errors)");
        
        // Phase 2: Semantic validation (existing validators)
        Console.WriteLine("\nPhase 2: Semantic Validation");

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
        
        // NEW: Enhanced Patch Target Existence Validator
        Console.Write("  PatchTargetExistenceValidator... ");
        var patchTargetResult = new PatchTargetExistenceValidator().Validate(bundle);
        result.Merge(patchTargetResult);
        Console.WriteLine(patchTargetResult.IsValid ? "✓" : $"✗ ({patchTargetResult.GetTotalErrorCount()} errors)");

        // 5. Install Order Validator (replaced with enhanced version)
        Console.Write("  InstallOrderCoverageValidator... ");
        var installOrderResult = new InstallOrderCoverageValidator().Validate(bundle);
        result.Merge(installOrderResult);
        Console.WriteLine(installOrderResult.IsValid ? "✓" : $"✗ ({installOrderResult.GetTotalErrorCount()} errors)");

        // 6. Object Format Validator
        Console.Write("  ObjectFormatValidator... ");
        var formatResult = new ObjectFormatValidator().Validate(bundle);
        result.Merge(formatResult);
        Console.WriteLine(formatResult.IsValid ? "✓" : $"✗ ({formatResult.Errors.Count} errors)");
        
        // NEW: Block Context Closure Graph Validator
        Console.Write("  BlockContextClosureGraphValidator... ");
        var blockResult = new BlockContextClosureGraphValidator().Validate(bundle);
        result.Merge(blockResult);
        Console.WriteLine(blockResult.IsValid ? "✓" : $"✗ ({blockResult.GetTotalErrorCount()} errors)");
        
        // Phase 3: Deep semantic validation
        Console.WriteLine("\nPhase 3: Deep Semantic Validation");
        
        // NEW: Class Hierarchy Sanity Validator
        Console.Write("  ClassHierarchySanityValidator... ");
        var hierarchyResult = new ClassHierarchySanityValidator().Validate(bundle);
        result.Merge(hierarchyResult);
        Console.WriteLine(hierarchyResult.IsValid ? "✓" : $"✗ ({hierarchyResult.GetTotalErrorCount()} errors)");
        
        // NEW: Method Primitive Validity Validator
        Console.Write("  MethodPrimitiveValidityValidator... ");
        var primitiveResult = new MethodPrimitiveValidityValidator().Validate(bundle);
        result.Merge(primitiveResult);
        Console.WriteLine(primitiveResult.IsValid ? "✓" : $"✗ ({primitiveResult.GetTotalErrorCount()} errors)");
        
        // NEW: Method Selector Presence Validator
        Console.Write("  MethodSelectorPresenceValidator... ");
        var selectorResult = new MethodSelectorPresenceValidator().Validate(bundle);
        result.Merge(selectorResult);
        Console.WriteLine(selectorResult.IsValid ? "✓" : $"✗ ({selectorResult.GetTotalErrorCount()} errors)");
        
        // NEW: Pool Dictionary Usage Validator
        Console.Write("  PoolDictionaryUsageValidator... ");
        var poolResult = new PoolDictionaryUsageValidator().Validate(bundle);
        result.Merge(poolResult);
        Console.WriteLine(poolResult.IsValid ? "✓" : $"✗ ({poolResult.GetTotalErrorCount()} errors)");
        
        // Phase 4: Static Analysis
        Console.WriteLine("\nPhase 4: Static Analysis");
        
        // NEW: Dead Code Detection Validator
        Console.Write("  DeadCodeDetectionValidator... ");
        var deadCodeResult = new DeadCodeDetectionValidator().Validate(bundle);
        result.Merge(deadCodeResult);
        Console.WriteLine(deadCodeResult.IsValid ? "✓" : $"✗ ({deadCodeResult.GetTotalErrorCount()} errors)");
        
        // Print diagnostic summary if verbose mode
        if (verbose && result.Diagnostics.Count > 0)
        {
            Console.WriteLine("\nDetailed Diagnostics:");
            foreach (var diag in result.Diagnostics.OrderBy(d => d.Severity))
            {
                Console.WriteLine($"  {diag}");
            }
        }

        return result;
    }
}
