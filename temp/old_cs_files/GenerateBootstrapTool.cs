using System;
using System.IO;
using System.Reflection;

public class GenerateBootstrapTool
{
    public static void Main(string[] args)
    {
        // Load the Smalltalk80 assembly
        var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Smalltalk80.dll");
        if (!File.Exists(assemblyPath))
        {
            // Try relative path from source
            assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "..", "..", "..", "..", "Smalltalk80", "bin", "Debug", "net9.0", "Smalltalk80.dll");
        }
        
        Console.WriteLine($"Loading assembly from: {assemblyPath}");
        var assembly = Assembly.LoadFrom(assemblyPath);
        
        // Get the BootstrapGenerator type
        var generatorType = assembly.GetType("Smalltalk80.BootstrapGenerator");
        if (generatorType == null)
        {
            Console.WriteLine("Could not find BootstrapGenerator type");
            return;
        }
        
        // Create instance
        var generator = Activator.CreateInstance(generatorType);
        
        // Get the Generate method
        var generateMethod = generatorType.GetMethod("Generate");
        if (generateMethod == null)
        {
            Console.WriteLine("Could not find Generate method");
            return;
        }
        
        // Determine paths
        var inputPath = args.Length > 0 ? args[0] : "Bootstrap.st";
        var outputPath = args.Length > 1 ? args[1] : Path.Combine("Smalltalk80", "GeneratedBootstrap.cs");
        
        Console.WriteLine($"Generating bootstrap from {inputPath} to {outputPath}...");
        
        // Call Generate
        generateMethod.Invoke(generator, new object[] { inputPath, outputPath });
        
        Console.WriteLine("Bootstrap generation complete!");
    }
}