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
        }

        var ast = BuildAst(methodName, argumentNames, instanceVariables);
        var compiled = AstMethodCompiler.CompileMethod(ast.Sequence, argumentNames, instanceVariables);

        if (methodName is not null)
            Tracer.Log($"Exiting method: {methodName}");

        return compiled;
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

    private ScriptNode BuildAst(string? methodName, IReadOnlyList<string>? arguments = null, IReadOnlyList<string>? variables = null)
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var visitor = new AstBuildingVisitor(methodName, arguments, variables);

        var unit = parser.replUnit();
        if (parser.NumberOfSyntaxErrors == 0)
        {
            if (unit is Smalltalk80Parser.ReplSequenceContext seqCtx)
                return new ScriptNode((SequenceNode)visitor.Visit(seqCtx.sequence())!);

            return (ScriptNode)visitor.Visit(unit)!;
        }

        tokens.Seek(0);
        parser = new Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();
        var methodDef = parser.methodDefinition();
        if (parser.NumberOfSyntaxErrors > 0)
            throw new InvalidOperationException("Invalid Smalltalk source.");

        var methodNode = (MethodDefinitionNode)visitor.Visit(methodDef)!;
        return new ScriptNode(methodNode.Body);
    }

}
