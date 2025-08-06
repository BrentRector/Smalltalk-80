using System;
using System.IO;
using Smalltalk80;

class TestLoader
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing updated StandardLibrary.st loading...");

        try
        {
            // Initialize system
            var vm = new VirtualMachine();

            // Get the full path to StandardLibrary.st
            var standardLibraryPath = Path.Combine(Directory.GetCurrentDirectory(), "StandardLibrary.st");

            if (!File.Exists(standardLibraryPath))
            {
                Console.WriteLine($"ERROR: StandardLibrary.st not found at {standardLibraryPath}");
                return;
            }

            Console.WriteLine($"Loading StandardLibrary.st from: {standardLibraryPath}");

            // Try to load StandardLibrary.st
            var result = SystemImageLoader.LoadSystemImage(standardLibraryPath);

            Console.WriteLine($"Load result: Success={result.Success}");
            Console.WriteLine($"Classes loaded: {result.ClassesLoaded}");
            Console.WriteLine($"Methods loaded: {result.MethodsLoaded}");
            Console.WriteLine($"Errors: {result.Errors.Count}");
            Console.WriteLine($"Warnings: {result.Warnings.Count}");

            if (result.Errors.Count > 0)
            {
                Console.WriteLine("\nERRORS:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  {error.Message}");
                }
            }

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine("\nWARNINGS:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  {warning.Message}");
                }
            }

            if (result.Success)
            {
                Console.WriteLine("\n✅ StandardLibrary.st loaded successfully!");
            }
            else
            {
                Console.WriteLine("\n❌ StandardLibrary.st failed to load!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception during loading: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nTest completed.");
    }
}
