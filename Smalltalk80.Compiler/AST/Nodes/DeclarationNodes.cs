namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Represents a temporary variable declaration.
/// </summary>
/// <param name="Name">The variable name.</param>
/// <param name="Location">The source location.</param>
public sealed record TemporaryDeclarationNode(
    string Name,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitTemporaryDeclaration(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitTemporaryDeclaration(this);
}

/// <summary>
/// Represents a block argument declaration.
/// </summary>
/// <param name="Name">The argument name.</param>
/// <param name="Location">The source location.</param>
public sealed record BlockArgumentNode(
    string Name,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBlockArgument(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitBlockArgument(this);
}

/// <summary>
/// Represents a method argument declaration.
/// </summary>
/// <param name="Name">The argument name.</param>
/// <param name="Location">The source location.</param>
public sealed record MethodArgumentNode(
    string Name,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitMethodArgument(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitMethodArgument(this);
}

/// <summary>
/// Represents a primitive method declaration.
/// </summary>
/// <param name="PrimitiveNumber">The primitive number.</param>
/// <param name="Location">The source location.</param>
public sealed record PrimitiveDeclarationNode(
    int PrimitiveNumber,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitPrimitiveDeclaration(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitPrimitiveDeclaration(this);
}