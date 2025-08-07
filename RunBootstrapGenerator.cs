using System;
using Smalltalk80;

/// <summary>
/// Simple runner for the BootstrapGenerator tool.
/// </summary>
public class RunBootstrapGenerator
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: RunBootstrapGenerator <input.st> <output.cs>");
            Environment.Exit(1);
        }
        
        var generator = new BootstrapGenerator();
        generator.Generate(args[0], args[1]);
    }
}