using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
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
                    var chunk = sb.ToString();
                    sb.Clear();
                    if (next == '\r')
                    {
                        reader.Read();
                        if (reader.Peek() == '\n') reader.Read();
                    }
                    else if (next == '\n')
                    {
                        reader.Read();
                    }

                    if (chunk.Length > 0)
                    {
                        ProcessChunk(chunk, ref currentHeader);
                    }
                    continue;
                }

                sb.Append('!');
                continue;
            }

            sb.Append((char)ch);
        }

        if (sb.Length > 0)
        {
            ProcessChunk(sb.ToString(), ref currentHeader);
        }
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

    private static void ProcessChunk(string chunk, ref MethodHeaderNode? currentHeader)
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
                if (lines.Length == 0) return;
                var selector = lines[0].Trim();
                int index = 1;
                string? comment = null;
                var bodyLines = new List<string>();
                while (index < lines.Length && lines[index].TrimStart().StartsWith("\""))
                {
                    var line = lines[index].Trim();
                    comment = comment is null ? line : string.Join('\n', comment, line);
                    index++;
                }
                if (index < lines.Length)
                    bodyLines.AddRange(lines[index..]);
                var bodyText = bodyLines.Count > 0 ? string.Join("\n", bodyLines) : string.Empty;
                var bodyAst = ParseScript(bodyText);
                var method = AstMethodCompiler.CompileMethod(bodyAst.Sequence, comment);
                var cls = SystemDictionary.DefineClass(currentHeader.ClassName);
                cls.MethodDictionary[new Symbol(selector)] = method;
                break;
        }
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
