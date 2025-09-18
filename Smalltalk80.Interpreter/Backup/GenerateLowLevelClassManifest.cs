using System;
using System.IO;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Console application to generate Phase 3 manifest from Smalltalk source.
/// </summary>
public class GeneratePhase3Manifest
{
    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: GeneratePhase3Manifest <input.st> [output.json]");
            Console.WriteLine("  input.st: Smalltalk source file to parse");
            Console.WriteLine("  output.json: Output manifest file (default: bootstrap_phase3_generated.json)");
            return;
        }
        
        var inputPath = args[0];
        var outputPath = args.Length > 1 ? args[1] : "bootstrap_phase3_generated.json";
        
        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"Error: Input file '{inputPath}' not found");
            return;
        }
        
        try
        {
            var generator = new LowLevelClassManifestGenerator();
            generator.GenerateFromFile(inputPath, outputPath);
            
            Console.WriteLine("Low-level class manifest generation complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating manifest: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}