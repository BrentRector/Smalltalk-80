using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Visitor that constructs an abstract syntax tree (AST) from the parse tree
/// produced by <see cref="Smalltalk80Parser"/>.
/// </summary>
public sealed class AstBuildingVisitor : Smalltalk80BaseVisitor<AstNode>
{
    /// <summary>
    /// Tracks the current scope depth while visiting sequences.
    /// </summary>
    private int scopeDepth;
    /// <inheritdoc/>
    public override AstNode VisitScript(Smalltalk80Parser.ScriptContext context)
        => new ScriptNode((SequenceNode)Visit(context.sequence())!);

    /// <inheritdoc/>
    public override AstNode VisitSequence(Smalltalk80Parser.SequenceContext context)
    {
        Console.WriteLine($"[DEBUG] Push scope: {++scopeDepth}");

        var temps = new List<string>();
        if (context.temps() is not null)
        {
            temps.AddRange(
                context.temps()
                    .IDENTIFIER()
                    .Select(id => id.GetText().Trim()));
            Console.WriteLine($"[DEBUG] Temps: {string.Join(", ", temps)}");
        }

        var statements = new List<StatementNode>();
        if (context.statements() is { } stmts)
        {
            if (Visit(stmts) is StatementsNode sn)
                statements.AddRange(sn.Statements);
        }

        var node = new SequenceNode(temps, statements);
        Console.WriteLine($"[DEBUG] Pop scope: {scopeDepth--}");
        return node;
    }

    /// <inheritdoc/>
    public override AstNode VisitStatementAnswer(Smalltalk80Parser.StatementAnswerContext context)
    {
        var expr = (ExpressionNode)Visit(context.answer().expression())!;
        return new StatementsNode(new[] { new AnswerStatement(expr) });
    }

    /// <inheritdoc/>
    public override AstNode VisitStatementExpressions(Smalltalk80Parser.StatementExpressionsContext context)
    {
        var exprs = (ExpressionsNode)Visit(context.expressions())!;
        var list = exprs.Expressions.Select(e => (StatementNode)new ExpressionStatement(e)).ToList();
        return new StatementsNode(list);
    }

    /// <inheritdoc/>
    public override AstNode VisitStatementExpressionsAnswer(Smalltalk80Parser.StatementExpressionsAnswerContext context)
    {
        var exprs = (ExpressionsNode)Visit(context.expressions())!;
        var statements = exprs.Expressions.Select(e => (StatementNode)new ExpressionStatement(e)).ToList();
        var answerExpr = (ExpressionNode)Visit(context.answer().expression())!;
        statements.Add(new AnswerStatement(answerExpr));
        return new StatementsNode(statements);
    }

    /// <inheritdoc/>
    public override AstNode VisitExpressions(Smalltalk80Parser.ExpressionsContext context)
    {
        var expressions = new List<ExpressionNode> { (ExpressionNode)Visit(context.expression())! };
        foreach (var list in context.expressionList())
        {
            expressions.Add((ExpressionNode)Visit(list.expression())!);
        }
        return new ExpressionsNode(expressions);
    }

    /// <inheritdoc/>
    public override AstNode VisitExpressionList(Smalltalk80Parser.ExpressionListContext context)
        => Visit(context.expression());

    /// <inheritdoc/>
    public override AstNode VisitExpression(Smalltalk80Parser.ExpressionContext context)
    {
        if (context.assignment() is not null) return Visit(context.assignment());
        if (context.cascade() is not null) return Visit(context.cascade());
        if (context.keywordSend() is not null) return Visit(context.keywordSend());
        if (context.binarySend() is not null) return Visit(context.binarySend());
        if (context.primitive() is not null) return Visit(context.primitive());
        return base.VisitExpression(context);
    }

    /// <inheritdoc/>
    public override AstNode VisitAssignment(Smalltalk80Parser.AssignmentContext context)
    {
        var variable = context.variable().GetText();
        var value = (ExpressionNode)Visit(context.expression())!;
        return new AssignmentExpression(variable, value);
    }

    /// <inheritdoc/>
    public override AstNode VisitVariable(Smalltalk80Parser.VariableContext context)
        => new VariableExpression(context.IDENTIFIER().GetText());

    /// <inheritdoc/>
    public override AstNode VisitBinarySend(Smalltalk80Parser.BinarySendContext context)
    {
        var left = (ExpressionNode)Visit(context.unarySend())!;
        return ProcessBinaryTail(left, context.binaryTail());
    }

        private ExpressionNode ProcessBinaryTail(ExpressionNode left, Smalltalk80Parser.BinaryTailContext? tail)
        {
            if (tail is null) return left;
            var msg = tail.binaryMessage();
            if (msg is null) return left;
            var selector = msg.BINARY_SELECTOR()?.GetText() ?? string.Empty;
            ExpressionNode right;
            if (msg.unarySend() is { } us)
                right = (ExpressionNode)Visit(us)!;
            else if (msg.operand() is { } op)
                right = (ExpressionNode)Visit(op)!;
            else
                return left;
            var expr = new BinarySendExpression(left, selector, right);
            return ProcessBinaryTail(expr, tail.binaryTail());
        }

    /// <inheritdoc/>
    public override AstNode VisitUnarySend(Smalltalk80Parser.UnarySendContext context)
    {
        var receiver = (ExpressionNode)Visit(context.operand())!;
        if (context.unaryTail() is { } tail)
            return ProcessUnaryTail(receiver, tail);
        return receiver;
    }

    private ExpressionNode ProcessUnaryTail(ExpressionNode receiver, Smalltalk80Parser.UnaryTailContext tail)
    {
        var selector = tail.unaryMessage().unarySelector().GetText();
        var expr = new UnarySendExpression(receiver, selector);
        return tail.unaryTail() is { } next ? ProcessUnaryTail(expr, next) : expr;
    }

    /// <inheritdoc/>
    public override AstNode VisitKeywordSend(Smalltalk80Parser.KeywordSendContext context)
    {
        var recv = (ExpressionNode)Visit(context.binarySend())!;
        var parts = new List<string>();
        var args = new List<ExpressionNode>();
        foreach (var pair in context.keywordMessage().keywordPair())
        {
            parts.Add(pair.KEYWORD().GetText());
            args.Add((ExpressionNode)Visit(pair.binarySend())!);
        }
        return new KeywordSendExpression(recv, parts, args);
    }

    /// <inheritdoc/>
    public override AstNode VisitCascade(Smalltalk80Parser.CascadeContext context)
    {
        ExpressionNode receiver = context.keywordSend() is { } ks
            ? (ExpressionNode)Visit(ks)!
            : (ExpressionNode)Visit(context.binarySend())!;
        var messages = new List<ExpressionNode>();
        foreach (var msg in context.message())
            messages.Add((ExpressionNode)Visit(msg)!);
        return new CascadeExpression(receiver, messages);
    }

    /// <inheritdoc/>
    public override AstNode VisitOperand(Smalltalk80Parser.OperandContext context)
    {
        if (context.literal() is not null) return Visit(context.literal());
        if (context.reference() is not null) return Visit(context.reference());
        return Visit(context.subexpression());
    }

    /// <inheritdoc/>
    public override AstNode VisitSubexpression(Smalltalk80Parser.SubexpressionContext context)
        => Visit(context.expression());

    /// <inheritdoc/>
    public override AstNode VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.runtimeLiteral() is not null) return Visit(context.runtimeLiteral());
        return Visit(context.parsetimeLiteral());
    }

    /// <inheritdoc/>
    public override AstNode VisitParsetimeLiteral(Smalltalk80Parser.ParsetimeLiteralContext context)
    {
        if (context.number() is not null) return Visit(context.number());
        if (context.stringLiteral() is not null) return new LiteralExpression(context.stringLiteral().GetText().Trim('\''));
        if (context.symbol() is not null) return new LiteralExpression(context.symbol().GetText());
        if (context.charConstant() is not null) return new LiteralExpression(context.charConstant().GetText());
        if (context.pseudoVariable() is { } pv)
            return new PseudoVariableExpression(pv.GetText());
        return base.VisitParsetimeLiteral(context);
    }

    /// <inheritdoc/>
    public override AstNode VisitNumber(Smalltalk80Parser.NumberContext context)
        => Visit(context.stInteger());

    /// <inheritdoc/>
    public override AstNode VisitStInteger(Smalltalk80Parser.StIntegerContext context)
    {
        var value = int.Parse(context.GetText());
        return new LiteralExpression(value);
    }

    /// <inheritdoc/>
    public override AstNode VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        var chunks = new List<AstNode>();
        foreach (var chunk in context.chunk())
        {
            var node = Visit(chunk);
            if (node is not null)
                chunks.Add(node);
        }

        return new FileNode(chunks);
    }

    /// <inheritdoc/>
    public override AstNode VisitChunk(Smalltalk80Parser.ChunkContext context)
    {
        if (context.classDefinition() is { } cls)
            return Visit(cls);

        if (context.methodCategoryHeader() is { } header)
            return Visit(header);

        if (context.classComment() is { } cmt)
            return Visit(cmt);

        if (context.sequence() is { } seq)
            return new ScriptNode((SequenceNode)Visit(seq)!);

        return new ScriptNode(new SequenceNode(Array.Empty<string>(), Array.Empty<StatementNode>()));
    }

    /// <inheritdoc/>
    public override AstNode VisitClassDefinition(Smalltalk80Parser.ClassDefinitionContext context)
    {
        var superclass = context.IDENTIFIER(0).GetText();
        var className = context.IDENTIFIER(1).GetText();
        var options = new Dictionary<string, string>();
        foreach (var opt in context.classOption())
        {
            var optionText = opt.GetText();
            var parts = optionText.Split("'", 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim().TrimEnd(':');
                var value = parts[1].TrimEnd('\'');
                options[key] = value;
            }
        }

        return new ClassDefinitionNode(superclass, className, options);
    }

    /// <inheritdoc/>
    public override AstNode VisitMethodCategoryHeader(Smalltalk80Parser.MethodCategoryHeaderContext context)
    {
        var className = context.IDENTIFIER().GetText();
        var isClassSide = context.GetToken(Smalltalk80Parser.T__4, 0) is not null;
        var category = context.stringLiteral().GetText().Trim('\'');
        return new MethodHeaderNode(className, isClassSide, category);
    }

    /// <inheritdoc/>
    public override AstNode VisitClassComment(Smalltalk80Parser.ClassCommentContext context)
    {
        var className = context.className().GetText();
        var comment = context.stringLiteral().GetText().Trim('\'');
        return new ClassCommentNode(className, comment);
    }

    /// <inheritdoc/>
    public override AstNode VisitMethodDefinition(Smalltalk80Parser.MethodDefinitionContext context)
    {
        Console.WriteLine("[DEBUG] Entering method definition");
        var body = (SequenceNode)Visit(context.sequence())!;
        Console.WriteLine("[DEBUG] Exiting method definition");
        return new MethodDefinitionNode(string.Empty, body, null);
    }
}

/// <summary>
/// Node representing a list of expressions.
/// </summary>
/// <param name="Expressions">The expressions in evaluation order.</param>
public sealed record ExpressionsNode(IReadOnlyList<ExpressionNode> Expressions) : AstNode;

/// <summary>
/// Node representing a collection of statements.
/// </summary>
/// <param name="Statements">The statements in execution order.</param>
public sealed record StatementsNode(IReadOnlyList<StatementNode> Statements) : AstNode;

