using System;
using System.Collections.Generic;
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
    /// Represents the parsed method information including primitive metadata.
    /// </summary>
    private sealed record ParsedMethod(SequenceNode Sequence, bool HasPrimitive, int PrimitiveIndex);

    /// <summary>
    /// Initializes a new instance of the <see cref="SmalltalkParser"/> class.
    /// </summary>
    /// <param name="source">The Smalltalk source code to parse.</param>
    public SmalltalkParser(string source) => this.source = source;

    /// <summary>
    /// Parses the source into a <see cref="CompiledMethod"/> using the provided
    /// argument and instance variable names.
    /// </summary>
    /// <param name="argumentNames">Method argument names in order.</param>
    /// <param name="instanceVariables">Instance variables available in the method.</param>
    /// <param name="methodName">Optional selector for debug tracing.</param>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod(
        IReadOnlyList<string>? argumentNames = null,
        IReadOnlyList<string>? instanceVariables = null,
        string? methodName = null)
    {
        argumentNames ??= Array.Empty<string>();
        instanceVariables ??= Array.Empty<string>();
        if (methodName is not null)
        {
            Tracer.Log($"Parsing method: {methodName}");
            Tracer.Log($"Entering method: {methodName}");
            if (instanceVariables.Count > 0)
                Tracer.Log($"Instance variables in scope: {string.Join(' ', instanceVariables)}");
        }

        var parsed = BuildAst(methodName, argumentNames, instanceVariables);
        var compiled = AstMethodCompiler.CompileMethod(
            parsed.Sequence,
            argumentNames,
            instanceVariables,
            parsed.HasPrimitive,
            parsed.PrimitiveIndex);

        if (methodName is not null)
            Tracer.Log($"Exiting method: {methodName}");

        return new CompiledMethod
        {
            Header = compiled.Header,
            Bytecodes = compiled.Bytecodes,
            Literals = compiled.Literals,
            TemporariesCount = compiled.TemporariesCount,
            HasPrimitive = parsed.HasPrimitive,
            PrimitiveIndex = parsed.PrimitiveIndex,
            Comment = compiled.Comment
        };
    }

    /// <summary>
    /// Parses the source into a <see cref="CompiledMethod"/> using instance
    /// variables defined on the specified class.
    /// </summary>
    /// <param name="className">Name of the class owning the method.</param>
    /// <param name="argumentNames">Method argument names in order.</param>
    /// <param name="methodName">Optional selector for debug tracing.</param>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod(
        string className,
        IReadOnlyList<string>? argumentNames = null,
        string? methodName = null)
    {
        if (!SystemDictionary.TryGetClass(className, out var cls))
            throw new InvalidOperationException($"Class '{className}' is not defined.");

        return ParseMethod(argumentNames, cls.InstanceVariables, methodName);
    }

    /// <summary>
    /// Parses the source into a <see cref="CompiledMethod"/> using variables defined on the specified class.
    /// </summary>
    /// <param name="className">The owning class name.</param>
    /// <param name="isClassSide">If <c>true</c>, class variables are used instead of instance variables.</param>
    /// <param name="argumentNames">Method argument names.</param>
    /// <param name="methodName">Optional selector for tracing.</param>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod(
        string className,
        bool isClassSide,
        IReadOnlyList<string>? argumentNames = null,
        string? methodName = null)
    {
        if (!SystemDictionary.TryGetClass(className, out var cls))
            throw new InvalidOperationException($"Class '{className}' is not defined.");

        var vars = isClassSide ? cls.ClassVariables : cls.InstanceVariables;
        return ParseMethod(argumentNames, vars, methodName);
    }

    private ParsedMethod BuildAst(string? methodName, IReadOnlyList<string>? arguments = null, IReadOnlyList<string>? variables = null)
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var visitor = new AstBuildingVisitor(tokens);

        // Check if this looks like a method definition with signature vs just method body
        var trimmedSource = source.Trim();
        bool hasMethodSignature =
            trimmedSource.Contains("::") ||
            trimmedSource.Contains("methodsFor:") ||
            System.Text.RegularExpressions.Regex.IsMatch(trimmedSource, @"^\s*[a-zA-Z_][a-zA-Z0-9_]*\s*(:|\s+[a-zA-Z_]|\s*$)");

        if (methodName is not null)
        {
            Tracer.Log($"[PARSER_DEBUG] Parsing source: '{trimmedSource}'");
            Tracer.Log($"[PARSER_DEBUG] HasMethodSignature: {hasMethodSignature}");
            Tracer.Log($"[PARSER_DEBUG] EndsWithExclamation: {trimmedSource.EndsWith("!")}");
        }

        // If source doesn't contain method signature, treat it as an expression or method body
        if (!hasMethodSignature)
        {
            // Try expression chunk first (handles expressions ending with !)
            if (trimmedSource.EndsWith("!"))
            {
                try
                {
                    var exprChunkCtx = parser.expressionChunk();
                    if (parser.NumberOfSyntaxErrors > 0)
                    {
                        if (methodName is not null)
                            Tracer.Log($"[PARSER_DEBUG] ExpressionChunk parsing failed with {parser.NumberOfSyntaxErrors} errors");
                        throw new InvalidOperationException("Invalid Smalltalk source.");
                    }

                    var expression = (ExpressionNode)visitor.Visit(exprChunkCtx.expression())!;
                    var exprSequence = new SequenceNode(new List<string>(), new[] { new ExpressionStatement(expression) });
                    if (methodName is not null)
                        Tracer.Log($"[PARSER_DEBUG] Successfully parsed as expression chunk");
                    return new ParsedMethod(exprSequence, false, 0);
                }
                catch (Exception ex)
                {
                    if (methodName is not null)
                        Tracer.Log($"[PARSER_DEBUG] ExpressionChunk parsing exception: {ex.Message}");
                    throw;
                }
            }
            else
            {
                // Parse as expression entry (for plain expressions)
                var exprCtx = parser.expressionEntry();
                if (parser.NumberOfSyntaxErrors > 0)
                    throw new InvalidOperationException("Invalid Smalltalk source.");

                var expression = (ExpressionNode)visitor.Visit(exprCtx.expression())!;
                var exprSequence = new SequenceNode(new List<string>(), new[] { new ExpressionStatement(expression) });
                return new ParsedMethod(exprSequence, false, 0);
            }
        }

        // Parse the full method definition to capture primitives
        var methodCtx = parser.methodDef();
        if (parser.NumberOfSyntaxErrors > 0)
            throw new InvalidOperationException("Invalid Smalltalk source.");

        // Extract primitive information if present
        bool hasPrimitive = false;
        int primitiveIndex = 0;
        var primitiveCtx = methodCtx.primitiveDecl();
        if (primitiveCtx != null)
        {
            hasPrimitive = true;
            var primitiveText = primitiveCtx.GetText();
            // Extract number from "<primitive: N>"
            var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"<primitive:\s*(\d+)>");
            if (match.Success)
            {
                primitiveIndex = int.Parse(match.Groups[1].Value);
            }
        }

        // Parse the method body
        var bodyCtx = methodCtx.methodBody();
        var methodSequence = (SequenceNode)visitor.Visit(bodyCtx)!;

        return new ParsedMethod(methodSequence, hasPrimitive, primitiveIndex);
    }

}
