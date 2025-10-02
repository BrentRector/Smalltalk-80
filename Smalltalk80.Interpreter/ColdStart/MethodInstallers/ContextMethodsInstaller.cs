namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Installs kernel methods for context-related classes including ContextPart,
/// MethodContext, and BlockContext.
/// </summary>
public static class ContextMethodsInstaller
{
    /// <summary>
    /// Installs all context-related methods.
    /// </summary>
    public static void Install()
    {
        InstallContextPartMethods();
        InstallMethodContextMethods();
        InstallBlockContextMethods();
    }

    /// <summary>
    /// Installs ContextPart methods.
    /// </summary>
    private static void InstallContextPartMethods()
    {
        var contextPartClassRef = SystemDictionary.ContextPartClassRef;
        if (contextPartClassRef.IsNil) return;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ContextPart: 0 instance methods installed");
        }
    }

    /// <summary>
    /// Installs MethodContext methods.
    /// </summary>
    private static void InstallMethodContextMethods()
    {
        var methodContextClassRef = SystemDictionary.MethodContextClassRef;
        if (methodContextClassRef.IsNil) return;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] MethodContext: 0 instance methods installed");
        }
    }

    /// <summary>
    /// Installs BlockContext methods.
    /// </summary>
    private static void InstallBlockContextMethods()
    {
        var blockContextClassRef = SystemDictionary.BlockContextClassRef;
        if (blockContextClassRef.IsNil) return;

        var instanceMethodCount = 0;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing BlockContext instance methods: #value, #value:, #value:value:, #fork, #newProcess");
        }

        // value - primitive 81
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "value",
            [], // No fallback needed - primitive 81 cannot fail
            [],
            0, 0, 81);
        ObjectMemoryNew.AddMethod(blockContextClassRef.AsObjectPointer(), "value", method1);
        instanceMethodCount++;

        // value: - primitive 81
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "value:",
            [], // No fallback needed - primitive 81 cannot fail
            [],
            1, 0, 81);
        ObjectMemoryNew.AddMethod(blockContextClassRef.AsObjectPointer(), "value:", method2);
        instanceMethodCount++;

        // value:value: - primitive 81
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "value:value:",
            [], // No fallback needed - primitive 81 cannot fail
            [],
            2, 0, 81);
        ObjectMemoryNew.AddMethod(blockContextClassRef.AsObjectPointer(), "value:value:", method3);
        instanceMethodCount++;

        // fork
        // self newProcess resume
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "fork",
            [0x70, 0xD0,     // Push self, send newProcess
             0xD1, 0x7C],    // Send resume, return
            [
                ObjectMemoryNew.InternSymbol("newProcess"),
                ObjectMemoryNew.InternSymbol("resume")
            ],
            0, 0);
        ObjectMemoryNew.AddMethod(blockContextClassRef.AsObjectPointer(), "fork", method4);
        instanceMethodCount++;

        // newProcess
        // ^Process forContext: [self value. Processor terminateActive] priority: Processor activePriority
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "newProcess",
            [0x89, 0x00,        // Create block context
             0x06,              // Block size
             0x70, 0xD2,        // self value
             0x87,              // Pop
             0xC3, 0xD4, 0x7D,  // Processor terminateActive, blockReturn
             0xC3, 0xD5,        // Processor activePriority
             0xC0, 0xF1, 0x7C], // Process forContext:priority:, return
            [
                SystemDictionary.ProcessClassRef,
                ObjectMemoryNew.InternSymbol("forContext:priority:"),
                ObjectMemoryNew.InternSymbol("value"),
                ObjectMemoryNew.InternSymbol("Processor"),
                ObjectMemoryNew.InternSymbol("terminateActive"),
                ObjectMemoryNew.InternSymbol("activePriority")
            ],
            0, 0);
        ObjectMemoryNew.AddMethod(blockContextClassRef.AsObjectPointer(), "newProcess", method5);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] BlockContext: {instanceMethodCount} instance methods installed");
        }
    }
}
