using System;
using Smalltalk80;

class TestStandardLibraryLoad
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing StandardLibrary.st loading with diagnostics...");

        try
        {
            // Initialize system
            var vm = new VirtualMachine();

            // Try to load StandardLibrary.st - this should trigger our diagnostics
            Console.WriteLine("Attempting to load StandardLibrary.st...");
            var result = SystemImageLoader.LoadSystemImage("StandardLibrary.st");

            Console.WriteLine($"Load result: Success={result.Success}, Errors={result.Errors.Count}");

            foreach (var error in result.Errors)
            {
                Console.WriteLine($"ERROR: {error}");
            }

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"WARNING: {warning}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("Test completed.");
    }
}
