using System;
using System.IO;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Smalltalk80.Compiler <input.st> <output.json>");
                Console.WriteLine();
                Console.WriteLine("Compiles a Smalltalk-80 file-out format source file to JSON.");
                return 1;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            if (!File.Exists(inputFile))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
                return 1;
            }

            try
            {
                Console.WriteLine($"Compiling {inputFile}...");
                
                // Read the input file
                string input = File.ReadAllText(inputFile);
                
                // Create ANTLR input stream
                var inputStream = new AntlrInputStream(input);
                
                // Create lexer
                var lexer = new Smalltalk80Lexer(inputStream);
                
                // Create token stream
                var tokenStream = new CommonTokenStream(lexer);
                
                // Create parser
                var parser = new Smalltalk80Parser(tokenStream);
                
                // Add error listener
                var errorListener = new ConsoleErrorListener();
                parser.RemoveErrorListeners();
                parser.AddErrorListener(errorListener);
                
                // Parse the file
                var tree = parser.fileIn();
                
                // Check for syntax errors
                if (parser.NumberOfSyntaxErrors > 0)
                {
                    Console.Error.WriteLine($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors.");
                    return 1;
                }
                
                // Build the AST
                var astBuilder = new FileInFormatAstBuilder(tokenStream);
                astBuilder.Visit(tree);
                
                // Get the compilation result
                var result = astBuilder.GetResult();
                
                // Serialize to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                string json = JsonSerializer.Serialize(result, jsonOptions);
                
                // Write output file
                File.WriteAllText(outputFile, json);
                
                Console.WriteLine($"Successfully compiled to {outputFile}");
                Console.WriteLine($"  Classes: {result.Classes.Count}");
                
                int totalMethods = 0;
                int totalClassMethods = 0;
                foreach (var cls in result.Classes)
                {
                    totalMethods += cls.Methods.Count;
                    totalClassMethods += cls.ClassMethods.Count;
                }
                
                Console.WriteLine($"  Instance Methods: {totalMethods}");
                Console.WriteLine($"  Class Methods: {totalClassMethods}");
                Console.WriteLine($"  Initialization Expressions: {result.InitializationExpressions.Count}");
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (args.Length > 2 && args[2] == "--verbose")
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }
    }
    
    class ConsoleErrorListener : BaseErrorListener
    {
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Console.Error.WriteLine($"Line {line}:{charPositionInLine} - {msg}");
        }
    }
}