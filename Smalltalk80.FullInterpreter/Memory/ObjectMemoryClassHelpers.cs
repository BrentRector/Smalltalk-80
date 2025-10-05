namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.BlueBook;

/// <summary>
/// Helper methods for working with classes and methods in ObjectMemory.
/// This is a temporary extension to add class manipulation helpers.
/// </summary>
public static partial class ObjectMemory
{
    #region Class Helpers

    /// <summary>
    /// Class layout constants matching the Smalltalk-80 specification.
    /// </summary>
    public static class ClassLayout
    {
        /// <summary>Index of the superclass field in Class objects.</summary>
        public const int SuperclassIndex = 0;

        /// <summary>Index of the method dictionary field in Class objects.</summary>
        public const int MethodDictionaryIndex = 1;

        /// <summary>Index of the instance specification field in Class objects.</summary>
        public const int InstanceSpecificationIndex = 2;

        /// <summary>Index of the subclasses field in Class objects.</summary>
        public const int SubclassesIndex = 3;

        /// <summary>Index of the instance variables field in Class objects.</summary>
        public const int InstanceVariablesIndex = 4;

        /// <summary>Index of the name field in Class objects.</summary>
        public const int NameIndex = 5;
    }

    /// <summary>
    /// Gets the superclass of a class.
    /// </summary>
    public static SObjectRef GetClassSuperclass(nint classOop)
    {
        var classRef = SObjectRef.FromObjectPointer(classOop);
        return GetInstanceVariable(classOop, LayoutManagement.Layout.Class_Superclass);
    }

    /// <summary>
    /// Gets the method dictionary of a class.
    /// </summary>
    public static SObjectRef GetClassMethodDictionary(nint classOop)
    {
        return GetInstanceVariable(classOop, LayoutManagement.Layout.Class_MethodDict);
    }

    /// <summary>
    /// Gets the name of a class.
    /// </summary>
    public static string GetClassName(nint classOop)
    {
        var nameRef = GetInstanceVariable(classOop, LayoutManagement.Layout.Class_Name);
        if (nameRef.IsNil)
            return "Unknown";

        if (!nameRef.IsHeapObject)
            return $"Invalid({nameRef})";

        // Assuming the name is stored as a Symbol
        return SymbolToString(nameRef);
    }

    /// <summary>
    /// Looks up a method in a class hierarchy.
    /// </summary>
    public static SObjectRef LookupMethod(nint classOop, string selector)
    {
        var currentClassRef = SObjectRef.FromObjectPointer(classOop);

        while (!currentClassRef.IsNil)
        {
            var methodDict = GetClassMethodDictionary(currentClassRef.AsObjectPointer());
            if (!methodDict.IsNil)
            {
                var method = LookupInMethodDictionary(methodDict.AsObjectPointer(), selector);
                if (!method.IsNil)
                    return method;
            }

            // Move to superclass
            currentClassRef = GetClassSuperclass(currentClassRef.AsObjectPointer());
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Creates a new instance of a class.
    /// </summary>
    public static SObjectRef CreateInstance(nint classOop, int indexedSize = 0)
    {
        // Get instance specification to determine format and variable count
        var instSpec = GetInstanceVariable(classOop, LayoutManagement.Layout.Class_Format); // Format field contains instance specification

        // Default format and instance variable count
        var format = ObjectFormat.FixedPointer;
        var instVarCount = 0;

        if (instSpec.IsSmallInteger)
        {
            var spec = instSpec.AsSmallInteger();
            // Decode instance specification to match CreateClassObject encoding
            // Bits 0-5: instance variable count
            // Bit 6 (0x40): indexable flag
            // Bit 7 (0x80): byte-indexed flag
            instVarCount = spec & 0x3F; // Lower 6 bits for instance variable count

            bool isIndexable = (spec & 0x40) != 0;   // Bit 6
            bool isByteIndexed = (spec & 0x80) != 0; // Bit 7

            if (!isIndexable)
            {
                format = ObjectFormat.FixedPointer;
            }
            else if (isByteIndexed)
            {
                format = ObjectFormat.ByteIndexed;
            }
            else
            {
                format = ObjectFormat.PointerIndexed;
            }
        }

        var oop = AllocateObject(
            SObjectRef.FromObjectPointer(classOop),
            format,
            instVarCount,
            indexedSize);

        return SObjectRef.FromObjectPointer(oop);
    }

    #endregion

    #region Method Dictionary Helpers

    /// <summary>
    /// MethodDictionary layout constants.
    /// Per Blue Book: MethodDictionary inherits from IdentityDictionary which uses parallel arrays.
    /// </summary>
    public static class MethodDictionaryLayout
    {
        /// <summary>Index of the tally field in MethodDictionary objects.</summary>
        public const int TallyIndex = 0;    // Instance var 0: tally (number of elements)

        /// <summary>Index of the array field in MethodDictionary objects.</summary>
        public const int ArrayIndex = 1;    // Instance var 1: valueArray (array of CompiledMethods)
        // Note: The MethodDictionary also has indexed slots that contain the selector Symbols (keys)
    }

    /// <summary>
    /// Looks up a method in a method dictionary.
    /// MethodDictionary uses parallel arrays: keys in indexed slots, values in valueArray.
    /// </summary>
    public static SObjectRef LookupInMethodDictionary(nint dictOop, string selector)
    {
        // MethodDictionary structure (per Blue Book):
        // - Instance var 0 (tally): number of elements
        // - Instance var 1 (valueArray): array of CompiledMethod objects
        // - Indexed slots: array of selector Symbols (keys)

        var dictSize = GetIndexedSize(dictOop);

        // Get valueArray from instance variable 1
        var valueArrayRef = GetInstanceVariable(dictOop, MethodDictionaryLayout.ArrayIndex);
        if (valueArrayRef.IsNil)
            return SObjectRef.Nil;

        var valueArrayOop = valueArrayRef.AsObjectPointer();

        // Linear search through keys (simplified - real implementation would use hashing)
        for (int i = 0; i < dictSize; i++)
        {
            // Get selector from MethodDictionary's indexed slots (the keys)
            var selectorRef = GetIndexedPointer(dictOop, i);

            if (!selectorRef.IsNil && selectorRef.IsHeapObject)
            {
                var selectorString = SymbolToString(selectorRef);
                if (selectorString == selector)
                {
                    // Found it - return the method from parallel valueArray
                    return GetIndexedPointer(valueArrayOop, i);
                }
            }
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Adds a method to a method dictionary.
    /// MethodDictionary uses parallel arrays: keys in indexed slots, values in valueArray.
    /// </summary>
    public static void AddMethodToDictionary(nint dictOop, string selector, SObjectRef methodRef)
    {
        // MethodDictionary structure (per Blue Book):
        // - Instance var 0 (tally): number of elements
        // - Instance var 1 (valueArray): array of CompiledMethod objects
        // - Indexed slots: array of selector Symbols (keys)

        var dictSize = GetIndexedSize(dictOop);

        // Get valueArray from instance variable 1
        var valueArrayRef = GetInstanceVariable(dictOop, MethodDictionaryLayout.ArrayIndex);
        if (valueArrayRef.IsNil)
            return; // Can't add to nil array

        var valueArrayOop = valueArrayRef.AsObjectPointer();

        // Find an empty slot or matching selector
        for (int i = 0; i < dictSize; i++)
        {
            // Get selector from MethodDictionary's indexed slots (the keys)
            var selectorRef = GetIndexedPointer(dictOop, i);

            if (selectorRef.IsNil)
            {
                // Found empty slot - add new method
                var selectorSymbol = InternSymbol(selector);
                SetIndexedPointer(dictOop, i, selectorSymbol);  // Set key in indexed slot
                SetIndexedPointer(valueArrayOop, i, methodRef); // Set method in valueArray

                // Update tally
                var tallyRef = GetInstanceVariable(dictOop, MethodDictionaryLayout.TallyIndex);
                if (tallyRef.IsSmallInteger)
                {
                    SetInstanceVariable(dictOop, MethodDictionaryLayout.TallyIndex,
                        SObjectRef.FromSmallInteger(tallyRef.AsSmallInteger() + 1));
                }
                return;
            }

            // Check if this slot has the same selector (and is a heap object)
            if (selectorRef.IsHeapObject)
            {
                var selectorString = SymbolToString(selectorRef);
                if (selectorString == selector)
                {
                    // Update existing method in parallel valueArray
                    SetIndexedPointer(valueArrayOop, i, methodRef);
                    return;
                }
            }
        }

        // No space found - would need to grow the dictionary (not implemented here)
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ERROR] Method dictionary full! Cannot add method '{selector}'. Dictionary size={dictSize}");
        }
        throw new InvalidOperationException($"Method dictionary is full, cannot add {selector}");
    }

    /// <summary>
    /// Creates an association (key-value pair).
    /// </summary>
    private static nint CreateAssociation(SObjectRef keyRef, SObjectRef valueRef)
    {
        // Association class would need to be looked up properly
        // For now, create a simple 2-element fixed pointer object
        var assocClass = GetAssociationClass();

        // Debug: check if Association class is properly initialized
        if (!assocClass.IsNil)
        {
            var assocFormat = GetInstanceVariable(assocClass.AsObjectPointer(), LayoutManagement.Layout.Class_Format);
            if (assocFormat.IsSmallInteger)
            {
                int assocInstVars = assocFormat.AsSmallInteger() & 0x3F;
                Console.WriteLine($"[DEBUG] CreateAssociation: Association class has {assocInstVars} instance variables");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] CreateAssociation: Association class is NIL!");
        }

        var assocOop = AllocateObject(
            assocClass,
            ObjectFormat.FixedPointer,
            2,
            0);

        // Set key and value using direct field access
        SetInstanceVariable(assocOop, 0, keyRef);  // Key is first field
        SetInstanceVariable(assocOop, 1, valueRef); // Value is second field

        return assocOop;
    }

    #endregion

    #region Compiled Method Helpers Extended

    /// <summary>
    /// CompiledMethod header layout.
    /// </summary>
    public static class CompiledMethodHeader
    {
        /// <summary>
        /// Extracts the number of temporaries from a method header.
        /// </summary>
        public static int GetTemporaryCount(ushort header)
        {
            // Bits 3-7 contain temporary count
            return (header >> 3) & 0x1F;
        }

        /// <summary>
        /// Extracts the number of literals from a method header.
        /// </summary>
        public static int GetLiteralCount(ushort header)
        {
            // Bits 8-14 contain literal count
            return (header >> 8) & 0x7F;
        }

        /// <summary>
        /// Extracts the primitive index from a method header.
        /// </summary>
        public static int GetPrimitiveIndex(ushort header)
        {
            // For methods with primitives, this would be encoded
            // Simplified for now
            return 0;
        }

        /// <summary>
        /// Checks if the method uses extended header.
        /// </summary>
        public static bool HasExtendedHeader(ushort header)
        {
            // Bit 15 indicates extended header
            return (header & 0x8000) != 0;
        }
    }

    /// <summary>
    /// Gets a specific literal from a CompiledMethod.
    /// </summary>
    public static SObjectRef GetCompiledMethodLiteral(nint methodOop, int index)
    {
        var literals = GetCompiledMethodLiterals(methodOop);
        if (index < 0 || index >= literals.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return literals[index];
    }

    /// <summary>
    /// Gets the selector from a CompiledMethod (usually the last literal).
    /// </summary>
    public static string GetCompiledMethodSelector(nint methodOop)
    {
        var literals = GetCompiledMethodLiterals(methodOop);
        if (literals.Length == 0)
            return "unknown";

        // Selector is typically the last literal
        var selectorRef = literals[literals.Length - 1];
        if (selectorRef.IsNil)
            return "unknown";

        return SymbolToString(selectorRef);
    }

    /// <summary>
    /// Gets the number of arguments for a CompiledMethod.
    /// </summary>
    public static int GetCompiledMethodArgumentCount(nint methodOop)
    {
        var selector = GetCompiledMethodSelector(methodOop);
        // Count colons in selector to determine argument count
        return selector.Count(c => c == ':');
    }

    /// <summary>
    /// Gets the number of temporaries for a CompiledMethod.
    /// </summary>
    public static int GetCompiledMethodTemporaryCount(nint methodOop)
    {
        var header = GetCompiledMethodHeader(methodOop);
        return CompiledMethodHeader.GetTemporaryCount(header);
    }

    /// <summary>
    /// Creates a new CompiledMethod.
    /// </summary>
    public static SObjectRef CreateCompiledMethod(
        byte[] bytecodes,
        SObjectRef[] literals,
        int numTemps,
        int numArgs,
        int primitiveIndex = 0)
    {
        // Get CompiledMethod class
        var compiledMethodClass = GetCompiledMethodClass();
        if (compiledMethodClass.IsNil)
            throw new InvalidOperationException("CompiledMethod class not initialized. Call SystemDictionary.Initialize() first.");

        // Create and set header FIRST to determine correct size
        // Header format: bits 0-2: arg count, bits 3-7: temp count, bits 8-14: literal count, bit 15: flag bit
        ushort header = 0;
        header |= (ushort)(numArgs & 0x07);                    // Bits 0-2: arg count
        header |= (ushort)((numTemps & 0x1F) << 3);           // Bits 3-7: temp count
        header |= (ushort)((literals.Length & 0x7F) << 8);    // Bits 8-14: literal count

        if (primitiveIndex > 0)
        {
            header |= 0x8000; // Set bit 15 to indicate primitive/extended header
        }

        // Determine header size BEFORE allocation
        var headerSize = primitiveIndex > 0 ? 4 : 2; // Extended header has 4 bytes (header + primitive index)

        // Create the compiled method object with CORRECT size including proper header
        // For CompiledMethod: instance variables hold the literals, indexed byte data holds bytecodes
        var methodOop = AllocateObject(
            compiledMethodClass,
            ObjectFormat.CompiledMethod,
            literals.Length, // Instance variables for literals
            bytecodes.Length + headerSize); // Byte-indexed data for header + bytecodes (FIXED: use headerSize not hardcoded 2)

        // Set literals in instance variables (only if there are any)
        if (literals.Length > 0)
        {
            for (int i = 0; i < literals.Length; i++)
            {
                SetInstanceVariable(methodOop, i, literals[i]);
            }
        }

        // Set header as first two bytes (header already calculated above)
        var fullData = new byte[bytecodes.Length + headerSize];
        fullData[0] = (byte)(header & 0xFF);
        fullData[1] = (byte)((header >> 8) & 0xFF);

        if (primitiveIndex > 0)
        {
            // Extended header: bytes 2-3 contain primitive index
            fullData[2] = (byte)(primitiveIndex & 0xFF);
            fullData[3] = (byte)((primitiveIndex >> 8) & 0xFF);
            System.Array.Copy(bytecodes, 0, fullData, 4, bytecodes.Length);
        }
        else
        {
            System.Array.Copy(bytecodes, 0, fullData, 2, bytecodes.Length);
        }

        // Set the bytecode data
        SetByteIndexedData(methodOop, fullData);

        return SObjectRef.FromObjectPointer(methodOop);
    }

    #endregion
}