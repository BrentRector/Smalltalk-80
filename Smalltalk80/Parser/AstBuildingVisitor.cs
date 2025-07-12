
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
    ///     Parses a single expression entry such as might be entered in a REPL.
    /// </summary>
    /// <param name="context">The expression entry context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitExpressionEntry(Smalltalk80Parser.ExpressionEntryContext context)
        => Visit(context.expression());

    /// <summary>
    ///     Parses a workspace statement sequence.
    /// </summary>
    /// <param name="context">The statements entry context.</param>
    /// <returns>A <see cref="SequenceNode"/> representing the statements.</returns>
    public override object? VisitStatementsEntry(Smalltalk80Parser.StatementsEntryContext context)
        => Visit(context.statements());

    /// <summary>
    ///     Parses a single method definition without trailing bang terminator.
    /// </summary>
    /// <param name="context">The method entry context.</param>
    /// <returns>The parsed <see cref="MethodDefinitionNode"/>.</returns>
    public override object? VisitMethodEntry(Smalltalk80Parser.MethodEntryContext context)
    {
        var header = (MethodHeaderParts?)Visit(context.methodHeader());
        if (header is null)
            return null;

        var temps = context.temporaries() is { } t
            ? ((List<string>)Visit(t)!)
            : new List<string>();

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

        var body = (SequenceNode)Visit(context.methodBodyNoBang())!;
        body = new SequenceNode(temps, body.Statements);

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
    ///     Parses a class definition entry.
    /// </summary>
    /// <param name="context">The class entry context.</param>
    /// <returns>The resulting <see cref="ClassDefinitionNode"/>.</returns>
    public override object? VisitClassEntry(Smalltalk80Parser.ClassEntryContext context)
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
        var isClassSide = context.children
            .OfType<ITerminalNode>()
            .Any(t => t.Symbol.Type == Smalltalk80Parser.T__10);
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

        var comment = context.stringConst() is { } c ? UnquoteString(c.GetText()) : null;

        var temps = context.temporaries() is { } t
            ? ((List<string>)Visit(t)!)
            : new List<string>();

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

        var body = (SequenceNode)Visit(context.methodBody())!;
        body = new SequenceNode(temps, body.Statements);

        return new MethodDefinitionNode(
            header.Selector,
            header.Arguments,
            body,
            comment,
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
    ///     Visits a list of temporary variable declarations.
    /// </summary>
    /// <param name="context">The temporaries context.</param>
    /// <returns>A list of declared temporary variable names.</returns>
    public override object? VisitTemporaries(Smalltalk80Parser.TemporariesContext context)
        => context.IDENTIFIER().Select(i => i.GetText()).ToList();

    /// <summary>
    ///     Visits a method body returning the contained statement sequence.
    /// </summary>
    /// <param name="context">The method body context.</param>
    /// <returns>The parsed <see cref="SequenceNode"/>.</returns>
    public override object? VisitMethodBody(Smalltalk80Parser.MethodBodyContext context)
    {
        if (context.statements() != null)
            return Visit(context.statements());

        if (context.returnExpr() != null)
        {
            var expr = (ExpressionNode)Visit(context.returnExpr())!;
            var stmt = new AnswerStatement(expr);
            return new SequenceNode(new List<string>(), new List<StatementNode> { stmt });
        }

        return new SequenceNode(new List<string>(), new List<StatementNode>());
    }

    /// <summary>
    ///     Visits a REPL-compatible method body that omits the terminating bang.
    /// </summary>
    /// <param name="context">The method body context.</param>
    /// <returns>The parsed <see cref="SequenceNode"/>.</returns>
    public override object? VisitMethodBodyNoBang(Smalltalk80Parser.MethodBodyNoBangContext context)
    {
        if (context.statements() != null)
            return Visit(context.statements());

        if (context.returnExpr() != null)
        {
            var expr = (ExpressionNode)Visit(context.returnExpr())!;
            var stmt = new AnswerStatement(expr);
            return new SequenceNode(new List<string>(), new List<StatementNode> { stmt });
        }

        return new SequenceNode(new List<string>(), new List<StatementNode>());
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
        var value = (ExpressionNode)Visit(context.expression())!;
        if (context.cascadeMessage().Length > 0)
        {
            var messages = new List<ExpressionNode>();
            foreach (var cascade in context.cascadeMessage())
                foreach (var msg in cascade.expression())
                    messages.Add(ApplyMessage(this, value, msg));

            value = new CascadeExpression(value, messages);
        }

        return new ReturnExpression(value);
    }

    /// <summary>
    ///     Visits an expression and applies operator precedence rules.
    /// </summary>
    /// <param name="context">The expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context)
        => context.messageExpr() != null
            ? Visit(context.messageExpr())
            : context.assignmentExpr() != null
                ? Visit(context.assignmentExpr())
                : Visit(context.lowPrecedenceExpr());

    /// <summary>
    ///     Visits a low precedence expression handling addition and subtraction operators.
    /// </summary>
    /// <param name="context">The low precedence expression context.</param>
    /// <returns>The evaluated <see cref="ExpressionNode"/>.</returns>
    public override object? VisitLowPrecedenceExpr(Smalltalk80Parser.LowPrecedenceExprContext context)
    {
        var expr = (ExpressionNode)Visit(context.mediumPrecedenceExpr(0))!;
        for (int i = 1; i < context.ChildCount; i += 2)
        {
            if (context.children[i] is ITerminalNode op &&
                context.children[i + 1] is Smalltalk80Parser.MediumPrecedenceExprContext rightCtx)
            {
                var right = (ExpressionNode)Visit(rightCtx)!;
                expr = new BinarySendExpression(expr, op.GetText(), right);
            }
        }

        return expr;
    }

    /// <summary>
    ///     Visits an assignment expression of the form <c>variable := value</c>.
    /// </summary>
    /// <param name="context">The assignment expression context.</param>
    /// <returns>An <see cref="AssignmentExpression"/> representing the assignment.</returns>
    public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
    {
        var variableName = context.lowPrecedenceExpr().GetText();
        var value = (ExpressionNode)Visit(context.expression())!;
        return new AssignmentExpression(variableName, value);
    }

    /// <summary>
    ///     Visits a medium precedence expression handling multiplication, division and modulo operators.
    /// </summary>
    /// <param name="context">The medium precedence expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitMediumPrecedenceExpr(Smalltalk80Parser.MediumPrecedenceExprContext context)
    {
        var expr = (ExpressionNode)Visit(context.highPrecedenceExpr(0))!;
        for (int i = 1; i < context.ChildCount; i += 2)
        {
            if (context.children[i] is ITerminalNode op &&
                context.children[i + 1] is Smalltalk80Parser.HighPrecedenceExprContext rightCtx)
            {
                var right = (ExpressionNode)Visit(rightCtx)!;
                expr = new BinarySendExpression(expr, op.GetText(), right);
            }
        }

        return expr;
    }

    /// <summary>
    ///     Visits a high precedence expression, which may be a unary message or a primary expression.
    /// </summary>
    /// <param name="context">The high precedence context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitHighPrecedenceExpr(Smalltalk80Parser.HighPrecedenceExprContext context)
        => context.unaryMessageExpr() != null
            ? Visit(context.unaryMessageExpr())
            : Visit(context.primaryExpr());

    /// <summary>
    ///     Visits a unary message expression.
    /// </summary>
    /// <param name="context">The unary message expression context.</param>
    /// <returns>The resulting <see cref="UnarySendExpression"/>.</returns>
    public override object? VisitUnaryMessageExpr(Smalltalk80Parser.UnaryMessageExprContext context)
    {
        var receiver = (ExpressionNode)Visit(context.primaryExpr())!;
        var selector = context.unarySelector().GetText();
        return new UnarySendExpression(receiver, selector);
    }

    /// <summary>
    ///     Visits a message expression consisting of a primary receiver and zero or more message parts.
    /// </summary>
    /// <param name="context">The message expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/>.</returns>
    public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
    {
        var expr = (ExpressionNode)Visit(context.primaryExpr())!;
        int index = 0;
        for (int i = 1; i < context.ChildCount;)
        {
            switch (context.children[i])
            {
                case Smalltalk80Parser.BinarySelectorContext binSel when context.children[i + 1] is Smalltalk80Parser.ExpressionContext rhsCtx:
                    var rhs = (ExpressionNode)Visit(rhsCtx)!;
                    expr = new BinarySendExpression(expr, binSel.GetText(), rhs);
                    i += 2;
                    index++;
                    break;

                case Smalltalk80Parser.KeywordMessageContext kwMsg when context.children[i + 1] is Smalltalk80Parser.ExpressionContext nextCtx:
                    var parts = kwMsg.keywordPart().Select(k => k.IDENTIFIER().GetText() + ":").ToList();
                    var args = kwMsg.keywordPart().Select(k => (ExpressionNode)Visit(k.expression())!).ToList();
                    expr = new KeywordSendExpression(expr, parts, args);
                    expr = (ExpressionNode)Visit(nextCtx)!;
                    i += 2;
                    index++;
                    break;

                default:
                    i++;
                    break;
            }
        }

        return expr;
    }

    /// <summary>
    ///     Visits a primary expression such as a literal, variable, block or parenthesized expression.
    /// </summary>
    /// <param name="context">The primary expression context.</param>
    /// <returns>The resulting <see cref="ExpressionNode"/> or <c>null</c> when the context is empty.</returns>
    public override object? VisitPrimaryExpr(Smalltalk80Parser.PrimaryExprContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());
        if (context.variable() != null)
            return Visit(context.variable());
        if (context.expression() != null)
            return Visit(context.expression());
        return null;
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
