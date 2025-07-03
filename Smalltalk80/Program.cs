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
    ///         <item><description><c>--repl</c> – start an interactive REPL.</description></item>
    ///         <item><description><c>--load &lt;file&gt;</c> – load a Smalltalk source file.</description></item>
    ///         <item><description><c>--filein &lt;file&gt;</c> – load a classic Smalltalk file-in.</description></item>
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

        if (args.Contains("--gui"))
        {
            Console.WriteLine("GUI mode is not supported in this build.");
        }

        if (args.Contains("--repl"))
        {
            vm.RunRepl();
        }
    }
}