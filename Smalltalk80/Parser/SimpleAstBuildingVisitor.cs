using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Simple AST building visitor that matches the current grammar structure.
/// </summary>
public class SimpleAstBuildingVisitor : Smalltalk80BaseVisitor<object?>
{
    /// <summary>
    /// Visit a return statement context and create an AnswerStatement.
    /// </summary>
    public override object? VisitReturnStatement(Smalltalk80Parser.ReturnStatementContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression())!;
        return new AnswerStatement(expression);
    }

    /// <summary>
    /// Visit an assignment expression context.
    /// </summary>
    public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
    {
        // Check if this is an assignment (variable ASSIGN assignmentExpr)
        if (context.variable() != null && context.ASSIGN() != null && context.assignmentExpr() != null)
        {
            var variable = context.variable().GetText();
            var value = (ExpressionNode)Visit(context.assignmentExpr())!;
            return new AssignmentExpression(variable, value);
        }

        // Otherwise, it's just a cascade expression
        return Visit(context.cascadeExpr());
    }

    /// <summary>
    /// Visit an expression statement context.
    /// </summary>
    public override object? VisitExpressionStatement(Smalltalk80Parser.ExpressionStatementContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression())!;
        return new ExpressionStatement(expression);
    }

    /// <summary>
    /// Visit an expression context.
    /// </summary>
    public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context)
    {
        return Visit(context.assignmentExpr());
    }

    /// <summary>
    /// Visit a cascade expression context.
    /// </summary>
    public override object? VisitCascadeExpr(Smalltalk80Parser.CascadeExprContext context)
    {
        try
        {
            var messageExprContext = context.messageExpr();
            if (messageExprContext == null)
            {
                var lineInfo = context.Start != null ? $"Line {context.Start.Line}, Column {context.Start.Column}" : "Unknown location";
                var contextText = context.GetText();
                throw new InvalidOperationException($"Cascade expression has no base message expression at {lineInfo}. Context: '{contextText}'");
            }

            var baseExprResult = Visit(messageExprContext);
            if (baseExprResult == null)
            {
                throw new InvalidOperationException("Cascade base expression returned null");
            }
            var baseExpr = (ExpressionNode)baseExprResult;

            var messageParts = context.messagePart();
            if (messageParts.Length == 0)
            {
                return baseExpr;
            }

            // Create a cascade expression
            var messages = new List<ExpressionNode>();
            foreach (var part in messageParts)
            {
                var partResult = Visit(part);
                if (partResult != null)
                {
                    messages.Add((ExpressionNode)partResult);
                }
            }

            return new CascadeExpression(baseExpr, messages);
        }
        catch (Exception ex)
        {
            var lineInfo = context.Start != null ? $"Line {context.Start.Line}, Column {context.Start.Column}" : "Unknown location";
            var contextText = context.GetText();
            throw new InvalidOperationException($"Error in VisitCascadeExpr at {lineInfo}: {ex.Message}. Context: '{contextText}'", ex);
        }
    }

    /// <summary>
    /// Visit a message expression context.
    /// </summary>
    public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
    {
        try
        {
            var messageBase = context.messageBase();
            if (messageBase == null)
            {
                throw new InvalidOperationException("MessageExpr context has null messageBase");
            }

            var receiverResult = Visit(messageBase);
            if (receiverResult == null)
            {
                throw new InvalidOperationException($"Message receiver Visit returned null for messageBase type: {messageBase.GetType().Name}");
            }
            var receiver = (ExpressionNode)receiverResult;

            // Apply all message sends to the receiver
            var current = receiver;
            for (int i = 0; i < context.ChildCount; i++)
            {
                var child = context.GetChild(i);
                if (child is Smalltalk80Parser.UnaryMessageContext unary)
                {
                    var selector = unary.unarySelector().GetText();
                    current = new UnarySendExpression(current, selector);
                }
                else if (child is Smalltalk80Parser.BinaryMessageContext binary)
                {
                    var selector = binary.binarySelector().GetText();
                    var argumentResult = Visit(binary.messageBase());
                    if (argumentResult == null)
                    {
                        throw new InvalidOperationException("Binary message argument cannot be null");
                    }
                    var argument = (ExpressionNode)argumentResult;
                    current = new BinarySendExpression(current, selector, argument);
                }
                else if (child is Smalltalk80Parser.KeywordMessageContext keyword)
                {
                    var parts = new List<string>();
                    var arguments = new List<ExpressionNode>();

                    foreach (var keywordPart in keyword.keywordPart())
                    {
                        parts.Add(keywordPart.IDENTIFIER().GetText() + ":");
                        var argResult = Visit(keywordPart.nonKeywordExpr());
                        if (argResult == null)
                        {
                            throw new InvalidOperationException("Keyword message argument cannot be null");
                        }
                        arguments.Add((ExpressionNode)argResult);
                    }

                    current = new KeywordSendExpression(current, parts, arguments);
                }
            }

            return current;
        }
        catch (Exception ex)
        {
            var lineInfo = context.Start != null ? $"Line {context.Start.Line}, Column {context.Start.Column}" : "Unknown location";
            var contextText = context.GetText();
            throw new InvalidOperationException($"Error in VisitMessageExpr at {lineInfo}: {ex.Message}. Context: '{contextText}'", ex);
        }
    }

    /// <summary>
    /// Visit a message base context.
    /// </summary>
    public override object? VisitMessageBase(Smalltalk80Parser.MessageBaseContext context)
    {
        if (context.parenthesizedExpr() != null)
            return Visit(context.parenthesizedExpr());
        if (context.block() != null)
            return Visit(context.block());
        if (context.literal() != null)
            return Visit(context.literal());
        if (context.variable() != null)
            return Visit(context.variable());
        if (context.negativeNumber() != null)
            return Visit(context.negativeNumber());

        throw new InvalidOperationException("Unknown message base type");
    }

    /// <summary>
    /// Visit a variable context.
    /// </summary>
    public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
    {
        return new VariableExpression(context.GetText());
    }

    /// <summary>
    /// Visit a literal context.
    /// </summary>
    public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.numberLiteral() != null)
            return Visit(context.numberLiteral());
        if (context.stringConst() != null)
            return Visit(context.stringConst());
        if (context.symbolConst() != null)
            return Visit(context.symbolConst());
        if (context.arrayLiteral() != null)
            return Visit(context.arrayLiteral());
        if (context.byteArrayLiteral() != null)
            return Visit(context.byteArrayLiteral());
        if (context.characterLiteral() != null)
            return Visit(context.characterLiteral());

        throw new InvalidOperationException("Unknown literal type");
    }

    /// <summary>
    /// Visit a number literal context.
    /// </summary>
    public override object? VisitNumberLiteral(Smalltalk80Parser.NumberLiteralContext context)
    {
        var text = context.GetText();
        if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
        {
            return new LiteralExpression(double.Parse(text));
        }
        else if (text.Contains('r') || text.Contains('R'))
        {
            // Handle radix numbers
            var parts = text.Split(['r', 'R']);
            var radix = int.Parse(parts[0]);
            var value = Convert.ToInt32(parts[1], radix);
            return new LiteralExpression(value);
        }
        else
        {
            return new LiteralExpression(int.Parse(text));
        }
    }

    /// <summary>
    /// Visit a block context.
    /// </summary>
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        var parameters = new List<string>();

        // Extract block arguments if present
        if (context.blockArgs() != null)
        {
            var argumentNames = context.blockArgs().argumentName();
            if (argumentNames != null)
            {
                foreach (var arg in argumentNames)
                {
                    if (arg != null)
                        parameters.Add(arg.GetText());
                }
            }
        }

        // Extract block temporaries if present
        var temporaries = new List<string>();
        if (context.blockTemps() != null)
        {
            var temporaryNames = context.blockTemps().temporaryName();
            if (temporaryNames != null)
            {
                foreach (var temp in temporaryNames)
                {
                    if (temp != null)
                        temporaries.Add(temp.GetText());
                }
            }
        }

        // Create block body - if no statements, create empty sequence
        SequenceNode body;
        if (context.statements() != null)
        {
            var statements = (List<StatementNode>)Visit(context.statements())!;
            body = new SequenceNode(temporaries, statements);
        }
        else
        {
            body = new SequenceNode(temporaries, new List<StatementNode>());
        }

        return new BlockExpression(parameters, body);
    }

    /// <summary>
    /// Visit a negative number context.
    /// </summary>
    public override object? VisitNegativeNumber(Smalltalk80Parser.NegativeNumberContext context)
    {
        var innerExpression = Visit(context.messageBase());
        if (innerExpression is LiteralExpression literal)
        {
            if (literal.Value is int intValue)
                return new LiteralExpression(-intValue);
            if (literal.Value is double doubleValue)
                return new LiteralExpression(-doubleValue);
        }
        throw new InvalidOperationException("Negative number must be applied to a numeric literal");
    }

    /// <summary>
    /// Visit a string constant context.
    /// </summary>
    public override object? VisitStringConst(Smalltalk80Parser.StringConstContext context)
    {
        var text = context.GetText();
        // Remove quotes and handle escaped quotes
        var content = text.Substring(1, text.Length - 2).Replace("''", "'");
        return new LiteralExpression(content);
    }

    /// <summary>
    /// Visit a character literal context.
    /// </summary>
    public override object? VisitCharacterLiteral(Smalltalk80Parser.CharacterLiteralContext context)
    {
        var text = context.GetText();
        if (text == "$!!")
        {
            return new LiteralExpression('!');
        }
        else
        {
            return new LiteralExpression(text[1]); // Skip the '$'
        }
    }

    /// <summary>
    /// Visit a parenthesized expression context.
    /// </summary>
    public override object? VisitParenthesizedExpr(Smalltalk80Parser.ParenthesizedExprContext context)
    {
        return Visit(context.expression());
    }

    /// <summary>
    /// Visit a statement context.
    /// </summary>
    public override object? VisitStatement(Smalltalk80Parser.StatementContext context)
    {
        if (context.returnStatement() != null)
            return Visit(context.returnStatement());
        if (context.expressionStatement() != null)
            return Visit(context.expressionStatement());

        throw new InvalidOperationException("Unknown statement type");
    }

    /// <summary>
    /// Visit a statements context.
    /// </summary>
    public override object? VisitStatements(Smalltalk80Parser.StatementsContext context)
    {
        var statements = new List<StatementNode>();
        foreach (var stmt in context.statement())
        {
            statements.Add((StatementNode)Visit(stmt)!);
        }
        return statements;
    }

    /// <summary>
    /// Visit a non-keyword expression context (used in keyword arguments).
    /// </summary>
    public override object? VisitNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext context)
    {
        // Start with the message base
        var current = (ExpressionNode)Visit(context.messageBase())!;

        // Apply unary and binary messages in order
        for (int i = 1; i < context.ChildCount; i++)
        {
            var child = context.GetChild(i);
            if (child is Smalltalk80Parser.UnaryMessageContext unary)
            {
                var selector = unary.unarySelector().GetText();
                current = new UnarySendExpression(current, selector);
            }
            else if (child is Smalltalk80Parser.BinaryMessageContext binary)
            {
                var selector = binary.binarySelector().GetText();
                var argument = (ExpressionNode)Visit(binary.messageBase())!;
                current = new BinarySendExpression(current, selector, argument);
            }
        }

        return current;
    }
}
