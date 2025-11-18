using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Handles method lookup in the Smalltalk-80 class hierarchy.
/// Walks the class hierarchy to find methods and extracts primitive numbers.
/// </summary>
internal class MessageDispatcher : IMessageDispatcher
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly IVMHelpers _helpers;

    public MessageDispatcher(
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        IVMHelpers helpers)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _helpers = helpers;
    }

    public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, string selectorName)
    {
        var currentClassOOP = classOOP;
        int depth = 0;
        const int maxDepth = 20;

        while (currentClassOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            string currentClassName = _helpers.GetClassName(currentClassOOP);

            // Get the method dictionary from the class
            var methodDictOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX);

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                // No method dictionary, try superclass
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                if (superclassOOP == SpecialObjectsArray.Instance.Nil)
                    break;
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // Get values array from method dictionary
            var valuesArrayOOP = _heapManager.GetInstanceVariable(methodDictOOP, 0);
            if (valuesArrayOOP == SpecialObjectsArray.Instance.Nil)
            {
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // Validate format
            var methodDictFormat = _heapManager.GetObjectFormat(methodDictOOP);
            if (methodDictFormat != ObjectFormat.PointerIndexed)
            {
                var methodDictClass = _helpers.GetClassName(_helpers.GetClass(methodDictOOP));
                throw new InvalidOperationException(
                    $"Class methodDict field contains {methodDictFormat} object (class={methodDictClass}) instead of PointerIndexed MethodDictionary");
            }

            var valuesArrayFormat = _heapManager.GetObjectFormat(valuesArrayOOP);
            if (valuesArrayFormat != ObjectFormat.PointerIndexed)
            {
                var valuesArrayClass = _helpers.GetClassName(_helpers.GetClass(valuesArrayOOP));
                throw new InvalidOperationException(
                    $"MethodDictionary slot[0] (valuesArray) contains {valuesArrayFormat} object (class={valuesArrayClass}) instead of PointerIndexed Array");
            }

            // Search for selector in method dictionary
            int dictSize = _heapManager.GetIndexedSize(methodDictOOP);
            for (int i = 0; i < dictSize; i++)
            {
                var selectorOOP = _heapManager.GetIndexedOOP(methodDictOOP, i);
                if (selectorOOP == SpecialObjectsArray.Instance.Nil)
                    continue;

                string? currentSelectorName = GetSymbolName(selectorOOP);
                if (currentSelectorName == selectorName)
                {
                    var methodOOP = _heapManager.GetIndexedOOP(valuesArrayOOP, i);
                    return new MethodLookupResult { MethodOOP = methodOOP };
                }
            }

            // Not found, try superclass
            var nextSuperclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        return null;
    }

    public int GetPrimitiveNumber(SmalltalkOOP methodOOP)
    {
        var headerInfo = _helpers.ParseMethodHeader(methodOOP);
        return headerInfo.Primitive;
    }

    private string? GetSymbolName(SmalltalkOOP symbolOOP)
    {
        if (symbolOOP == SpecialObjectsArray.Instance.Nil)
            return null;

        int length = _heapManager.GetIndexedSize(symbolOOP);
        if (length == 0)
            return string.Empty;

        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}
