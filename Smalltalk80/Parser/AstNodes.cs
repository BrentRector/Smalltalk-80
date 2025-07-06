namespace Smalltalk80.Parser;

/// <summary>
/// Base type for all AST nodes produced by the parser.
/// </summary>
public abstract record AstNode;

/// <summary>
/// Root AST node representing a parsed script.
/// </summary>
/// <param name="Sequence">The top-level sequence.</param>
public sealed record ScriptNode(SequenceNode Sequence) : AstNode;

/// <summary>
/// A sequence of statements with optional temporary variables.
/// </summary>
/// <param name="Temporaries">Temporary variable names.</param>
/// <param name="Statements">The contained statements.</param>
public sealed record SequenceNode(IReadOnlyList<string> Temporaries, IReadOnlyList<StatementNode> Statements) : AstNode;

/// <summary>
/// Base type for all statement nodes.
/// </summary>
public abstract record StatementNode : AstNode;

/// <summary>
/// Statement consisting of a single expression.
/// </summary>
/// <param name="Expression">The expression to evaluate.</param>
public sealed record ExpressionStatement(ExpressionNode Expression) : StatementNode;

/// <summary>
/// Statement that returns the value of an expression.
/// </summary>
/// <param name="Expression">The expression whose value is returned.</param>
public sealed record AnswerStatement(ExpressionNode Expression) : StatementNode;

/// <summary>
/// Base type for expression nodes.
/// </summary>
public abstract record ExpressionNode : AstNode;

/// <summary>
/// A literal value such as an integer or string.
/// </summary>
/// <param name="Value">The literal value.</param>
public sealed record LiteralExpression(object Value) : ExpressionNode;

/// <summary>
/// Reference to a variable in the current scope.
/// </summary>
/// <param name="Name">The variable name.</param>
public sealed record VariableExpression(string Name) : ExpressionNode;

/// <summary>
/// Assignment of a value to a variable.
/// </summary>
/// <param name="Variable">The variable being assigned.</param>
/// <param name="Value">The assigned value.</param>
public sealed record AssignmentExpression(string Variable, ExpressionNode Value) : ExpressionNode;

/// <summary>
/// A binary message send such as <c>a + b</c>.
/// </summary>
/// <param name="Receiver">The receiver expression.</param>
/// <param name="Selector">The binary selector.</param>
/// <param name="Argument">The argument expression.</param>
public sealed record BinarySendExpression(ExpressionNode Receiver, string Selector, ExpressionNode Argument) : ExpressionNode;

/// <summary>
/// A keyword message send such as <c>a at: 1</c>.
/// </summary>
/// <param name="Receiver">The receiver expression.</param>
/// <param name="SelectorParts">The individual keyword parts including trailing colons.</param>
/// <param name="Arguments">The argument expressions corresponding to <paramref name="SelectorParts"/>.</param>
public sealed record KeywordSendExpression(
    ExpressionNode Receiver,
    IReadOnlyList<string> SelectorParts,
    IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;

/// <summary>
/// A unary message send such as <c>a size</c>.
/// </summary>
/// <param name="Receiver">The receiver expression.</param>
/// <param name="Selector">The unary selector.</param>
public sealed record UnarySendExpression(ExpressionNode Receiver, string Selector) : ExpressionNode;

/// <summary>
/// A cascade of messages sent to the same receiver.
/// </summary>
/// <param name="Receiver">The common receiver expression.</param>
/// <param name="Messages">The messages sent in order.</param>
public sealed record CascadeExpression(ExpressionNode Receiver, IReadOnlyList<ExpressionNode> Messages) : ExpressionNode;

/// <summary>
/// Represents one of the Smalltalk pseudo variables such as <c>self</c> or <c>nil</c>.
/// </summary>
/// <param name="Name">The pseudo variable name.</param>
public sealed record PseudoVariableExpression(string Name) : ExpressionNode;

/// <summary>
/// Root node representing a file-in consisting of multiple chunks.
/// </summary>
/// <param name="Chunks">The ordered chunks in the file.</param>
public sealed record FileNode(IReadOnlyList<AstNode> Chunks) : AstNode;

/// <summary>
/// Node representing a class definition chunk.
/// </summary>
/// <param name="Superclass">The superclass name.</param>
/// <param name="ClassName">The class being defined.</param>
/// <param name="InstanceVariables">Declared instance variables for the class.</param>
/// <param name="ClassVariables">Declared class variables shared by all instances.</param>
/// <param name="PoolDictionaries">Names of any pool dictionaries referenced by the class.</param>
/// <param name="Category">Organizational category for the class.</param>
public sealed record ClassDefinitionNode(
    string Superclass,
    string ClassName,
    IReadOnlyList<string> InstanceVariables,
    IReadOnlyList<string> ClassVariables,
    IReadOnlyList<string> PoolDictionaries,
    string? Category) : AstNode;

/// <summary>
/// Node representing a class comment chunk.
/// </summary>
/// <param name="ClassName">The class being documented.</param>
/// <param name="Comment">The text of the comment.</param>
public sealed record ClassCommentNode(string ClassName, string Comment) : AstNode;

/// <summary>
/// Node representing a method category header chunk.
/// </summary>
/// <param name="ClassName">The class to which subsequent methods belong.</param>
/// <param name="IsClassSide">Whether the methods belong to the metaclass.</param>
/// <param name="Category">The category name.</param>
public sealed record MethodHeaderNode(string ClassName, bool IsClassSide, string Category) : AstNode;

/// <summary>
/// Node representing a method definition.
/// </summary>
/// <param name="Selector">The method selector.</param>
/// <param name="Arguments">Names of the method parameters in declaration order.</param>
/// <param name="Body">The method body.</param>
/// <param name="Comment">Optional method comment text.</param>
/// <param name="Source">The raw Smalltalk source for the method body.</param>
public sealed record MethodDefinitionNode(
    string Selector,
    IReadOnlyList<string> Arguments,
    SequenceNode Body,
    string? Comment,
    string Source = "") : AstNode;

/// <summary>
/// Expression representing a block literal.
/// </summary>
/// <param name="Parameters">Names of the block parameters in declaration order.</param>
/// <param name="Body">The sequence executed when the block is evaluated.</param>
public sealed record BlockExpression(IReadOnlyList<string> Parameters, SequenceNode Body) : ExpressionNode;

/// <summary>
/// Expression representing a dynamic array literal evaluated at runtime.
/// </summary>
/// <param name="Elements">The element expressions comprising the array.</param>
public sealed record DynamicArrayExpression(IReadOnlyList<ExpressionNode> Elements) : ExpressionNode;

/// <summary>
/// Expression representing a dynamic dictionary literal evaluated at runtime.
/// </summary>
/// <param name="Elements">The expressions contained within the dictionary literal.</param>
public sealed record DynamicDictionaryExpression(IReadOnlyList<ExpressionNode> Elements) : ExpressionNode;

