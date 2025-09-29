namespace Smalltalk80.Interpreter.Bootstrap;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using System.Text;

/// <summary>
/// Handles cold start bootstrapping and post-initialization class creation.
/// </summary>
public static class ColdStartBootstrap
{
    /// <summary>
    /// Performs post-cold-start initialization.
    /// Float class is now created in SystemDictionary.Initialize() as part of the complete hierarchy.
    /// </summary>
    public static void PostColdStartInitialization(BytecodeInterpreter interpreter)
    {
        // Float class is now created in SystemDictionary.Initialize()
        // Just ensure Float methods are installed
        EnsureFloatMethods();
        
        // Add asFloat method to SmallInteger class
        AddSmallIntegerFloatMethods();
        
        // Initialize Transcript if needed
        InitializeTranscript();
        
        // Install compilation methods for self-hosted compilation
        CompilationMethodsInstaller.InstallAll();
    }
    
    /// <summary>
    /// Ensures Float methods are installed on the Float class.
    /// The Float class is created in SystemDictionary.Initialize() as part of the complete hierarchy.
    /// </summary>
    private static void EnsureFloatMethods()
    {
        var floatClass = SystemDictionary.FloatClass;
        if (floatClass == null)
        {
            Console.WriteLine("[WARNING] Float class not found in SystemDictionary");
            return;
        }
        
        // Register the Float class with SFloat so instances can find their class
        SFloat.SetFloatClass(floatClass);
        
        // Add Float-specific methods
        AddFloatMethods(floatClass);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            var floatMethodCount = floatClass.GetMethodCount();
            Console.WriteLine($"[COLD START] Float: {floatMethodCount} instance methods, 0 class methods installed");
        }
    }
    
    /// <summary>
    /// Initializes the Transcript global for console output.
    /// </summary>
    private static void InitializeTranscript()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Initializing Transcript...");
        }
        
        var textCollectorClass = SystemDictionary.TextCollectorClass;
        if (textCollectorClass != null)
        {
            // Create a simple Transcript instance
            var transcript = textCollectorClass.NewInstance();
            SystemDictionary.RegisterGlobal("Transcript", transcript);
            
            // Install Transcript-specific primitive methods
            InstallTranscriptMethods(textCollectorClass);
            
            if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
            {
                Console.WriteLine("[COLD START] Transcript initialized and registered as global");
            }
        }
    }
    
    /// <summary>
    /// Installs console output methods on TextCollector class.
    /// </summary>
    private static void InstallTranscriptMethods(SClass textCollectorClass)
    {
        // Add show: method with primitive 253 (console output)
        textCollectorClass.AddMethod("show:", new SCompiledMethod(
            "show:",
            [0x8F, 0xFD, 0x01, 0x7C], // Primitive 253 with 1 arg, return top
            new SArray(),
            1, 0, 253));
        
        // Add cr method with primitive 254 (console newline)
        textCollectorClass.AddMethod("cr", new SCompiledMethod(
            "cr",
            [0x8F, 0xFE, 0x00, 0x7C], // Primitive 254 with 0 args, return top
            new SArray(),
            0, 0, 254));
        
        // Add clear method with primitive 252 (console clear)
        textCollectorClass.AddMethod("clear", new SCompiledMethod(
            "clear",
            [0x8F, 0xFC, 0x00, 0x7C], // Primitive 252 with 0 args, return top
            new SArray(),
            0, 0, 252));
    }
    
    /// <summary>
    /// Adds Float-specific methods using Blue Book primitives 40-54.
    /// </summary>
    private static void AddFloatMethods(SClass floatClass)
    {
        // Add asFloat method (returns self)
        floatClass.AddMethod("asFloat", new SCompiledMethod(
            "asFloat",
            [0x70, 0x7C], // Push receiver, return top
            new SArray(),
            0, 0, 0));
        
        // Add + method with primitive 41
        floatClass.AddMethod("+", new SCompiledMethod(
            "+",
            [0x8F, 0x29, 0x01, 0x7C], // Primitive 41 with 1 arg, return top
            new SArray(),
            1, 0, 41));
        
        // Add - method with primitive 42
        floatClass.AddMethod("-", new SCompiledMethod(
            "-",
            [0x8F, 0x2A, 0x01, 0x7C], // Primitive 42 with 1 arg, return top
            new SArray(),
            1, 0, 42));
        
        // Add * method with primitive 49
        floatClass.AddMethod("*", new SCompiledMethod(
            "*",
            [0x8F, 0x31, 0x01, 0x7C], // Primitive 49 with 1 arg, return top
            new SArray(),
            1, 0, 49));
        
        // Add / method with primitive 50
        floatClass.AddMethod("/", new SCompiledMethod(
            "/",
            [0x8F, 0x32, 0x01, 0x7C], // Primitive 50 with 1 arg, return top
            new SArray(),
            1, 0, 50));
        
        // Add < method with primitive 43
        floatClass.AddMethod("<", new SCompiledMethod(
            "<",
            [0x8F, 0x2B, 0x01, 0x7C], // Primitive 43 with 1 arg, return top
            new SArray(),
            1, 0, 43));
        
        // Add > method with primitive 44
        floatClass.AddMethod(">", new SCompiledMethod(
            ">",
            [0x8F, 0x2C, 0x01, 0x7C], // Primitive 44 with 1 arg, return top
            new SArray(),
            1, 0, 44));
        
        // Add = method with primitive 47
        floatClass.AddMethod("=", new SCompiledMethod(
            "=",
            [0x8F, 0x2F, 0x01, 0x7C], // Primitive 47 with 1 arg, return top
            new SArray(),
            1, 0, 47));
        
        // Add truncate method with primitive 51
        floatClass.AddMethod("truncate", new SCompiledMethod(
            "truncate",
            [0x8F, 0x33, 0x00, 0x7C], // Primitive 51 with 0 args, return top
            new SArray(),
            0, 0, 51));
    }
    
    /// <summary>
    /// Adds Float-related methods to SmallInteger class.
    /// </summary>
    private static void AddSmallIntegerFloatMethods()
    {
        var smallIntegerClass = SystemDictionary.SmallIntegerClass;
        
        // Add asFloat method with primitive 40
        smallIntegerClass.AddMethod("asFloat", new SCompiledMethod(
            "asFloat",
            [0x8F, 0x28, 0x00, 0x7C], // Primitive 40 with 0 args, return top
            new SArray(),
            0, 0, 40));
            
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Added asFloat method to SmallInteger class");
        }
    }
}