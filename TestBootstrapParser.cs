// ==============================================================================
// Simplified test to verify we can parse Bootstrap.st
// ==============================================================================

using System;
using System.IO;
using Smalltalk80.Parser;

namespace Smalltalk80;

public class TestBootstrapParser
{
    public static void Main(string[] args)
    {
        try
        {
            string bootstrapPath = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80\Bootstrap.st";
            
            if (!File.Exists(bootstrapPath))
            {
                Console.WriteLine($"Bootstrap.st not found at: {bootstrapPath}");
                return;
            }
            
            Console.WriteLine($"Reading Bootstrap.st from: {bootstrapPath}");
            var content = File.ReadAllText(bootstrapPath);
            Console.WriteLine($"File size: {content.Length} characters");
            
            Console.WriteLine("\nParsing Bootstrap.st...");
            var fileNode = ParserService.ParseFileIn(content);
            
            Console.WriteLine($"Successfully parsed {fileNode.Chunks.Count} chunks:");
            
            int classCount = 0;
            int methodCount = 0;
            int commentCount = 0;
            
            foreach (var chunk in fileNode.Chunks)
            {
                switch (chunk)
                {
                    case ClassDefinitionNode classDef:
                        classCount++;
                        Console.WriteLine($"  Class: {classDef.ClassName}");
                        break;
                    case MethodCategoryNode methodCat:
                        methodCount += methodCat.Methods.Count;
                        Console.WriteLine($"  Methods: {methodCat.Header.ClassName}{(methodCat.Header.IsClassSide ? " class" : "")} >> {methodCat.Header.Category} ({methodCat.Methods.Count} methods)");
                        break;
                    case ClassCommentNode comment:
                        commentCount++;
                        break;
                }
            }
            
            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"  Classes: {classCount}");
            Console.WriteLine($"  Methods: {methodCount}");
            Console.WriteLine($"  Comments: {commentCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }
}