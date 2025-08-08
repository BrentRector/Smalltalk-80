using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

namespace Smalltalk80.Compiler;

/// <summary>
/// Extracts class definitions from the Smalltalk-80 system image and generates Phase 2 bootstrap JSON.
/// This simpler version focuses on class structure without bytecode compilation.
/// </summary>
public class SimpleClassExtractor
{
    private readonly string systemImagePath;
    private readonly Dictionary<string, ClassInfo> classes = new();
    
    public SimpleClassExtractor(string systemImagePath)
    {
        this.systemImagePath = systemImagePath;
    }

    /// <summary>
    /// Parse the system image and extract class information.
    /// </summary>
    public void ParseSystemImage()
    {
        Console.WriteLine($"Parsing system image: {systemImagePath}");
        
        string input = File.ReadAllText(systemImagePath);
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        // Parse the file
        var tree = parser.fileIn();
        
        // Visit the tree to extract classes
        var visitor = new ClassDefBuildingVisitor();
        var fileNode = visitor.Visit(tree) as FileNode;
        
        if (fileNode == null)
        {
            throw new InvalidOperationException("Failed to parse system image");
        }

        ProcessFileNode(fileNode);
        
        Console.WriteLine($"Extracted {classes.Count} classes");
    }

    private void ProcessFileNode(FileNode fileNode)
    {
        foreach (var chunk in fileNode.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef)
            {
                classes[classDef.ClassName] = new ClassInfo
                {
                    Name = classDef.ClassName,
                    Superclass = classDef.Superclass,
                    InstanceVariables = classDef.InstanceVariables.ToList(),
                    ClassVariables = classDef.ClassVariables.ToList(),
                    PoolDictionaries = classDef.PoolDictionaries.ToList(),
                    Category = classDef.Category
                };
            }
        }
    }

    /// <summary>
    /// Generate Phase 2 bootstrap JSON for specified classes.
    /// </summary>
    public string GeneratePhase2Json(params string[] classNames)
    {
        var phase2 = new
        {
            phase = "2-extracted",
            description = $"Classes extracted from system image: {string.Join(", ", classNames)}",
            comments = new[]
            {
                "These classes were extracted from the Smalltalk-80 system image.",
                "This Phase 2 creates the class structure without methods.",
                "Methods should be added in Phase 3."
            },
            classes = new List<object>()
        };

        foreach (var className in classNames)
        {
            if (!classes.ContainsKey(className))
            {
                Console.WriteLine($"Warning: Class {className} not found in system image");
                continue;
            }

            var classInfo = classes[className];
            
            // Check if this class has variable-sized instances
            bool variableSize = IsVariableSizeClass(className, classInfo);
            
            var classEntry = new Dictionary<string, object>
            {
                ["name"] = classInfo.Name,
                ["superclass"] = classInfo.Superclass,
                ["instanceVariables"] = classInfo.InstanceVariables.ToArray()
            };

            // Only add non-empty arrays
            if (classInfo.ClassVariables.Count > 0)
            {
                classEntry["classVariables"] = classInfo.ClassVariables.ToArray();
            }
            
            if (classInfo.PoolDictionaries.Count > 0)
            {
                classEntry["poolDictionaries"] = classInfo.PoolDictionaries.ToArray();
            }
            
            if (variableSize)
            {
                classEntry["variableSize"] = true;
            }
            
            phase2.classes.Add(classEntry);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(phase2, options);
    }

    private bool IsVariableSizeClass(string className, ClassInfo classInfo)
    {
        // Classes that typically have variable-sized instances
        var variableSizeClasses = new HashSet<string>
        {
            "Array", "ByteArray", "String", "Symbol",
            "OrderedCollection", "Set", "Dictionary",
            "MethodDictionary", "IdentityDictionary",
            "ArrayedCollection", "SequenceableCollection",
            "CompiledMethod", "BlockContext", "MethodContext"
        };
        
        // Check if this class or its superclass is known to be variable-sized
        if (variableSizeClasses.Contains(className))
            return true;
            
        // Check superclass chain (simplified - just immediate superclass)
        if (variableSizeClasses.Contains(classInfo.Superclass))
            return true;
            
        return false;
    }

    private class ClassInfo
    {
        public string Name { get; set; } = "";
        public string Superclass { get; set; } = "";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public List<string> PoolDictionaries { get; set; } = new();
        public string? Category { get; set; }
    }
}

/// <summary>
/// Command-line interface for the class extractor.
/// </summary>
public static class ClassExtractorCLI
{
    public static int ExtractClasses(string systemImagePath, string outputPath, params string[] classNames)
    {
        try
        {
            var extractor = new SimpleClassExtractor(systemImagePath);
            extractor.ParseSystemImage();
            
            var json = extractor.GeneratePhase2Json(classNames);
            
            File.WriteAllText(outputPath, json);
            Console.WriteLine($"Successfully extracted classes to: {outputPath}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Extraction failed: {ex.Message}");
            return 1;
        }
    }
}