namespace Smalltalk80.Interpreter.ObjectModel;
/// <summary>
/// Well-known object indices used throughout the system.
/// These are the indices into the Special Objects Array maintained by the VM.
/// </summary>
public static class SpecialObjects
{
    /// <summary>
    /// Index of nil object in Special Objects Array.
    /// </summary>
    public const int NilObject = 0;

    /// <summary>
    /// Index of false object in Special Objects Array.
    /// </summary>
    public const int FalseObject = 1;

    /// <summary>
    /// Index of true object in Special Objects Array.
    /// </summary>
    public const int TrueObject = 2;

    /// <summary>
    /// Index of Behavior class in Special Objects Array.
    /// </summary>
    public const int BehaviorClass = 3;

    /// <summary>
    /// Index of Metaclass class in Special Objects Array.
    /// </summary>
    public const int MetaclassClass = 4;

    /// <summary>
    /// Index of SystemDictionary instance (Smalltalk global) in Special Objects Array.
    /// </summary>
    public const int SystemDictionaryInstance = 5;

    /// <summary>
    /// Index of Symbol table in Special Objects Array.
    /// </summary>
    public const int SymbolTable = 6;

    /// <summary>
    /// Index of Process list in Special Objects Array (future support).
    /// </summary>
    public const int ProcessList = 7;

    /// <summary>
    /// Index of ProcessorScheduler in Special Objects Array (future support).
    /// </summary>
    public const int ProcessorScheduler = 8;

    /// <summary>
    /// Total number of special objects.
    /// Per user requirement: exactly 9 entries, no more.
    /// </summary>
    public const int Count = 9;

    /// <summary>
    /// Gets the name of a special object by index.
    /// </summary>
    public static string GetName(int index) => index switch
    {
        NilObject => "nil",
        FalseObject => "false",
        TrueObject => "true",
        BehaviorClass => "Behavior",
        MetaclassClass => "Metaclass",
        SystemDictionaryInstance => "Smalltalk",
        SymbolTable => "SymbolTable",
        ProcessList => "ProcessList",
        ProcessorScheduler => "ProcessorScheduler",
        _ => $"Unknown({index})"
    };
}