using System.Collections.Immutable;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Base class for all literal nodes.
/// </summary>
/// <param name="Location">The source location.</param>
public abstract record LiteralNode(SourceLocation Location) : ExpressionNode(Location);

/// <summary>
/// Represents a number literal.
/// </summary>
/// <param name="Value">The numeric value.</param>
/// <param name="IsFloat">Whether this is a floating-point number.</param>
/// <param name="Location">The source location.</param>
public sealed record NumberLiteralNode(
    object Value,
    bool IsFloat,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNumberLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitNumberLiteral(this);
}

/// <summary>
/// Represents a string literal.
/// </summary>
/// <param name="Value">The string value.</param>
/// <param name="Location">The source location.</param>
public sealed record StringLiteralNode(
    string Value,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitStringLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitStringLiteral(this);
}

/// <summary>
/// Represents a symbol literal (#symbol).
/// </summary>
/// <param name="Value">The symbol value.</param>
/// <param name="Location">The source location.</param>
public sealed record SymbolLiteralNode(
    string Value,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSymbolLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitSymbolLiteral(this);
}

/// <summary>
/// Represents an array literal #(elements).
/// </summary>
/// <param name="Elements">The array elements.</param>
/// <param name="Location">The source location.</param>
public sealed record ArrayLiteralNode(
    ImmutableArray<LiteralNode> Elements,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArrayLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitArrayLiteral(this);
}

/// <summary>
/// Represents a character literal ($c).
/// </summary>
/// <param name="Value">The character value.</param>
/// <param name="Location">The source location.</param>
public sealed record CharacterLiteralNode(
    char Value,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCharacterLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitCharacterLiteral(this);
}

/// <summary>
/// Represents a boolean literal (true or false).
/// </summary>
/// <param name="Value">The boolean value.</param>
/// <param name="Location">The source location.</param>
public sealed record BooleanLiteralNode(
    bool Value,
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBooleanLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitBooleanLiteral(this);
}

/// <summary>
/// Represents the nil literal.
/// </summary>
/// <param name="Location">The source location.</param>
public sealed record NilLiteralNode(
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNilLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitNilLiteral(this);
}

/// <summary>
/// Represents the self pseudo-variable.
/// </summary>
/// <param name="Location">The source location.</param>
public sealed record SelfLiteralNode(
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSelfLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitSelfLiteral(this);
}

/// <summary>
/// Represents the super pseudo-variable.
/// </summary>
/// <param name="Location">The source location.</param>
public sealed record SuperLiteralNode(
    SourceLocation Location) : LiteralNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSuperLiteral(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitSuperLiteral(this);
}