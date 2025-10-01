namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Installs kernel methods for Boolean, True, and False classes.
/// </summary>
public static class BooleanMethodsInstaller
{
    /// <summary>
    /// Installs all Boolean, True, and False methods.
    /// </summary>
    public static void Install()
    {
        InstallBooleanMethods();
        InstallTrueMethods();
        InstallFalseMethods();
    }
    
    /// <summary>
    /// Installs Boolean methods.
    /// </summary>
    private static void InstallBooleanMethods()
    {
        var booleanClassRef = SystemDictionary.BooleanClassRef;
        if (booleanClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Boolean instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:");
        }
        
        // These are abstract in Boolean, implemented in True/False subclasses
        // But we'll add them as self subclassResponsibility
        
        // & (and)
        booleanClassRef.AddMethod("&", SCompiledMethod.CreateCompiledMethodRef(
            "&",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("subclassResponsibility")),
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or)
        booleanClassRef.AddMethod("|", SCompiledMethod.CreateCompiledMethodRef(
            "|",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("subclassResponsibility")),
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        booleanClassRef.AddMethod("not", SCompiledMethod.CreateCompiledMethodRef(
            "not",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("subclassResponsibility")),
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
        var trueClassRef = SystemDictionary.TrueClassRef;
        if (trueClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing True instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:, #printOn:");
        }
        
        // & (and) - depends on argument
        trueClassRef.AddMethod("&", SCompiledMethod.CreateCompiledMethodRef(
            "&",
            [0x10, 0x7C], // ^arg
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or) - always true
        trueClassRef.AddMethod("|", SCompiledMethod.CreateCompiledMethodRef(
            "|",
            [0x71, 0x7C], // ^true
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        trueClassRef.AddMethod("not", SCompiledMethod.CreateCompiledMethodRef(
            "not",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // and:
        trueClassRef.AddMethod("and:", SCompiledMethod.CreateCompiledMethodRef(
            "and:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // or:
        trueClassRef.AddMethod("or:", SCompiledMethod.CreateCompiledMethodRef(
            "or:",
            [0x71, 0x7C], // ^true
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:
        trueClassRef.AddMethod("ifTrue:", SCompiledMethod.CreateCompiledMethodRef(
            "ifTrue:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifFalse:
        trueClassRef.AddMethod("ifFalse:", SCompiledMethod.CreateCompiledMethodRef(
            "ifFalse:",
            [0x73, 0x7C], // ^nil
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:ifFalse:
        trueClassRef.AddMethod("ifTrue:ifFalse:", SCompiledMethod.CreateCompiledMethodRef(
            "ifTrue:ifFalse:",
            [0x10, 0xC9, 0x7C], // trueBlock value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        trueClassRef.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethodRef(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'true'
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("true"), SSymbol.Intern("nextPutAll:")),
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
        var falseClassRef = SystemDictionary.FalseClassRef;
        if (falseClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing False instance methods: #&, #|, #not, #and:, #or:, #ifTrue:, #ifFalse:, #ifTrue:ifFalse:, #printOn:");
        }
        
        // & (and) - always false
        falseClassRef.AddMethod("&", SCompiledMethod.CreateCompiledMethodRef(
            "&",
            [0x72, 0x7C], // ^false
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // | (or) - depends on argument
        falseClassRef.AddMethod("|", SCompiledMethod.CreateCompiledMethodRef(
            "|",
            [0x10, 0x7C], // ^arg
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // not
        falseClassRef.AddMethod("not", SCompiledMethod.CreateCompiledMethodRef(
            "not",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // and:
        falseClassRef.AddMethod("and:", SCompiledMethod.CreateCompiledMethodRef(
            "and:",
            [0x72, 0x7C], // ^false
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // or:
        falseClassRef.AddMethod("or:", SCompiledMethod.CreateCompiledMethodRef(
            "or:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:
        falseClassRef.AddMethod("ifTrue:", SCompiledMethod.CreateCompiledMethodRef(
            "ifTrue:",
            [0x73, 0x7C], // ^nil
            [],
            1, 0, 0));
        instanceMethodCount++;
        
        // ifFalse:
        falseClassRef.AddMethod("ifFalse:", SCompiledMethod.CreateCompiledMethodRef(
            "ifFalse:",
            [0x10, 0xC9, 0x7C], // arg value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 0, 0));
        instanceMethodCount++;
        
        // ifTrue:ifFalse:
        falseClassRef.AddMethod("ifTrue:ifFalse:", SCompiledMethod.CreateCompiledMethodRef(
            "ifTrue:ifFalse:",
            [0x11, 0xC9, 0x7C], // falseBlock value
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            2, 0, 0));
        instanceMethodCount++;
        
        // printOn:
        falseClassRef.AddMethod("printOn:", SCompiledMethod.CreateCompiledMethodRef(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'false'
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("false"), SSymbol.Intern("nextPutAll:")),
            1, 0, 0));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] False: {instanceMethodCount} instance methods installed");
        }
    }
}
