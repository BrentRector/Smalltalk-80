namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Temporary helper methods to support migration from old SObject architecture to pure SObjectRef.
/// These methods provide convenience wrappers and missing functionality.
/// </summary>
public static partial class ObjectMemory
{
    /// <summary>
    /// Creates a compiled method with a selector (convenience wrapper).
    /// </summary>
    public static SObjectRef CreateCompiledMethod(
        string selector,
        byte[] bytecodes,
        SObjectRef[] literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0)
    {
        // Per Blue Book: the selector is stored as the LAST literal in the literals array
        var selectorSymbol = InternSymbol(selector);

        // Add selector as the last literal
        var allLiterals = new SObjectRef[literals.Length + 1];
        Array.Copy(literals, 0, allLiterals, 0, literals.Length);
        allLiterals[literals.Length] = selectorSymbol;  // Selector is last literal

        return CreateCompiledMethod(bytecodes, allLiterals, numTemps, numArgs, primitiveIndex);
    }

    /// <summary>
    /// Adds a method to a class's method dictionary.
    /// </summary>
    public static void AddMethod(nint classOop, string selector, SObjectRef methodRef)
    {
        var methodDict = GetClassMethodDictionary(classOop);
        if (!methodDict.IsNil)
        {
            AddMethodToDictionary(methodDict.AsObjectPointer(), selector, methodRef);
        }
        else
        {
            if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
            {
                Console.WriteLine($"[WARNING] Cannot add method '{selector}' - class has no method dictionary");
            }
        }
    }

    /// <summary>
    /// Gets all method selectors from a class.
    /// </summary>
    public static List<string> GetClassMethodSelectors(nint classOop)
    {
        var selectors = new List<string>();
        var methodDict = GetClassMethodDictionary(classOop);

        if (!methodDict.IsNil)
        {
            // Method dictionary is a Dictionary with associations
            // We need to iterate through its keys
            // NOTE: For MethodDictionary/IdentityDictionary, field 0 is tally and field 1 is valueArray
            // For regular Dictionary, the indexed slots contain the associations
            var associations = GetInstanceVariable(methodDict.AsObjectPointer(), 0); // This is the tally, not what we want

            // TODO: This code appears to be incorrect - it's accessing tally instead of the dictionary contents
            // For MethodDictionary, we should access the indexed slots (keys) or use proper lookup methods

            if (!associations.IsNil && !associations.IsSmallInteger)
            {
                var size = GetIndexedSize(associations.AsObjectPointer());
                for (int i = 0; i < size; i++)
                {
                    var assoc = GetIndexedPointer(associations.AsObjectPointer(), i);
                    if (!assoc.IsNil)
                    {
                        // Association has key at index 0
                        Generated.Layouts.STAssociation assocStruct = assoc;
                        var key = assocStruct.Key;
                        if (!key.IsNil)
                        {
                            selectors.Add(SymbolToString(key));
                        }
                    }
                }
            }
        }

        return selectors;
    }

    /// <summary>
    /// Gets instance variable names from a class.
    /// </summary>
    public static string[] GetClassInstanceVariableNames(nint classOop)
    {
        var instanceVars = GetInstanceVariable(classOop, ClassLayout.InstanceVariablesIndex);

        if (instanceVars.IsNil)
            return [];

        // Instance variables are typically stored as an array of symbols
        if (!instanceVars.IsSmallInteger)
        {
            var size = GetIndexedSize(instanceVars.AsObjectPointer());
            var names = new string[size];

            for (int i = 0; i < size; i++)
            {
                var varNameRef = GetIndexedPointer(instanceVars.AsObjectPointer(), i);
                if (!varNameRef.IsNil)
                {
                    names[i] = SymbolToString(varNameRef);
                }
                else
                {
                    names[i] = $"var{i}";
                }
            }

            return names;
        }

        return [];
    }

    /// <summary>
    /// Gets the total number of instance variables (including inherited).
    /// </summary>
    public static int GetClassTotalInstanceVariableCount(nint classOop)
    {
        var instSpec = GetInstanceVariable(classOop, ClassLayout.InstanceSpecificationIndex);

        if (instSpec.IsSmallInteger)
        {
            var spec = instSpec.AsSmallInteger();
            // Lower 6 bits encode the instance variable count
            return spec & 0x3F;
        }

        // Count instance variables manually by walking the hierarchy
        var count = 0;
        var currentClass = SObjectRef.FromObjectPointer(classOop);

        while (!currentClass.IsNil)
        {
            var instVars = GetClassInstanceVariableNames(currentClass.AsObjectPointer());
            count += instVars.Length;
            currentClass = GetClassSuperclass(currentClass.AsObjectPointer());
        }

        return count;
    }

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    public static SObjectRef GetObjectClass(SObjectRef objectRef)
    {
        if (objectRef.IsNil)
            return GetNilClass();
        if (objectRef.IsSmallInteger)
            return GetSmallIntegerClass();

        return GetClass(objectRef.AsObjectPointer());
    }

    /// <summary>
    /// Gets the primitive index from a compiled method.
    /// </summary>
    public static int GetCompiledMethodPrimitiveIndex(nint methodOop)
    {
        // For now, return 0 as we don't store primitive index in the current implementation
        // TODO: Extend CompiledMethod format to store primitive index
        return 0;
    }

    /// <summary>
    /// Gets the number of literals in a compiled method.
    /// </summary>
    public static int GetCompiledMethodLiteralCount(nint methodOop)
    {
        var literals = GetCompiledMethodLiterals(methodOop);
        return literals.Length;
    }


    /// <summary>
    /// Gets the string value from a String object.
    /// </summary>
    public static string GetStringValue(nint stringOop)
    {
        return StringToString(SObjectRef.FromObjectPointer(stringOop));
    }

    /// <summary>
    /// Gets the symbol value from a Symbol object.
    /// </summary>
    public static string GetSymbolValue(nint symbolOop)
    {
        return SymbolToString(SObjectRef.FromObjectPointer(symbolOop));
    }

    /// <summary>
    /// Gets the size of an object (number of indexed slots).
    /// </summary>
    public static int GetObjectSize(nint oop)
    {
        return GetIndexedSize(oop);
    }
}