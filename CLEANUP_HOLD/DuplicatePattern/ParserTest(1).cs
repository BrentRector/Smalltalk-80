using System;
using System.IO;

class Program
{
    static void Main()
    {
        // Test the parser directly with the problematic input
        var parser = new SmalltalkParser("^42!");
        try
        {
            var result = parser.ParseMethod(new string[0], new string[0], "testMethod");
            Console.WriteLine("SUCCESS: Parser worked!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}
