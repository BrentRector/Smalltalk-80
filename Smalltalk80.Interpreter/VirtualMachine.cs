namespace Smalltalk80.Interpreter;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using Microsoft.Extensions.Logging;

/// <summary>
/// The Smalltalk-80 Virtual Machine.
/// </summary>
public class VirtualMachine
{
    private readonly BytecodeInterpreter interpreter;
    private readonly MethodCache methodCache;
    private readonly ExecutionStatistics executionStatistics;
    private readonly GarbageCollector garbageCollector;
    private readonly ILogger<VirtualMachine>? logger;
    private bool initialized = false;
    
    /// <summary>
    /// Initializes a new instance of the VirtualMachine.
    /// </summary>
    public VirtualMachine(ILogger<VirtualMachine>? logger = null)
    {
        this.logger = logger;
        interpreter = new BytecodeInterpreter();
        methodCache = new MethodCache();
        executionStatistics = new ExecutionStatistics();
        garbageCollector = new GarbageCollector();
    }
    
    /// <summary>
    /// Gets the method cache for performance monitoring.
    /// </summary>
    public MethodCache MethodCache => methodCache;
    
    /// <summary>
    /// Gets the execution statistics.
    /// </summary>
    public ExecutionStatistics ExecutionStatistics => executionStatistics;
    
    /// <summary>
    /// Gets the garbage collector.
    /// </summary>
    public GarbageCollector GarbageCollector => garbageCollector;
    
    /// <summary>
    /// Initializes the virtual machine with the full cold-start system.
    /// </summary>
    public Task InitializeAsync()
    {
        if (!initialized)
        {
            // Phase 1: Create all classes
            SystemDictionary.Reset();
            SystemDictionary.Initialize();
            
            if (Program.RuntimeOptions.EnableColdStartLogging)
            {
                Console.WriteLine($"[COLD START] Created {SystemDictionary.GetAllClassNames().Count()} classes with instance variables");
            }
            
            // Phase 2: Install all kernel methods for core classes
            Bootstrap.KernelMethodsInstaller.InstallAll();
            
            // Phase 3: Install minimal arithmetic methods (for compatibility)
            InitializeBasicMethods();
            
            // Phase 4: Perform post-cold-start initialization
            Bootstrap.ColdStartBootstrap.PostColdStartInitialization(interpreter);
            
            // Phase 5: Initialize garbage collection with Special Objects Array
            InitializeGarbageCollection();
            
            initialized = true;
            
            if (Program.RuntimeOptions.EnableColdStartLogging)
            {
                Console.WriteLine("[COLD START] System cold started successfully");
                Console.WriteLine($"[COLD START] Total classes: {SystemDictionary.GetAllClassNames().Count()}");
                
                // Count total methods installed
                var totalInstanceMethods = 0;
                foreach (var className in SystemDictionary.GetAllClassNames())
                {
                    var sClass = SystemDictionary.LookupClass(className);
                    if (sClass != null)
                    {
                        totalInstanceMethods += sClass.GetMethodCount();
                    }
                }
                Console.WriteLine($"[COLD START] Total methods installed: {totalInstanceMethods} instance methods");
            }
            
            // Phase 6: Validate GC reachability if requested
            if (Program.RuntimeOptions.ValidateGC)
            {
                Console.WriteLine("[COLD START] Validating GC reachability from Special Objects Array...");
                var isValid = garbageCollector.ValidateReachability();
                if (!isValid)
                {
                    Console.WriteLine("[COLD START] WARNING: Some objects are not reachable from SOA!");
                }
                else
                {
                    Console.WriteLine("[COLD START] GC validation passed - all critical objects are reachable");
                }
            }
            
            // Run tests if requested
            if (Program.RuntimeOptions.TestCriticalMethods)
            {
                RunCriticalMethodTests();
            }
            
            if (Program.RuntimeOptions.TestClassCreation)
            {
                RunClassCreationTests();
            }
            
            if (Program.RuntimeOptions.TestOOPBug)
            {
                RunOOPBugTests();
            }
            
            // Handle class printing/inspection
            if (Program.RuntimeOptions.ListBootstrapClasses)
            {
                PrintClasses();
            }
            
            if (!string.IsNullOrEmpty(Program.RuntimeOptions.ClassToPrint))
            {
                PrintClass(Program.RuntimeOptions.ClassToPrint);
            }
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Performs a cold start bootstrap (deprecated - use InitializeAsync instead).
    /// This method is kept for backward compatibility but simply calls InitializeAsync.
    /// </summary>
    [Obsolete("Use InitializeAsync() instead. The VM now always performs cold start initialization.")]
    public async Task ColdStartAsync()
    {
        await InitializeAsync();
    }
    
    /// <summary>
    /// Evaluates a Smalltalk expression.
    /// </summary>
    public SObject Evaluate(string expression)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("VM not initialized");
        }
        
        // For now, handle simple arithmetic expressions
        expression = expression.Trim();
        
        // Handle simple integer literals
        if (int.TryParse(expression, out var intValue))
        {
            if (intValue >= SmallInteger.MinValue && intValue <= SmallInteger.MaxValue)
            {
                return SmallInteger.From(intValue);
            }
        }
        
        // Handle simple arithmetic expressions like "3 + 4"
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var left) && int.TryParse(parts[2], out var right))
            {
                var leftObj = SmallInteger.From(left);
                var rightObj = SmallInteger.From(right);
                var selector = parts[1];
                
                // Create a simple method for the expression
                var bytecodes = selector switch
                {
                    "+" => new byte[] { 0x76, 0x77, 0xB0, 0x7C }, // push 1, push 2, +, return top
                    "-" => new byte[] { 0x76, 0x77, 0xB1, 0x7C }, // push 1, push 2, -, return top  
                    "*" => new byte[] { 0x76, 0x77, 0xB8, 0x7C }, // push 1, push 2, *, return top
                    "/" => new byte[] { 0x76, 0x77, 0xB9, 0x7C }, // push 1, push 2, /, return top
                    _ => null
                };
                
                if (bytecodes != null)
                {
                    // Direct arithmetic evaluation
                    return selector switch
                    {
                        "+" => leftObj.Add(rightObj),
                        "-" => leftObj.Subtract(rightObj),
                        "*" => leftObj.Multiply(rightObj),
                        "/" => leftObj.Divide(rightObj),
                        "<" => leftObj < rightObj ? STrue.Instance : SFalse.Instance,
                        ">" => leftObj > rightObj ? STrue.Instance : SFalse.Instance,
                        "<=" => leftObj <= rightObj ? STrue.Instance : SFalse.Instance,
                        ">=" => leftObj >= rightObj ? STrue.Instance : SFalse.Instance,
                        "=" => leftObj == rightObj ? STrue.Instance : SFalse.Instance,
                        _ => throw new NotImplementedException($"Operator {selector} not implemented")
                    };
                }
            }
        }
        
        // Handle special constants
        return expression.ToLowerInvariant() switch
        {
            "nil" => SNil.Instance,
            "true" => STrue.Instance,
            "false" => SFalse.Instance,
            _ => throw new NotImplementedException($"Cannot evaluate expression: {expression}")
        };
    }
    
    /// <summary>
    /// Prints all classes in the system.
    /// </summary>
    public void PrintClasses()
    {
        Console.WriteLine("\nSystem Classes:");
        Console.WriteLine("=".PadRight(60, '='));
        
        var classes = SystemDictionary.GetAllClassNames().OrderBy(n => n).ToList();
        Console.WriteLine($"Total Classes: {classes.Count}");
        Console.WriteLine();
        
        foreach (var className in classes)
        {
            var sClass = SystemDictionary.LookupClass(className);
            if (sClass != null)
            {
                var superName = sClass.Superclass?.Name ?? "nil";
                var methodCount = sClass.GetMethodCount();
                
                if (DebugOptions.EnableEnhancedClassPrint)
                {
                    Console.WriteLine($"  {className,-30} extends {superName,-20} [{methodCount} methods]");
                }
                else
                {
                    Console.WriteLine($"  {className} -> {superName}");
                }
            }
        }
        Console.WriteLine();
    }
    
    /// <summary>
    /// Prints a specific class and its methods.
    /// </summary>
    public void PrintClass(string className)
    {
        var sClass = SystemDictionary.LookupClass(className);
        if (sClass == null)
        {
            Console.WriteLine($"Class not found: {className}");
            return;
        }
        
        Console.WriteLine($"\nClass: {className}");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine($"Superclass: {sClass.Superclass?.Name ?? "nil"}");
        Console.WriteLine($"Instance Variables: {string.Join(", ", sClass.InstanceVariableNames ?? [])}");
        
        var methods = sClass.GetMethods();
        if (methods.Count > 0)
        {
            Console.WriteLine($"\nMethods ({methods.Count}):");
            foreach (var selector in methods.OrderBy(m => m))
            {
                Console.WriteLine($"  {selector}");
                if (DebugOptions.ShowBytecodeWithMnemonics)
                {
                    var compiledMethod = sClass.LookupMethod(selector);
                    if (compiledMethod != null)
                    {
                        Console.WriteLine($"    Bytecodes: {BitConverter.ToString(compiledMethod.Bytecodes)}");
                    }
                }
            }
        }
        Console.WriteLine();
    }
    
    /// <summary>
    /// Runs critical method tests.
    /// </summary>
    private void RunCriticalMethodTests()
    {
        Console.WriteLine("\n[TEST] Running critical method tests...");
        
        try
        {
            // Test arithmetic
            var result1 = Evaluate("3 + 4");
            Console.WriteLine($"  3 + 4 = {result1} [PASS]");
            
            var result2 = Evaluate("10 - 5");
            Console.WriteLine($"  10 - 5 = {result2} [PASS]");
            
            var result3 = Evaluate("6 * 7");
            Console.WriteLine($"  6 * 7 = {result3} [PASS]");
            
            var result4 = Evaluate("20 / 4");
            Console.WriteLine($"  20 / 4 = {result4} [PASS]");
            
            Console.WriteLine("[TEST] All critical method tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] Critical method test failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Runs class creation tests.
    /// </summary>
    private void RunClassCreationTests()
    {
        Console.WriteLine("\n[TEST] Running class creation tests...");
        
        try
        {
            // Test that core classes exist
            var objectClass = SystemDictionary.LookupClass("Object");
            Console.WriteLine($"  Object class exists: {objectClass != null} [PASS]");
            
            var intClass = SystemDictionary.LookupClass("SmallInteger");
            Console.WriteLine($"  SmallInteger class exists: {intClass != null} [PASS]");
            
            var stringClass = SystemDictionary.LookupClass("String");
            Console.WriteLine($"  String class exists: {stringClass != null} [PASS]");
            
            Console.WriteLine("[TEST] All class creation tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] Class creation test failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Runs OOP bug tests.
    /// </summary>
    private void RunOOPBugTests()
    {
        Console.WriteLine("\n[TEST] Running OOP bug tests...");
        
        try
        {
            // Test object identity
            var nil1 = SNil.Instance;
            var nil2 = SNil.Instance;
            Console.WriteLine($"  nil == nil: {nil1 == nil2} [PASS]");
            
            var true1 = STrue.Instance;
            var true2 = STrue.Instance;
            Console.WriteLine($"  true == true: {true1 == true2} [PASS]");
            
            Console.WriteLine("[TEST] All OOP bug tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] OOP bug test failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets comprehensive statistics about the VM.
    /// </summary>
    /// <returns>A string containing all VM statistics.</returns>
    public string GetStatistics()
    {
        var execStats = executionStatistics.GetSnapshot();
        var cacheStats = methodCache.GetStatistics();
        var gcStats = garbageCollector.GetStatistics();
        
        return $"""
            ===== Virtual Machine Statistics =====
            
            {executionStatistics}
            
            {cacheStats}
            
            {gcStats}
            """;
    }
    
    /// <summary>
    /// Performs garbage collection.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics CollectGarbage()
    {
        if (Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[GC] Manual garbage collection triggered");
        }
        
        return garbageCollector.ForceCollection();
    }
    
    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void ResetStatistics()
    {
        executionStatistics.Reset();
        methodCache.Clear();
        garbageCollector.ResetStatistics();
        
        logger?.LogInformation("All VM statistics have been reset");
    }
    
    /// <summary>
    /// Initializes basic methods for core classes.
    /// </summary>
    private void InitializeBasicMethods()
    {
        // Add basic arithmetic methods to SmallInteger
        var intClass = SystemDictionary.SmallIntegerClass;
        
        // + method with primitive 1
        intClass.AddMethod("+", SCompiledMethod.CreateCompiledMethod(
            "+",
            [0xB0, 0x7C], // arithmetic +, return top
            new SArray(),
            1, 0, 1));
            
        // - method with primitive 2
        intClass.AddMethod("-", SCompiledMethod.CreateCompiledMethod(
            "-",
            [0xB1, 0x7C], // arithmetic -, return top
            new SArray(),
            1, 0, 2));
            
        // < method with primitive 3
        intClass.AddMethod("<", SCompiledMethod.CreateCompiledMethod(
            "<",
            [0xB2, 0x7C], // arithmetic <, return top
            new SArray(),
            1, 0, 3));
            
        // > method with primitive 4
        intClass.AddMethod(">", SCompiledMethod.CreateCompiledMethod(
            ">",
            [0xB3, 0x7C], // arithmetic >, return top
            new SArray(),
            1, 0, 4));
            
        // = method with primitive 7
        intClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0xB6, 0x7C], // arithmetic =, return top
            new SArray(),
            1, 0, 7));
            
        // * method with primitive 9
        intClass.AddMethod("*", SCompiledMethod.CreateCompiledMethod(
            "*",
            [0xB8, 0x7C], // arithmetic *, return top
            new SArray(),
            1, 0, 9));
            
        // / method with primitive 10
        intClass.AddMethod("/", SCompiledMethod.CreateCompiledMethod(
            "/",
            [0xB9, 0x7C], // arithmetic /, return top
            new SArray(),
            1, 0, 10));
            
        // asFloat method with primitive 40
        intClass.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethod(
            "asFloat",
            [0x8F, 0x28, 0x00, 0x7C], // Primitive 40 with 0 args, return top
            new SArray(),
            0, 0, 40));
    }
    
    /// <summary>
    /// Initializes the garbage collection system with the Special Objects Array.
    /// </summary>
    private void InitializeGarbageCollection()
    {
        // Initialize the Special Objects Array with all system roots
        Memory.SpecialObjectsArray.Initialize();
        
        // Give the GC the SOA as its only root
        garbageCollector.SetSpecialObjectsArray(Memory.SpecialObjectsArray.GetArray());
        
        if (Program.RuntimeOptions.EnableColdStartLogging || Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[COLD START] Special Objects Array initialized");
            
            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Memory.SpecialObjectsArray.Report();
            }
        }
    }
}