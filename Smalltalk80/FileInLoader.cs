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
        var source = File.ReadAllText(path);
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var fileCtx = parser.fileIn();

        MethodHeaderNode? currentHeader = null;
        foreach (var chunk in fileCtx.chunk())
        {
            var text = source.Substring(chunk.Start.StartIndex,
                chunk.Stop.StopIndex - chunk.Start.StartIndex + 1);
            if (text.TrimStart().StartsWith("!"))
            {
                ProcessCategoryChunk(text.Trim(), ref currentHeader);
            }
            else
            {
                ProcessChunk(text.Trim(), ref currentHeader);
            }
        }
    }


    private static AstNode ParseChunk(string chunk)
    {
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
        var lines = chunk.Split('\n');
        var body = lines.Length > 1 ? string.Join("\n", lines[1..]) : string.Empty;
        var scriptCtx = ParseScript(body);
        return scriptCtx;
    }

    private static void ProcessCategoryChunk(string chunk, ref MethodHeaderNode? currentHeader)
    {
        var lines = chunk.Split('\n');
        if (lines.Length == 0)
            return;

        var headerLine = lines[0].Trim();
        if (headerLine == "!!" || headerLine == "!")
            return;

        // Some sources place the terminating '!' for an empty category on the
        // same line as the header (e.g. "!Class methodsFor: 'cat'! !"). Trim any
        // characters after the first closing bang so the parser sees a
        // well-formed header.
        int closeIndex = headerLine.IndexOf("'!");
        if (closeIndex >= 0)
            headerLine = headerLine.Substring(0, closeIndex + 2);

        headerLine = headerLine.Replace("! !", "!");

        var node = ParseChunk(headerLine);
        if (node is not MethodHeaderNode headerNode)
            return;
        currentHeader = headerNode;

        var methodLines = new List<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "!!")
                break;

            methodLines.Add(line);

            if (line.TrimEnd().EndsWith("!"))
            {
                var methodChunk = string.Join('\n', methodLines);
                ProcessChunk(methodChunk, ref currentHeader);
                methodLines.Clear();
            }
        }
    }

    private static void ProcessChunk(string chunk, ref MethodHeaderNode? currentHeader)
    {
        var node = ParseChunk(chunk);
        switch (node)
        {
            case ClassDefinitionNode cdef:
                var super = SystemDictionary.TryGetClass(cdef.Superclass, out var sup) ? sup : null;
                cdef.Options.TryGetValue("instanceVariableNames", out var ivars);
                var varList = ivars is not null ? ivars.Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
                SystemDictionary.DefineClass(cdef.ClassName, super, varList);
                break;
            case MethodHeaderNode header:
                currentHeader = header;
                break;
            case ClassCommentNode:
                // Comment chunks do not affect loading state.
                break;
            case ScriptNode script when currentHeader is not null:
                var lines = chunk.Split('\n');
                if (lines.Length == 0) return;
                var headerLine = lines[0].Trim();
                if (headerLine.StartsWith("--"))
                    break;
                var (selector, args) = ParseSelector(headerLine);
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
                while (bodyLines.Count > 0 && bodyLines[^1].Trim() == "!")
                    bodyLines.RemoveAt(bodyLines.Count - 1);

                var bodyText = bodyLines.Count > 0 ? string.Join("\n", bodyLines) : string.Empty;
                if (bodyText.EndsWith("!"))
                    bodyText = bodyText.TrimEnd().TrimEnd('!');
                var bodyAst = ParseScript(bodyText);
                var cls = SystemDictionary.DefineClass(currentHeader.ClassName);
                var method = AstMethodCompiler.CompileMethod(bodyAst.Sequence, args, cls.InstanceVariables, comment);
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
        parser.RemoveErrorListeners();
        var ctx = parser.script();
        return (ScriptNode)new AstBuildingVisitor().Visit(ctx)!;
    }

    private static (string Selector, List<string> Arguments) ParseSelector(string line)
    {
        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var selectorParts = new List<string>();
        var args = new List<string>();

        for (int i = 0; i < tokens.Length; i++)
        {
            var tok = tokens[i];
            if (tok.EndsWith(":"))
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
