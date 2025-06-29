using System;
using System.Linq;
// GUI functionality removed for cross-platform build

public class Program {
    [STAThread]
    public static void Main(string[] args) {
        Console.WriteLine("Smalltalk-80 System Boot");
        StandardLibrary.Initialize();

        if (args.Contains("--gui")) {
            Console.WriteLine("GUI mode is not supported in this build.");
        } else if (args.Contains("--repl")) {
            Repl.Run();
        }
    }
}