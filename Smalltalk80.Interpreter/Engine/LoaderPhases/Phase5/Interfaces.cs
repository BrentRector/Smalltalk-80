using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Resolves FieldValue descriptors to SmalltalkOOP values.
/// </summary>
public interface IFieldValueResolver
{
    SmalltalkOOP Resolve(FieldValue? value);
}

/// <summary>
/// Hash-based dictionary operations matching Smalltalk-80 semantics.
/// </summary>
public interface IDictionaryOperations
{
    void InstallAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string keyName);
    void RemoveAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbolOOP, string keyName);
    int GetSymbolHash(SmalltalkOOP symbolOOP);
    void FixCollisionsFrom(SmalltalkOOP dictionaryOOP, int startIndex);
}

/// <summary>
/// Manages Association creation and canonical identity.
/// </summary>
public interface IAssociationManager
{
    SmalltalkOOP GetOrCreateCanonical(string keyName);
    SmalltalkOOP Allocate(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP);
    SmalltalkOOP GetSymbolOOP(string symbolName);
}

/// <summary>
/// Applies post-allocation patches to fix circular references.
/// </summary>
public interface IPatchApplier
{
    PatchApplicationResult Apply(PatchDescriptor[] patches);
}

/// <summary>
/// Creates and populates the Universal Symbol Table.
/// </summary>
public interface IUSTableBuilder
{
    SmalltalkOOP Build();
}

/// <summary>
/// Installs global variables in SystemDictionary.
/// </summary>
public interface ISystemDictionaryInstaller
{
    void InstallGlobals();
}

/// <summary>
/// Creates runtime objects (DefaultTextStyle, Display).
/// </summary>
public interface IRuntimeObjectCreator
{
    void CreateDefaultTextStyle();
    void CreateDisplay();
}
