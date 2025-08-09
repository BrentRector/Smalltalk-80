using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler;

/// <summary>
/// Extract Phase 3 classes from system image using the Phase3ClassExtractor
/// </summary>
class ExtractPhase3
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet script ExtractPhase3.cs <ClassName> <OutputFile> [SystemImageFile]");
            Environment.Exit(1);
        }
        
        string className = args[0];
        string outputFile = args[1]; 
        string systemImageFile = args.Length > 2 ? args[2] : @"Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
        
        try
        {
            Console.WriteLine($"Extracting class {className} from {systemImageFile}");
            
            // Read and parse the system image
            var systemImageContent = File.ReadAllText(systemImageFile);
            var inputStream = new AntlrInputStream(systemImageContent);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            var tree = parser.chunk();
            
            // Create extractor and extract the class
            var extractor = new Phase3ClassExtractor();
            var phase3Class = extractor.ExtractClass(tree, className);
            
            if (phase3Class == null)
            {
                Console.WriteLine($"Class {className} not found in system image");
                Environment.Exit(1);
            }
            
            // Serialize to JSON
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonString = JsonSerializer.Serialize(phase3Class, options);
            
            // Write to output file
            File.WriteAllText(outputFile, jsonString);
            Console.WriteLine($"Successfully extracted {className} to {outputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}