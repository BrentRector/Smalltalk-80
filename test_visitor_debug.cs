using System;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

class Program
{
    static void Main()
    {
        // Test visitor with hash symbol
        var input = "#hello";
        Console.WriteLine($"Testing visitor with: {input}");
        
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        try
        {
            // Parse as messageBase
            var messageBaseContext = parser.messageBase();
            Console.WriteLine($"MessageBase context: {messageBaseContext?.GetType().Name}");
            Console.WriteLine($"MessageBase children count: {messageBaseContext?.ChildCount}");
            
            if (messageBaseContext != null)
            {
                for (int i = 0; i < messageBaseContext.ChildCount; i++)
                {
                    var child = messageBaseContext.GetChild(i);
                    Console.WriteLine($"  Child[{i}]: {child.GetType().Name} = '{child.GetText()}'");
                }
                
                // Now test the visitor
                Console.WriteLine("\nTesting visitor:");
                var visitor = new SimpleAstBuildingVisitor();
                
                try
                {
                    var result = visitor.Visit(messageBaseContext);
                    Console.WriteLine($"Visitor result: {result?.GetType().Name}");
                    if (result is LiteralExpression lit)
                    {
                        Console.WriteLine($"Literal value: {lit.Value} (type: {lit.Value?.GetType().Name})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Visitor error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing: {ex.Message}");
        }
    }
}