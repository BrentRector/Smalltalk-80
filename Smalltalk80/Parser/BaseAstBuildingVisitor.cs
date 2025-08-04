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
    /// <summary>
    /// The token stream used for retrieving additional token information during AST construction.
    /// This stream provides access to whitespace, comments, and detailed token positions
    /// that may be needed for advanced parsing scenarios or error reporting.
    /// May be null if detailed token information is not required.
    /// </summary>
    protected readonly CommonTokenStream? tokens;

    /// <summary>
    /// Initializes a new instance of the BaseAstBuildingVisitor class with an optional token stream.
    /// The token stream can be used to access detailed token information during AST construction.
    /// </summary>
    /// <param name="tokens">The token stream to use for detailed token access, or null if not needed.</param>
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
        if (context.numberLiteral() != null)
        {
            return Visit(context.numberLiteral());
        }

        if (context.stringConst() != null)
        {
            return Visit(context.stringConst());
        }

        if (context.characterLiteral() != null)
        {
            return Visit(context.characterLiteral());
        }

        if (context.symbolConst() != null)
        {
            return Visit(context.symbolConst());
        }

        if (context.arrayLiteral() != null)
        {
            return Visit(context.arrayLiteral());
        }

        if (context.byteArrayLiteral() != null)
        {
            return Visit(context.byteArrayLiteral());
        }

        throw new InvalidOperationException("Unknown literal type");
    }

    /// <summary>
    /// Visit a number literal context.
    /// </summary>
    public override object? VisitNumberLiteral(Smalltalk80Parser.NumberLiteralContext context)
    {
        var text = context.NUMBER().GetText();
        if (text.Contains('.'))
            return new LiteralExpression(double.Parse(text));
        else
            return new LiteralExpression(int.Parse(text));
    }

    /// <summary>
    /// Visit a string constant context.
    /// </summary>
    public override object? VisitStringConst(Smalltalk80Parser.StringConstContext context)
    {
        var text = context.STRING().GetText();
        // Remove quotes and handle escape sequences
        var value = text[1..^1].Replace("''", "'");
        return new LiteralExpression(value);
    }

    /// <summary>
    /// Visit a character literal context.
    /// </summary>
    public override object? VisitCharacterLiteral(Smalltalk80Parser.CharacterLiteralContext context)
    {
        var text = context.CHAR_LITERAL().GetText();
        return new LiteralExpression(text[1]); // Skip the $
    }

    /// <summary>
    /// Visit a symbol constant context.
    /// </summary>
    public override object? VisitSymbolConst(Smalltalk80Parser.SymbolConstContext context)
    {
        if (context.QUOTED_SYMBOL() != null)
        {
            var text = context.QUOTED_SYMBOL().GetText();
            return new LiteralExpression(text[1..]); // Skip the #
        }

        if (context.symbolName() != null)
        {
            var symbolName = context.symbolName().GetText();
            return new LiteralExpression(symbolName);
        }

        throw new InvalidOperationException("Unknown symbol type");
    }

    /// <summary>
    /// Visit an array literal context.
    /// </summary>
    public override object? VisitArrayLiteral(Smalltalk80Parser.ArrayLiteralContext context)
    {
        var elements = new List<object>();

        if (context.arrayElement() != null)
        {
            foreach (var element in context.arrayElement())
            {
                var elementValue = Visit(element);
                if (elementValue is LiteralExpression literal)
                    elements.Add(literal.Value);
                else
                    elements.Add(elementValue!);
            }
        }

        return new LiteralExpression(elements.ToArray());
    }

    /// <summary>
    /// Visit an array element context.
    /// </summary>
    public override object? VisitArrayElement(Smalltalk80Parser.ArrayElementContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());

        if (context.symbolName() != null)
        {
            var symbolName = context.symbolName().GetText();
            return new LiteralExpression(symbolName);
        }

        // Handle nested array elements
        if (context.arrayElement() != null && context.arrayElement().Length > 0)
        {
            var elements = new List<object>();
            foreach (var element in context.arrayElement())
            {
                var elementValue = Visit(element);
                if (elementValue is LiteralExpression literal)
                    elements.Add(literal.Value);
                else
                    elements.Add(elementValue!);
            }
            return new LiteralExpression(elements.ToArray());
        }

        throw new InvalidOperationException("Unknown array element type");
    }

    /// <summary>
    /// Visit a block expression context.
    /// </summary>
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        var parameters = new List<string>();

        if (context.blockArgs() != null)
        {
            foreach (var param in context.blockArgs().argumentName())
            {
                parameters.Add(param.IDENTIFIER().GetText());
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

        if (context.expressionStatement() != null)
        {
            return Visit(context.expressionStatement());
        }

        throw new InvalidOperationException("Unknown statement type");
    }

    /// <summary>
    /// Visit an expression statement context.
    /// </summary>
    public override object? VisitExpressionStatement(Smalltalk80Parser.ExpressionStatementContext context)
    {
        var expr = (ExpressionNode)Visit(context.expression())!;
        return new ExpressionStatement(expr);
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
