using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Caching;

/// <summary>
/// Manages selector OOP caches for optimized bytecode dispatch.
/// Caches arithmetic (0xB0-0xBF) and special (0xC0-0xCF) selectors.
/// </summary>
public class SelectorCache
{
    private readonly ISmalltalkHeapManager _heapManager;

    /// <summary>Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF).</summary>
    public SmalltalkOOP[]? ArithmeticSelectors { get; private set; }

    /// <summary>Cached selector OOPs for special send bytecodes (0xC0-0xCF).</summary>
    public SmalltalkOOP[]? SpecialSelectors { get; private set; }

    /// <summary>Whether the caches have been initialized.</summary>
    public bool IsInitialized => ArithmeticSelectors != null && SpecialSelectors != null;

    /// <summary>
    /// Creates a new SelectorCache.
    /// </summary>
    public SelectorCache(ISmalltalkHeapManager heapManager)
    {
        _heapManager = heapManager;
    }

    /// <summary>
    /// Initializes the selector caches by looking up selectors in the Smalltalk heap.
    /// Must be called after cold start completes and all symbols are interned.
    /// </summary>
    public void Initialize()
    {
        // Initialize arithmetic selector OOPs (bytecodes 0xB0-0xBF)
        string[] arithmeticSelectorNames =
        [
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
        ];

        ArithmeticSelectors = new SmalltalkOOP[16];
        for (int i = 0; i < arithmeticSelectorNames.Length; i++)
        {
            ArithmeticSelectors[i] = LookupSymbolInHeap(arithmeticSelectorNames[i]);
        }

        // Initialize special selector OOPs (bytecodes 0xC0-0xCF)
        string[] specialSelectorNames =
        [
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        ];

        SpecialSelectors = new SmalltalkOOP[16];
        for (int i = 0; i < specialSelectorNames.Length; i++)
        {
            SpecialSelectors[i] = LookupSymbolInHeap(specialSelectorNames[i]);
        }
    }

    /// <summary>
    /// Looks up a symbol in the Smalltalk heap by searching USTable.
    /// CRITICAL: Uses heap-only access, no C# proxy data.
    /// </summary>
    private SmalltalkOOP LookupSymbolInHeap(string symbolName)
    {
        // Get USTable OOP
        // NOTE: During VM initialization (cold start), GlobalRegistry is valid and contains USTable
        // This is acceptable because VM initialization happens during cold start, not runtime
        if (!GlobalRegistry.TryGet("USTable", out var ustableOOP))
        {
            throw new InvalidOperationException("USTable not found - cannot initialize selector caches. " +
                "This method should only be called during cold start initialization.");
        }

        // USTable is an Array in the Smalltalk heap containing Symbol OOPs
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Convert search string to bytes (Latin-1 encoding)
        byte[] searchBytes = System.Text.Encoding.Latin1.GetBytes(symbolName);

        // Search through USTable (Array-of-Arrays structure for collision chaining)
        for (int i = 0; i < ustableSize; i++)
        {
            var bucketOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil buckets (empty slots)
            if (bucketOOP == nilOOP)
                continue;

            // Bucket is an Array of Symbols - iterate through it
            int bucketSize = _heapManager.GetIndexedSize(bucketOOP);
            for (int k = 0; k < bucketSize; k++)
            {
                var symbolOOP = _heapManager.GetIndexedOOP(bucketOOP, k);

                // Skip nil slots (shouldn't happen, but defensive)
                if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                    continue;

                // Compare Symbol bytes in heap
                int symbolLength = _heapManager.GetIndexedSize(symbolOOP);
                if (symbolLength == searchBytes.Length)
                {
                    bool match = true;
                    for (int j = 0; j < searchBytes.Length; j++)
                    {
                        byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                        if (symbolByte != searchBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return symbolOOP;
                }
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in USTable during selector cache initialization");
    }
}
