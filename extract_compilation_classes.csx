#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;

// Classes to extract in dependency order
var targetClasses = new[]
{
    "OrderedCollection",  // Base collection class
    "ParseNode",         // Base parse node class
    "LeafNode",          // Subclass of ParseNode
    "LiteralNode",       // Subclass of LeafNode
    "VariableNode",      // Subclass of LeafNode
    "MessageNode",       // Subclass of ParseNode
    "MethodNode",        // Subclass of ParseNode
    "BlockNode",         // Subclass of ParseNode
    "Scanner",           // Lexical analyzer
    "Parser",            // Parser
    "Encoder",           // Bytecode encoder
    "Compiler"           // Main compiler
};

try
{
    // Read the complete system JSON
    var jsonContent = File.ReadAllText(@"D:\Users\brent\OneDrive\Src\Smalltalk-80\complete_system_final.json");
    var systemData = JsonNode.Parse(jsonContent);
    
    var classes = systemData["classes"].AsArray();
    var methods = systemData["methods"].AsArray();
    
    var extractedClasses = new List<JsonNode>();
    var extractedMethods = new List<JsonNode>();
    
    Console.WriteLine($"Total classes in system: {classes.Count}");
    Console.WriteLine($"Total methods in system: {methods.Count}");
    Console.WriteLine();
    
    // Extract classes in the specified order
    foreach (var targetClassName in targetClasses)
    {
        Console.WriteLine($"Extracting class: {targetClassName}");
        
        // Find the class definition
        var classNode = classes.FirstOrDefault(c => c["name"]?.ToString() == targetClassName);
        if (classNode != null)
        {
            extractedClasses.Add(classNode);
            Console.WriteLine($"  Found class definition for {targetClassName}");
            
            // Find all methods for this class
            var classMethods = methods.Where(m => m["class"]?.ToString() == targetClassName).ToList();
            foreach (var method in classMethods)
            {
                extractedMethods.Add(method);
            }
            Console.WriteLine($"  Found {classMethods.Count} methods for {targetClassName}");
        }
        else
        {
            Console.WriteLine($"  WARNING: Class {targetClassName} not found!");
        }
    }
    
    Console.WriteLine();
    Console.WriteLine($"Total extracted classes: {extractedClasses.Count}");
    Console.WriteLine($"Total extracted methods: {extractedMethods.Count}");
    
    // Create the output structure
    var output = new JsonObject
    {
        ["classes"] = new JsonArray(extractedClasses.ToArray()),
        ["methods"] = new JsonArray(extractedMethods.ToArray())
    };
    
    // Write to output file
    var outputPath = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\compilation_classes_extracted.json";
    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    
    File.WriteAllText(outputPath, output.ToJsonString(options));
    Console.WriteLine($"Extracted data written to: {outputPath}");
    
    // Print summary for each class
    Console.WriteLine();
    Console.WriteLine("=== EXTRACTION SUMMARY ===");
    foreach (var targetClassName in targetClasses)
    {
        var classNode = extractedClasses.FirstOrDefault(c => c["name"]?.ToString() == targetClassName);
        if (classNode != null)
        {
            var superclass = classNode["superclass"]?.ToString();
            var category = classNode["category"]?.ToString();
            var instanceVars = classNode["instanceVariables"]?.AsArray()?.Count ?? 0;
            var classVars = classNode["classVariables"]?.AsArray()?.Count ?? 0;
            
            var classMethods = extractedMethods.Where(m => m["class"]?.ToString() == targetClassName).ToList();
            var instanceMethods = classMethods.Where(m => m["isClassMethod"]?.GetValue<bool>() == false).Count();
            var classMethodsCount = classMethods.Where(m => m["isClassMethod"]?.GetValue<bool>() == true).Count();
            
            Console.WriteLine($"{targetClassName}:");
            Console.WriteLine($"  Superclass: {superclass}");
            Console.WriteLine($"  Category: {category}");
            Console.WriteLine($"  Instance variables: {instanceVars}");
            Console.WriteLine($"  Class variables: {classVars}");
            Console.WriteLine($"  Instance methods: {instanceMethods}");
            Console.WriteLine($"  Class methods: {classMethodsCount}");
            Console.WriteLine();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}