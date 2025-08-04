using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

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
    public VirtualMachine()
    {
        StandardLibrary.Initialize();
        LoadStandardLibraryClasses();
    }

    /// <summary>
    /// Loads the standard library classes from StandardLibrary.st if it exists.
    /// This provides essential classes like Object, Boolean, True, False, SmallInteger
    /// with all their control flow and arithmetic methods.
    /// </summary>
    private void LoadStandardLibraryClasses()
    {
        var standardLibraryPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "StandardLibrary.st");

        if (File.Exists(standardLibraryPath))
        {
            try
            {
                Console.WriteLine($"[BOOTSTRAP] Loading standard library from: {standardLibraryPath}");
                LoadSystemFile(standardLibraryPath);
                Console.WriteLine("[BOOTSTRAP] Standard library loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOTSTRAP] Warning: Failed to load standard library: {ex.Message}");
                Console.WriteLine("[BOOTSTRAP] Continuing with minimal hardcoded standard library");
            }
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] StandardLibrary.st not found at: {standardLibraryPath}");
            Console.WriteLine("[BOOTSTRAP] Using minimal hardcoded standard library only");
        }
    }

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
        var headerPart = parts[1].Trim();
        var (selectorName, args) = ParseSelector(headerPart);
        var body = string.Join(Environment.NewLine, lines[1..]);
        if (!body.TrimEnd().EndsWith("!"))
            body += "!";

        var parser = new SmalltalkParser(body);
        var superclass = SystemDictionary.TryGetClass("Object", out var objCls) ? objCls : null;
        var cls = SystemDictionary.DefineClass(className, superclass);
        var method = parser.ParseMethod(args, cls.InstanceVariables, selectorName);
        cls.MethodDictionary[new Symbol(selectorName)] = method;
    }

    /// <summary>
    /// Loads a Smalltalk system image or file-in source containing multiple class
    /// and method definitions. Uses the advanced SystemImageLoader which handles
    /// forward references and complex dependencies.
    /// </summary>
    /// <param name="path">The path to the system image or file-in source.</param>
    public void LoadSystemFile(string path)
    {
        var result = SystemImageLoader.LoadSystemImage(path);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to load system file: {result.Errors.Count} errors");
        }
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


    private static (string Selector, List<string> Arguments) ParseSelector(string line)
    {
        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var selectorParts = new List<string>();
        var args = new List<string>();

        for (int i = 0; i < tokens.Length; i++)
        {
            var tok = tokens[i];
            if (tok.EndsWith(':'))
            {
                selectorParts.Add(tok);
                if (i + 1 < tokens.Length)
                    args.Add(tokens[++i]);
            }
            else if (selectorParts.Count == 0)
            {
                selectorParts.Add(tok);
                if (i + 1 < tokens.Length)
                    args.Add(tokens[++i]);
            }
            else
            {
                selectorParts.Add(tok);
            }
        }

        var selector = string.Concat(selectorParts);
        return (selector, args);
    }
}
