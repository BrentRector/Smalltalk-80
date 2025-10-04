namespace Smalltalk80.Interpreter.Core.Values;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Generated.Layouts;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Extension methods for SObjectRef to provide class-related operations.
/// These methods allow an SObjectRef pointing to a Class object to act as a class.
/// All operations go through ObjectMemoryNew - never exposing internal SObject instances.
/// </summary>
public static class SObjectRefClassExtensions
{
    // Thread-safe method cache for LookupMethod

    /// <summary>
    /// Gets the name of this class as a string.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The class name as a string, or an empty string if the name is nil.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static string GetClassName(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetClassName requires a heap object (class)");

        // Get the name instance variable (at index 6 for Class)
        STClass classStruct = classRef;
        var nameSymbolRef = classStruct.Name;

        if (nameSymbolRef.IsNil)
            return string.Empty;

        // Convert the Symbol to a string
        return ObjectMemoryNew.SymbolToString(nameSymbolRef);
    }

    /// <summary>
    /// Gets the superclass of this class.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The superclass SObjectRef, or nil if this class has no superclass.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static SObjectRef GetSuperclass(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetSuperclass requires a heap object (class)");

        STClass classStruct = classRef;
        return classStruct.Superclass;
    }

    /// <summary>
    /// Gets the method dictionary of this class.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The method dictionary SObjectRef.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static SObjectRef GetMethodDictionary(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetMethodDictionary requires a heap object (class)");

        STClass classStruct = classRef;
        return classStruct.MethodDict;
    }

    /// <summary>
    /// Gets the instance variable names defined in this class (not including inherited ones).
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>An array of instance variable names as strings.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static string[] GetInstanceVariableNames(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetInstanceVariableNames requires a heap object (class)");

        STClass classStruct = classRef;
        var instanceVarsRef = classStruct.InstanceVariables;

        if (instanceVarsRef.IsNil)
            return Array.Empty<string>();

        // The instance variables field is an Array of Symbols
        var count = instanceVarsRef.GetIndexedSize();
        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            var symbolRef = instanceVarsRef.GetIndexedPointer(i);
            if (!symbolRef.IsNil)
            {
                names[i] = ObjectMemoryNew.SymbolToString(symbolRef);
            }
            else
            {
                names[i] = string.Empty;
            }
        }

        return names;
    }

    /// <summary>
    /// Gets the total number of instance variables for instances of this class,
    /// including those inherited from superclasses.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The total count of instance variables.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static int GetTotalInstanceVariableCount(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetTotalInstanceVariableCount requires a heap object (class)");

        int totalCount = 0;
        var currentClass = classRef;

        // Walk up the class hierarchy counting instance variables
        // Stop when we reach nil (the singleton instance of UndefinedObject, which marks the root)
        while (currentClass.IsHeapObject)
        {
            // Stop if we've reached the nil singleton (sentinel at root of class hierarchy)
            // NOTE: We compare against the SOA nil singleton, not SObjectRef.Nil (value=0)
            if (currentClass == Memory.SpecialObjectsArray.Instance.Nil)
                break;

            // Access class layout information (only valid on class objects, not regular instances like nil)
            STClass currentClassStruct = currentClass;
            var instanceVarsRef = currentClassStruct.InstanceVariables;
            if (instanceVarsRef.IsHeapObject)
            {
                totalCount += instanceVarsRef.GetIndexedSize();
            }

            // Move to superclass
            currentClass = currentClassStruct.Superclass;
        }

        return totalCount;
    }

    /// <summary>
    /// Looks up a method in this class and its superclass chain.
    /// Uses a cache for improved performance.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <param name="selector">The method selector to look up.</param>
    /// <returns>The compiled method if found, or nil if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static SObjectRef LookupMethod(this SObjectRef classRef, string selector)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("LookupMethod requires a heap object (class)");

        // Note: The method cache expects SClass objects, which we don't have access to here
        // For now, we'll do direct lookup without caching
        // TODO: Consider creating a separate cache that works with SObjectRef

        var selectorSymbol = ObjectMemoryNew.InternSymbol(selector);
        var currentClass = classRef;

        while (!currentClass.IsNil && currentClass.IsHeapObject)
        {
            var methodDict = currentClass.GetMethodDictionary();

            if (!methodDict.IsNil)
            {
                // MethodDictionary is an IdentityDictionary
                // It has tally (index 0), valueArray (index 1), and indexed part contains keys
                Generated.Layouts.STIdentityDictionary dict = methodDict;
                var valueArray = dict.ValueArray;
                var keyCount = methodDict.GetIndexedSize();

                // Search for the selector in the keys
                for (int i = 0; i < keyCount; i++)
                {
                    var key = methodDict.GetIndexedPointer(i);
                    if (key.IsIdenticalTo(selectorSymbol))
                    {
                        // Found the selector, get the corresponding method from valueArray
                        if (!valueArray.IsNil && i < valueArray.GetIndexedSize())
                        {
                            return valueArray.GetIndexedPointer(i);
                        }
                        break;
                    }
                }
            }

            // Move to superclass
            currentClass = currentClass.GetSuperclass();
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Adds a method to this class's method dictionary.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <param name="selector">The method selector.</param>
    /// <param name="compiledMethod">The compiled method to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object or if the method dictionary is nil.</exception>
    public static void AddMethod(this SObjectRef classRef, string selector, SObjectRef compiledMethod)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("AddMethod requires a heap object (class)");

        var methodDict = classRef.GetMethodDictionary();
        if (methodDict.IsNil)
            throw new InvalidOperationException("Method dictionary is nil");

        var selectorSymbol = ObjectMemoryNew.InternSymbol(selector);

        // Get the tally and valueArray
        Generated.Layouts.STIdentityDictionary dict = methodDict;
        var tallyRef = dict.Tally;
        var valueArray = dict.ValueArray;

        if (!tallyRef.IsSmallInteger || valueArray.IsNil)
            throw new InvalidOperationException("Invalid method dictionary structure");

        var tally = tallyRef.AsSmallInteger();
        var capacity = methodDict.GetIndexedSize();

        // Find an empty slot or replace existing method
        int targetIndex = -1;
        bool replacing = false;

        for (int i = 0; i < capacity; i++)
        {
            var key = methodDict.GetIndexedPointer(i);

            if (key.IsNil && targetIndex == -1)
            {
                // Found an empty slot
                targetIndex = i;
                if (!replacing) break;
            }
            else if (key.IsIdenticalTo(selectorSymbol))
            {
                // Replacing existing method
                targetIndex = i;
                replacing = true;
                break;
            }
        }

        if (targetIndex == -1)
        {
            // Dictionary is full - would need to grow it
            throw new InvalidOperationException("Method dictionary is full - resize not implemented");
        }

        // Set the key and value
        methodDict.SetIndexedPointer(targetIndex, selectorSymbol);
        valueArray.SetIndexedPointer(targetIndex, compiledMethod);

        // Update tally if we added a new method (not replacing)
        if (!replacing)
        {
            dict.Tally = SObjectRef.FromSmallInteger(tally + 1);
        }

        // Invalidate any cached entries for this class
        // Note: Since we don't have access to SClass, we can't use the existing cache
        // This would need to be handled at a higher level
    }

    /// <summary>
    /// Gets the number of methods defined in this class (not counting inherited methods).
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The number of methods defined in this class.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static int GetMethodCount(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetMethodCount requires a heap object (class)");

        var methodDict = classRef.GetMethodDictionary();

        if (methodDict.IsNil)
            return 0;

        Generated.Layouts.STIdentityDictionary dict = methodDict;
        var tallyRef = dict.Tally;

        if (!tallyRef.IsSmallInteger)
            return 0;

        return tallyRef.AsSmallInteger();
    }

    /// <summary>
    /// Gets all method selectors defined in this class (not counting inherited methods).
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>A list of all method selectors as strings.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static List<string> GetAllMethods(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetAllMethods requires a heap object (class)");

        var methods = new List<string>();
        var methodDict = classRef.GetMethodDictionary();

        if (methodDict.IsNil)
            return methods;

        var capacity = methodDict.GetIndexedSize();

        // Iterate through all keys in the method dictionary
        for (int i = 0; i < capacity; i++)
        {
            var key = methodDict.GetIndexedPointer(i);
            if (!key.IsNil)
            {
                // Convert symbol to string
                methods.Add(ObjectMemoryNew.SymbolToString(key));
            }
        }

        return methods;
    }

    /// <summary>
    /// Creates an instance of this class.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <param name="indexedSize">The size of the indexed portion for variable classes (default 0).</param>
    /// <returns>A new instance of this class.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static SObjectRef CreateInstance(this SObjectRef classRef, int indexedSize = 0)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("CreateInstance requires a heap object (class)");

        // Get the format from the class
        STClass classStruct = classRef;
        var formatRef = classStruct.Format;

        if (!formatRef.IsSmallInteger)
            throw new InvalidOperationException("Invalid class format field");

        var formatValue = formatRef.AsSmallInteger();

        // Decode the format (simplified - actual format encoding is complex)
        // The format encodes: instance variable count, indexability, pointer/byte/word type
        // For now, we'll use a simplified approach
        var instanceVarCount = classRef.GetTotalInstanceVariableCount();

        // Determine the object format based on the class format field
        // This is simplified - the actual Blue Book encoding is more complex
        ObjectFormat objectFormat;
        if (indexedSize == 0)
        {
            objectFormat = ObjectFormat.FixedPointer;
        }
        else if ((formatValue & 0x100) != 0) // Check if byte-indexed
        {
            objectFormat = ObjectFormat.ByteIndexed;
        }
        else if ((formatValue & 0x200) != 0) // Check if word-indexed
        {
            objectFormat = ObjectFormat.WordIndexed;
        }
        else
        {
            objectFormat = ObjectFormat.PointerIndexed;
        }

        // Allocate the object
        var oop = ObjectMemoryNew.AllocateObject(classRef, objectFormat, instanceVarCount, indexedSize);

        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Checks if this class is a metaclass.
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>True if this is a metaclass; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    public static bool IsMetaclass(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("IsMetaclass requires a heap object (class)");

        // A metaclass has the Metaclass class as its class
        var classOfClass = classRef.GetClassRef();

        if (classOfClass.IsNil)
            return false;

        // Check if the class of this class is named "Metaclass"
        // This is a simplified check - in a real system we'd compare against the actual Metaclass class
        var className = classOfClass.GetClassName();
        return className == "Metaclass";
    }

    /// <summary>
    /// Gets the metaclass of this class (the class of this class).
    /// </summary>
    /// <param name="classRef">The SObjectRef pointing to a Class object.</param>
    /// <returns>The metaclass SObjectRef.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a heap object.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef GetMetaclass(this SObjectRef classRef)
    {
        if (!classRef.IsHeapObject)
            throw new InvalidOperationException("GetMetaclass requires a heap object (class)");

        return classRef.GetClassRef();
    }
}