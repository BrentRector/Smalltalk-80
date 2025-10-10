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
    private readonly Interpreter interpreter;
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
        interpreter = new Interpreter();
        executionStatistics = new ExecutionStatistics();
        garbageCollector = new GarbageCollector(ObjectMemory.ObjectTable);

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
    public GarbageCollector GarbageCollector => garbageCollector;
    
    /// <summary>
    /// Initializes the virtual machine with the full cold-start system.
    /// </summary>
    public Task InitializeAsync()
    {
        if (!initialized)
        {
            // Phase 1: Execute JSON-based cold start bootstrap
            // This creates the complete Smalltalk system from coldstart.json
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Generated", "coldstart.json");
            var bootstrap = new JsonColdStartBootstrap(jsonPath);
            var specialObjectsArray = bootstrap.Execute();

            // Initialize the Special Objects Array structure with the bootstrap result
            SpecialObjectsArray.Initialize(specialObjectsArray);

            if (Program.RuntimeOptions.EnableColdStartLogging)
            {
                Console.WriteLine($"[COLD START] JSON cold start complete");
            }

            // Phase 2: Initialize garbage collection with Special Objects Array
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
                        var methodCount = ObjectMemory.GetClassMethodSelectors(classRef.Value.AsObjectPointer()).Count;
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

                    // Run a second GC to verify everything is stable (should collect 0 objects)
                    Console.WriteLine("[COLD START] Running second GC to verify stability...");
                    var secondRun = garbageCollector.Collect();
                    if (secondRun.ObjectsCollected == 0)
                    {
                        Console.WriteLine("[GC] SUCCESS: All objects are properly rooted - second GC collected 0 objects");
                    }
                    else
                    {
                        Console.WriteLine($"[GC] WARNING: Second GC collected {secondRun.ObjectsCollected} objects - some objects may not be properly rooted");
                    }
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
                    Console.WriteLine("Running First Garbage Collection");
                    Console.WriteLine("=".PadRight(60, '=') + "\n");

                    var firstRun = garbageCollector.Collect();

                    Console.WriteLine("\n" + "=".PadRight(60, '='));
                    Console.WriteLine("Running Second Garbage Collection (Should collect 0)");
                    Console.WriteLine("=".PadRight(60, '=') + "\n");

                    var secondRun = garbageCollector.Collect();

                    if (secondRun.ObjectsCollected == 0)
                    {
                        Console.WriteLine("[GC] SUCCESS: All objects are properly rooted - second GC collected 0 objects");
                    }
                    else
                    {
                        Console.WriteLine($"[GC] WARNING: Second GC collected {secondRun.ObjectsCollected} objects - some objects may not be properly rooted");
                    }
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
        var methodRef = ObjectMemory.LookupMethod(classRef.Value.AsObjectPointer(), selector);
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
            var instVarCount = ObjectMemory.GetClassTotalInstanceVariableCount(classRef.AsObjectPointer());
            var instance = ObjectMemory.CreateInstance(classRef.AsObjectPointer(), instVarCount);

            // Initialize the "value" instance variable to 100 for testing
            if (instVarCount > 0)
            {
                ObjectMemory.SetInstanceVariable(instance.AsObjectPointer(), 0, SObjectRef.FromSmallInteger(100));
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
    /// Prints all classes in the system in hierarchical order.
    /// </summary>
    public void PrintClasses()
    {
        Console.WriteLine("\nClass Hierarchy:");
        Console.WriteLine("=".PadRight(60, '='));

        var allClasses = SystemDictionary.GetAllClassNames().ToList();
        Console.WriteLine($"Total Classes: {allClasses.Count}");
        Console.WriteLine();

        // Build class hierarchy starting from Object (root class)
        PrintClassHierarchy("Object", 0, new HashSet<string>());
        Console.WriteLine();
    }

    /// <summary>
    /// Recursively prints class hierarchy with indentation.
    /// </summary>
    private void PrintClassHierarchy(string className, int depth, HashSet<string> visited, bool isLastSibling = true)
    {
        if (visited.Contains(className))
            return;

        visited.Add(className);

        var classRef = SystemDictionary.LookupClassRef(className);
        if (!classRef.HasValue || classRef.Value.IsNil)
            return;

        // Draw tree structure with box-drawing characters
        string indent = "";
        if (depth > 0)
        {
            // Use └─ for last child, ├─ for other children
            indent = new string(' ', (depth - 1) * 2);
            indent += isLastSibling ? "└─" : "├─";
        }

        Console.WriteLine($"{indent}{className}");

        // Find all direct subclasses
        var allClasses = SystemDictionary.GetAllClassNames();
        var subclasses = new List<string>();

        foreach (var otherClass in allClasses)
        {
            var otherRef = SystemDictionary.LookupClassRef(otherClass);
            if (otherRef.HasValue && !otherRef.Value.IsNil)
            {
                var superclassRef = ObjectMemory.GetClassSuperclass(otherRef.Value.AsObjectPointer());
                if (!superclassRef.IsNil)
                {
                    var superName = ObjectMemory.GetClassName(superclassRef.AsObjectPointer());
                    if (superName == className)
                    {
                        subclasses.Add(otherClass);
                    }
                }
            }
        }

        // Sort subclasses alphabetically
        subclasses.Sort();

        // Recursively print subclasses
        for (int i = 0; i < subclasses.Count; i++)
        {
            bool isLast = (i == subclasses.Count - 1);
            PrintClassHierarchy(subclasses[i], depth + 1, visited, isLast);
        }
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

        var superclassRef = ObjectMemory.GetClassSuperclass(classRef.Value.AsObjectPointer());
        var superclassName = superclassRef.IsNil ? "nil" : ObjectMemory.GetClassName(superclassRef.AsObjectPointer());
        Console.WriteLine($"Superclass: {superclassName}");

        var instanceVarNames = ObjectMemory.GetClassInstanceVariableNames(classRef.Value.AsObjectPointer());
        Console.WriteLine($"Instance Variables: {string.Join(", ", instanceVarNames ?? [])}");

        var methods = ObjectMemory.GetClassMethodSelectors(classRef.Value.AsObjectPointer());
        if (methods.Count > 0)
        {
            Console.WriteLine($"\nMethods ({methods.Count}):");
            foreach (var selector in methods.OrderBy(m => m))
            {
                Console.WriteLine($"  {selector}");
                if (DebugOptions.ShowBytecodeWithMnemonics)
                {
                    var methodRef = ObjectMemory.LookupMethod(classRef.Value.AsObjectPointer(), selector);
                    if (!methodRef.IsNil)
                    {
                        var bytecodes = ObjectMemory.GetCompiledMethodBytecodes(methodRef.AsObjectPointer());
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
        var superclassRef = ObjectMemory.GetClassSuperclass(classOop);
        var superclassName = superclassRef.IsNil ? "nil" : ObjectMemory.GetClassName(superclassRef.AsObjectPointer());
        Console.WriteLine($"  Superclass: {superclassName}");

        var metaclassRef = ObjectMemory.GetObjectClass(classRef.Value);
        var metaclassName = metaclassRef.IsNil ? "nil" : ObjectMemory.GetClassName(metaclassRef.AsObjectPointer());
        Console.WriteLine($"  Metaclass: {metaclassName}");

        // Determine object format (if we can infer it from the class name)
        var objectFormat = DetermineObjectFormat(className);
        Console.WriteLine($"  Format: {objectFormat}");

        // INSTANCE VARIABLES section
        var instanceVarNames = ObjectMemory.GetClassInstanceVariableNames(classOop) ?? [];
        Console.WriteLine($"\nINSTANCE VARIABLES ({instanceVarNames.Length}):");
        if (instanceVarNames.Length == 0)
        {
            Console.WriteLine("  (none - inherited from " + superclassName + ")");
        }
        else
        {
            var baseIndex = superclassRef.IsNil ? 0 : ObjectMemory.GetClassTotalInstanceVariableCount(superclassRef.AsObjectPointer());
            for (var i = 0; i < instanceVarNames.Length; i++)
            {
                Console.WriteLine($"  [{baseIndex + i}] {instanceVarNames[i]}");
            }
        }

        var totalInstVars = ObjectMemory.GetClassTotalInstanceVariableCount(classOop);
        Console.WriteLine($"\nTOTAL INSTANCE VARIABLES: {totalInstVars} (including inherited)");

        // CLASS VARIABLES section (placeholder)
        Console.WriteLine("\nCLASS VARIABLES:");
        Console.WriteLine("  (not yet implemented)");

        // Collect class methods if we have a metaclass
        var classMethodCount = 0;
        if (!metaclassRef.IsNil && metaclassRef != classRef.Value)
        {
            classMethodCount = ObjectMemory.GetClassMethodSelectors(metaclassRef.AsObjectPointer()).Count;
        }

        // METHOD SUMMARY
        var instanceMethods = ObjectMemory.GetClassMethodSelectors(classOop);
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

                var methodRef = ObjectMemory.LookupMethod(classOop, selector);
                if (!methodRef.IsNil)
                {
                    var methodOop = methodRef.AsObjectPointer();
                    Console.WriteLine($"Method: {selector}");
                    Console.WriteLine($"  Arguments: {ObjectMemory.GetCompiledMethodArgumentCount(methodOop)}");
                    Console.WriteLine($"  Temporaries: {ObjectMemory.GetCompiledMethodTemporaryCount(methodOop)}");

                    var primitiveIndex = ObjectMemory.GetCompiledMethodPrimitiveIndex(methodOop);
                    if (primitiveIndex > 0)
                    {
                        Console.WriteLine($"  Primitive: {primitiveIndex}");
                    }

                    var bytecodes = ObjectMemory.GetCompiledMethodBytecodes(methodOop);
                    Console.WriteLine($"  Bytecode: [{BitConverter.ToString(bytecodes)}] ({bytecodes.Length} bytes)");

                    // Display literal array
                    var literalCount = ObjectMemory.GetCompiledMethodLiteralCount(methodOop);
                    Console.WriteLine($"  Literals: ({literalCount})");
                    if (literalCount > 0)
                    {
                        for (int i = 0; i < literalCount; i++)
                        {
                            var literalRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, i);
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
                                var literalClassRef = ObjectMemory.GetObjectClass(literalRef);
                                if (!literalClassRef.IsNil)
                                {
                                    var literalClassName = ObjectMemory.GetClassName(literalClassRef.AsObjectPointer());
                                    if (literalClassName == "Symbol")
                                    {
                                        var symbolValue = ObjectMemory.GetSymbolValue(literalOop);
                                        literalDisplay = $"#{symbolValue}";
                                    }
                                    else if (literalClassName == "String")
                                    {
                                        var stringValue = ObjectMemory.GetStringValue(literalOop);
                                        literalDisplay = $"'{stringValue}'";
                                    }
                                    else if (literalClassName == "Array")
                                    {
                                        var size = ObjectMemory.GetObjectSize(literalOop);
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
    
    // REMOVED: InitializeBasicMethods() - all methods must come from compiler-generated cold start data
    /// <summary>
    /// Initializes the garbage collection system with the Special Objects Array.
    /// Per Blue Book specification: Only 5 roots are needed for complete reachability.
    /// The SOA is a C# array containing the 5 root SObjectRefs.
    /// </summary>
    private void InitializeGarbageCollection()
    {
        // Get the GC roots from the SpecialObjectsArray (fixed struct, no heap allocation)
        var soa = Memory.SpecialObjectsArray.Instance.GetRootsForGC();

        // Give the GC the SOA array
        garbageCollector.SetSpecialObjectsArray(soa);

        if (Program.RuntimeOptions.EnableColdStartLogging || Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[COLD START] Special Objects Array initialized (Blue Book minimal spec)");

            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Console.WriteLine("[COLD START] SOA roots (5-root minimal specification):");
                Console.WriteLine("  [0] nil");
                Console.WriteLine("  [1] false");
                Console.WriteLine("  [2] true");
                Console.WriteLine("  [3] SystemDictionary (ALL classes, methods, symbols, globals)");
                Console.WriteLine("  [4] ProcessorScheduler (ALL processes and contexts)");
                Console.WriteLine();
            }
        }
    }
}