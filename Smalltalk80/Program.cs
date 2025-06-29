using System;
using System.Linq;
// GUI functionality removed for cross-platform build

/// <summary>
///     Entry point for the Smalltalk-80 runtime.
/// </summary>
public static class Program
{
    /// <summary>
    ///     Application entry method.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 System Boot");
        StandardLibrary.Initialize();

        if (args.Contains("--gui")) {
            Console.WriteLine("GUI mode is not supported in this build.");
        } else if (args.Contains("--repl")) {
            Repl.Run();
        }
    }
}