namespace Smalltalk80.Interpreter.Core.BlueBook;

/// <summary>
/// Defines the standard instance variable layout for Class objects per the Blue Book specification.
/// Classes in Smalltalk-80 are objects with a specific structure inherited from Behavior, ClassDescription, and Class.
/// </summary>
public static class ClassLayout
{
    // Instance variables from Behavior
    /// <summary>
    /// Index of the superclass instance variable (SObjectRef to superclass or nil).
    /// </summary>
    public const int Superclass = 0;

    /// <summary>
    /// Index of the method dictionary instance variable (SObjectRef to MethodDictionary).
    /// </summary>
    public const int MethodDictionary = 1;

    /// <summary>
    /// Index of the format instance variable (SmallInteger encoding the object format).
    /// Format encodes: instance variable count, indexability type, pointer/byte/word format.
    /// </summary>
    public const int Format = 2;

    /// <summary>
    /// Index of the subclasses instance variable (SObjectRef to Set of subclasses).
    /// Contains back-pointers to all direct subclasses of this class.
    /// </summary>
    public const int Subclasses = 3;

    // Instance variables from ClassDescription
    /// <summary>
    /// Index of the instance variables instance variable (SObjectRef to Array of Symbols).
    /// Contains the names of instance variables defined in this class (not including inherited).
    /// </summary>
    public const int InstanceVariables = 4;

    /// <summary>
    /// Index of the organization instance variable (SObjectRef to ClassOrganizer).
    /// Contains method categorization information.
    /// </summary>
    public const int Organization = 5;

    // Instance variables from Class
    /// <summary>
    /// Index of the name instance variable (SObjectRef to Symbol).
    /// The class name as a Symbol.
    /// </summary>
    public const int Name = 6;

    /// <summary>
    /// Index of the class pool instance variable (SObjectRef to Dictionary).
    /// Contains class variables (shared by all instances).
    /// </summary>
    public const int ClassPool = 7;

    /// <summary>
    /// Index of the shared pools instance variable (SObjectRef to Array of Dictionaries).
    /// References to shared pool dictionaries.
    /// </summary>
    public const int SharedPools = 8;

    /// <summary>
    /// Total number of instance variables for Class objects.
    /// Behavior(4) + ClassDescription(2) + Class(3) = 9 instance variables.
    /// </summary>
    public const int InstanceVariableCount = 9;
}

/// <summary>
/// Defines the instance variable layout for Metaclass objects per the Blue Book specification.
/// IMPORTANT: Metaclass inherits from ClassDescription, NOT from Class!
/// Metaclasses do NOT have the Class-specific fields (name, classPool, sharedPools).
/// </summary>
public static class MetaclassLayout
{
    // All ClassDescription instance variables (0-5)
    // Plus one additional:

    /// <summary>
    /// Index of the thisClass instance variable (SObjectRef to the class this is the metaclass for).
    /// This links a metaclass back to its class.
    /// IMPORTANT: Index is 6, not 9! Metaclass extends ClassDescription (6 fields), not Class (9 fields).
    /// </summary>
    public const int ThisClass = 6;

    /// <summary>
    /// Total number of instance variables for Metaclass objects.
    /// Behavior(4) + ClassDescription(2) + Metaclass(1) = 7 instance variables.
    /// Metaclass does NOT inherit the Class fields (name, classPool, sharedPools).
    /// </summary>
    public const int InstanceVariableCount = 7;
}
