using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Manages Association creation and maintains canonical identity through AssociationRegistry.
/// Ensures only one Association per global variable name exists.
/// </summary>
public class AssociationManager : IAssociationManager
{
    private readonly Phase5Context _context;
    private readonly SymbolService _symbolService;
    private readonly DictionaryService _dictionaryService;

    public AssociationManager(Phase5Context context, SymbolService symbolService, DictionaryService dictionaryService)
    {
        _context = context;
        _symbolService = symbolService;
        _dictionaryService = dictionaryService;
    }

    /// <summary>
    /// Gets or creates a canonical Association for the given key name.
    /// Uses AssociationRegistry to ensure identity.
    /// </summary>
    public SmalltalkOOP GetOrCreateCanonical(string keyName)
    {
        SmalltalkOOP keySymbolOOP = _symbolService.InternSymbol(keyName);

        return AssociationRegistry.GetOrCreate(keyName, () =>
        {
            return _dictionaryService.AllocateAssociation(keySymbolOOP, SpecialObjectsArray.Instance.Nil);
        });
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    public SmalltalkOOP Allocate(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        return _dictionaryService.AllocateAssociation(keyOOP, valueOOP);
    }

    /// <summary>
    /// Gets the symbol OOP for a given symbol name from the bundle.
    /// </summary>
    public SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        return _symbolService.InternSymbol(symbolName);
    }
}
