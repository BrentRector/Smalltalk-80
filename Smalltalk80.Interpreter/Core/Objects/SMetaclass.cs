namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk metaclass - the class of a class.
/// </summary>
/// <remarks>
/// From the Blue Book page 269:
/// Every class has a unique metaclass. The metaclass defines the behavior of the class
/// as an object (class-side methods). The metaclass hierarchy parallels the class hierarchy.
/// 
/// Key principles:
/// 1. Every class is an instance of its own unique metaclass
/// 2. The superclass of a metaclass is the metaclass of the class's superclass
/// 3. The class "Metaclass" is the class of all metaclasses
/// 4. Metaclass class is an instance of Metaclass (circular)
/// </remarks>
public sealed class SMetaclass : SClass
{
    private readonly SClass thisClass;
    private SClass? overrideClass;
    
    /// <summary>
    /// Initializes a new instance of the SMetaclass class.
    /// </summary>
    /// <param name="forClass">The class this metaclass is for.</param>
    /// <param name="superMetaclass">The superclass of this metaclass.</param>
    public SMetaclass(SClass forClass, SMetaclass? superMetaclass = null)
        : base($"{forClass.Name} class", superMetaclass, ["thisClass"])
    {
        thisClass = forClass;
        
        // All metaclasses are instances of Metaclass
        // This will be set properly during system initialization
        overrideClass = SystemDictionary.MetaclassClass ?? this;
    }
    
    /// <summary>
    /// Gets the class of this metaclass (which is Metaclass).
    /// </summary>
    public override SClass Class => overrideClass ?? SystemDictionary.MetaclassClass ?? this;
    
    /// <summary>
    /// Gets the class this metaclass is for.
    /// </summary>
    public SClass ThisClass => thisClass;
    
    /// <summary>
    /// Gets whether this is a metaclass.
    /// </summary>
    public bool IsMetaclass => true;
    
    /// <summary>
    /// Creates a new instance (which would be the class itself).
    /// </summary>
    public override SObject NewInstance()
    {
        // A metaclass's instance is the class itself
        return thisClass;
    }
    
    /// <summary>
    /// Gets the sole instance of this metaclass (the class itself).
    /// </summary>
    public SClass SoleInstance => thisClass;
    
    /// <summary>
    /// Returns a string representation of this metaclass.
    /// </summary>
    public override string ToDisplayString()
    {
        return $"{thisClass.Name} class";
    }
}

/// <summary>
/// Extension methods to properly set up metaclass relationships.
/// </summary>
public static class MetaclassExtensions
{
    /// <summary>
    /// Creates a proper metaclass for a class.
    /// </summary>
    public static SMetaclass CreateMetaclass(this SClass sClass)
    {
        // Determine the superclass's metaclass
        SMetaclass? superMetaclass = null;
        if (sClass.Superclass != null)
        {
            // The metaclass's superclass is the metaclass of the class's superclass
            superMetaclass = sClass.Superclass.Class as SMetaclass;
            if (superMetaclass == null && sClass.Superclass.Class != null)
            {
                // Create metaclass for superclass if it doesn't have one
                superMetaclass = sClass.Superclass.CreateMetaclass();
            }
        }
        
        return new SMetaclass(sClass, superMetaclass);
    }
    
    /// <summary>
    /// Sets up the proper metaclass hierarchy for the system.
    /// </summary>
    public static void InitializeMetaclassHierarchy()
    {
        // This is called during system initialization to set up the metaclass hierarchy
        // according to the Blue Book specification
        
        // 1. Create Metaclass class if it doesn't exist
        var metaclassClass = SystemDictionary.LookupClass("Metaclass");
        if (metaclassClass == null)
        {
            var classClass = SystemDictionary.LookupClass("Class");
            metaclassClass = new SClass("Metaclass", classClass, ["thisClass"]);
            SystemDictionary.RegisterClass(metaclassClass);
        }
        
        // 2. Create Metaclass's metaclass (Metaclass class)
        var metaclassMetaclass = new SMetaclass(metaclassClass);
        
        // 3. Set up the circular relationship: Metaclass class is an instance of Metaclass
        SetClassField(metaclassClass, metaclassMetaclass);
        SetClassField(metaclassMetaclass, metaclassClass);
        
        // 4. Update SystemDictionary
        SystemDictionary.MetaclassClass = metaclassClass;
        
        // 5. Create metaclasses for all existing classes
        foreach (var className in SystemDictionary.GetAllClassNames())
        {
            var sClass = SystemDictionary.LookupClass(className);
            if (sClass != null && sClass.Class == null)
            {
                var metaclass = sClass.CreateMetaclass();
                SetClassField(sClass, metaclass);
            }
        }
    }
    
    /// <summary>
    /// Helper to set the Class field of an SClass.
    /// </summary>
    private static void SetClassField(SClass target, SClass metaclass)
    {
        target.SetMetaclass(metaclass);
    }
}