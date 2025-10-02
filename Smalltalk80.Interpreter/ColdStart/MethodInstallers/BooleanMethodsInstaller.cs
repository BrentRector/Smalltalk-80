namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

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
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "&",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(booleanClassRef.AsObjectPointer(), "&", method1);
        instanceMethodCount++;

        // | (or)
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "|",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(booleanClassRef.AsObjectPointer(), "|", method2);
        instanceMethodCount++;

        // not
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "not",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(booleanClassRef.AsObjectPointer(), "not", method3);
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
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "&",
            [0x10, 0x7C], // ^arg
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "&", method1);
        instanceMethodCount++;

        // | (or) - always true
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "|",
            [0x71, 0x7C], // ^true
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "|", method2);
        instanceMethodCount++;

        // not
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "not",
            [0x72, 0x7C], // ^false
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "not", method3);
        instanceMethodCount++;

        // and:
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "and:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "and:", method4);
        instanceMethodCount++;

        // or:
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "or:",
            [0x71, 0x7C], // ^true
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "or:", method5);
        instanceMethodCount++;

        // ifTrue:
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "ifTrue:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "ifTrue:", method6);
        instanceMethodCount++;

        // ifFalse:
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "ifFalse:",
            [0x73, 0x7C], // ^nil
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "ifFalse:", method7);
        instanceMethodCount++;

        // ifTrue:ifFalse:
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "ifTrue:ifFalse:",
            [0x10, 0xC9, 0x7C], // trueBlock value
            [ObjectMemoryNew.InternSymbol("value")],
            2, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "ifTrue:ifFalse:", method8);
        instanceMethodCount++;

        // printOn:
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'true'
            [ObjectMemoryNew.InternSymbol("true"), ObjectMemoryNew.InternSymbol("nextPutAll:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(trueClassRef.AsObjectPointer(), "printOn:", method9);
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
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "&",
            [0x72, 0x7C], // ^false
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "&", method1);
        instanceMethodCount++;

        // | (or) - depends on argument
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "|",
            [0x10, 0x7C], // ^arg
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "|", method2);
        instanceMethodCount++;

        // not
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "not",
            [0x71, 0x7C], // ^true
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "not", method3);
        instanceMethodCount++;

        // and:
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "and:",
            [0x72, 0x7C], // ^false
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "and:", method4);
        instanceMethodCount++;

        // or:
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "or:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "or:", method5);
        instanceMethodCount++;

        // ifTrue:
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "ifTrue:",
            [0x73, 0x7C], // ^nil
            [],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "ifTrue:", method6);
        instanceMethodCount++;

        // ifFalse:
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "ifFalse:",
            [0x10, 0xC9, 0x7C], // arg value
            [ObjectMemoryNew.InternSymbol("value")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "ifFalse:", method7);
        instanceMethodCount++;

        // ifTrue:ifFalse:
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "ifTrue:ifFalse:",
            [0x11, 0xC9, 0x7C], // falseBlock value
            [ObjectMemoryNew.InternSymbol("value")],
            2, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "ifTrue:ifFalse:", method8);
        instanceMethodCount++;

        // printOn:
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "printOn:",
            [0x10, 0x21, 0xE0, 0x7C], // stream nextPutAll: 'false'
            [ObjectMemoryNew.InternSymbol("false"), ObjectMemoryNew.InternSymbol("nextPutAll:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(falseClassRef.AsObjectPointer(), "printOn:", method9);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] False: {instanceMethodCount} instance methods installed");
        }
    }
}
