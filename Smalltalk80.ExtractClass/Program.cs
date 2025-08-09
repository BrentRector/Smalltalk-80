using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smalltalk80.ExtractClass;

/// <summary>
/// Smalltalk-80 Class Extractor - Extracts class definitions with compiled bytecode.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Class Extractor");
        Console.WriteLine("=============================");
        Console.WriteLine();

        if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
        {
            ShowUsage();
            return args.Length == 0 ? 1 : 0;
        }

        // Parse arguments
        string? systemImagePath = null;
        string? outputDir = null;
        var classNames = new List<string>();
        bool singleFile = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s":
                case "--source":
                    if (i + 1 < args.Length)
                    {
                        systemImagePath = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --source requires a file path");
                        return 1;
                    }
                    break;
                    
                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        outputDir = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --output requires a directory path");
                        return 1;
                    }
                    break;
                    
                case "--single-file":
                    singleFile = true;
                    break;
                    
                default:
                    if (!args[i].StartsWith("-"))
                    {
                        classNames.Add(args[i]);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown option: {args[i]}");
                        return 1;
                    }
                    break;
            }
        }

        // Validate arguments
        if (classNames.Count == 0)
        {
            Console.Error.WriteLine("Error: No class names specified");
            ShowUsage();
            return 1;
        }

        // Default paths
        systemImagePath ??= @"Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
        outputDir ??= "extracted";

        // Check if system image exists
        if (!File.Exists(systemImagePath))
        {
            Console.Error.WriteLine($"Error: System image not found: {systemImagePath}");
            return 1;
        }

        // Create output directory if needed
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Extract classes
        var extractor = new ClassExtractor();
        var extractedClasses = new List<ExtractedClass>();
        var failedClasses = new List<string>();

        foreach (var className in classNames)
        {
            try
            {
                var extracted = extractor.ExtractClass(systemImagePath, className);
                if (extracted != null)
                {
                    extractedClasses.Add(extracted);
                    
                    if (!singleFile)
                    {
                        // Save each class to its own file
                        var outputPath = Path.Combine(outputDir, $"{className}.json");
                        extractor.SaveToJson(extracted, outputPath);
                    }
                }
                else
                {
                    failedClasses.Add(className);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error extracting {className}: {ex.Message}");
                failedClasses.Add(className);
            }
        }

        // If single file mode, save all classes together
        if (singleFile && extractedClasses.Count > 0)
        {
            var outputPath = Path.Combine(outputDir, "extracted_classes.json");
            SaveMultipleClasses(extractedClasses, outputPath);
        }

        // Report results
        Console.WriteLine();
        Console.WriteLine("Extraction Summary:");
        Console.WriteLine($"  Successfully extracted: {extractedClasses.Count} class(es)");
        
        if (failedClasses.Count > 0)
        {
            Console.WriteLine($"  Failed to extract: {string.Join(", ", failedClasses)}");
            return 1;
        }

        return 0;
    }

    private static void SaveMultipleClasses(List<ExtractedClass> classes, string outputPath)
    {
        var wrapper = new
        {
            ExtractedClasses = classes,
            Count = classes.Count,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
        };

        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        var json = System.Text.Json.JsonSerializer.Serialize(wrapper, options);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Saved {classes.Count} classes to {outputPath}");
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: Smalltalk80.ExtractClass [options] <class-names...>");
        Console.WriteLine();
        Console.WriteLine("Extracts Smalltalk-80 classes from the system image with compiled bytecode.");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  class-names           One or more class names to extract");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -s, --source <file>   Path to system image (default: Smalltalk-80.st)");
        Console.WriteLine("  -o, --output <dir>    Output directory (default: extracted)");
        Console.WriteLine("  --single-file         Save all classes to a single JSON file");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Extract Object class");
        Console.WriteLine("  Smalltalk80.ExtractClass Object");
        Console.WriteLine();
        Console.WriteLine("  # Extract multiple classes");
        Console.WriteLine("  Smalltalk80.ExtractClass Object Boolean True False");
        Console.WriteLine();
        Console.WriteLine("  # Extract with custom paths");
        Console.WriteLine("  Smalltalk80.ExtractClass -s system.st -o output Object");
        Console.WriteLine();
        Console.WriteLine("  # Extract multiple classes to single file");
        Console.WriteLine("  Smalltalk80.ExtractClass --single-file Object Boolean");
        Console.WriteLine();
        Console.WriteLine("Output Format:");
        Console.WriteLine("  Each extracted class contains:");
        Console.WriteLine("  - Class metadata (name, superclass, variables, category)");
        Console.WriteLine("  - Instance and class method counts");
        Console.WriteLine("  - Full method definitions with compiled bytecode");
        Console.WriteLine("  - All information needed for runtime class installation");
    }
}