using System.Collections.Immutable;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Represents the root of a Smalltalk program AST.
/// </summary>
/// <param name="Elements">The top-level program elements.</param>
/// <param name="Location">The source location.</param>
public sealed record ProgramNode(
    ImmutableArray<AstNode> Elements,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitProgram(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitProgram(this);
}

/// <summary>
/// Represents a class definition in Smalltalk.
/// </summary>
/// <param name="ClassName">The name of the class.</param>
/// <param name="SuperclassName">The name of the superclass.</param>
/// <param name="SubclassKind">The kind of subclass (e.g., "subclass:", "variableSubclass:").</param>
/// <param name="InstanceVariables">The instance variable names.</param>
/// <param name="ClassVariables">The class variable names.</param>
/// <param name="PoolDictionaries">The pool dictionary names.</param>
/// <param name="Category">The class category.</param>
/// <param name="Methods">The methods defined in the class.</param>
/// <param name="Location">The source location.</param>
/// <param name="SourceText">The original source text.</param>
public sealed record ClassDefinitionNode(
    string ClassName,
    string SuperclassName,
    string SubclassKind,
    ImmutableArray<string> InstanceVariables,
    ImmutableArray<string> ClassVariables,
    ImmutableArray<string> PoolDictionaries,
    string Category,
    ImmutableArray<MethodDefinitionNode> Methods,
    SourceLocation Location,
    string? SourceText = null) : AstNode(Location, SourceText)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitClassDefinition(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitClassDefinition(this);
}

/// <summary>
/// Represents a method definition in Smalltalk.
/// </summary>
/// <param name="Selector">The method selector.</param>
/// <param name="Arguments">The method arguments.</param>
/// <param name="Temporaries">The temporary variable declarations.</param>
/// <param name="Primitive">The primitive declaration, if any.</param>
/// <param name="Body">The method body statements.</param>
/// <param name="SourceCode">The original source code.</param>
/// <param name="Location">The source location.</param>
public sealed record MethodDefinitionNode(
    string Selector,
    ImmutableArray<MethodArgumentNode> Arguments,
    ImmutableArray<TemporaryDeclarationNode> Temporaries,
    PrimitiveDeclarationNode? Primitive,
    StatementListNode Body,
    string SourceCode,
    SourceLocation Location) : AstNode(Location)
{
    /// <inheritdoc/>
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitMethodDefinition(this);

    /// <inheritdoc/>
    public override void Accept(IAstVisitor visitor) => visitor.VisitMethodDefinition(this);
}