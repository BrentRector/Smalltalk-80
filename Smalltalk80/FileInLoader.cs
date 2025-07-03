using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides functionality to load classes and methods from classic Smalltalk
/// file-in format using the ANTLR4 parser and AST nodes.
/// </summary>
public static class FileInLoader
{
    /// <summary>
    /// Reads the specified file and installs the classes and methods it defines
    /// into the <see cref="SystemDictionary"/>.
    /// </summary>
    /// <param name="path">Path to the Smalltalk source file.</param>
    public static void Load(string path)
    {
        using var reader = new StreamReader(path);
        MethodHeaderNode? currentHeader = null;

        foreach (var chunk in ReadChunks(reader))
        {
            var node = ParseChunk(chunk);
            switch (node)
            {
                case ClassDefinitionNode cdef:
                    var super = SystemDictionary.TryGetClass(cdef.Superclass, out var sup) ? sup : null;
                    SystemDictionary.DefineClass(cdef.ClassName, super);
                    break;
                case MethodHeaderNode header:
                    currentHeader = header;
                    break;
                case ScriptNode script when currentHeader is not null:
                    var lines = chunk.Split('\n');
                    if (lines.Length == 0) continue;
                    var selector = lines[0].Trim();
                    var bodyText = lines.Length > 1 ? string.Join("\n", lines[1..]) : string.Empty;
                    var bodyAst = ParseScript(bodyText);
                    var method = AstMethodCompiler.CompileMethod(bodyAst.Sequence);
                    var cls = SystemDictionary.DefineClass(currentHeader.ClassName);
                    cls.MethodDictionary[new Symbol(selector)] = method;
                    break;
            }
        }
    }

    private static IEnumerable<string> ReadChunks(StreamReader reader)
    {
        var sb = new StringBuilder();
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            if (ch == '!')
            {
                int next = reader.Peek();
                if (next == '!')
                {
                    reader.Read();
                    sb.Append('!');
                    continue;
                }

                if (next == '\n' || next == '\r' || next == -1)
                {
                    yield return sb.ToString();
                    sb.Clear();
                    if (next == '\r')
                    {
                        reader.Read();
                        if (reader.Peek() == '\n') reader.Read();
                    }
                    else if (next == '\n')
                        reader.Read();
                    continue;
                }

                sb.Append('!');
                continue;
            }

            sb.Append((char)ch);
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    private static AstNode ParseChunk(string chunk)
    {
        var input = new AntlrInputStream(chunk);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var visitor = new AstBuildingVisitor();

        try
        {
            var classCtx = parser.classDefinition();
            if (parser.NumberOfSyntaxErrors == 0)
                return visitor.Visit(classCtx)!;
        }
        catch
        {
        }

        parser.Reset();
        try
        {
            var headerInput = new AntlrInputStream(chunk + "!");
            var headerLexer = new Smalltalk80Lexer(headerInput);
            var headerTokens = new CommonTokenStream(headerLexer);
            var headerParser = new Smalltalk80Parser(headerTokens);
            var headerCtx = headerParser.methodHeader();
            if (headerParser.NumberOfSyntaxErrors == 0)
                return visitor.Visit(headerCtx)!;
        }
        catch
        {
        }

        parser.Reset();
        var lines = chunk.Split('\n');
        var body = lines.Length > 1 ? string.Join("\n", lines[1..]) : string.Empty;
        var scriptCtx = ParseScript(body);
        return scriptCtx;
    }

    private static ScriptNode ParseScript(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var ctx = parser.script();
        return (ScriptNode)new AstBuildingVisitor().Visit(ctx)!;
    }
}
