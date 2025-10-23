using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Primitives;
using System.Text;

namespace Smalltalk80.Interpreter.Engine.ColdStart;

/// <summary>
/// Phase 0X: Block Ownership Validation
///
/// Validates that every block literal in the system:
/// 1. Has a valid ownerLocalId in the blockOwnerMap
/// 2. Can be traced back to its defining method
/// 3. Has a CompiledMethod that can be located in the heap
///
/// This phase should run after Phase 8 (Method Installation) when all methods
/// and block ownership relationships are established.
/// </summary>
public class Phase0X_BlockOwnershipValidation
{
    private readonly SmalltalkHeapManager _heapManager;
    private readonly GlobalRegistry _globalRegistry;
    private readonly Dictionary<string, string> _blockOwnerMap;
    private readonly StringBuilder _errors;
    private readonly StringBuilder _warnings;

    public Phase0X_BlockOwnershipValidation(
        SmalltalkHeapManager heapManager,
        GlobalRegistry globalRegistry)
    {
        _heapManager = heapManager;
        _globalRegistry = globalRegistry;
        _blockOwnerMap = globalRegistry.GetObject<Dictionary<string, string>>("blockOwnerMap")
            ?? throw new InvalidOperationException("blockOwnerMap not found in GlobalRegistry");
        _errors = new StringBuilder();
        _warnings = new StringBuilder();
    }

    /// <summary>
    /// Execute the validation phase.
    /// Returns true if validation passed (no errors, warnings are acceptable).
    /// </summary>
    public bool Execute()
    {
        Console.WriteLine("Phase 0X: Block Ownership Validation...");

        int totalBlocks = 0;
        int validBlocks = 0;
        int orphanedBlocks = 0;
        int missingOwners = 0;

        // Iterate through all entries in blockOwnerMap
        foreach (var kvp in _blockOwnerMap)
        {
            string blockLocalId = kvp.Key;
            string ownerLocalId = kvp.Value;
            totalBlocks++;

            // Validate that the block exists in the heap
            if (!_globalRegistry.TryGet(blockLocalId, out SmalltalkOOP blockOOP))
            {
                _errors.AppendLine($"  ERROR: Block {blockLocalId} in blockOwnerMap not found in heap");
                orphanedBlocks++;
                continue;
            }

            // Validate that the owner method exists
            if (!_globalRegistry.TryGet(ownerLocalId, out SmalltalkOOP ownerOOP))
            {
                _errors.AppendLine($"  ERROR: Owner {ownerLocalId} for block {blockLocalId} not found in heap");
                missingOwners++;
                continue;
            }

            // Validate that the block is a CompiledMethod
            var blockClass = _heapManager.GetClass(blockOOP);
            var blockClassName = GetClassName(blockClass);
            if (blockClassName != "CompiledMethod")
            {
                _warnings.AppendLine($"  WARNING: Block {blockLocalId} has unexpected class {blockClassName}");
            }

            // Validate that the owner is a CompiledMethod
            var ownerClass = _heapManager.GetClass(ownerOOP);
            var ownerClassName = GetClassName(ownerClass);
            if (ownerClassName != "CompiledMethod")
            {
                _warnings.AppendLine($"  WARNING: Owner {ownerLocalId} has unexpected class {ownerClassName}");
            }

            // Validate ownership chain (for nested blocks)
            string currentId = blockLocalId;
            int chainDepth = 0;
            int maxDepth = 100; // Prevent infinite loops

            while (_blockOwnerMap.TryGetValue(currentId, out string? parentId))
            {
                chainDepth++;
                if (chainDepth > maxDepth)
                {
                    _errors.AppendLine($"  ERROR: Circular ownership chain detected for block {blockLocalId}");
                    break;
                }

                currentId = parentId;

                // Ensure parent exists
                if (!_globalRegistry.TryGet(parentId, out SmalltalkOOP _))
                {
                    _errors.AppendLine($"  ERROR: Parent {parentId} in ownership chain for {blockLocalId} not found");
                    break;
                }
            }

            // The final parent should be a method (not a block)
            if (chainDepth > 0 && currentId.StartsWith("block:"))
            {
                _warnings.AppendLine($"  WARNING: Ownership chain for {blockLocalId} terminates at block {currentId} instead of method");
            }

            validBlocks++;
        }

        // Report results
        Console.WriteLine($"  Total blocks validated: {totalBlocks}");
        Console.WriteLine($"  Valid blocks: {validBlocks}");

        if (orphanedBlocks > 0)
            Console.WriteLine($"  Orphaned blocks (in map but not in heap): {orphanedBlocks}");

        if (missingOwners > 0)
            Console.WriteLine($"  Missing owners: {missingOwners}");

        // Print warnings
        if (_warnings.Length > 0)
        {
            Console.WriteLine("\n  Warnings:");
            Console.WriteLine(_warnings.ToString());
        }

        // Print errors
        if (_errors.Length > 0)
        {
            Console.WriteLine("\n  ERRORS:");
            Console.WriteLine(_errors.ToString());
            Console.WriteLine("✗ Block ownership validation FAILED");
            return false;
        }

        Console.WriteLine("✓ Block ownership validation PASSED");
        return true;
    }

    /// <summary>
    /// Validates that all block literals in a CompiledMethod are properly registered.
    /// </summary>
    public void ValidateMethodLiterals(string methodLocalId, SmalltalkOOP methodOOP)
    {
        // Get the method's literals
        var literals = GetMethodLiterals(methodOOP);

        foreach (var literalOOP in literals)
        {
            // Check if this literal is a block (CompiledMethod)
            if (IsBlockLiteral(literalOOP))
            {
                string? blockLocalId = GetLocalId(literalOOP);
                if (blockLocalId == null)
                {
                    _warnings.AppendLine($"  WARNING: Block literal in {methodLocalId} has no local ID");
                    continue;
                }

                // Verify it's in the blockOwnerMap
                if (!_blockOwnerMap.ContainsKey(blockLocalId))
                {
                    _errors.AppendLine($"  ERROR: Block {blockLocalId} in method {methodLocalId} not in blockOwnerMap");
                }
                else if (_blockOwnerMap[blockLocalId] != methodLocalId)
                {
                    _warnings.AppendLine($"  WARNING: Block {blockLocalId} in method {methodLocalId} mapped to {_blockOwnerMap[blockLocalId]}");
                }
            }
        }
    }

    private bool IsBlockLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return false;

        try
        {
            var classOOP = _heapManager.GetClass(oop);
            var className = GetClassName(classOOP);

            // Blocks are CompiledMethods with specific characteristics
            // For now, we rely on the blockOwnerMap to identify blocks
            string? localId = GetLocalId(oop);
            return localId != null && localId.StartsWith("block:");
        }
        catch
        {
            return false;
        }
    }

    private SmalltalkOOP[] GetMethodLiterals(SmalltalkOOP methodOOP)
    {
        // CompiledMethod literals are in the indexed portion
        var entry = _heapManager.GetObjectTableEntry(methodOOP);
        int indexedSize = _heapManager.GetIndexedSizeFromEntry(entry);

        var literals = new SmalltalkOOP[indexedSize];
        for (int i = 0; i < indexedSize; i++)
        {
            literals[i] = _heapManager.GetIndexedOOP(methodOOP, i);
        }

        return literals;
    }

    private string? GetLocalId(SmalltalkOOP oop)
    {
        // Try to find the local ID in the GlobalRegistry
        foreach (var kvp in _globalRegistry.GetAllEntries())
        {
            if (kvp.Value.Equals(oop))
            {
                return kvp.Key;
            }
        }
        return null;
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        try
        {
            // Try to get the class name from GlobalRegistry
            foreach (var kvp in _globalRegistry.GetAllEntries())
            {
                if (kvp.Value.Equals(classOOP) && kvp.Key.StartsWith("class:"))
                {
                    return kvp.Key.Substring(6); // Remove "class:" prefix
                }
            }
            return "UnknownClass";
        }
        catch
        {
            return "UnknownClass";
        }
    }

    /// <summary>
    /// Generates a detailed report of block ownership relationships.
    /// Useful for debugging and understanding the closure structure.
    /// </summary>
    public string GenerateOwnershipReport()
    {
        var report = new StringBuilder();
        report.AppendLine("=== Block Ownership Report ===");
        report.AppendLine();

        // Group blocks by owner
        var blocksByOwner = new Dictionary<string, List<string>>();
        foreach (var kvp in _blockOwnerMap)
        {
            string blockId = kvp.Key;
            string ownerId = kvp.Value;

            if (!blocksByOwner.ContainsKey(ownerId))
            {
                blocksByOwner[ownerId] = new List<string>();
            }
            blocksByOwner[ownerId].Add(blockId);
        }

        // Sort owners and report
        foreach (var owner in blocksByOwner.Keys.OrderBy(k => k))
        {
            report.AppendLine($"Owner: {owner}");
            foreach (var block in blocksByOwner[owner].OrderBy(b => b))
            {
                report.AppendLine($"  └─ {block}");

                // Show nested blocks
                if (blocksByOwner.ContainsKey(block))
                {
                    foreach (var nestedBlock in blocksByOwner[block])
                    {
                        report.AppendLine($"     └─ {nestedBlock}");
                    }
                }
            }
            report.AppendLine();
        }

        return report.ToString();
    }
}
