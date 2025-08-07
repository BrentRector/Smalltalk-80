using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Smalltalk80;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides a high level interface combining the interpreter and object memory
/// into a simple virtual machine suitable for the REPL and other hosts.
/// </summary>
public sealed class VirtualMachine
{
    /// <summary>
    /// Gets the interpreter instance executing methods for this VM.
    /// </summary>
    public Interpreter Interpreter { get; } = new() { ExitOnReturn = false };

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMachine"/> class and
    /// loads the standard library objects.
    /// </summary>
    public VirtualMachine(bool skipStandardLibrary = false, bool useGeneratedBootstrap = true)
    {
        if (useGeneratedBootstrap)
        {
            // Use the new generated bootstrap approach
            InitializeWithGeneratedBootstrap(skipStandardLibrary);
        }
        else if (!skipStandardLibrary)
        {
            // Legacy: Load StandardLibrary.st directly via C#
            LoadStandardLibraryClasses();
        }
    }
    
    /// <summary>
    /// Initializes the VM with the generated bootstrap.
    /// This is the new, efficient approach where core classes are "baked in".
    /// </summary>
    private void InitializeWithGeneratedBootstrap(bool skipStandardLibrary)
    {
        Console.WriteLine("[BOOTSTRAP] Using generated bootstrap...");
        
        // Phase 1: Initialize core classes from generated code (instant)
        GeneratedBootstrap.Initialize();
        
        if (!skipStandardLibrary)
        {
            // Phase 2: Load StandardLibrary.st via Smalltalk class creation
            LoadStandardLibraryViaSmalltalk();
        }
        
        Console.WriteLine("[BOOTSTRAP] Bootstrap complete!");
    }
    
    /// <summary>
    /// Loads the standard library using Smalltalk's own class creation mechanism.
    /// This assumes the bootstrap classes are already loaded.
    /// </summary>
    private void LoadStandardLibraryViaSmalltalk()
    {
        // First, check if we should load the second stage bootstrap
        var secondStageBootstrapPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "SecondStageBootstrap.st");
            
        if (File.Exists(secondStageBootstrapPath))
        {
            Console.WriteLine($"[BOOTSTRAP] Loading second stage bootstrap (class creation infrastructure)...");
            
            // Load the second stage bootstrap using C# loader (since we don't have class creation yet)
            var secondStageResult = SystemImageLoader.LoadSystemImage(secondStageBootstrapPath);
            
            if (!secondStageResult.Success)
            {
                Console.WriteLine("[BOOTSTRAP] Warning: Failed to load second stage bootstrap completely");
                foreach (var error in secondStageResult.Errors.Take(3))
                {
                    Console.WriteLine($"  Error: {error.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[BOOTSTRAP] Second stage bootstrap loaded: {secondStageResult.MethodsLoaded} methods");
            }
        }
        
        // Now load HeadlessStandardLibrary using the SmalltalkLoader
        // First try HeadlessStandardLibrary.st, fall back to StandardLibrary.st if not found
        var headlessLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "HeadlessStandardLibrary.st");
            
        var standardLibraryPath = headlessLibraryPath;
        
        if (!File.Exists(headlessLibraryPath))
        {
            // Fall back to StandardLibrary.st
            standardLibraryPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "StandardLibrary.st");
                
            if (!File.Exists(standardLibraryPath))
            {
                Console.WriteLine("[BOOTSTRAP] No standard library found (tried HeadlessStandardLibrary.st and StandardLibrary.st)");
                return;
            }
            Console.WriteLine($"[BOOTSTRAP] HeadlessStandardLibrary.st not found, falling back to StandardLibrary.st");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Using HeadlessStandardLibrary.st for non-graphical Smalltalk system");
        }
        
        Console.WriteLine($"[BOOTSTRAP] Loading {Path.GetFileName(standardLibraryPath)} via Smalltalk class creation...");
        
        // Use the SmalltalkLoader which sends Smalltalk messages
        var loader = new SmalltalkLoader(Interpreter);
        var result = loader.LoadFile(standardLibraryPath);
        
        if (result.Success)
        {
            Console.WriteLine($"[BOOTSTRAP] Loaded {result.ClassesLoaded} classes and {result.MethodsLoaded} methods from {Path.GetFileName(standardLibraryPath)}");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Failed to load some parts of {Path.GetFileName(standardLibraryPath)}");
            foreach (var error in result.Errors.Take(5))
            {
                Console.WriteLine($"  Error: {error}");
            }
            if (result.Warnings.Count > 0)
            {
                Console.WriteLine($"[BOOTSTRAP] Warnings during load:");
                foreach (var warning in result.Warnings.Take(5))
                {
                    Console.WriteLine($"  Warning: {warning}");
                }
            }
        }
    }
    
    /// <summary>
    /// Initializes the VM with three-phase bootstrap for Smalltalk class creation.
    /// Phase 1: Core bootstrap (27 classes)
    /// Phase 2: Infrastructure classes (15 classes)
    /// Phase 3: Everything else via Smalltalk class creation
    /// </summary>
    private void InitializeWithSmalltalkClassCreation()
    {
        Console.WriteLine("[BOOTSTRAP] Starting three-phase bootstrap with Smalltalk class creation...");
        
        // Phase 1: Core bootstrap classes (via C#)
        Console.WriteLine("[BOOTSTRAP] Phase 1: Creating core bootstrap classes...");
        MinimalBootstrap.CreateEssentialClasses();
        
        // Load bootstrap methods for core classes
        var bootstrapMethodsPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "BootstrapMethods.st");
            
        if (File.Exists(bootstrapMethodsPath))
        {
            Console.WriteLine($"[BOOTSTRAP] Loading bootstrap methods from: {bootstrapMethodsPath}");
            LoadSystemFile(bootstrapMethodsPath);
        }
        
        // Phase 2: Infrastructure classes needed for class creation (via C#)
        Console.WriteLine("[BOOTSTRAP] Phase 2: Loading infrastructure classes...");
        LoadInfrastructureClasses();
        
        // Initialize globals
        SystemDictionary.InitializeGlobals();
        InitializeSystemGlobals();
        
        // Phase 3: Load remaining classes via Smalltalk class creation
        Console.WriteLine("[BOOTSTRAP] Phase 3: Loading remaining classes via Smalltalk class creation...");
        LoadRemainingClassesViaSmalltalk();
        
        Console.WriteLine("[BOOTSTRAP] Three-phase bootstrap completed!");
    }
    
    /// <summary>
    /// Loads infrastructure classes needed for class creation from StandardLibrary.
    /// </summary>
    private void LoadInfrastructureClasses()
    {
        var standardLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "StandardLibrary.st");
            
        if (!File.Exists(standardLibraryPath))
        {
            Console.WriteLine("[BOOTSTRAP] Error: StandardLibrary.st not found!");
            return;
        }
        
        // Parse the file
        var fileAst = FileInReader.Parse(standardLibraryPath);
        
        // List of infrastructure classes to load in Phase 2
        var infrastructureClasses = new HashSet<string>
        {
            "Character", "LookupKey", "IdentityDictionary", "OrderedCollection",
            "Stream", "PositionableStream", "WriteStream", "ReadStream",
            "ReadWriteStream", "ExternalStream"
        };
        
        // Load infrastructure classes - Pass 1: Create class stubs
        var createdClasses = new Dictionary<string, SmalltalkClass>();
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef && infrastructureClasses.Contains(classDef.ClassName))
            {
                Console.WriteLine($"[BOOTSTRAP] Creating infrastructure class: {classDef.ClassName}");
                // Create the class using SystemDictionary
                var newClass = SystemDictionary.DefineClass(
                    classDef.ClassName,
                    null, // Superclass will be resolved later
                    classDef.InstanceVariables,
                    classDef.ClassVariables,
                    classDef.PoolDictionaries,
                    classDef.Category);
                createdClasses[classDef.ClassName] = newClass;
            }
        }
        
        // Pass 2: Resolve superclass relationships
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef && infrastructureClasses.Contains(classDef.ClassName))
            {
                if (createdClasses.TryGetValue(classDef.ClassName, out var cls))
                {
                    // Resolve superclass
                    if (!string.IsNullOrEmpty(classDef.Superclass) && classDef.Superclass != "nil")
                    {
                        if (SystemDictionary.TryGetClass(classDef.Superclass, out var superclass))
                        {
                            cls.Superclass = superclass;
                            Console.WriteLine($"[BOOTSTRAP] Set superclass {classDef.ClassName} -> {classDef.Superclass}");
                        }
                    }
                }
            }
        }
        
        // Load infrastructure class methods
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is MethodCategoryNode methodCategory)
            {
                if (infrastructureClasses.Contains(methodCategory.Header.ClassName))
                {
                    // Load methods for infrastructure classes
                    if (SystemDictionary.TryGetClass(methodCategory.Header.ClassName, out var targetClass))
                    {
                        foreach (var method in methodCategory.Methods)
                        {
                            try
                            {
                                // Use AstMethodCompiler directly with the parsed AST
                                // Collect all instance variables including inherited ones
                                var variables = new List<string>();
                                if (methodCategory.Header.IsClassSide)
                                {
                                    variables.AddRange(targetClass.ClassVariables);
                                }
                                else
                                {
                                    // Add inherited instance variables
                                    var currentClass = targetClass;
                                    while (currentClass != null)
                                    {
                                        variables.AddRange(currentClass.InstanceVariables);
                                        currentClass = currentClass.Superclass;
                                    }
                                }
                                
                                var compiledMethod = AstMethodCompiler.CompileMethod(
                                    method.Body,
                                    method.Arguments,
                                    variables,
                                    method.HasPrimitive,
                                    method.PrimitiveIndex);
                                
                                var selector = new Symbol(method.Selector);
                                if (methodCategory.Header.IsClassSide)
                                {
                                    // Install class-side method in the metaclass
                                    if (targetClass.Metaclass != null)
                                    {
                                        targetClass.Metaclass.MethodDictionary[selector] = compiledMethod;
                                    }
                                }
                                else
                                {
                                    // Install instance-side method
                                    targetClass.MethodDictionary[selector] = compiledMethod;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[BOOTSTRAP] Warning: Failed to compile method {method.Selector} in {methodCategory.Header.ClassName}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            else if (chunk is MethodDefinitionNode methodDef)
            {
                // Single method definitions don't have a class name - skip for now
                // They would need to be preceded by a header
                if (false) // Placeholder
                {
                }
            }
        }
        
        // Load class creation stubs
        var stubsPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "ClassCreationStubs.st");
            
        if (File.Exists(stubsPath))
        {
            Console.WriteLine("[BOOTSTRAP] Loading class creation stubs...");
            LoadSystemFile(stubsPath);
        }
        
        // Load class creation methods
        var methodsPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "ClassCreationMethods.st");
            
        if (File.Exists(methodsPath))
        {
            Console.WriteLine("[BOOTSTRAP] Loading class creation methods...");
            LoadSystemFile(methodsPath);
        }
    }
    
    /// <summary>
    /// Initializes system global variables.
    /// </summary>
    private void InitializeSystemGlobals()
    {
        // Initialize SystemOrganization
        if (!SystemDictionary.TryGetGlobal("SystemOrganization", out _))
        {
            if (SystemDictionary.TryGetClass("SystemOrganizer", out var organizerClass))
            {
                var systemOrganization = new SmalltalkObject(organizerClass, 0);
                SystemDictionary.SetGlobal("SystemOrganization", systemOrganization);
                Console.WriteLine("[BOOTSTRAP] Created SystemOrganization global");
            }
        }
        
        // Initialize Undeclared
        if (!SystemDictionary.TryGetGlobal("Undeclared", out _))
        {
            if (SystemDictionary.TryGetClass("Dictionary", out var dictClass))
            {
                var undeclared = new SmalltalkObject(dictClass, 0);
                SystemDictionary.SetGlobal("Undeclared", undeclared);
                Console.WriteLine("[BOOTSTRAP] Created Undeclared global");
            }
        }
    }
    
    /// <summary>
    /// Loads remaining classes via Smalltalk class creation.
    /// </summary>
    private void LoadRemainingClassesViaSmalltalk()
    {
        var standardLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "StandardLibrary.st");
            
        if (!File.Exists(standardLibraryPath))
        {
            Console.WriteLine("[BOOTSTRAP] Error: StandardLibrary.st not found!");
            return;
        }
        
        // Parse the file
        var fileAst = FileInReader.Parse(standardLibraryPath);
        
        // Classes already loaded in Phase 1 and 2
        var alreadyLoaded = new HashSet<string>
        {
            // Phase 1 classes
            "Object", "UndefinedObject", "Behavior", "ClassDescription", "Class", "Metaclass",
            "Boolean", "True", "False", "SmallInteger", "Symbol", "String", "Array",
            "BlockContext", "Collection", "Set", "Dictionary", "SystemDictionary", "Association",
            // Phase 2 classes
            "Character", "LookupKey", "IdentityDictionary", "OrderedCollection",
            "Stream", "PositionableStream", "WriteStream", "ReadStream", "ReadWriteStream", "ExternalStream",
            // Stub classes
            "MethodDictionary", "ClassOrganizer", "SystemOrganizer", "Scanner"
        };
        
        // Load remaining classes via Smalltalk
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef && !alreadyLoaded.Contains(classDef.ClassName))
            {
                var expression = GenerateClassCreationExpression(classDef);
                Console.WriteLine($"[BOOTSTRAP] Creating class via Smalltalk: {classDef.ClassName}");
                
                try
                {
                    EvaluateSource(expression);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BOOTSTRAP] Warning: Failed to create class {classDef.ClassName}: {ex.Message}");
                }
            }
        }
        
        // Load methods for remaining classes
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is MethodCategoryNode methodCategory && !alreadyLoaded.Contains(methodCategory.Header.ClassName))
            {
                // Load methods for classes created via Smalltalk
                if (SystemDictionary.TryGetClass(methodCategory.Header.ClassName, out var targetClass))
                {
                    foreach (var method in methodCategory.Methods)
                    {
                        try
                        {
                            // Use AstMethodCompiler directly with the parsed AST
                            // Collect all instance variables including inherited ones
                            var variables = new List<string>();
                            if (methodCategory.Header.IsClassSide)
                            {
                                variables.AddRange(targetClass.ClassVariables);
                            }
                            else
                            {
                                // Add inherited instance variables
                                var currentClass = targetClass;
                                while (currentClass != null)
                                {
                                    variables.AddRange(currentClass.InstanceVariables);
                                    currentClass = currentClass.Superclass;
                                }
                            }
                            
                            var compiledMethod = AstMethodCompiler.CompileMethod(
                                method.Body,
                                method.Arguments,
                                variables,
                                method.HasPrimitive,
                                method.PrimitiveIndex);
                            
                            var selector = new Symbol(method.Selector);
                            if (methodCategory.Header.IsClassSide)
                            {
                                // Install class-side method in the metaclass
                                if (targetClass.Metaclass != null)
                                {
                                    targetClass.Metaclass.MethodDictionary[selector] = compiledMethod;
                                }
                            }
                            else
                            {
                                // Install instance-side method
                                targetClass.MethodDictionary[selector] = compiledMethod;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BOOTSTRAP] Warning: Failed to compile method {method.Selector} in {methodCategory.Header.ClassName}: {ex.Message}");
                        }
                    }
                }
            }
            // Single method definitions are handled differently - skip for now
            else if (false) // Placeholder
            {
            }
        }
    }
    
    /// <summary>
    /// Initializes the VM with minimal bootstrap classes and loads the rest via Smalltalk.
    /// </summary>
    private void InitializeWithMinimalBootstrap()
    {
        Console.WriteLine("[BOOTSTRAP] Using minimal bootstrap approach...");
        
        // Step 1: Create essential classes
        MinimalBootstrap.CreateEssentialClasses();
        
        // Step 2: Load essential bootstrap methods
        var bootstrapMethodsPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "BootstrapMethods.st");
            
        if (File.Exists(bootstrapMethodsPath))
        {
            Console.WriteLine($"[BOOTSTRAP] Loading bootstrap methods from: {bootstrapMethodsPath}");
            LoadSystemFile(bootstrapMethodsPath);
            Console.WriteLine("[BOOTSTRAP] Bootstrap methods loaded");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: BootstrapMethods.st not found at: {bootstrapMethodsPath}");
        }
        
        // Step 3: Initialize globals (Smalltalk, Transcript, etc.)
        SystemDictionary.InitializeGlobals();
        
        // Step 4: Now we can load the rest of StandardLibrary via Smalltalk class creation
        var standardLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "StandardLibrary.st");
            
        if (File.Exists(standardLibraryPath))
        {
            Console.WriteLine($"[BOOTSTRAP] Loading standard library via Smalltalk from: {standardLibraryPath}");
            LoadSystemFileViaSmalltalk(standardLibraryPath);
            Console.WriteLine("[BOOTSTRAP] Standard library loaded via Smalltalk");
        }
    }
    
    /// <summary>
    /// Loads a system file by executing class definitions as Smalltalk code.
    /// </summary>
    private void LoadSystemFileViaSmalltalk(string path)
    {
        // Parse the file
        var fileAst = FileInReader.Parse(path);
        
        // Execute class definitions as Smalltalk expressions
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef)
            {
                // Generate and execute the Smalltalk expression
                var expression = GenerateClassCreationExpression(classDef);
                Console.WriteLine($"[BOOTSTRAP] Executing class definition: {classDef.ClassName}");
                
                try
                {
                    EvaluateSource(expression);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BOOTSTRAP] Failed to create class {classDef.ClassName}: {ex.Message}");
                }
            }
        }
        
        // Then load methods normally
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is MethodCategoryNode || chunk is MethodDefinitionNode)
            {
                // Process methods as before
                // This part can stay the same since methods are added to existing classes
            }
        }
    }
    
    /// <summary>
    /// Generates a Smalltalk expression to create a class.
    /// </summary>
    private string GenerateClassCreationExpression(ClassDefinitionNode classDef)
    {
        var superclass = string.IsNullOrEmpty(classDef.Superclass) || classDef.Superclass == "nil" 
            ? "nil" 
            : classDef.Superclass;
            
        var instVars = string.Join(" ", classDef.InstanceVariables);
        var classVars = string.Join(" ", classDef.ClassVariables);
        var pools = string.Join(" ", classDef.PoolDictionaries);
        var category = classDef.Category ?? "";
        
        return $@"{superclass} subclass: #{classDef.ClassName}
            instanceVariableNames: '{instVars}'
            classVariableNames: '{classVars}'
            poolDictionaries: '{pools}'
            category: '{category}'";
    }

    /// <summary>
    /// Loads the standard library classes from HeadlessStandardLibrary.st or StandardLibrary.st.
    /// Prefers HeadlessStandardLibrary.st for non-graphical systems.
    /// This provides essential classes like Object, Boolean, True, False, SmallInteger
    /// with all their control flow and arithmetic methods.
    /// </summary>
    private void LoadStandardLibraryClasses()
    {
        // Try HeadlessStandardLibrary.st first
        var headlessLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "HeadlessStandardLibrary.st");
            
        var standardLibraryPath = headlessLibraryPath;
        
        if (!File.Exists(headlessLibraryPath))
        {
            // Fall back to StandardLibrary.st
            standardLibraryPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "StandardLibrary.st");
        }

        if (File.Exists(standardLibraryPath))
        {
            try
            {
                var libraryName = Path.GetFileName(standardLibraryPath);
                Console.WriteLine($"[BOOTSTRAP] Loading {libraryName} from: {standardLibraryPath}");
                LoadSystemFile(standardLibraryPath);
                Console.WriteLine($"[BOOTSTRAP] {libraryName} loaded successfully");

                // Initialize classes that have class-side initialize methods
                InitializeClasses();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOTSTRAP] Warning: Failed to load {Path.GetFileName(standardLibraryPath)}: {ex.Message}");
                Console.WriteLine("[BOOTSTRAP] Continuing with minimal bootstrap only");
            }
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] No standard library found (tried HeadlessStandardLibrary.st and StandardLibrary.st)");
            Console.WriteLine("[BOOTSTRAP] Using minimal bootstrap only");
        }
    }

    /// <summary>
    /// Loads the test framework from Testing/TestLibrary.st if it exists.
    /// This provides test classes like TestCase, TestSuite, TestResult, TestRunner
    /// for validating interpreter operation with Smalltalk test cases.
    /// </summary>
    public void LoadTestLibrary()
    {
        var testLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "Testing", "TestLibrary.st");

        if (File.Exists(testLibraryPath))
        {
            try
            {
                Console.WriteLine($"[TEST-FRAMEWORK] Loading test library from: {testLibraryPath}");
                LoadSystemFile(testLibraryPath);
                Console.WriteLine("[TEST-FRAMEWORK] Test library loaded successfully");

                // Print discovered test classes
                PrintTestFrameworkSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST-FRAMEWORK] Warning: Failed to load test library: {ex.Message}");
                throw new InvalidOperationException($"Test framework loading failed: {ex.Message}", ex);
            }
        }
        else
        {
            Console.WriteLine($"[TEST-FRAMEWORK] TestLibrary.st not found at: {testLibraryPath}");
            throw new FileNotFoundException($"TestLibrary.st not found at: {testLibraryPath}");
        }
    }

    /// <summary>
    /// Creates a VirtualMachine with both standard library and test framework loaded.
    /// </summary>
    /// <returns>A VirtualMachine ready for running Smalltalk test cases.</returns>
    public static VirtualMachine CreateWithTestFramework()
    {
        var vm = new VirtualMachine();
        vm.LoadTestLibrary();
        return vm;
    }

    /// <summary>
    /// Prints a summary of the loaded test framework classes.
    /// </summary>
    private void PrintTestFrameworkSummary()
    {
        Console.WriteLine("[TEST-FRAMEWORK] Test Framework Classes Loaded:");

        var testFrameworkClasses = new[] { "TestCase", "TestResult", "TestSuite", "TestRunner", "TestFailure", "SampleBasicTest" };
        foreach (var className in testFrameworkClasses)
        {
            if (SystemDictionary.TryGetClass(className, out var cls))
            {
                Console.WriteLine($"[TEST-FRAMEWORK]   ✅ {className} ({cls.MethodDictionary.Count} methods)");
            }
            else
            {
                Console.WriteLine($"[TEST-FRAMEWORK]   ❌ {className} (not found)");
            }
        }
        Console.WriteLine($"[TEST-FRAMEWORK] Total classes in system: {SystemDictionary.AllClasses.Count}");
    }

    /// <summary>
    /// Loads a Smalltalk source file containing a single method definition and
    /// installs it on the specified class. The first line of the file must be
    /// of the form <c>ClassName&gt;&gt;selector</c> with the remaining lines
    /// representing the method body.
    /// </summary>
    /// <param name="path">Path to the source file.</param>
    public void LoadSourceFile(string path)
    {
        var lines = System.IO.File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Source file is empty.");

        var header = lines[0];
        var parts = header.Split(new[] { ">>" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException("First line must be 'ClassName>>selector'.");

        var className = parts[0].Trim();
        var headerPart = parts[1].Trim();
        var (selectorName, args) = ParseSelector(headerPart);
        var body = string.Join(Environment.NewLine, lines[1..]);
        if (!body.TrimEnd().EndsWith("!"))
            body += "!";

        var parser = new SmalltalkParser(body);
        var superclass = SystemDictionary.TryGetClass("Object", out var objCls) ? objCls : null;
        var cls = SystemDictionary.DefineClass(className, superclass);
        var method = parser.ParseMethod(args, cls.InstanceVariables, selectorName);
        cls.MethodDictionary[new Symbol(selectorName)] = method;
    }

    /// <summary>
    /// Loads a Smalltalk system image or file-in source containing multiple class
    /// and method definitions. Uses the advanced SystemImageLoader which handles
    /// forward references and complex dependencies.
    /// </summary>
    /// <param name="path">The path to the system image or file-in source.</param>
    public void LoadSystemFile(string path)
    {
        var result = SystemImageLoader.LoadSystemImage(path);
        if (!result.Success)
        {
            Console.WriteLine($"[SYSTEM_LOAD] Failed to load {path}. Detailed errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"[SYSTEM_LOAD_ERROR] Line {error.Line}, Column {error.Column}: {error.Message}");
            }
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"[SYSTEM_LOAD_WARNING] Line {warning.Line}, Column {warning.Column}: {warning.Message}");
            }
            throw new InvalidOperationException($"Failed to load system file: {result.Errors.Count} errors");
        }
        SystemStatePrinter.PrintSummary();
    }

    /// <summary>
    /// Compiles the provided source code and evaluates the resulting method in
    /// the context of the global <c>Object</c> receiver.
    /// </summary>
    /// <param name="source">Smalltalk source code to execute.</param>
    /// <returns>The result of evaluating the method.</returns>
    public object? EvaluateSource(string source)
    {
        try
        {
            // For class initialization like "SmallInteger initialize", we need to:
            // 1. Parse the class name from the source
            // 2. Find the actual class object 
            // 3. Use that class as receiver

            var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1] == "initialize")
            {
                // This is a class initialization message
                var className = parts[0];
                if (SystemDictionary.TryGetClass(className, out var targetClass))
                {
                    // Use the class itself as the receiver for class-side messages
                    var method = ParserService.CompileExpression(source);
                    return Interpreter.Evaluate(method, targetClass);
                }
                else
                {
                    throw new InvalidOperationException($"Class {className} not found in system dictionary.");
                }
            }
            else
            {
                // For other expressions, use a default Object receiver
                var method = ParserService.CompileExpression(source);

                if (!SystemDictionary.TryGetClass("Object", out var objectClass))
                    throw new InvalidOperationException("Object class not found in system dictionary.");

                var receiver = new SmalltalkObject(objectClass, 0);
                return Interpreter.Evaluate(method, receiver);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate source: {source}. Error: {ex.Message}", ex);
        }
    }


    private static (string Selector, List<string> Arguments) ParseSelector(string line)
    {
        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var selectorParts = new List<string>();
        var args = new List<string>();

        for (int i = 0; i < tokens.Length; i++)
        {
            var tok = tokens[i];
            if (tok.EndsWith(':'))
            {
                selectorParts.Add(tok);
                if (i + 1 < tokens.Length)
                    args.Add(tokens[++i]);
            }
            else if (selectorParts.Count == 0)
            {
                selectorParts.Add(tok);
                if (i + 1 < tokens.Length)
                    args.Add(tokens[++i]);
            }
            else
            {
                selectorParts.Add(tok);
            }
        }

        var selector = string.Concat(selectorParts);
        return (selector, args);
    }

    /// <summary>
    /// Initialize all classes that have a class-side initialize method.
    /// This should be called after all classes and methods have been loaded.
    /// </summary>
    private void InitializeClasses()
    {
        Console.WriteLine("[BOOTSTRAP] Initializing global variables...");
        SystemDictionary.InitializeGlobals();
        
        Console.WriteLine("[BOOTSTRAP] Initializing classes...");
        var initializeSymbol = new Symbol("initialize");
        var initializedCount = 0;

        // Get all classes from the SystemDictionary
        foreach (var (className, cls) in SystemDictionary.AllClasses)
        {
            try
            {
                // Check if this class has a class-side initialize method
                // Class-side methods are now stored in the metaclass's regular MethodDictionary
                if (cls.Metaclass != null && cls.Metaclass.MethodDictionary.ContainsKey(initializeSymbol))
                {
                    Console.WriteLine($"[BOOTSTRAP] Initializing class: {className}");

                    // Verify the class has proper metaclass setup
                    if (cls.ClassPointer == null)
                    {
                        Console.WriteLine($"[BOOTSTRAP] Warning: Class {className} has no class pointer (metaclass). Skipping initialization.");
                        continue;
                    }

                    // Execute the initialization by evaluating "ClassName initialize"
                    EvaluateSource($"{className} initialize");
                    initializedCount++;

                    Console.WriteLine($"[BOOTSTRAP] Successfully initialized class: {className}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOTSTRAP] Warning: Failed to initialize class {className}: {ex.Message}");
            }
        }

        Console.WriteLine($"[BOOTSTRAP] Initialized {initializedCount} classes");
    }
}
