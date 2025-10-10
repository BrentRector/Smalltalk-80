using System.Text.Json;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates BlockContext closure graph integrity:
/// - homeContext chains terminate at methods (not cycles)
/// - outerContext references are valid and point to methods
/// - No orphaned blocks
/// - Proper lexical scoping structure
/// </summary>
public class BlockContextClosureGraphValidator
{
    private class BlockContextInfo
    {
        public string LocalId { get; set; } = "";
        public string? HomeContext { get; set; }
        public string? OuterContext { get; set; }
        public string? Method { get; set; }  // The compiled method localId for this block
    }
    
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Build map of all block contexts
        var blockContexts = new Dictionary<string, BlockContextInfo>();
        
        // Find all block contexts in objects
        foreach (var obj in bundle.Objects)
        {
            if (obj.FormatHint == "blockContext" || obj.LocalId.StartsWith("blockctx:"))
            {
                var blockInfo = ExtractBlockContextInfo(obj);
                if (blockInfo != null)
                {
                    blockContexts[obj.LocalId] = blockInfo;
                }
            }
        }
        
        // Find all blocks in methods (blocks are compiled as methods with null selector)
        foreach (var method in bundle.Methods)
        {
            if (method.SelectorIndex == -1 || method.LocalId.StartsWith("block:"))
            {
                // This is a block compiled as a method
                if (!blockContexts.ContainsKey(method.LocalId))
                {
                    blockContexts[method.LocalId] = new BlockContextInfo
                    {
                        LocalId = method.LocalId,
                        Method = method.LocalId,
                        HomeContext = method.HomeClassLocalId  // May contain the home method
                    };
                }
            }
        }
        
        // Validate each block context
        foreach (var kvp in blockContexts)
        {
            var blockId = kvp.Key;
            var blockInfo = kvp.Value;
            
            ValidateBlockContext(result, context, blockInfo, blockContexts);
        }
        
        // Check for cycles in home context chains
        DetectCycles(result, blockContexts);
        
        // Add summary
        if (blockContexts.Count > 0)
        {
            if (result.IsValid)
            {
                result.AddInfo($"✓ All {blockContexts.Count} block contexts have valid closure graphs");
            }
            else
            {
                result.AddInfo($"Validated {blockContexts.Count} block contexts");
            }
        }
        else
        {
            result.AddInfo("No block contexts found to validate");
        }
        
        return result;
    }
    
    private BlockContextInfo? ExtractBlockContextInfo(ObjectDefinition obj)
    {
        var info = new BlockContextInfo
        {
            LocalId = obj.LocalId
        };
        
        // Try to extract blockContextStructure if it exists
        if (obj.Fields != null)
        {
            // Look for fields that might contain home/outer context references
            // This depends on the exact JSON structure, which may vary
            
            // Common patterns:
            // - First field might be homeContext
            // - Second field might be outerContext
            // - Or there might be a blockContextStructure field
            
            foreach (var field in obj.Fields)
            {
                if (field is JsonElement jsonField)
                {
                    if (jsonField.ValueKind == JsonValueKind.Object)
                    {
                        // Check for blockContextStructure
                        if (jsonField.TryGetProperty("blockContextStructure", out var bcs))
                        {
                            if (bcs.TryGetProperty("homeContext", out var home))
                            {
                                info.HomeContext = GetStringValue(home);
                            }
                            if (bcs.TryGetProperty("outerContext", out var outer))
                            {
                                info.OuterContext = GetStringValue(outer);
                            }
                            if (bcs.TryGetProperty("method", out var method))
                            {
                                info.Method = GetStringValue(method);
                            }
                        }
                        // Check for direct homeContext/outerContext properties
                        else if (jsonField.TryGetProperty("homeContext", out var home))
                        {
                            info.HomeContext = GetStringValue(home);
                        }
                        else if (jsonField.TryGetProperty("outerContext", out var outer))
                        {
                            info.OuterContext = GetStringValue(outer);
                        }
                    }
                    else if (jsonField.ValueKind == JsonValueKind.String)
                    {
                        var value = jsonField.GetString();
                        if (value != null && value.StartsWith("blockctx:"))
                        {
                            // Heuristic: first block reference might be home
                            if (info.HomeContext == null)
                                info.HomeContext = value;
                        }
                        else if (value != null && value.StartsWith("method:"))
                        {
                            // Method reference might be outer context
                            if (info.OuterContext == null)
                                info.OuterContext = value;
                        }
                    }
                }
            }
        }
        
        return info;
    }
    
    private string? GetStringValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();
        if (element.ValueKind == JsonValueKind.Null)
            return null;
        return null;
    }
    
    private void ValidateBlockContext(ValidationResult result, ValidationContext context,
        BlockContextInfo blockInfo, Dictionary<string, BlockContextInfo> allBlocks)
    {
        var blockId = blockInfo.LocalId;
        
        // Validate homeContext
        if (!string.IsNullOrEmpty(blockInfo.HomeContext))
        {
            if (!context.LocalIdExists(blockInfo.HomeContext))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.BlockHomeMissing,
                    Severity.Error,
                    $"Block '{blockId}' references non-existent homeContext '{blockInfo.HomeContext}'",
                    new DiagnosticLocation("objects", blockId),
                    "HomeContext must reference an existing method or block");
            }
            else
            {
                // Verify home context chain eventually reaches a method
                if (!VerifyReachesMethod(blockInfo.HomeContext, allBlocks, new HashSet<string>()))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.BlockHomeMissing,
                        Severity.Warning,
                        $"Block '{blockId}' homeContext chain does not reach a method",
                        new DiagnosticLocation("objects", blockId),
                        "HomeContext chain should terminate at a method");
                }
            }
        }
        
        // Validate outerContext
        if (!string.IsNullOrEmpty(blockInfo.OuterContext))
        {
            if (!context.LocalIdExists(blockInfo.OuterContext))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.BlockOuterMissing,
                    Severity.Error,
                    $"Block '{blockId}' references non-existent outerContext '{blockInfo.OuterContext}'",
                    new DiagnosticLocation("objects", blockId),
                    "OuterContext must reference an existing method");
            }
            else if (!blockInfo.OuterContext.StartsWith("method:"))
            {
                // OuterContext should always be a method (lexical parent)
                result.AddDiagnostic(
                    DiagnosticCodes.BlockOuterNotMethod,
                    Severity.Error,
                    $"Block '{blockId}' outerContext '{blockInfo.OuterContext}' is not a method",
                    new DiagnosticLocation("objects", blockId),
                    "OuterContext must reference a method (lexical parent)");
            }
        }
        
        // Validate method reference if present
        if (!string.IsNullOrEmpty(blockInfo.Method))
        {
            if (!context.LocalIdExists(blockInfo.Method))
            {
                result.AddDiagnostic(
                    DiagnosticCodes.BlockHomeMissing,
                    Severity.Error,
                    $"Block '{blockId}' references non-existent method '{blockInfo.Method}'",
                    new DiagnosticLocation("objects", blockId),
                    "Method reference must exist");
            }
        }
    }
    
    private bool VerifyReachesMethod(string contextId, Dictionary<string, BlockContextInfo> allBlocks,
        HashSet<string> visited)
    {
        // Check if this is a method
        if (contextId.StartsWith("method:"))
            return true;
        
        // Avoid infinite loops
        if (visited.Contains(contextId))
            return false;
        visited.Add(contextId);
        
        // If it's a block, follow its home context
        if (allBlocks.TryGetValue(contextId, out var blockInfo))
        {
            if (!string.IsNullOrEmpty(blockInfo.HomeContext))
            {
                return VerifyReachesMethod(blockInfo.HomeContext, allBlocks, visited);
            }
        }
        
        // Unknown or orphaned
        return false;
    }
    
    private void DetectCycles(ValidationResult result, Dictionary<string, BlockContextInfo> blockContexts)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        
        foreach (var blockId in blockContexts.Keys)
        {
            if (!visited.Contains(blockId))
            {
                DetectCyclesHelper(blockId, blockContexts, visited, recursionStack, result);
            }
        }
    }
    
    private bool DetectCyclesHelper(string blockId, Dictionary<string, BlockContextInfo> blockContexts,
        HashSet<string> visited, HashSet<string> recursionStack, ValidationResult result)
    {
        visited.Add(blockId);
        recursionStack.Add(blockId);
        
        if (blockContexts.TryGetValue(blockId, out var blockInfo))
        {
            // Check home context
            if (!string.IsNullOrEmpty(blockInfo.HomeContext))
            {
                if (recursionStack.Contains(blockInfo.HomeContext))
                {
                    // Found a cycle
                    result.AddDiagnostic(
                        DiagnosticCodes.BlockCycle,
                        Severity.Error,
                        $"Closure cycle detected: '{blockId}' -> '{blockInfo.HomeContext}' forms a cycle",
                        new DiagnosticLocation("objects", blockId),
                        "HomeContext chains must not form cycles");
                    return true;
                }
                
                if (blockContexts.ContainsKey(blockInfo.HomeContext) && !visited.Contains(blockInfo.HomeContext))
                {
                    if (DetectCyclesHelper(blockInfo.HomeContext, blockContexts, visited, recursionStack, result))
                    {
                        return true;
                    }
                }
            }
        }
        
        recursionStack.Remove(blockId);
        return false;
    }
}