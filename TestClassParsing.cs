using System;
using System.IO;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Tests;

/// <summary>
/// Test program to verify class definition parsing.
/// </summary>
public static class TestClassParsing
{
    /// <summary>
    /// Main entry point for the test program.
    /// </summary>
    public static void Main()
    {
        var sourceCode = @"!Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!";
        
        Console.WriteLine("Testing class definition parsing...");
        Console.WriteLine("Source code:");
        Console.WriteLine(sourceCode);
        Console.WriteLine();
        
        try
        {
            // Set up ANTLR
            var inputStream = new AntlrInputStream(sourceCode);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Parse as file-in format
            var parseTree = parser.fileIn();
            
            // Build AST
            var astBuilder = new AstBuilder(sourceCode);
            var ast = astBuilder.Build(parseTree);
            
            // Check if we got a ProgramNode
            if (ast is ProgramNode program)
            {
                Console.WriteLine($"Successfully parsed! Found {program.Elements.Length} element(s).");
                
                // Look for ClassDefinitionNode
                foreach (var element in program.Elements)
                {
                    if (element is ClassDefinitionNode classDef)
                    {
                        Console.WriteLine($"\nClass Definition Found:");
                        Console.WriteLine($"  Name: {classDef.ClassName}");
                        Console.WriteLine($"  Superclass: {classDef.SuperclassName}");
                        Console.WriteLine($"  Subclass Kind: {classDef.SubclassKind}");
                        Console.WriteLine($"  Instance Variables: [{string.Join(", ", classDef.InstanceVariables)}]");
                        Console.WriteLine($"  Class Variables: [{string.Join(", ", classDef.ClassVariables)}]");
                        Console.WriteLine($"  Pool Dictionaries: [{string.Join(", ", classDef.PoolDictionaries)}]");
                        Console.WriteLine($"  Category: {classDef.Category}");
                        Console.WriteLine($"  Source preserved: {!string.IsNullOrEmpty(classDef.SourceText)}");
                        
                        // Verify expected values
                        var success = true;
                        if (classDef.ClassName != "Point")
                        {
                            Console.WriteLine($"ERROR: Expected class name 'Point', got '{classDef.ClassName}'");
                            success = false;
                        }
                        if (classDef.SuperclassName != "Object")
                        {
                            Console.WriteLine($"ERROR: Expected superclass 'Object', got '{classDef.SuperclassName}'");
                            success = false;
                        }
                        if (classDef.InstanceVariables.Length != 2 || 
                            classDef.InstanceVariables[0] != "x" || 
                            classDef.InstanceVariables[1] != "y")
                        {
                            Console.WriteLine($"ERROR: Expected instance variables [x, y], got [{string.Join(", ", classDef.InstanceVariables)}]");
                            success = false;
                        }
                        if (classDef.Category != "Graphics-Primitives")
                        {
                            Console.WriteLine($"ERROR: Expected category 'Graphics-Primitives', got '{classDef.Category}'");
                            success = false;
                        }
                        
                        if (success)
                        {
                            Console.WriteLine("\nAll tests PASSED!");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Expected ProgramNode, got {ast?.GetType().Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}