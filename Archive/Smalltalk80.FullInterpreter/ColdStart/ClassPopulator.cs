namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Populates Class and Metaclass object fields after allocation.
///
/// Why separate from allocation?
/// Classes reference each other (superclass links). By allocating all classes first,
/// every class OOP exists when we start setting fields.
///
/// Blue Book Class Structure (6 instance variables):
/// 0. superclass - pointer to superclass (or nil for Object)
/// 1. methodDict - MethodDictionary with this class's methods
/// 2. format - instance specification (SmallInteger encoding instance var count and format)
/// 3. subclasses - Array of subclasses (or nil)
/// 4. instanceVariables - Array of instance variable names (Symbols)
/// 5. name - Symbol naming this class
/// </summary>
public class ClassPopulator(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Populates all fields of a class and its metaclass from JSON definition.
    ///
    /// Fields set:
    /// - superclass (class link)
    /// - methodDict (created empty, methods added later by MethodInstaller)
    /// - format (instance specification word)
    /// - name (Symbol reference)
    /// - instanceVariables (Array of Symbols)
    /// </summary>
    public void PopulateClass(string localId)
    {
        // Extract class name from localId
        if (!localId.StartsWith("class:"))
            throw new ArgumentException($"Expected localId to start with 'class:', got: {localId}");

        var className = localId.Substring(6);

        // Resolve class OOP
        var classRef = _loader.ResolveLocalId(localId);
        var classOop = classRef.AsObjectPointer();

        // Find JSON definition
        var jsonClass = FindClassDefinition(className, isMetaclass: false);
        if (jsonClass == null)
            throw new InvalidOperationException($"Class definition not found: {className}");

        // Populate class fields
        PopulateClassFields(classOop, jsonClass, className);

        // Populate metaclass fields
        var metaclassLocalId = $"meta:{className}";
        var metaclassRef = _loader.ResolveLocalId(metaclassLocalId);
        var metaclassOop = metaclassRef.AsObjectPointer();

        var jsonMetaclass = FindClassDefinition(className, isMetaclass: true);
        if (jsonMetaclass == null)
            throw new InvalidOperationException($"Metaclass definition not found: {className} class");

        PopulateClassFields(metaclassOop, jsonMetaclass, $"{className} class");
    }

    /// <summary>
    /// Populates fields for a single class object (works for both classes and metaclasses).
    /// </summary>
    private void PopulateClassFields(nint classOop, JsonClass jsonClass, string className)
    {
        // Field 0: superclass
        SetSuperclass(classOop, jsonClass);

        // Field 1: methodDict (create empty, methods installed later)
        SetMethodDictionary(classOop, jsonClass);

        // Field 2: format (instance specification)
        SetFormat(classOop, jsonClass);

        // Field 3: subclasses (initially nil, could be populated later)
        // Blue Book: This field can be nil or an Array
        // We'll leave it nil for now - not critical for bootstrap
        var subclassesIndex = LayoutManagement.Layout.Class_Subclasses;
        ObjectMemory.SetInstanceVariable(classOop, subclassesIndex, SObjectRef.Nil);

        // Field 4: instanceVariables (Array of Symbols)
        SetInstanceVariables(classOop, jsonClass);

        // Field 5: name (Symbol)
        SetName(classOop, jsonClass);
    }

    /// <summary>
    /// Sets the superclass field (field 0).
    ///
    /// Blue Book: Every class except Object has a superclass.
    /// Object's superclass is nil.
    /// </summary>
    private void SetSuperclass(nint classOop, JsonClass jsonClass)
    {
        var superclassIndex = LayoutManagement.Layout.Class_Superclass;

        if (!jsonClass.SuperNameIndex.HasValue)
        {
            // No superclass (e.g., Object) - set to nil
            ObjectMemory.SetInstanceVariable(classOop, superclassIndex, SObjectRef.Nil);
        }
        else
        {
            // Resolve superclass by name
            var superName = _loader.Bundle.Symbols[jsonClass.SuperNameIndex.Value];
            var superLocalId = jsonClass.IsMetaclass ? $"meta:{superName}" : $"class:{superName}";

            try
            {
                var superclassRef = _loader.ResolveLocalId(superLocalId);
                ObjectMemory.SetInstanceVariable(classOop, superclassIndex, superclassRef);
            }
            catch
            {
                // Superclass not yet created - this happens during bootstrap
                // Will be patched later by PatchApplicator
                ObjectMemory.SetInstanceVariable(classOop, superclassIndex, SObjectRef.Nil);
            }
        }
    }

    /// <summary>
    /// Sets the methodDict field (field 1).
    ///
    /// Creates an empty MethodDictionary with appropriate capacity.
    /// Methods are installed later by MethodInstaller.
    ///
    /// Blue Book MethodDictionary:
    /// - Inherits from IdentityDictionary
    /// - Has 2 instance variables: tally (count), valueArray (array of CompiledMethods)
    /// - Has indexed slots containing selector Symbols (keys)
    /// - Uses parallel arrays: selectors in indexed slots, methods in valueArray
    /// </summary>
    private void SetMethodDictionary(nint classOop, JsonClass jsonClass)
    {
        var methodDictIndex = LayoutManagement.Layout.Class_MethodDict;

        // Count methods for this class in JSON
        var methodCount = CountMethodsForClass(jsonClass);

        // Create MethodDictionary with appropriate capacity
        // Capacity should be >= method count to avoid rehashing
        var capacity = CalculateDictionaryCapacity(methodCount);

        var methodDictClass = ObjectMemory.GetMethodDictionaryClass();
        if (methodDictClass.IsNil)
            throw new InvalidOperationException("MethodDictionary class not found");

        // Allocate MethodDictionary: 2 instance vars + indexed slots for selectors
        var methodDictOop = ObjectMemory.AllocateObject(
            methodDictClass,
            ObjectFormat.PointerIndexed,        // Has both instance vars and indexed slots
            instanceVarCount: 2,                    // tally, valueArray
            indexedSize: capacity               // Slots for selector keys
        );

        // Initialize tally to 0
        ObjectMemory.SetInstanceVariable(methodDictOop, 0, SObjectRef.FromSmallInteger(0));

        // Create valueArray (Array to hold CompiledMethods)
        var arrayClass = ObjectMemory.GetArrayClass();
        if (arrayClass.IsNil)
            throw new InvalidOperationException("Array class not found");

        var valueArrayOop = ObjectMemory.AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: capacity
        );

        // Initialize valueArray to all nil
        for (int i = 0; i < capacity; i++)
        {
            ObjectMemory.SetIndexedPointer(valueArrayOop, i, SObjectRef.Nil);
        }

        // Set valueArray in methodDict
        ObjectMemory.SetInstanceVariable(methodDictOop, 1, SObjectRef.FromObjectPointer(valueArrayOop));

        // Initialize indexed slots (selectors) to nil
        for (int i = 0; i < capacity; i++)
        {
            ObjectMemory.SetIndexedPointer(methodDictOop, i, SObjectRef.Nil);
        }

        // Set methodDict field in class
        ObjectMemory.SetInstanceVariable(classOop, methodDictIndex, SObjectRef.FromObjectPointer(methodDictOop));
    }

    /// <summary>
    /// Sets the format field (field 2).
    ///
    /// Blue Book Chapter 16: Instance Specification
    /// Format word (SmallInteger) encoding:
    /// - Bits 0-5: instance variable count (0-63)
    /// - Bit 6 (0x40): indexable flag (can have indexed slots)
    /// - Bit 7 (0x80): byte-indexed flag (indexed slots are bytes, not pointers)
    ///
    /// Examples:
    /// - Point: 2 instance vars, not indexable → 0x02
    /// - Array: 0 instance vars, indexable → 0x40
    /// - String: 0 instance vars, byte-indexable → 0xC0
    /// </summary>
    private void SetFormat(nint classOop, JsonClass jsonClass)
    {
        var formatIndex = LayoutManagement.Layout.Class_Format;

        // JSON provides format word directly (compiler computed it)
        var formatWord = jsonClass.Format;

        // Store as SmallInteger
        ObjectMemory.SetInstanceVariable(classOop, formatIndex, SObjectRef.FromSmallInteger(formatWord));
    }

    /// <summary>
    /// Sets the instanceVariables field (field 4).
    ///
    /// Creates an Array of Symbols representing instance variable names.
    /// </summary>
    private void SetInstanceVariables(nint classOop, JsonClass jsonClass)
    {
        var instVarsIndex = LayoutManagement.Layout.Class_InstanceVariables;

        if (jsonClass.InstVarNames.Length == 0)
        {
            // No instance variables - set to nil
            ObjectMemory.SetInstanceVariable(classOop, instVarsIndex, SObjectRef.Nil);
        }
        else
        {
            // Create Array of Symbols
            var arrayClass = ObjectMemory.GetArrayClass();
            if (arrayClass.IsNil)
                throw new InvalidOperationException("Array class not found");

            var arrayOop = ObjectMemory.AllocateObject(
                arrayClass,
                ObjectFormat.PointerIndexed,
                instanceVarCount: 0,
                indexedSize: jsonClass.InstVarNames.Length
            );

            // Populate with Symbols
            for (int i = 0; i < jsonClass.InstVarNames.Length; i++)
            {
                var symbolIndex = jsonClass.InstVarNames[i];
                var symbolLocalId = $"symbol:{symbolIndex}";
                var symbolRef = _loader.ResolveLocalId(symbolLocalId);
                ObjectMemory.SetIndexedPointer(arrayOop, i, symbolRef);
            }

            ObjectMemory.SetInstanceVariable(classOop, instVarsIndex, SObjectRef.FromObjectPointer(arrayOop));
        }
    }

    /// <summary>
    /// Sets the name field (field 5).
    ///
    /// Class name is a Symbol (unique string).
    /// </summary>
    private void SetName(nint classOop, JsonClass jsonClass)
    {
        var nameIndex = LayoutManagement.Layout.Class_Name;

        // Resolve symbol from JSON
        var symbolLocalId = $"symbol:{jsonClass.NameIndex}";
        var nameSymbol = _loader.ResolveLocalId(symbolLocalId);

        ObjectMemory.SetInstanceVariable(classOop, nameIndex, nameSymbol);
    }

    /// <summary>
    /// Counts how many methods this class has in JSON.
    /// Used to size the MethodDictionary appropriately.
    /// </summary>
    private int CountMethodsForClass(JsonClass jsonClass)
    {
        // Build the expected localId for this class
        var className = _loader.Bundle.Symbols[jsonClass.NameIndex];
        var expectedLocalId = jsonClass.IsMetaclass ? $"meta:{className}" : $"class:{className}";

        int count = 0;

        foreach (var method in _loader.Bundle.Methods)
        {
            // Match by homeClassLocalId
            if (method.HomeClassLocalId == expectedLocalId)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Calculates dictionary capacity for given element count.
    /// Uses power of 2 sizing with load factor ~0.75
    /// </summary>
    private int CalculateDictionaryCapacity(int elementCount)
    {
        if (elementCount == 0)
            return 4; // Minimum size

        // Target 75% load factor
        var targetCapacity = (int)(elementCount / 0.75);

        // Round up to next power of 2
        var capacity = 4;
        while (capacity < targetCapacity)
            capacity *= 2;

        return capacity;
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
}
