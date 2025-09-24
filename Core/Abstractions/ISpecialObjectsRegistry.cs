using Core.Memory;

namespace Core.Abstractions;

/// <summary>
/// Registry for special Smalltalk objects that the VM needs direct access to.
/// Based on the Blue Book specification and Smalltalk-80 implementation.
/// </summary>
public interface ISpecialObjectsRegistry
{
    // Core Classes (must be set during bootstrap)
    OOP ObjectClass { get; set; }
    OOP BehaviorClass { get; set; }
    OOP ClassDescriptionClass { get; set; }
    OOP MetaclassClass { get; set; }
    OOP ClassClass { get; set; }
    
    // Method Storage Classes
    OOP MethodDictionaryClass { get; set; }
    OOP CompiledMethodClass { get; set; }
    OOP SymbolClass { get; set; }
    
    // Execution Classes
    OOP MethodContextClass { get; set; }
    OOP BlockContextClass { get; set; }
    OOP ProcessClass { get; set; }
    OOP SemaphoreClass { get; set; }
    
    // Basic Type Classes
    OOP SmallIntegerClass { get; set; }
    OOP LargePositiveIntegerClass { get; set; }
    OOP LargeNegativeIntegerClass { get; set; }
    OOP FloatClass { get; set; }
    OOP CharacterClass { get; set; }
    OOP StringClass { get; set; }
    OOP ByteArrayClass { get; set; }
    OOP ArrayClass { get; set; }
    
    // Collection Classes
    OOP DictionaryClass { get; set; }
    OOP AssociationClass { get; set; }
    OOP OrderedCollectionClass { get; set; }
    OOP SetClass { get; set; }
    
    // Boolean Classes
    OOP TrueClass { get; set; }
    OOP FalseClass { get; set; }
    OOP UndefinedObjectClass { get; set; }
    
    // Special Objects (singletons and well-known instances)
    OOP NilObject { get; }
    OOP TrueObject { get; }
    OOP FalseObject { get; }
    OOP SchedulerAssociation { get; set; }
    OOP CharacterTable { get; set; }
    OOP SymbolTable { get; set; }
    OOP Smalltalk { get; set; } // The global system dictionary
    
    // Message-related
    OOP DoesNotUnderstand { get; set; } // The #doesNotUnderstand: selector
    OOP CannotReturn { get; set; } // The #cannotReturn: selector
    OOP MustBeBoolean { get; set; } // The #mustBeBoolean selector
    
    // Process priorities (SmallInteger OOPs)
    OOP LowIOPriority { get; }
    OOP UserInterruptPriority { get; }
    OOP UserSchedulingPriority { get; }
    OOP SystemBackgroundPriority { get; }
    OOP TimingPriority { get; }
    
    /// <summary>
    /// Gets a special object by its index in the special objects array.
    /// This matches the Blue Book's special objects array indexing.
    /// </summary>
    OOP GetSpecialObject(int index);
    
    /// <summary>
    /// Sets a special object at the given index.
    /// </summary>
    void SetSpecialObject(int index, OOP value);
    
    /// <summary>
    /// Validates that all required special objects are initialized.
    /// </summary>
    bool ValidateRequiredObjects();
    
    /// <summary>
    /// Gets a class by name from the registry.
    /// </summary>
    OOP GetClass(string className);
}

/// <summary>
/// Well-known indices in the special objects array.
/// Based on Smalltalk-80 Blue Book specification.
/// </summary>
public static class SpecialObjectIndices
{
    public const int NilObject = 0;
    public const int FalseObject = 1;
    public const int TrueObject = 2;
    public const int SchedulerAssociation = 3;
    public const int SymbolTable = 4;
    public const int SmallIntegerClass = 5;
    public const int StringClass = 6;
    public const int ArrayClass = 7;
    public const int Smalltalk = 8;
    public const int FloatClass = 9;
    public const int MethodContextClass = 10;
    public const int BlockContextClass = 11;
    public const int PointClass = 12;
    public const int LargePositiveIntegerClass = 13;
    public const int DisplayClass = 14;
    public const int MessageClass = 15;
    public const int CompiledMethodClass = 16;
    public const int SemaphoreClass = 17;
    public const int CharacterClass = 18;
    public const int DoesNotUnderstand = 19;
    public const int CannotReturn = 20;
    public const int ProcessClass = 21;
    public const int ByteArrayClass = 22;
    public const int SpecialObjectsArraySize = 48; // Standard size
}