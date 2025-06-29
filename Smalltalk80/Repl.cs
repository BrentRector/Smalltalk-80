using System;

/// <summary>
/// Provides an interactive Smalltalk-80 read–eval–print loop.
/// </summary>
public static class Repl
{
    /// <summary>
    /// Starts the interactive session.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("Smalltalk-80 REPL. Type 'exit' to quit.");
        var interpreter = new Interpreter { ExitOnReturn = false };
        while (true)
        {
            Console.Write("st> ");
            var line = Console.ReadLine();
            if (line is null)
                break;

            line = line.Trim();
            if (line.Length == 0)
                continue;
            if (string.Equals(line, "exit", StringComparison.OrdinalIgnoreCase))
                break;

            try
            {
                var method = SourceCompiler.Compile(line);
                var result = interpreter.Evaluate(method, StandardLibrary.Get("Object"));
                Console.WriteLine($"=> {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"! {ex.Message}");
            }
        }
    }
}
