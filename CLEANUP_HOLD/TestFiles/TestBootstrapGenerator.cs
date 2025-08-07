// ==============================================================================
// Test program for the Updated Bootstrap Generator
// ==============================================================================

using System;
using System.IO;
using Smalltalk80;

class TestBootstrapGenerator
{
    static void Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Bootstrap Generator Test");
        Console.WriteLine("===========================================");
        Console.WriteLine();
        
        try
        {
            // Find Bootstrap.st
            string bootstrapPath = FindBootstrapFile();
            if (bootstrapPath == null)
            {
                Console.WriteLine("ERROR: Could not find Bootstrap.st");
                Console.WriteLine("Searched in:");
                Console.WriteLine("  - Current directory");
                Console.WriteLine("  - Smalltalk80 directory");
                Console.WriteLine("  - Parent directory");
                return;
            }
            
            Console.WriteLine($"Found Bootstrap.st at: {bootstrapPath}");
            Console.WriteLine();
            
            // Generate the bootstrap code
            string outputPath = "Smalltalk80/GeneratedBootstrap.cs";
            Console.WriteLine($"Generating bootstrap code to: {outputPath}");
            Console.WriteLine();
            
            UpdatedBootstrapGenerator.Generate(bootstrapPath, outputPath);
            
            // Show some statistics about what was generated
            var generatedCode = File.ReadAllText(outputPath);
            var lines = generatedCode.Split('\n');
            Console.WriteLine($"Generated {lines.Length} lines of C# code");
            
            // Try to compile and run the generated bootstrap
            Console.WriteLine();
            Console.WriteLine("Testing generated bootstrap...");
            Console.WriteLine("===========================================");
            
            // Clear existing state
            SystemDictionary.Clear();
            
            // Run the generated bootstrap
            Console.WriteLine("Calling GeneratedBootstrap.Initialize()...");
            GeneratedBootstrap.Initialize();
            
            // Verify the bootstrap worked
            Console.WriteLine();
            Console.WriteLine("Verification:");
            Console.WriteLine("===========================================");
            
            // Check that essential classes exist
            string[] essentialClasses = {
                "Object", "Class", "Metaclass", "Behavior",
                "Symbol", "Dictionary", "Array", "CompiledMethod",
                "UndefinedObject"
            };
            
            int foundCount = 0;
            foreach (var className in essentialClasses)
            {
                if (SystemDictionary.TryGetClass(className, out var cls))
                {
                    Console.WriteLine($"✓ {className} found");
                    foundCount++;
                    
                    // Check that it has a metaclass
                    if (cls.Metaclass != null)
                    {
                        Console.WriteLine($"  └─ Has metaclass");
                    }
                    
                    // Check method dictionary
                    if (cls.Fields.Length > 1 && cls.Fields[1] != null)
                    {
                        Console.WriteLine($"  └─ Has method dictionary");
                    }
                }
                else
                {
                    Console.WriteLine($"✗ {className} NOT FOUND");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"Found {foundCount}/{essentialClasses.Length} essential classes");
            
            // Check special objects
            Console.WriteLine();
            Console.WriteLine("Special objects:");
            Console.WriteLine($"  nil: {(SystemDictionary.Nil != null ? "✓" : "✗")}");
            Console.WriteLine($"  true: {(SystemDictionary.True != null ? "✓" : "✗")}");
            Console.WriteLine($"  false: {(SystemDictionary.False != null ? "✓" : "✗")}");
            
            // Check Smalltalk global
            if (SystemDictionary.TryGetGlobal("Smalltalk", out var smalltalk))
            {
                Console.WriteLine($"  Smalltalk global: ✓");
            }
            else
            {
                Console.WriteLine($"  Smalltalk global: ✗");
            }
            
            // Test method lookup
            Console.WriteLine();
            Console.WriteLine("Method lookup test:");
            if (SystemDictionary.TryGetClass("Object", out var objectClass))
            {
                // Check if Object has any methods installed
                var methodDict = objectClass.Fields[1] as SmalltalkObject;
                if (methodDict != null && methodDict.Fields.Length > 1)
                {
                    var dictArray = methodDict.Fields[1] as SmalltalkObject;
                    if (dictArray != null)
                    {
                        int methodCount = 0;
                        for (int i = 0; i < dictArray.Fields.Length; i++)
                        {
                            if (dictArray.Fields[i] != SystemDictionary.Nil &&
                                dictArray.Fields[i] != null)
                            {
                                methodCount++;
                            }
                        }
                        Console.WriteLine($"  Object has {methodCount} methods installed");
                    }
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("===========================================");
            Console.WriteLine("Bootstrap test complete!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    static string FindBootstrapFile()
    {
        // Try various locations for Bootstrap.st
        string[] searchPaths = {
            "Bootstrap.st",
            "Smalltalk80/Bootstrap.st",
            "../Bootstrap.st",
            "../../Bootstrap.st",
            "Smalltalk80/Smalltalk-80 Source/Bootstrap.st"
        };
        
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        
        return null;
    }
}