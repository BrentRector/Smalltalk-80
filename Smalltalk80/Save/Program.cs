using System;
using System.Linq;
using System.IO;
// GUI functionality removed for cross-platform build

/// <summary>
///     Entry point for the Smalltalk-80 runtime.
/// </summary>
public static class Program
{
    /// <summary>
    ///     Application entry method.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments. Supported options are:
    ///     <list type="bullet">
    ///         <item><description><c>--load &lt;file&gt;</c> – load a Smalltalk source file.</description></item>
    ///         <item><description><c>--filein &lt;file&gt;</c> – load a classic Smalltalk file-in.</description></item>
    ///         <item><description><c>--system-image &lt;file&gt;</c> – load a complete Smalltalk-80 system image with dependency resolution.</description></item>
    ///     </list>
    /// </param>
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 System Boot");

        var vm = new VirtualMachine();

        var loadIndex = Array.IndexOf(args, "--load");
        if (loadIndex >= 0 && loadIndex < args.Length - 1)
        {
            var path = args[loadIndex + 1];
            try
            {
                vm.LoadSourceFile(path);
                Console.WriteLine($"Loaded '{Path.GetFileName(path)}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load source file: {ex.Message}");
                return;
            }
        }

        var fileInIndex = Array.IndexOf(args, "--filein");
        if (fileInIndex >= 0 && fileInIndex < args.Length - 1)
        {
            var path = args[fileInIndex + 1];
            try
            {
                vm.LoadSystemFile(path);
                Console.WriteLine($"Filed in '{Path.GetFileName(path)}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to file in source: {ex.Message}");
                return;
            }
        }

        var systemImageIndex = Array.IndexOf(args, "--system-image");
        if (systemImageIndex >= 0 && systemImageIndex < args.Length - 1)
        {
            var path = args[systemImageIndex + 1];
            try
            {
                var result = SystemImageLoader.LoadSystemImage(path);
                if (result.Success)
                {
                    Console.WriteLine($"Successfully loaded system image '{Path.GetFileName(path)}'.");
                    Console.WriteLine($"Loaded {result.ClassesLoaded} classes and {result.MethodsLoaded} method chunks.");
                    if (result.Warnings.Count > 0)
                    {
                        Console.WriteLine($"Warnings: {result.Warnings.Count}");
                        foreach (var warning in result.Warnings.Take(5))
                        {
                            Console.WriteLine($"  Warning: {warning.Message}");
                        }
                        if (result.Warnings.Count > 5)
                        {
                            Console.WriteLine($"  ... and {result.Warnings.Count - 5} more warnings");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to load system image: {result.Errors.Count} errors");
                    foreach (var error in result.Errors.Take(5))
                    {
                        Console.WriteLine($"  Error: {error.Message}");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load system image: {ex.Message}");
                return;
            }
        }

        if (args.Contains("--gui"))
        {
            Console.WriteLine("GUI mode is not supported in this build.");
        }

    }
}