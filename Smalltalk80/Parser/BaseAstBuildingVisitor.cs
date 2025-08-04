using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Base visitor class for AST building with common functionality.
/// </summary>
public abstract class BaseAstBuildingVisitor : Smalltalk80BaseVisitor<object?>
{
    protected readonly CommonTokenStream? tokens;

    protected BaseAstBuildingVisitor(CommonTokenStream? tokens = null)
    {
        this.tokens = tokens;
    }

    /// <summary>
    /// Visit a variable context and create a VariableExpression.
    /// </summary>
    public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
    {
        return new VariableExpression(context.GetText());
    }

    /// <summary>
    /// Visit a literal context and create appropriate literal expression.
    /// </summary>
    public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.NUMBER() != null)
        {
            var text = context.NUMBER().GetText();
            if (text.Contains('.'))
                return new FloatLiteralExpression(double.Parse(text));
            else
                return new IntegerLiteralExpression(int.Parse(text));
        }

        if (context.STRING() != null)
        {
            var text = context.STRING().GetText();
            // Remove quotes and handle escape sequences
            var value = text[1..^1].Replace("''", "'");
            return new StringLiteralExpression(value);
        }

        if (context.CHARACTER() != null)
        {
            var text = context.CHARACTER().GetText();
            return new CharacterLiteralExpression(text[1]); // Skip the $
        }

        if (context.SYMBOL() != null)
        {
            var text = context.SYMBOL().GetText();
            return new SymbolLiteralExpression(text[1..]); // Skip the #
        }

        if (context.arrayLiteral() != null)
        {
            return Visit(context.arrayLiteral());
        }

        throw new InvalidOperationException("Unknown literal type");
    }

    /// <summary>
    /// Visit an array literal context.
    /// </summary>
    public override object? VisitArrayLiteral(Smalltalk80Parser.ArrayLiteralContext context)
    {
        var elements = new List<ExpressionNode>();

        if (context.arrayElement() != null)
        {
            foreach (var element in context.arrayElement())
            {
                elements.Add((ExpressionNode)Visit(element)!);
            }
        }

        return new ArrayLiteralExpression(elements);
    }

    /// <summary>
    /// Visit an array element context.
    /// </summary>
    public override object? VisitArrayElement(Smalltalk80Parser.ArrayElementContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());

        if (context.IDENTIFIER() != null)
            return new SymbolLiteralExpression(context.IDENTIFIER().GetText());

        return Visit(context.arrayLiteral());
    }

    /// <summary>
    /// Visit a block expression context.
    /// </summary>
    public override object? VisitBlockExpr(Smalltalk80Parser.BlockExprContext context)
    {
        var parameters = new List<string>();

        if (context.blockParameters() != null)
        {
            foreach (var param in context.blockParameters().IDENTIFIER())
            {
                parameters.Add(param.GetText());
            }
        }

        var statements = new List<StatementNode>();

        if (context.statements() != null)
        {
            var stmtList = (List<StatementNode>)Visit(context.statements())!;
            statements.AddRange(stmtList);
        }

        var sequence = new SequenceNode(new List<string>(), statements);
        return new BlockExpression(parameters, sequence);
    }

    /// <summary>
    /// Visit statements context and return list of statements.
    /// </summary>
    public override object? VisitStatements(Smalltalk80Parser.StatementsContext context)
    {
        var statements = new List<StatementNode>();

        foreach (var stmt in context.statement())
        {
            var statementNode = (StatementNode)Visit(stmt)!;
            statements.Add(statementNode);
        }

        return statements;
    }

    /// <summary>
    /// Visit a statement context.
    /// </summary>
    public override object? VisitStatement(Smalltalk80Parser.StatementContext context)
    {
        if (context.returnStatement() != null)
            return Visit(context.returnStatement());

        if (context.expression() != null)
        {
            var expr = (ExpressionNode)Visit(context.expression())!;
            return new ExpressionStatement(expr);
        }

        throw new InvalidOperationException("Unknown statement type");
    }

    /// <summary>
    /// Visit a return statement context.
    /// </summary>
    public override object? VisitReturnStatement(Smalltalk80Parser.ReturnStatementContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression())!;
        return new AnswerStatement(expression);
    }

    /// <summary>
    /// Visit an expression context.
    /// </summary>
    public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context)
    {
        return Visit(context.assignmentExpr());
    }

    /// <summary>
    /// Visit an assignment expression context.
    /// </summary>
    public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
    {
        if (context.variable() != null && context.ASSIGN() != null)
        {
            var variable = context.variable().GetText();
            var value = (ExpressionNode)Visit(context.assignmentExpr())!;
            return new AssignmentExpression(variable, value);
        }
        else
        {
            return Visit(context.cascadeExpr());
        }
    }
}

/// <summary>
/// Message node base class for cascade expressions.
/// </summary>
public abstract class MessageNode
{
    public abstract string Selector { get; }
}

/// <summary>
/// Unary message node for cascade expressions.
/// </summary>
public class UnaryMessageNode : MessageNode
{
    public override string Selector { get; }

    public UnaryMessageNode(string selector)
    {
        Selector = selector;
    }
}

/// <summary>
/// Binary message node for cascade expressions.
/// </summary>
public class BinaryMessageNode : MessageNode
{
    public override string Selector { get; }
    public ExpressionNode Argument { get; }

    public BinaryMessageNode(string selector, ExpressionNode argument)
    {
        Selector = selector;
        Argument = argument;
    }
}

/// <summary>
/// Keyword message node for cascade expressions.
/// </summary>
public class KeywordMessageNode : MessageNode
{
    public List<string> Keywords { get; }
    public List<ExpressionNode> Arguments { get; }
    public override string Selector => string.Join("", Keywords);

    public KeywordMessageNode(List<string> keywords, List<ExpressionNode> arguments)
    {
        Keywords = keywords;
        Arguments = arguments;
    }
}
