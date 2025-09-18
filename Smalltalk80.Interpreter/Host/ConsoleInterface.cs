using System;
using Smalltalk80.Interpreter.VM;

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
        Console.WriteLine("║     Smalltalk-80 Virtual Machine - Clean Slate Edition    ║");
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
        // Would parse and evaluate Smalltalk expression
        Console.WriteLine("  (Expression evaluation not yet implemented - requires compiler)");
    }
}