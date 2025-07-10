using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Provides a custom visitor that translates the ANTLR parse tree for the
/// Smalltalk-80 grammar into strongly typed abstract syntax tree (AST) nodes.
/// This visitor is intentionally skeletal and will evolve to cover the full
/// language. Only a handful of rules are currently handled.
/// </summary>
public sealed class AstBuildingVisitor : Smalltalk80BaseVisitor<object?>
{
    private readonly CommonTokenStream tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstBuildingVisitor"/> class.
    /// </summary>
    /// <param name="tokens">The token stream produced by the lexer.</param>
    public AstBuildingVisitor(CommonTokenStream tokens) => this.tokens = tokens;

    /// <summary>
    /// Visits a complete file-in unit and produces a <see cref="FileNode"/>
    /// containing the parsed chunks. Unsupported chunks are ignored for now.
    /// </summary>
    /// <param name="context">The parse context representing the file.</param>
    /// <returns>A <see cref="FileNode"/> instance.</returns>
    public override object? VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        var nodes = new List<AstNode>();
        foreach (var chunk in context.chunk())
        {
            if (Visit(chunk) is AstNode node)
                nodes.Add(node);
        }
        return new FileNode(nodes);
    }

    /// <summary>
    /// Visits a class definition chunk and returns a <see cref="ClassDefinitionNode"/>.
    /// The implementation currently extracts only the superclass name, class name,
    /// and category information.
    /// </summary>
    public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
    {
        var header = context.classHeader();
        var body = context.classBody();
        var superclass = header.className().GetText();
        var className = UnquoteSymbol(header.symbolConst().GetText());
        var category = UnquoteString(body.categoryDecl().stringConst().GetText());

        return new ClassDefinitionNode(
            superclass,
            className,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            category);
    }

    /// <summary>
    /// Visits a class comment chunk and returns a <see cref="ClassCommentNode"/>.
    /// </summary>
    public override object? VisitClassCommentChunk(Smalltalk80Parser.ClassCommentChunkContext context)
    {
        var className = context.className().GetText();
        var comment = UnquoteString(context.stringConst().GetText());
        return new ClassCommentNode(className, comment);
    }

    /// <summary>
    /// Visits a method category chunk. This skeleton creates a <see cref="MethodHeaderNode"/>
    /// followed by a list of <see cref="MethodDefinitionNode"/> objects representing
    /// the methods in the chunk. The caller is responsible for flattening the
    /// returned <see cref="FileNode"/>.
    /// </summary>
    public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
    {
        var className = context.className().GetText();
        var isClassSide = context.GetText().Contains(" class ", StringComparison.Ordinal);
        var category = UnquoteString(context.stringConst().GetText());

        var nodes = new List<AstNode>
        {
            new MethodHeaderNode(className, isClassSide, category)
        };

        foreach (var method in context.methodDef())
        {
            if (Visit(method) is MethodDefinitionNode def)
                nodes.Add(def);
        }

        return new FileNode(nodes);
    }

    /// <summary>
    /// Visits a method definition. Only the selector and primitive metadata are
    /// processed at the moment; the method body is left empty.
    /// </summary>
    public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        var header = (MethodHeaderParts?)Visit(context.methodHeader());
        if (header is null)
            return null;

        bool hasPrimitive = false;
        int primitiveIndex = 0;
        if (context.primitiveDecl() is { } primitive)
        {
            var numberText = primitive.UNSIGNED_NUMBER().GetText();
            if (int.TryParse(numberText, out var parsed))
            {
                primitiveIndex = parsed;
                hasPrimitive = true;
            }
        }

        var body = new SequenceNode(new List<string>(), new List<StatementNode>());
        return new MethodDefinitionNode(
            header.Selector,
            header.Arguments,
            body,
            null,
            string.Empty,
            hasPrimitive,
            primitiveIndex);
    }

    /// <summary>
    /// Helper record returned when visiting a <c>methodHeader</c> rule.
    /// </summary>
    private sealed record MethodHeaderParts(string Selector, List<string> Arguments);

    /// <summary>
    /// Parses the selector and argument names from a method header.
    /// </summary>
    public override object? VisitMethodHeader(Smalltalk80Parser.MethodHeaderContext context)
    {
        if (context.unarySelector() != null)
        {
            return new MethodHeaderParts(context.unarySelector().GetText(), new());
        }

        if (context.binarySelector() != null)
        {
            var arg = context.formalArg(0).GetText();
            return new MethodHeaderParts(context.binarySelector().GetText(), [arg]);
        }

        // Keyword selector case — we now look for IDENTIFIER COLON and formalArg pairs
        var selector = "";
        var args = new List<string>();

        // Count how many keyword parts (should equal number of formalArgs)
        int partCount = context.children.Count / 2; // assuming (IDENTIFIER COLON formalArg)+
                                                    // Keyword selector case — we now look for IDENTIFIER COLON and formalArg pairs
        selector = "";
        args = new List<string>();

        var formals = context.formalArg(); // correctly call method to get FormalArgContext[]
        for (int i = 0; i < context.ChildCount - 1; i++)
        {
            if (context.children[i] is ITerminalNode id && id.Symbol.Type == Smalltalk80Parser.IDENTIFIER &&
                context.children[i + 1] is ITerminalNode colon && colon.Symbol.Type == Smalltalk80Parser.COLON)
            {
                selector += id.GetText() + ":";
                if (formals.Length > args.Count)
                    args.Add(formals[args.Count].GetText());
            }
        }


        return new MethodHeaderParts(selector, args);
    }


    /// <summary>
    /// Visits a sequence of statements. The skeleton currently returns an empty
    /// sequence because expression handling has not been implemented.
    /// </summary>
    public override object? VisitStatements(Smalltalk80Parser.StatementsContext context)
        => new SequenceNode(new List<string>(), new List<StatementNode>());

    /// <summary>
    /// Visits an individual statement. Expressions are not yet supported, so a
    /// placeholder <see cref="ExpressionStatement"/> is returned.
    /// </summary>
    public override object? VisitStatement(Smalltalk80Parser.StatementContext context)
        => new ExpressionStatement(new LiteralExpression(0));

    /// <summary>
    /// Visits a primary variable and returns a <see cref="VariableExpression"/>.
    /// </summary>
    public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
        => new VariableExpression(context.IDENTIFIER().GetText());

    /// <summary>
    /// Visits a numeric literal and converts it to either an integer or floating
    /// point value based on its textual form.
    /// </summary>
    public override object? VisitNumberLiteral(Smalltalk80Parser.NumberLiteralContext context)
    {
        var text = context.UNSIGNED_NUMBER().GetText();
        return int.TryParse(text, out var i)
            ? new LiteralExpression(i)
            : new LiteralExpression(double.Parse(text));
    }

    /// <summary>
    /// Visits the literal <c>true</c> keyword.
    /// </summary>
    public override object? VisitTrueLiteral(Smalltalk80Parser.TrueLiteralContext context)
        => new LiteralExpression(true);

    /// <summary>
    /// Visits the literal <c>false</c> keyword.
    /// </summary>
    public override object? VisitFalseLiteral(Smalltalk80Parser.FalseLiteralContext context)
        => new LiteralExpression(false);

    /// <summary>
    /// Visits the literal <c>nil</c> keyword.
    /// </summary>
    public override object? VisitNilLiteral(Smalltalk80Parser.NilLiteralContext context)
        => new LiteralExpression(null!);

    /// <summary>
    /// Visits a block literal and returns a minimal <see cref="BlockExpression"/>.
    /// Statement parsing is not yet implemented.
    /// </summary>
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
        => new BlockExpression(new List<string>(), new SequenceNode(new List<string>(), new List<StatementNode>()));

    // Helper methods -------------------------------------------------------

    private static string UnquoteString(string text)
    {
        if (text.Length > 1 && text.StartsWith("'", StringComparison.Ordinal) && text.EndsWith("'", StringComparison.Ordinal))
            text = text[1..^1].Replace("''", "'");
        return text;
    }

    private static string UnquoteSymbol(string text)
    {
        if (text.StartsWith("#'", StringComparison.Ordinal))
            return UnquoteString(text[1..]);
        return text.TrimStart('#');
    }
}
