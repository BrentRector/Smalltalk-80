using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// AST building visitor that matches the current grammar structure.
/// </summary>
public class AstBuildingVisitor : Smalltalk80BaseVisitor<object?>
{
    private readonly CommonTokenStream tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstBuildingVisitor"/> class.
    /// </summary>
    /// <param name="tokens">The token stream produced by the lexer.</param>
    public AstBuildingVisitor(CommonTokenStream tokens) => this.tokens = tokens;

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
        if (context.variable() != null && context.ASSIGN() != null)
        {
            // This is an assignment
            var variable = context.variable().GetText();
            var value = (ExpressionNode)Visit(context.assignmentExpr())!;
            return new AssignmentExpression(variable, value);
        }
        else
        {
            // This is just a cascade expression
            return Visit(context.cascadeExpr());
        }
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
        var baseExpr = (ExpressionNode)Visit(context.messageExpr())!;

        var messageParts = context.messagePart();
        if (messageParts.Length == 0)
        {
            return baseExpr;
        }

        // Create a cascade expression
        var messages = new List<ExpressionNode>();
        foreach (var part in messageParts)
        {
            messages.Add((ExpressionNode)Visit(part)!);
        }

        return new CascadeExpression(baseExpr, messages);
    }

    /// <summary>
    /// Visit a message expression context.
    /// </summary>
    public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
    {
        var receiver = (ExpressionNode)Visit(context.messageBase())!;

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
                var argument = (ExpressionNode)Visit(binary.messageBase())!;
                current = new BinarySendExpression(current, selector, argument);
            }
            else if (child is Smalltalk80Parser.KeywordMessageContext keyword)
            {
                var parts = new List<string>();
                var arguments = new List<ExpressionNode>();

                foreach (var keywordPart in keyword.keywordPart())
                {
                    parts.Add(keywordPart.IDENTIFIER().GetText() + ":");
                    arguments.Add((ExpressionNode)Visit(keywordPart.messageExpr())!);
                }

                current = new KeywordSendExpression(current, parts, arguments);
            }
        }

        return current;
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
}
