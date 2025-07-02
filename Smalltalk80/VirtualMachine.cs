using System;

/// <summary>
/// Provides a high level interface combining the interpreter and object memory
/// into a simple virtual machine suitable for the REPL and other hosts.
/// </summary>
public sealed class VirtualMachine
{
    /// <summary>
    /// Gets the interpreter instance executing methods for this VM.
    /// </summary>
    public Interpreter Interpreter { get; } = new() { ExitOnReturn = false };

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMachine"/> class and
    /// loads the standard library objects.
    /// </summary>
    public VirtualMachine() => StandardLibrary.Initialize();

    /// <summary>
    /// Compiles the provided source code and evaluates the resulting method in
    /// the context of the global <c>Object</c> receiver.
    /// </summary>
    /// <param name="source">Smalltalk source code to execute.</param>
    /// <returns>The result of evaluating the method.</returns>
    public object? EvaluateSource(string source)
    {
        var method = SourceCompiler.Compile(source);
        var receiver = StandardLibrary.Get("Object");
        return Interpreter.Evaluate(method, receiver);
    }

    /// <summary>
    /// Starts an interactive REPL session using this virtual machine.
    /// </summary>
    public void RunRepl()
    {
        Console.WriteLine("Smalltalk-80 REPL. Type 'exit' to quit.");
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
                var result = EvaluateSource(line);
                Console.WriteLine($"=> {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"! {ex.Message}");
            }
        }
    }
}
