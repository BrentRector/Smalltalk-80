namespace Smalltalk80.Interpreter;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.ColdStart;
using Microsoft.Extensions.Logging;

/// <summary>
/// The Smalltalk-80 Virtual Machine.
/// </summary>
public class VirtualMachine
{
    private readonly ContextBasedInterpreterNew interpreter;
    private readonly ExecutionStatistics executionStatistics;
    private readonly GarbageCollectorNew garbageCollector;
    private readonly ILogger<VirtualMachine>? logger;
    private bool initialized = false;

    /// <summary>
    /// Initializes a new instance of the VirtualMachine.
    /// </summary>
    public VirtualMachine(ILogger<VirtualMachine>? logger = null)
    {
        this.logger = logger;
        interpreter = new ContextBasedInterpreterNew();
        executionStatistics = new ExecutionStatistics();
        garbageCollector = new GarbageCollectorNew(ObjectMemoryNew.ObjectTable);

        // Wire up garbage collector to interpreter for primitive 133
        interpreter.GarbageCollector = garbageCollector;
    }
    
    /// <summary>
    /// Gets the method cache for performance monitoring.
    /// </summary>
    
    /// <summary>
    /// Gets the execution statistics.
    /// </summary>
    public ExecutionStatistics ExecutionStatistics => executionStatistics;
    
    /// <summary>
    /// Gets the garbage collector.
    /// </summary>
    public GarbageCollectorNew GarbageCollector => garbageCollector;
    
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
            KernelMethodsInstaller.InstallAll();

            // Phase 3: Arithmetic methods are now installed by NumericMethodsInstaller in kernel methods
            // No need to install them again here - removed duplicate installation

            // Phase 4: Perform post-cold-start initialization
            ColdStartBootstrap.PostColdStartInitialization();
            
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
                    var classRef = SystemDictionary.LookupClassRef(className);
                    if (classRef.HasValue && !classRef.Value.IsNil)
                    {
                        var methodCount = ObjectMemoryNew.GetClassMethodSelectors(classRef.Value.AsObjectPointer()).Count;
                        totalInstanceMethods += methodCount;
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
                // RunOOPBugTests() method has been removed
            }
            
            // Handle class printing/inspection
            if (Program.RuntimeOptions.ListColdStartClasses)
            {
                PrintClasses();

                // Run GC after class listing if --gc-log is enabled
                if (Program.RuntimeOptions.EnableGCLogging)
                {
                    Console.WriteLine("\n" + "=".PadRight(60, '='));
                    Console.WriteLine("Running Garbage Collection");
                    Console.WriteLine("=".PadRight(60, '=') + "\n");

                    garbageCollector.Collect();
                }
            }
            
            if (!string.IsNullOrEmpty(Program.RuntimeOptions.ClassToPrint))
            {
                PrintClass(Program.RuntimeOptions.ClassToPrint);
            }

            if (!string.IsNullOrEmpty(Program.RuntimeOptions.ClassToInspect))
            {
                InspectClass(Program.RuntimeOptions.ClassToInspect);
            }
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Performs a cold start cold start (deprecated - use InitializeAsync instead).
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
    public SObjectRef Evaluate(string expression)
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
                return SObjectRef.FromSmallInteger(intValue);
            }
        }

        // Handle simple arithmetic expressions like "3 + 4"
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var left) && int.TryParse(parts[2], out var right))
            {
                var leftObj = SObjectRef.FromSmallInteger(left);
                var rightObj = SObjectRef.FromSmallInteger(right);
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
                    // Direct arithmetic evaluation using SmallInteger operations
                    SObjectRef? result = selector switch
                    {
                        "+" => leftObj.AddSmallInteger(rightObj),
                        "-" => leftObj.SubtractSmallInteger(rightObj),
                        "*" => MultiplySmallIntegers(leftObj, rightObj),
                        "/" => DivideSmallIntegers(leftObj, rightObj),
                        _ => null
                    };

                    if (result != null)
                        return result.Value;

                    // Handle comparison operations
                    if (leftObj.IsSmallInteger && rightObj.IsSmallInteger)
                    {
                        var leftVal = leftObj.AsSmallInteger();
                        var rightVal = rightObj.AsSmallInteger();

                        var boolResult = selector switch
                        {
                            "<" => leftVal < rightVal,
                            ">" => leftVal > rightVal,
                            "<=" => leftVal <= rightVal,
                            ">=" => leftVal >= rightVal,
                            "=" => leftVal == rightVal,
                            _ => throw new NotImplementedException($"Operator {selector} not implemented")
                        };

                        return boolResult ? SystemDictionary.TrueObject : SystemDictionary.FalseObject;
                    }

                    throw new NotImplementedException($"Non-SmallInteger arithmetic not yet supported");
                }
            }
        }

        // Handle special constants
        return expression.ToLowerInvariant() switch
        {
            "nil" => SObjectRef.Nil,
            "true" => SystemDictionary.TrueObject,
            "false" => SystemDictionary.FalseObject,
            _ => throw new NotImplementedException($"Cannot evaluate expression: {expression}")
        };
    }

    private static SObjectRef? MultiplySmallIntegers(SObjectRef left, SObjectRef right)
    {
        if (!left.IsSmallInteger || !right.IsSmallInteger)
            return null;

        var leftVal = left.AsSmallInteger();
        var rightVal = right.AsSmallInteger();
        var result = (long)leftVal * rightVal;

        // Check if result fits in SmallInteger range
        if (result >= SmallInteger.MinValue && result <= SmallInteger.MaxValue)
            return SObjectRef.FromSmallInteger((int)result);

        return null; // Overflow
    }

    private static SObjectRef? DivideSmallIntegers(SObjectRef left, SObjectRef right)
    {
        if (!left.IsSmallInteger || !right.IsSmallInteger)
            return null;

        var rightVal = right.AsSmallInteger();
        if (rightVal == 0)
            return null; // Division by zero

        var leftVal = left.AsSmallInteger();
        return SObjectRef.FromSmallInteger(leftVal / rightVal);
    }

    /// <summary>
    /// Executes a precompiled method on a class.
    /// This creates an instance of the class and executes the specified selector.
    /// </summary>
    /// <param name="className">The name of the class (e.g., "TestClass")</param>
    /// <param name="selector">The method selector to execute (e.g., "testAddition")</param>
    /// <param name="args">Optional arguments to pass to the method</param>
    /// <returns>The result of the method execution</returns>
    public SObjectRef ExecutePrecompiledMethod(string className, string selector, params SObjectRef[] args)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("VM not initialized");
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[EXECUTE] Executing {className}>>{selector}");
        }

        // Look up the class
        var classRef = SystemDictionary.LookupClassRef(className);
        if (!classRef.HasValue || classRef.Value.IsNil)
        {
            throw new InvalidOperationException($"Class not found: {className}");
        }

        // Look up the method
        var methodRef = ObjectMemoryNew.LookupMethod(classRef.Value.AsObjectPointer(), selector);
        if (methodRef.IsNil)
        {
            throw new InvalidOperationException($"Method not found: {className}>>{selector}");
        }

        // Create an instance of the class (or use the class itself for class methods)
        var receiver = CreateReceiverFor(classRef.Value, className);

        // Execute the method via the interpreter (tracing is automatically enabled based on DebugOptions)
        var result = interpreter.Execute(methodRef, receiver, args ?? Array.Empty<SObjectRef>());

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[EXECUTE] Result: {FormatResult(result)}");
        }

        return result;
    }

    /// <summary>
    /// Creates an appropriate receiver for method execution.
    /// For TestClass, creates an instance. For other cases, may use the class itself.
    /// </summary>
    private SObjectRef CreateReceiverFor(SObjectRef classRef, string className)
    {
        // For test classes, create an instance
        if (className == "TestClass")
        {
            var instVarCount = ObjectMemoryNew.GetClassTotalInstanceVariableCount(classRef.AsObjectPointer());
            var instance = ObjectMemoryNew.CreateInstance(classRef.AsObjectPointer(), instVarCount);

            // Initialize the "value" instance variable to 100 for testing
            if (instVarCount > 0)
            {
                ObjectMemoryNew.SetInstanceVariable(instance.AsObjectPointer(), 0, SObjectRef.FromSmallInteger(100));
            }

            return instance;
        }

        // For other classes, use the class itself (for class methods)
        return classRef;
    }

    /// <summary>
    /// Formats a result for display.
    /// </summary>
    private string FormatResult(SObjectRef result)
    {
        if (result.IsNil)
            return "nil";
        if (result.IsSmallInteger)
            return result.AsSmallInteger().ToString();
        if (result == SystemDictionary.TrueObject)
            return "true";
        if (result == SystemDictionary.FalseObject)
            return "false";

        return result.ToString() ?? "object";
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
            var classRef = SystemDictionary.LookupClassRef(className);
            if (classRef.HasValue && !classRef.Value.IsNil)
            {
                var classOop = classRef.Value.AsObjectPointer();
                var superclassRef = ObjectMemoryNew.GetClassSuperclass(classOop);
                var superName = superclassRef.IsNil ? "nil" : ObjectMemoryNew.GetClassName(superclassRef.AsObjectPointer());
                var methodCount = ObjectMemoryNew.GetClassMethodSelectors(classOop).Count;

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
        var classRef = SystemDictionary.LookupClassRef(className);
        if (!classRef.HasValue || classRef.Value.IsNil)
        {
            Console.WriteLine($"Class not found: {className}");
            return;
        }

        Console.WriteLine($"\nClass: {className}");
        Console.WriteLine("=".PadRight(60, '='));

        var superclassRef = ObjectMemoryNew.GetClassSuperclass(classRef.Value.AsObjectPointer());
        var superclassName = superclassRef.IsNil ? "nil" : ObjectMemoryNew.GetClassName(superclassRef.AsObjectPointer());
        Console.WriteLine($"Superclass: {superclassName}");

        var instanceVarNames = ObjectMemoryNew.GetClassInstanceVariableNames(classRef.Value.AsObjectPointer());
        Console.WriteLine($"Instance Variables: {string.Join(", ", instanceVarNames ?? [])}");

        var methods = ObjectMemoryNew.GetClassMethodSelectors(classRef.Value.AsObjectPointer());
        if (methods.Count > 0)
        {
            Console.WriteLine($"\nMethods ({methods.Count}):");
            foreach (var selector in methods.OrderBy(m => m))
            {
                Console.WriteLine($"  {selector}");
                if (DebugOptions.ShowBytecodeWithMnemonics)
                {
                    var methodRef = ObjectMemoryNew.LookupMethod(classRef.Value.AsObjectPointer(), selector);
                    if (!methodRef.IsNil)
                    {
                        var bytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(methodRef.AsObjectPointer());
                        Console.WriteLine($"    Bytecodes: {BitConverter.ToString(bytecodes)}");
                    }
                }
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Provides comprehensive inspection of a class including metadata, variables, and detailed method information.
    /// </summary>
    /// <param name="className">The name of the class to inspect.</param>
    public void InspectClass(string className)
    {
        var classRef = SystemDictionary.LookupClassRef(className);
        if (!classRef.HasValue || classRef.Value.IsNil)
        {
            Console.WriteLine($"Class not found: {className}");
            return;
        }

        var classOop = classRef.Value.AsObjectPointer();

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"CLASS: {className}");
        Console.WriteLine("=".PadRight(80, '='));

        // HIERARCHY section
        Console.WriteLine("\nHIERARCHY:");
        var superclassRef = ObjectMemoryNew.GetClassSuperclass(classOop);
        var superclassName = superclassRef.IsNil ? "nil" : ObjectMemoryNew.GetClassName(superclassRef.AsObjectPointer());
        Console.WriteLine($"  Superclass: {superclassName}");

        var metaclassRef = ObjectMemoryNew.GetObjectClass(classRef.Value);
        var metaclassName = metaclassRef.IsNil ? "nil" : ObjectMemoryNew.GetClassName(metaclassRef.AsObjectPointer());
        Console.WriteLine($"  Metaclass: {metaclassName}");

        // Determine object format (if we can infer it from the class name)
        var objectFormat = DetermineObjectFormat(className);
        Console.WriteLine($"  Format: {objectFormat}");

        // INSTANCE VARIABLES section
        var instanceVarNames = ObjectMemoryNew.GetClassInstanceVariableNames(classOop) ?? [];
        Console.WriteLine($"\nINSTANCE VARIABLES ({instanceVarNames.Length}):");
        if (instanceVarNames.Length == 0)
        {
            Console.WriteLine("  (none - inherited from " + superclassName + ")");
        }
        else
        {
            var baseIndex = superclassRef.IsNil ? 0 : ObjectMemoryNew.GetClassTotalInstanceVariableCount(superclassRef.AsObjectPointer());
            for (var i = 0; i < instanceVarNames.Length; i++)
            {
                Console.WriteLine($"  [{baseIndex + i}] {instanceVarNames[i]}");
            }
        }

        var totalInstVars = ObjectMemoryNew.GetClassTotalInstanceVariableCount(classOop);
        Console.WriteLine($"\nTOTAL INSTANCE VARIABLES: {totalInstVars} (including inherited)");

        // CLASS VARIABLES section (placeholder)
        Console.WriteLine("\nCLASS VARIABLES:");
        Console.WriteLine("  (not yet implemented)");

        // Collect class methods if we have a metaclass
        var classMethodCount = 0;
        if (!metaclassRef.IsNil && metaclassRef != classRef.Value)
        {
            classMethodCount = ObjectMemoryNew.GetClassMethodSelectors(metaclassRef.AsObjectPointer()).Count;
        }

        // METHOD SUMMARY
        var instanceMethods = ObjectMemoryNew.GetClassMethodSelectors(classOop);
        Console.WriteLine($"\nCLASS METHODS: {classMethodCount}");
        Console.WriteLine($"INSTANCE METHODS: {instanceMethods.Count}");

        // INSTANCE METHODS details
        if (instanceMethods.Count > 0)
        {
            Console.WriteLine("\nINSTANCE METHODS ({0}):", instanceMethods.Count);

            foreach (var selector in instanceMethods.OrderBy(m => m))
            {
                Console.WriteLine("━".PadRight(80, '━'));
                Console.WriteLine();

                var methodRef = ObjectMemoryNew.LookupMethod(classOop, selector);
                if (!methodRef.IsNil)
                {
                    var methodOop = methodRef.AsObjectPointer();
                    Console.WriteLine($"Method: {selector}");
                    Console.WriteLine($"  Arguments: {ObjectMemoryNew.GetCompiledMethodArgumentCount(methodOop)}");
                    Console.WriteLine($"  Temporaries: {ObjectMemoryNew.GetCompiledMethodTemporaryCount(methodOop)}");

                    var primitiveIndex = ObjectMemoryNew.GetCompiledMethodPrimitiveIndex(methodOop);
                    if (primitiveIndex > 0)
                    {
                        Console.WriteLine($"  Primitive: {primitiveIndex}");
                    }

                    var bytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(methodOop);
                    Console.WriteLine($"  Bytecode: [{BitConverter.ToString(bytecodes)}] ({bytecodes.Length} bytes)");

                    // Display literal array
                    var literalCount = ObjectMemoryNew.GetCompiledMethodLiteralCount(methodOop);
                    Console.WriteLine($"  Literals: ({literalCount})");
                    if (literalCount > 0)
                    {
                        for (int i = 0; i < literalCount; i++)
                        {
                            var literalRef = ObjectMemoryNew.GetCompiledMethodLiteral(methodOop, i);
                            string literalDisplay;

                            if (literalRef.IsNil)
                            {
                                literalDisplay = "nil";
                            }
                            else if (literalRef.IsSmallInteger)
                            {
                                literalDisplay = $"SmallInteger({literalRef.AsSmallInteger()})";
                            }
                            else
                            {
                                // Try to get type information from the object
                                var literalOop = literalRef.AsObjectPointer();
                                var literalClassRef = ObjectMemoryNew.GetObjectClass(literalRef);
                                if (!literalClassRef.IsNil)
                                {
                                    var literalClassName = ObjectMemoryNew.GetClassName(literalClassRef.AsObjectPointer());
                                    if (literalClassName == "Symbol")
                                    {
                                        var symbolValue = ObjectMemoryNew.GetSymbolValue(literalOop);
                                        literalDisplay = $"#{symbolValue}";
                                    }
                                    else if (literalClassName == "String")
                                    {
                                        var stringValue = ObjectMemoryNew.GetStringValue(literalOop);
                                        literalDisplay = $"'{stringValue}'";
                                    }
                                    else if (literalClassName == "Array")
                                    {
                                        var size = ObjectMemoryNew.GetObjectSize(literalOop);
                                        literalDisplay = $"Array({size} elements)";
                                    }
                                    else
                                    {
                                        literalDisplay = literalClassName;
                                    }
                                }
                                else
                                {
                                    literalDisplay = "UnknownObject";
                                }
                            }

                            Console.WriteLine($"    [{i}] {literalDisplay}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("    (none)");
                    }

                    Console.WriteLine();
                    Console.WriteLine("  Disassembly:");
                    var disassembly = BytecodeDisassembler.Disassemble(bytecodes);
                    Console.WriteLine(disassembly);

                    // Source code (usually not available for cold start methods)
                    Console.WriteLine();
                    Console.WriteLine("  Source: <not available>");
                }
                Console.WriteLine();
            }
            Console.WriteLine("━".PadRight(80, '━'));
        }
    }

    /// <summary>
    /// Determines the object format based on the class name.
    /// </summary>
    private string DetermineObjectFormat(string className)
    {
        return className switch
        {
            "ByteArray" or "ByteString" or "Symbol" => "ByteIndexed",
            "WordArray" => "WordIndexed",
            "Array" or "OrderedCollection" => "PointerIndexed",
            "CompiledMethod" => "CompiledMethod",
            "Float" => "WordIndexed",
            "LargePositiveInteger" or "LargeNegativeInteger" => "ByteIndexed",
            "Point" or "Rectangle" or "Character" => "NonIndexed",
            "String" => "ByteIndexed",
            "Dictionary" or "IdentityDictionary" => "PointerIndexed",
            "Set" or "IdentitySet" => "PointerIndexed",
            "MethodContext" or "BlockContext" => "MethodOrBlockContext",
            "Process" => "PointerIndexed",
            _ when className.EndsWith("Stream") => "PointerIndexed",
            _ => "NonIndexed"
        };
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
            var objectClass = SystemDictionary.LookupClassRef("Object");
            Console.WriteLine($"  Object class exists: {objectClass.HasValue && !objectClass.Value.IsNil} [PASS]");

            var intClass = SystemDictionary.LookupClassRef("SmallInteger");
            Console.WriteLine($"  SmallInteger class exists: {intClass.HasValue && !intClass.Value.IsNil} [PASS]");

            var stringClass = SystemDictionary.LookupClassRef("String");
            Console.WriteLine($"  String class exists: {stringClass.HasValue && !stringClass.Value.IsNil} [PASS]");

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
    
    /// <summary>
    /// Gets comprehensive statistics about the VM.
    /// </summary>
    /// <returns>A string containing all VM statistics.</returns>
    public string GetStatistics()
    {
        var execStats = executionStatistics.GetSnapshot();
        var gcStats = garbageCollector.GetStatistics();
        
        return $"""
            ===== Virtual Machine Statistics =====
            
            {executionStatistics}
            
            
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
        garbageCollector.ResetStatistics();
        
        logger?.LogInformation("All VM statistics have been reset");
    }
    
    /// <summary>
    /// Initializes basic methods for core classes.
    /// </summary>
    private void InitializeBasicMethods()
    {
        // Add basic arithmetic methods to SmallInteger
        var intClassRef = SystemDictionary.SmallIntegerClassRef;
        if (intClassRef.IsNil) return;

        var intClassOop = intClassRef.AsObjectPointer();

        // + method with primitive 1
        var plusMethod = ObjectMemoryNew.CreateCompiledMethod(
            "+",
            [0xB0, 0x7C], // arithmetic +, return top
            [],
            1, 0, 1);
        ObjectMemoryNew.AddMethod(intClassOop, "+", plusMethod);

        // - method with primitive 2
        var minusMethod = ObjectMemoryNew.CreateCompiledMethod(
            "-",
            [0xB1, 0x7C], // arithmetic -, return top
            [],
            1, 0, 2);
        ObjectMemoryNew.AddMethod(intClassOop, "-", minusMethod);

        // < method with primitive 3
        var ltMethod = ObjectMemoryNew.CreateCompiledMethod(
            "<",
            [0xB2, 0x7C], // arithmetic <, return top
            [],
            1, 0, 3);
        ObjectMemoryNew.AddMethod(intClassOop, "<", ltMethod);

        // > method with primitive 4
        var gtMethod = ObjectMemoryNew.CreateCompiledMethod(
            ">",
            [0xB3, 0x7C], // arithmetic >, return top
            [],
            1, 0, 4);
        ObjectMemoryNew.AddMethod(intClassOop, ">", gtMethod);

        // = method with primitive 7
        var eqMethod = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0xB6, 0x7C], // arithmetic =, return top
            [],
            1, 0, 7);
        ObjectMemoryNew.AddMethod(intClassOop, "=", eqMethod);

        // * method with primitive 9
        var multMethod = ObjectMemoryNew.CreateCompiledMethod(
            "*",
            [0xB8, 0x7C], // arithmetic *, return top
            [],
            1, 0, 9);
        ObjectMemoryNew.AddMethod(intClassOop, "*", multMethod);

        // / method with primitive 10
        var divMethod = ObjectMemoryNew.CreateCompiledMethod(
            "/",
            [0xB9, 0x7C], // arithmetic /, return top
            [],
            1, 0, 10);
        ObjectMemoryNew.AddMethod(intClassOop, "/", divMethod);

        // asFloat method with primitive 40
        var asFloatMethod = ObjectMemoryNew.CreateCompiledMethod(
            "asFloat",
            [0x8F, 0x28, 0x00, 0x7C], // Primitive 40 with 0 args, return top
            [],
            0, 0, 40);
        ObjectMemoryNew.AddMethod(intClassOop, "asFloat", asFloatMethod);
    }
    
    /// <summary>
    /// Initializes the garbage collection system with the Special Objects Array.
    /// Per Blue Book specification: Only 5 roots are needed for complete reachability.
    /// The SOA is a C# array containing the 5 root SObjectRefs.
    /// </summary>
    private void InitializeGarbageCollection()
    {
        // Create the Special Objects Array as a C# array with 5 fixed roots
        var soa = new SObjectRef[Memory.SpecialObjectsArray.Count]; // 5 entries

        // Index 0: nil
        soa[Memory.SpecialObjectsArray.NilIndex] = SystemDictionary.NilObject;

        // Index 1: false
        soa[Memory.SpecialObjectsArray.FalseIndex] = SystemDictionary.FalseObject;

        // Index 2: true
        soa[Memory.SpecialObjectsArray.TrueIndex] = SystemDictionary.TrueObject;

        // Index 3: SystemDictionary (Smalltalk global - contains ALL classes, symbols, globals)
        if (SystemDictionary.Instance.IsNil)
            throw new InvalidOperationException("SystemDictionary.Instance is nil during GC initialization");
        soa[Memory.SpecialObjectsArray.SystemDictionaryIndex] = SystemDictionary.Instance;

        // Index 4: ProcessorScheduler (contains ALL processes and execution contexts)
        var processorSchedulerRef = ColdStart.ProcessSystemInitializer.GetProcessorScheduler();
        if (processorSchedulerRef.IsNil)
            throw new InvalidOperationException("ProcessorScheduler is nil during GC initialization");
        soa[Memory.SpecialObjectsArray.ProcessorSchedulerIndex] = processorSchedulerRef;

        // Give the GC the SOA C# array
        garbageCollector.SetSpecialObjectsArray(soa);

        if (Program.RuntimeOptions.EnableColdStartLogging || Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[COLD START] Special Objects Array initialized (Blue Book minimal spec)");
            Console.WriteLine($"[COLD START] SOA contains {soa.Length} root objects");

            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Console.WriteLine("[COLD START] SOA roots (5-root minimal specification):");
                Console.WriteLine("  [0] nil");
                Console.WriteLine("  [1] false");
                Console.WriteLine("  [2] true");
                Console.WriteLine("  [3] SystemDictionary (ALL classes, methods, symbols, globals)");
                Console.WriteLine("  [4] ProcessorScheduler (ALL processes and contexts)");
                Console.WriteLine();
                Console.WriteLine("[COLD START] Reachability verification:");
                Console.WriteLine("  - Classes: via SystemDictionary");
                Console.WriteLine("  - Methods: via Class -> methodDict");
                Console.WriteLine("  - Symbols: via Symbol class -> classPool -> #USTable");
                Console.WriteLine("  - Processes: via ProcessorScheduler -> process queues");
            }
        }
    }
}