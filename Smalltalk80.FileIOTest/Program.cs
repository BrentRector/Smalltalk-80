using System;
using System.IO;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.Host;

namespace Smalltalk80.FileIOTest
{
    /// <summary>
    /// Test program for file I/O functionality.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         Smalltalk-80 File I/O Test Program                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Create and bootstrap the VM
                var vm = new VirtualMachine();
                
                if (!vm.IsBootstrapped)
                {
                    Console.WriteLine("Bootstrapping virtual machine...");
                    vm.Bootstrap();
                    Console.WriteLine("✓ Bootstrap complete.\n");
                }
                
                // Test console interface with file-in capability
                Console.WriteLine("Starting console interface with file-in support...");
                Console.WriteLine("Commands:");
                Console.WriteLine("  filein <filename>  - Load and execute a Smalltalk source file");
                Console.WriteLine("  help              - Show all commands");
                Console.WriteLine("  quit              - Exit the interpreter\n");
                
                var console = new ConsoleInterface(vm);
                console.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}