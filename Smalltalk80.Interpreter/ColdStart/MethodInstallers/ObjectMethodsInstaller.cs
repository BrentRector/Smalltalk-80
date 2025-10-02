namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

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
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "==",
            [0x72, 0x7C], // push false, return (fallback for primitive 110)
            [],
            1, 0, 110);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "==", method1);
        instanceMethodCount++;

        // ~~ (identity not equal)
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "~~",
            [0x70, 0x10, 0xB6, 0x9C, 0x7C], // self == arg not
            [ObjectMemoryNew.InternSymbol("==")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "~~", method2);
        instanceMethodCount++;

        // = (equality) - defaults to ==
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            [ObjectMemoryNew.InternSymbol("==")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "=", method3);
        instanceMethodCount++;

        // ~= (not equal)
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "~=",
            [0x70, 0x10, 0xC0, 0x9C, 0x7C], // self = arg not
            [ObjectMemoryNew.InternSymbol("=")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "~=", method4);
        instanceMethodCount++;

        // hash - primitive 75
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash (fallback for primitive 75)
            [ObjectMemoryNew.InternSymbol("identityHash")],
            0, 0, 75);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "hash", method5);
        instanceMethodCount++;

        // class - primitive 111
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "class",
            [], // No fallback needed - primitive 111 cannot fail
            [],
            0, 0, 111);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "class", method6);
        instanceMethodCount++;

        // yourself
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "yourself",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "yourself", method7);
        instanceMethodCount++;

        // isNil
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "isNil",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "isNil", method8);
        instanceMethodCount++;

        // notNil
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "notNil",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "notNil", method9);
        instanceMethodCount++;

        // ifNil:
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "ifNil:",
            [0x73, 0x7C], // ^nil (not nil, so return nil)
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "ifNil:", method10);
        instanceMethodCount++;

        // ifNotNil:
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "ifNotNil:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "ifNotNil:", method11);
        instanceMethodCount++;

        // copy
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "copy",
            [0x70, 0xD0, 0x7C], // self shallowCopy
            [ObjectMemoryNew.InternSymbol("shallowCopy")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "copy", method12);
        instanceMethodCount++;

        // shallowCopy - primitive 148
        var method13 = ObjectMemoryNew.CreateCompiledMethod(
            "shallowCopy",
            [0x70, 0x7C], // ^self (fallback for primitive 148)
            [],
            0, 0, 148);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "shallowCopy", method13);
        instanceMethodCount++;
        
        // isKindOf: - primitive 9 (special object-testing primitive)
        var method14 = ObjectMemoryNew.CreateCompiledMethod(
            "isKindOf:",
            [0x70, 0xD0, 0x68, // self class
             0x10, 0x69, 0xB6, 0x9A, 0x02, 0x71, 0x7C, // aClass == self class ifTrue: [^true]
             0x69, 0xD1, 0x69, // aClass := aClass superclass
             0x73, 0xB6, 0x9A, 0x02, 0x72, 0x7C, // aClass == nil ifTrue: [^false]
             0xA3, 0xED], // jump back to check
            [ObjectMemoryNew.InternSymbol("class"),
             ObjectMemoryNew.InternSymbol("=="),
             ObjectMemoryNew.InternSymbol("superclass")],
            1, 1, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "isKindOf:", method14);
        instanceMethodCount++;

        // isMemberOf:
        var method15 = ObjectMemoryNew.CreateCompiledMethod(
            "isMemberOf:",
            [0x70, 0xD0, 0x10, 0xB6, 0x7C], // self class == aClass
            [ObjectMemoryNew.InternSymbol("class"),
             ObjectMemoryNew.InternSymbol("==")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "isMemberOf:", method15);
        instanceMethodCount++;

        // respondsTo:
        var method16 = ObjectMemoryNew.CreateCompiledMethod(
            "respondsTo:",
            [0x70, 0xD0, 0x10, 0xE1, 0x73, 0xF0, 0x7C], // self class canUnderstand: aSymbol
            [ObjectMemoryNew.InternSymbol("class"),
             ObjectMemoryNew.InternSymbol("canUnderstand:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "respondsTo:", method16);
        instanceMethodCount++;

        // printString
        var method17 = ObjectMemoryNew.CreateCompiledMethod(
            "printString",
            [0x42, 0xD1, 0x68, 0x70, 0x11, 0xE2, 0x10, 0x7C], // String streamContents: [:s | self printOn: s]
            [ObjectMemoryNew.InternSymbol("String"),
             ObjectMemoryNew.InternSymbol("streamContents:"),
             ObjectMemoryNew.InternSymbol("printOn:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "printString", method17);
        instanceMethodCount++;

        // error: - primitive 131
        var method18 = ObjectMemoryNew.CreateCompiledMethod(
            "error:",
            [0x70, 0x10, 0xE0, 0x7C], // self primitiveFailed: errorString (fallback for primitive 131)
            [ObjectMemoryNew.InternSymbol("primitiveFailed")],
            1, 0, 131);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "error:", method18);
        instanceMethodCount++;

        // halt - primitive 123
        var method19 = ObjectMemoryNew.CreateCompiledMethod(
            "halt",
            [0x70, 0x7C], // ^self (fallback for primitive 123)
            [],
            0, 0, 123);
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "halt", method19);
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
        var method20 = ObjectMemoryNew.CreateCompiledMethod(
            "isNil",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "isNil", method20);
        instanceMethodCount++;

        // notNil
        var method21 = ObjectMemoryNew.CreateCompiledMethod(
            "notNil",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "notNil", method21);
        instanceMethodCount++;

        // ifNil:
        var method22 = ObjectMemoryNew.CreateCompiledMethod(
            "ifNil:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "ifNil:", method22);
        instanceMethodCount++;

        // ifNotNil:
        var method23 = ObjectMemoryNew.CreateCompiledMethod(
            "ifNotNil:",
            [0x73, 0x7C], // ^nil (is nil, so return nil)
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "ifNotNil:", method23);
        instanceMethodCount++;

        // ifNil:ifNotNil:
        var method24 = ObjectMemoryNew.CreateCompiledMethod(
            "ifNil:ifNotNil:",
            [0x10, 0xC9, 0x7C], // nilBlock value
            [ObjectMemoryNew.InternSymbol("value")],
            2, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "ifNil:ifNotNil:", method24);
        instanceMethodCount++;

        // printOn:
        var method25 = ObjectMemoryNew.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'nil'
            [ObjectMemoryNew.InternSymbol("nil"), ObjectMemoryNew.InternSymbol("nextPutAll:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(undefinedObjectClassRef.AsObjectPointer(), "printOn:", method25);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] UndefinedObject: {instanceMethodCount} instance methods installed");
        }
    }
}
