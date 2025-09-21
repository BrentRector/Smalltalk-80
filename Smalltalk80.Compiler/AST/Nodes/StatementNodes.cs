using System.Collections.Immutable;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Base class for all statement nodes.
/// </summary>
/// <param name="Location">The source location.</param>
public abstract record StatementNode(SourceLocation Location) : AstNode(Location);

/// <summary>
/// Represents a list of statements.
/// </summary>
/// <param name="Statements">The statements in the list.</param>
/// <param name="Location">The source location.</param>
public sealed record StatementListNode(
    ImmutableArray<StatementNode> Statements,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitStatementList(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitStatementList(this);
}

/// <summary>
/// Represents a return statement (^expression).
/// </summary>
/// <param name="Expression">The expression to return.</param>
/// <param name="Location">The source location.</param>
public sealed record ReturnStatementNode(
    ExpressionNode Expression,
    SourceLocation Location) : StatementNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitReturnStatement(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitReturnStatement(this);
}

/// <summary>
/// Represents an expression used as a statement.
/// </summary>
/// <param name="Expression">The expression.</param>
/// <param name="Location">The source location.</param>
public sealed record ExpressionStatementNode(
    ExpressionNode Expression,
    SourceLocation Location) : StatementNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExpressionStatement(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitExpressionStatement(this);
}