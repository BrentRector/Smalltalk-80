// ==============================================================================
// Test runner for UpdatedBootstrapGenerator
// ==============================================================================

using System;
using System.IO;

namespace Smalltalk80;

/// <summary>
/// Simple test runner to verify the bootstrap generator works.
/// </summary>
public class RunBootstrapGenerator
{
    public static void Main(string[] args)
    {
        try
        {
            string bootstrapStPath = args.Length > 0 ? args[0] : "Bootstrap.st";
            string outputPath = args.Length > 1 ? args[1] : "GeneratedBootstrap.cs";
            
            if (!File.Exists(bootstrapStPath))
            {
                Console.WriteLine($"Bootstrap file not found: {bootstrapStPath}");
                Console.WriteLine("Please provide the path to Bootstrap.st as the first argument.");
                return;
            }
            
            Console.WriteLine($"Generating bootstrap from: {bootstrapStPath}");
            Console.WriteLine($"Output will be written to: {outputPath}");
            
            UpdatedBootstrapGenerator.Generate(bootstrapStPath, outputPath);
            
            Console.WriteLine("\nBootstrap generation complete!");
            Console.WriteLine($"Generated file: {outputPath}");
            
            // Verify the generated file exists
            if (File.Exists(outputPath))
            {
                var lines = File.ReadAllLines(outputPath);
                Console.WriteLine($"Generated {lines.Length} lines of code.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during bootstrap generation: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }
}