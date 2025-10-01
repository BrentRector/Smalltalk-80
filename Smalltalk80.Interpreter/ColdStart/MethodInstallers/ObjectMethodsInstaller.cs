namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Installs kernel methods for Object and UndefinedObject classes.
/// </summary>
public static class ObjectMethodsInstaller
{
    /// <summary>
    /// Installs all Object and UndefinedObject methods.
    /// </summary>
    public static void Install()
    {
        InstallObjectMethods();
        InstallUndefinedObjectMethods();
    }
    
    /// <summary>
    /// Installs Object instance and class methods.
    /// </summary>
    private static void InstallObjectMethods()
    {
        var objectClassRef = SystemDictionary.ObjectClassRef;
        if (objectClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Object instance methods: #==, #~~, #=, #~=, #hash, #class, #yourself, #isNil, #notNil, #ifNil:, #ifNotNil:, #isKindOf:, #isMemberOf:, #respondsTo:, #perform:, #perform:with:, #error:, #doesNotUnderstand:, #halt, #subclassResponsibility, #shouldNotImplement, #printString, #printOn:, #copy, #shallowCopy, #deepCopy");
        }
        
        // == (identity comparison) - primitive 110
        objectClassRef.AddMethod("==", SCompiledMethod.CreateCompiledMethodRef(
            "==",
            [0x72, 0x7C], // push false, return (fallback for primitive 110)
            [],
            1, 0, 110));
        instanceMethodCount++;
        
        // ~~ (identity not equal)
        objectClassRef.AddMethod("~~", SCompiledMethod.CreateCompiledMethodRef(
            "~~",
            [0x70, 0x10, 0xB6, 0x9C, 0x7C], // self == arg not
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // = (equality) - defaults to ==
        objectClassRef.AddMethod("=", SCompiledMethod.CreateCompiledMethodRef(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ~= (not equal)
        objectClassRef.AddMethod("~=", SCompiledMethod.CreateCompiledMethodRef(
            "~=",
            [0x70, 0x10, 0xC0, 0x9C, 0x7C], // self = arg not
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("=")),
            1, 0, 0));
        instanceMethodCount++;
        
        // hash - primitive 75
        objectClassRef.AddMethod("hash", SCompiledMethod.CreateCompiledMethodRef(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash (fallback for primitive 75)
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("identityHash")),
            0, 0, 75));
        instanceMethodCount++;
        
        // class - primitive 111
        objectClassRef.AddMethod("class", SCompiledMethod.CreateCompiledMethodRef(
            "class",
            [], // No fallback needed - primitive 111 cannot fail
            [],
            0, 0, 111));
        instanceMethodCount++;
        
        // yourself
        objectClassRef.AddMethod("yourself", SCompiledMethod.CreateCompiledMethodRef(
            "yourself",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // isNil
        objectClassRef.AddMethod("isNil", SCompiledMethod.CreateCompiledMethodRef(
            "isNil",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // notNil
        objectClassRef.AddMethod("notNil", SCompiledMethod.CreateCompiledMethodRef(
            "notNil",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // ifNil:
        objectClassRef.AddMethod("ifNil:", SCompiledMethod.CreateCompiledMethodRef(
            "ifNil:",
            [0x73, 0x7C], // ^nil (not nil, so return nil)
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNotNil:
        objectClassRef.AddMethod("ifNotNil:", SCompiledMethod.CreateCompiledMethodRef(
            "ifNotNil:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // copy
        objectClassRef.AddMethod("copy", SCompiledMethod.CreateCompiledMethodRef(
            "copy",
            [0x70, 0xD0, 0x7C], // self shallowCopy
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("shallowCopy")),
            0, 0, 0));
        instanceMethodCount++;
        
        // shallowCopy - primitive 148
        objectClassRef.AddMethod("shallowCopy", SCompiledMethod.CreateCompiledMethodRef(
            "shallowCopy",
            [0x70, 0x7C], // ^self (fallback for primitive 148)
            [],
            0, 0, 148));
        instanceMethodCount++;
        
        // isKindOf: - primitive 9 (special object-testing primitive)
        objectClassRef.AddMethod("isKindOf:", SCompiledMethod.CreateCompiledMethodRef(
            "isKindOf:",
            [0x70, 0xD0, 0x68, // self class
             0x10, 0x69, 0xB6, 0x9A, 0x02, 0x71, 0x7C, // aClass == self class ifTrue: [^true]
             0x69, 0xD1, 0x69, // aClass := aClass superclass
             0x73, 0xB6, 0x9A, 0x02, 0x72, 0x7C, // aClass == nil ifTrue: [^false]
             0xA3, 0xED], // jump back to check
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("class"),
                SSymbol.Intern("=="),
                SSymbol.Intern("superclass")),
            1, 1, 0));
        instanceMethodCount++;
        
        // isMemberOf:
        objectClassRef.AddMethod("isMemberOf:", SCompiledMethod.CreateCompiledMethodRef(
            "isMemberOf:",
            [0x70, 0xD0, 0x10, 0xB6, 0x7C], // self class == aClass
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("class"),
                SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // respondsTo:
        objectClassRef.AddMethod("respondsTo:", SCompiledMethod.CreateCompiledMethodRef(
            "respondsTo:",
            [0x70, 0xD0, 0x10, 0xE1, 0x73, 0xF0, 0x7C], // self class canUnderstand: aSymbol
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("class"),
                SSymbol.Intern("canUnderstand:")),
            1, 0, 0));
        instanceMethodCount++;
        
        // printString
        objectClassRef.AddMethod("printString", SCompiledMethod.CreateCompiledMethodRef(
            "printString",
            [0x42, 0xD1, 0x68, 0x70, 0x11, 0xE2, 0x10, 0x7C], // String streamContents: [:s | self printOn: s]
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("String"),
                SSymbol.Intern("streamContents:"),
                SArray.From(SSymbol.Intern("printOn:"))),
            0, 0, 0));
        instanceMethodCount++;
        
        // error: - primitive 131
        objectClassRef.AddMethod("error:", SCompiledMethod.CreateCompiledMethodRef(
            "error:",
            [0x70, 0x10, 0xE0, 0x7C], // self primitiveFailed: errorString (fallback for primitive 131)
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("primitiveFailed")),
            1, 0, 131));
        instanceMethodCount++;
        
        // halt - primitive 123
        objectClassRef.AddMethod("halt", SCompiledMethod.CreateCompiledMethodRef(
            "halt",
            [0x70, 0x7C], // ^self (fallback for primitive 123)
            [],
            0, 0, 123));
        instanceMethodCount++;
        
        // Note: Class methods would normally be installed on the metaclass
        // Since this implementation doesn't have full metaclass support,
        // we can't install class methods yet. They would include:
        // #new, #basicNew, #basicNew:, #initialize
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Object: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs UndefinedObject methods (for nil).
    /// </summary>
    private static void InstallUndefinedObjectMethods()
    {
        var undefinedObjectClassRef = SystemDictionary.UndefinedObjectClassRef;
        if (undefinedObjectClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing UndefinedObject instance methods: #isNil, #notNil, #ifNil:, #ifNotNil:, #ifNil:ifNotNil:, #printOn:");
        }
        
        // isNil
        undefinedObjectClassRef.AddMethod("isNil", SCompiledMethod.CreateCompiledMethodRef(
            "isNil",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // notNil
        undefinedObjectClassRef.AddMethod("notNil", SCompiledMethod.CreateCompiledMethodRef(
            "notNil",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // ifNil:
        undefinedObjectClassRef.AddMethod("ifNil:", SCompiledMethod.CreateCompiledMethodRef(
            "ifNil:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNotNil:
        undefinedObjectClassRef.AddMethod("ifNotNil:", SCompiledMethod.CreateCompiledMethodRef(
            "ifNotNil:",
            [0x73, 0x7C], // ^nil (is nil, so return nil)
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNil:ifNotNil:
        undefinedObjectClassRef.AddMethod("ifNil:ifNotNil:", SCompiledMethod.CreateCompiledMethodRef(
            "ifNil:ifNotNil:",
            [0x10, 0xC9, 0x7C], // nilBlock value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        undefinedObjectClassRef.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethodRef(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'nil'
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("nil"), SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] UndefinedObject: {instanceMethodCount} instance methods installed");
        }
    }
}
