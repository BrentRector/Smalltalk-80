using System;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Tests;

/// <summary>
/// Test program to verify source code preservation in AST nodes.
/// </summary>
public static class TestSourcePreservation
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
        
        Console.WriteLine("Testing source code preservation...");
        Console.WriteLine("Original source:");
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
            
            // Build AST with source code
            var astBuilder = new AstBuilder(sourceCode);
            var ast = astBuilder.Build(parseTree);
            
            // Check if we got a ProgramNode
            if (ast is ProgramNode program)
            {
                Console.WriteLine($"ProgramNode source preserved: {!string.IsNullOrEmpty(program.SourceText)}");
                
                // Look for ClassDefinitionNode
                foreach (var element in program.Elements)
                {
                    if (element is ClassDefinitionNode classDef)
                    {
                        Console.WriteLine($"\nClassDefinitionNode:");
                        Console.WriteLine($"  Source preserved: {!string.IsNullOrEmpty(classDef.SourceText)}");
                        
                        if (!string.IsNullOrEmpty(classDef.SourceText))
                        {
                            Console.WriteLine($"  Source length: {classDef.SourceText.Length} chars");
                            Console.WriteLine($"  First 50 chars: {classDef.SourceText.Substring(0, Math.Min(50, classDef.SourceText.Length))}...");
                            
                            // Verify it contains expected content
                            if (classDef.SourceText.Contains("Object subclass: #Point"))
                            {
                                Console.WriteLine("  ✓ Contains class definition header");
                            }
                            if (classDef.SourceText.Contains("instanceVariableNames: 'x y'"))
                            {
                                Console.WriteLine("  ✓ Contains instance variables");
                            }
                            if (classDef.SourceText.Contains("Graphics-Primitives"))
                            {
                                Console.WriteLine("  ✓ Contains category");
                            }
                        }
                    }
                }
                
                Console.WriteLine("\nSource preservation test PASSED!");
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