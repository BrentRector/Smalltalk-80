using System;
using System.IO;

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
    /// Loads a Smalltalk source file containing a single method definition and
    /// installs it on the specified class. The first line of the file must be
    /// of the form <c>ClassName&gt;&gt;selector</c> with the remaining lines
    /// representing the method body.
    /// </summary>
    /// <param name="path">Path to the source file.</param>
    public void LoadSourceFile(string path)
    {
        var lines = System.IO.File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Source file is empty.");

        var header = lines[0];
        var parts = header.Split(new[] { ">>" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException("First line must be 'ClassName>>selector'.");

        var className = parts[0].Trim();
        var selectorName = parts[1].Trim();
        var body = string.Join(Environment.NewLine, lines[1..]);

        var parser = new SmalltalkParser(body);
        var method = parser.ParseMethod();

        var superclass = SystemDictionary.TryGetClass("Object", out var objCls) ? objCls : null;
        var cls = SystemDictionary.DefineClass(className, superclass);
        cls.MethodDictionary[new Symbol(selectorName)] = method;
    }

    /// <summary>
    /// Loads a traditional Smalltalk file-in source containing multiple class
    /// and method definitions.
    /// </summary>
    /// <param name="path">The path to the file-in source.</param>
    public void LoadSystemFile(string path)
    {
        FileInLoader.Load(path);
        SystemStatePrinter.PrintSummary();
    }

    /// <summary>
    /// Compiles the provided source code and evaluates the resulting method in
    /// the context of the global <c>Object</c> receiver.
    /// </summary>
    /// <param name="source">Smalltalk source code to execute.</param>
    /// <returns>The result of evaluating the method.</returns>
    public object? EvaluateSource(string source)
    {
        var parser = new SmalltalkParser(source);
        var method = parser.ParseMethod();
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
