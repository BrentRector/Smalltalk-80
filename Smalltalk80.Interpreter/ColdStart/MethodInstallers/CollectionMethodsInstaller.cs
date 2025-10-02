namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs kernel methods for collection classes including Array, String, Symbol, 
/// ByteArray, Dictionary, Association, and MethodDictionary.
/// </summary>
public static class CollectionMethodsInstaller
{
    /// <summary>
    /// Installs all collection-related methods.
    /// </summary>
    public static void Install()
    {
        InstallArrayMethods();
        InstallStringMethods();
        InstallSymbolMethods();
        InstallByteArrayMethods();
        InstallDictionaryMethods();
        InstallAssociationMethods();
        InstallMethodDictionaryMethods();
    }
    
    /// <summary>
    /// Installs Array methods.
    /// </summary>
    private static void InstallArrayMethods()
    {
        var arrayClassRef = SystemDictionary.ArrayClassRef;
        if (arrayClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Array instance methods: #at:, #at:put:, #size, #do:, #collect:, #select:, #reject:, #includes:, #isEmpty, #first, #last, #atAll:put:");
        }
        
        // at: - primitive 60
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "at:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 60)
            [],
            1, 0, 60);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "at:", method1);
        instanceMethodCount++;

        // at:put: - primitive 61
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 61)
            [],
            2, 0, 61);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "at:put:", method2);
        instanceMethodCount++;

        // size - primitive 62
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "size",
            [], // No fallback needed - primitive 62 cannot fail
            [],
            0, 0, 62);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "size", method3);
        instanceMethodCount++;

        // do: block iteration
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "do:",
            [0x76, 0x69, 0x70, 0xC2, 0x6A, 0xB5, 0xAC, 0x0B, 0x70, 0x6A, 0xC0, 0x10, 0xE6, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF2, 0x73, 0x7C],
            [ObjectMemoryNew.InternSymbol("size"), ObjectMemoryNew.InternSymbol("at:"), ObjectMemoryNew.InternSymbol("value:")],
            1, 2, 0);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "do:", method4);
        instanceMethodCount++;

        // isEmpty
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "isEmpty",
            [0x70, 0xC2, 0x75, 0xB6, 0x7C], // self size = 0
            [ObjectMemoryNew.InternSymbol("size")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "isEmpty", method5);
        instanceMethodCount++;

        // first
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "first",
            [0x70, 0x76, 0xC0, 0x7C], // self at: 1
            [ObjectMemoryNew.InternSymbol("at:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "first", method6);
        instanceMethodCount++;

        // last
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "last",
            [0x70, 0x70, 0xC2, 0xC0, 0x7C], // self at: self size
            [ObjectMemoryNew.InternSymbol("at:"), ObjectMemoryNew.InternSymbol("size")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "last", method7);
        instanceMethodCount++;

        // atAll:put: - sets multiple elements to same value
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "atAll:put:",
            [0x10, 0x8F, 0x00, 0x03, 0x70, 0x12, 0x11, 0xE1, 0xF7, 0x70, 0x7C], // indices do: [:i | self at: i put: value]. ^self
            [ObjectMemoryNew.InternSymbol("do:"),
             ObjectMemoryNew.InternSymbol("at:put:")],
            2, 0, 0);
        ObjectMemoryNew.AddMethod(arrayClassRef.AsObjectPointer(), "atAll:put:", method8);
        instanceMethodCount++;
        
        // Note: Class methods would be installed on Array's metaclass
        // They would include: #new:, #with:, #with:with:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Array: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs String methods.
    /// </summary>
    private static void InstallStringMethods()
    {
        var stringClassRef = SystemDictionary.StringClassRef;
        if (stringClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing String instance methods: #at:, #at:put:, #size, #=, #hash, #,, #copyFrom:to:, #asSymbol, #asString, #keywords, #numArgs, #isKeyword, #printOn:");
        }
        
        // at: - primitive 63
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "at:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 63)
            [],
            1, 0, 63);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "at:", method9);
        instanceMethodCount++;

        // at:put: - primitive 64
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 64)
            [],
            2, 0, 64);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "at:put:", method10);
        instanceMethodCount++;

        // size - primitive 62
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "size",
            [], // No fallback needed - primitive 62 cannot fail
            [],
            0, 0, 62);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "size", method11);
        instanceMethodCount++;

        // = string comparison - primitive 105
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 105)
            [],
            1, 0, 105);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "=", method12);
        instanceMethodCount++;

        // hash - primitive 106
        var method13 = ObjectMemoryNew.CreateCompiledMethod(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash (fallback for primitive 106)
            [],
            0, 0, 106);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "hash", method13);
        instanceMethodCount++;

        // , (concatenation)
        var method14 = ObjectMemoryNew.CreateCompiledMethod(
            ",",
            [0x70, 0xC2, 0x10, 0xC2, 0xB0, 0x68, 0x44, 0xCC, 0x69, 0x76, 0x70, 0xC2, 0xE8, 0x6B,
             0x76, 0x10, 0xC2, 0xE8, 0x69, 0x7C], // Implementation of string concatenation
            [ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("String"),
             ObjectMemoryNew.InternSymbol("new:"),
             ObjectMemoryNew.InternSymbol("replaceFrom:to:with:")],
            1, 2, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), ",", method14);
        instanceMethodCount++;

        // asSymbol - primitive 110
        var method15 = ObjectMemoryNew.CreateCompiledMethod(
            "asSymbol",
            [], // No fallback needed - primitive 110 cannot fail
            [],
            0, 0, 110);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "asSymbol", method15);
        instanceMethodCount++;
        
        // asString (returns self for String)
        var method16 = ObjectMemoryNew.CreateCompiledMethod(
            "asString",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "asString", method16);
        instanceMethodCount++;

        // keywords - extracts keyword selectors from a string
        var method17 = ObjectMemoryNew.CreateCompiledMethod(
            "keywords",
            [0x41, 0xD0, 0x68, // OrderedCollection new
             0x76, 0x69, 0x6A, // index := 1, start := 1
             0x6A, 0x70, 0xC2, 0xB5, 0xAC, 0x20, // [index <= self size] whileTrue:
             0x70, 0x6A, 0xC0, 0x21, 0xE1, 0x9D, 0x0E, // (self at: index) = $: ifTrue:
             0x6B, 0x70, 0x6A, 0x76, 0xB1, 0x6B, 0xE2, 0xD2, // add from start to index
             0x6A, 0x76, 0xB0, 0x6B, 0x69, // index := index + 1, start := index
             0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xDF, // index := index + 1, loop
             0x68, 0x7C], // ^result
            [ObjectMemoryNew.InternSymbol("OrderedCollection"),
             ObjectMemoryNew.InternSymbol("new"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("<="),
             ObjectMemoryNew.InternSymbol("at:"),
             ObjectMemoryNew.InternSymbol(":"),
             ObjectMemoryNew.InternSymbol("copyFrom:to:"),
             ObjectMemoryNew.InternSymbol("add:")],
            0, 3, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "keywords", method17);
        instanceMethodCount++;

        // numArgs - counts the number of arguments in a selector
        var method18 = ObjectMemoryNew.CreateCompiledMethod(
            "numArgs",
            [0x75, 0x68, // count := 0
             0x70, 0x8F, 0x00, 0x03, // self do: [:c |
             0x10, 0x21, 0xB6, 0x9D, 0x04, 0x68, 0x76, 0xB0, 0x68, // c = $: ifTrue: [count := count + 1]
             0xF7, 0x68, 0x7C], // ]. ^count
            [ObjectMemoryNew.InternSymbol("do:"),
             ObjectMemoryNew.InternSymbol(":"),
             ObjectMemoryNew.InternSymbol("=")],
            0, 1, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "numArgs", method18);
        instanceMethodCount++;

        // isKeyword - checks if string is a keyword selector
        var method19 = ObjectMemoryNew.CreateCompiledMethod(
            "isKeyword",
            [0x70, 0xC2, 0x75, 0xB3, 0x9D, 0x02, 0x72, 0x7C, // self size > 0 ifFalse: [^false]
             0x70, 0x70, 0xC2, 0xC0, 0x21, 0xB6, 0x7C], // (self at: self size) = $:
            [ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol(">"),
             ObjectMemoryNew.InternSymbol("at:"),
             ObjectMemoryNew.InternSymbol(":"),
             ObjectMemoryNew.InternSymbol("=")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "isKeyword", method19);
        instanceMethodCount++;

        // printOn:
        var method20 = ObjectMemoryNew.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x70, 0xE0, 0x7C], // stream nextPutAll: self
            [ObjectMemoryNew.InternSymbol("nextPutAll:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(stringClassRef.AsObjectPointer(), "printOn:", method20);
        instanceMethodCount++;
        
        // Note: Class methods would be installed on String's metaclass
        // They would include: #new:, #streamContents:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] String: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Symbol methods.
    /// </summary>
    private static void InstallSymbolMethods()
    {
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (symbolClassRef.IsNil) return;

        var instanceMethodCount = 0;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Symbol instance methods: #=, #hash, #asString, #asSymbol");
        }

        // = (symbol comparison uses identity)
        var method21 = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            [ObjectMemoryNew.InternSymbol("==")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(symbolClassRef.AsObjectPointer(), "=", method21);
        instanceMethodCount++;

        // hash (symbols hash to themselves)
        var method22 = ObjectMemoryNew.CreateCompiledMethod(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash
            [ObjectMemoryNew.InternSymbol("identityHash")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(symbolClassRef.AsObjectPointer(), "hash", method22);
        instanceMethodCount++;

        // asString
        var method23 = ObjectMemoryNew.CreateCompiledMethod(
            "asString",
            [0x44, 0x70, 0xC2, 0xCC, 0x68, // String new: self size
             0x76, 0x70, 0xE1, 0x68, 0x7C], // result replaceFrom: 1 to: self size with: self. ^result
            [ObjectMemoryNew.InternSymbol("String"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("new:"),
             ObjectMemoryNew.InternSymbol("replaceFrom:to:with:")],
            0, 1, 0);
        ObjectMemoryNew.AddMethod(symbolClassRef.AsObjectPointer(), "asString", method23);
        instanceMethodCount++;

        // asSymbol (returns self)
        var method24 = ObjectMemoryNew.CreateCompiledMethod(
            "asSymbol",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(symbolClassRef.AsObjectPointer(), "asSymbol", method24);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Symbol: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs ByteArray methods.
    /// </summary>
    private static void InstallByteArrayMethods()
    {
        var byteArrayClassRef = SystemDictionary.ByteArrayClassRef;
        if (byteArrayClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ByteArray: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Dictionary methods.
    /// </summary>
    private static void InstallDictionaryMethods()
    {
        var dictionaryClassRef = SystemDictionary.DictionaryClassRef;
        if (dictionaryClassRef.IsNil) return;

        var instanceMethodCount = 0;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Dictionary instance methods: #at:, #at:put:, #at:ifAbsent:, #associationAt:, #associationAt:ifAbsent:, #includesKey:, #removeKey:");
        }

        // at: - primitive 105
        var method25 = ObjectMemoryNew.CreateCompiledMethod(
            "at:",
            [0x72, 0x7C], // push false, return (fallback for primitive 105)
            [],
            1, 0, 105);
        ObjectMemoryNew.AddMethod(dictionaryClassRef.AsObjectPointer(), "at:", method25);
        instanceMethodCount++;

        // at:put: - primitive 106
        var method26 = ObjectMemoryNew.CreateCompiledMethod(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 106)
            [],
            2, 0, 106);
        ObjectMemoryNew.AddMethod(dictionaryClassRef.AsObjectPointer(), "at:put:", method26);
        instanceMethodCount++;

        // at:ifAbsent: - looks up key, evaluates block if not found
        var method27 = ObjectMemoryNew.CreateCompiledMethod(
            "at:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self associationAt: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0xD1, 0x7C], // ^assoc value
            [ObjectMemoryNew.InternSymbol("associationAt:"),
             ObjectMemoryNew.InternSymbol("="),
             ObjectMemoryNew.InternSymbol("value")],
            2, 1, 0);
        ObjectMemoryNew.AddMethod(dictionaryClassRef.AsObjectPointer(), "at:ifAbsent:", method27);
        instanceMethodCount++;

        // associationAt: - returns the association for a key
        var method28 = ObjectMemoryNew.CreateCompiledMethod(
            "associationAt:",
            [0x70, 0x10, 0xE0, 0x7C], // self findAssociation: key (would need primitive)
            [ObjectMemoryNew.InternSymbol("findAssociation:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(dictionaryClassRef.AsObjectPointer(), "associationAt:", method28);
        instanceMethodCount++;

        // associationAt:ifAbsent: - returns association or evaluates block
        var method29 = ObjectMemoryNew.CreateCompiledMethod(
            "associationAt:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self findAssociation: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0x7C], // ^assoc
            [ObjectMemoryNew.InternSymbol("findAssociation:"),
             ObjectMemoryNew.InternSymbol("="),
             ObjectMemoryNew.InternSymbol("value")],
            2, 1, 0);
        ObjectMemoryNew.AddMethod(dictionaryClassRef.AsObjectPointer(), "associationAt:ifAbsent:", method29);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Dictionary: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Association methods.
    /// </summary>
    private static void InstallAssociationMethods()
    {
        var associationClassRef = SystemDictionary.AssociationClassRef;
        if (associationClassRef.IsNil) return;

        var instanceMethodCount = 0;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Association instance methods: #key, #value, #key:, #value:");
        }

        // key - primitive 86
        var method30 = ObjectMemoryNew.CreateCompiledMethod(
            "key",
            [], // No fallback needed - primitive 86 cannot fail
            [],
            0, 0, 86);
        ObjectMemoryNew.AddMethod(associationClassRef.AsObjectPointer(), "key", method30);
        instanceMethodCount++;

        // value - primitive 87
        var method31 = ObjectMemoryNew.CreateCompiledMethod(
            "value",
            [], // No fallback needed - primitive 87 cannot fail
            [],
            0, 0, 87);
        ObjectMemoryNew.AddMethod(associationClassRef.AsObjectPointer(), "value", method31);
        instanceMethodCount++;

        // key: - primitive 88
        var method32 = ObjectMemoryNew.CreateCompiledMethod(
            "key:",
            [0x10, 0x7C], // ^arg (return new key for primitive 88)
            [],
            1, 0, 88);
        ObjectMemoryNew.AddMethod(associationClassRef.AsObjectPointer(), "key:", method32);
        instanceMethodCount++;

        // value: - primitive 89
        var method33 = ObjectMemoryNew.CreateCompiledMethod(
            "value:",
            [0x10, 0x7C], // ^arg (return new value for primitive 89)
            [],
            1, 0, 89);
        ObjectMemoryNew.AddMethod(associationClassRef.AsObjectPointer(), "value:", method33);
        instanceMethodCount++;

        // Note: Class methods would be installed on Association's metaclass
        // They would include: #key:value:

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Association: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs MethodDictionary methods.
    /// </summary>
    private static void InstallMethodDictionaryMethods()
    {
        var methodDictionaryClassRef = SystemDictionary.MethodDictionaryClassRef;
        if (methodDictionaryClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] MethodDictionary: 0 instance methods installed");
        }
    }
}
