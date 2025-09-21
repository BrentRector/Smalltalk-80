using System.Collections.Immutable;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Base class for all expression nodes.
/// </summary>
/// <param name="Location">The source location.</param>
public abstract record ExpressionNode(SourceLocation Location) : AstNode(Location);

/// <summary>
/// Represents an assignment expression (variable := expression).
/// </summary>
/// <param name="Variable">The variable being assigned to.</param>
/// <param name="Value">The value being assigned.</param>
/// <param name="Location">The source location.</param>
public sealed record AssignmentNode(
    string Variable,
    ExpressionNode Value,
    SourceLocation Location) : ExpressionNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignment(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitAssignment(this);
}

/// <summary>
/// Represents a cascade expression (receiver message1; message2; message3).
/// </summary>
/// <param name="Receiver">The receiver expression.</param>
/// <param name="Messages">The cascade messages.</param>
/// <param name="Location">The source location.</param>
public sealed record CascadeNode(
    ExpressionNode Receiver,
    ImmutableArray<MessageNode> Messages,
    SourceLocation Location) : ExpressionNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCascade(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitCascade(this);
}

/// <summary>
/// Represents a message send expression.
/// </summary>
/// <param name="Receiver">The receiver expression.</param>
/// <param name="Message">The message being sent.</param>
/// <param name="Location">The source location.</param>
public sealed record MessageSendNode(
    ExpressionNode Receiver,
    MessageNode Message,
    SourceLocation Location) : ExpressionNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitMessageSend(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitMessageSend(this);
}

/// <summary>
/// Represents a block expression ([args | temps | statements]).
/// </summary>
/// <param name="Arguments">The block arguments.</param>
/// <param name="Temporaries">The temporary variable declarations.</param>
/// <param name="Body">The block body statements.</param>
/// <param name="Location">The source location.</param>
public sealed record BlockNode(
    ImmutableArray<BlockArgumentNode> Arguments,
    ImmutableArray<TemporaryDeclarationNode> Temporaries,
    StatementListNode Body,
    SourceLocation Location) : ExpressionNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBlock(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitBlock(this);
}

/// <summary>
/// Represents a variable reference.
/// </summary>
/// <param name="Name">The variable name.</param>
/// <param name="Location">The source location.</param>
public sealed record VariableNode(
    string Name,
    SourceLocation Location) : ExpressionNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVariable(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitVariable(this);
}