using System;

/// <summary>
///     Utility class that compiles Smalltalk source code directly into bytecode.
/// </summary>
public static class SourceCompiler
{
    /// <summary>
    ///     Compiles a snippet of Smalltalk source code into a <see cref="CompiledMethod"/>.
    /// </summary>
    /// <param name="source">The source code to compile.</param>
    /// <returns>The compiled method.</returns>
    public static CompiledMethod Compile(string source)
    {
        var tokenizer = new SmalltalkTokenizer(source);
        var parser = new SmalltalkParser(tokenizer);
        return parser.ParseMethod();
    }
}