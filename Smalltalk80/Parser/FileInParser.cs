using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Provides functionality to parse classic Smalltalk file-in sources
/// into an abstract syntax tree.
/// </summary>
public static class FileInParser
{
    /// <summary>
    /// Parses the specified file and returns the resulting <see cref="FileNode"/>.
    /// </summary>
    /// <param name="path">Path to the Smalltalk file-in source.</param>
    /// <returns>The parsed <see cref="FileNode"/>.</returns>
    public static FileNode Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        var builder = new StringBuilder();
        var chunks = new List<AstNode>();
        MethodHeaderNode? currentHeader = null;
        List<string>? partialMethod = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine;
            if (line.TrimStart().StartsWith("\"") && !line.TrimEnd().EndsWith("!"))
                continue;

            builder.AppendLine(line);

            if (!line.TrimEnd().EndsWith("!"))
                continue;

            var chunk = builder.ToString().Trim();
            builder.Clear();

            if (chunk.StartsWith("!", StringComparison.Ordinal))
                ProcessCategoryChunk(chunk, ref currentHeader, ref partialMethod, chunks);
            else
                ProcessChunk(chunk, ref currentHeader, ref partialMethod, chunks);
        }

        return new FileNode(chunks);
    }

    private static AstNode ParseChunk(string chunk)
    {
        // Some file-in sources place two exclamation marks at the end of
        // a method. Strip the trailing separator so the parser sees a
        // single bang terminator.
        if (chunk.EndsWith("!!", StringComparison.Ordinal))
            chunk = chunk.Substring(0, chunk.Length - 1);

        var input = new AntlrInputStream(chunk);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
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
            var catCtx = parser.methodCategoryHeader();
            if (parser.NumberOfSyntaxErrors == 0)
                return visitor.Visit(catCtx)!;
        }
        catch
        {
        }

        parser.Reset();
        try
        {
            var commentCtx = parser.classComment();
            if (parser.NumberOfSyntaxErrors == 0)
                return visitor.Visit(commentCtx)!;
        }
        catch
        {
        }

        parser.Reset();
        try
        {
            var methodCtx = parser.methodDefinition();
            if (parser.NumberOfSyntaxErrors == 0)
                return visitor.Visit(methodCtx)!;
        }
        catch
        {
        }

        parser.Reset();
        var lines = chunk.Split('\n');
        var body = lines.Length > 1 ? string.Join('\n', lines[1..]) : string.Empty;
        var script = ParseScript(body);
        return script;
    }

    private static void ProcessCategoryChunk(
        string chunk,
        ref MethodHeaderNode? currentHeader,
        ref List<string>? partialMethod,
        List<AstNode> output)
    {
        var lines = chunk.Split('\n');
        if (lines.Length == 0)
            return;

        var headerLine = lines[0].Trim();
        if (headerLine == "!!" || headerLine == "!")
            return;

        int closeIndex = headerLine.IndexOf("'!");
        if (closeIndex >= 0)
            headerLine = headerLine.Substring(0, closeIndex + 2);

        headerLine = headerLine.Replace("! !", "!");

        var node = ParseChunk(headerLine);
        if (node is not MethodHeaderNode headerNode)
            return;
        currentHeader = headerNode;
        output.Add(headerNode);

        partialMethod ??= new List<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "!!")
                break;

            partialMethod!.Add(line);

            if (line.TrimEnd().EndsWith("!"))
            {
                var methodChunk = string.Join('\n', partialMethod);
                partialMethod = null;
                ProcessChunk(methodChunk, ref currentHeader, ref partialMethod, output);
            }
        }
    }

    private static void ProcessChunk(
        string chunk,
        ref MethodHeaderNode? currentHeader,
        ref List<string>? partialMethod,
        List<AstNode> output)
    {
        if (currentHeader is not null && partialMethod is not null)
        {
            var lines = chunk.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim() == "!!")
                    continue;
                partialMethod.Add(line);
                if (line.TrimEnd().EndsWith("!"))
                {
                    var methodChunk = string.Join('\n', partialMethod);
                    partialMethod = null;
                    ProcessChunk(methodChunk, ref currentHeader, ref partialMethod, output);
                    return;
                }
            }
            return;
        }

        if (currentHeader is not null && partialMethod is null)
        {
            var node = ParseChunk(chunk);
            if (node is MethodDefinitionNode m)
            {
                var lines = chunk.Split('\n');
                var body = lines.Length > 1 ? string.Join('\n', lines[1..]) : string.Empty;
                body = body.TrimEnd('!');
                node = new MethodDefinitionNode(m.Selector, m.Arguments, m.Body, m.Comment, body);
            }
            output.Add(node);
            return;
        }

        var parsed = ParseChunk(chunk);
        if (parsed is MethodDefinitionNode m2)
        {
            var lines = chunk.Split('\n');
            var body = lines.Length > 1 ? string.Join('\n', lines[1..]) : string.Empty;
            body = body.TrimEnd('!');
            parsed = new MethodDefinitionNode(m2.Selector, m2.Arguments, m2.Body, m2.Comment, body);
        }
        output.Add(parsed);
    }

    private static ScriptNode ParseScript(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var ctx = parser.script();
        return (ScriptNode)new AstBuildingVisitor().Visit(ctx)!;
    }

}
