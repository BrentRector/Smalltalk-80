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
            var phase3Manifests = new List<string>();
            var sourceFiles = new List<string>();
            var classesToPrint = new List<string>();
            bool skipPhase3 = false;
            bool listPhase2Classes = false;
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
                if (args[i] == "--json" && i + 1 < args.Length)
                {
                    jsonManifests.Add(args[++i]);
                }
                else if (args[i] == "--skip-phase3")
                {
                    skipPhase3 = true;
                }
                else if (args[i] == "--list-phase2-classes")
                {
                    listPhase2Classes = true;
                    skipPhase3 = true; // Always skip Phase 3 when listing Phase 2 classes
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
                else if (args[i] == "--phase3" && i + 1 < args.Length)
                {
                    phase3Manifests.Add(args[++i]);
                    skipPhase3 = true; // Skip standard Phase 3 when loading custom manifests
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
            
            // Create the VM with bootstrap through Phase 3
            var vm = new VirtualMachine(skipStandardLibrary: skipPhase3);
            
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
            
            // Load Phase 3 high-level manifests if specified
            if (phase3Manifests.Count > 0 && vm.Adapter != null)
            {
                Console.WriteLine();
                Console.WriteLine("=== Loading Phase 3 High-Level Manifests ===");
                
                // Enable VM debug if requested
                if (enableVMDebug)
                {
                    Bootstrap.VMPrimitiveAdapter.EnableVMDebugOutput = true;
                }
                
                // Check if the manifest requests bytecode execution
                bool useBytecodeExecution = false;
                foreach (var manifestFile in phase3Manifests)
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
                    Console.WriteLine("Using bytecode executor for Phase 2...");
                    var phase2Executor = new Bootstrap.Phase2Executor(vm.Adapter);
                    
                    // Load the test counter manifest
                    var testCounterManifest = phase3Manifests.FirstOrDefault(f => f.Contains("phase2_test_counter.json"));
                    if (testCounterManifest != null)
                    {
                        try
                        {
                            var jsonContent = System.IO.File.ReadAllText(testCounterManifest);
                            phase2Executor.ExecuteManifest(jsonContent);
                            Console.WriteLine($"✓ Successfully loaded Phase 3 manifest: {testCounterManifest}");
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
                    
                    foreach (var manifestFile in phase3Manifests)
                    {
                        Console.WriteLine($"\nLoading Phase 3 manifest: {manifestFile}");
                        try
                        {
                            var jsonContent = System.IO.File.ReadAllText(manifestFile);
                            phase2Executor.ExecuteManifest(jsonContent);
                            Console.WriteLine($"✓ Successfully loaded Phase 3 manifest: {manifestFile}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"✗ Failed to load Phase 3 manifest {manifestFile}: {ex.Message}");
                            if (enableBootstrapDebug)
                            {
                                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                            }
                        }
                    }
                }
            }
            else if (phase3Manifests.Count > 0)
            {
                Console.WriteLine("ERROR: VM adapter not available for Phase 3 execution");
            }
            
            // If listing Phase 2 classes, do that and exit
            if (listPhase2Classes)
            {
                ListPhase2Classes(vm);
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
    
    private static void ListPhase2Classes(VirtualMachine vm)
    {
        Console.WriteLine("\n=== Classes Present After Phase 2 ===");
        
        // Get all objects and find the ones that look like classes
        var allObjects = ObjectMemory.Objects.ToList();
        var classNames = new List<string>();
        
        // Try to get all class names by checking all objects that look like symbols
        // and seeing if they resolve to class objects
        var potentialClassNames = new HashSet<string>();
        
        // First add all known classes from both phases
        var knownClasses = new[] {
            // Phase 1 classes - Object is the root!
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
}