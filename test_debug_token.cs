using System;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

class Program
{
    static void Main()
    {
        // Test with hash symbol that we know is problematic
        var input = "#hello";
        Console.WriteLine($"Testing input: {input}");
        
        // Create lexer and get tokens
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        
        // Print all tokens
        Console.WriteLine("Tokens:");
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();
        
        for (int i = 0; i < tokens.Size; i++)
        {
            var token = tokens.Get(i);
            var tokenName = lexer.Vocabulary.GetDisplayName(token.Type);
            Console.WriteLine($"  Token[{i}]: {tokenName} = '{token.Text}' (type={token.Type}, start={token.StartIndex}, stop={token.StopIndex})");
        }
        
        // Reset for parsing
        tokens.Seek(0);
        var parser = new Smalltalk80Parser(tokens);
        
        // Enable detailed parser debugging
        parser.Trace = true;
        parser.ErrorHandler = new DefaultErrorStrategy();
        
        try
        {
            // Parse as literal directly
            Console.WriteLine("\nParsing as literal:");
            var literalContext = parser.literal();
            Console.WriteLine($"Literal context: {literalContext?.GetType().Name}");
            Console.WriteLine($"Literal children count: {literalContext?.ChildCount}");
            
            if (literalContext != null)
            {
                for (int i = 0; i < literalContext.ChildCount; i++)
                {
                    var child = literalContext.GetChild(i);
                    Console.WriteLine($"  Child[{i}]: {child.GetType().Name} = '{child.GetText()}'");
                }
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing as literal: {ex.Message}");
        }
        
        // Reset and try parsing as messageBase
        tokens.Seek(0);
        parser = new Smalltalk80Parser(tokens);
        parser.Trace = true;
        
        try
        {
            Console.WriteLine("\nParsing as messageBase:");
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
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing as messageBase: {ex.Message}");
        }
    }
}