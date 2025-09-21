using System;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.Generated;

public class TestExprParser
{
    public static void Main()
    {
        var testExpr = "Character initialize";
        Console.WriteLine($"Parsing expression: {testExpr}");
        
        var inputStream = new AntlrInputStream(testExpr);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);
        
        // Try parsing as expression
        var expr = parser.expression();
        
        if (parser.NumberOfSyntaxErrors > 0)
        {
            Console.WriteLine($"Parse errors: {parser.NumberOfSyntaxErrors}");
        }
        else
        {
            Console.WriteLine("Parsed successfully!");
            Console.WriteLine($"Expression tree: {expr.ToStringTree(parser)}");
            
            // Try building AST
            var astBuilder = new AstBuilder(testExpr);
            var ast = astBuilder.Visit(expr);
            
            if (ast == null)
            {
                Console.WriteLine("AST building failed - returned null");
            }
            else
            {
                Console.WriteLine($"AST type: {ast.GetType().Name}");
            }
        }
    }
}