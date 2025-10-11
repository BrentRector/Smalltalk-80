using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates method primitive indices:
/// - Primitive indices are in valid range
/// - Blocks have primitiveIndex=0
/// - Primitive encoding matches header flags
/// - Known primitives are used correctly
/// </summary>
public class MethodPrimitiveValidityValidator
{
    // Smalltalk-80 Blue Book primitive ranges
    private const int MinPrimitive = 0;
    private const int MaxPrimitive = 255; // Extended in modern VMs, but 255 is safe upper bound
    
    // Common primitive groups
    private static readonly Dictionary<string, (int Min, int Max)> PrimitiveGroups = new()
    {
        // Arithmetic primitives
        { "SmallInteger arithmetic", (1, 17) },
        { "LargeInteger arithmetic", (21, 37) },
        { "Float arithmetic", (41, 59) },
        
        // Array/Stream primitives  
        { "Array/Stream access", (60, 67) },
        
        // Storage management
        { "Object access", (68, 79) },
        
        // Control primitives
        { "Block/Context", (80, 89) },
        
        // System primitives
        { "System", (110, 119) },
        
        // File primitives
        { "File I/O", (150, 169) },
        
        // Optional/Extended primitives
        { "Optional", (200, 255) }
    };
    
    // Well-known primitive indices and their expected selectors
    private static readonly Dictionary<int, string[]> WellKnownPrimitives = new()
    {
        { 1, new[] { "+", "plus:" } },
        { 2, new[] { "-", "minus:" } },
        { 3, new[] { "<", "lessThan:" } },
        { 4, new[] { ">", "greaterThan:" } },
        { 5, new[] { "<=", "lessThanOrEqual:" } },
        { 6, new[] { ">=", "greaterThanOrEqual:" } },
        { 7, new[] { "=", "equals:" } },
        { 8, new[] { "~=", "notEquals:", "!=" } },
        { 9, new[] { "*", "times:" } },
        { 10, new[] { "/", "divide:", "quo:" } },
        { 11, new[] { "\\\\", "mod:", "rem:" } },
        { 12, new[] { "//", "div:" } },
        { 13, new[] { "quo:" } },
        { 14, new[] { "bitAnd:" } },
        { 15, new[] { "bitOr:" } },
        { 16, new[] { "bitXor:" } },
        { 17, new[] { "bitShift:" } },
        
        { 60, new[] { "at:" } },
        { 61, new[] { "at:put:" } },
        { 62, new[] { "size" } },
        { 63, new[] { "next" } },
        { 64, new[] { "nextPut:" } },
        { 65, new[] { "atEnd" } },
        
        { 70, new[] { "new", "basicNew" } },
        { 71, new[] { "new:", "basicNew:" } },
        { 75, new[] { "hash" } },
        
        { 80, new[] { "blockCopy:" } },
        { 81, new[] { "value", "value:" } },
        { 82, new[] { "value:value:" } },
        { 83, new[] { "perform:" } },
        { 84, new[] { "perform:with:" } },
        
        { 110, new[] { "==", "identical:" } },
        { 111, new[] { "class" } }
    };
    
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        int methodsWithPrimitives = 0;
        int blocksChecked = 0;
        
        foreach (var method in bundle.Methods)
        {
            var primitiveIndex = method.Header.PrimitiveIndex ?? 0;
            var selector = method.SelectorIndex >= 0 ? context.GetSymbol(method.SelectorIndex) : null;
            var isBlock = method.SelectorIndex == -1 || method.LocalId.StartsWith("block:");
            
            if (isBlock)
            {
                blocksChecked++;
                ValidateBlockPrimitive(result, method, primitiveIndex);
            }
            else
            {
                ValidateMethodPrimitive(result, context, method, primitiveIndex, selector);
                if (primitiveIndex > 0)
                {
                    methodsWithPrimitives++;
                }
            }
            
            // Validate primitive encoding in header
            ValidatePrimitiveHeaderEncoding(result, method);
        }
        
        // Add summary
        if (result.IsValid)
        {
            result.AddInfo($"✓ All primitives validated: {methodsWithPrimitives} methods with primitives, " +
                          $"{blocksChecked} blocks verified");
        }
        else
        {
            result.AddInfo($"Validated primitives in {bundle.Methods.Length} methods");
        }
        
        return result;
    }
    
    private void ValidateBlockPrimitive(ValidationResult result, MethodDefinition method, int primitiveIndex)
    {
        // Blocks should have primitiveIndex=0 (no primitive)
        if (primitiveIndex != 0)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                $"Block '{method.LocalId}' has primitiveIndex={primitiveIndex} (blocks cannot have primitives)",
                new DiagnosticLocation("methods", method.LocalId),
                "Set primitiveIndex to 0 for blocks");
        }
    }
    
    private void ValidateMethodPrimitive(ValidationResult result, ValidationContext context,
        MethodDefinition method, int primitiveIndex, string? selector)
    {
        if (primitiveIndex == 0)
        {
            // No primitive - this is fine
            return;
        }
        
        // Check primitive range
        if (primitiveIndex < MinPrimitive || primitiveIndex > MaxPrimitive)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Error,
                $"Method '{method.LocalId}' has invalid primitiveIndex={primitiveIndex} (valid range: {MinPrimitive}-{MaxPrimitive})",
                new DiagnosticLocation("methods", method.LocalId),
                $"Use primitive index in range {MinPrimitive}-{MaxPrimitive}");
            return;
        }
        
        // Check if primitive matches known selector patterns
        if (selector != null && WellKnownPrimitives.TryGetValue(primitiveIndex, out var expectedSelectors))
        {
            if (!expectedSelectors.Contains(selector))
            {
                // This is a warning, not an error - custom uses are possible
                result.AddDiagnostic(
                    DiagnosticCodes.PatchInvalid,
                    Severity.Warning,
                    $"Method '{selector}' uses primitive {primitiveIndex} which is typically for {string.Join("/", expectedSelectors)}",
                    new DiagnosticLocation("methods", method.LocalId),
                    $"Verify primitive {primitiveIndex} is correct for selector '{selector}'");
            }
        }
        
        // Check primitive groups for informational purposes
        foreach (var group in PrimitiveGroups)
        {
            if (primitiveIndex >= group.Value.Min && primitiveIndex <= group.Value.Max)
            {
                // Valid primitive in known group - no action needed
                break;
            }
        }
        
        // Special validation for specific primitives
        ValidateSpecificPrimitives(result, context, method, primitiveIndex, selector);
    }
    
    private void ValidateSpecificPrimitives(ValidationResult result, ValidationContext context,
        MethodDefinition method, int primitiveIndex, string? selector)
    {
        switch (primitiveIndex)
        {
            case 70: // new
            case 71: // new:
                // These should be on class side (metaclass)
                if (!method.HomeClassLocalId.StartsWith("meta:"))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Primitive {primitiveIndex} (new) typically belongs on class side, but method is on instance side",
                        new DiagnosticLocation("methods", method.LocalId),
                        "Consider moving to class side or verify primitive is correct");
                }
                break;
                
            case 60: // at:
            case 61: // at:put:
                // Should have correct number of arguments
                var expectedArgs = primitiveIndex == 60 ? 1 : 2;
                if (method.Header.ArgCount != expectedArgs)
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Primitive {primitiveIndex} expects {expectedArgs} arguments but method has {method.Header.ArgCount}",
                        new DiagnosticLocation("methods", method.LocalId),
                        $"Verify argument count matches primitive requirements");
                }
                break;
                
            case 80: // blockCopy:
                // Should be in BlockContext or similar
                if (selector != null && !selector.Contains("block") && !selector.Contains("Block"))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Primitive 80 (blockCopy:) used in method '{selector}' which doesn't appear block-related",
                        new DiagnosticLocation("methods", method.LocalId),
                        "Primitive 80 is typically for block creation");
                }
                break;
        }
    }
    
    private void ValidatePrimitiveHeaderEncoding(ValidationResult result, MethodDefinition method)
    {
        var header = method.Header;
        var word = header.Word;
        
        // Decode flags field (bits 12-14)
        var flags = (word >> 12) & 0x07;
        
        // If method has a primitive, it should use extended form (flags=7)
        if (header.PrimitiveIndex.HasValue && header.PrimitiveIndex.Value > 0)
        {
            if (flags != 7)
            {
                // Some primitives might use quick-return forms (flags 5-6)
                // but most should use extended form
                if (flags < 5)
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Warning,
                        $"Method with primitive {header.PrimitiveIndex.Value} has flags={flags} (expected 7 for extended form)",
                        new DiagnosticLocation("methods", method.LocalId),
                        "Methods with primitives typically use extended header form (flags=7)");
                }
            }
            else
            {
                // Extended form - verify last literal contains primitive/argcount encoding
                if (method.Literals.Count == 0)
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Error,
                        $"Extended form method missing literal for primitive/argcount encoding",
                        new DiagnosticLocation("methods", method.LocalId),
                        "Extended form requires last literal to encode primitive and argcount");
                }
            }
        }
        
        // Check for primitive index consistency
        var declaredPrimitive = header.PrimitiveIndex ?? 0;
        
        // If using extended form, primitive should be encoded in last literal
        if (flags == 7 && method.Literals.Count > 0)
        {
            // Extended form encoding is validated in MethodHeaderValidator
            // We just check for consistency here
            if (declaredPrimitive > 0 && declaredPrimitive > 255)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.PatchInvalid,
                    Severity.Error,
                    $"Primitive index {declaredPrimitive} exceeds maximum encodable value (255)",
                    new DiagnosticLocation("methods", method.LocalId),
                    "Primitive index must be <= 255");
            }
        }
    }
}