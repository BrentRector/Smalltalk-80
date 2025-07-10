using System;
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
        parser.Interpreter.PredictionMode = Antlr4.Runtime.Atn.PredictionMode.LL_EXACT_AMBIG_DETECTION;
        parser.RemoveErrorListeners();
        var fileCtx = parser.fileIn();
        var visitor = new AstBuildingVisitor(tokens);
        var chunks = new List<AstNode>();

        foreach (var chunk in fileCtx.chunk())
        {
            if (visitor.Visit(chunk) is AstNode node)
                chunks.Add(node);
        }

        return new FileNode(chunks);
    }
}
