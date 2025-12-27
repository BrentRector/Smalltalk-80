using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
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
    PatchApplicationResult Apply(PatchDescriptor[]? patches);
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
    void InstallGlobals(SmalltalkOOP ustableOOP);
}

/// <summary>
/// Creates runtime objects (DefaultTextStyle, Display).
/// </summary>
public interface IRuntimeObjectCreator
{
    void CreateDefaultTextStyle();
    void CreateDisplay();
}
