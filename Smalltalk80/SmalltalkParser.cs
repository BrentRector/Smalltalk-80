using System;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides a parser that converts Smalltalk source code into
/// <see cref="CompiledMethod"/> instances.
/// </summary>
public sealed class SmalltalkParser
{
    private readonly string source;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmalltalkParser"/> class.
    /// </summary>
    /// <param name="source">The Smalltalk source code to parse.</param>
    public SmalltalkParser(string source) => this.source = source;

    /// <summary>
    /// Parses the source into a <see cref="CompiledMethod"/>.
    /// </summary>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod()
    {
        var ast = BuildAst();
        return AstMethodCompiler.CompileMethod(ast.Sequence, Array.Empty<string>(), Array.Empty<string>());
    }

    private ScriptNode BuildAst()
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var tree = parser.script();
        var visitor = new AstBuildingVisitor();
        return (ScriptNode)visitor.Visit(tree)!;
    }

}
