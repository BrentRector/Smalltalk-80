namespace Smalltalk80.Compiler.Compilation.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a compiled Smalltalk class definition with all its metadata.
/// </summary>
/// <param name="Name">The name of the class.</param>
/// <param name="Superclass">The name of the superclass.</param>
public sealed record ClassDefinition(string Name, string Superclass)
{
    /// <summary>
    /// Gets the instance variable names for this class.
    /// </summary>
    public List<string> InstanceVariables { get; init; } = [];

    /// <summary>
    /// Gets the class variable names shared by all instances.
    /// </summary>
    public List<string> ClassVariables { get; init; } = [];

    /// <summary>
    /// Gets the pool dictionaries this class can access.
    /// </summary>
    public List<string> PoolDictionaries { get; init; } = [];

    /// <summary>
    /// Gets the system category this class belongs to.
    /// </summary>
    public string Category { get; init; } = "";

    /// <summary>
    /// Gets the optional class comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets the method selectors defined in this class.
    /// </summary>
    [JsonPropertyName("methods")]
    public List<string> MethodReferences { get; init; } = [];
}

/// <summary>
/// Represents a compiled Smalltalk method with bytecode and metadata.
/// </summary>
public sealed record CompiledMethod
{
    /// <summary>
    /// Gets the name of the class this method belongs to.
    /// </summary>
    [JsonPropertyName("class")]
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the selector (method name) for this method.
    /// </summary>
    public required string Selector { get; init; }

    /// <summary>
    /// Gets the compiled bytecode instructions.
    /// </summary>
    public required IReadOnlyList<byte> Bytecode { get; init; }

    /// <summary>
    /// Gets the literal pool for this method.
    /// </summary>
    public required IReadOnlyList<object> Literals { get; init; }

    /// <summary>
    /// Gets the primitive index if this is a primitive method.
    /// </summary>
    public int PrimitiveIndex { get; init; }

    /// <summary>
    /// Gets the number of arguments this method accepts.
    /// </summary>
    public required int NumArgs { get; init; }

    /// <summary>
    /// Gets the number of temporary variables in this method.
    /// </summary>
    public required int NumTemps { get; init; }

    /// <summary>
    /// Gets the maximum stack depth required by this method.
    /// </summary>
    public required int StackDepth { get; init; }

    /// <summary>
    /// Gets whether this is a class method (vs. instance method).
    /// </summary>
    public bool IsClassMethod { get; init; }

    /// <summary>
    /// Gets the protocol (category) this method belongs to.
    /// </summary>
    public string Protocol { get; init; } = "unclassified";

    /// <summary>
    /// Gets the original source code of the method.
    /// </summary>
    public string? SourceCode { get; init; }

    /// <summary>
    /// Gets the names of the method arguments for debugging.
    /// </summary>
    public IReadOnlyList<string> ArgumentNames { get; init; } = [];

    /// <summary>
    /// Gets the names of the temporary variables for debugging.
    /// </summary>
    public IReadOnlyList<string> TemporaryNames { get; init; } = [];
}

/// <summary>
/// Represents a compiled initialization expression to be executed at system startup.
/// </summary>
public sealed record InitializationExpression
{
    /// <summary>
    /// Gets the compiled bytecode for this expression.
    /// </summary>
    public required IReadOnlyList<byte> Bytecode { get; init; }

    /// <summary>
    /// Gets the literals used in this expression.
    /// </summary>
    public required IReadOnlyList<object> Literals { get; init; }

    /// <summary>
    /// Gets the number of temporary variables needed.
    /// </summary>
    public required int NumTemps { get; init; }

    /// <summary>
    /// Gets the maximum stack depth required.
    /// </summary>
    public required int StackDepth { get; init; }

    /// <summary>
    /// Gets the source code of the expression.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the receiver of the message send, if applicable.
    /// </summary>
    public string? Receiver { get; init; }

    /// <summary>
    /// Gets the selector of the message send, if applicable.
    /// </summary>
    public string? Selector { get; init; }

    /// <summary>
    /// Gets the arguments of the message send, if applicable.
    /// </summary>
    public IReadOnlyList<string>? Arguments { get; init; }
}

/// <summary>
/// Represents a symbol literal in Smalltalk.
/// </summary>
/// <param name="Name">The symbol name without the # prefix.</param>
public sealed record Symbol(string Name)
{
    /// <summary>
    /// Returns the string representation of this symbol.
    /// </summary>
    public override string ToString() => $"#{Name}";
}

/// <summary>
/// Represents an association (key-value pair) in Smalltalk.
/// </summary>
/// <param name="Key">The association key.</param>
/// <param name="Value">The associated value.</param>
public sealed record Association(string Key, object? Value);

/// <summary>
/// Represents the complete output of the system compiler.
/// </summary>
public sealed record SystemCompilationOutput
{
    /// <summary>
    /// Gets the schema reference for JSON validation.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "./bootstrap-schema.json";

    /// <summary>
    /// Gets the compilation description with timestamp.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets all compiled class definitions.
    /// </summary>
    public required IReadOnlyList<object> Classes { get; init; }

    /// <summary>
    /// Gets all compiled methods.
    /// </summary>
    public required IReadOnlyList<object> Methods { get; init; }

    /// <summary>
    /// Gets all initialization expressions.
    /// </summary>
    public required IReadOnlyList<object> InitializationExpressions { get; init; }
}