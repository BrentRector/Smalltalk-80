using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// AST Node classes for the Smalltalk-80 compiler.
/// These classes represent the parsed AST structure and are used for JSON serialization.
/// </summary>

// Compilation result and class definitions

/// <summary>
/// Root result of parsing a Smalltalk source file.
/// Contains all parsed classes and initialization expressions.
/// </summary>
public class CompilationResult
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("classes")]
    public List<ClassDefinitionNode> Classes { get; set; } = [];

    [JsonPropertyName("initializationExpressions")]
    public List<InitializationExpression> InitializationExpressions { get; set; } = [];
}

/// <summary>
/// Represents a Smalltalk class definition in the AST.
/// </summary>
public class ClassDefinitionNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("superclass")]
    public string Superclass { get; set; } = "";

    [JsonPropertyName("subclassKind")]
    public string SubclassKind { get; set; } = "subclass:";  // Default to fixed pointer

    [JsonPropertyName("instanceVariables")]
    public List<string> InstanceVariables { get; set; } = [];

    /// <summary>
    /// Total count of all instance variables including inherited ones.
    /// Used for format word calculation.
    /// </summary>
    [JsonIgnore]
    public int TotalInstanceVariableCount { get; set; }

    [JsonPropertyName("classVariables")]
    public List<string> ClassVariables { get; set; } = [];

    [JsonPropertyName("poolDictionaries")]
    public List<string> PoolDictionaries { get; set; } = [];

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("methods")]
    public List<MethodDefinitionNode> Methods { get; set; } = [];

    [JsonPropertyName("classMethods")]
    public List<MethodDefinitionNode> ClassMethods { get; set; } = [];
}

/// <summary>
/// Represents a Smalltalk method definition in the AST.
/// </summary>
public class MethodDefinitionNode
{
    [JsonPropertyName("class")]
    public string Class { get; set; } = "";

    [JsonPropertyName("selector")]
    public string Selector { get; set; } = "";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("bytecode")]
    public List<int> Bytecode { get; set; } = [];

    [JsonPropertyName("literals")]
    public List<object> Literals { get; set; } = [];

    [JsonPropertyName("primitiveIndex")]
    public int PrimitiveIndex { get; set; }

    [JsonPropertyName("numArgs")]
    public int NumArgs { get; set; }

    [JsonPropertyName("numTemps")]
    public int NumTemps { get; set; }

    [JsonPropertyName("stackDepth")]
    public int StackDepth { get; set; }

    [JsonPropertyName("isClassMethod")]
    public bool IsClassMethod { get; set; }

    [JsonPropertyName("argNames")]
    public List<string> ArgNames { get; set; } = [];

    [JsonPropertyName("tempNames")]
    public List<string> TempNames { get; set; } = [];

    [JsonPropertyName("isQuickReturnSelf")]
    public bool IsQuickReturnSelf { get; set; }

    [JsonPropertyName("quickReturnFieldIndex")]
    public int QuickReturnFieldIndex { get; set; } = -1;
}

/// <summary>
/// Represents an initialization expression to be executed after loading classes.
/// </summary>
public class InitializationExpression
{
    [JsonPropertyName("bytecode")]
    public List<int> Bytecode { get; set; } = [];

    [JsonPropertyName("literals")]
    public List<object> Literals { get; set; } = [];

    [JsonPropertyName("numTemps")]
    public int NumTemps { get; set; }

    [JsonPropertyName("stackDepth")]
    public int StackDepth { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("receiver")]
    public string? Receiver { get; set; }

    [JsonPropertyName("selector")]
    public string? Selector { get; set; }
}

// Expression nodes

/// <summary>
/// Base class for all expression nodes in the AST.
/// Uses polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AssignmentExpressionNode), "assignment")]
[JsonDerivedType(typeof(CascadeExpressionNode), "cascade")]
[JsonDerivedType(typeof(MessageExpressionNode), "message")]
[JsonDerivedType(typeof(VariableNode), "variable")]
[JsonDerivedType(typeof(BlockNode), "block")]
[JsonDerivedType(typeof(DynamicArrayNode), "dynamicArray")]
[JsonDerivedType(typeof(IntegerLiteralNode), "integerLiteral")]
[JsonDerivedType(typeof(FloatLiteralNode), "floatLiteral")]
[JsonDerivedType(typeof(StringLiteralNode), "stringLiteral")]
[JsonDerivedType(typeof(SymbolLiteralNode), "symbolLiteral")]
[JsonDerivedType(typeof(CharacterLiteralNode), "characterLiteral")]
[JsonDerivedType(typeof(ArrayLiteralNode), "arrayLiteral")]
[JsonDerivedType(typeof(ByteArrayLiteralNode), "byteArrayLiteral")]
public abstract class ExpressionNode { }

/// <summary>
/// Represents an assignment expression (variable := value).
/// </summary>
public class AssignmentExpressionNode : ExpressionNode
{
    [JsonPropertyName("variable")]
    public string Variable { get; set; } = "";

    [JsonPropertyName("value")]
    public ExpressionNode? Value { get; set; }
}

/// <summary>
/// Represents a cascade expression (receiver msg1; msg2; msg3).
/// </summary>
public class CascadeExpressionNode : ExpressionNode
{
    [JsonPropertyName("receiver")]
    public ExpressionNode? Receiver { get; set; }

    [JsonPropertyName("messages")]
    public List<MessageNode> Messages { get; set; } = [];
}

/// <summary>
/// Represents a message send expression (receiver message).
/// </summary>
public class MessageExpressionNode : ExpressionNode
{
    [JsonPropertyName("receiver")]
    public ExpressionNode? Receiver { get; set; }

    [JsonPropertyName("message")]
    public MessageNode? Message { get; set; }
}

/// <summary>
/// Represents a variable reference.
/// </summary>
public class VariableNode : ExpressionNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>
/// Represents a block closure [:args | statements].
/// </summary>
public class BlockNode : ExpressionNode
{
    [JsonPropertyName("parameters")]
    public List<string> Parameters { get; set; } = [];

    [JsonPropertyName("temporaries")]
    public List<string> Temporaries { get; set; } = [];

    [JsonPropertyName("statements")]
    public List<StatementNode> Statements { get; set; } = [];
}

/// <summary>
/// Represents a dynamic array literal {expr. expr. expr}.
/// </summary>
public class DynamicArrayNode : ExpressionNode
{
    [JsonPropertyName("elements")]
    public List<ExpressionNode> Elements { get; set; } = [];
}

// Message nodes

/// <summary>
/// Base class for message nodes in the AST.
/// Uses polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(UnaryMessageNode), "unary")]
[JsonDerivedType(typeof(BinaryMessageNode), "binary")]
[JsonDerivedType(typeof(KeywordMessageNode), "keyword")]
public abstract class MessageNode
{
    [JsonPropertyName("selector")]
    public string Selector { get; set; } = "";
}

/// <summary>
/// Represents a unary message (no arguments).
/// </summary>
public class UnaryMessageNode : MessageNode { }

/// <summary>
/// Represents a binary message (one argument, operator selector).
/// </summary>
public class BinaryMessageNode : MessageNode
{
    [JsonPropertyName("argument")]
    public ExpressionNode? Argument { get; set; }
}

/// <summary>
/// Represents a keyword message (one or more keyword:argument pairs).
/// </summary>
public class KeywordMessageNode : MessageNode
{
    [JsonPropertyName("arguments")]
    public List<ExpressionNode> Arguments { get; set; } = [];
}

// Statement nodes

/// <summary>
/// Base class for statement nodes in the AST.
/// Uses polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ExpressionStatementNode), "expression")]
[JsonDerivedType(typeof(ReturnStatementNode), "return")]
public abstract class StatementNode { }

/// <summary>
/// Represents an expression statement (expression without return).
/// </summary>
public class ExpressionStatementNode : StatementNode
{
    [JsonPropertyName("expression")]
    public ExpressionNode? Expression { get; set; }
}

/// <summary>
/// Represents a return statement (^expression).
/// </summary>
public class ReturnStatementNode : StatementNode
{
    [JsonPropertyName("expression")]
    public ExpressionNode? Expression { get; set; }
}

// Literal nodes

/// <summary>
/// Base class for literal nodes in the AST.
/// </summary>
public abstract class LiteralNode : ExpressionNode { }

/// <summary>
/// Represents an integer literal.
/// </summary>
public class IntegerLiteralNode : LiteralNode
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
}

/// <summary>
/// Represents a floating-point literal.
/// </summary>
public class FloatLiteralNode : LiteralNode
{
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

/// <summary>
/// Represents a string literal.
/// </summary>
public class StringLiteralNode : LiteralNode
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

/// <summary>
/// Represents a symbol literal (#symbol).
/// </summary>
public class SymbolLiteralNode : LiteralNode
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

/// <summary>
/// Represents a character literal ($c).
/// </summary>
public class CharacterLiteralNode : LiteralNode
{
    [JsonPropertyName("value")]
    public char Value { get; set; }
}

/// <summary>
/// Represents an array literal #(elements).
/// </summary>
public class ArrayLiteralNode : LiteralNode
{
    [JsonPropertyName("elements")]
    public List<LiteralNode> Elements { get; set; } = [];
}

/// <summary>
/// Represents a byte array literal #[bytes].
/// </summary>
public class ByteArrayLiteralNode : LiteralNode
{
    [JsonPropertyName("bytes")]
    public List<byte> Bytes { get; set; } = [];
}
