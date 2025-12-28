using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Handles method lookup in the Smalltalk-80 class hierarchy.
/// Walks the class hierarchy to find methods and extracts primitive numbers.
/// </summary>
/// <param name="heapManager">The heap manager for memory operations.</param>
/// <param name="helpers">VM helper methods.</param>
/// <param name="metadata">VM metadata for known objects.</param>
/// <param name="dictionaryService">Service for hash-based dictionary lookups.</param>
internal class MessageDispatcher(
    ISmalltalkHeap heapManager,
    IObjectMetadataAccessor helpers,
    VMMetadata metadata,
    RuntimeDictionaryService dictionaryService) : IMessageDispatcher
{
    /// <summary>
    /// Maximum inheritance chain depth for method lookup.
    /// Prevents infinite loops in circular class hierarchies.
    /// </summary>
    private const int MaxInheritanceDepth = 20;

    /// <summary>
    /// Cache for method lookups: (classOOP, selectorOOP) → methodOOP.
    /// Avoids repeated class hierarchy walks for the same message send.
    /// </summary>
    private readonly Dictionary<(SmalltalkOOP, SmalltalkOOP), SmalltalkOOP> _methodCache = new();

    /// <inheritdoc />
    public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, string selectorName)
    {
        var currentClassOOP = classOOP;
        int depth = 0;

        while (currentClassOOP != metadata.KnownObjects.Nil && depth < MaxInheritanceDepth)
        {
            // Validate the class OOP before trying to get its name
            if (!heapManager.IsValidOOP(currentClassOOP))
            {
                throw new InvalidOperationException(
                    $"Invalid class OOP {currentClassOOP.RawValue} encountered at depth {depth} during lookup of '{selectorName}' " +
                    $"(starting from class OOP {classOOP.RawValue})");
            }

            string currentClassName = helpers.GetClassName(currentClassOOP);

            // Get the method dictionary from the class
            var methodDictOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX);

            if (methodDictOOP == metadata.KnownObjects.Nil)
            {
                // No method dictionary, try superclass
                var superclassOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                if (superclassOOP == metadata.KnownObjects.Nil)
                {
                    break;
                }

                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // Get values array from method dictionary
            var valuesArrayOOP = heapManager.GetInstanceVariable(methodDictOOP, VMConstants.METHOD_DICTIONARY_VALUE_ARRAY_INDEX);
            if (valuesArrayOOP == metadata.KnownObjects.Nil)
            {
                var superclassOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // Validate format
            var methodDictFormat = heapManager.GetObjectFormat(methodDictOOP);
            if (methodDictFormat != ObjectFormat.PointerIndexed)
            {
                var methodDictClass = helpers.GetClassName(helpers.GetClass(methodDictOOP));
                throw new InvalidOperationException(
                    $"Class methodDict field contains {methodDictFormat} object (class={methodDictClass}) instead of PointerIndexed MethodDictionary");
            }

            var valuesArrayFormat = heapManager.GetObjectFormat(valuesArrayOOP);
            if (valuesArrayFormat != ObjectFormat.PointerIndexed)
            {
                var valuesArrayClass = helpers.GetClassName(helpers.GetClass(valuesArrayOOP));
                throw new InvalidOperationException(
                    $"MethodDictionary slot[1] (valuesArray) contains {valuesArrayFormat} object (class={valuesArrayClass}) instead of PointerIndexed Array");
            }

            // Search for selector in method dictionary
            int dictSize = heapManager.GetIndexedSize(methodDictOOP);
            for (int i = 0; i < dictSize; i++)
            {
                var selectorOOP = heapManager.GetIndexedOOP(methodDictOOP, i);
                if (selectorOOP == metadata.KnownObjects.Nil)
                {
                    continue;
                }

                string? currentSelectorName = GetSymbolName(selectorOOP);
                if (currentSelectorName == selectorName)
                {
                    var methodOOP = heapManager.GetIndexedOOP(valuesArrayOOP, i);
                    return new MethodLookupResult { MethodOOP = methodOOP };
                }
            }

            // Not found, try superclass
            var nextSuperclassOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

            // Validate superclass OOP before following it
            if (nextSuperclassOOP != metadata.KnownObjects.Nil && !heapManager.IsValidOOP(nextSuperclassOOP))
            {
                throw new InvalidOperationException(
                    $"Invalid superclass OOP {nextSuperclassOOP.RawValue} found in class {currentClassName} (OOP {currentClassOOP.RawValue}) " +
                    $"at depth {depth} during lookup of '{selectorName}'");
            }

            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        return null;
    }

    /// <inheritdoc />
    public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, SmalltalkOOP selectorOOP)
    {
        // Check cache first
        var cacheKey = (classOOP, selectorOOP);
        if (_methodCache.TryGetValue(cacheKey, out var cachedMethodOOP))
        {
            return new MethodLookupResult { MethodOOP = cachedMethodOOP };
        }

        // Cache miss - perform full hierarchy walk
        // LookupMethodUncached caches for ALL visited classes, not just the starting class
        return LookupMethodUncached(classOOP, selectorOOP);
    }

    /// <summary>
    /// Performs uncached method lookup by walking the class hierarchy.
    /// Checks cache at each step and caches results for all visited classes.
    /// </summary>
    private MethodLookupResult? LookupMethodUncached(SmalltalkOOP classOOP, SmalltalkOOP selectorOOP)
    {
        var currentClassOOP = classOOP;
        int depth = 0;

        // Track all classes visited during the walk for cache population
        Span<SmalltalkOOP> visitedClasses = stackalloc SmalltalkOOP[MaxInheritanceDepth];
        int visitedCount = 0;

        while (currentClassOOP != metadata.KnownObjects.Nil && depth < MaxInheritanceDepth)
        {
            // Validate the class OOP before trying to access it
            if (!heapManager.IsValidOOP(currentClassOOP))
            {
                string? selectorName = GetSymbolName(selectorOOP);
                throw new InvalidOperationException(
                    $"Invalid class OOP {currentClassOOP.RawValue} encountered at depth {depth} during lookup of '{selectorName}' " +
                    $"(starting from class OOP {classOOP.RawValue})");
            }

            // Check cache at each step AFTER the first (starting class was already checked in LookupMethod)
            // This catches sibling class scenarios where a shared ancestor was cached
            if (depth > 0 && _methodCache.TryGetValue((currentClassOOP, selectorOOP), out var cachedMethodOOP))
            {
                // Cache hit! Also cache for all classes we visited getting here
                for (int i = 0; i < visitedCount; i++)
                {
                    _methodCache[(visitedClasses[i], selectorOOP)] = cachedMethodOOP;
                }
                return new MethodLookupResult { MethodOOP = cachedMethodOOP };
            }

            // Record this class for cache population
            visitedClasses[visitedCount++] = currentClassOOP;

            // Get the method dictionary from the class
            var methodDictOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX);

            if (methodDictOOP == metadata.KnownObjects.Nil)
            {
                // No method dictionary, try superclass
                var superclassOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                if (superclassOOP == metadata.KnownObjects.Nil)
                {
                    break;
                }

                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // Hash-based lookup in method dictionary (MethodDictionary is an IdentityDictionary)
            var methodOOP = dictionaryService.LookupInIdentityDictionary(methodDictOOP, selectorOOP);
            if (methodOOP != metadata.KnownObjects.Nil)
            {
                // Found! Cache for ALL visited classes, not just the starting class
                for (int i = 0; i < visitedCount; i++)
                {
                    _methodCache[(visitedClasses[i], selectorOOP)] = methodOOP;
                }
                return new MethodLookupResult { MethodOOP = methodOOP };
            }

            // Not found, try superclass
            var nextSuperclassOOP = heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

            // Validate superclass OOP before following it
            if (nextSuperclassOOP != metadata.KnownObjects.Nil && !heapManager.IsValidOOP(nextSuperclassOOP))
            {
                string currentClassName = helpers.GetClassName(currentClassOOP);
                string? selectorName = GetSymbolName(selectorOOP);
                throw new InvalidOperationException(
                    $"Invalid superclass OOP {nextSuperclassOOP.RawValue} found in class {currentClassName} (OOP {currentClassOOP.RawValue}) " +
                    $"at depth {depth} during lookup of '{selectorName}'");
            }

            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        return null;
    }

    /// <inheritdoc />
    public int GetPrimitiveNumber(SmalltalkOOP methodOOP)
    {
        var headerInfo = helpers.ParseMethodHeader(methodOOP);
        return headerInfo.Primitive;
    }

    /// <summary>
    /// Gets the string name from a Symbol OOP.
    /// </summary>
    /// <param name="symbolOOP">The Symbol OOP to get the name from.</param>
    /// <returns>The symbol name as a string, or null if the symbol is nil.</returns>
    private string? GetSymbolName(SmalltalkOOP symbolOOP)
    {
        if (symbolOOP == metadata.KnownObjects.Nil)
        {
            return null;
        }

        int length = heapManager.GetIndexedSize(symbolOOP);
        if (length == 0)
        {
            return string.Empty;
        }

        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}
