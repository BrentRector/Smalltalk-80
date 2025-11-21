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
internal class MessageDispatcher(
    ISmalltalkHeap heapManager,
    IObjectMetadataAccessor helpers,
    VMMetadata metadata) : IMessageDispatcher
{
    /// <inheritdoc />
    public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, string selectorName)
    {
        var currentClassOOP = classOOP;
        int depth = 0;
        const int maxDepth = 20;

        while (currentClassOOP != metadata.KnownObjects.Nil && depth < maxDepth)
        {
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
            var valuesArrayOOP = heapManager.GetInstanceVariable(methodDictOOP, 0);
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
                    $"MethodDictionary slot[0] (valuesArray) contains {valuesArrayFormat} object (class={valuesArrayClass}) instead of PointerIndexed Array");
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
