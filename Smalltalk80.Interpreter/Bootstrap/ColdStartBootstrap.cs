namespace Smalltalk80.Interpreter.Bootstrap;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
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
        
        // Initialize Process system (Processor singleton and initial process)
        InitializeProcessSystem();
        
        // Install compilation methods for self-hosted compilation
        CompilationMethodsInstaller.InstallAll();
        
        // Run bytecode validation test if enabled or if tracing is requested
        if ((Program.RuntimeOptions?.ValidateBytecodeExecution ?? false)
            || DebugOptions.EnableMethodTrace 
            || DebugOptions.EnableBytecodeTrace)
        {
            RunBytecodeValidationTest();
        }
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
            var transcript = textCollectorClass.CreateInstance();
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
    /// Initializes the process system with Processor singleton and initial process.
    /// </summary>
    private static void InitializeProcessSystem()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Initializing Process system...");
        }
        
        // Create the ProcessorScheduler singleton (Processor)
        var processorSchedulerClass = SystemDictionary.ProcessorSchedulerClass;
        if (processorSchedulerClass == null)
        {
            Console.WriteLine("[COLD START] WARNING: ProcessorScheduler class not found");
            return;
        }
        
        // Create instance of ProcessorScheduler
        // It has 2 instance variables: quiescentProcessLists and activeProcess
        var processor = processorSchedulerClass.CreateInstance() as SInstance;
        if (processor == null) return;
        
        // Initialize quiescentProcessLists as an Array of 8 LinkedLists (priority levels 1-8)
        var linkedListClass = SystemDictionary.LinkedListClass;
        if (linkedListClass != null)
        {
            var priorityLists = new SArray(8);
            for (int i = 1; i <= 8; i++)
            {
                var list = linkedListClass.CreateInstance();
                priorityLists.AtPut(i, list);
            }
            processor.SetInstanceVariable(0, priorityLists); // quiescentProcessLists
        }
        
        // Create the initial process for the REPL
        var processClass = SystemDictionary.ProcessClass;
        var blockContextClass = SystemDictionary.BlockContextClass;
        if (processClass != null && blockContextClass != null)
        {
            // Create a simple block context that represents the REPL main loop
            var replBlock = blockContextClass.CreateInstance() as SInstance;
            if (replBlock != null)
            {
                // Set block context fields: nargs=0, startpc=0, home=nil
                replBlock.SetInstanceVariable(0, SmallInteger.From(0)); // nargs
                replBlock.SetInstanceVariable(1, SmallInteger.From(0)); // startpc
                replBlock.SetInstanceVariable(2, SNil.Instance); // home
                
                // Create initial process at normal priority (4)
                var initialProcess = processClass.CreateInstance() as SInstance;
                if (initialProcess != null)
                {
                    initialProcess.SetInstanceVariable(0, replBlock); // suspendedContext
                    initialProcess.SetInstanceVariable(1, SmallInteger.From(4)); // priority
                    initialProcess.SetInstanceVariable(2, SNil.Instance); // myList (will be set when scheduled)
                    
                    // Set as active process
                    processor.SetInstanceVariable(1, initialProcess); // activeProcess
                }
                
                if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
                {
                    Console.WriteLine("[COLD START] Created initial Process for REPL");
                }
            }
        }
        
        // Register Processor as global
        SystemDictionary.RegisterGlobal("Processor", processor);
        
        // Also add to Special Objects Array at ProcessorSchedulerIndex
        Memory.SpecialObjectsArray.Set(Memory.SpecialObjectsArray.ProcessorSchedulerIndex, processor);
        
        // Create and register a ProcessList (LinkedList) at ProcessListIndex in SOA
        if (linkedListClass != null)
        {
            var processList = linkedListClass.CreateInstance();
            Memory.SpecialObjectsArray.Set(Memory.SpecialObjectsArray.ProcessListIndex, processList);
            
            // Add the initial process to the process list if we have one
            var initialProcess = processor.GetInstanceVariable(1); // activeProcess
            if (initialProcess != null && initialProcess != SNil.Instance)
            {
                // We need to make Process inherit from Link for this to work properly
                // For now, just set the ProcessList in SOA
            }
        }
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Process system initialized with Processor singleton");
        }
    }
    
    /// <summary>
    /// Installs console output methods on TextCollector class.
    /// </summary>
    private static void InstallTranscriptMethods(SClass textCollectorClass)
    {
        // Add show: method with primitive 253 (console output)
        textCollectorClass.AddMethod("show:", SCompiledMethod.CreateCompiledMethod(
            "show:",
            [0x8F, 0xFD, 0x01, 0x7C], // Primitive 253 with 1 arg, return top
            new SArray(),
            1, 0, 253));
        
        // Add cr method with primitive 254 (console newline)
        textCollectorClass.AddMethod("cr", SCompiledMethod.CreateCompiledMethod(
            "cr",
            [0x8F, 0xFE, 0x00, 0x7C], // Primitive 254 with 0 args, return top
            new SArray(),
            0, 0, 254));
        
        // Add clear method with primitive 252 (console clear)
        textCollectorClass.AddMethod("clear", SCompiledMethod.CreateCompiledMethod(
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
        floatClass.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethod(
            "asFloat",
            [0x70, 0x7C], // Push receiver, return top
            new SArray(),
            0, 0, 0));
        
        // Add + method with primitive 41
        floatClass.AddMethod("+", SCompiledMethod.CreateCompiledMethod(
            "+",
            [0x8F, 0x29, 0x01, 0x7C], // Primitive 41 with 1 arg, return top
            new SArray(),
            1, 0, 41));
        
        // Add - method with primitive 42
        floatClass.AddMethod("-", SCompiledMethod.CreateCompiledMethod(
            "-",
            [0x8F, 0x2A, 0x01, 0x7C], // Primitive 42 with 1 arg, return top
            new SArray(),
            1, 0, 42));
        
        // Add * method with primitive 49
        floatClass.AddMethod("*", SCompiledMethod.CreateCompiledMethod(
            "*",
            [0x8F, 0x31, 0x01, 0x7C], // Primitive 49 with 1 arg, return top
            new SArray(),
            1, 0, 49));
        
        // Add / method with primitive 50
        floatClass.AddMethod("/", SCompiledMethod.CreateCompiledMethod(
            "/",
            [0x8F, 0x32, 0x01, 0x7C], // Primitive 50 with 1 arg, return top
            new SArray(),
            1, 0, 50));
        
        // Add < method with primitive 43
        floatClass.AddMethod("<", SCompiledMethod.CreateCompiledMethod(
            "<",
            [0x8F, 0x2B, 0x01, 0x7C], // Primitive 43 with 1 arg, return top
            new SArray(),
            1, 0, 43));
        
        // Add > method with primitive 44
        floatClass.AddMethod(">", SCompiledMethod.CreateCompiledMethod(
            ">",
            [0x8F, 0x2C, 0x01, 0x7C], // Primitive 44 with 1 arg, return top
            new SArray(),
            1, 0, 44));
        
        // Add = method with primitive 47
        floatClass.AddMethod("=", SCompiledMethod.CreateCompiledMethod(
            "=",
            [0x8F, 0x2F, 0x01, 0x7C], // Primitive 47 with 1 arg, return top
            new SArray(),
            1, 0, 47));
        
        // Add truncate method with primitive 51
        floatClass.AddMethod("truncate", SCompiledMethod.CreateCompiledMethod(
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
        smallIntegerClass.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethod(
            "asFloat",
            [0x8F, 0x28, 0x00, 0x7C], // Primitive 40 with 0 args, return top
            new SArray(),
            0, 0, 40));
            
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Added asFloat method to SmallInteger class");
        }
    }
    
    /// <summary>
    /// Runs a comprehensive bytecode validation test that exercises the interpreter
    /// through proper method contexts, message sends, and bytecode execution.
    /// </summary>
    private static void RunBytecodeValidationTest()
    {
        Console.WriteLine("\n=== BYTECODE VALIDATION TEST ===");
        Console.WriteLine("Testing that the interpreter executes bytecode through method contexts...\n");
        
        try
        {
            // Test 1: Simple arithmetic through method context
            Console.WriteLine("Test 1: Simple arithmetic (3 + 4)");
            var result1 = TestSimpleArithmetic();
            Console.WriteLine($"  Result: {result1} (expected: 7)");
            Console.WriteLine($"  Status: {(result1 == 7 ? "PASS" : "FAIL")}\n");
            
            // Test 2: Message send that creates new method context
            Console.WriteLine("Test 2: Message send creating new context");
            var result2 = TestMessageSend();
            Console.WriteLine($"  Result: {result2} (expected: 10)");
            Console.WriteLine($"  Status: {(result2 == 10 ? "PASS" : "FAIL")}\n");
            
            // Test 3: Nested message sends creating context chain
            Console.WriteLine("Test 3: Nested message sends");
            var result3 = TestNestedMessageSends();
            Console.WriteLine($"  Result: {result3} (expected: 41)");
            Console.WriteLine($"  Status: {(result3 == 41 ? "PASS" : "FAIL")}\n");
            
            Console.WriteLine("=== BYTECODE VALIDATION COMPLETE ===\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BYTECODE VALIDATION FAILED: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Test 1: Simple arithmetic through a method context.
    /// Creates a method that does: 3 + 4
    /// </summary>
    private static int TestSimpleArithmetic()
    {
        // Create a CompiledMethod for "testAdd" that does: 3 + 4
        // Bytecode sequence:
        // 0x20: push literal 0 (3)
        // 0x21: push literal 1 (4) 
        // 0xB0: send + with 1 arg
        // 0x7C: return top
        
        // Note: SArray uses 1-based indexing like Smalltalk
        // But CompiledMethod.GetLiteral uses 0-based indexing
        var literals = new SArray(3); // Need 3 slots for 1-based array
        literals.AtPut(1, SmallInteger.From(3));  // Index 1 in SArray = literal index 0
        literals.AtPut(2, SmallInteger.From(4));  // Index 2 in SArray = literal index 1
        literals.AtPut(3, SSymbol.Intern("+"));  // Index 3 in SArray = literal index 2
        
        var testMethod = SCompiledMethod.CreateCompiledMethod(
            "testAdd",
            new byte[] {
                0x20,  // push literal 0 (value 3)
                0x21,  // push literal 1 (value 4)
                0xB0,  // send arithmetic selector + 
                0x7C   // return top
            },
            literals,
            0,  // no args
            0,  // no temps
            0   // no primitive
        );
        
        // Create a context-based interpreter and execute
        var interpreter = new ContextBasedInterpreter();
        // Enable tracing if requested via command-line options or validation flag
        var enableTracing = (Program.RuntimeOptions?.ValidateBytecodeExecution ?? false)
            || DebugOptions.EnableMethodTrace 
            || DebugOptions.EnableBytecodeTrace;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"  Debug: ValidateBytecodeExecution={Program.RuntimeOptions?.ValidateBytecodeExecution}");
            Console.WriteLine($"  Debug: EnableMethodTrace={DebugOptions.EnableMethodTrace}");
            Console.WriteLine($"  Debug: EnableBytecodeTrace={DebugOptions.EnableBytecodeTrace}");
            Console.WriteLine($"  Debug: TracingEnabled={enableTracing}");
        }
        
        interpreter.TracingEnabled = enableTracing;
        
        // Debug: verify the test method
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"  Debug: TestMethod created - bytecodes={testMethod.Bytecodes.Length}, literals={literals.Size}");
            Console.WriteLine($"  Debug: Literal 0 (SArray[1]) = {testMethod.GetLiteral(0)?.ToDisplayString() ?? "null"}");
            Console.WriteLine($"  Debug: Literal 1 (SArray[2]) = {testMethod.GetLiteral(1)?.ToDisplayString() ?? "null"}");
            Console.WriteLine($"  Debug: Literal 2 (SArray[3]) = {testMethod.GetLiteral(2)?.ToDisplayString() ?? "null"}");
        }
        
        var result = interpreter.Execute(testMethod, SNil.Instance, new SObject[0]);
        
        // Extract integer value
        if (result is SmallInteger si)
            return si.Value;
        
        throw new InvalidOperationException($"Expected SmallInteger result, got {result?.GetType().Name}");
    }
    
    /// <summary>
    /// Test 2: Message send that creates a new method context.
    /// Creates two methods: one that returns 10, another that calls it.
    /// </summary>
    private static int TestMessageSend()
    {
        // First, create a simple method "getValue" that returns 10
        var getValueLiterals = new SArray(1);
        getValueLiterals.AtPut(1, SmallInteger.From(10)); // Index 1 in SArray = literal index 0
        
        var getValueMethod = SCompiledMethod.CreateCompiledMethod(
            "getValue",
            new byte[] {
                0x20,  // push literal 0 (10)
                0x7C   // return top
            },
            getValueLiterals,
            0, 0, 0
        );
        
        // Add this method to a test class
        var testClass = new SClass("TestClass", SystemDictionary.ObjectClass, new string[0]);
        testClass.AddMethod("getValue", getValueMethod);
        var testInstance = testClass.CreateInstance();
        
        // Now create a method that sends getValue message
        var senderLiterals = new SArray(1);
        senderLiterals.AtPut(1, SSymbol.Intern("getValue")); // Index 1 in SArray = literal index 0
        
        var senderMethod = SCompiledMethod.CreateCompiledMethod(
            "callGetValue", 
            new byte[] {
                0x70,  // push receiver (self)
                0xD0,  // send literal selector with 0 args (first part)
                0x00,  // selector index 0 (second part) - combines to make selector index 0
                0x7C   // return top
            },
            senderLiterals,
            0, 0, 0
        );
        
        // Execute - this should create a new context when sending getValue
        var interpreter = new ContextBasedInterpreter();
        // Enable tracing if requested via command-line options or validation flag
        var enableTracing = (Program.RuntimeOptions?.ValidateBytecodeExecution ?? false)
            || DebugOptions.EnableMethodTrace 
            || DebugOptions.EnableBytecodeTrace;
        interpreter.TracingEnabled = enableTracing;
        var result = interpreter.Execute(senderMethod, testInstance, new SObject[0]);
        
        if (result is SmallInteger si)
            return si.Value;
            
        throw new InvalidOperationException($"Expected SmallInteger result, got {result?.GetType().Name}");
    }
    
    /// <summary>
    /// Test 3: Nested message sends creating a context chain.
    /// Creates methods that call each other, building up a context chain.
    /// </summary>
    private static int TestNestedMessageSends()
    {
        // Create a test class with multiple methods
        var testClass = new SClass("TestNested", SystemDictionary.ObjectClass, new[] { "value" });
        var instance = testClass.CreateInstance() as SInstance;
        
        // Method 1: multiplyBy2 - multiplies instance variable by 2
        var multiply2Literals = new SArray(2);
        multiply2Literals.AtPut(1, SmallInteger.From(2));   // Index 1 in SArray = literal index 0
        multiply2Literals.AtPut(2, SSymbol.Intern("*"));   // Index 2 in SArray = literal index 1
        
        var multiply2Method = SCompiledMethod.CreateCompiledMethod(
            "multiplyBy2",
            new byte[] {
                0x00,  // push instance variable 0
                0x20,  // push literal 0 (2)
                0xB8,  // send * (arithmetic bytecode)
                0x7C   // return top
            },
            multiply2Literals,
            0, 0, 0
        );
        testClass.AddMethod("multiplyBy2", multiply2Method);
        
        // Method 2: add21 - adds 21 to the value  
        var add21Literals = new SArray(1);
        add21Literals.AtPut(1, SmallInteger.From(21));  // Index 1 in SArray = literal index 0
        
        var add21Method = SCompiledMethod.CreateCompiledMethod(
            "add21",
            new byte[] {
                0x00,  // push instance variable 0
                0x20,  // push literal 0 (21)
                0xB0,  // send + (arithmetic bytecode)
                0x7C   // return top
            },
            add21Literals,
            0, 0, 0
        );
        testClass.AddMethod("add21", add21Method);
        
        // Method 3: compute - calls multiplyBy2 then add21
        // Should compute: (value * 2) + 21
        // If value = 10, result should be 41 (10*2 + 21)
        var computeLiterals = new SArray(2);
        computeLiterals.AtPut(1, SSymbol.Intern("multiplyBy2"));  // Index 1 in SArray = literal index 0
        computeLiterals.AtPut(2, SSymbol.Intern("add21"));        // Index 2 in SArray = literal index 1
        
        var computeMethod = SCompiledMethod.CreateCompiledMethod(
            "compute",
            new byte[] {
                0x70,  // push receiver
                0xD0,  // send literal selector with 0 args (first part)
                0x00,  // selector index 0 (second part) - multiplyBy2
                0x60,  // pop and store in instance var 0
                0x70,  // push receiver  
                0xD0,  // send literal selector with 0 args (first part)
                0x01,  // selector index 1 (second part) - add21
                0x7C   // return top
            },
            computeLiterals,
            0, 0, 0
        );
        testClass.AddMethod("compute", computeMethod);
        
        // Set initial value to 10 (which * 2 + 21 = 41)
        instance?.SetInstanceVariable(0, SmallInteger.From(10));
        
        // Execute - this should create nested contexts
        var interpreter = new ContextBasedInterpreter();
        // Enable tracing if requested via command-line options or validation flag
        var enableTracing = (Program.RuntimeOptions?.ValidateBytecodeExecution ?? false)
            || DebugOptions.EnableMethodTrace 
            || DebugOptions.EnableBytecodeTrace;
        interpreter.TracingEnabled = enableTracing;
        var result = interpreter.Execute(computeMethod, instance!, new SObject[0]);
        
        if (result is SmallInteger si)
            return si.Value;
            
        throw new InvalidOperationException($"Expected SmallInteger result, got {result?.GetType().Name}");
    }
}