using System.Collections.Immutable;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Base class for all message nodes.
/// </summary>
/// <param name="Location">The source location.</param>
public abstract record MessageNode(SourceLocation Location) : AstNode(Location)
{
    /// <summary>
    /// Gets the selector for this message.
    /// </summary>
    public abstract string GetSelector();

    /// <summary>
    /// Gets the arguments for this message.
    /// </summary>
    public abstract ImmutableArray<ExpressionNode> GetArguments();
}

/// <summary>
/// Represents a unary message (no arguments).
/// </summary>
/// <param name="Selector">The message selector.</param>
/// <param name="Location">The source location.</param>
public sealed record UnaryMessageNode(
    string Selector,
    SourceLocation Location) : MessageNode(Location)
{
    /// <inheritdoc/>
    public override string GetSelector() => Selector;

    /// <inheritdoc/>
    public override ImmutableArray<ExpressionNode> GetArguments() => ImmutableArray<ExpressionNode>.Empty;

    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnaryMessage(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitUnaryMessage(this);
}

/// <summary>
/// Represents a binary message (one argument with operator selector).
/// </summary>
/// <param name="Selector">The operator selector.</param>
/// <param name="Argument">The argument expression.</param>
/// <param name="Location">The source location.</param>
public sealed record BinaryMessageNode(
    string Selector,
    ExpressionNode Argument,
    SourceLocation Location) : MessageNode(Location)
{
    /// <inheritdoc/>
    public override string GetSelector() => Selector;

    /// <inheritdoc/>
    public override ImmutableArray<ExpressionNode> GetArguments() => ImmutableArray.Create(Argument);

    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinaryMessage(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitBinaryMessage(this);
}

/// <summary>
/// Represents a keyword message (one or more keyword:argument pairs).
/// </summary>
/// <param name="Keywords">The keyword parts of the selector.</param>
/// <param name="Arguments">The argument expressions.</param>
/// <param name="Location">The source location.</param>
public sealed record KeywordMessageNode(
    ImmutableArray<string> Keywords,
    ImmutableArray<ExpressionNode> Arguments,
    SourceLocation Location) : MessageNode(Location)
{
    /// <inheritdoc/>
    public override string GetSelector() => string.Concat(Keywords);

    /// <inheritdoc/>
    public override ImmutableArray<ExpressionNode> GetArguments() => Arguments;

    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitKeywordMessage(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitKeywordMessage(this);
}