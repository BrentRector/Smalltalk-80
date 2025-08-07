#!/usr/bin/env dotnet-script
#r "System.Text.RegularExpressions"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Define the headless-compatible classes to extract
var headlessClasses = new HashSet<string>
{
    // Core classes
    "Object", "UndefinedObject", "Behavior", "ClassDescription", "Class", "Metaclass",
    
    // Boolean classes
    "Boolean", "True", "False",
    
    // Number classes  
    "Magnitude", "Number", "Integer", "SmallInteger", "LargePositiveInteger", "LargeNegativeInteger", 
    "Float", "Fraction", "Random",
    
    // Character and String classes
    "Character", "String", "Symbol",
    
    // Date/Time classes
    "Date", "Time",
    
    // Collection classes
    "Collection", "SequenceableCollection", "ArrayedCollection", "Array", "ByteArray", "WordArray",
    "OrderedCollection", "SortedCollection", "Set", "IdentitySet", "Bag", "Dictionary", 
    "IdentityDictionary", "SystemDictionary", "MethodDictionary", "Interval", "LinkedList",
    
    // Key-Value classes
    "LookupKey", "Association", "Link",
    
    // Stream classes
    "Stream", "PositionableStream", "ReadStream", "WriteStream", "ReadWriteStream", 
    "ExternalStream", "FileStream",
    
    // Text classes
    "Text", "RunArray",
    
    // Execution classes
    "CompiledMethod", "MethodContext", "ContextPart", "BlockContext", "Message",
    
    // Process classes
    "Process", "ProcessorScheduler", "Semaphore", "SharedQueue", "Delay",
    
    // Parser/Compiler classes
    "Scanner", "Parser", "Compiler", "Encoder", "ParseNode", "LeafNode", "LiteralNode", 
    "SelectorNode", "VariableNode", "AssignmentNode", "BlockNode", "CascadeNode", 
    "MessageNode", "MethodNode", "ReturnNode", "DecompilerConstructor",
    "InstructionStream", "InstructionPrinter", "Decompiler", "SyntaxError", "ParseStack",
    
    // System classes
    "SystemOrganizer", "ClassOrganizer", "ClassCategoryReader",
    
    // File system classes (non-Alto versions preferred)
    "File", "FileDirectory", "FilePage",
    
    // Change management classes
    "Change", "ChangeSet", "ClassRelatedChange", "ClassChange", "ClassDefinitionChange",
    "ClassCommentChange", "ClassOtherChange", "MethodChange", "MethodDefinitionChange",
    "MethodOtherChange", "OtherChange", "Checker",
    
    // Basic geometry (not graphics)
    "Point", "Rectangle",
    
    // Utility classes
    "RemoteString", "MethodDescription", "MessageTally"
};

string inputFile = @"Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
string outputFile = "HeadlessStandardLibrary.st";

Console.WriteLine($"Reading file: {inputFile}");
var lines = File.ReadAllLines(inputFile);
Console.WriteLine($"Total lines: {lines.Length}");

var extractedContent = new List<string>();

// Add header
extractedContent.Add("\"Headless Standard Library extracted from Smalltalk-80 System Image\"");
extractedContent.Add($"\"Generated on: {DateTime.Now}\"");
extractedContent.Add($"\"Source classes: {headlessClasses.Count}\"");
extractedContent.Add("");

// Pattern to match class definitions
var classDefPattern = new Regex(@"^(.+)\s+subclass:\s+#(\w+)");
var classMethodsPattern = new Regex(@"^!(\w+)\s+(class\s+)?methodsFor:");
var endPattern = new Regex(@"^(\w+.*subclass:|^$)");

var foundClasses = new HashSet<string>();
var currentClass = "";
var inTargetClass = false;

for (int i = 0; i < lines.Length; i++)
{
    var line = lines[i];
    
    // Check for class definition
    var classMatch = classDefPattern.Match(line);
    if (classMatch.Success)
    {
        var className = classMatch.Groups[2].Value;
        
        if (headlessClasses.Contains(className))
        {
            Console.WriteLine($"Found class: {className} at line {i + 1}");
            foundClasses.Add(className);
            currentClass = className;
            inTargetClass = true;
            
            // Add the class definition
            extractedContent.Add(line);
            
            // Continue adding lines until we hit the next class or end
            for (int j = i + 1; j < lines.Length; j++)
            {
                var nextLine = lines[j];
                
                // Check if this is the start of a new class that we don't want
                var nextClassMatch = classDefPattern.Match(nextLine);
                if (nextClassMatch.Success)
                {
                    var nextClassName = nextClassMatch.Groups[2].Value;
                    if (!headlessClasses.Contains(nextClassName))
                    {
                        // End of our current class, next class is not one we want
                        break;
                    }
                    else
                    {
                        // Next class is also one we want, but we're done with current class
                        break;
                    }
                }
                
                extractedContent.Add(nextLine);
                i = j; // Update main loop index
            }
            
            extractedContent.Add(""); // Add separation between classes
            inTargetClass = false;
        }
    }
}

Console.WriteLine($"\nExtracted {foundClasses.Count} classes:");
foreach (var cls in foundClasses.OrderBy(x => x))
{
    Console.WriteLine($"  - {cls}");
}

Console.WriteLine($"\nMissing classes:");
foreach (var cls in headlessClasses.Except(foundClasses).OrderBy(x => x))
{
    Console.WriteLine($"  - {cls}");
}

// Write the output
Console.WriteLine($"\nWriting to {outputFile}...");
File.WriteAllLines(outputFile, extractedContent);
Console.WriteLine($"Done! {extractedContent.Count} lines written.");