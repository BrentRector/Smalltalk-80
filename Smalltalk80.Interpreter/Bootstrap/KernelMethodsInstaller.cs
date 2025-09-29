namespace Smalltalk80.Interpreter.Bootstrap;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Installs core kernel methods during cold start for all system classes.
/// These are the fundamental methods needed for a functioning Smalltalk system.
/// </summary>
public static class KernelMethodsInstaller
{
    /// <summary>
    /// Installs all kernel methods for system classes.
    /// </summary>
    public static void InstallAll()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing kernel methods for all system classes...");
        }

        // Install methods for each class in hierarchical order
        InstallObjectMethods();
        InstallBehaviorMethods();
        InstallClassDescriptionMethods();
        InstallClassMethods();
        InstallMetaclassMethods();
        InstallUndefinedObjectMethods();
        InstallBooleanMethods();
        InstallTrueMethods();
        InstallFalseMethods();
        InstallMagnitudeMethods();
        InstallNumberMethods();
        InstallIntegerMethods();
        InstallSmallIntegerMethods();
        InstallFloatMethods();
        InstallCharacterMethods();
        InstallStringMethods();
        InstallSymbolMethods();
        InstallArrayMethods();
        InstallByteArrayMethods();
        InstallCompiledMethodMethods();
        InstallMethodDictionaryMethods();
        InstallAssociationMethods();
        InstallDictionaryMethods();
        InstallLinkMethods();
        InstallProcessMethods();
        InstallLinkedListMethods();
        InstallSemaphoreMethods();
        InstallProcessorSchedulerMethods();
        InstallStreamMethods();
        InstallPositionableStreamMethods();
        InstallWriteStreamMethods();
        InstallReadStreamMethods();
        InstallContextPartMethods();
        InstallMethodContextMethods();
        InstallBlockContextMethods();
        
        // Install class-side methods for compilation support
        InstallClassSideMethods();
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Kernel methods installation complete");
        }
    }
    
    /// <summary>
    /// Installs Object instance and class methods.
    /// </summary>
    private static void InstallObjectMethods()
    {
        var objectClass = SystemDictionary.ObjectClass;
        if (objectClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Object instance methods: #==, #~~, #=, #~=, #hash, #class, #yourself, #isNil, #notNil, #ifNil:, #ifNotNil:, #isKindOf:, #isMemberOf:, #respondsTo:, #perform:, #perform:with:, #error:, #doesNotUnderstand:, #halt, #subclassResponsibility, #shouldNotImplement, #printString, #printOn:, #copy, #shallowCopy, #deepCopy");
        }
        
        // == (identity comparison) - primitive 110
        objectClass.AddMethod("==", SCompiledMethod.CreateCompiledMethod(
            "==",
            [0x8F, 0x6E, 0x01, 0x70, 0x71, 0xC6, 0x7C], // Primitive 110 with fallback
            new SArray(),
            1, 0, 110));
        instanceMethodCount++;
        
        // ~~ (identity not equal)
        objectClass.AddMethod("~~", SCompiledMethod.CreateCompiledMethod(
            "~~",
            [0x70, 0x10, 0xB6, 0x9C, 0x7C], // self == arg not
            SArray.From(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // = (equality) - defaults to ==
        objectClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            SArray.From(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ~= (not equal)
        objectClass.AddMethod("~=", SCompiledMethod.CreateCompiledMethod(
            "~=",
            [0x70, 0x10, 0xC0, 0x9C, 0x7C], // self = arg not
            SArray.From(SSymbol.Intern("=")),
            1, 0, 0));
        instanceMethodCount++;
        
        // hash - primitive 75
        objectClass.AddMethod("hash", SCompiledMethod.CreateCompiledMethod(
            "hash",
            [0x8F, 0x4B, 0x00, 0x70, 0xC8, 0x7C], // Primitive 75 with fallback
            SArray.From(SSymbol.Intern("identityHash")),
            0, 0, 75));
        instanceMethodCount++;
        
        // class - primitive 111
        objectClass.AddMethod("class", SCompiledMethod.CreateCompiledMethod(
            "class",
            [0x8F, 0x6F, 0x00, 0x7C], // Primitive 111
            new SArray(),
            0, 0, 111));
        instanceMethodCount++;
        
        // yourself
        objectClass.AddMethod("yourself", SCompiledMethod.CreateCompiledMethod(
            "yourself",
            [0x70, 0x7C], // ^self
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // isNil
        objectClass.AddMethod("isNil", SCompiledMethod.CreateCompiledMethod(
            "isNil",
            [0x72, 0x7C], // ^false
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // notNil
        objectClass.AddMethod("notNil", SCompiledMethod.CreateCompiledMethod(
            "notNil",
            [0x71, 0x7C], // ^true
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // ifNil:
        objectClass.AddMethod("ifNil:", SCompiledMethod.CreateCompiledMethod(
            "ifNil:",
            [0x73, 0x7C], // ^nil (not nil, so return nil)
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNotNil:
        objectClass.AddMethod("ifNotNil:", SCompiledMethod.CreateCompiledMethod(
            "ifNotNil:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // copy
        objectClass.AddMethod("copy", SCompiledMethod.CreateCompiledMethod(
            "copy",
            [0x70, 0xD0, 0x7C], // self shallowCopy
            SArray.From(SSymbol.Intern("shallowCopy")),
            0, 0, 0));
        instanceMethodCount++;
        
        // shallowCopy - primitive 148
        objectClass.AddMethod("shallowCopy", SCompiledMethod.CreateCompiledMethod(
            "shallowCopy",
            [0x8F, 0x94, 0x00, 0x70, 0x7C], // Primitive 148, fallback to self
            new SArray(),
            0, 0, 148));
        instanceMethodCount++;
        
        // isKindOf: - primitive 9 (special object-testing primitive)
        objectClass.AddMethod("isKindOf:", SCompiledMethod.CreateCompiledMethod(
            "isKindOf:",
            [0x70, 0xD0, 0x68, // self class
             0x10, 0x69, 0xB6, 0x9A, 0x02, 0x71, 0x7C, // aClass == self class ifTrue: [^true]
             0x69, 0xD1, 0x69, // aClass := aClass superclass
             0x73, 0xB6, 0x9A, 0x02, 0x72, 0x7C, // aClass == nil ifTrue: [^false]
             0xA3, 0xED], // jump back to check
            SArray.From(
                SSymbol.Intern("class"),
                SSymbol.Intern("=="),
                SSymbol.Intern("superclass")),
            1, 1, 0));
        instanceMethodCount++;
        
        // isMemberOf:
        objectClass.AddMethod("isMemberOf:", SCompiledMethod.CreateCompiledMethod(
            "isMemberOf:",
            [0x70, 0xD0, 0x10, 0xB6, 0x7C], // self class == aClass
            SArray.From(
                SSymbol.Intern("class"),
                SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // respondsTo:
        objectClass.AddMethod("respondsTo:", SCompiledMethod.CreateCompiledMethod(
            "respondsTo:",
            [0x70, 0xD0, 0x10, 0xE1, 0x73, 0xF0, 0x7C], // self class canUnderstand: aSymbol
            SArray.From(
                SSymbol.Intern("class"),
                SSymbol.Intern("canUnderstand:")),
            1, 0, 0));
        instanceMethodCount++;
        
        // printString
        objectClass.AddMethod("printString", SCompiledMethod.CreateCompiledMethod(
            "printString",
            [0x42, 0xD1, 0x68, 0x70, 0x11, 0xE2, 0x10, 0x7C], // String streamContents: [:s | self printOn: s]
            SArray.From(
                SSymbol.Intern("String"),
                SSymbol.Intern("streamContents:"),
                SArray.From(SSymbol.Intern("printOn:"))),
            0, 0, 0));
        instanceMethodCount++;
        
        // error: - primitive 131
        objectClass.AddMethod("error:", SCompiledMethod.CreateCompiledMethod(
            "error:",
            [0x8F, 0x83, 0x01, 0x70, 0x10, 0xE3, 0x7C], // Primitive 131, fallback
            SArray.From(SSymbol.Intern("primitiveFailed")),
            1, 0, 131));
        instanceMethodCount++;
        
        // halt - primitive 123
        objectClass.AddMethod("halt", SCompiledMethod.CreateCompiledMethod(
            "halt",
            [0x8F, 0x7B, 0x00, 0x70, 0x7C], // Primitive 123
            new SArray(),
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
        var undefinedObjectClass = SystemDictionary.UndefinedObjectClass;
        if (undefinedObjectClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing UndefinedObject instance methods: #isNil, #notNil, #ifNil:, #ifNotNil:, #ifNil:ifNotNil:, #printOn:");
        }
        
        // isNil
        undefinedObjectClass.AddMethod("isNil", SCompiledMethod.CreateCompiledMethod(
            "isNil",
            [0x71, 0x7C], // ^true
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // notNil
        undefinedObjectClass.AddMethod("notNil", SCompiledMethod.CreateCompiledMethod(
            "notNil",
            [0x72, 0x7C], // ^false
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // ifNil:
        undefinedObjectClass.AddMethod("ifNil:", SCompiledMethod.CreateCompiledMethod(
            "ifNil:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNotNil:
        undefinedObjectClass.AddMethod("ifNotNil:", SCompiledMethod.CreateCompiledMethod(
            "ifNotNil:",
            [0x73, 0x7C], // ^nil (is nil, so return nil)
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifNil:ifNotNil:
        undefinedObjectClass.AddMethod("ifNil:ifNotNil:", SCompiledMethod.CreateCompiledMethod(
            "ifNil:ifNotNil:",
            [0x10, 0xC9, 0x7C], // nilBlock value
            SArray.From(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        undefinedObjectClass.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'nil'
            SArray.From(SSymbol.Intern("nil"), SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] UndefinedObject: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Boolean methods.
    /// </summary>
    private static void InstallBooleanMethods()
    {
        var booleanClass = SystemDictionary.BooleanClass;
        if (booleanClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Boolean instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:");
        }
        
        // These are abstract in Boolean, implemented in True/False subclasses
        // But we'll add them as self subclassResponsibility
        
        // & (and)
        booleanClass.AddMethod("&", SCompiledMethod.CreateCompiledMethod(
            "&",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or)
        booleanClass.AddMethod("|", SCompiledMethod.CreateCompiledMethod(
            "|",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        booleanClass.AddMethod("not", SCompiledMethod.CreateCompiledMethod(
            "not",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            0, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Boolean: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs True methods.
    /// </summary>
    private static void InstallTrueMethods()
    {
        var trueClass = SystemDictionary.TrueClass;
        if (trueClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing True instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:, #printOn:");
        }
        
        // & (and) - depends on argument
        trueClass.AddMethod("&", SCompiledMethod.CreateCompiledMethod(
            "&",
            [0x10, 0x7C], // ^arg
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or) - always true
        trueClass.AddMethod("|", SCompiledMethod.CreateCompiledMethod(
            "|",
            [0x71, 0x7C], // ^true
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        trueClass.AddMethod("not", SCompiledMethod.CreateCompiledMethod(
            "not",
            [0x72, 0x7C], // ^false
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // and:
        trueClass.AddMethod("and:", SCompiledMethod.CreateCompiledMethod(
            "and:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // or:
        trueClass.AddMethod("or:", SCompiledMethod.CreateCompiledMethod(
            "or:",
            [0x71, 0x7C], // ^true
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:
        trueClass.AddMethod("ifTrue:", SCompiledMethod.CreateCompiledMethod(
            "ifTrue:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifFalse:
        trueClass.AddMethod("ifFalse:", SCompiledMethod.CreateCompiledMethod(
            "ifFalse:",
            [0x73, 0x7C], // ^nil
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:ifFalse:
        trueClass.AddMethod("ifTrue:ifFalse:", SCompiledMethod.CreateCompiledMethod(
            "ifTrue:ifFalse:",
            [0x10, 0xC9, 0x7C], // trueBlock value
            SArray.From(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        trueClass.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'true'
            SArray.From(SSymbol.Intern("true"), SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] True: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs False methods.
    /// </summary>
    private static void InstallFalseMethods()
    {
        var falseClass = SystemDictionary.FalseClass;
        if (falseClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing False instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:, #printOn:");
        }
        
        // & (and) - always false
        falseClass.AddMethod("&", SCompiledMethod.CreateCompiledMethod(
            "&",
            [0x72, 0x7C], // ^false
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or) - depends on argument
        falseClass.AddMethod("|", SCompiledMethod.CreateCompiledMethod(
            "|",
            [0x10, 0x7C], // ^arg
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        falseClass.AddMethod("not", SCompiledMethod.CreateCompiledMethod(
            "not",
            [0x71, 0x7C], // ^true
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // and:
        falseClass.AddMethod("and:", SCompiledMethod.CreateCompiledMethod(
            "and:",
            [0x72, 0x7C], // ^false
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // or:
        falseClass.AddMethod("or:", SCompiledMethod.CreateCompiledMethod(
            "or:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:
        falseClass.AddMethod("ifTrue:", SCompiledMethod.CreateCompiledMethod(
            "ifTrue:",
            [0x73, 0x7C], // ^nil
            new SArray(),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifFalse:
        falseClass.AddMethod("ifFalse:", SCompiledMethod.CreateCompiledMethod(
            "ifFalse:",
            [0x10, 0xC9, 0x7C], // arg value
            SArray.From(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:ifFalse:
        falseClass.AddMethod("ifTrue:ifFalse:", SCompiledMethod.CreateCompiledMethod(
            "ifTrue:ifFalse:",
            [0x11, 0xC9, 0x7C], // falseBlock value
            SArray.From(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        falseClass.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'false'
            SArray.From(SSymbol.Intern("false"), SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] False: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs SmallInteger methods.
    /// </summary>
    private static void InstallSmallIntegerMethods()
    {
        var smallIntegerClass = SystemDictionary.SmallIntegerClass;
        if (smallIntegerClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing SmallInteger instance methods: #+, #-, #*, #/, #//, #\\\\, #<, #>, #<=, #>=, #=, #~=, #negated, #abs, #min:, #max:, #between:and:, #to:, #to:do:, #to:by:do:, #timesRepeat:, #asFloat, #printOn:");
        }
        
        // + (addition) - primitive 1
        smallIntegerClass.AddMethod("+", SCompiledMethod.CreateCompiledMethod(
            "+",
            [0x8F, 0x01, 0x01, 0x7C], // Primitive 1
            new SArray(),
            1, 0, 1));
        instanceMethodCount++;
        
        // - (subtraction) - primitive 2
        smallIntegerClass.AddMethod("-", SCompiledMethod.CreateCompiledMethod(
            "-",
            [0x8F, 0x02, 0x01, 0x7C], // Primitive 2
            new SArray(),
            1, 0, 2));
        instanceMethodCount++;
        
        // * (multiplication) - primitive 9
        smallIntegerClass.AddMethod("*", SCompiledMethod.CreateCompiledMethod(
            "*",
            [0x8F, 0x09, 0x01, 0x7C], // Primitive 9
            new SArray(),
            1, 0, 9));
        instanceMethodCount++;
        
        // / (division) - primitive 10
        smallIntegerClass.AddMethod("/", SCompiledMethod.CreateCompiledMethod(
            "/",
            [0x8F, 0x0A, 0x01, 0x7C], // Primitive 10
            new SArray(),
            1, 0, 10));
        instanceMethodCount++;
        
        // // (integer division) - primitive 12
        smallIntegerClass.AddMethod("//", SCompiledMethod.CreateCompiledMethod(
            "//",
            [0x8F, 0x0C, 0x01, 0x7C], // Primitive 12
            new SArray(),
            1, 0, 12));
        instanceMethodCount++;
        
        // \\ (modulo) - primitive 11
        smallIntegerClass.AddMethod("\\\\", SCompiledMethod.CreateCompiledMethod(
            "\\\\",
            [0x8F, 0x0B, 0x01, 0x7C], // Primitive 11
            new SArray(),
            1, 0, 11));
        instanceMethodCount++;
        
        // < - primitive 3
        smallIntegerClass.AddMethod("<", SCompiledMethod.CreateCompiledMethod(
            "<",
            [0x8F, 0x03, 0x01, 0x7C], // Primitive 3
            new SArray(),
            1, 0, 3));
        instanceMethodCount++;
        
        // > - primitive 4
        smallIntegerClass.AddMethod(">", SCompiledMethod.CreateCompiledMethod(
            ">",
            [0x8F, 0x04, 0x01, 0x7C], // Primitive 4
            new SArray(),
            1, 0, 4));
        instanceMethodCount++;
        
        // <= - primitive 5
        smallIntegerClass.AddMethod("<=", SCompiledMethod.CreateCompiledMethod(
            "<=",
            [0x8F, 0x05, 0x01, 0x7C], // Primitive 5
            new SArray(),
            1, 0, 5));
        instanceMethodCount++;
        
        // >= - primitive 6
        smallIntegerClass.AddMethod(">=", SCompiledMethod.CreateCompiledMethod(
            ">=",
            [0x8F, 0x06, 0x01, 0x7C], // Primitive 6
            new SArray(),
            1, 0, 6));
        instanceMethodCount++;
        
        // = - primitive 7
        smallIntegerClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x8F, 0x07, 0x01, 0x7C], // Primitive 7
            new SArray(),
            1, 0, 7));
        instanceMethodCount++;
        
        // ~= - primitive 8
        smallIntegerClass.AddMethod("~=", SCompiledMethod.CreateCompiledMethod(
            "~=",
            [0x8F, 0x08, 0x01, 0x7C], // Primitive 8
            new SArray(),
            1, 0, 8));
        instanceMethodCount++;
        
        // negated
        smallIntegerClass.AddMethod("negated", SCompiledMethod.CreateCompiledMethod(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // abs
        smallIntegerClass.AddMethod("abs", SCompiledMethod.CreateCompiledMethod(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            SArray.From(SSymbol.Intern("negated")),
            0, 0, 0));
        instanceMethodCount++;
        
        // asFloat - primitive 40
        smallIntegerClass.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethod(
            "asFloat",
            [0x8F, 0x28, 0x00, 0x7C], // Primitive 40
            new SArray(),
            0, 0, 40));
        instanceMethodCount++;
        
        // to:do: block iteration
        smallIntegerClass.AddMethod("to:do:", SCompiledMethod.CreateCompiledMethod(
            "to:do:",
            [0x70, 0x69, 0x10, 0xB5, 0xAC, 0x09, 0x11, 0x6A, 0xD5, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF4, 0x73, 0x7C],
            SArray.From(SSymbol.Intern("value:")),
            2, 1, 0));
        instanceMethodCount++;
        
        // timesRepeat: block iteration
        smallIntegerClass.AddMethod("timesRepeat:", SCompiledMethod.CreateCompiledMethod(
            "timesRepeat:",
            [0x76, 0x69, 0x70, 0xB5, 0xAC, 0x07, 0x10, 0xC9, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF6, 0x73, 0x7C],
            SArray.From(SSymbol.Intern("value")),
            1, 1, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] SmallInteger: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Float methods.
    /// </summary>
    private static void InstallFloatMethods()
    {
        var floatClass = SystemDictionary.FloatClass;
        if (floatClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Float instance methods: #+, #-, #*, #/, #<, #>, #<=, #>=, #=, #~=, #negated, #abs, #truncate, #rounded, #asFloat, #printOn:");
        }
        
        // + - primitive 41
        floatClass.AddMethod("+", SCompiledMethod.CreateCompiledMethod(
            "+",
            [0x8F, 0x29, 0x01, 0x7C], // Primitive 41
            new SArray(),
            1, 0, 41));
        instanceMethodCount++;
        
        // - - primitive 42
        floatClass.AddMethod("-", SCompiledMethod.CreateCompiledMethod(
            "-",
            [0x8F, 0x2A, 0x01, 0x7C], // Primitive 42
            new SArray(),
            1, 0, 42));
        instanceMethodCount++;
        
        // * - primitive 49
        floatClass.AddMethod("*", SCompiledMethod.CreateCompiledMethod(
            "*",
            [0x8F, 0x31, 0x01, 0x7C], // Primitive 49
            new SArray(),
            1, 0, 49));
        instanceMethodCount++;
        
        // / - primitive 50
        floatClass.AddMethod("/", SCompiledMethod.CreateCompiledMethod(
            "/",
            [0x8F, 0x32, 0x01, 0x7C], // Primitive 50
            new SArray(),
            1, 0, 50));
        instanceMethodCount++;
        
        // < - primitive 43
        floatClass.AddMethod("<", SCompiledMethod.CreateCompiledMethod(
            "<",
            [0x8F, 0x2B, 0x01, 0x7C], // Primitive 43
            new SArray(),
            1, 0, 43));
        instanceMethodCount++;
        
        // > - primitive 44
        floatClass.AddMethod(">", SCompiledMethod.CreateCompiledMethod(
            ">",
            [0x8F, 0x2C, 0x01, 0x7C], // Primitive 44
            new SArray(),
            1, 0, 44));
        instanceMethodCount++;
        
        // <= - primitive 45
        floatClass.AddMethod("<=", SCompiledMethod.CreateCompiledMethod(
            "<=",
            [0x8F, 0x2D, 0x01, 0x7C], // Primitive 45
            new SArray(),
            1, 0, 45));
        instanceMethodCount++;
        
        // >= - primitive 46
        floatClass.AddMethod(">=", SCompiledMethod.CreateCompiledMethod(
            ">=",
            [0x8F, 0x2E, 0x01, 0x7C], // Primitive 46
            new SArray(),
            1, 0, 46));
        instanceMethodCount++;
        
        // = - primitive 47
        floatClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x8F, 0x2F, 0x01, 0x7C], // Primitive 47
            new SArray(),
            1, 0, 47));
        instanceMethodCount++;
        
        // ~= - primitive 48
        floatClass.AddMethod("~=", SCompiledMethod.CreateCompiledMethod(
            "~=",
            [0x8F, 0x30, 0x01, 0x7C], // Primitive 48
            new SArray(),
            1, 0, 48));
        instanceMethodCount++;
        
        // truncate - primitive 51
        floatClass.AddMethod("truncate", SCompiledMethod.CreateCompiledMethod(
            "truncate",
            [0x8F, 0x33, 0x00, 0x7C], // Primitive 51
            new SArray(),
            0, 0, 51));
        instanceMethodCount++;
        
        // asFloat (returns self)
        floatClass.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethod(
            "asFloat",
            [0x70, 0x7C], // ^self
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Float: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Array methods.
    /// </summary>
    private static void InstallArrayMethods()
    {
        var arrayClass = SystemDictionary.ArrayClass;
        if (arrayClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Array instance methods: #at:, #at:put:, #size, #do:, #collect:, #select:, #reject:, #includes:, #isEmpty, #first, #last, #atAll:put:");
        }
        
        // at: - primitive 60
        arrayClass.AddMethod("at:", SCompiledMethod.CreateCompiledMethod(
            "at:",
            [0x8F, 0x3C, 0x01, 0x7C], // Primitive 60
            new SArray(),
            1, 0, 60));
        instanceMethodCount++;
        
        // at:put: - primitive 61
        arrayClass.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethod(
            "at:put:",
            [0x8F, 0x3D, 0x02, 0x7C], // Primitive 61
            new SArray(),
            2, 0, 61));
        instanceMethodCount++;
        
        // size - primitive 62
        arrayClass.AddMethod("size", SCompiledMethod.CreateCompiledMethod(
            "size",
            [0x8F, 0x3E, 0x00, 0x7C], // Primitive 62
            new SArray(),
            0, 0, 62));
        instanceMethodCount++;
        
        // do: block iteration
        arrayClass.AddMethod("do:", SCompiledMethod.CreateCompiledMethod(
            "do:",
            [0x76, 0x69, 0x70, 0xC2, 0x6A, 0xB5, 0xAC, 0x0B, 0x70, 0x6A, 0xC0, 0x10, 0xE6, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF2, 0x73, 0x7C],
            SArray.From(SSymbol.Intern("size"), SSymbol.Intern("at:"), SSymbol.Intern("value:")),
            1, 2, 0));
        instanceMethodCount++;
        
        // isEmpty
        arrayClass.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethod(
            "isEmpty",
            [0x70, 0xC2, 0x75, 0xB6, 0x7C], // self size = 0
            SArray.From(SSymbol.Intern("size")),
            0, 0, 0));
        instanceMethodCount++;
        
        // first
        arrayClass.AddMethod("first", SCompiledMethod.CreateCompiledMethod(
            "first",
            [0x70, 0x76, 0xC0, 0x7C], // self at: 1
            SArray.From(SSymbol.Intern("at:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // last
        arrayClass.AddMethod("last", SCompiledMethod.CreateCompiledMethod(
            "last",
            [0x70, 0x70, 0xC2, 0xC0, 0x7C], // self at: self size
            SArray.From(SSymbol.Intern("at:"), SSymbol.Intern("size")),
            0, 0, 0));
        instanceMethodCount++;
        
        // atAll:put: - sets multiple elements to same value
        arrayClass.AddMethod("atAll:put:", SCompiledMethod.CreateCompiledMethod(
            "atAll:put:",
            [0x10, 0x8F, 0x00, 0x03, 0x70, 0x12, 0x11, 0xE1, 0xF7, 0x70, 0x7C], // indices do: [:i | self at: i put: value]. ^self
            SArray.From(
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
        var stringClass = SystemDictionary.StringClass;
        if (stringClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing String instance methods: #at:, #at:put:, #size, #=, #hash, #,, #copyFrom:to:, #asSymbol, #asString, #keywords, #numArgs, #isKeyword, #printOn:");
        }
        
        // at: - primitive 63
        stringClass.AddMethod("at:", SCompiledMethod.CreateCompiledMethod(
            "at:",
            [0x8F, 0x3F, 0x01, 0x7C], // Primitive 63
            new SArray(),
            1, 0, 63));
        instanceMethodCount++;
        
        // at:put: - primitive 64
        stringClass.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethod(
            "at:put:",
            [0x8F, 0x40, 0x02, 0x7C], // Primitive 64
            new SArray(),
            2, 0, 64));
        instanceMethodCount++;
        
        // size - primitive 62
        stringClass.AddMethod("size", SCompiledMethod.CreateCompiledMethod(
            "size",
            [0x8F, 0x3E, 0x00, 0x7C], // Primitive 62
            new SArray(),
            0, 0, 62));
        instanceMethodCount++;
        
        // = string comparison - primitive 105
        stringClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x8F, 0x69, 0x01, 0x7C], // Primitive 105
            new SArray(),
            1, 0, 105));
        instanceMethodCount++;
        
        // hash - primitive 106
        stringClass.AddMethod("hash", SCompiledMethod.CreateCompiledMethod(
            "hash",
            [0x8F, 0x6A, 0x00, 0x7C], // Primitive 106
            new SArray(),
            0, 0, 106));
        instanceMethodCount++;
        
        // , (concatenation)
        stringClass.AddMethod(",", SCompiledMethod.CreateCompiledMethod(
            ",",
            [0x70, 0xC2, 0x10, 0xC2, 0xB0, 0x68, 0x44, 0xCC, 0x69, 0x76, 0x70, 0xC2, 0xE8, 0x6B,
             0x76, 0x10, 0xC2, 0xE8, 0x69, 0x7C], // Implementation of string concatenation
            SArray.From(
                SSymbol.Intern("size"),
                SSymbol.Intern("String"),
                SSymbol.Intern("new:"),
                SSymbol.Intern("replaceFrom:to:with:")),
            1, 2, 0));
        instanceMethodCount++;
        
        // asSymbol - primitive 110
        stringClass.AddMethod("asSymbol", SCompiledMethod.CreateCompiledMethod(
            "asSymbol",
            [0x8F, 0x6E, 0x00, 0x7C], // Primitive 110 (intern as symbol)
            new SArray(),
            0, 0, 110));
        instanceMethodCount++;
        
        // asString (returns self for String)
        stringClass.AddMethod("asString", SCompiledMethod.CreateCompiledMethod(
            "asString",
            [0x70, 0x7C], // ^self
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // keywords - extracts keyword selectors from a string
        stringClass.AddMethod("keywords", SCompiledMethod.CreateCompiledMethod(
            "keywords",
            [0x41, 0xD0, 0x68, // OrderedCollection new
             0x76, 0x69, 0x6A, // index := 1, start := 1
             0x6A, 0x70, 0xC2, 0xB5, 0xAC, 0x20, // [index <= self size] whileTrue:
             0x70, 0x6A, 0xC0, 0x21, 0xE1, 0x9D, 0x0E, // (self at: index) = $: ifTrue:
             0x6B, 0x70, 0x6A, 0x76, 0xB1, 0x6B, 0xE2, 0xD2, // add from start to index
             0x6A, 0x76, 0xB0, 0x6B, 0x69, // index := index + 1, start := index
             0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xDF, // index := index + 1, loop
             0x68, 0x7C], // ^result
            SArray.From(
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
        stringClass.AddMethod("numArgs", SCompiledMethod.CreateCompiledMethod(
            "numArgs",
            [0x75, 0x68, // count := 0
             0x70, 0x8F, 0x00, 0x03, // self do: [:c |
             0x10, 0x21, 0xB6, 0x9D, 0x04, 0x68, 0x76, 0xB0, 0x68, // c = $: ifTrue: [count := count + 1]
             0xF7, 0x68, 0x7C], // ]. ^count
            SArray.From(
                SSymbol.Intern("do:"),
                SSymbol.Intern(":"),
                SSymbol.Intern("=")),
            0, 1, 0));
        instanceMethodCount++;
        
        // isKeyword - checks if string is a keyword selector
        stringClass.AddMethod("isKeyword", SCompiledMethod.CreateCompiledMethod(
            "isKeyword",
            [0x70, 0xC2, 0x75, 0xB3, 0x9D, 0x02, 0x72, 0x7C, // self size > 0 ifFalse: [^false]
             0x70, 0x70, 0xC2, 0xC0, 0x21, 0xB6, 0x7C], // (self at: self size) = $:
            SArray.From(
                SSymbol.Intern("size"),
                SSymbol.Intern(">"),
                SSymbol.Intern("at:"),
                SSymbol.Intern(":"),
                SSymbol.Intern("=")),
            0, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        stringClass.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x70, 0xE0, 0x7C], // stream nextPutAll: self
            SArray.From(SSymbol.Intern("nextPutAll:")),
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
        var symbolClass = SystemDictionary.SymbolClass;
        if (symbolClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Symbol instance methods: #=, #hash, #asString, #asSymbol");
        }
        
        // = (symbol comparison uses identity)
        symbolClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x70, 0x10, 0xB6, 0x7C], // self == arg
            SArray.From(SSymbol.Intern("==")),
            1, 0, 0));
        instanceMethodCount++;
        
        // hash (symbols hash to themselves)
        symbolClass.AddMethod("hash", SCompiledMethod.CreateCompiledMethod(
            "hash",
            [0x70, 0xC8, 0x7C], // self identityHash
            SArray.From(SSymbol.Intern("identityHash")),
            0, 0, 0));
        instanceMethodCount++;
        
        // asString
        symbolClass.AddMethod("asString", SCompiledMethod.CreateCompiledMethod(
            "asString",
            [0x70, 0xD0, 0x7C], // self copy (symbols are strings, so copy returns a String)
            SArray.From(SSymbol.Intern("copy")),
            0, 0, 0));
        instanceMethodCount++;
        
        // asSymbol (returns self)
        symbolClass.AddMethod("asSymbol", SCompiledMethod.CreateCompiledMethod(
            "asSymbol",
            [0x70, 0x7C], // ^self
            new SArray(),
            0, 0, 0));
        instanceMethodCount++;
        
        // asString
        symbolClass.AddMethod("asString", SCompiledMethod.CreateCompiledMethod(
            "asString",
            [0x44, 0x70, 0xC2, 0xCC, 0x68, // String new: self size
             0x76, 0x70, 0xE1, 0x68, 0x7C], // result replaceFrom: 1 to: self size with: self. ^result
            SArray.From(
                SSymbol.Intern("String"),
                SSymbol.Intern("size"),
                SSymbol.Intern("new:"),
                SSymbol.Intern("replaceFrom:to:with:")),
            0, 1, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Symbol: {instanceMethodCount} instance methods installed");
        }
    }
    
    // Stub implementations for other classes - these would need to be expanded
    private static void InstallBehaviorMethods()
    {
        var behaviorClass = SystemDictionary.BehaviorClass;
        if (behaviorClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Behavior instance methods: #new, #new:, #basicNew, #basicNew:, #compile:, #addSelector:withMethod:");
        }
        
        // new
        behaviorClass.AddMethod("new", SCompiledMethod.CreateCompiledMethod(
            "new",
            [0x70, 0xCB, 0xD2, 0x7C], // self basicNew initialize
            SArray.From(SSymbol.Intern("basicNew"), SSymbol.Intern("initialize")),
            0, 0, 0));
        instanceMethodCount++;
        
        // new: with size
        behaviorClass.AddMethod("new:", SCompiledMethod.CreateCompiledMethod(
            "new:",
            [0x70, 0x10, 0xCC, 0xD2, 0x7C], // self basicNew: size initialize
            SArray.From(SSymbol.Intern("basicNew:"), SSymbol.Intern("initialize")),
            1, 0, 0));
        instanceMethodCount++;
        
        // basicNew - primitive 70
        behaviorClass.AddMethod("basicNew", SCompiledMethod.CreateCompiledMethod(
            "basicNew",
            [0x8F, 0x46, 0x00, 0x7C], // Primitive 70
            new SArray(),
            0, 0, 70));
        instanceMethodCount++;
        
        // basicNew: - primitive 71
        behaviorClass.AddMethod("basicNew:", SCompiledMethod.CreateCompiledMethod(
            "basicNew:",
            [0x8F, 0x47, 0x01, 0x7C], // Primitive 71
            new SArray(),
            1, 0, 71));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Behavior: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallClassDescriptionMethods()
    {
        var classDescriptionClass = SystemDictionary.ClassDescriptionClass;
        if (classDescriptionClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ClassDescription: 0 instance methods installed");
        }
    }
    
    private static void InstallClassMethods()
    {
        var classClass = SystemDictionary.ClassClass;
        if (classClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Class: 0 instance methods installed");
        }
    }
    
    private static void InstallMetaclassMethods()
    {
        var metaclassClass = SystemDictionary.MetaclassClass;
        if (metaclassClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Metaclass: 0 instance methods installed");
        }
    }
    
    private static void InstallMagnitudeMethods()
    {
        var magnitudeClass = SystemDictionary.MagnitudeClass;
        if (magnitudeClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Magnitude instance methods: #<, #>, #<=, #>=, #between:and:, #min:, #max:");
        }
        
        // These are abstract in Magnitude, subclasses must implement <
        
        // > (greater than in terms of <)
        magnitudeClass.AddMethod(">", SCompiledMethod.CreateCompiledMethod(
            ">",
            [0x10, 0x70, 0xB2, 0x7C], // arg < self
            SArray.From(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // <= (less than or equal)
        magnitudeClass.AddMethod("<=", SCompiledMethod.CreateCompiledMethod(
            "<=",
            [0x70, 0x10, 0xB3, 0x9C, 0x7C], // (self > arg) not
            SArray.From(SSymbol.Intern(">")),
            1, 0, 0));
        instanceMethodCount++;
        
        // >= (greater than or equal)
        magnitudeClass.AddMethod(">=", SCompiledMethod.CreateCompiledMethod(
            ">=",
            [0x70, 0x10, 0xB2, 0x9C, 0x7C], // (self < arg) not
            SArray.From(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // between:and:
        magnitudeClass.AddMethod("between:and:", SCompiledMethod.CreateCompiledMethod(
            "between:and:",
            [0x70, 0x10, 0xB5, 0x9D, 0x05, 0x70, 0x11, 0xB5, 0x91, 0x72, 0x7C], // self >= low and: [self <= high]
            SArray.From(SSymbol.Intern(">=")),
            2, 0, 0));
        instanceMethodCount++;
        
        // min:
        magnitudeClass.AddMethod("min:", SCompiledMethod.CreateCompiledMethod(
            "min:",
            [0x70, 0x10, 0xB2, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self < arg ifTrue: [self] ifFalse: [arg]
            SArray.From(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // max:
        magnitudeClass.AddMethod("max:", SCompiledMethod.CreateCompiledMethod(
            "max:",
            [0x70, 0x10, 0xB3, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self > arg ifTrue: [self] ifFalse: [arg]
            SArray.From(SSymbol.Intern(">")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Magnitude: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallNumberMethods()
    {
        var numberClass = SystemDictionary.NumberClass;
        if (numberClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Number instance methods: #negated, #abs, #positive, #negative, #isZero");
        }
        
        // negated
        numberClass.AddMethod("negated", SCompiledMethod.CreateCompiledMethod(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            SArray.From(SSymbol.Intern("-")),
            0, 0, 0));
        instanceMethodCount++;
        
        // abs
        numberClass.AddMethod("abs", SCompiledMethod.CreateCompiledMethod(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            SArray.From(SSymbol.Intern("<"), SSymbol.Intern("negated")),
            0, 0, 0));
        instanceMethodCount++;
        
        // positive
        numberClass.AddMethod("positive", SCompiledMethod.CreateCompiledMethod(
            "positive",
            [0x70, 0x75, 0xB3, 0x7C], // self > 0
            SArray.From(SSymbol.Intern(">")),
            0, 0, 0));
        instanceMethodCount++;
        
        // negative
        numberClass.AddMethod("negative", SCompiledMethod.CreateCompiledMethod(
            "negative",
            [0x70, 0x75, 0xB2, 0x7C], // self < 0
            SArray.From(SSymbol.Intern("<")),
            0, 0, 0));
        instanceMethodCount++;
        
        // isZero
        numberClass.AddMethod("isZero", SCompiledMethod.CreateCompiledMethod(
            "isZero",
            [0x70, 0x75, 0xB6, 0x7C], // self = 0
            SArray.From(SSymbol.Intern("=")),
            0, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Number: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallIntegerMethods()
    {
        var integerClass = SystemDictionary.IntegerClass;
        if (integerClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Integer: 0 instance methods installed");
        }
    }
    
    private static void InstallCharacterMethods()
    {
        var characterClass = SystemDictionary.CharacterClass;
        if (characterClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Character instance methods: #=, #hash, #asInteger, #isLetter, #isDigit, #isAlphaNumeric, #isUppercase, #isLowercase, #isSeparator, #isSpecial");
        }
        
        // asInteger - primitive 171
        characterClass.AddMethod("asInteger", SCompiledMethod.CreateCompiledMethod(
            "asInteger",
            [0x8F, 0xAB, 0x00, 0x7C], // Primitive 171
            new SArray(),
            0, 0, 171));
        instanceMethodCount++;
        
        // isLetter - checks if character is a letter (A-Z, a-z)
        characterClass.AddMethod("isLetter", SCompiledMethod.CreateCompiledMethod(
            "isLetter",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x41, 0xB5, 0x9D, 0x06, // >= 65 ($A)
             0x68, 0x22, 0x5A, 0xB5, 0x91, 0x71, 0x7C, // <= 90 ($Z) -> true
             0x68, 0x22, 0x61, 0xB5, 0x9D, 0x06, // >= 97 ($a)
             0x68, 0x22, 0x7A, 0xB5, 0x91, 0x71, 0x7C, // <= 122 ($z) -> true
             0x72, 0x7C], // false
            SArray.From(
                SSymbol.Intern("asInteger"),
                SmallInteger.From(65),
                SSymbol.Intern(">="),
                SmallInteger.From(90),
                SSymbol.Intern("<="),
                SmallInteger.From(97),
                SmallInteger.From(122)),
            0, 1, 0));
        instanceMethodCount++;
        
        // isDigit - checks if character is a digit (0-9)
        characterClass.AddMethod("isDigit", SCompiledMethod.CreateCompiledMethod(
            "isDigit",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x30, 0xB5, 0x9D, 0x06, // >= 48 ($0)
             0x68, 0x22, 0x39, 0xB5, 0x91, 0x71, 0x7C, // <= 57 ($9) -> true
             0x72, 0x7C], // false
            SArray.From(
                SSymbol.Intern("asInteger"),
                SmallInteger.From(48),
                SSymbol.Intern(">="),
                SmallInteger.From(57),
                SSymbol.Intern("<=")),
            0, 1, 0));
        instanceMethodCount++;
        
        // isAlphaNumeric - letter or digit
        characterClass.AddMethod("isAlphaNumeric", SCompiledMethod.CreateCompiledMethod(
            "isAlphaNumeric",
            [0x70, 0xD0, 0x99, 0x02, 0x71, 0x7C, // self isLetter ifTrue: [^true]
             0x70, 0xD1, 0x7C], // self isDigit
            SArray.From(
                SSymbol.Intern("isLetter"),
                SSymbol.Intern("isDigit")),
            0, 0, 0));
        instanceMethodCount++;
        
        // isSeparator - space, tab, cr, lf, etc.
        characterClass.AddMethod("isSeparator", SCompiledMethod.CreateCompiledMethod(
            "isSeparator",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x20, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 32 (space) -> true
             0x68, 0x22, 0x09, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 9 (tab) -> true
             0x68, 0x22, 0x0D, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 13 (cr) -> true
             0x68, 0x22, 0x0A, 0xB6, 0x7C], // = 10 (lf)
            SArray.From(
                SSymbol.Intern("asInteger"),
                SmallInteger.From(32),
                SSymbol.Intern("="),
                SmallInteger.From(9),
                SmallInteger.From(13),
                SmallInteger.From(10)),
            0, 1, 0));
        instanceMethodCount++;
        
        // isSpecial - special characters used in Smalltalk syntax
        characterClass.AddMethod("isSpecial", SCompiledMethod.CreateCompiledMethod(
            "isSpecial",
            [0x21, 0x10, 0xE0, 0x7C], // '+/\\*~<>=@%|&?!' includes: self
            SArray.From(
                SSymbol.Intern("+/\\*~<>=@%|&?!"),
                SSymbol.Intern("includes:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // Note: Class methods would be installed on Character's metaclass
        // They would include: #value:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Character: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallByteArrayMethods()
    {
        var byteArrayClass = SystemDictionary.ByteArrayClass;
        if (byteArrayClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ByteArray: 0 instance methods installed");
        }
    }
    
    private static void InstallCompiledMethodMethods()
    {
        var compiledMethodClass = SystemDictionary.CompiledMethodClass;
        if (compiledMethodClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] CompiledMethod: 0 instance methods installed");
        }
    }
    
    private static void InstallMethodDictionaryMethods()
    {
        var methodDictionaryClass = SystemDictionary.MethodDictionaryClass;
        if (methodDictionaryClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] MethodDictionary: 0 instance methods installed");
        }
    }
    
    private static void InstallAssociationMethods()
    {
        var associationClass = SystemDictionary.AssociationClass;
        if (associationClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Association instance methods: #key, #value, #key:, #value:");
        }
        
        // key - primitive 86
        associationClass.AddMethod("key", SCompiledMethod.CreateCompiledMethod(
            "key",
            [0x8F, 0x56, 0x00, 0x7C], // Primitive 86
            new SArray(),
            0, 0, 86));
        instanceMethodCount++;
        
        // value - primitive 87
        associationClass.AddMethod("value", SCompiledMethod.CreateCompiledMethod(
            "value",
            [0x8F, 0x57, 0x00, 0x7C], // Primitive 87
            new SArray(),
            0, 0, 87));
        instanceMethodCount++;
        
        // key: - primitive 88
        associationClass.AddMethod("key:", SCompiledMethod.CreateCompiledMethod(
            "key:",
            [0x8F, 0x58, 0x01, 0x7C], // Primitive 88
            new SArray(),
            1, 0, 88));
        instanceMethodCount++;
        
        // value: - primitive 89
        associationClass.AddMethod("value:", SCompiledMethod.CreateCompiledMethod(
            "value:",
            [0x8F, 0x59, 0x01, 0x7C], // Primitive 89
            new SArray(),
            1, 0, 89));
        instanceMethodCount++;
        
        // Note: Class methods would be installed on Association's metaclass
        // They would include: #key:value:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Association: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallDictionaryMethods()
    {
        var dictionaryClass = SystemDictionary.DictionaryClass;
        if (dictionaryClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Dictionary instance methods: #at:, #at:put:, #at:ifAbsent:, #associationAt:, #associationAt:ifAbsent:, #includesKey:, #removeKey:");
        }
        
        // at: - primitive 105
        dictionaryClass.AddMethod("at:", SCompiledMethod.CreateCompiledMethod(
            "at:",
            [0x8F, 0x69, 0x01, 0x7C], // Primitive 105
            new SArray(),
            1, 0, 105));
        instanceMethodCount++;
        
        // at:put: - primitive 106
        dictionaryClass.AddMethod("at:put:", SCompiledMethod.CreateCompiledMethod(
            "at:put:",
            [0x8F, 0x6A, 0x02, 0x7C], // Primitive 106
            new SArray(),
            2, 0, 106));
        instanceMethodCount++;
        
        // at:ifAbsent: - looks up key, evaluates block if not found
        dictionaryClass.AddMethod("at:ifAbsent:", SCompiledMethod.CreateCompiledMethod(
            "at:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self associationAt: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0xD1, 0x7C], // ^assoc value
            SArray.From(
                SSymbol.Intern("associationAt:"),
                SSymbol.Intern("="),
                SSymbol.Intern("value")),
            2, 1, 0));
        instanceMethodCount++;
        
        // associationAt: - returns the association for a key
        dictionaryClass.AddMethod("associationAt:", SCompiledMethod.CreateCompiledMethod(
            "associationAt:",
            [0x70, 0x10, 0xE0, 0x7C], // self findAssociation: key (would need primitive)
            SArray.From(SSymbol.Intern("findAssociation:")),
            1, 0, 0));
        instanceMethodCount++;
        
        // associationAt:ifAbsent: - returns association or evaluates block
        dictionaryClass.AddMethod("associationAt:ifAbsent:", SCompiledMethod.CreateCompiledMethod(
            "associationAt:ifAbsent:",
            [0x70, 0x10, 0xE0, 0x68, // assoc := self findAssociation: key
             0x68, 0x73, 0xB6, 0x9D, 0x03, 0x11, 0xC9, 0x7C, // assoc isNil ifTrue: [^absentBlock value]
             0x68, 0x7C], // ^assoc
            SArray.From(
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
    
    private static void InstallLinkMethods()
    {
        var linkClass = SystemDictionary.LinkClass;
        if (linkClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Link: 0 instance methods installed");
        }
    }
    
    private static void InstallProcessMethods()
    {
        var processClass = SystemDictionary.ProcessClass;
        if (processClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Process instance methods: #resume, #suspend, #terminate");
        }
        
        // resume - primitive 87
        processClass.AddMethod("resume", SCompiledMethod.CreateCompiledMethod(
            "resume",
            [0x8F, 0x57, 0x00, 0x7C], // Primitive 87
            new SArray(),
            0, 0, 87));
        instanceMethodCount++;
        
        // suspend - primitive 88
        processClass.AddMethod("suspend", SCompiledMethod.CreateCompiledMethod(
            "suspend",
            [0x8F, 0x58, 0x00, 0x7C], // Primitive 88
            new SArray(),
            0, 0, 88));
        instanceMethodCount++;
        
        // terminate - primitive 91
        processClass.AddMethod("terminate", SCompiledMethod.CreateCompiledMethod(
            "terminate",
            [0x8F, 0x5B, 0x00, 0x7C], // Primitive 91
            new SArray(),
            0, 0, 91));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Process: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallLinkedListMethods()
    {
        var linkedListClass = SystemDictionary.LinkedListClass;
        if (linkedListClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing LinkedList instance methods: #isEmpty, #addFirst:, #addLast:, #removeFirst, #first");
        }
        
        // isEmpty
        // ^firstLink == nil
        linkedListClass.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethod(
            "isEmpty",
            [0x00, 0x71, 0xC6, 0x7C], // Push firstLink, push nil, ==, return
            new SArray(),
            0, 0));
        instanceMethodCount++;
        
        // addFirst: aLink
        // firstLink := aLink. lastLink ifNil: [lastLink := aLink]. ^aLink
        linkedListClass.AddMethod("addFirst:", SCompiledMethod.CreateCompiledMethod(
            "addFirst:",
            [0x10, 0x60, 0x01, 0xC7, // Push arg, store in firstLink
             0x01, 0x71, 0x99, 0x04, // Push lastLink, nil check, jump if not nil
             0x10, 0x61,             // Push arg, store in lastLink
             0x10, 0x7C],            // Push arg, return
            new SArray(),
            1, 0));
        instanceMethodCount++;
        
        // addLast: aLink
        // isEmpty ifTrue: [firstLink := aLink] ifFalse: [lastLink nextLink: aLink].
        // lastLink := aLink. ^aLink
        linkedListClass.AddMethod("addLast:", SCompiledMethod.CreateCompiledMethod(
            "addLast:",
            [0x70, 0xCE,             // Push self, send isEmpty
             0x99, 0x06,             // Jump if false
             0x10, 0x60,             // Push arg, store in firstLink
             0xA4, 0x05,             // Jump to end
             0x01, 0x10, 0xE0,       // Push lastLink, push arg, send nextLink:
             0x87,                   // Pop
             0x10, 0x61,             // Push arg, store in lastLink
             0x10, 0x7C],            // Push arg, return
            new SArray(),
            1, 0));
        instanceMethodCount++;
        
        // removeFirst
        // | oldLink | 
        // self emptyCheck.
        // oldLink := firstLink.
        // firstLink == lastLink
        //     ifTrue: [firstLink := nil. lastLink := nil]
        //     ifFalse: [firstLink := oldLink nextLink].
        // oldLink nextLink: nil.
        // ^oldLink
        linkedListClass.AddMethod("removeFirst", SCompiledMethod.CreateCompiledMethod(
            "removeFirst",
            [0x70, 0xD0,             // Push self, send emptyCheck
             0x87,                   // Pop
             0x00, 0x68,             // Push firstLink, store temp 0 (oldLink)
             0x00, 0x01, 0xC6,       // Push firstLink, lastLink, ==
             0x99, 0x08,             // Jump if false
             0x71, 0x60,             // nil, store firstLink
             0x71, 0x61,             // nil, store lastLink
             0xA4, 0x06,             // Jump to end
             0x11, 0xCF, 0x60,       // Push oldLink, nextLink, store firstLink
             0x11, 0x71, 0xE0,       // Push oldLink, nil, send nextLink:
             0x87,                   // Pop
             0x11, 0x7C],            // Push oldLink, return
            new SArray(),
            0, 1));
        instanceMethodCount++;
        
        // first
        // self emptyCheck. ^firstLink
        linkedListClass.AddMethod("first", SCompiledMethod.CreateCompiledMethod(
            "first",
            [0x70, 0xD0,             // Push self, send emptyCheck
             0x87,                   // Pop
             0x00, 0x7C],            // Push firstLink, return
            new SArray(),
            0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] LinkedList: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallSemaphoreMethods()
    {
        var semaphoreClass = SystemDictionary.SemaphoreClass;
        if (semaphoreClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Semaphore instance methods: #signal, #wait");
        }
        
        // signal - primitive 85
        semaphoreClass.AddMethod("signal", SCompiledMethod.CreateCompiledMethod(
            "signal",
            [0x8F, 0x55, 0x00, 0x7C], // Primitive 85
            new SArray(),
            0, 0, 85));
        instanceMethodCount++;
        
        // wait - primitive 86
        semaphoreClass.AddMethod("wait", SCompiledMethod.CreateCompiledMethod(
            "wait",
            [0x8F, 0x56, 0x00, 0x7C], // Primitive 86
            new SArray(),
            0, 0, 86));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Semaphore: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallProcessorSchedulerMethods()
    {
        var processorSchedulerClass = SystemDictionary.ProcessorSchedulerClass;
        if (processorSchedulerClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing ProcessorScheduler instance methods: #activeProcess, #activePriority, #yield, #terminateActive");
        }
        
        // activeProcess
        // ^activeProcess
        processorSchedulerClass.AddMethod("activeProcess", SCompiledMethod.CreateCompiledMethod(
            "activeProcess",
            [0x01, 0x7C], // Push activeProcess instance var, return
            new SArray(),
            0, 0));
        instanceMethodCount++;
        
        // activePriority  
        // ^activeProcess priority
        var activePriorityLiterals = SArray.From(SSymbol.Intern("priority"));
        processorSchedulerClass.AddMethod("activePriority", SCompiledMethod.CreateCompiledMethod(
            "activePriority",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send priority, return
            activePriorityLiterals,
            0, 0));
        instanceMethodCount++;
        
        // yield - primitive 167
        processorSchedulerClass.AddMethod("yield", SCompiledMethod.CreateCompiledMethod(
            "yield",
            [0x8F, 0xA7, 0x00, 0x7C], // Primitive 167
            new SArray(),
            0, 0, 167));
        instanceMethodCount++;
        
        // terminateActive
        // activeProcess terminate
        var terminateActiveLiterals = SArray.From(SSymbol.Intern("terminate"));
        processorSchedulerClass.AddMethod("terminateActive", SCompiledMethod.CreateCompiledMethod(
            "terminateActive",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send terminate, return
            terminateActiveLiterals,
            0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] ProcessorScheduler: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallStreamMethods()
    {
        var streamClass = SystemDictionary.StreamClass;
        if (streamClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Stream instance methods: #next, #next:, #nextPut:, #nextPutAll:, #atEnd");
        }
        
        // Abstract methods - subclasses must implement
        // next
        streamClass.AddMethod("next", SCompiledMethod.CreateCompiledMethod(
            "next",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            0, 0, 0));
        instanceMethodCount++;
        
        // nextPut:
        streamClass.AddMethod("nextPut:", SCompiledMethod.CreateCompiledMethod(
            "nextPut:",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            1, 0, 0));
        instanceMethodCount++;
        
        // atEnd
        streamClass.AddMethod("atEnd", SCompiledMethod.CreateCompiledMethod(
            "atEnd",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SArray.From(SSymbol.Intern("subclassResponsibility")),
            0, 0, 0));
        instanceMethodCount++;
        
        // nextPutAll:
        streamClass.AddMethod("nextPutAll:", SCompiledMethod.CreateCompiledMethod(
            "nextPutAll:",
            [0x10, 0x8F, 0x00, 0x02, 0x70, 0x11, 0xE1, 0xF7, 0x10, 0x7C], // aCollection do: [:each | self nextPut: each]. ^aCollection
            SArray.From(SSymbol.Intern("do:"), SSymbol.Intern("nextPut:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Stream: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallPositionableStreamMethods()
    {
        var positionableStreamClass = SystemDictionary.PositionableStreamClass;
        if (positionableStreamClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] PositionableStream: 0 instance methods installed");
        }
    }
    
    private static void InstallWriteStreamMethods()
    {
        var writeStreamClass = SystemDictionary.WriteStreamClass;
        if (writeStreamClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing WriteStream instance methods: #nextPut:, #nextPutAll:, #contents, #position, #space, #cr, #tab");
        }
        
        // nextPut:
        writeStreamClass.AddMethod("nextPut:", SCompiledMethod.CreateCompiledMethod(
            "nextPut:",
            [0x00, 0x01, 0x76, 0xB0, 0x60, 0x01, 0x10, 0xC1, 0x60, 0x76, 0xB0, 0x60, 0x10, 0x7C],
            SArray.From(SSymbol.Intern("collection"), SSymbol.Intern("position"), SSymbol.Intern("at:put:")),
            1, 0, 0));
        instanceMethodCount++;
        
        // position - return current position
        writeStreamClass.AddMethod("position", SCompiledMethod.CreateCompiledMethod(
            "position",
            [0x01, 0x7C], // ^position
            SArray.From(SSymbol.Intern("position")),
            0, 0, 0));
        instanceMethodCount++;
        
        // space - write a space character
        writeStreamClass.AddMethod("space", SCompiledMethod.CreateCompiledMethod(
            "space",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 32)
            SArray.From(
                SSymbol.Intern("Character"),
                SmallInteger.From(32),
                SSymbol.Intern("value:"),
                SSymbol.Intern("nextPut:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // cr - write a carriage return
        writeStreamClass.AddMethod("cr", SCompiledMethod.CreateCompiledMethod(
            "cr",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 13)
            SArray.From(
                SSymbol.Intern("Character"),
                SmallInteger.From(13),
                SSymbol.Intern("value:"),
                SSymbol.Intern("nextPut:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // tab - write a tab character
        writeStreamClass.AddMethod("tab", SCompiledMethod.CreateCompiledMethod(
            "tab",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 9)
            SArray.From(
                SSymbol.Intern("Character"),
                SmallInteger.From(9),
                SSymbol.Intern("value:"),
                SSymbol.Intern("nextPut:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // contents
        writeStreamClass.AddMethod("contents", SCompiledMethod.CreateCompiledMethod(
            "contents",
            [0x00, 0x76, 0x01, 0xF0, 0x7C], // collection copyFrom: 1 to: position
            SArray.From(SSymbol.Intern("collection"), SSymbol.Intern("position"), SSymbol.Intern("copyFrom:to:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // Note: Class methods would be installed on WriteStream's metaclass
        // They would include: #on:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] WriteStream: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallReadStreamMethods()
    {
        var readStreamClass = SystemDictionary.ReadStreamClass;
        if (readStreamClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing ReadStream instance methods: #next, #peek, #position, #position:, #skip:, #upTo:, #atEnd");
        }
        
        // next - read and advance position
        readStreamClass.AddMethod("next", SCompiledMethod.CreateCompiledMethod(
            "next",
            [0x01, 0x01, 0x76, 0xB0, 0x60, // position := position + 1
             0x00, 0x01, 0xC1, 0x7C], // ^collection at: position
            SArray.From(
                SSymbol.Intern("collection"),
                SSymbol.Intern("position"),
                SSymbol.Intern("at:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // peek - look at next element without advancing
        readStreamClass.AddMethod("peek", SCompiledMethod.CreateCompiledMethod(
            "peek",
            [0x01, 0x00, 0xC2, 0xB5, 0x9D, 0x02, 0x73, 0x7C, // position >= collection size ifTrue: [^nil]
             0x00, 0x01, 0x76, 0xB0, 0xC1, 0x7C], // ^collection at: position + 1
            SArray.From(
                SSymbol.Intern("collection"),
                SSymbol.Intern("position"),
                SSymbol.Intern("size"),
                SSymbol.Intern(">="),
                SSymbol.Intern("at:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // position - return current position
        readStreamClass.AddMethod("position", SCompiledMethod.CreateCompiledMethod(
            "position",
            [0x01, 0x7C], // ^position
            SArray.From(SSymbol.Intern("position")),
            0, 0, 0));
        instanceMethodCount++;
        
        // position: - set position
        readStreamClass.AddMethod("position:", SCompiledMethod.CreateCompiledMethod(
            "position:",
            [0x10, 0x61, 0x10, 0x7C], // position := anInteger. ^anInteger
            SArray.From(SSymbol.Intern("position")),
            1, 0, 0));
        instanceMethodCount++;
        
        // skip: - advance position by n
        readStreamClass.AddMethod("skip:", SCompiledMethod.CreateCompiledMethod(
            "skip:",
            [0x01, 0x10, 0xB0, 0x61, 0x70, 0x7C], // position := position + n. ^self
            SArray.From(SSymbol.Intern("position")),
            1, 0, 0));
        instanceMethodCount++;
        
        // upTo: - read elements up to a delimiter
        readStreamClass.AddMethod("upTo:", SCompiledMethod.CreateCompiledMethod(
            "upTo:",
            [0x41, 0xD0, 0x68, // result := OrderedCollection new
             0x70, 0xD2, 0x9C, 0xAC, 0x12, // [self atEnd not] whileTrue:
             0x70, 0xD3, 0x69, // element := self next
             0x69, 0x10, 0xB6, 0x99, 0x02, 0x68, 0x7C, // element = delimiter ifTrue: [^result]
             0x68, 0x69, 0xD2, 0xA3, 0xEB, // result add: element. loop
             0x68, 0x7C], // ^result
            SArray.From(
                SSymbol.Intern("OrderedCollection"),
                SSymbol.Intern("new"),
                SSymbol.Intern("atEnd"),
                SSymbol.Intern("next"),
                SSymbol.Intern("="),
                SSymbol.Intern("add:")),
            1, 2, 0));
        instanceMethodCount++;
        
        // atEnd - check if at end of stream
        readStreamClass.AddMethod("atEnd", SCompiledMethod.CreateCompiledMethod(
            "atEnd",
            [0x01, 0x00, 0xC2, 0xB5, 0x7C], // ^position >= collection size
            SArray.From(
                SSymbol.Intern("position"),
                SSymbol.Intern("collection"),
                SSymbol.Intern("size"),
                SSymbol.Intern(">=")),
            0, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] ReadStream: {instanceMethodCount} instance methods installed");
        }
    }
    
    private static void InstallContextPartMethods()
    {
        var contextPartClass = SystemDictionary.ContextPartClass;
        if (contextPartClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ContextPart: 0 instance methods installed");
        }
    }
    
    private static void InstallMethodContextMethods()
    {
        var methodContextClass = SystemDictionary.MethodContextClass;
        if (methodContextClass == null) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] MethodContext: 0 instance methods installed");
        }
    }
    
    private static void InstallBlockContextMethods()
    {
        var blockContextClass = SystemDictionary.BlockContextClass;
        if (blockContextClass == null) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing BlockContext instance methods: #value, #value:, #value:value:, #fork, #newProcess");
        }
        
        // value - primitive 81
        blockContextClass.AddMethod("value", SCompiledMethod.CreateCompiledMethod(
            "value",
            [0x8F, 0x51, 0x00, 0x7C], // Primitive 81
            new SArray(),
            0, 0, 81));
        instanceMethodCount++;
        
        // value: - primitive 81
        blockContextClass.AddMethod("value:", SCompiledMethod.CreateCompiledMethod(
            "value:",
            [0x8F, 0x51, 0x01, 0x7C], // Primitive 81 with 1 arg
            new SArray(),
            1, 0, 81));
        instanceMethodCount++;
        
        // value:value: - primitive 81
        blockContextClass.AddMethod("value:value:", SCompiledMethod.CreateCompiledMethod(
            "value:value:",
            [0x8F, 0x51, 0x02, 0x7C], // Primitive 81 with 2 args
            new SArray(),
            2, 0, 81));
        instanceMethodCount++;
        
        // fork
        // self newProcess resume
        var forkLiterals = SArray.From(
            SSymbol.Intern("newProcess"),
            SSymbol.Intern("resume"));
        blockContextClass.AddMethod("fork", SCompiledMethod.CreateCompiledMethod(
            "fork",
            [0x70, 0xD0,     // Push self, send newProcess
             0xD1, 0x7C],    // Send resume, return
            forkLiterals,
            0, 0));
        instanceMethodCount++;
        
        // newProcess
        // ^Process forContext: [self value. Processor terminateActive] priority: Processor activePriority
        var newProcessLiterals = SArray.From(
            SystemDictionary.ProcessClass,
            SSymbol.Intern("forContext:priority:"),
            SSymbol.Intern("value"),
            SystemDictionary.LookupGlobal("Processor"),
            SSymbol.Intern("terminateActive"),
            SSymbol.Intern("activePriority"));
        blockContextClass.AddMethod("newProcess", SCompiledMethod.CreateCompiledMethod(
            "newProcess",
            [0x8F, 0x89, 0x00,  // Create block context (primitive 137)
             0x06,              // Block size
             0x70, 0xD2,        // self value
             0x87,              // Pop
             0xC3, 0xD4, 0x7D,  // Processor terminateActive, blockReturn
             0xC3, 0xD5,        // Processor activePriority
             0xC0, 0xF1, 0x7C], // Process forContext:priority:, return
            newProcessLiterals,
            0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] BlockContext: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs class-side methods for classes that need them for compilation.
    /// Since metaclasses aren't fully implemented, we install these on the class itself for now.
    /// </summary>
    private static void InstallClassSideMethods()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing class-side methods for compilation support...");
        }
        
        // Install class methods for Scanner
        var scannerClass = SystemDictionary.ScannerClass;
        if (scannerClass != null)
        {
            scannerClass.AddMethod("new", SCompiledMethod.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SArray.From(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install class methods for Parser
        var parserClass = SystemDictionary.ParserClass;
        if (parserClass != null)
        {
            parserClass.AddMethod("new", SCompiledMethod.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SArray.From(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install class methods for Encoder
        var encoderClass = SystemDictionary.EncoderClass;
        if (encoderClass != null)
        {
            encoderClass.AddMethod("new", SCompiledMethod.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SArray.From(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install class methods for Compiler
        var compilerClass = SystemDictionary.CompilerClass;
        if (compilerClass != null)
        {
            compilerClass.AddMethod("new", SCompiledMethod.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SArray.From(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install instance methods for Collection
        var collectionClass = SystemDictionary.CollectionClass;
        if (collectionClass != null)
        {
            // size - basic size method
            collectionClass.AddMethod("size", SCompiledMethod.CreateCompiledMethod(
                "size",
                [0x75, 0x68, // tally := 0
                 0x70, 0x8F, 0x00, 0x02, 0x68, 0x76, 0xB0, 0x69, 0xF7, // self do: [:each | tally := tally + 1]
                 0x68, 0x7C], // ^tally
                SArray.From(
                    SSymbol.Intern("do:"),
                    SmallInteger.From(1)),
                0, 1, 0));
            
            // isEmpty
            collectionClass.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethod(
                "isEmpty",
                [0x70, 0xC0, 0x75, 0xB6, 0x7C], // ^self size = 0
                SArray.From(
                    SSymbol.Intern("size"),
                    SmallInteger.From(0),
                    SSymbol.Intern("=")),
                0, 0, 0));
            
            // do: - abstract, subclasses must override
            collectionClass.AddMethod("do:", SCompiledMethod.CreateCompiledMethod(
                "do:",
                [0x70, 0x21, 0xE0, 0x7C], // self subclassResponsibility
                SArray.From(
                    SSymbol.Intern("subclassResponsibility")),
                1, 0, 0));
        }
        
        // Install instance methods for SequenceableCollection
        var sequenceableCollectionClass = SystemDictionary.SequenceableCollectionClass;
        if (sequenceableCollectionClass != null)
        {
            // first
            sequenceableCollectionClass.AddMethod("first", SCompiledMethod.CreateCompiledMethod(
                "first",
                [0x70, 0x76, 0xC0, 0x7C], // ^self at: 1
                SArray.From(
                    SmallInteger.From(1),
                    SSymbol.Intern("at:")),
                0, 0, 0));
            
            // last
            sequenceableCollectionClass.AddMethod("last", SCompiledMethod.CreateCompiledMethod(
                "last",
                [0x70, 0x70, 0xC0, 0xC1, 0x7C], // ^self at: self size
                SArray.From(
                    SSymbol.Intern("size"),
                    SSymbol.Intern("at:")),
                0, 0, 0));
            
            // at:ifAbsent:
            sequenceableCollectionClass.AddMethod("at:ifAbsent:", SCompiledMethod.CreateCompiledMethod(
                "at:ifAbsent:",
                [0x10, 0x76, 0xB2, 0x9B, 0x08, // index < 1 ifTrue:
                 0x10, 0x70, 0xC0, 0xB4, 0x99, 0x03, // index > self size ifTrue:
                 0x11, 0xC9, 0x7C, // ^exceptionBlock value
                 0x70, 0x10, 0xC0, 0x7C], // ^self at: index
                SArray.From(
                    SmallInteger.From(1),
                    SSymbol.Intern("<"),
                    SSymbol.Intern("size"),
                    SSymbol.Intern(">"),
                    SSymbol.Intern("value"),
                    SSymbol.Intern("at:")),
                2, 0, 0));
        }
        
        // Install instance methods for OrderedCollection
        var orderedCollectionClass = SystemDictionary.OrderedCollectionClass;
        if (orderedCollectionClass != null)
        {
            // add:
            orderedCollectionClass.AddMethod("add:", SCompiledMethod.CreateCompiledMethod(
                "add:",
                [0x70, 0x10, 0xE0, 0x10, 0x7C], // self addLast: anObject. ^anObject
                SArray.From(SSymbol.Intern("addLast:")),
                1, 0, 0));
            
            // addLast:
            orderedCollectionClass.AddMethod("addLast:", SCompiledMethod.CreateCompiledMethod(
                "addLast:",
                [0x00, 0x01, 0x76, 0xB0, 0x60, // lastIndex := lastIndex + 1
                 0x00, 0x01, 0x10, 0xE1, 0x10, 0x7C], // self at: lastIndex put: anObject. ^anObject
                SArray.From(
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern("at:put:")),
                1, 0, 0));
            
            // addAll:
            orderedCollectionClass.AddMethod("addAll:", SCompiledMethod.CreateCompiledMethod(
                "addAll:",
                [0x10, 0x8F, 0x00, 0x02, 0x70, 0x11, 0xE0, 0xF7, 0x10, 0x7C], // aCollection do: [:each | self add: each]. ^aCollection
                SArray.From(
                    SSymbol.Intern("do:"),
                    SSymbol.Intern("add:")),
                1, 0, 0));
            
            // isEmpty
            orderedCollectionClass.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethod(
                "isEmpty",
                [0x00, 0x01, 0xB6, 0x7C], // firstIndex > lastIndex
                SArray.From(
                    SSymbol.Intern("firstIndex"),
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern(">")),
                0, 0, 0));
            
            // first
            orderedCollectionClass.AddMethod("first", SCompiledMethod.CreateCompiledMethod(
                "first",
                [0x70, 0x00, 0xC0, 0x7C], // self at: firstIndex
                SArray.From(
                    SSymbol.Intern("firstIndex"),
                    SSymbol.Intern("at:")),
                0, 0, 0));
            
            // last
            orderedCollectionClass.AddMethod("last", SCompiledMethod.CreateCompiledMethod(
                "last",
                [0x70, 0x00, 0xC0, 0x7C], // self at: lastIndex
                SArray.From(
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern("at:")),
                0, 0, 0));
        }
        
        // Install instance methods for Set
        var setClass = SystemDictionary.SetClass;
        if (setClass != null)
        {
            // add:
            setClass.AddMethod("add:", SCompiledMethod.CreateCompiledMethod(
                "add:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9D, 0x05, // (self at: index) isNil ifTrue:
                 0x00, 0x68, 0x10, 0xE1, // [self at: index put: anObject]
                 0x10, 0x7C], // ^anObject
                SArray.From(
                    SSymbol.Intern("findElementOrNil:"),
                    SSymbol.Intern("at:"),
                    SSymbol.Intern("isNil"),
                    SSymbol.Intern("at:put:")),
                1, 1, 0));
            
            // includes:
            setClass.AddMethod("includes:", SCompiledMethod.CreateCompiledMethod(
                "includes:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9C, 0x7C], // ^(self at: index) isNil not
                SArray.From(
                    SSymbol.Intern("findElementOrNil:"),
                    SSymbol.Intern("at:"),
                    SSymbol.Intern("isNil")),
                1, 1, 0));
        }
        
        // Install class methods for ReadStream
        var readStreamClass = SystemDictionary.ReadStreamClass;
        if (readStreamClass != null)
        {
            readStreamClass.AddMethod("on:", SCompiledMethod.CreateCompiledMethod(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                SArray.From(
                    SSymbol.Intern("basicNew"),
                    SSymbol.Intern("collection:"),
                    SSymbol.Intern("position:")),
                1, 1, 0));
        }
        
        // Install class methods for WriteStream
        var writeStreamClass = SystemDictionary.WriteStreamClass;
        if (writeStreamClass != null)
        {
            writeStreamClass.AddMethod("on:", SCompiledMethod.CreateCompiledMethod(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                SArray.From(
                    SSymbol.Intern("basicNew"),
                    SSymbol.Intern("collection:"),
                    SSymbol.Intern("position:")),
                1, 1, 0));
        }
        
        // Install class methods for Symbol
        var symbolClass = SystemDictionary.SymbolClass;
        if (symbolClass != null)
        {
            symbolClass.AddMethod("intern:", SCompiledMethod.CreateCompiledMethod(
                "intern:",
                [0x10, 0xD0, 0x7C], // aString asSymbol
                SArray.From(SSymbol.Intern("asSymbol")),
                1, 0, 0));
        }
        
        // Install class methods for Character
        var characterClass = SystemDictionary.CharacterClass;
        if (characterClass != null)
        {
            characterClass.AddMethod("value:", SCompiledMethod.CreateCompiledMethod(
                "value:",
                [0x8F, 0xAA, 0x01, 0x7C], // Primitive 170 (Character from integer)
                new SArray(),
                1, 0, 170));
        }
        
        // Install class methods for Process
        var processClass = SystemDictionary.ProcessClass;
        if (processClass != null)
        {
            // forContext:priority:
            // | newProcess |
            // newProcess := self new.
            // newProcess suspendedContext: aContext.
            // newProcess priority: anInteger.
            // ^newProcess
            var forContextLiterals = SArray.From(
                SSymbol.Intern("new"),
                SSymbol.Intern("suspendedContext:"),
                SSymbol.Intern("priority:"));
            processClass.AddMethod("forContext:priority:", SCompiledMethod.CreateCompiledMethod(
                "forContext:priority:",
                [0x70, 0xD0, 0x68,  // self new, store temp
                 0x11, 0x10, 0xF1,  // push temp, push arg0, send suspendedContext:
                 0x87,              // Pop
                 0x11, 0x12, 0xF2,  // push temp, push arg1, send priority:
                 0x87,              // Pop
                 0x11, 0x7C],       // push temp, return
                forContextLiterals,
                2, 1));
        }
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Class-side methods installation complete");
        }
    }
}