namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Base class for all Abstract Syntax Tree nodes in the Smalltalk-80 compiler.
/// </summary>
public abstract record AstNode
{
    /// <summary>
    /// Gets the source location information for this AST node.
    /// </summary>
    public SourceLocation Location { get; init; }
    
    /// <summary>
    /// Gets the original source text for this AST node, if available.
    /// </summary>
    public string? SourceText { get; init; }

    /// <summary>
    /// Accepts a visitor for processing this node.
    /// </summary>
    /// <typeparam name="T">The return type of the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of visiting this node.</returns>
    public abstract T Accept<T>(IAstVisitor<T> visitor);

    /// <summary>
    /// Accepts a visitor for processing this node without a return value.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public abstract void Accept(IAstVisitor visitor);

    /// <summary>
    /// Initializes a new instance of the AstNode class.
    /// </summary>
    /// <param name="Location">The source location of this node.</param>
    protected AstNode(SourceLocation Location)
    {
        this.Location = Location;
        this.SourceText = null;
    }
    
    /// <summary>
    /// Initializes a new instance of the AstNode class with source text.
    /// </summary>
    /// <param name="Location">The source location of this node.</param>
    /// <param name="SourceText">The original source text of this node.</param>
    protected AstNode(SourceLocation Location, string? SourceText)
    {
        this.Location = Location;
        this.SourceText = SourceText;
    }
}

/// <summary>
/// Represents source location information for AST nodes.
/// </summary>
/// <param name="Line">The line number (1-based).</param>
/// <param name="Column">The column number (1-based).</param>
/// <param name="StartIndex">The start character index in the source.</param>
/// <param name="EndIndex">The end character index in the source.</param>
public record struct SourceLocation(int Line, int Column, int StartIndex, int EndIndex)
{
    /// <summary>
    /// Gets an empty source location.
    /// </summary>
    public static SourceLocation Empty => new(0, 0, 0, 0);
}