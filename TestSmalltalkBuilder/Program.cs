using System;
using System.Collections.Generic;
using Smalltalk80.Bootstrap;

namespace TestSmalltalkBuilder;

/// <summary>
/// Tests the SmalltalkLevelClassBuilder functionality.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Testing SmalltalkLevelClassBuilder");
        Console.WriteLine("==================================");
        
        TestSourceGeneration();
        TestStandaloneCompilerIntegration();
        TestSelfContainedGeneration();
    }
    
    private static void TestSourceGeneration()
    {
        Console.WriteLine("\n1. Testing Smalltalk source generation...");
        
        // Create a simple test class definition
        var classDef = new SmalltalkLevelClassBuilder.ClassDefinition
        {
            Name = "TestDisplayObject",
            Superclass = "DisplayObject",
            InstanceVariables = new List<string> { "extent", "offset" },
            ClassVariables = new List<string>(),
            PoolDictionaries = new List<string>(),
            Category = "Graphics-Test"
        };
        
        // Create some test methods
        var methods = new List<SmalltalkLevelClassBuilder.MethodDefinition>
        {
            new SmalltalkLevelClassBuilder.MethodDefinition
            {
                ClassName = "TestDisplayObject",
                Selector = "extent",
                Bytecode = new List<int> { 0x70, 0x7C }, // pushReceiverVariable: 0, returnTop
                Literals = new List<string>(),
                PrimitiveIndex = 0,
                NumArgs = 0,
                NumTemps = 0,
                IsClassMethod = false
            },
            new SmalltalkLevelClassBuilder.MethodDefinition
            {
                ClassName = "TestDisplayObject",
                Selector = "extent:",
                Bytecode = new List<int> { 0x68, 0x80, 0x7C }, // pushTemporaryVariable: 0, storeReceiverVariable: 0, returnTop
                Literals = new List<string>(),
                PrimitiveIndex = 0,
                NumArgs = 1,
                NumTemps = 0,
                IsClassMethod = false
            }
        };
        
        // Create builder (without VM for source generation test)
        var systemImagePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "complete_system_final.json");
        if (!File.Exists(systemImagePath))
        {
            Console.WriteLine($"✗ System image not found at: {systemImagePath}");
            return;
        }
        var builder = new SmalltalkLevelClassBuilder(null!, "dummy_path", systemImagePath);
        
        try
        {
            // Use reflection to call the private method for testing
            var method = typeof(SmalltalkLevelClassBuilder).GetMethod("GenerateBuilderClassSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var source = (string)method!.Invoke(builder, new object[] { "TestBuilder1", classDef, methods })!;
            
            Console.WriteLine("Generated Smalltalk source:");
            Console.WriteLine("---------------------------");
            Console.WriteLine(source);
            Console.WriteLine("---------------------------");
            
            // Verify source contains expected elements
            if (source.Contains("Object subclass: #TestBuilder1") &&
                source.Contains("buildIt") &&
                source.Contains("Smalltalk at: #DisplayObject") &&
                source.Contains("subclass: #TestDisplayObject") &&
                source.Contains("instanceVariableNames: 'extent offset'") &&
                source.Contains("installMethod: #extent") &&
                source.Contains("installMethod: #extent:"))
            {
                Console.WriteLine("✓ Source generation test PASSED");
            }
            else
            {
                Console.WriteLine("✗ Source generation test FAILED - missing expected elements");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Source generation test FAILED: {ex.Message}");
        }
    }
    
    private static void TestStandaloneCompilerIntegration()
    {
        Console.WriteLine("\n2. Testing standalone compiler integration...");
        
        try
        {
            // Path to standalone compiler
            var compilerPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "bin", "Debug", "net9.0", "StandaloneCompiler.exe");
            if (!File.Exists(compilerPath))
            {
                Console.WriteLine($"✗ Standalone compiler not found at: {compilerPath}");
                return;
            }
            
            // Create a test class that requires dependencies (DisplayObject hierarchy)
            var classDef = new SmalltalkLevelClassBuilder.ClassDefinition
            {
                Name = "TestDisplayWidget",
                Superclass = "DisplayObject",
                InstanceVariables = new List<string> { "widget", "active" },
                ClassVariables = new List<string>(),
                PoolDictionaries = new List<string>(),
                Category = "Graphics-Test"
            };
            
            // Create test methods
            var methods = new List<SmalltalkLevelClassBuilder.MethodDefinition>
            {
                new SmalltalkLevelClassBuilder.MethodDefinition
                {
                    ClassName = "TestDisplayWidget",
                    Selector = "widget",
                    Bytecode = new List<int> { 0x70, 0x7C }, // pushReceiverVariable: 0, returnTop
                    Literals = new List<string>(),
                    PrimitiveIndex = 0,
                    NumArgs = 0,
                    NumTemps = 0,
                    IsClassMethod = false
                },
                new SmalltalkLevelClassBuilder.MethodDefinition
                {
                    ClassName = "TestDisplayWidget", 
                    Selector = "activate",
                    Bytecode = new List<int> { 0x71, 0x81, 0x70, 0x7C }, // pushReceiverVariable: 1, storeReceiverVariable: 1, pushReceiver, returnTop
                    Literals = new List<string>(),
                    PrimitiveIndex = 0,
                    NumArgs = 0,
                    NumTemps = 0,
                    IsClassMethod = false
                }
            };
            
            // Path to system image
            var systemImagePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "complete_system_final.json");
            if (!File.Exists(systemImagePath))
            {
                Console.WriteLine($"✗ System image not found at: {systemImagePath}");
                return;
            }
            
            // Create builder 
            var builder = new SmalltalkLevelClassBuilder(null!, compilerPath, systemImagePath);
            
            // Use reflection to call the private methods
            var generateMethod = typeof(SmalltalkLevelClassBuilder).GetMethod("GenerateSelfContainedBuilderSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var compileMethod = typeof(SmalltalkLevelClassBuilder).GetMethod("CompileBuilderClass", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Generate self-contained source with dependencies
            var source = (string)generateMethod!.Invoke(builder, new object[] { "DisplayWidgetBuilder", classDef, methods })!;
            Console.WriteLine("Generated Smalltalk source successfully");
            
            // Try to compile it
            Console.WriteLine("Attempting to compile with standalone compiler...");
            var compiledClass = compileMethod!.Invoke(builder, new object[] { "DisplayWidgetBuilder", source });
            
            Console.WriteLine("✓ Standalone compiler integration test PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Standalone compiler test FAILED: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
        }
    }
    
    private static void TestSelfContainedGeneration()
    {
        Console.WriteLine("\n3. Testing self-contained source generation with dependencies...");
        
        try
        {
            // Create a test class that requires dependencies
            var classDef = new SmalltalkLevelClassBuilder.ClassDefinition
            {
                Name = "TestWidget",
                Superclass = "DisplayObject",
                InstanceVariables = new List<string> { "bounds" },
                ClassVariables = new List<string>(),
                PoolDictionaries = new List<string>(),
                Category = "Graphics-Widgets"
            };
            
            var methods = new List<SmalltalkLevelClassBuilder.MethodDefinition>
            {
                new SmalltalkLevelClassBuilder.MethodDefinition
                {
                    ClassName = "TestWidget",
                    Selector = "bounds",
                    Bytecode = new List<int> { 0x70, 0x7C },
                    Literals = new List<string>(),
                    PrimitiveIndex = 0,
                    NumArgs = 0,
                    NumTemps = 0,
                    IsClassMethod = false
                }
            };
            
            var systemImagePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "complete_system_final.json");
            if (!File.Exists(systemImagePath))
            {
                Console.WriteLine($"✗ System image not found at: {systemImagePath}");
                return;
            }
            
            var builder = new SmalltalkLevelClassBuilder(null!, "dummy", systemImagePath);
            
            // Generate self-contained source
            var generateMethod = typeof(SmalltalkLevelClassBuilder).GetMethod("GenerateSelfContainedBuilderSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var source = (string)generateMethod!.Invoke(builder, new object[] { "TestWidgetBuilder", classDef, methods })!;
            
            Console.WriteLine("Generated self-contained source (first 2000 chars):");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(source.Length > 2000 ? source.Substring(0, 2000) + "..." : source);
            Console.WriteLine("--------------------------------------------------");
            
            // Check that dependencies are included
            if (source.Contains("Object subclass: #DisplayObject") && 
                source.Contains("Object subclass: #TestWidgetBuilder") &&
                source.Contains("superclass := Smalltalk at: #DisplayObject"))
            {
                Console.WriteLine("✓ Self-contained generation test PASSED - includes dependencies and builder");
                Console.WriteLine($"  Source length: {source.Length} characters");
                Console.WriteLine($"  Includes Object definition: {source.Contains("nil subclass: #Object")}");
                Console.WriteLine($"  Includes DisplayObject definition: {source.Contains("Object subclass: #DisplayObject")}");
                Console.WriteLine($"  Includes TestWidgetBuilder: {source.Contains("TestWidgetBuilder")}");
            }
            else
            {
                Console.WriteLine("✗ Self-contained generation test FAILED - missing dependencies");
                Console.WriteLine($"  Has Object->DisplayObject: {source.Contains("Object subclass: #DisplayObject")}");
                Console.WriteLine($"  Has TestWidgetBuilder: {source.Contains("TestWidgetBuilder")}");
                Console.WriteLine($"  Has superclass lookup: {source.Contains("superclass := Smalltalk at: #DisplayObject")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Self-contained generation test FAILED: {ex.Message}");
        }
    }
}