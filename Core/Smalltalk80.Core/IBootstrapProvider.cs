namespace Smalltalk80.Core;

/// <summary>
/// Provides bootstrap data for initializing the Smalltalk VM.
/// Implementations can load data from JSON files, embedded resources, or other sources.
/// </summary>
public interface IBootstrapProvider
{
    /// <summary>
    /// Loads the Phase 1 bootstrap data (VM primitives for core class creation).
    /// </summary>
    /// <returns>The Phase 1 bootstrap configuration.</returns>
    Task<Phase1BootstrapData> LoadPhase1Async();

    /// <summary>
    /// Loads the Phase 2 bootstrap data (messaging-based class and method creation).
    /// </summary>
    /// <returns>The Phase 2 bootstrap configuration.</returns>
    Task<Phase2BootstrapData> LoadPhase2Async();

    /// <summary>
    /// Loads the Phase 3 bootstrap data (file loading for complete system).
    /// </summary>
    /// <returns>The Phase 3 bootstrap configuration.</returns>
    Task<Phase3BootstrapData> LoadPhase3Async();

    /// <summary>
    /// Validates that the bootstrap data is consistent and complete.
    /// </summary>
    /// <returns>Validation results with any errors or warnings.</returns>
    Task<BootstrapValidationResult> ValidateAsync();
}

/// <summary>
/// Contains Phase 1 bootstrap operations using VM primitives.
/// </summary>
public record Phase1BootstrapData
{
    /// <summary>
    /// Gets the list of VM primitive operations to execute.
    /// </summary>
    public IReadOnlyList<PrimitiveOperation> Operations { get; init; } = Array.Empty<PrimitiveOperation>();

    /// <summary>
    /// Gets the validation checkpoints to verify after bootstrap.
    /// </summary>
    public IReadOnlyList<ValidationCheckpoint> Checkpoints { get; init; } = Array.Empty<ValidationCheckpoint>();
}

/// <summary>
/// Represents a single VM primitive operation in Phase 1 bootstrap.
/// </summary>
public record PrimitiveOperation
{
    /// <summary>
    /// Gets the operation type (e.g., "newClass", "setSlot", "installMethod").
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target object reference for operations that modify existing objects.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets the result variable name for operations that create new objects.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// Gets operation-specific parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets an optional comment describing this operation.
    /// </summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Contains Phase 2 bootstrap operations using Smalltalk messaging.
/// </summary>
public record Phase2BootstrapData
{
    /// <summary>
    /// Gets the list of class definitions to create via subclass: messages.
    /// </summary>
    public IReadOnlyList<ClassDefinition> Classes { get; init; } = Array.Empty<ClassDefinition>();

    /// <summary>
    /// Gets the list of methods to compile and install.
    /// </summary>
    public IReadOnlyList<MethodDefinition> Methods { get; init; } = Array.Empty<MethodDefinition>();

    /// <summary>
    /// Gets the list of global objects to instantiate.
    /// </summary>
    public IReadOnlyList<GlobalDefinition> Globals { get; init; } = Array.Empty<GlobalDefinition>();

    /// <summary>
    /// Gets the validation checkpoints.
    /// </summary>
    public IReadOnlyList<ValidationCheckpoint> Checkpoints { get; init; } = Array.Empty<ValidationCheckpoint>();
}

/// <summary>
/// Defines a class to be created in Phase 2.
/// </summary>
public record ClassDefinition
{
    /// <summary>
    /// Gets the name of the class.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the superclass name.
    /// </summary>
    public string Superclass { get; init; } = "Object";

    /// <summary>
    /// Gets the instance variable names.
    /// </summary>
    public IReadOnlyList<string> InstanceVariables { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the class variable names.
    /// </summary>
    public IReadOnlyList<string> ClassVariables { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the pool dictionary names.
    /// </summary>
    public IReadOnlyList<string> PoolDictionaries { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the class category.
    /// </summary>
    public string Category { get; init; } = "Unclassified";
}

/// <summary>
/// Defines a method to be compiled and installed.
/// </summary>
public record MethodDefinition
{
    /// <summary>
    /// Gets the class name where the method will be installed.
    /// </summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this is a class method (true) or instance method (false).
    /// </summary>
    public bool IsClassMethod { get; init; }

    /// <summary>
    /// Gets the method selector (name).
    /// </summary>
    public string Selector { get; init; } = string.Empty;

    /// <summary>
    /// Gets the method source code.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets the method category for organization.
    /// </summary>
    public string Category { get; init; } = "unclassified";
}

/// <summary>
/// Defines a global object to be created.
/// </summary>
public record GlobalDefinition
{
    /// <summary>
    /// Gets the global variable name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the class to instantiate.
    /// </summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this is a singleton (like nil, true, false).
    /// </summary>
    public bool IsSingleton { get; init; }
}

/// <summary>
/// Contains Phase 3 bootstrap configuration for loading files.
/// </summary>
public record Phase3BootstrapData
{
    /// <summary>
    /// Gets the list of source files to load in order.
    /// </summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the load order for class categories.
    /// </summary>
    public IReadOnlyList<string> LoadOrder { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets configuration options for the file loader.
    /// </summary>
    public FileLoaderOptions Options { get; init; } = new();
}

/// <summary>
/// Options for loading Smalltalk source files.
/// </summary>
public record FileLoaderOptions
{
    /// <summary>
    /// Gets whether to stop on compilation errors.
    /// </summary>
    public bool StopOnError { get; init; } = true;

    /// <summary>
    /// Gets whether to log loaded classes and methods.
    /// </summary>
    public bool EnableLogging { get; init; } = true;

    /// <summary>
    /// Gets whether to validate loaded code.
    /// </summary>
    public bool ValidateAfterLoad { get; init; } = true;
}

/// <summary>
/// Represents a validation checkpoint to verify bootstrap success.
/// </summary>
public record ValidationCheckpoint
{
    /// <summary>
    /// Gets the description of what is being validated.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the validation test expression.
    /// </summary>
    public string Test { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this checkpoint is critical (failure stops bootstrap).
    /// </summary>
    public bool IsCritical { get; init; } = true;
}

/// <summary>
/// Results from validating bootstrap data.
/// </summary>
public record BootstrapValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets detailed validation information.
    /// </summary>
    public IReadOnlyDictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}