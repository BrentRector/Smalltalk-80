using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Resolves FieldValue descriptors to SmalltalkOOP values.
/// Handles all value types: nil, true, false, smallInt, objectRef, stringRef, symbolRef.
/// </summary>
public class FieldValueResolver : IFieldValueResolver
{
    private readonly Phase5Context _context;
    private readonly SymbolService _symbolService;

    public FieldValueResolver(Phase5Context context)
    {
        _context = context;
        _symbolService = new SymbolService(context.Bundle);
    }

    public SmalltalkOOP Resolve(FieldValue? value)
    {
        if (value == null)
            return GlobalRegistry.Get("nil");

        if (value.Type == "objectRef")
        {
            return _context.LocalIdToObjectId[value.LocalId!];
        }

        return value.Type switch
        {
            "nil" => GlobalRegistry.Get("nil"),
            "true" => GlobalRegistry.Get("true"),
            "false" => GlobalRegistry.Get("false"),
            "smallInt" => SmalltalkOOP.FromSmallInteger((int)value.Value!),
            "stringRef" => _context.Allocator.AllocateString(_context.Bundle.Strings[value.Index!.Value]),
            "symbolRef" => _symbolService.GetSymbolOOPByIndex(value.Index!.Value),
            _ => throw new InvalidOperationException($"Unknown field value type: {value.Type}")
        };
    }
}
