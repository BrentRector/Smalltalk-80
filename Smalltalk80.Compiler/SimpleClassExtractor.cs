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
    private readonly string? systemImagePath;
    private readonly Dictionary<string, ClassInfo> classes = new();
    
    public SimpleClassExtractor(string systemImagePath)
    {
        this.systemImagePath = systemImagePath;
    }
    
    public SimpleClassExtractor()
    {
        this.systemImagePath = null;
    }

    /// <summary>
    /// Parse the system image and extract class information.
    /// </summary>
    public void ParseSystemImage()
    {
        if (systemImagePath == null) 
            throw new InvalidOperationException("System image path not provided");
            
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

    /// <summary>
    /// Extracts methods for a specific class from the system image.
    /// </summary>
    public List<MethodInfo> ExtractMethodsForClass(string systemImagePath, string className)
    {
        var methods = new List<MethodInfo>();
        
        Console.WriteLine($"Extracting methods for {className} from {systemImagePath}");
        
        // Parse the system image
        var input = File.ReadAllText(systemImagePath);
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        var tree = parser.fileIn();
        var visitor = new MethodExtractingVisitor(className);
        visitor.Visit(tree);
        
        return visitor.ExtractedMethods;
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
    
    /// <summary>
    /// Visitor that extracts methods for a specific class from the system image.
    /// </summary>
    private class MethodExtractingVisitor : Smalltalk80BaseVisitor<object?>
    {
        private readonly string targetClassName;
        private readonly List<MethodInfo> extractedMethods = new();
        private bool inTargetClass = false;
        
        public List<MethodInfo> ExtractedMethods => extractedMethods;
        
        public MethodExtractingVisitor(string targetClassName)
        {
            this.targetClassName = targetClassName;
        }
        
        public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
        {
            // Check if this is methods for our target class
            var className = context.className()?.GetText();
            
            // Handle both instance and class methods
            if (className == targetClassName || 
                (className + " class" == targetClassName) ||
                (context.IDENTIFIER()?.GetText() == "class" && className == targetClassName.Replace(" class", "")))
            {
                inTargetClass = true;
                Console.WriteLine($"Found methods for {targetClassName}");
                
                // Visit all method definitions in this chunk
                var methods = context.methodDef();
                foreach (var method in methods)
                {
                    Visit(method);
                }
                
                inTargetClass = false;
            }
            
            return null;
        }
        
        public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
        {
            if (!inTargetClass) return null;
            
            // Extract method information
            var header = context.methodHeader();
            var selector = ExtractSelector(header);
            var source = context.GetText(); // Get the full method source
            
            // Check for primitive
            var primitiveDecl = context.primitiveDecl();
            bool isPrimitive = primitiveDecl != null;
            int primitiveNumber = 0;
            
            if (isPrimitive && primitiveDecl != null)
            {
                // Extract number from primitive declaration text like "<primitive: 123>"
                var primitiveText = primitiveDecl.GetText();
                var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"<primitive:\s*(\d+)>");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out primitiveNumber);
                }
            }
            
            var methodInfo = new MethodInfo
            {
                Selector = selector,
                Source = source,
                IsPrimitive = isPrimitive,
                PrimitiveNumber = primitiveNumber
            };
            
            extractedMethods.Add(methodInfo);
            Console.WriteLine($"  Extracted method: {selector}");
            
            return null;
        }
        
        private string ExtractSelector(Smalltalk80Parser.MethodHeaderContext header)
        {
            if (header.unarySelector() != null)
            {
                return header.unarySelector().GetText();
            }
            else if (header.binarySelector() != null)
            {
                return header.binarySelector().GetText();
            }
            else
            {
                // Keyword selector
                var keywords = header.identifierColon();
                return string.Join("", keywords.Select(k => k.GetText()));
            }
        }
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