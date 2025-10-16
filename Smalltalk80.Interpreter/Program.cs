using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using ClassHierarchyUtils = Smalltalk80.Interpreter.Engine.LoaderPhases.ClassHierarchyUtils;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// Loads a cold start bundle and initializes the virtual machine.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================\n");

        try
        {
            // Parse command line arguments
            var (coldStartFile, dumpClassName) = ParseCommandLine(args);

            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {coldStartFile}");
                Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
                Console.Error.WriteLine("                         (default: coldstart.json in executable directory)");
                Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
                Console.Error.WriteLine("                         after cold start completes");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Loading cold start bundle: {coldStartFile}");

            // Load the cold start bundle from JSON
            var bundle = ColdStartBundle.LoadFromFile(coldStartFile);

            Console.WriteLine($"Bundle loaded: {bundle.Metadata.Version}");
            Console.WriteLine($"  Symbols: {bundle.Symbols.Length}");
            Console.WriteLine($"  Classes: {bundle.Classes.Length}");
            Console.WriteLine($"  Objects: {bundle.Objects.Length}");
            Console.WriteLine($"  Methods: {bundle.Methods.Length}");
            Console.WriteLine();

            // Create the heap manager
            Console.WriteLine("Initializing heap manager...");
            var heapManager = new SmalltalkHeapManager();

            // Create and run the loader
            Console.WriteLine("Starting cold start loader...");
            var loader = new Loader(bundle, heapManager);
            loader.Run();

            // Execute init expressions - ONLY Float>>initialize for testing
            if (bundle.InitExpressions.Length > 0)
            {
                Console.WriteLine("\n=== Init Expression Execution ===");
                Console.WriteLine($"  Found {bundle.InitExpressions.Length} init expression(s) in bundle");
                Console.WriteLine($"  ⚠️  TEST MODE: Only executing Float>>initialize (init:123)");
                
                if (loader.LayoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                    return;
                }
                
                var vmMetadata = loader.BuildVMMetadata();
                var vm = new VirtualMachine(heapManager, loader.LayoutResolver, vmMetadata, bundle.Symbols);
                
                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();
                
                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
                    loader.LayoutResolver,
                    loader.LocalIdToObjectId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    InitPolicy.ContinueAndCollect,
                    defaultTimeoutMs: 5000,
                    symbols: bundle.Symbols,
                    strings: bundle.Strings,
                    allocator: loader.Allocator
                );
                
                // Filter to only execute Float>>initialize (init:123)
                var floatInitExpr = bundle.InitExpressions.FirstOrDefault(e => e.LocalId == "init:123");
                if (floatInitExpr != null)
                {
                    var filteredBundle = new ColdStartBundle
                    {
                        Metadata = bundle.Metadata,
                        Symbols = bundle.Symbols,
                        Strings = bundle.Strings,
                        OptimizedSelectors = bundle.OptimizedSelectors,
                        Classes = bundle.Classes,
                        Objects = bundle.Objects,
                        Methods = bundle.Methods,
                        InitExpressions = new[] { floatInitExpr },
                        InstallOrder = bundle.InstallOrder,
                        Patches = bundle.Patches
                    };
                    
                    var report = executor.ExecuteFromBundle(filteredBundle, loader.Allocator);
                    
                    Console.WriteLine($"  Executed: {report.Succeeded}/1");
                    if (report.Failed > 0)
                    {
                        Console.WriteLine($"  ⚠️  Failed: {report.Failed}");
                        foreach (var result in report.Results.Where(r => r.Status != InitStatus.Success))
                        {
                            Console.WriteLine($"    - {result.InitLocalId}: {result.Status} - {result.Diagnostic?.Message}");
                        }
                    }
                    else
                    {
                        // Diagnose Float class variables after successful initialization
                        Console.WriteLine("\n=== Float Class Variable Diagnostics ===");
                        DiagnoseFloatClassVariables(heapManager, loader, bundle);
                    }
                }
                else
                {
                    Console.WriteLine("  ⚠️  Float>>initialize (init:123) not found in bundle");
                }
            }

            Console.WriteLine("\n✓ All systems ready!");
            Console.WriteLine("Virtual machine initialized successfully.");

            // TODO: Implement --dump-class after ClassPrettyPrinter is migrated to new heap manager
            if (dumpClassName != null)
            {
                Console.WriteLine($"\n⚠️  --dump-class not yet implemented (ClassPrettyPrinter not migrated to new heap manager)");
            }

            // TODO: Start REPL or execute user code
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFatal error: {ex.Message}");
            Console.Error.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses command line arguments and returns the cold start file path and optional class name to dump.
    /// Supports: --coldstart path, --dump-class ClassName
    /// Default: coldstart.json in the same directory as the executable
    /// </summary>
    private static (string coldStartFile, string? dumpClassName) ParseCommandLine(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--coldstart" && i + 1 < args.Length)
            {
                coldStartFile = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i] == "--dump-class" && i + 1 < args.Length)
            {
                dumpClassName = args[i + 1];
                i++; // Skip the next argument
            }
        }

        // Default: coldstart.json in the executable directory
        if (coldStartFile == null)
        {
            string exeDir = AppContext.BaseDirectory;
            coldStartFile = Path.Combine(exeDir, "coldstart.json");
        }

        return (coldStartFile, dumpClassName);
    }

    private static void DiagnoseFloatClassVariables(ISmalltalkHeapManager heapManager, Loader loader, ColdStartBundle bundle)
    {
        try
        {
            // Expected values from Float>>initialize source code
            var expectedValues = new Dictionary<string, string>
            {
                { "Pi", "3.14159" },
                { "Halfpi", "1.5708 (Pi / 2.0)" },
                { "Fourthpi", "0.78540 (Pi / 4.0)" },
                { "Twopi", "6.28318 (Pi * 2.0)" },
                { "RadiansPerDegree", "0.01745 (Pi / 180.0)" },
                { "Ln2", "0.693147" },
                { "Sqrt2", "1.41421" },
                { "SinCoefficients", "Array: #(-0.166667 0.00833333 -1.98409e-4 2.7526e-6 -2.39e-8)" },
                { "TanCoefficients", "Array: #(0.333331 0.133392 0.0533741 0.0245651 0.00290052 0.00951681)" },
                { "ExpPCoefficients", "Array: #(28.8756 2525.04)" },
                { "ExpQCoefficients", "Array: #(1.0 375.022 7285.73)" },
                { "LnCoefficients", "Array: #(0.237625 0.285254 0.400006 0.666667 2.0)" }
            };

            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                Console.WriteLine("  ⚠️  Float class not found in GlobalRegistry");
                return;
            }
            var floatClassOOP = floatClassId;

            // Get the metaclass (Float class)
            var floatMetaclassOOP = heapManager.GetClassOOP(floatClassOOP);

            Console.WriteLine($"Float class: OOP={floatClassOOP} (index={floatClassId})");
            Console.WriteLine($"Float metaclass: OOP={floatMetaclassOOP}");
            Console.WriteLine();

            // Get class variables from SystemDictionary associations
            foreach (var varName in expectedValues.Keys)
            {
                var actual = GetClassVariableValue(heapManager, bundle, varName);
                var expected = expectedValues[varName];
                
                Console.WriteLine($"{varName}:");
                Console.WriteLine($"  Expected: {expected}");
                Console.WriteLine($"  Actual:   {actual}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Error diagnosing Float class variables: {ex.Message}");
        }
    }

    private static string GetClassVariableValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, string varName)
    {
        try
        {
            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                return "Float class not found";
            }
            var floatClassOOP = floatClassId;

            // Float class is an instance of Class, which has instance variables from Behavior/ClassDescription/Class hierarchy:
            // Behavior: superclass, methodDict, format, subclasses
            // ClassDescription: instanceVariables, organization
            // Class: name, classPool, sharedPools
            // So classPool is at index: 4 (Behavior) + 2 (ClassDescription) + 1 (name) = 7
            
            // Get the classPool instance variable (Dictionary containing class variables)
            var classPoolOOP = heapManager.GetInstanceVariable(floatClassOOP, 7);
            
            if (classPoolOOP == GlobalRegistry.NilOOP)
            {
                return "classPool is nil";
            }

            // classPool is a Dictionary with indexed array of associations
            int size = heapManager.GetIndexedSize(classPoolOOP);
            
            // Check if classPool is empty (no entries yet - they're lazily created)
            int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", bundle);
            if (tallyIndex >= 0)
            {
                var tallyOOP = heapManager.GetInstanceVariable(classPoolOOP, tallyIndex);
                if (tallyOOP.IsSmallInteger && tallyOOP.AsSmallInteger() == 0)
                {
                    return "classPool empty (variable not yet created - awaiting first access)";
                }
            }
            
            // Search for the variable name association
            for (int i = 0; i < size; i++)
            {
                var assocOOP = heapManager.GetIndexedOOP(classPoolOOP, i);
                if (assocOOP == GlobalRegistry.NilOOP)
                    continue;

                // Association has key at slot 1, value at slot 2
                var keyOOP = heapManager.GetSlot(assocOOP, 1);
                var keyName = GetSymbolName(heapManager, bundle, keyOOP);
                
                if (keyName == varName)
                {
                    var valueOOP = heapManager.GetSlot(assocOOP, 2);
                    return FormatValue(heapManager, bundle, valueOOP);
                }
            }

            return $"Not found in Float classPool (searched {size} slots)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string GetSymbolName(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP symbolOOP)
    {
        if (symbolOOP.IsSmallInteger)
            return $"SmallInteger({symbolOOP.AsSmallInteger()})";

        try
        {
            // Symbol is a byte-indexed object containing characters
            int size = heapManager.GetIndexedSize(symbolOOP);
            char[] chars = new char[size];
            for (int i = 0; i < size; i++)
            {
                chars[i] = (char)heapManager.GetIndexedByte(symbolOOP, i);
            }
            return new string(chars);
        }
        catch
        {
            return $"<Symbol OOP={symbolOOP}>";
        }
    }

    private static string FormatValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP valueOOP)
    {
        if (valueOOP.IsSmallInteger)
            return $"{valueOOP.AsSmallInteger()}";

        try
        {
            var format = heapManager.GetObjectFormat(valueOOP);
            
            // Check if it's a Float (ByteIndexed with 8 bytes)
            if (format == ObjectFormat.ByteIndexed && heapManager.GetIndexedSize(valueOOP) == 8)
            {
                byte[] bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    bytes[i] = heapManager.GetIndexedByte(valueOOP, i);
                }
                double value = BitConverter.ToDouble(bytes, 0);
                return $"{value:F6}";
            }
            
            // Check if it's an Array
            var classOOP = heapManager.GetClassOOP(valueOOP);
            if (GlobalRegistry.TryGet("class:Array", out SmalltalkOOP arrayClassOOP))
            {
                if (classOOP == arrayClassOOP)
                {
                    int size = heapManager.GetIndexedSize(valueOOP);
                    var elements = new List<string>();
                    for (int i = 0; i < size && i < 10; i++)  // Limit to 10 elements
                    {
                        var elemOOP = heapManager.GetIndexedOOP(valueOOP, i);
                        elements.Add(FormatValue(heapManager, bundle, elemOOP));
                    }
                    if (size > 10)
                        elements.Add("...");
                    return $"Array({size}): #({string.Join(" ", elements)})";
                }
            }

            return $"<Object OOP={valueOOP}, format={format}>";
        }
        catch (Exception ex)
        {
            return $"<Error: {ex.Message}>";
        }
    }
}
