using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Creates and populates the Universal Symbol Table (USTable).
/// USTable is an Array of Arrays providing collision chaining for symbol lookup.
/// Uses two-pass approach to avoid creating temporary arrays.
/// </summary>
public class USTableBuilder(Phase5Context context) : IUSTableBuilder
{
    public SmalltalkOOP Build()
    {
        var symbolObjects = SymbolRegistry.AllSymbols().ToList();
        int ustableSize = ComputeUSTableSize(symbolObjects.Count);

        // Pass 1: Count symbols per bucket
        var bucketCounts = new int[ustableSize];
        foreach (var (index, _) in symbolObjects)
        {
            string symbolName = context.Bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            int bucketIndex = hash % ustableSize;
            bucketCounts[bucketIndex]++;
        }

        // Create USTable with pre-sized buckets
        SmalltalkOOP ustableOOP = CreateUSTableWithBuckets(ustableSize, bucketCounts);

        // Pass 2: Insert symbols into buckets
        var bucketPositions = new int[ustableSize];
        foreach (var (index, symbolOOP) in symbolObjects)
        {
            string symbolName = context.Bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            int bucketIndex = hash % ustableSize;

            var bucketOOP = context.Allocator.GetIndexedOOP(ustableOOP, bucketIndex);
            int position = bucketPositions[bucketIndex]++;

            context.Allocator.SetIndexedOOP(bucketOOP, position, symbolOOP);
        }

        GlobalRegistry.Register("USTable", ustableOOP);
        context.Report.Metrics.USTableCapacity = ustableSize;

        return ustableOOP;
    }

    private static int ComputeUSTableSize(int count) => count * 2; // Load factor heuristic

    /// <summary>
    /// Creates USTable as Array of Arrays with pre-allocated bucket sizes.
    /// </summary>
    private SmalltalkOOP CreateUSTableWithBuckets(int size, int[] bucketCounts)
    {
        var ustableOOP = context.Allocator.AllocateArray("Array", size);

        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        context.Allocator.SetClassOOP(ustableOOP, arrayClassOOP);

        var nilOOP = GlobalRegistry.Get("nil");

        for (int i = 0; i < size; i++)
        {
            // Allocate bucket (even empty buckets must be Arrays, not nil)
            var bucketOOP = context.Allocator.AllocateArray("Array", bucketCounts[i]);
            context.Allocator.SetClassOOP(bucketOOP, arrayClassOOP);

            // Initialize indexed slots to nil
            for (int j = 0; j < bucketCounts[i]; j++)
            {
                context.Allocator.SetIndexedOOP(bucketOOP, j, nilOOP);
            }

            context.Allocator.SetIndexedOOP(ustableOOP, i, bucketOOP);
        }

        return ustableOOP;
    }
}
