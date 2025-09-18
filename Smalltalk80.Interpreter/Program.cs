using System;
using Smalltalk80.Interpreter.Host;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// Main entry point for the Smalltalk-80 interpreter.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Create VM with default memory size
                var vm = new VirtualMachine();
                
                // Create and start console interface
                var console = new ConsoleInterface(vm);
                console.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}