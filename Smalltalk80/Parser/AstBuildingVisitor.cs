
using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
///     ANTLR visitor that constructs an abstract syntax tree from a parsed
///     <see cref="Smalltalk80Parser"/> parse tree.
/// </summary>
public sealed class AstBuildingVisitor : Smalltalk80BaseVisitor<object?>
{
    private readonly CommonTokenStream tokens;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AstBuildingVisitor"/> class.
    /// </summary>
    /// <param name="tokens">The token stream produced by the lexer.</param>
    public AstBuildingVisitor(CommonTokenStream tokens) => this.tokens = tokens;

    /// <summary>
    ///     Visits the root of a file-in operation and returns a <see cref="FileNode"/>
    ///     representing all parsed chunks.
    /// </summary>
    /// <param name="context">The parse context for a file.</param>
    /// <returns>The constructed <see cref="FileNode"/>.</returns>
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
    ///     Converts an expression chunk into its AST representation.
    /// </summary>
    /// <param name="context">The expression chunk context.</param>
    /// <returns>An <see cref="ExpressionChunkNode"/> for the chunk.</returns>
    public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
        => new ExpressionChunkNode((ExpressionNode)Visit(context.expression())!);

    /// <summary>
    ///     Creates a <see cref="ClassDefinitionNode"/> from a class definition chunk.
    /// </summary>
    /// <param name="context">The class definition context.</param>
    /// <returns>The resulting class definition node.</returns>
    public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
    {
        var header = context.classHeader();
        var body = context.classBody();
        var superclass = header.className().GetText();
        var className = UnquoteSymbol(header.symbolConst().GetText());
        var category = body.categoryDecl() is { } cat
            ? UnquoteString(cat.stringConst().GetText())
            : string.Empty;

        return new ClassDefinitionNode(
            superclass,
            className,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            category);
    }

    /// <summary>
    ///     Processes a class comment chunk.
    /// </summary>
    /// <param name="context">The class comment context.</param>
    /// <returns>A <see cref="ClassCommentNode"/> containing the comment.</returns>
    public override object? VisitClassCommentChunk(Smalltalk80Parser.ClassCommentChunkContext context)
    {
        var className = context.className().GetText();
        var comment = UnquoteString(context.stringConst().GetText());
        return new ClassCommentNode(className, comment);
    }

    /// <summary>
    ///     Parses a method category block and its contained methods.
    /// </summary>
    /// <param name="context">The method category context.</param>
    /// <returns>A <see cref="FileNode"/> representing the category header and methods.</returns>
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
    ///     Converts a method definition into a <see cref="MethodDefinitionNode"/>.
    /// </summary>
    /// <param name="context">The method definition context.</param>
    /// <returns>The constructed method definition node, or <c>null</c> if the header is invalid.</returns>
    public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        var header = (MethodHeaderParts?)Visit(context.methodHeader());
        if (header is null)
            return null;

        bool hasPrimitive = false;
        int primitiveIndex = 0;
        if (context.primitiveDecl() is { } primitive)
        {
            var text = primitive.PRIMITIVE_DECL().GetText();
            var numberText = text["<primitive:".Length..^1];
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

    private sealed record MethodHeaderParts(string Selector, List<string> Arguments);

    /// <summary>
    ///     Parses a method header and extracts the selector and argument list.
    /// </summary>
    /// <param name="context">The method header context.</param>
    /// <returns>A <see cref="MethodHeaderParts"/> instance describing the header.</returns>
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

        var selector = "";
        var args = new List<string>();
        var formals = context.formalArg();
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
    ///     Visits a sequence of statements and returns a <see cref="SequenceNode"/> containing them.
    /// </summary>
    /// <param name="context">The statements context.</param>
    /// <returns>A <see cref="SequenceNode"/> of statements.</returns>
    public override object? VisitStatements(Smalltalk80Parser.StatementsContext context)
    {
        var statements = new List<StatementNode>();
        foreach (var stmt in context.statement())
        {
            if (Visit(stmt) is StatementNode node)
                statements.Add(node);
        }

        return new SequenceNode(new List<string>(), statements);
    }

    /// <summary>
    ///     Processes a single statement, which may be an answer expression or a regular expression.
    /// </summary>
    /// <param name="context">The statement context.</param>
    public override object? VisitStatement(Smalltalk80Parser.StatementContext context)
        => context.answer() != null
            ? Visit(context.answer())
            : new ExpressionStatement((ExpressionNode)Visit(context.expression())!);

    /// <summary>
    ///     Processes an ^ answer statement.
    /// </summary>
    /// <param name="context">The answer context.</param>
    /// <returns>An <see cref="AnswerStatement"/> node.</returns>
    public override object? VisitAnswer(Smalltalk80Parser.AnswerContext context)
        => new AnswerStatement((ExpressionNode)Visit(context.returnExpr())!);

    /// <summary>
    ///     Parses a return expression that may contain cascaded messages.
    /// </summary>
    /// <param name="context">The return expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitReturnExpr(Smalltalk80Parser.ReturnExprContext context)
    {
        var expr = (ExpressionNode)Visit(context.expression())!;
        if (context.cascadeMessage().Length == 0)
            return expr;

        var messages = new List<ExpressionNode>();
        foreach (var cascade in context.cascadeMessage())
        {
            foreach (var msg in cascade.expression())
                messages.Add(ApplyMessage(this, expr, msg));
        }

        return new CascadeExpression(expr, messages);
    }

    /// <summary>
    ///     Visits an assignment expression.
    /// </summary>
    /// <param name="context">The assignment expression context.</param>
    /// <returns>The resulting <see cref="AssignmentExpression"/> node.</returns>
    public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
    {
        var variable = context.assignable().variable().IDENTIFIER().GetText();
        var value = (ExpressionNode)Visit(context.expression())!;
        return new AssignmentExpression(variable, value);
    }

    /// <summary>
    ///     Evaluates a message chain starting with a primary expression.
    /// </summary>
    /// <param name="context">The message chain context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/> after applying all messages.</returns>
    public override object? VisitMessageChain(Smalltalk80Parser.MessageChainContext context)
    {
        var expr = (ExpressionNode)Visit(context.primary())!;
        foreach (var part in context.messagePart())
        {
            expr = ApplyMessagePart(this, expr, part);
        }

        return expr;
    }

    /// <summary>
    ///     Applies an individual message part to the given receiver.
    /// </summary>
    /// <param name="visitor">The active AST visitor.</param>
    /// <param name="receiver">The receiver expression.</param>
    /// <param name="part">The parsed message part.</param>
    /// <returns>The expression resulting from applying the message.</returns>
    private static ExpressionNode ApplyMessagePart(AstBuildingVisitor visitor, ExpressionNode receiver, Smalltalk80Parser.MessagePartContext part)
    {
        if (part.unaryMessage() != null)
        {
            var selector = part.unaryMessage().unarySelector().GetText();
            return new UnarySendExpression(receiver, selector);
        }

        if (part.binaryMessage() != null)
        {
            var selector = part.binaryMessage().binarySelector().GetText();
            var arg = (ExpressionNode)visitor.Visit(part.binaryMessage().messageChain())!;
            return new BinarySendExpression(receiver, selector, arg);
        }

        if (part.keywordMessage() != null)
        {
            var parts = new List<string>();
            var args = new List<ExpressionNode>();
            foreach (var kw in part.keywordMessage().keywordPart())
            {
                parts.Add(kw.IDENTIFIER().GetText() + ":");
                args.Add((ExpressionNode)visitor.Visit(kw.messageChain())!);
            }

            return new KeywordSendExpression(receiver, parts, args);
        }

        throw new NotSupportedException("Unknown message part type.");
    }

    /// <summary>
    ///     Visits a primary expression such as a literal, variable, block or parenthesized expression.
    /// </summary>
    /// <param name="context">The primary expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/> or <c>null</c> when the context is empty.</returns>
    public override object? VisitPrimary(Smalltalk80Parser.PrimaryContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());
        if (context.variable() != null)
            return Visit(context.variable());
        if (context.block() != null)
            return Visit(context.block());
        if (context.expression() != null)
            return Visit(context.expression());
        return null;
    }

    /// <summary>
    ///     Applies a cascaded message to a receiver.
    /// </summary>
    /// <param name="visitor">The active visitor.</param>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="message">The message expression.</param>
    /// <returns>The resulting expression after applying the message.</returns>
    private static ExpressionNode ApplyMessage(AstBuildingVisitor visitor, ExpressionNode receiver, Smalltalk80Parser.ExpressionContext message)
    {
        var node = (ExpressionNode)visitor.Visit(message)!;

        return node switch
        {
            UnarySendExpression u => new UnarySendExpression(receiver, u.Selector),
            BinarySendExpression b => new BinarySendExpression(receiver, b.Selector, b.Argument),
            KeywordSendExpression k => new KeywordSendExpression(receiver, k.SelectorParts, k.Arguments),
            _ => throw new NotSupportedException("Unknown cascade message type.")
        };
    }

    /// <summary>
    ///     Visits a variable reference.
    /// </summary>
    /// <param name="context">The variable context.</param>
    /// <returns>A <see cref="VariableExpression"/> node.</returns>
    public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
        => new VariableExpression(context.IDENTIFIER()?.GetText() ?? context.GetText());

    /// <summary>
    ///     Parses a numeric literal and converts it to either an <see cref="int"/> or <see cref="double"/> literal expression.
    /// </summary>
    /// <param name="context">The numeric literal context.</param>
    /// <returns>The resulting <see cref="LiteralExpression"/>.</returns>
    public override object? VisitNumberLiteral(Smalltalk80Parser.NumberLiteralContext context)
    {
        var text = context.NUMBER().GetText();
        return int.TryParse(text, out var i)
            ? new LiteralExpression(i)
            : new LiteralExpression(double.Parse(text));
    }

    /// <summary>
    ///     Visits a single quoted string constant.
    /// </summary>
    /// <param name="context">The string constant context.</param>
    /// <returns>A <see cref="LiteralExpression"/> containing the unescaped text.</returns>
    public override object? VisitStringConst(Smalltalk80Parser.StringConstContext context)
        => new LiteralExpression(UnquoteString(context.STRING().GetText()));

    /// <summary>
    ///     Visits a character literal (e.g., $a).
    /// </summary>
    /// <param name="context">The character literal context.</param>
    /// <returns>A <see cref="LiteralExpression"/> with the character value.</returns>
    public override object? VisitCharacterLiteral(Smalltalk80Parser.CharacterLiteralContext context)
        => new LiteralExpression(context.CHAR_LITERAL().GetText()[1]);

    /// <summary>
    ///     Processes a symbol constant.
    /// </summary>
    /// <param name="context">The symbol constant context.</param>
    /// <returns>A <see cref="LiteralExpression"/> containing the symbol.</returns>
    public override object? VisitSymbolConst(Smalltalk80Parser.SymbolConstContext context)
        => new LiteralExpression(new Symbol(UnquoteSymbol(context.GetText())));

    /// <summary>
    ///     Visits a dynamic array literal.
    /// </summary>
    /// <param name="context">The array literal context.</param>
    /// <returns>A <see cref="DynamicArrayExpression"/> with its elements.</returns>
    public override object? VisitArrayLiteral(Smalltalk80Parser.ArrayLiteralContext context)
    {
        var elements = new List<ExpressionNode>();
        foreach (var element in context.arrayElement())
        {
            if (element.literal() != null)
                elements.Add((ExpressionNode)Visit(element.literal())!);
            else if (element.symbolName() != null)
                elements.Add(new LiteralExpression(new Symbol(element.symbolName().GetText())));
        }

        return new DynamicArrayExpression(elements);
    }

    /// <summary>
    ///     Visits a byte array literal and converts it into a dynamic array of byte values.
    /// </summary>
    /// <param name="context">The byte array literal context.</param>
    /// <returns>A <see cref="DynamicArrayExpression"/> representing the array.</returns>
    public override object? VisitByteArrayLiteral(Smalltalk80Parser.ByteArrayLiteralContext context)
    {
        var bytes = context.NUMBER().Select(t => byte.Parse(t.GetText())).ToArray();
        var elements = bytes.Select(b => (ExpressionNode)new LiteralExpression(b)).ToList();
        return new DynamicArrayExpression(elements);
    }

    /// <summary>
    ///     Dispatches to the specific literal type contained in the context.
    /// </summary>
    /// <param name="context">The literal context.</param>
    /// <returns>The resulting literal expression.</returns>
    public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.TRUE() != null)
            return new LiteralExpression(true);
        if (context.FALSE() != null)
            return new LiteralExpression(false);
        if (context.NIL() != null)
            return new LiteralExpression(null!);
        if (context.stringConst() != null)
            return Visit(context.stringConst());
        if (context.numberLiteral() != null)
            return Visit(context.numberLiteral());
        if (context.symbolConst() != null)
            return Visit(context.symbolConst());
        if (context.arrayLiteral() != null)
            return Visit(context.arrayLiteral());
        if (context.byteArrayLiteral() != null)
            return Visit(context.byteArrayLiteral());
        if (context.characterLiteral() != null)
            return Visit(context.characterLiteral());

        return null!;
    }

    /// <summary>
    ///     Visits a block literal without parameters or statements.
    /// </summary>
    /// <param name="context">The block context.</param>
    /// <returns>A minimal <see cref="BlockExpression"/>.</returns>
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
        => new BlockExpression(new List<string>(), new SequenceNode(new List<string>(), new List<StatementNode>()));

    /// <summary>
    ///     Removes surrounding single quotes and unescapes doubled quotes.
    /// </summary>
    /// <param name="text">The quoted text.</param>
    /// <returns>The unquoted string.</returns>
    private static string UnquoteString(string text)
    {
        if (text.Length > 1 && text.StartsWith("'", StringComparison.Ordinal) && text.EndsWith("'", StringComparison.Ordinal))
            text = text[1..^1].Replace("''", "'");
        return text;
    }

    /// <summary>
    ///     Removes the leading <c>#</c> and unquotes symbol text if necessary.
    /// </summary>
    /// <param name="text">The symbol token text.</param>
    /// <returns>The symbol name.</returns>
    private static string UnquoteSymbol(string text)
    {
        if (text.StartsWith("#'", StringComparison.Ordinal))
            return UnquoteString(text[1..]);
        return text.TrimStart('#');
    }
}
