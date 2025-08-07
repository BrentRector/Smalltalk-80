using System;
using System.IO;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Test program for the PrimitiveBootstrapGenerator
/// </summary>
public class TestBootstrapGenerator_Disabled
{
    // Disabled to avoid duplicate entry point
    /*
    public static void Main(string[] args)
    {
        var bootstrapPath = args.Length > 0 ? args[0] : "CorrectBootstrap.st";
        var outputPath = args.Length > 1 ? args[1] : "GeneratedPrimitiveBootstrap.cs";
        
        if (!File.Exists(bootstrapPath))
        {
            Console.WriteLine($"Bootstrap file not found: {bootstrapPath}");
            Console.WriteLine("Usage: TestBootstrapGenerator [bootstrap.st] [output.cs]");
            return;
        }
        
        try
        {
            Console.WriteLine($"Reading Bootstrap.st from: {bootstrapPath}");
            
            var generator = new PrimitiveBootstrapGenerator(bootstrapPath);
            var generatedCode = generator.Generate();
            
            Console.WriteLine($"Writing generated code to: {outputPath}");
            File.WriteAllText(outputPath, generatedCode);
            
            Console.WriteLine("Bootstrap generation complete!");
            Console.WriteLine($"Generated {generatedCode.Length} characters of C# code");
            
            // Show summary
            var lines = generatedCode.Split('\n');
            var classCount = 0;
            var methodCount = 0;
            
            foreach (var line in lines)
            {
                if (line.Contains("CreateClass("))
                    classCount++;
                if (line.Contains("InstallMethod(") || line.Contains("InstallClassMethod("))
                    methodCount++;
            }
            
            Console.WriteLine($"Summary: {classCount} classes, {methodCount} methods");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating bootstrap: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    */
}