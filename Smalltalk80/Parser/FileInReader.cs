using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Provides parsing utilities for classic Smalltalk file-in sources.
/// </summary>
public static class FileInReader
{
    /// <summary>
    /// Parses a file-in source into an abstract syntax tree using the ANTLR
    /// grammar. Method bodies are extracted verbatim for later compilation.
    /// </summary>
    /// <param name="path">Path of the Smalltalk file to parse.</param>
    /// <returns>A <see cref="FileNode"/> representing the file contents.</returns>
    public static FileNode Parse(string path)
    {
        var source = File.ReadAllText(path);
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var fileCtx = parser.fileIn();
        var visitor = new AstBuildingVisitor();
        var chunks = new List<AstNode>();

        foreach (var chunk in fileCtx.chunk())
        {
            if (chunk.classDefinition() is { } cdef)
            {
                chunks.Add((AstNode)visitor.Visit(cdef)!);
                continue;
            }

            if (chunk.methodCategoryHeader() is { } headerCtx)
            {
                var header = (MethodHeaderNode)visitor.Visit(headerCtx)!;
                chunks.Add(header);
                foreach (var methodCtx in chunk.methodDefinition())
                {
                    var method = (MethodDefinitionNode)visitor.Visit(methodCtx)!;
                    var start = methodCtx.sequence().Start.StartIndex;
                    var end = methodCtx.sequence().Stop.StopIndex;
                    var bodyText = source.Substring(start, end - start + 1).TrimEnd('!');
                    method = method with { Source = bodyText };
                    chunks.Add(method);
                }
                continue;
            }

            if (chunk.classComment() is { } commentCtx)
            {
                chunks.Add((AstNode)visitor.Visit(commentCtx)!);
                continue;
            }

            if (chunk.sequence() is { } seqCtx)
            {
                var seq = (SequenceNode)visitor.Visit(seqCtx)!;
                chunks.Add(new ScriptNode(seq));
            }
        }

        return new FileNode(chunks);
    }
}
