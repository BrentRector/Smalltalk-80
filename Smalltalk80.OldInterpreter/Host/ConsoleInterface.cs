using System;
using System.IO;
using System.Text;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Host;
/// <summary>
/// Console interface for the Smalltalk-80 VM.
/// Provides a REPL and debugging capabilities.
/// </summary>
/// <param name="virtualMachine">The virtual machine instance to control.</param>
public sealed class ConsoleInterface(VirtualMachine virtualMachine)
{
    private readonly VirtualMachine vm = virtualMachine ?? throw new ArgumentNullException(nameof(virtualMachine));
    private bool running = true;

    /// <summary>
    /// Starts the interactive console interface.
    /// </summary>
    public void Start()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     Smalltalk-80 Virtual Machine - Clean Slate Edition     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
            
        if (!vm.IsBootstrapped)
        {
            Console.WriteLine("Bootstrapping system...");
            try
            {
                vm.Bootstrap();
                Console.WriteLine("Bootstrap successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bootstrap failed: {ex.Message}");
                return;
            }
        }
            
        Console.WriteLine("\nType 'help' for available commands, 'quit' to exit.\n");
        
        while (running)
        {
            Console.Write("ST-80> ");
            var input = Console.ReadLine();
            
            // Handle EOF or closed input stream
            if (input == null)
            {
                running = false;
                break;
            }
            
            if (string.IsNullOrWhiteSpace(input))
                continue;
            
            ProcessCommand(input.Trim());
        }
        
        Console.WriteLine("\nGoodbye!");
    }

    private void ProcessCommand(string command)
    {
        var parts = command.Split(' ', 2);
        var cmd = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1] : "";
        
        switch (cmd)
        {
            case "help":
                ShowHelp();
                break;
                
            case "quit" or "exit":
                running = false;
                break;
                
            case "gc":
                RunGarbageCollection();
                break;
                
            case "memory":
                ShowMemoryStats();
                break;
                
            case "classes":
                ListClasses();
                break;
                
            case "inspect" when !string.IsNullOrEmpty(args):
                InspectObject(args);
                break;
                
            case "inspect":
                Console.WriteLine("Usage: inspect <class-name>");
                break;
                
            case "eval" when !string.IsNullOrEmpty(args):
                Evaluate(args);
                break;
                
            case "eval":
                Console.WriteLine("Usage: eval <expression>");
                break;
                
            case "filein" when !string.IsNullOrEmpty(args):
                FileIn(args);
                break;
                
            case "filein":
                Console.WriteLine("Usage: filein <filename.st>");
                break;
                
            default:
                // Treat as Smalltalk expression
                Evaluate(command);
                break;
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("""
            
            Available commands:
              help           - Show this help message
              quit/exit      - Exit the interpreter
              gc             - Run garbage collection
              memory         - Show memory statistics
              classes        - List all registered classes
              inspect <name> - Inspect a class or object
              eval <expr>    - Evaluate a Smalltalk expression
              filein <file>  - Load and execute a Smalltalk source file
            
            Or just type any Smalltalk expression to evaluate it.
            
            """);
    }

    private void RunGarbageCollection()
    {
        Console.WriteLine("Running garbage collection...");
        vm.RunGarbageCollection();
    }

    private void ShowMemoryStats()
    {
        var stats = vm.Memory.GetStatistics();
        Console.WriteLine($"""
            
            Memory Statistics:
              Total Objects: {stats.TotalObjects}
              Total Capacity: {stats.TotalCapacity}
              Usage: {stats.UsagePercentage:F1}%
              Next Index: {stats.NextIndex}
            
            """);
    }

    private void ListClasses()
    {
        Console.WriteLine("\nRegistered Classes:");
        
        // This would iterate through the system dictionary
        // For now, show kernel classes
        foreach (var kernelClass in Bootstrap.KernelSpecification.Classes)
        {
            Console.WriteLine($"  {kernelClass.Name}");
            if (kernelClass.Superclass != null)
            {
                Console.WriteLine($"    superclass: {kernelClass.Superclass}");
            }
            if (kernelClass.InstanceVariables.Length > 0)
            {
                Console.WriteLine($"    inst vars: {string.Join(", ", kernelClass.InstanceVariables)}");
            }
        }
        Console.WriteLine();
    }

    private void InspectObject(string name)
    {
        Console.WriteLine($"Inspecting: {name}");
        // Would implement object inspection
        Console.WriteLine("  (Object inspection not yet implemented)");
    }

    private void Evaluate(string expression)
    {
        Console.WriteLine($"Evaluating: {expression}");
        
        try
        {
            // Use the VM's evaluation method which uses the Smalltalk Compiler
            var result = vm.EvaluateExpression(expression);
            
            if (!result.IsNil)
            {
                // Display the result
                DisplayObject(result);
            }
            else
            {
                Console.WriteLine("  Result: nil");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error evaluating expression: {ex.Message}");
        }
    }
    
    private void DisplayObject(ObjectModel.OOP obj)
    {
        // Basic object display - can be enhanced later
        if (obj.IsNil)
        {
            Console.WriteLine("  Result: nil");
        }
        else if (obj.IsSmallInteger)
        {
            Console.WriteLine($"  Result: {obj.SmallIntegerValue}");
        }
        else
        {
            // For now, just show the OOP value
            // Later we can call the printString method on the object
            Console.WriteLine($"  Result: Object {obj}");
        }
    }
    
    /// <summary>
    /// Loads and evaluates a Smalltalk source file.
    /// </summary>
    /// <param name="fileName">Path to the .st file to load.</param>
    private void FileIn(string fileName)
    {
        Console.WriteLine($"Loading file: {fileName}");
        
        try
        {
            // Check if file exists
            if (!File.Exists(fileName))
            {
                // Try with .st extension if not already present
                if (!fileName.EndsWith(".st", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = fileName + ".st";
                    if (!File.Exists(fileName))
                    {
                        Console.WriteLine($"  Error: File not found: {fileName}");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"  Error: File not found: {fileName}");
                    return;
                }
            }
            
            // Read the file contents
            string sourceCode;
            using (var reader = new StreamReader(fileName, Encoding.UTF8))
            {
                sourceCode = reader.ReadToEnd();
            }
            
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                Console.WriteLine("  Warning: File is empty");
                return;
            }
            
            Console.WriteLine($"  File loaded, {sourceCode.Length} characters");
            
            // Split the source code into chunks by exclamation marks (Smalltalk chunk format)
            // In Smalltalk file format, code is often separated by ! characters
            var chunks = SplitIntoChunks(sourceCode);
            
            Console.WriteLine($"  Processing {chunks.Length} code chunks...");
            
            int successCount = 0;
            int errorCount = 0;
            
            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk))
                    continue;
                    
                try
                {
                    // Evaluate each chunk
                    var result = vm.EvaluateExpression(chunk);
                    successCount++;
                    
                    // Don't display intermediate results unless they're errors
                    // This keeps the output clean when loading large files
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine($"  Error in chunk: {ex.Message}");
                    // Show a preview of the problematic chunk
                    var preview = chunk.Length > 50 ? chunk.Substring(0, 50) + "..." : chunk;
                    Console.WriteLine($"    Chunk: {preview}");
                }
            }
            
            // Summary
            Console.WriteLine($"  File-in complete: {successCount} chunks processed successfully");
            if (errorCount > 0)
            {
                Console.WriteLine($"  Warning: {errorCount} chunks had errors");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error reading file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Splits Smalltalk source code into chunks.
    /// Chunks are typically separated by exclamation marks in Smalltalk file-out format.
    /// </summary>
    private string[] SplitIntoChunks(string sourceCode)
    {
        // Simple implementation - can be enhanced to handle more complex formats
        // In standard Smalltalk file-out format, chunks are separated by '!'
        // But for now, let's also support newline-separated statements
        
        // If the file contains exclamation marks, use them as delimiters
        if (sourceCode.Contains('!'))
        {
            return sourceCode.Split('!', StringSplitOptions.RemoveEmptyEntries);
        }
        
        // Otherwise, treat the entire file as one chunk
        // (The Smalltalk compiler should handle multiple statements)
        return new[] { sourceCode };
    }
}