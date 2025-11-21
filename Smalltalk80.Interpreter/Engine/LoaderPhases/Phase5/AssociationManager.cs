using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Manages Association creation and maintains canonical identity through AssociationRegistry.
/// Ensures only one Association per global variable name exists.
/// </summary>
public class AssociationManager(Phase5Context context) : IAssociationManager
{
    /// <summary>
    /// Gets or creates a canonical Association for the given key name.
    /// Uses AssociationRegistry to ensure identity.
    /// </summary>
    public SmalltalkOOP GetOrCreateCanonical(string keyName)
    {
        SmalltalkOOP keySymbolOOP = GetSymbolOOP(keyName);

        return AssociationRegistry.GetOrCreate(keyName, () =>
        {
            return Allocate(keySymbolOOP, GlobalRegistry.Get("nil"));
        });
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    public SmalltalkOOP Allocate(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = context.Allocator.AllocateFixedObject("Association");

        // Set Association class reference
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        context.Allocator.SetClassOOP(associationOOP, associationClassOOP);

        // Set instance variables: [0] = key, [1] = value
        context.Allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        context.Allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Gets the symbol OOP for a given symbol name from the bundle.
    /// </summary>
    public SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        for (int i = 0; i < context.Bundle.Symbols.Length; i++)
        {
            if (context.Bundle.Symbols[i] == symbolName)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table");
    }
}
