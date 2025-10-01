namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

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
        arrayClassRef.AddMethod("at:", SCompiledMethod.CreateCompiledMethodRef(
            "at:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 60)
            [],
            1, 0, 60));
        instanceMethodCount++;
        
        // at:put: - primitive 61
        arrayClassRef.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethodRef(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 61)
            [],
            2, 0, 61));
        instanceMethodCount++;
        
        // size - primitive 62
        arrayClassRef.AddMethod("size", SCompiledMethod.CreateCompiledMethodRef(
            "size",
            [], // No fallback needed - primitive 62 cannot fail
            [],
            0, 0, 62));
        instanceMethodCount++;
        
        // do: block iteration
        arrayClassRef.AddMethod("do:", SCompiledMethod.CreateCompiledMethodRef(
            "do:",
            [0x76, 0x69, 0x70, 0xC2, 0x6A, 0xB5, 0xAC, 0x0B, 0x70, 0x6A, 0xC0, 0x10, 0xE6, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF2, 0x73, 0x7C],
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("size"), SSymbol.Intern("at:"), SSymbol.Intern("value:")),
            1, 2, 0));
        instanceMethodCount++;
        
        // isEmpty
        arrayClassRef.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethodRef(
            "isEmpty",
            [0x70, 0xC2, 0x75, 0xB6, 0x7C], // self size = 0
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("size")),
            0, 0, 0));
        instanceMethodCount++;
        
        // first
        arrayClassRef.AddMethod("first", SCompiledMethod.CreateCompiledMethodRef(
            "first",
            [0x70, 0x76, 0xC0, 0x7C], // self at: 1
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("at:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // last
        arrayClassRef.AddMethod("last", SCompiledMethod.CreateCompiledMethodRef(
            "last",
            [0x70, 0x70, 0xC2, 0xC0, 0x7C], // self at: self size
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("at:"), SSymbol.Intern("size")),
            0, 0, 0));
        instanceMethodCount++;
        
        // atAll:put: - sets multiple elements to same value
        arrayClassRef.AddMethod("atAll:put:", SCompiledMethod.CreateCompiledMethodRef(
            "atAll:put:",
            [0x10, 0x8F, 0x00, 0x03, 0x70, 0x12, 0x11, 0xE1, 0xF7, 0x70, 0x7C], // indices do: [:i | self at: i put: value]. ^self
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("do:"),
                SSymbol.Intern("at:put:")),
            2, 0, 0));
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
        stringClassRef.AddMethod("at:", SCompiledMethod.CreateCompiledMethodRef(
            "at:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 63)
            [],
            1, 0, 63));
        instanceMethodCount++;
        
        // at:put: - primitive 64
        stringClassRef.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethodRef(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 64)
            [],
            2, 0, 64));
        instanceMethodCount++;
        
        // size - primitive 62
        stringClassRef.AddMethod("size", SCompiledMethod.CreateCompiledMethodRef(
            "size",
            [], // No fallback needed - primitive 62 cannot fail
            [],
            0, 0, 62));
        instanceMethodCount++;
        
        // = string comparison - primitive 105
        stringClassRef.AddMethod("=", SCompiledMethod.CreateCompiledMethodRef(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 105)
            [],
            1, 0, 105));
        instanceMethodCount++;
        
        // hash - primitive 106
        stringClassRef.AddMethod("hash", SCompiledMethod.CreateCompiledMethodRef(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash (fallback for primitive 106)
            [],
            0, 0, 106));
        instanceMethodCount++;
        
        // , (concatenation)
        stringClassRef.AddMethod(",", SCompiledMethod.CreateCompiledMethodRef(
            ",",
            [0x70, 0xC2, 0x10, 0xC2, 0xB0, 0x68, 0x44, 0xCC, 0x69, 0x76, 0x70, 0xC2, 0xE8, 0x6B,
             0x76, 0x10, 0xC2, 0xE8, 0x69, 0x7C], // Implementation of string concatenation
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("size"),
                SSymbol.Intern("String"),
                SSymbol.Intern("new:"),
                SSymbol.Intern("replaceFrom:to:with:")),
            1, 2, 0));
        instanceMethodCount++;
        
        // asSymbol - primitive 110
        stringClassRef.AddMethod("asSymbol", SCompiledMethod.CreateCompiledMethodRef(
            "asSymbol",
            [], // No fallback needed - primitive 110 cannot fail
            [],
            0, 0, 110));
        instanceMethodCount++;
        
        // asString (returns self for String)
        stringClassRef.AddMethod("asString", SCompiledMethod.CreateCompiledMethodRef(
            "asString",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // keywords - extracts keyword selectors from a string
        stringClassRef.AddMethod("keywords", SCompiledMethod.CreateCompiledMethodRef(
            "keywords",
            [0x41, 0xD0, 0x68, // OrderedCollection new
             0x76, 0x69, 0x6A, // index := 1, start := 1
             0x6A, 0x70, 0xC2, 0xB5, 0xAC, 0x20, // [index <= self size] whileTrue:
             0x70, 0x6A, 0xC0, 0x21, 0xE1, 0x9D, 0x0E, // (self at: index) = $: ifTrue:
             0x6B, 0x70, 0x6A, 0x76, 0xB1, 0x6B, 0xE2, 0xD2, // add from start to index
             0x6A, 0x76, 0xB0, 0x6B, 0x69, // index := index + 1, start := index
             0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xDF, // index := index + 1, loop
             0x68, 0x7C], // ^result
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("OrderedCollection"),
                SSymbol.Intern("new"),
                SSymbol.Intern("size"),
                SSymbol.Intern("<="),
                SSymbol.Intern("at:"),
                SSymbol.Intern(":"),
                SSymbol.Intern("copyFrom:to:"),
                SSymbol.Intern("add:")),
            0, 3, 0));
        instanceMethodCount++;
        
        // numArgs - counts the number of arguments in a selector
        stringClassRef.AddMethod("numArgs", SCompiledMethod.CreateCompiledMethodRef(
            "numArgs",
            [0x75, 0x68, // count := 0
             0x70, 0x8F, 0x00, 0x03, // self do: [:c |
             0x10, 0x21, 0xB6, 0x9D, 0x04, 0x68, 0x76, 0xB0, 0x68, // c = $: ifTrue: [count := count + 1]
             0xF7, 0x68, 0x7C], // ]. ^count
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("do:"),
                SSymbol.Intern(":"),
                SSymbol.Intern("=")),
            0, 1, 0));
        instanceMethodCount++;
        
        // isKeyword - checks if string is a keyword selector
        stringClassRef.AddMethod("isKeyword", SCompiledMethod.CreateCompiledMethodRef(
            "isKeyword",
            [0x70, 0xC2, 0x75, 0xB3, 0x9D, 0x02, 0x72, 0x7C, // self size > 0 ifFalse: [^false]
             0x70, 0x70, 0xC2, 0xC0, 0x21, 0xB6, 0x7C], // (self at: self size) = $:
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("size"),
                SSymbol.Intern(">"),
                SSymbol.Intern("at:"),
                SSymbol.Intern(":"),
                SSymbol.Intern("=")),
            0, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        stringClassRef.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethodRef(
            "printOn:",
            [0x10, 0x70, 0xE0, 0x7C], // stream nextPutAll: self
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
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
        symbolClassRef.AddMethod("=", SCompiledMethod.CreateCompiledMethodRef(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // hash (symbols hash to themselves)
        symbolClassRef.AddMethod("hash", SCompiledMethod.CreateCompiledMethodRef(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("identityHash")),
            0, 0, 0));
        instanceMethodCount++;
        
        // asString
        symbolClassRef.AddMethod("asString", SCompiledMethod.CreateCompiledMethodRef(
            "asString",
            [0x44, 0x70, 0xC2, 0xCC, 0x68, // String new: self size
             0x76, 0x70, 0xE1, 0x68, 0x7C], // result replaceFrom: 1 to: self size with: self. ^result
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("String"),
                SSymbol.Intern("size"),
                SSymbol.Intern("new:"),
                SSymbol.Intern("replaceFrom:to:with:")),
            0, 1, 0));
        instanceMethodCount++;
        
        // asSymbol (returns self)
        symbolClassRef.AddMethod("asSymbol", SCompiledMethod.CreateCompiledMethodRef(
            "asSymbol",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0));
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
        dictionaryClassRef.AddMethod("at:", SCompiledMethod.CreateCompiledMethodRef(
            "at:",
            [0x72, 0x7C], // push false, return (fallback for primitive 105)
            [],
            1, 0, 105));
        instanceMethodCount++;
        
        // at:put: - primitive 106
        dictionaryClassRef.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethodRef(
            "at:put:",
            [0x11, 0x7C], // ^arg2 (return value for primitive 106)
            [],
            2, 0, 106));
        instanceMethodCount++;
        
        // at:ifAbsent: - looks up key, evaluates block if not found
        dictionaryClassRef.AddMethod("at:ifAbsent:", SCompiledMethod.CreateCompiledMethodRef(
            "at:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self associationAt: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0xD1, 0x7C], // ^assoc value
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("associationAt:"),
                SSymbol.Intern("="),
                SSymbol.Intern("value")),
            2, 1, 0));
        instanceMethodCount++;
        
        // associationAt: - returns the association for a key
        dictionaryClassRef.AddMethod("associationAt:", SCompiledMethod.CreateCompiledMethodRef(
            "associationAt:",
            [0x70, 0x10, 0xE0, 0x7C], // self findAssociation: key (would need primitive)
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("findAssociation:")),
            1, 0, 0));
        instanceMethodCount++;
        
        // associationAt:ifAbsent: - returns association or evaluates block
        dictionaryClassRef.AddMethod("associationAt:ifAbsent:", SCompiledMethod.CreateCompiledMethodRef(
            "associationAt:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self findAssociation: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0x7C], // ^assoc
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("findAssociation:"),
                SSymbol.Intern("="),
                SSymbol.Intern("value")),
            2, 1, 0));
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
        associationClassRef.AddMethod("key", SCompiledMethod.CreateCompiledMethodRef(
            "key",
            [], // No fallback needed - primitive 86 cannot fail
            [],
            0, 0, 86));
        instanceMethodCount++;
        
        // value - primitive 87
        associationClassRef.AddMethod("value", SCompiledMethod.CreateCompiledMethodRef(
            "value",
            [], // No fallback needed - primitive 87 cannot fail
            [],
            0, 0, 87));
        instanceMethodCount++;
        
        // key: - primitive 88
        associationClassRef.AddMethod("key:", SCompiledMethod.CreateCompiledMethodRef(
            "key:",
            [0x10, 0x7C], // ^arg (return new key for primitive 88)
            [],
            1, 0, 88));
        instanceMethodCount++;
        
        // value: - primitive 89
        associationClassRef.AddMethod("value:", SCompiledMethod.CreateCompiledMethodRef(
            "value:",
            [0x10, 0x7C], // ^arg (return new value for primitive 89)
            [],
            1, 0, 89));
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
