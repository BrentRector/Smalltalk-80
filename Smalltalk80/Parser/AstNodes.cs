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
