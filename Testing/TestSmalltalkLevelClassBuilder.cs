using System;
using System.Collections.Generic;
using Smalltalk80.Bootstrap;

namespace Testing;

/// <summary>
/// Tests the SmalltalkLevelClassBuilder functionality.
/// </summary>
public class TestSmalltalkLevelClassBuilder
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Testing SmalltalkLevelClassBuilder");
        Console.WriteLine("==================================");
        
        TestSourceGeneration();
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
        var builder = new SmalltalkLevelClassBuilder(null!, "dummy_path");
        
        try
        {
            // Use reflection to call the private method for testing
            var method = typeof(SmalltalkLevelClassBuilder).GetMethod("GenerateBuilderSource", 
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
}