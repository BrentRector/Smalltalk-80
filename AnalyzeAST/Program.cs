using System;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using StandaloneCompiler;

class ASTAnalyzer
{
    static void Main()
    {
        string methodSource = @"comment: aString 
	""Set the receiver's comment to be the argument, aString.""

	| aStream |
	aString size = 0
		ifTrue: 
			[self organization classComment: aString]
		ifFalse: 
			[""double internal quotes of the comment string""
			aStream _ WriteStream on: (String new: aString size).
			aStream nextPutAll: self name , ' comment:'; cr.
			aString storeOn: aStream.
			self organization classComment: aStream contents.
	Smalltalk changes commentClass: self]!";

        Console.WriteLine("=== Parsing Smalltalk Method ===\n");
        Console.WriteLine("Source:");
        Console.WriteLine(methodSource);
        Console.WriteLine("\n=== AST Structure ===\n");

        var inputStream = new AntlrInputStream(methodSource);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);

        // Parse as a method definition
        var tree = parser.methodDef();
        
        // Print the AST structure
        PrintTree(tree, parser, "", true);

        // Analyze the specific path to blocks
        Console.WriteLine("\n=== Path Analysis to Blocks ===\n");
        AnalyzeBlockPath(tree, parser);
    }

    static void PrintTree(IParseTree node, Smalltalk80Parser parser, string indent, bool last)
    {
        string marker = last ? "└── " : "├── ";
        
        if (node is ParserRuleContext ctx)
        {
            string ruleName = parser.RuleNames[ctx.RuleIndex];
            Console.WriteLine($"{indent}{marker}{ruleName}");
            
            string newIndent = indent + (last ? "    " : "│   ");
            
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                PrintTree(ctx.GetChild(i), parser, newIndent, i == ctx.ChildCount - 1);
            }
        }
        else if (node is ITerminalNode terminal)
        {
            string text = terminal.GetText();
            if (text.Length > 50)
                text = text.Substring(0, 47) + "...";
            text = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            Console.WriteLine($"{indent}{marker}'{text}'");
        }
    }

    static void AnalyzeBlockPath(Smalltalk80Parser.MethodDefContext method, Smalltalk80Parser parser)
    {
        Console.WriteLine("Finding path to blocks containing global variables...\n");
        
        // Navigate through the AST to find blocks
        FindBlocksWithGlobals(method, parser, "methodDef");
    }

    static void FindBlocksWithGlobals(IParseTree node, Smalltalk80Parser parser, string path)
    {
        if (node is ParserRuleContext ctx)
        {
            string ruleName = parser.RuleNames[ctx.RuleIndex];
            string currentPath = path + " -> " + ruleName;
            
            // Check if this is a block
            if (ctx is Smalltalk80Parser.BlockContext blockCtx)
            {
                Console.WriteLine($"Found block at: {currentPath}");
                
                // Look for variables in this block
                FindVariables(blockCtx, parser, currentPath);
            }
            
            // Recurse through children
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                FindBlocksWithGlobals(ctx.GetChild(i), parser, currentPath);
            }
        }
    }

    static void FindVariables(IParseTree node, Smalltalk80Parser parser, string path)
    {
        if (node is ParserRuleContext ctx)
        {
            string ruleName = parser.RuleNames[ctx.RuleIndex];
            
            // Check if this is a variable
            if (ctx is Smalltalk80Parser.VariableContext varCtx)
            {
                var identifier = varCtx.GetChild(0);
                if (identifier != null)
                {
                    string varName = identifier.GetText();
                    // Check if it's a global (starts with uppercase)
                    if (char.IsUpper(varName[0]))
                    {
                        Console.WriteLine($"  Global variable '{varName}' at: {path} -> variable");
                    }
                }
            }
            
            // Recurse through children
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                FindVariables(ctx.GetChild(i), parser, path);
            }
        }
    }
}