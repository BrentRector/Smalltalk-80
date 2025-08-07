using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Core;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Provides bootstrap data from JSON files for initializing the Smalltalk VM.
/// This implementation reads bootstrap_phase1.json, bootstrap_phase2.json, and bootstrap_phase3.json
/// to configure the three-phase bootstrap process.
/// </summary>
public class JsonBootstrapProvider : IBootstrapProvider
{
    private readonly string _bootstrapDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<JsonBootstrapProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the JsonBootstrapProvider class.
    /// </summary>
    /// <param name="bootstrapDirectory">The directory containing bootstrap JSON files.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public JsonBootstrapProvider(string bootstrapDirectory, ILogger<JsonBootstrapProvider>? logger = null)
    {
        _bootstrapDirectory = bootstrapDirectory ?? throw new ArgumentNullException(nameof(bootstrapDirectory));
        _logger = logger;
        
        if (!Directory.Exists(bootstrapDirectory))
        {
            throw new DirectoryNotFoundException($"Bootstrap directory not found: {bootstrapDirectory}");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <inheritdoc/>
    public async Task<Phase1BootstrapData> LoadPhase1Async()
    {
        var filePath = Path.Combine(_bootstrapDirectory, "bootstrap_phase1.json");
        _logger?.LogInformation("Loading Phase 1 bootstrap from {FilePath}", filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<Phase1JsonData>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Phase 1 bootstrap data");

        var operations = new List<PrimitiveOperation>();
        foreach (var op in data.Operations)
        {
            operations.Add(ConvertToPrimitiveOperation(op));
        }

        var checkpoints = data.Validation?.Checkpoints?.Select(c => new ValidationCheckpoint
        {
            Description = c.Description ?? string.Empty,
            Test = c.Test ?? string.Empty,
            IsCritical = c.IsCritical ?? true
        }).ToList() ?? new List<ValidationCheckpoint>();

        _logger?.LogInformation("Loaded {Count} Phase 1 operations", operations.Count);

        return new Phase1BootstrapData
        {
            Operations = operations,
            Checkpoints = checkpoints
        };
    }

    /// <inheritdoc/>
    public async Task<Phase2BootstrapData> LoadPhase2Async()
    {
        var filePath = Path.Combine(_bootstrapDirectory, "bootstrap_phase2.json");
        _logger?.LogInformation("Loading Phase 2 bootstrap from {FilePath}", filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<Phase2JsonData>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Phase 2 bootstrap data");

        var classes = new List<ClassDefinition>();
        var methods = new List<MethodDefinition>();
        var globals = new List<GlobalDefinition>();

        foreach (var op in data.Operations)
        {
            switch (op.Op?.ToLower())
            {
                case "subclass":
                    classes.Add(new ClassDefinition
                    {
                        Name = op.Name ?? string.Empty,
                        Superclass = op.Superclass ?? "Object",
                        InstanceVariables = op.InstanceVariables ?? Array.Empty<string>(),
                        ClassVariables = op.ClassVariables ?? Array.Empty<string>(),
                        PoolDictionaries = op.PoolDictionaries ?? Array.Empty<string>(),
                        Category = op.Category ?? "Unclassified"
                    });
                    break;

                case "compilemethod":
                    methods.Add(new MethodDefinition
                    {
                        ClassName = op.Class ?? string.Empty,
                        IsClassMethod = false,
                        Selector = op.Selector ?? string.Empty,
                        Source = op.Source ?? string.Empty,
                        Category = op.Category ?? "unclassified"
                    });
                    break;

                case "compileclassmethod":
                    methods.Add(new MethodDefinition
                    {
                        ClassName = op.Class ?? string.Empty,
                        IsClassMethod = true,
                        Selector = op.Selector ?? string.Empty,
                        Source = op.Source ?? string.Empty,
                        Category = op.Category ?? "unclassified"
                    });
                    break;

                case "instantiate":
                    globals.Add(new GlobalDefinition
                    {
                        Name = op.Global ?? string.Empty,
                        ClassName = op.Class ?? string.Empty,
                        IsSingleton = op.IsSingleton ?? false
                    });
                    break;
            }
        }

        var checkpoints = data.Validation?.Checkpoints?.Select(c => new ValidationCheckpoint
        {
            Description = c.Description ?? string.Empty,
            Test = c.Test ?? string.Empty,
            IsCritical = c.IsCritical ?? true
        }).ToList() ?? new List<ValidationCheckpoint>();

        _logger?.LogInformation("Loaded {ClassCount} classes, {MethodCount} methods, {GlobalCount} globals for Phase 2",
            classes.Count, methods.Count, globals.Count);

        return new Phase2BootstrapData
        {
            Classes = classes,
            Methods = methods,
            Globals = globals,
            Checkpoints = checkpoints
        };
    }

    /// <inheritdoc/>
    public async Task<Phase3BootstrapData> LoadPhase3Async()
    {
        var filePath = Path.Combine(_bootstrapDirectory, "bootstrap_phase3.json");
        
        // Phase 3 is optional
        if (!File.Exists(filePath))
        {
            _logger?.LogInformation("Phase 3 bootstrap file not found, returning empty configuration");
            return new Phase3BootstrapData();
        }

        _logger?.LogInformation("Loading Phase 3 bootstrap from {FilePath}", filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<Phase3JsonData>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Phase 3 bootstrap data");

        return new Phase3BootstrapData
        {
            SourceFiles = data.SourceFiles ?? Array.Empty<string>(),
            LoadOrder = data.LoadOrder ?? Array.Empty<string>(),
            Options = new FileLoaderOptions
            {
                StopOnError = data.Configuration?.StopOnError ?? true,
                EnableLogging = data.Configuration?.EnableLogging ?? true,
                ValidateAfterLoad = data.Configuration?.ValidateAfterLoad ?? true
            }
        };
    }

    /// <inheritdoc/>
    public async Task<BootstrapValidationResult> ValidateAsync()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var details = new Dictionary<string, object>();

        // Validate Phase 1
        try
        {
            var phase1 = await LoadPhase1Async();
            details["Phase1Operations"] = phase1.Operations.Count;
            
            // Check for required operations
            var hasMetaclass = phase1.Operations.Any(op => 
                op.Operation == "newClass" && op.Result == "Metaclass");
            var hasProtoObject = phase1.Operations.Any(op => 
                op.Operation == "newClass" && op.Result == "ProtoObject");
            
            if (!hasMetaclass)
                errors.Add("Phase 1: Missing Metaclass creation");
            if (!hasProtoObject)
                errors.Add("Phase 1: Missing ProtoObject creation");
        }
        catch (Exception ex)
        {
            errors.Add($"Phase 1 validation failed: {ex.Message}");
        }

        // Validate Phase 2
        try
        {
            var phase2 = await LoadPhase2Async();
            details["Phase2Classes"] = phase2.Classes.Count;
            details["Phase2Methods"] = phase2.Methods.Count;
            
            // Check for required classes
            var requiredClasses = new[] { "Boolean", "True", "False", "UndefinedObject", "BlockContext" };
            foreach (var required in requiredClasses)
            {
                if (!phase2.Classes.Any(c => c.Name == required))
                {
                    warnings.Add($"Phase 2: Missing required class {required}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Phase 2 validation failed: {ex.Message}");
        }

        // Validate Phase 3 (optional)
        try
        {
            var phase3 = await LoadPhase3Async();
            details["Phase3Files"] = phase3.SourceFiles.Count;
        }
        catch (Exception ex)
        {
            warnings.Add($"Phase 3 validation warning: {ex.Message}");
        }

        return new BootstrapValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Details = details
        };
    }

    private PrimitiveOperation ConvertToPrimitiveOperation(Phase1Operation op)
    {
        var parameters = new Dictionary<string, object>();
        
        // Extract common parameters
        if (op.Slot.HasValue)
            parameters["slot"] = op.Slot.Value;
        if (!string.IsNullOrEmpty(op.Name))
            parameters["name"] = op.Name;
        if (!string.IsNullOrEmpty(op.Value))
            parameters["value"] = op.Value;
        if (!string.IsNullOrEmpty(op.Selector))
            parameters["selector"] = op.Selector;
        if (!string.IsNullOrEmpty(op.Source))
            parameters["source"] = op.Source;
        if (!string.IsNullOrEmpty(op.Key))
            parameters["key"] = op.Key;
        if (!string.IsNullOrEmpty(op.Class))
            parameters["class"] = op.Class;
        if (!string.IsNullOrEmpty(op.Global))
            parameters["global"] = op.Global;

        // Handle nested operations (for values that are operations themselves)
        if (op.ValueOp != null)
        {
            parameters["valueOperation"] = new Dictionary<string, object>
            {
                ["op"] = op.ValueOp.Op ?? string.Empty,
                ["size"] = op.ValueOp.Size ?? 0
            };
        }

        return new PrimitiveOperation
        {
            Operation = op.Op ?? string.Empty,
            Target = op.Target,
            Result = op.Result,
            Parameters = parameters,
            Comment = op.Comment ?? op.Comment2
        };
    }

    // JSON deserialization classes
    private class Phase1JsonData
    {
        public string? Phase { get; set; }
        public string? Description { get; set; }
        public List<Phase1Operation>? Operations { get; set; }
        public ValidationData? Validation { get; set; }
    }

    private class Phase1Operation
    {
        public string? Op { get; set; }
        public string? Target { get; set; }
        public string? Result { get; set; }
        public int? Slot { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Selector { get; set; }
        public string? Source { get; set; }
        public string? Key { get; set; }
        public string? Class { get; set; }
        public string? Global { get; set; }
        public string? Comment { get; set; }
        public string? Comment2 { get; set; }
        public ValueOperation? ValueOp { get; set; }
    }

    private class ValueOperation
    {
        public string? Op { get; set; }
        public int? Size { get; set; }
    }

    private class Phase2JsonData
    {
        public string? Phase { get; set; }
        public string? Description { get; set; }
        public List<Phase2Operation>? Operations { get; set; }
        public ValidationData? Validation { get; set; }
    }

    private class Phase2Operation
    {
        public string? Op { get; set; }
        public string? Superclass { get; set; }
        public string? Name { get; set; }
        public string? Class { get; set; }
        public string? Selector { get; set; }
        public string? Source { get; set; }
        public string? Global { get; set; }
        public string? Category { get; set; }
        public string? Comment { get; set; }
        public bool? IsSingleton { get; set; }
        public List<string>? InstanceVariables { get; set; }
        public List<string>? ClassVariables { get; set; }
        public List<string>? PoolDictionaries { get; set; }
    }

    private class Phase3JsonData
    {
        public string? Phase { get; set; }
        public string? Description { get; set; }
        public List<string>? SourceFiles { get; set; }
        public List<string>? LoadOrder { get; set; }
        public Phase3Configuration? Configuration { get; set; }
    }

    private class Phase3Configuration
    {
        public bool? StopOnError { get; set; }
        public bool? EnableLogging { get; set; }
        public bool? ValidateAfterLoad { get; set; }
    }

    private class ValidationData
    {
        public List<CheckpointData>? Checkpoints { get; set; }
    }

    private class CheckpointData
    {
        public string? Description { get; set; }
        public string? Test { get; set; }
        public bool? IsCritical { get; set; }
    }
}

/// <summary>
/// Logging interface for bootstrap operations.
/// </summary>
public interface ILogger<T>
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, params object[] args);
}