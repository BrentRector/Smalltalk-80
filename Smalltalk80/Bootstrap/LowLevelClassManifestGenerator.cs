using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Generates a low-level class JSON manifest from parsed Smalltalk source.
/// The manifest contains message sends to create classes and install methods.
/// </summary>
public class LowLevelClassManifestGenerator
{
    /// <summary>
    /// Represents a class definition in the manifest.
    /// </summary>
    public class ClassDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "Object";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unclassified";
    }
    
    /// <summary>
    /// Represents a method definition in the manifest.
    /// </summary>
    public class MethodDefinition
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        
        [JsonPropertyName("bytecodes")]
        public List<byte> Bytecodes { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "accessing";
    }
    
    /// <summary>
    /// The complete Phase 3 manifest.
    /// </summary>
    public class Phase3Manifest
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "./bootstrap-schema.json";
        
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "3";
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = "Generated Phase 3 manifest from parsed Smalltalk source";
        
        [JsonPropertyName("classes")]
        public List<ClassDefinition> Classes { get; set; } = new();
        
        [JsonPropertyName("methods")]
        public List<MethodDefinition> Methods { get; set; } = new();
    }
    
    private readonly Phase3Manifest manifest = new();
    private string? currentClassName;
    private bool isClassSide;
    private string currentCategory = "accessing";
    
    /// <summary>
    /// Generate a Phase 3 manifest from a Smalltalk source file.
    /// </summary>
    public void GenerateFromFile(string inputPath, string outputPath)
    {
        Console.WriteLine($"Generating Phase 3 manifest from {inputPath}...");
        
        var source = File.ReadAllText(inputPath);
        ParseSource(source);
        
        // Order classes by dependency (superclasses first)
        OrderClassesByDependency();
        
        // Generate simple bytecodes for methods
        GenerateBytecodes();
        
        // Write the manifest
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var json = JsonSerializer.Serialize(manifest, options);
        File.WriteAllText(outputPath, json);
        
        Console.WriteLine($"Generated manifest with {manifest.Classes.Count} classes and {manifest.Methods.Count} methods");
        Console.WriteLine($"Saved to {outputPath}");
    }
    
    /// <summary>
    /// Parse Smalltalk source and extract class and method definitions.
    /// </summary>
    private void ParseSource(string source)
    {
        var lines = source.Split('\n');
        var inMethod = false;
        var methodLines = new List<string>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Skip empty lines and separators
            if (string.IsNullOrWhiteSpace(line) || line.Trim() == "------------------------------------------------------------")
                continue;
            
            // Check for class declaration
            if (line.Contains("subclass: #") && !inMethod)
            {
                ParseClassDeclaration(lines, ref i);
            }
            // Check for method category
            else if (line.StartsWith("!") && line.Contains("methodsFor:"))
            {
                ParseMethodCategory(line);
                inMethod = false;
            }
            // Check for method start/end
            else if (line == "!" && !inMethod)
            {
                // End of method or section
                if (methodLines.Count > 0)
                {
                    ParseMethod(string.Join("\n", methodLines));
                    methodLines.Clear();
                }
                inMethod = false;
            }
            else if (currentClassName != null && !line.StartsWith("!"))
            {
                // Accumulate method lines
                if (!inMethod && !string.IsNullOrWhiteSpace(line))
                {
                    inMethod = true;
                }
                
                if (inMethod)
                {
                    methodLines.Add(line);
                }
            }
            
            // Handle method terminator
            if (line.Trim() == "! !")
            {
                if (methodLines.Count > 0)
                {
                    ParseMethod(string.Join("\n", methodLines));
                    methodLines.Clear();
                }
                inMethod = false;
            }
        }
    }
    
    /// <summary>
    /// Parse a class declaration.
    /// </summary>
    private void ParseClassDeclaration(string[] lines, ref int index)
    {
        var declaration = new List<string>();
        
        // Collect all lines of the class declaration
        while (index < lines.Length && !lines[index].EndsWith("!"))
        {
            declaration.Add(lines[index]);
            index++;
        }
        
        if (index < lines.Length && lines[index].EndsWith("!"))
        {
            declaration.Add(lines[index]);
        }
        
        var fullDeclaration = string.Join("\n", declaration);
        
        // Parse the declaration
        var classMatch = Regex.Match(fullDeclaration, @"(\w+)\s+subclass:\s+#(\w+)");
        if (!classMatch.Success) return;
        
        var classDef = new ClassDefinition
        {
            Superclass = classMatch.Groups[1].Value,
            Name = classMatch.Groups[2].Value
        };
        
        // Parse instance variables
        var instVarsMatch = Regex.Match(fullDeclaration, @"instanceVariableNames:\s+'([^']*)'");
        if (instVarsMatch.Success && !string.IsNullOrWhiteSpace(instVarsMatch.Groups[1].Value))
        {
            classDef.InstanceVariables = instVarsMatch.Groups[1].Value.Split(' ')
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
        }
        
        // Parse class variables
        var classVarsMatch = Regex.Match(fullDeclaration, @"classVariableNames:\s+'([^']*)'");
        if (classVarsMatch.Success && !string.IsNullOrWhiteSpace(classVarsMatch.Groups[1].Value))
        {
            classDef.ClassVariables = classVarsMatch.Groups[1].Value.Split(' ')
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
        }
        
        // Parse pool dictionaries
        var poolMatch = Regex.Match(fullDeclaration, @"poolDictionaries:\s+'([^']*)'");
        if (poolMatch.Success && !string.IsNullOrWhiteSpace(poolMatch.Groups[1].Value))
        {
            classDef.PoolDictionaries = poolMatch.Groups[1].Value.Split(' ')
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
        }
        
        // Parse category
        var categoryMatch = Regex.Match(fullDeclaration, @"category:\s+'([^']*)'");
        if (categoryMatch.Success)
        {
            classDef.Category = categoryMatch.Groups[1].Value;
        }
        
        manifest.Classes.Add(classDef);
        currentClassName = classDef.Name;
        Console.WriteLine($"  Found class: {classDef.Name} < {classDef.Superclass}");
    }
    
    /// <summary>
    /// Parse a method category declaration.
    /// </summary>
    private void ParseMethodCategory(string line)
    {
        // Format: !ClassName methodsFor: 'category'!
        // or: !ClassName class methodsFor: 'category'!
        
        var match = Regex.Match(line, @"!(\w+)(\s+class)?\s+methodsFor:\s+'([^']*)'!");
        if (match.Success)
        {
            currentClassName = match.Groups[1].Value;
            isClassSide = !string.IsNullOrWhiteSpace(match.Groups[2].Value);
            currentCategory = match.Groups[3].Value;
        }
    }
    
    /// <summary>
    /// Parse a method definition.
    /// </summary>
    private void ParseMethod(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || currentClassName == null) return;
        
        // Extract selector from first line
        var lines = source.Split('\n');
        if (lines.Length == 0) return;
        
        var selector = ExtractSelector(lines[0]);
        if (string.IsNullOrWhiteSpace(selector)) return;
        
        var method = new MethodDefinition
        {
            ClassName = isClassSide ? $"{currentClassName} class" : currentClassName,
            Selector = selector,
            Source = source.Trim(),
            Category = currentCategory
        };
        
        // Extract literals from source (simplified - just find symbols and strings)
        ExtractLiterals(source, method.Literals);
        
        manifest.Methods.Add(method);
    }
    
    /// <summary>
    /// Extract the selector from a method signature.
    /// </summary>
    private string ExtractSelector(string signature)
    {
        signature = signature.Trim();
        
        // Binary selector
        if (Regex.IsMatch(signature, @"^[+\-*/\\<>=@,%|&?!]+"))
        {
            var match = Regex.Match(signature, @"^([+\-*/\\<>=@,%|&?!]+)");
            return match.Groups[1].Value;
        }
        
        // Keyword selector
        if (signature.Contains(":"))
        {
            var parts = new List<string>();
            var matches = Regex.Matches(signature, @"(\w+:)");
            foreach (Match match in matches)
            {
                parts.Add(match.Groups[1].Value);
            }
            return string.Join("", parts);
        }
        
        // Unary selector
        var unaryMatch = Regex.Match(signature, @"^(\w+)");
        if (unaryMatch.Success)
        {
            return unaryMatch.Groups[1].Value;
        }
        
        return "";
    }
    
    /// <summary>
    /// Extract literals from method source.
    /// </summary>
    private void ExtractLiterals(string source, List<string> literals)
    {
        // Find symbols (#symbol)
        var symbolMatches = Regex.Matches(source, @"#(\w+)");
        foreach (Match match in symbolMatches)
        {
            var symbol = match.Groups[1].Value;
            if (!literals.Contains(symbol))
            {
                literals.Add(symbol);
            }
        }
        
        // Find strings ('string')
        var stringMatches = Regex.Matches(source, @"'([^']*)'");
        foreach (Match match in stringMatches)
        {
            var str = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(str) && !literals.Contains(str))
            {
                literals.Add(str);
            }
        }
        
        // Find message selectors used in the method
        var selectorMatches = Regex.Matches(source, @"\s(\w+:?)(?=\s|$|\.)");
        foreach (Match match in selectorMatches)
        {
            var selector = match.Groups[1].Value;
            if (!literals.Contains(selector) && IsLikelySelector(selector))
            {
                literals.Add(selector);
            }
        }
    }
    
    /// <summary>
    /// Check if a word is likely a selector.
    /// </summary>
    private bool IsLikelySelector(string word)
    {
        // Common selectors to include
        var commonSelectors = new[] { "new", "at:", "at:put:", "size", "do:", "ifTrue:", "ifFalse:", 
                                      "whileTrue:", "value", "class", "isNil", "notNil", "+", "-", "*", "/" };
        return commonSelectors.Contains(word);
    }
    
    /// <summary>
    /// Order classes by dependency (superclasses before subclasses).
    /// </summary>
    private void OrderClassesByDependency()
    {
        var ordered = new List<ClassDefinition>();
        var remaining = new Queue<ClassDefinition>(manifest.Classes);
        var addedClasses = new HashSet<string> { "Object", "ProtoObject" }; // Bootstrap classes
        
        while (remaining.Count > 0)
        {
            var current = remaining.Dequeue();
            
            // Check if superclass is already added or is a bootstrap class
            if (addedClasses.Contains(current.Superclass))
            {
                ordered.Add(current);
                addedClasses.Add(current.Name);
            }
            else
            {
                // Re-queue for later
                remaining.Enqueue(current);
            }
            
            // Prevent infinite loop
            if (remaining.Count > 0 && remaining.Peek() == current)
            {
                // Force add if we've cycled through all
                ordered.Add(current);
                addedClasses.Add(current.Name);
                remaining.Dequeue();
            }
        }
        
        manifest.Classes = ordered;
    }
    
    /// <summary>
    /// Generate simple bytecodes for methods.
    /// </summary>
    private void GenerateBytecodes()
    {
        foreach (var method in manifest.Methods)
        {
            // Generate very simple bytecodes based on method pattern
            if (method.Source.Contains("^self"))
            {
                method.Bytecodes = new List<byte> { 112, 124 }; // push self, return
            }
            else if (method.Source.Contains("^true"))
            {
                method.Bytecodes = new List<byte> { 113, 124 }; // push true, return
            }
            else if (method.Source.Contains("^false"))
            {
                method.Bytecodes = new List<byte> { 114, 124 }; // push false, return
            }
            else if (method.Source.Contains("^nil"))
            {
                method.Bytecodes = new List<byte> { 115, 124 }; // push nil, return
            }
            else if (Regex.IsMatch(method.Source, @"\^[a-z]\w*\s*$"))
            {
                // Return instance variable
                method.Bytecodes = new List<byte> { 0, 124 }; // push inst var 0, return
            }
            else if (method.Source.Contains(":="))
            {
                // Assignment
                method.Bytecodes = new List<byte> { 16, 104 }; // push temp 0, store and pop
            }
            else
            {
                // Default: just return self
                method.Bytecodes = new List<byte> { 112, 124 };
            }
        }
    }
}