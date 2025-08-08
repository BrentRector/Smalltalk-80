using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Extracts headless-compatible classes from the Smalltalk-80 system image.
/// </summary>
class ExtractHeadlessClasses
{
    // Classes to include for a headless system
    private static readonly HashSet<string> IncludedClasses = new()
    {
        // Core Object System (from bootstrap, will be enhanced)
        "Object", "UndefinedObject", "Behavior", "ClassDescription", 
        "Class", "Metaclass",
        
        // Booleans
        "Boolean", "True", "False",
        
        // Magnitudes and Numbers
        "Magnitude", "Number", "Integer", "SmallInteger", 
        "LargePositiveInteger", "LargeNegativeInteger",
        "Float", "Fraction", "Random",
        
        // Characters and Strings
        "Character", "String", "Symbol",
        
        // Date and Time
        "Date", "Time",
        
        // Collections - Basic
        "Collection", "SequenceableCollection", "ArrayedCollection",
        "Array", "ByteArray", "WordArray", "RunArray",
        
        // Collections - Ordered
        "OrderedCollection", "SortedCollection",
        
        // Collections - Sets
        "Set", "IdentitySet", "Bag",
        
        // Collections - Dictionaries
        "LookupKey", "Association", "Link",
        "Dictionary", "IdentityDictionary", "SystemDictionary",
        "MethodDictionary", "LiteralDictionary",
        
        // Collections - Others
        "Interval", "LinkedList", "MappedCollection",
        
        // Streams
        "Stream", "PositionableStream", 
        "ReadStream", "WriteStream", "ReadWriteStream",
        "ExternalStream", "FileStream",
        
        // Text (basic support)
        "Text", "RunArray",
        
        // Execution Machinery
        "CompiledMethod", "MethodContext", "ContextPart",
        "BlockContext", "Message",
        
        // Processes
        "Process", "ProcessorScheduler", "Semaphore", 
        "SharedQueue", "Delay",
        
        // Compiler/Parser
        "Scanner", "Parser", "Compiler", "Encoder",
        "ParseNode", "LeafNode", "VariableNode", "LiteralNode",
        "SelectorNode", "SpecialSelectorNode",
        "MessageNode", "MethodNode", "ReturnNode",
        "AssignmentNode", "BlockNode", "CascadeNode",
        "ParseStack", "RemoteString",
        
        // Decompiler
        "InstructionStream", "InstructionPrinter", 
        "Decompiler", "DecompilerConstructor",
        
        // System Organization
        "SystemOrganizer", "ClassOrganizer", 
        "ClassCategoryReader", "MethodDescription",
        
        // File System (non-Alto)
        "File", "FileDirectory", "FilePage",
        
        // Change Management
        "Change", "ChangeSet", "ClassChange", "MethodChange",
        "ClassDefinitionChange", "ClassCommentChange",
        "MethodDefinitionChange", "ChangeList",
        
        // Error Handling
        "Error", "Notification", "Warning",
        "MessageNotUnderstood", "SyntaxError",
        
        // System Support
        "SystemDictionary", "Symbol", "Metaclass",
        "Point", "Rectangle", "Interval"
    };

    // Categories to exclude entirely
    private static readonly HashSet<string> ExcludedCategories = new()
    {
        "Graphics-Primitives", "Graphics-Display Objects", "Graphics-Paths",
        "Graphics-Views", "Graphics-Editors", "Graphics-Support",
        "Interface-Framework", "Interface-Menus", "Interface-Browser",
        "Interface-Debugger", "Interface-Inspector", "Interface-Transcript",
        "Interface-Support", "Interface-Prompt",
        "Files-Alto", "System-Primitives"
    };

    static void Main(string[] args)
    {
        var systemImagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "Smalltalk80", "Smalltalk-80 Source", "Smalltalk-80.st");
            
        var outputPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "HeadlessStandardLibrary.st");

        if (!File.Exists(systemImagePath))
        {
            Console.WriteLine($"System image not found at: {systemImagePath}");
            return;
        }

        Console.WriteLine("Extracting headless-compatible classes from system image...");
        Console.WriteLine($"Source: {systemImagePath}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine($"Target classes: {IncludedClasses.Count}");

        var output = new StringBuilder();
        var extractedClasses = new HashSet<string>();
        var extractedMethods = 0;

        // Add header
        output.AppendLine("\"===========================================\"");
        output.AppendLine("\"Headless Standard Library for Smalltalk-80\"");
        output.AppendLine("\"===========================================\"");
        output.AppendLine("\"");
        output.AppendLine("\"This file contains all non-graphical classes from the Smalltalk-80 system image\"");
        output.AppendLine("\"suitable for running a headless Smalltalk system.\"");
        output.AppendLine("\"");
        output.AppendLine($"\"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
        output.AppendLine($"\"Classes: {IncludedClasses.Count} targeted\"");
        output.AppendLine("\"");
        output.AppendLine("\"===========================================\"");
        output.AppendLine();

        var lines = File.ReadAllLines(systemImagePath);
        var i = 0;
        
        while (i < lines.Length)
        {
            var line = lines[i];
            
            // Check for class definition
            var classMatch = Regex.Match(line, @"(\w+)\s+subclass:\s+#(\w+)");
            if (!classMatch.Success)
            {
                classMatch = Regex.Match(line, @"(\w+)\s+variableSubclass:\s+#(\w+)");
            }
            if (!classMatch.Success)
            {
                classMatch = Regex.Match(line, @"(\w+)\s+variableByteSubclass:\s+#(\w+)");
            }
            if (!classMatch.Success)
            {
                classMatch = Regex.Match(line, @"(\w+)\s+variableWordSubclass:\s+#(\w+)");
            }
            
            if (classMatch.Success)
            {
                var className = classMatch.Groups[2].Value;
                
                // Check if we should include this class
                if (IncludedClasses.Contains(className))
                {
                    Console.WriteLine($"Extracting class: {className}");
                    
                    // Copy the class definition
                    var classDefStart = i;
                    output.AppendLine(line);
                    i++;
                    
                    // Copy until we hit the end marker (!)
                    while (i < lines.Length && !lines[i].Trim().Equals("!"))
                    {
                        // Check if category should be excluded
                        if (lines[i].Contains("category:"))
                        {
                            var categoryMatch = Regex.Match(lines[i], @"category:\s*'([^']+)'");
                            if (categoryMatch.Success)
                            {
                                var category = categoryMatch.Groups[1].Value;
                                if (ExcludedCategories.Contains(category))
                                {
                                    // Skip this class
                                    while (i < lines.Length && !lines[i].Trim().Equals("!"))
                                    {
                                        i++;
                                    }
                                    i++; // Skip the !
                                    continue;
                                }
                            }
                        }
                        output.AppendLine(lines[i]);
                        i++;
                    }
                    if (i < lines.Length)
                    {
                        output.AppendLine(lines[i]); // The closing !
                    }
                    output.AppendLine();
                    extractedClasses.Add(className);
                    i++;
                    continue;
                }
            }
            
            // Check for method category for an included class
            var methodMatch = Regex.Match(line, @"!(\w+)\s+(class\s+)?methodsFor:\s*'([^']*)'!");
            if (methodMatch.Success)
            {
                var className = methodMatch.Groups[1].Value;
                var isClassSide = methodMatch.Groups[2].Success;
                var category = methodMatch.Groups[3].Value;
                
                if (extractedClasses.Contains(className))
                {
                    // Check if category should be excluded
                    if (!category.Contains("private") || category.Contains("initialization"))
                    {
                        // Copy the method category header
                        output.AppendLine(line);
                        i++;
                        
                        // Copy all methods in this category
                        var methodCount = 0;
                        while (i < lines.Length)
                        {
                            // Check for method separator or end of category
                            if (lines[i].Trim() == "! !")
                            {
                                output.AppendLine(lines[i]);
                                i++;
                                break;
                            }
                            
                            // Copy method
                            output.AppendLine(lines[i]);
                            i++;
                            
                            // Copy method body until separator
                            while (i < lines.Length && lines[i].Trim() != "! !" && lines[i].Trim() != "!")
                            {
                                output.AppendLine(lines[i]);
                                i++;
                            }
                            
                            if (i < lines.Length && lines[i].Trim() == "!")
                            {
                                output.AppendLine(lines[i] + " !");
                                methodCount++;
                                extractedMethods++;
                                i++;
                            }
                        }
                        
                        if (methodCount > 0)
                        {
                            output.AppendLine();
                        }
                        continue;
                    }
                }
            }
            
            // Check for class comment
            var commentMatch = Regex.Match(line, @"(\w+)\s+comment:");
            if (commentMatch.Success)
            {
                var className = commentMatch.Groups[1].Value;
                if (extractedClasses.Contains(className))
                {
                    // Copy the comment
                    output.AppendLine(line);
                    i++;
                    while (i < lines.Length && !lines[i].EndsWith("'!"))
                    {
                        output.AppendLine(lines[i]);
                        i++;
                    }
                    if (i < lines.Length)
                    {
                        output.AppendLine(lines[i]);
                    }
                    output.AppendLine();
                    i++;
                    continue;
                }
            }
            
            i++;
        }

        // Add footer
        output.AppendLine();
        output.AppendLine("\"===========================================\"");
        output.AppendLine("\"End of Headless Standard Library\"");
        output.AppendLine($"\"Extracted {extractedClasses.Count} classes with {extractedMethods} method categories\"");
        output.AppendLine("\"===========================================\"");

        // Write the output file
        File.WriteAllText(outputPath, output.ToString());

        Console.WriteLine($"\nExtraction complete!");
        Console.WriteLine($"Classes extracted: {extractedClasses.Count}/{IncludedClasses.Count}");
        Console.WriteLine($"Methods extracted: {extractedMethods}");
        Console.WriteLine($"Output written to: {outputPath}");

        // Report missing classes
        var missing = IncludedClasses.Except(extractedClasses).ToList();
        if (missing.Any())
        {
            Console.WriteLine($"\nClasses not found in system image ({missing.Count}):");
            foreach (var cls in missing.OrderBy(c => c))
            {
                Console.WriteLine($"  - {cls}");
            }
        }
    }
}