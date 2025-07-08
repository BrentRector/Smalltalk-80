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
            switch (chunk)
            {
                case Smalltalk80Parser.ClassChunkContext classChunk:
                    chunks.Add((AstNode)visitor.Visit(classChunk.classDefinition())!);
                    break;

                case Smalltalk80Parser.MethodCategoryChunkContext methodChunk:
                    var header = (MethodHeaderNode)visitor.Visit(methodChunk.methodCategoryHeaderFile())!;
                    chunks.Add(header);
                    foreach (var methodWithBang in methodChunk.methodDefinitionWithBang())
                    {
                        var innerMethod = methodWithBang.methodDefinition();
                        var method = (MethodDefinitionNode)visitor.Visit(innerMethod)!;
                        var seqCtx = innerMethod.sequence();
                        int start;
                        int end;
                        string bodyText;
                        if (seqCtx is not null)
                        {
                            start = seqCtx.Start.StartIndex;
                            end = seqCtx.Stop.StopIndex;
                            bodyText = end < start
                                ? string.Empty
                                : source.Substring(start, end - start + 1);
                        }
                        else
                        {
                            start = innerMethod.Start.StartIndex;
                            end = innerMethod.Stop.StopIndex;
                            bodyText = string.Empty;
                        }
                        method = method with { Source = bodyText };
                        chunks.Add(method);
                    }
                    break;

                case Smalltalk80Parser.ClassCommentChunkContext commentChunk:
                    chunks.Add((AstNode)visitor.Visit(commentChunk.classComment())!);
                    break;

                case Smalltalk80Parser.ScriptChunkContext scriptChunk:
                    var seq = (SequenceNode)visitor.Visit(scriptChunk.sequence())!;
                    chunks.Add(new ScriptNode(seq));
                    break;

                case Smalltalk80Parser.StandaloneCommentChunkContext standalone:
                    chunks.Add((AstNode)visitor.Visit(standalone)!);
                    break;
            }
        }

        return new FileNode(chunks);
    }
}
