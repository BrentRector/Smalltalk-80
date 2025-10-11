using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates BlockContext closure graph integrity:
/// - homeContext chains terminate at methods (not cycles)
/// - outerContext references are valid and point to methods
/// - No orphaned blocks
/// - Proper lexical scoping structure
/// </summary>
public class BlockContextClosureGraphValidator : IValidator
{
    private class BlockContextInfo
    {
        public string BlockMethodId { get; set; } = "";
        public string? HomeContext { get; set; }
        public string? OuterContext { get; set; }
    }

    public string Id => "BlockContextClosureGraphValidator";
    public bool IsHeavy => true;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Build map of all block contexts from metadata.blockContexts (v1.0.5)
        var blockContexts = new Dictionary<string, BlockContextInfo>();

        // Read BlockContext metadata from metadata.blockContexts array
        foreach (var blockCtx in ctx.BlockContexts)
        {
            var blockMethodId = blockCtx.GetStringProp("blockMethodId");
            if (blockMethodId == null) continue;

            var homeContext = blockCtx.GetStringProp("homeContext");
            var outerContext = blockCtx.GetStringProp("outerContext");

            var blockInfo = new BlockContextInfo
            {
                BlockMethodId = blockMethodId,
                HomeContext = homeContext,
                OuterContext = outerContext
            };

            blockContexts[blockMethodId] = blockInfo;
        }

        // v1.0.4: Verify that there are NO BlockContext objects in objects[] array
        // (they should only be in metadata.blockContexts)
        foreach (var obj in ctx.Objects)
        {
            var localId = obj.GetStringProp("localId");
            var formatHint = obj.GetStringProp("formatHint");

            if (formatHint == "blockContext" || (localId != null && localId.StartsWith("blockctx:")))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.BlockContextInObjectsArray,
                    Severity.Error,
                    $"BlockContext '{localId}' found in objects[] array. v1.0.4: BlockContext entries must be in metadata.blockContexts[], not objects[]",
                    new[] { new Location("objects", localId ?? "") },
                    "Move BlockContext entries to metadata.blockContexts[] array"));
            }
        }

        // Validate each block context
        foreach (var kvp in blockContexts)
        {
            ValidateBlockContext(diagnostics, ctx, kvp.Value, blockContexts);
        }

        // Check for cycles in home context chains
        DetectCycles(diagnostics, blockContexts);

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

    private void ValidateBlockContext(List<Diagnostic> diagnostics, ValidationContext ctx,
        BlockContextInfo blockInfo, Dictionary<string, BlockContextInfo> allBlocks)
    {
        var blockId = blockInfo.BlockMethodId;

        // Validate homeContext
        if (!string.IsNullOrEmpty(blockInfo.HomeContext))
        {
            if (!ctx.IndexByLocalId.ContainsKey(blockInfo.HomeContext))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.BlockHomeMissing,
                    Severity.Error,
                    $"Block '{blockId}' references non-existent homeContext '{blockInfo.HomeContext}'",
                    new[] { new Location("metadata.blockContexts", blockId) },
                "HomeContext must reference an existing method or block"));
            }
            else
            {
                // Verify home context chain eventually reaches a method
                if (!VerifyReachesMethod(blockInfo.HomeContext, allBlocks, new HashSet<string>()))
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.BlockHomeMissing,
                        Severity.Error,
                        $"Block '{blockId}' homeContext chain does not reach a method",
                        new[] { new Location("metadata.blockContexts", blockId) },
                "HomeContext chain MUST terminate at a method"));
                }
            }
        }

        // Validate outerContext
        if (!string.IsNullOrEmpty(blockInfo.OuterContext))
        {
            if (!ctx.IndexByLocalId.ContainsKey(blockInfo.OuterContext))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.BlockOuterMissing,
                    Severity.Error,
                    $"Block '{blockId}' references non-existent outerContext '{blockInfo.OuterContext}'",
                    new[] { new Location("metadata.blockContexts", blockId) },
                "OuterContext must reference an existing method"));
            }
            else if (!blockInfo.OuterContext.StartsWith("method:"))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.BlockOuterNotMethod,
                    Severity.Error,
                    $"Block '{blockId}' outerContext '{blockInfo.OuterContext}' is not a method",
                    new[] { new Location("metadata.blockContexts", blockId) },
                "OuterContext must reference a method (lexical parent)"));
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

        return false;
    }

    private void DetectCycles(List<Diagnostic> diagnostics, Dictionary<string, BlockContextInfo> blockContexts)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var blockId in blockContexts.Keys)
        {
            if (!visited.Contains(blockId))
            {
                DetectCyclesHelper(blockId, blockContexts, visited, recursionStack, diagnostics);
            }
        }
    }

    private bool DetectCyclesHelper(string blockId, Dictionary<string, BlockContextInfo> blockContexts,
        HashSet<string> visited, HashSet<string> recursionStack, List<Diagnostic> diagnostics)
    {
        visited.Add(blockId);
        recursionStack.Add(blockId);

        if (blockContexts.TryGetValue(blockId, out var blockInfo))
        {
            if (!string.IsNullOrEmpty(blockInfo.HomeContext))
            {
                // If homeContext is a method, no cycle possible
                if (blockInfo.HomeContext.StartsWith("method:"))
                {
                    recursionStack.Remove(blockId);
                    return false;
                }

                if (recursionStack.Contains(blockInfo.HomeContext))
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.BlockCycle,
                        Severity.Error,
                        $"Closure cycle detected: '{blockId}' -> '{blockInfo.HomeContext}' forms a cycle",
                        new[] { new Location("metadata.blockContexts", blockId) },
                "HomeContext chains must not form cycles"));
                    recursionStack.Remove(blockId);
                    return true;
                }

                if (blockContexts.ContainsKey(blockInfo.HomeContext) && !visited.Contains(blockInfo.HomeContext))
                {
                    if (DetectCyclesHelper(blockInfo.HomeContext, blockContexts, visited, recursionStack, diagnostics))
                    {
                        recursionStack.Remove(blockId);
                        return true;
                    }
                }
            }
        }

        recursionStack.Remove(blockId);
        return false;
    }
}
