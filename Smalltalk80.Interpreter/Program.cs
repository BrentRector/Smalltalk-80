using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Smalltalk-80 Interpreter - Runs the Smalltalk virtual machine and executes Smalltalk code.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================");
        Console.WriteLine();

        try
        {
            // Parse command line arguments
            var jsonManifests = new List<string>();
            var extendedLibraryManifests = new List<string>();
            var sourceFiles = new List<string>();
            var classesToPrint = new List<string>();
            bool skipExtendedLibrary = false;
            bool listBootstrapClasses = false;
            bool enableVMDebug = false;
            bool enableBootstrapDebug = false;
            bool useEnhancedPrinter = false;
            bool showBytecode = true;
            bool showMnemonics = true;
            bool testCriticalMethods = false;
            bool testUndefinedObject = false;
            bool enableMethodTracing = false;
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintHelp();
                    return 0;
                }
                else if (args[i] == "--json" && i + 1 < args.Length)
                {
                    jsonManifests.Add(args[++i]);
                }
                else if (args[i] == "--skip-extended" || args[i] == "--skip-phase3")
                {
                    skipExtendedLibrary = true;
                }
                else if (args[i] == "--list-bootstrap-classes" || args[i] == "--list-phase2-classes")
                {
                    listBootstrapClasses = true;
                    skipExtendedLibrary = true; // Always skip extended library when listing bootstrap classes
                }
                else if (args[i] == "--debug-vm")
                {
                    enableVMDebug = true;
                }
                else if (args[i] == "--debug-bootstrap")
                {
                    enableBootstrapDebug = true;
                }
                else if (args[i] == "--print-class" && i + 1 < args.Length)
                {
                    classesToPrint.Add(args[++i]);
                    useEnhancedPrinter = true;
                }
                else if (args[i] == "--enhanced-print")
                {
                    useEnhancedPrinter = true;
                }
                else if (args[i] == "--no-bytecode")
                {
                    showBytecode = false;
                }
                else if (args[i] == "--no-mnemonics")
                {
                    showMnemonics = false;
                }
                else if (args[i] == "--test-critical-methods")
                {
                    testCriticalMethods = true;
                }
                else if (args[i] == "--test-undefined-object")
                {
                    testUndefinedObject = true;
                }
                else if (args[i] == "--trace-methods")
                {
                    enableMethodTracing = true;
                }
                else if ((args[i] == "--extended-library" || args[i] == "--phase3") && i + 1 < args.Length)
                {
                    extendedLibraryManifests.Add(args[++i]);
                    skipExtendedLibrary = true; // Skip standard extended library when loading custom manifests
                }
                else if (!args[i].StartsWith("--"))
                {
                    sourceFiles.Add(args[i]);
                }
            }
            
            Console.WriteLine("Initializing Virtual Machine...");
            
            // Set VM debug output flag
            if (enableVMDebug)
            {
                Bootstrap.VMPrimitiveAdapter.EnableVMDebugOutput = true;
                Console.WriteLine("✓ VM debug output enabled");
            }
            
            // Set bootstrap debug output flags
            if (enableBootstrapDebug)
            {
                Bootstrap.JsonBootstrapExecutor.EnableBootstrapDebugOutput = true;
                Bootstrap.Phase2Executor.EnablePhase2DebugOutput = true;
                Console.WriteLine("✓ Bootstrap debug output enabled");
            }
            
            // Enable method tracing if requested
            if (enableMethodTracing)
            {
                MethodTracer.Enabled = true;
                Console.WriteLine("✓ Method tracing enabled");
            }
            
            // Create the VM with bootstrap through extended library
            var vm = new VirtualMachine(skipStandardLibrary: skipExtendedLibrary);
            
            Console.WriteLine("✓ Virtual Machine created");
            Console.WriteLine();
            
            // Load additional JSON manifests if specified
            if (jsonManifests.Count > 0)
            {
                foreach (var jsonFile in jsonManifests)
                {
                    Console.WriteLine($"[BOOTSTRAP] Loading additional JSON manifest: {jsonFile}");
                    try
                    {
                        vm.LoadJsonManifest(jsonFile);
                        Console.WriteLine($"✓ Successfully loaded JSON manifest: {jsonFile}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed to load JSON manifest {jsonFile}: {ex.Message}");
                    }
                }
            }
            
            // Load extended library manifests if specified
            if (extendedLibraryManifests.Count > 0 && vm.Adapter != null)
            {
                Console.WriteLine();
                Console.WriteLine("=== Loading Extended Library Manifests ===");
                
                // Enable VM debug if requested
                if (enableVMDebug)
                {
                    Bootstrap.VMPrimitiveAdapter.EnableVMDebugOutput = true;
                }
                
                // Check if the manifest requests bytecode execution
                bool useBytecodeExecution = false;
                foreach (var manifestFile in extendedLibraryManifests)
                {
                    if (manifestFile.Contains("test_counter", StringComparison.OrdinalIgnoreCase) ||
                        manifestFile.Contains("bytecode", StringComparison.OrdinalIgnoreCase))
                    {
                        useBytecodeExecution = true;
                        break;
                    }
                }
                
                if (useBytecodeExecution)
                {
                    // Use the Phase2Executor for bytecode-based execution
                    Console.WriteLine("Using bytecode executor for extended library...");
                    var phase2Executor = new Bootstrap.Phase2Executor(vm.Adapter);
                    
                    // Load the test counter manifest
                    var testCounterManifest = extendedLibraryManifests.FirstOrDefault(f => f.Contains("test_counter.json"));
                    if (testCounterManifest != null)
                    {
                        try
                        {
                            var jsonContent = System.IO.File.ReadAllText(testCounterManifest);
                            phase2Executor.ExecuteManifest(jsonContent);
                            Console.WriteLine($"✓ Successfully loaded extended library manifest: {testCounterManifest}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"✗ Failed to load manifest: {ex.Message}");
                            if (enableBootstrapDebug)
                            {
                                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                            }
                        }
                    }
                }
                else
                {
                    // Use the regular Phase2Executor
                    var phase2Executor = new Bootstrap.Phase2Executor(vm.Adapter);
                    
                    foreach (var manifestFile in extendedLibraryManifests)
                    {
                        Console.WriteLine($"\nLoading extended library manifest: {manifestFile}");
                        try
                        {
                            var jsonContent = System.IO.File.ReadAllText(manifestFile);
                            phase2Executor.ExecuteManifest(jsonContent);
                            Console.WriteLine($"✓ Successfully loaded extended library manifest: {manifestFile}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"✗ Failed to load extended library manifest {manifestFile}: {ex.Message}");
                            if (enableBootstrapDebug)
                            {
                                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                            }
                        }
                    }
                }
            }
            else if (extendedLibraryManifests.Count > 0)
            {
                Console.WriteLine("ERROR: VM adapter not available for extended library execution");
            }
            
            // If listing bootstrap classes, do that and exit
            if (listBootstrapClasses)
            {
                ListBootstrapClasses(vm);
                return 0;
            }
            
            // If testing critical methods, do that
            if (testCriticalMethods)
            {
                // Suppress the Phase 2 dump for testing
                TestCriticalMethods.RunTests(vm);
                return 0;
            }
            
            // If testing UndefinedObject, do that
            if (testUndefinedObject)
            {
                TestUndefinedObject.RunTests(vm);
                return 0;
            }
            
            // Load Smalltalk source files if specified
            if (sourceFiles.Count > 0)
            {
                foreach (var file in sourceFiles)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Loading: {file}");
                    try
                    {
                        vm.LoadSourceFile(file);
                        Console.WriteLine($"✓ Successfully loaded: {file}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed to load {file}: {ex.Message}");
                    }
                }
            }
            
            // Print final VM state using proper Smalltalk object examination
            Console.WriteLine();
            if (vm.Adapter != null)
            {
                if (useEnhancedPrinter)
                {
                    var enhancedPrinter = new SmalltalkPrettyPrinterEnhanced(vm.Adapter, showBytecode, showMnemonics);
                    if (classesToPrint.Count > 0)
                    {
                        enhancedPrinter.SetClassFilter(classesToPrint.ToArray());
                    }
                    enhancedPrinter.PrintSystemState();
                }
                else
                {
                    var prettyPrinter = new SmalltalkPrettyPrinter(vm.Adapter);
                    prettyPrinter.PrintSystemState();
                }
            }
            else
            {
                Console.WriteLine("=== VM State ===");
                PrettyPrintVMState(vm);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Pretty prints the internal state of the Smalltalk VM.
    /// </summary>
    private static void PrettyPrintVMState(VirtualMachine vm)
    {
        Console.WriteLine("=== Smalltalk VM State ===");
        Console.WriteLine();
        
        // Get all objects from the VM's object memory
        var allObjects = ObjectMemory.Objects.ToList();
        
        Console.WriteLine($"Total objects in memory: {allObjects.Count}");
        Console.WriteLine();
        
        // Find class-like objects (objects that look like classes)
        var classObjects = new List<(SmalltalkObject obj, string name)>();
        
        foreach (var obj in allObjects)
        {
            // Try to determine if this is a class object
            if (IsLikelyClass(obj))
            {
                var className = GetClassName(vm, obj);
                if (!string.IsNullOrEmpty(className))
                {
                    classObjects.Add((obj, className));
                }
            }
        }
        
        // Sort classes by name
        classObjects.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        
        Console.WriteLine($"Classes found: {classObjects.Count}");
        Console.WriteLine("=" + new string('=', 40));
        
        foreach (var (classObj, className) in classObjects)
        {
            PrettyPrintClass(vm, classObj, className);
        }
        
        // Print global variables
        Console.WriteLine();
        Console.WriteLine("=== Global Variables ===");
        var globals = vm.GetGlobalVariables();
        foreach (var kvp in globals.OrderBy(k => k.Key))
        {
            var valueDesc = DescribeObject(vm, kvp.Value);
            Console.WriteLine($"  {kvp.Key} = {valueDesc}");
        }
    }
    
    /// <summary>
    /// Pretty prints a single class object.
    /// </summary>
    private static void PrettyPrintClass(VirtualMachine vm, SmalltalkObject classObj, string className)
    {
        Console.WriteLine();
        Console.WriteLine($"Class: {className}");
        Console.WriteLine("-" + new string('-', className.Length + 6));
        
        // Get superclass
        var superclass = GetSuperclass(classObj);
        if (superclass != null)
        {
            var superclassName = GetClassName(vm, superclass);
            Console.WriteLine($"  Superclass: {superclassName ?? "Unknown"}");
        }
        else
        {
            Console.WriteLine($"  Superclass: nil");
        }
        
        // Get type (regular class or metaclass)
        var isMetaclass = className.EndsWith(" class");
        Console.WriteLine($"  Type: {(isMetaclass ? "Metaclass" : "Class")}");
        
        // Get instance variables
        var instVars = GetInstanceVariables(classObj);
        if (instVars.Count > 0)
        {
            Console.WriteLine($"  Instance Variables: {string.Join(", ", instVars)}");
        }
        
        // Get methods
        var methods = GetMethods(classObj);
        if (methods.Count > 0)
        {
            Console.WriteLine($"  Methods ({methods.Count}):");
            foreach (var method in methods.OrderBy(m => m.selector))
            {
                PrintMethod(method.selector, method.method);
            }
        }
    }
    
    /// <summary>
    /// Prints information about a method.
    /// </summary>
    private static void PrintMethod(string selector, CompiledMethod method)
    {
        Console.Write($"    {selector}");
        
        // Show method header info
        var header = method.Header;
        Console.Write($" (temps: {header.TemporaryCount}, args: {header.ArgumentCount})");
        
        // Show bytecode length
        if (method.Bytecode != null && method.Bytecode.Length > 0)
        {
            Console.Write($" [{method.Bytecode.Length} bytes]");
            
            // Show first few bytecodes if small
            if (method.Bytecode.Length <= 10)
            {
                Console.Write(" <");
                Console.Write(string.Join(" ", method.Bytecode.Select(b => b.ToString("X2"))));
                Console.Write(">");
            }
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Determines if an object looks like a class.
    /// </summary>
    private static bool IsLikelyClass(SmalltalkObject obj)
    {
        // Classes typically have certain fields set
        // This is a heuristic - adjust based on actual VM implementation
        if (obj == null) return false;
        
        // Check if it has a method dictionary
        if (obj.MethodDictionary != null && obj.MethodDictionary.Count > 0)
            return true;
            
        // Check if it's referenced as a class by other objects
        // Or has other class-like properties
        return false;
    }
    
    /// <summary>
    /// Gets the name of a class object.
    /// </summary>
    private static string? GetClassName(VirtualMachine vm, SmalltalkObject classObj)
    {
        // Try to find the class name
        // This might be stored in various ways depending on the VM implementation
        
        // First check if this object is registered as a global
        var globals = vm.GetGlobalVariables();
        foreach (var kvp in globals)
        {
            if (ReferenceEquals(kvp.Value, classObj))
            {
                return kvp.Key;
            }
        }
        
        // For metaclasses, the name might be derived
        // Check if there's a 'thisClass' field pointing to the instance side
        if (classObj.TryGetField("thisClass", out var thisClass) && thisClass is SmalltalkObject thisClassObj)
        {
            var baseName = GetClassName(vm, thisClassObj);
            if (baseName != null)
            {
                return baseName + " class";
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the superclass of a class object.
    /// </summary>
    private static SmalltalkObject? GetSuperclass(SmalltalkObject classObj)
    {
        return classObj.Superclass;
    }
    
    /// <summary>
    /// Gets the instance variables of a class.
    /// </summary>
    private static List<string> GetInstanceVariables(SmalltalkObject classObj)
    {
        if (classObj.InstanceVariables != null)
        {
            return classObj.InstanceVariables;
        }
        return new List<string>();
    }
    
    /// <summary>
    /// Gets the methods of a class.
    /// </summary>
    private static List<(string selector, CompiledMethod method)> GetMethods(SmalltalkObject classObj)
    {
        var methods = new List<(string selector, CompiledMethod method)>();
        
        if (classObj.MethodDictionary != null)
        {
            foreach (var kvp in classObj.MethodDictionary)
            {
                methods.Add((kvp.Key.Value, kvp.Value));
            }
        }
        
        return methods;
    }
    
    /// <summary>
    /// Provides a string description of an object.
    /// </summary>
    private static string DescribeObject(VirtualMachine vm, SmalltalkObject? obj)
    {
        if (obj == null)
            return "nil";
            
        // Check if it's a known special object
        // TODO: Check against global objects once ObjectMemory is updated
            
        // Check if it's a class
        var className = GetClassName(vm, obj);
        if (!string.IsNullOrEmpty(className))
            return $"<Class: {className}>";
            
        // Check if it's a small integer
        if (obj.IsSmallInteger)
            return obj.SmallIntegerValue.ToString();
            
        // Check if it's a string
        if (obj.TryGetStringValue(out var strValue))
            return $"'{strValue}'";
            
        // Check if it's a symbol
        // Symbol would need special handling
            
        // Default description
        return $"<{obj.GetType().Name}@{obj.GetHashCode():X}>";
    }
    
    private static void ListBootstrapClasses(VirtualMachine vm)
    {
        Console.WriteLine("\n=== Classes Present After Bootstrap ===");
        
        // Get all objects and find the ones that look like classes
        var allObjects = ObjectMemory.Objects.ToList();
        var classNames = new List<string>();
        
        // Try to get all class names by checking all objects that look like symbols
        // and seeing if they resolve to class objects
        var potentialClassNames = new HashSet<string>();
        
        // First add all known classes from bootstrap
        var knownClasses = new[] {
            // Bootstrap classes - Object is the root!
            "Object", "Behavior", "Class", "Metaclass",
            "CompiledMethod", "MethodDictionary",
            "Symbol", "String", "Array", "ByteArray",
            "Collection", "Dictionary", "SystemDictionary",
            // Phase 2 classes - including intermediate classes
            "UndefinedObject", "SmallInteger", "Boolean", "True", "False",
            "ClassDescription", "Message", "MethodContext", "BlockContext",
            "Association", "SequenceableCollection", 
            "ArrayedCollection", "Set", "IdentityDictionary",
            // Intermediate classes from system image hierarchy
            "Magnitude", "Number", "Integer", "LookupKey",
            "InstructionStream", "ContextPart"
        };
        
        foreach (var name in knownClasses)
        {
            potentialClassNames.Add(name);
        }
        
        // Check which classes actually exist
        foreach (var className in potentialClassNames.OrderBy(n => n))
        {
            var oop = vm.Adapter?.PrimitiveLookupGlobal(className) ?? -1;
            if (oop >= 0)  // Object has OOP 0, so we need >= not >
            {
                classNames.Add(className);
            }
        }
        
        Console.WriteLine($"\nFound {classNames.Count} classes:");
        Console.WriteLine("------------------------");
        foreach (var name in classNames)
        {
            Console.WriteLine($"  - {name}");
        }
        
        // Generate the command to extract these classes
        Console.WriteLine("\n=== Compiler Extraction Command ===");
        Console.WriteLine("To extract these classes from the system image, run:");
        Console.WriteLine();
        var classArgs = string.Join(" ", classNames.Select(n => $"-c {n}"));
        Console.WriteLine($"dotnet run --project Smalltalk80.Compiler -- --compile-classes {classArgs} -o bootstrap_phase2_complete.json \"Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st\"");
    }
    
    /// <summary>
    /// Prints the help message for the interpreter.
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine("Usage: Smalltalk80.Interpreter [options] [source-files...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h                      Show this help message");
        Console.WriteLine("  --json <file>                   Load additional JSON manifest");
        Console.WriteLine("  --skip-extended                 Skip loading extended library (formerly --skip-phase3)");
        Console.WriteLine("  --extended-library <file>       Load custom extended library manifest (formerly --phase3)");
        Console.WriteLine("  --list-bootstrap-classes        List all classes in bootstrap");
        Console.WriteLine("  --debug-vm                      Enable VM debug output");
        Console.WriteLine("  --debug-bootstrap               Enable bootstrap debug output");
        Console.WriteLine("  --print-class <name>            Print specific class details");
        Console.WriteLine("  --enhanced-print                Use enhanced pretty printer");
        Console.WriteLine("  --no-bytecode                   Hide bytecode in method displays");
        Console.WriteLine("  --no-mnemonics                  Hide mnemonics in method displays");
        Console.WriteLine("  --trace-methods                 Enable method entry/exit tracing");
        Console.WriteLine("  --test-critical-methods         Run critical method tests");
        Console.WriteLine("  --test-undefined-object         Run UndefinedObject tests");
        Console.WriteLine();
        Console.WriteLine("Source Files:");
        Console.WriteLine("  Any non-option arguments are treated as Smalltalk source files to load");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Smalltalk80.Interpreter                       # Run with default bootstrap");
        Console.WriteLine("  Smalltalk80.Interpreter --enhanced-print      # Use enhanced pretty printer");
        Console.WriteLine("  Smalltalk80.Interpreter --print-class Object  # Print Object class details");
        Console.WriteLine("  Smalltalk80.Interpreter --trace-methods       # Enable method tracing");
        Console.WriteLine("  Smalltalk80.Interpreter mycode.st             # Load Smalltalk source file");
        Console.WriteLine();
        Console.WriteLine("Bootstrap Process:");
        Console.WriteLine("  1. Core bootstrap with essential classes and methods");
        Console.WriteLine("  2. Extended library (optional, can be skipped with --skip-extended)");
        Console.WriteLine("  3. Custom manifests via --extended-library option");
    }
}