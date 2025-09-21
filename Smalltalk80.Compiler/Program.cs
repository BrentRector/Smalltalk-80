using System;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.AST.Processors;
using Smalltalk80.Compiler.Compilation;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler;

/// <summary>
/// Command-line interface for the Smalltalk-80 compiler.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point for the compiler.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return 1;
        }

        string inputFile = args[0];
        string outputFile = args.Length > 1 ? args[1] : Path.ChangeExtension(inputFile, ".json");
        
        bool verbose = Array.IndexOf(args, "--verbose") >= 0 || Array.IndexOf(args, "-v") >= 0;
        bool astDump = Array.IndexOf(args, "--ast") >= 0;

        try
        {
            // Read source file
            if (!File.Exists(inputFile))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
                return 1;
            }

            string sourceCode = File.ReadAllText(inputFile, Encoding.UTF8);
            
            if (verbose)
            {
                Console.WriteLine($"Compiling: {inputFile}");
                Console.WriteLine($"Output:    {outputFile}");
                Console.WriteLine();
            }

            // Parse the source code
            var parseTree = ParseSource(sourceCode, inputFile);
            if (parseTree == null)
            {
                Console.Error.WriteLine("Error: Failed to parse source file.");
                return 1;
            }

            // Build AST from parse tree
            var astBuilder = new AstBuilder(sourceCode);
            var ast = astBuilder.Build(parseTree);
            if (ast == null)
            {
                Console.Error.WriteLine("Error: Failed to build AST.");
                return 1;
            }

            // Optionally dump AST
            if (astDump)
            {
                DumpAst(ast, 0);
                Console.WriteLine();
            }

            // Choose compiler based on content type
            IAstProcessor compiler;
            if (sourceCode.Contains('!'))
            {
                // Use SystemCompiler for file-in format
                compiler = new SystemCompiler();
                if (verbose)
                {
                    Console.WriteLine("Using SystemCompiler for file-in format");
                }
            }
            else
            {
                // Use BytecodeCompiler for individual methods/expressions
                compiler = new BytecodeCompiler();
                if (verbose)
                {
                    Console.WriteLine("Using BytecodeCompiler for single method/expression");
                }
            }
            
            var context = new ProcessorContext(inputFile, sourceCode);
            var result = compiler.Process(ast, context);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Compilation failed: {result.ErrorMessage}");
                return 1;
            }

            if (result is BytecodeResult bytecodeResult && bytecodeResult.JsonOutput != null)
            {
                // Write output file
                File.WriteAllText(outputFile, bytecodeResult.JsonOutput, Encoding.UTF8);
                
                if (verbose)
                {
                    Console.WriteLine($"Successfully compiled to: {outputFile}");
                    
                    // Parse and display summary for SystemCompiler output
                    if (compiler is SystemCompiler)
                    {
                        try
                        {
                            var json = System.Text.Json.JsonDocument.Parse(bytecodeResult.JsonOutput);
                            var root = json.RootElement;
                            
                            int classCount = 0;
                            int methodCount = 0;
                            int initCount = 0;
                            
                            if (root.TryGetProperty("classes", out var classes))
                                classCount = classes.GetArrayLength();
                            if (root.TryGetProperty("methods", out var methods))
                                methodCount = methods.GetArrayLength();
                            if (root.TryGetProperty("initializationExpressions", out var inits))
                                initCount = inits.GetArrayLength();
                            
                            Console.WriteLine();
                            Console.WriteLine($"Compilation Summary:");
                            Console.WriteLine($"  Classes: {classCount}");
                            Console.WriteLine($"  Methods: {methodCount}");
                            Console.WriteLine($"  Initialization expressions: {initCount}");
                        }
                        catch
                        {
                            // Ignore JSON parsing errors
                        }
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("Error: No bytecode generated.");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static IParseTree? ParseSource(string sourceCode, string fileName)
    {
        try
        {
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(sourceCode);
            
            // Create lexer
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            
            // Create parser
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Add error listener
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ConsoleErrorListener(fileName));
            
            // Determine entry point based on content
            if (sourceCode.Contains('!'))
            {
                // File-in format
                return parser.fileIn();
            }
            else if (sourceCode.TrimStart().StartsWith('^'))
            {
                // Expression with return
                return parser.statementsEntry();
            }
            else if (LooksLikeMethod(sourceCode))
            {
                // Method definition
                return parser.methodEntry();
            }
            else
            {
                // Default to expression
                return parser.expressionEntry();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            return null;
        }
    }

    private static bool LooksLikeMethod(string source)
    {
        var trimmed = source.TrimStart();
        
        // Check for unary method
        if (char.IsLetter(trimmed[0]))
        {
            int i = 0;
            while (i < trimmed.Length && char.IsLetterOrDigit(trimmed[i]))
                i++;
            
            if (i < trimmed.Length && char.IsWhiteSpace(trimmed[i]))
                return true;
        }
        
        // Check for binary method
        if (!char.IsLetterOrDigit(trimmed[0]) && trimmed[0] != '"' && trimmed[0] != '\'' && trimmed[0] != '[')
        {
            return true;
        }
        
        // Check for keyword method
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0 && colonIndex < trimmed.Length - 1)
        {
            return char.IsLetter(trimmed[0]);
        }
        
        return false;
    }

    private static void DumpAst(AstNode node, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        
        Console.Write(indentStr);
        Console.WriteLine($"{node.GetType().Name.Replace("Node", "")}");
        
        // Dump children based on node type
        switch (node)
        {
            case ProgramNode program:
                foreach (var element in program.Elements)
                    DumpAst(element, indent + 1);
                break;
                
            case MethodDefinitionNode method:
                Console.WriteLine($"{indentStr}  Selector: {method.Selector}");
                foreach (var arg in method.Arguments)
                    Console.WriteLine($"{indentStr}    Arg: {arg.Name}");
                foreach (var temp in method.Temporaries)
                    Console.WriteLine($"{indentStr}    Temp: {temp.Name}");
                DumpAst(method.Body, indent + 1);
                break;
                
            case StatementListNode stmts:
                foreach (var stmt in stmts.Statements)
                    DumpAst(stmt, indent + 1);
                break;
                
            case ReturnStatementNode ret:
                DumpAst(ret.Expression, indent + 1);
                break;
                
            case ExpressionStatementNode expr:
                DumpAst(expr.Expression, indent + 1);
                break;
                
            case AssignmentNode assign:
                Console.WriteLine($"{indentStr}  Variable: {assign.Variable}");
                DumpAst(assign.Value, indent + 1);
                break;
                
            case MessageSendNode send:
                DumpAst(send.Receiver, indent + 1);
                Console.WriteLine($"{indentStr}  Message: {send.Message.GetSelector()}");
                break;
                
            case VariableNode var:
                Console.WriteLine($"{indentStr}  Name: {var.Name}");
                break;
                
            case NumberLiteralNode num:
                Console.WriteLine($"{indentStr}  Value: {num.Value}");
                break;
                
            case StringLiteralNode str:
                Console.WriteLine($"{indentStr}  Value: \"{str.Value}\"");
                break;
                
            case SymbolLiteralNode sym:
                Console.WriteLine($"{indentStr}  Value: #{sym.Value}");
                break;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Smalltalk-80 Compiler");
        Console.WriteLine();
        Console.WriteLine("Usage: Smalltalk80.Compiler <input.st> [output.json] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --verbose, -v    Show verbose output");
        Console.WriteLine("  --ast            Dump the AST structure");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Smalltalk80.Compiler method.st");
        Console.WriteLine("  Smalltalk80.Compiler method.st output.json --verbose");
        Console.WriteLine("  Smalltalk80.Compiler program.st --ast");
    }

    private sealed class ConsoleErrorListener : BaseErrorListener
    {
        private readonly string _fileName;

        public ConsoleErrorListener(string fileName)
        {
            _fileName = fileName;
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Console.Error.WriteLine($"{_fileName}:{line}:{charPositionInLine}: error: {msg}");
        }
    }
}