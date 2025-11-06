using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// VM metadata provided by the cold start loader.
/// Contains field indices and well-known class OOPs that the VM needs to execute code.
/// This allows the VM to operate purely on the Smalltalk heap without depending on
/// cold start data structures or hard-coded indices.
/// </summary>
public class VMMetadata
{
    public int ClassNameFieldIndex { get; init; }
    public int ClassSuperclassFieldIndex { get; init; }
    public int ClassMethodDictFieldIndex { get; init; }
    public int ClassFormatFieldIndex { get; init; }
    
    public int MetaclassThisClassFieldIndex { get; init; }
    
    public int MethodDictTallyFieldIndex { get; init; }
    public int MethodDictKeysFieldIndex { get; init; }
    public int MethodDictValuesFieldIndex { get; init; }
    
    public SmalltalkOOP ClassClassOOP { get; init; }
    public SmalltalkOOP MetaclassClassOOP { get; init; }
    public SmalltalkOOP SymbolClassOOP { get; init; }
    public SmalltalkOOP StringClassOOP { get; init; }
    public SmalltalkOOP ArrayClassOOP { get; init; }
    public SmalltalkOOP CompiledMethodClassOOP { get; init; }
    public SmalltalkOOP SmallIntegerClassOOP { get; init; }
    public SmalltalkOOP BlockContextClassOOP { get; init; }
    public SmalltalkOOP BlockClosureClassOOP { get; init; }
}
