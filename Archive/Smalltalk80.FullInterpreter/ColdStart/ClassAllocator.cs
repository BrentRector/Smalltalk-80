namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Allocates Class and Metaclass objects from JSON definitions.
///
/// Why separate allocation and population phases?
/// Classes can reference each other (superclass links, instance var types).
/// By allocating all classes first, we ensure every class OOP exists
/// before we start wiring references between them.
///
/// Blue Book Metaclass Wiring:
/// - Every class has a metaclass (class of the class)
/// - Metaclass instance = the class itself
/// - Metaclass.class = Metaclass (the Metaclass class)
/// - Class.class = Class metaclass
/// </summary>
public class ClassAllocator(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Allocates a class and its metaclass from JSON definition.
    /// Objects are initially empty - fields populated later by ClassPopulator.
    ///
    /// LocalId registration:
    /// - "class:ClassName" → the class object
    /// - "meta:ClassName" → the metaclass object
    ///
    /// Why allocate empty?
    /// Avoids chicken-and-egg problems. For example, Object's superclass is nil,
    /// but Array's superclass is Object - we need Object's OOP to exist first.
    /// </summary>
    public void AllocateClass(string localId)
    {
        // Extract class name from localId (format: "class:ClassName" or "meta:ClassName")
        if (!localId.StartsWith("class:"))
            throw new ArgumentException($"Expected localId to start with 'class:', got: {localId}");

        var className = localId.Substring(6); // Skip "class:" prefix

        // Find JSON class definition
        var jsonClass = FindClassDefinition(className, isMetaclass: false);
        if (jsonClass == null)
            throw new InvalidOperationException($"Class definition not found in JSON: {className}");

        // Allocate the class object (initially empty)
        // Blue Book: Class has 6 instance variables (superclass, methodDict, format, subclasses, instVars, name)
        var classOop = ObjectMemory.AllocateObject(
            GetClassClass(),                    // Class of this object is Class (or a subclass)
            ObjectFormat.FixedPointer,          // Classes are fixed pointer objects
            instanceVarCount: 6,                    // Standard Class instance variables
            indexedSize: 0                      // Classes have no indexed slots
        );

        // Register class localId
        _loader.RegisterLocalId(localId, SObjectRef.FromObjectPointer(classOop));

        // Allocate metaclass for this class
        AllocateMetaclass(className, classOop);
    }

    /// <summary>
    /// Allocates a metaclass for the given class.
    ///
    /// Blue Book Metaclass Semantics:
    /// - Metaclass is the class of a class
    /// - Metaclass has same instance structure as Class
    /// - Metaclass's sole instance is the class itself
    /// - Metaclass's class is Metaclass class
    ///
    /// Why metaclasses?
    /// Allow classes to have class-side methods (e.g., Array new, Point x:y:)
    /// </summary>
    private void AllocateMetaclass(string className, nint classOop)
    {
        // Find metaclass definition in JSON
        var jsonMetaclass = FindClassDefinition(className, isMetaclass: true);
        if (jsonMetaclass == null)
            throw new InvalidOperationException($"Metaclass definition not found in JSON: {className} class");

        // Allocate metaclass object
        // Metaclass has same structure as Class (6 instance variables)
        var metaclassOop = ObjectMemory.AllocateObject(
            GetMetaclassClass(),                // Metaclass's class is Metaclass
            ObjectFormat.FixedPointer,
            instanceVarCount: 6,                    // Same as Class
            indexedSize: 0
        );

        // Register metaclass localId
        var metaclassLocalId = $"meta:{className}";
        _loader.RegisterLocalId(metaclassLocalId, SObjectRef.FromObjectPointer(metaclassOop));

        // Wire bidirectional link: class.class = metaclass, metaclass.thisClass = class
        // This is done here because both OOPs now exist
        // (Full field population happens later in ClassPopulator)
        var classRef = SObjectRef.FromObjectPointer(classOop);
        var metaclassRef = SObjectRef.FromObjectPointer(metaclassOop);

        // Set class's class to metaclass
        ObjectMemory.SetClass(classOop, metaclassRef);

        // Note: metaclass.thisClass set in ClassPopulator (requires format field access)
    }

    /// <summary>
    /// Finds class definition in JSON by name and metaclass flag.
    /// </summary>
    private JsonClass? FindClassDefinition(string className, bool isMetaclass)
    {
        foreach (var jsonClass in _loader.Bundle.Classes)
        {
            var name = _loader.Bundle.Symbols[jsonClass.NameIndex];
            if (name == className && jsonClass.IsMetaclass == isMetaclass)
                return jsonClass;
        }
        return null;
    }

    /// <summary>
    /// Gets the Class class object.
    /// This must be one of the first classes created (bootstrap phase).
    /// </summary>
    private SObjectRef GetClassClass()
    {
        // During initial bootstrap, Class might not exist yet
        // In that case, we need special handling (circular dependency)
        try
        {
            return _loader.ResolveLocalId("class:Class");
        }
        catch
        {
            // If Class doesn't exist yet, we're in bootstrap phase
            // Return nil and fix up later in patches
            return SObjectRef.Nil;
        }
    }

    /// <summary>
    /// Gets the Metaclass class object.
    /// </summary>
    private SObjectRef GetMetaclassClass()
    {
        try
        {
            return _loader.ResolveLocalId("class:Metaclass");
        }
        catch
        {
            // Bootstrap: Metaclass might not exist yet
            return SObjectRef.Nil;
        }
    }
}
