namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Installs kernel methods for behavior-related classes including Behavior, 
/// ClassDescription, Class, Metaclass, and CompiledMethod.
/// </summary>
public static class BehaviorMethodsInstaller
{
    /// <summary>
    /// Installs all behavior-related methods.
    /// </summary>
    public static void Install()
    {
        InstallBehaviorMethods();
        InstallClassDescriptionMethods();
        InstallClassMethods();
        InstallMetaclassMethods();
        InstallCompiledMethodMethods();
    }
    
    /// <summary>
    /// Installs Behavior methods.
    /// </summary>
    private static void InstallBehaviorMethods()
    {
        var behaviorClassRef = SystemDictionary.BehaviorClassRef;
        if (behaviorClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Behavior instance methods: #new, #new:, #basicNew, #basicNew:, #compile:, #addSelector:withMethod:");
        }
        
        // new
        behaviorClassRef.AddMethod("new", SCompiledMethod.CreateCompiledMethodRef(
            "new",
            [0x70, 0xCB, 0xD2, 0x7C], // self basicNew initialize
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew"), SSymbol.Intern("initialize")),
            0, 0, 0));
        instanceMethodCount++;
        
        // new: with size
        behaviorClassRef.AddMethod("new:", SCompiledMethod.CreateCompiledMethodRef(
            "new:",
            [0x70, 0x10, 0xCC, 0xD2, 0x7C], // self basicNew: size initialize
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew:"), SSymbol.Intern("initialize")),
            1, 0, 0));
        instanceMethodCount++;
        
        // basicNew - primitive 70
        behaviorClassRef.AddMethod("basicNew", SCompiledMethod.CreateCompiledMethodRef(
            "basicNew",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 70)
            [],
            0, 0, 70));
        instanceMethodCount++;
        
        // basicNew: - primitive 71
        behaviorClassRef.AddMethod("basicNew:", SCompiledMethod.CreateCompiledMethodRef(
            "basicNew:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 71)
            [],
            1, 0, 71));
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Behavior: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs ClassDescription methods.
    /// </summary>
    private static void InstallClassDescriptionMethods()
    {
        var classDescriptionClassRef = SystemDictionary.ClassDescriptionClassRef;
        if (classDescriptionClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] ClassDescription: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Class methods.
    /// </summary>
    private static void InstallClassMethods()
    {
        var classClassRef = SystemDictionary.ClassClassRef;
        if (classClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Class: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Metaclass methods.
    /// </summary>
    private static void InstallMetaclassMethods()
    {
        var metaclassClassRef = SystemDictionary.MetaclassClassRef;
        if (metaclassClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Metaclass: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs CompiledMethod methods.
    /// </summary>
    private static void InstallCompiledMethodMethods()
    {
        var compiledMethodClassRef = SystemDictionary.CompiledMethodClassRef;
        if (compiledMethodClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] CompiledMethod: 0 instance methods installed");
        }
    }
}
