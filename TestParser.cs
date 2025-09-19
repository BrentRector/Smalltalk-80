using System;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using StandaloneCompiler.Generated;

class TestParser
{
    static void Main(string[] args)
    {
        string input = @"!TestClass class methodsFor: 'test'!
with: firstObject with: secondObject
    ^self new: 2! !";

        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);
        
        var tree = parser.methodCategoryChunk();
        
        // Print the tree
        PrintTree(tree, parser, 0);
    }
    
    static void PrintTree(IParseTree node, Smalltalk80Parser parser, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        
        if (node is ITerminalNode term)
        {
            Console.WriteLine($"{indentStr}Terminal: {term.GetText()}");
        }
        else if (node is ParserRuleContext ctx)
        {
            string ruleName = parser.RuleNames[ctx.RuleIndex];
            Console.WriteLine($"{indentStr}{ruleName}");
            
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                PrintTree(ctx.GetChild(i), parser, indent + 1);
            }
        }
    }
}