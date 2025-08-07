using System;
using System.IO;
using Smalltalk80.Bootstrap;

class TestGenerator
{
    static void Main()
    {
        try
        {
            var generator = new PrimitiveBootstrapGenerator("CorrectBootstrap.st");
            var code = generator.Generate();
            File.WriteAllText("GeneratedBootstrap.cs", code);
            Console.WriteLine($"Generated {code.Length} characters of bootstrap code");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}