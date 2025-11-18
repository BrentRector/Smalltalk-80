using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Resolves FieldValue descriptors to SmalltalkOOP values.
/// Handles all value types: nil, true, false, smallInt, objectRef, stringRef, symbolRef.
/// </summary>
public class FieldValueResolver(Phase5Context context) : IFieldValueResolver
{
    public SmalltalkOOP Resolve(FieldValue? value)
    {
        if (value == null)
            return SpecialObjectsArray.Instance.Nil;

        if (value.Type == "objectRef")
        {
            return context.LocalIdToObjectId[value.LocalId!];
        }

        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger((int)value.Value!),
            "stringRef" => context.Allocator.AllocateString(context.Bundle.Strings[value.Index!.Value]),
            "symbolRef" => SymbolRegistry.GetSymbolOOP(value.Index!.Value),
            _ => throw new InvalidOperationException($"Unknown field value type: {value.Type}")
        };
    }
}
