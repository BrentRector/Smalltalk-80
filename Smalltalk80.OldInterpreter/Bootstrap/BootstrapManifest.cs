using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Root structure for bootstrap manifest JSON files.
    /// Supports both nested methods (within class) and flat methods (separate array).
    /// </summary>
    public sealed record BootstrapManifest
    {
        /// <summary>
        /// Version of the bootstrap manifest format.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; init; }

        /// <summary>
        /// Bootstrap phase identifier.
        /// </summary>
        [JsonPropertyName("phase")]
        public string? Phase { get; init; }

        /// <summary>
        /// Human-readable description of this manifest.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// Timestamp when the manifest was generated.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; init; }

        /// <summary>
        /// Optional comments about the manifest contents.
        /// </summary>
        [JsonPropertyName("comments")]
        public List<string> Comments { get; init; } = [];

        /// <summary>
        /// Class definitions in the manifest.
        /// </summary>
        [JsonPropertyName("classes")]
        public List<ClassDefinition> Classes { get; init; } = [];

        /// <summary>
        /// Method definitions in flat format (separate from classes).
        /// </summary>
        [JsonPropertyName("methods")]
        public List<MethodDefinition>? Methods { get; init; }

        /// <summary>
        /// Messages to send during bootstrap (phase 2 style).
        /// </summary>
        [JsonPropertyName("messages")]
        public List<MessageDefinition>? Messages { get; init; }

        /// <summary>
        /// Low-level bootstrap operations (phase 1 style).
        /// </summary>
        [JsonPropertyName("operations")]
        public List<OperationDefinition>? Operations { get; init; }

        /// <summary>
        /// Initialization expressions to execute after loading classes.
        /// These are file-scope expressions from the original Smalltalk source.
        /// </summary>
        [JsonPropertyName("initializationExpressions")]
        public List<InitializationExpression>? InitializationExpressions { get; init; }
    }

    /// <summary>
    /// Represents a class definition in the bootstrap manifest.
    /// </summary>
    public sealed record ClassDefinition
    {
        /// <summary>
        /// Name of the class.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        /// <summary>
        /// Name of the superclass, or "nil" for Object.
        /// </summary>
        [JsonPropertyName("superclass")]
        public string? Superclass { get; init; }

        /// <summary>
        /// Instance variable names for this class.
        /// </summary>
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; init; } = [];

        /// <summary>
        /// Class variable names for this class.
        /// </summary>
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; init; } = [];

        /// <summary>
        /// Pool dictionary names referenced by this class.
        /// </summary>
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; init; } = [];

        /// <summary>
        /// Class format specification (0 for normal, 1 for variable, etc.).
        /// </summary>
        [JsonPropertyName("format")]
        public int Format { get; init; } = 0;

        /// <summary>
        /// Category for this class.
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; init; }

        /// <summary>
        /// Instance methods defined inline with the class.
        /// </summary>
        [JsonPropertyName("methods")]
        public List<MethodDefinition>? Methods { get; init; }

        /// <summary>
        /// Class methods defined inline with the class.
        /// </summary>
        [JsonPropertyName("classMethods")]
        public List<MethodDefinition>? ClassMethods { get; init; }

        /// <summary>
        /// Total instance variable count including inherited variables.
        /// </summary>
        [JsonPropertyName("instanceVariableCount")]
        public int? InstanceVariableCount { get; init; }

        /// <summary>
        /// Initial size for the method dictionary.
        /// </summary>
        [JsonPropertyName("methodDictionarySize")]
        public int? MethodDictionarySize { get; init; }
    }

    /// <summary>
    /// Represents a compiled method definition.
    /// </summary>
    public sealed record MethodDefinition
    {
        /// <summary>
        /// Method selector (message name).
        /// </summary>
        [JsonPropertyName("selector")]
        public required string Selector { get; init; }

        /// <summary>
        /// Class name (for flat method definitions).
        /// </summary>
        [JsonPropertyName("class")]
        public string? ClassName { get; init; }

        /// <summary>
        /// Bytecode array for the method.
        /// </summary>
        [JsonPropertyName("bytecode")]
        public required List<int> Bytecode { get; init; }

        /// <summary>
        /// Literal objects referenced by the method.
        /// </summary>
        [JsonPropertyName("literals")]
        public List<object> Literals { get; init; } = [];

        /// <summary>
        /// Number of arguments the method accepts.
        /// </summary>
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; init; } = 0;

        /// <summary>
        /// Number of temporary variables used by the method.
        /// </summary>
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; init; } = 0;

        /// <summary>
        /// Number of temporary variables (alternate name).
        /// </summary>
        [JsonPropertyName("temporaries")]
        public int? Temporaries { get; init; }

        /// <summary>
        /// Primitive index if this is a primitive method.
        /// </summary>
        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; init; } = 0;

        // Removed duplicate 'primitive' property - standardizing on 'primitiveIndex'

        /// <summary>
        /// Whether this is a class method (true) or instance method (false).
        /// </summary>
        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; init; } = false;

        /// <summary>
        /// Optional source code for the method.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; init; }

        /// <summary>
        /// Method category for organization.
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; init; }

        /// <summary>
        /// Maximum stack depth required by the method.
        /// </summary>
        [JsonPropertyName("stackDepth")]
        public int StackDepth { get; init; } = 0;

        /// <summary>
        /// Gets the effective number of temporaries.
        /// </summary>
        [JsonIgnore]
        public int EffectiveTemporaries => Temporaries ?? NumTemps;

        // Removed EffectivePrimitive - using PrimitiveIndex directly
    }

    /// <summary>
    /// Represents a message send operation during bootstrap.
    /// </summary>
    public sealed record MessageDefinition
    {
        /// <summary>
        /// The receiver of the message.
        /// </summary>
        [JsonPropertyName("receiver")]
        public required string Receiver { get; init; }

        /// <summary>
        /// The message selector to send.
        /// </summary>
        [JsonPropertyName("selector")]
        public required string Selector { get; init; }

        /// <summary>
        /// Arguments for the message.
        /// </summary>
        [JsonPropertyName("arguments")]
        public List<object>? Arguments { get; init; }

        /// <summary>
        /// Variable name to store the result.
        /// </summary>
        [JsonPropertyName("resultName")]
        public string? ResultName { get; init; }

        /// <summary>
        /// Optional comment explaining the message.
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; init; }
    }

    /// <summary>
    /// Represents a low-level bootstrap operation.
    /// </summary>
    public sealed record OperationDefinition
    {
        /// <summary>
        /// The operation type.
        /// </summary>
        [JsonPropertyName("op")]
        public required string Op { get; init; }

        /// <summary>
        /// Target object for the operation.
        /// </summary>
        [JsonPropertyName("target")]
        public string? Target { get; init; }

        /// <summary>
        /// Name parameter for various operations.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        /// <summary>
        /// Value to set.
        /// </summary>
        [JsonPropertyName("value")]
        public object? Value { get; init; }

        /// <summary>
        /// Superclass for class definition.
        /// </summary>
        [JsonPropertyName("superclass")]
        public string? Superclass { get; init; }

        /// <summary>
        /// Instance variables for class definition.
        /// </summary>
        [JsonPropertyName("instanceVariables")]
        public List<string>? InstanceVariables { get; init; }

        /// <summary>
        /// Class variables for class definition.
        /// </summary>
        [JsonPropertyName("classVariables")]
        public List<string>? ClassVariables { get; init; }

        /// <summary>
        /// Method selector.
        /// </summary>
        [JsonPropertyName("selector")]
        public string? Selector { get; init; }

        /// <summary>
        /// Method source code.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; init; }

        /// <summary>
        /// Optional comment.
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; init; }
    }

    /// <summary>
    /// Represents an initialization expression to execute at startup.
    /// These are file-scope expressions from Smalltalk source files.
    /// </summary>
    public sealed record InitializationExpression
    {
        /// <summary>
        /// The bytecode for the initialization expression.
        /// </summary>
        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; init; } = [];
        
        /// <summary>
        /// The literal objects referenced by the expression.
        /// </summary>
        [JsonPropertyName("literals")]
        public List<object> Literals { get; init; } = [];
        
        /// <summary>
        /// Number of temporary variables needed.
        /// </summary>
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; init; } = 0;
        
        /// <summary>
        /// Maximum stack depth required by the expression.
        /// </summary>
        [JsonPropertyName("stackDepth")]
        public int StackDepth { get; init; } = 0;
        
        /// <summary>
        /// The original source text of the expression.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; init; }
        
        /// <summary>
        /// Optional: The receiver of the initialization message (for documentation/sorting).
        /// </summary>
        [JsonPropertyName("receiver")]
        public string? Receiver { get; init; }
        
        /// <summary>
        /// Optional: The selector to send (for documentation).
        /// </summary>
        [JsonPropertyName("selector")]
        public string? Selector { get; init; }
        
        /// <summary>
        /// Optional: Arguments if any (for documentation).
        /// </summary>
        [JsonPropertyName("arguments")]
        public List<string>? Arguments { get; init; }
    }
}