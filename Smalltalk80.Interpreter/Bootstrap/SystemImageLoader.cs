using System.Text.Json;
using System.Text.Json.Serialization;
using Core;
using Core.Memory;
using Microsoft.Extensions.Logging;
using Smalltalk80.Compiler;

namespace Smalltalk80.Interpreter.Bootstrap;

/// <summary>
/// Loads a Smalltalk-80 system image from JSON format.
/// </summary>
public sealed class SystemImageLoader
{
    private readonly ILogger<SystemImageLoader> _logger;
    private readonly Dictionary<string, OOP> _classOops = new();
    private readonly Dictionary<string, OOP> _globalOops = new();
    private readonly Dictionary<string, CompiledMethod> _methods = new();
    
    public SystemImageLoader(ILogger<SystemImageLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a system image from the specified file.
    /// </summary>
    public async Task<SystemImage> LoadFromFileAsync(string filePath)
    {
        _logger.LogInformation("Loading system image from {FilePath}", filePath);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"System image file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Loads a system image from JSON string.
    /// </summary>
    public SystemImage LoadFromJson(string json)
    {
        try
        {
            var imageData = JsonSerializer.Deserialize<SystemImageData>(json) 
                ?? throw new InvalidOperationException("Failed to deserialize system image");

            _logger.LogInformation("Loading {ClassCount} classes", imageData.Classes?.Count ?? 0);

            // First pass: Create all class OOPs
            CreateClassOops(imageData);

            // Second pass: Load all methods
            LoadMethods(imageData);

            // Third pass: Run initialization expressions
            var initExpressions = ExtractInitExpressions(imageData);

            return new SystemImage
            {
                Classes = _classOops,
                Globals = _globalOops,
                Methods = _methods,
                InitializationExpressions = initExpressions
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid system image format", ex);
        }
    }

    private void CreateClassOops(SystemImageData imageData)
    {
        if (imageData.Classes == null) return;

        foreach (var classData in imageData.Classes)
        {
            if (string.IsNullOrEmpty(classData.Name)) continue;

            // Create OOP for the class
            var classOop = OOP.FromPointer((uint)HashCode.Combine(classData.Name));
            _classOops[classData.Name] = classOop;
            _globalOops[classData.Name] = classOop;

            _logger.LogDebug("Created class OOP for {ClassName}: {OOP}", classData.Name, classOop);

            // Also create metaclass OOP
            var metaclassName = $"{classData.Name} class";
            var metaclassOop = OOP.FromPointer((uint)HashCode.Combine(metaclassName));
            _classOops[metaclassName] = metaclassOop;
        }
    }

    private void LoadMethods(SystemImageData imageData)
    {
        if (imageData.Classes == null) return;

        foreach (var classData in imageData.Classes)
        {
            if (classData.Methods == null) continue;

            // Load instance methods
            foreach (var methodData in classData.Methods)
            {
                var method = CreateCompiledMethod(methodData);
                var key = $"{classData.Name}>>{methodData.Selector}";
                _methods[key] = method;
                _logger.LogDebug("Loaded instance method {Key}", key);
            }

            // Load class methods
            if (classData.ClassMethods != null)
            {
                foreach (var methodData in classData.ClassMethods)
                {
                    var method = CreateCompiledMethod(methodData);
                    var key = $"{classData.Name} class>>{methodData.Selector}";
                    _methods[key] = method;
                    _logger.LogDebug("Loaded class method {Key}", key);
                }
            }
        }
    }

    private CompiledMethod CreateCompiledMethod(MethodData methodData)
    {
        // Parse bytecodes
        var bytecodes = ParseBytecodes(methodData.Bytecodes);
        
        // Parse literals
        var literals = ParseLiterals(methodData.Literals);

        return CompiledMethod.Create(
            argumentCount: methodData.ArgumentCount ?? 0,
            temporaryCount: methodData.TemporaryCount ?? 0,
            primitiveIndex: methodData.PrimitiveIndex ?? 0,
            literals: literals,
            bytecodes: bytecodes,
            useLargeContext: methodData.UseLargeContext ?? false
        );
    }

    private byte[] ParseBytecodes(object? bytecodesData)
    {
        if (bytecodesData == null) return Array.Empty<byte>();

        return bytecodesData switch
        {
            JsonElement jsonElement => ParseBytecodeArray(jsonElement),
            string str => ParseBytecodeString(str),
            byte[] bytes => bytes,
            _ => Array.Empty<byte>()
        };
    }

    private byte[] ParseBytecodeArray(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<byte>();
        }

        var bytes = new List<byte>();
        foreach (var element in jsonElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                bytes.Add((byte)element.GetInt32());
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (!string.IsNullOrEmpty(str) && str.StartsWith("0x"))
                {
                    bytes.Add(Convert.ToByte(str[2..], 16));
                }
            }
        }
        return bytes.ToArray();
    }

    private byte[] ParseBytecodeString(string str)
    {
        // Handle space or comma separated hex values
        var parts = str.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        var bytes = new List<byte>();
        
        foreach (var part in parts)
        {
            var cleanPart = part.Trim();
            if (cleanPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                bytes.Add(Convert.ToByte(cleanPart[2..], 16));
            }
            else if (byte.TryParse(cleanPart, out var b))
            {
                bytes.Add(b);
            }
        }
        
        return bytes.ToArray();
    }

    private OOP[] ParseLiterals(object? literalsData)
    {
        if (literalsData == null) return Array.Empty<OOP>();

        var literals = new List<OOP>();

        if (literalsData is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in jsonElement.EnumerateArray())
            {
                literals.Add(ParseLiteral(element));
            }
        }

        return literals.ToArray();
    }

    private OOP ParseLiteral(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => OOP.FromSmallInteger(element.GetInt32()),
            JsonValueKind.String => ParseStringLiteral(element.GetString() ?? ""),
            JsonValueKind.True => OOP.True,
            JsonValueKind.False => OOP.False,
            JsonValueKind.Null => OOP.Nil,
            _ => OOP.Nil
        };
    }

    private OOP ParseStringLiteral(string str)
    {
        // Check for special literals
        return str switch
        {
            "nil" => OOP.Nil,
            "true" => OOP.True,
            "false" => OOP.False,
            _ when str.StartsWith("#") => CreateSymbol(str[1..]),
            _ when int.TryParse(str, out var value) => OOP.FromSmallInteger(value),
            _ => CreateString(str)
        };
    }

    private OOP CreateSymbol(string symbol)
    {
        // Create or lookup symbol
        var key = $"#symbol:{symbol}";
        if (_globalOops.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var oop = OOP.FromPointer((uint)HashCode.Combine(key));
        _globalOops[key] = oop;
        return oop;
    }

    private OOP CreateString(string str)
    {
        // Create string object
        return OOP.FromPointer((uint)HashCode.Combine("string", str));
    }

    private List<InitializationExpression> ExtractInitExpressions(SystemImageData imageData)
    {
        var expressions = new List<InitializationExpression>();

        if (imageData.Classes == null) return expressions;

        foreach (var classData in imageData.Classes)
        {
            // Check for initialization expressions in class
            if (!string.IsNullOrEmpty(classData.InitializationExpression))
            {
                expressions.Add(new InitializationExpression
                {
                    Source = $"{classData.Name ?? "Unknown"}: {classData.InitializationExpression}",
                    Receiver = classData.Name,
                    Selector = "initialize"
                });
            }

            // Check for method initialization expressions
            if (classData.Methods != null)
            {
                foreach (var method in classData.Methods)
                {
                    if (!string.IsNullOrEmpty(method.InitExpression))
                    {
                        expressions.Add(new InitializationExpression
                        {
                            Source = $"{classData.Name ?? "Unknown"}>>#{method.Selector}: {method.InitExpression}",
                            Receiver = classData.Name,
                            Selector = method.Selector
                        });
                    }
                }
            }
        }

        // Sort by dependency order if needed
        return SortByDependencies(expressions);
    }

    private List<InitializationExpression> SortByDependencies(List<InitializationExpression> expressions)
    {
        // Simple sorting - in full implementation would do topological sort
        return expressions.OrderBy(e => GetInitPriority(e.Receiver ?? "")).ToList();
    }

    private int GetInitPriority(string className)
    {
        // Core classes initialize first
        return className switch
        {
            "Object" => 0,
            "Behavior" => 1,
            "ClassDescription" => 2,
            "Metaclass" => 3,
            "Class" => 4,
            _ => 100
        };
    }
}

/// <summary>
/// Represents a loaded system image.
/// </summary>
public sealed class SystemImage
{
    public Dictionary<string, OOP> Classes { get; init; } = new();
    public Dictionary<string, OOP> Globals { get; init; } = new();
    public Dictionary<string, CompiledMethod> Methods { get; init; } = new();
    public List<InitializationExpression> InitializationExpressions { get; init; } = new();
}


// JSON deserialization models
internal sealed class SystemImageData
{
    [JsonPropertyName("classes")]
    public List<ClassData>? Classes { get; set; }

    [JsonPropertyName("globals")]
    public Dictionary<string, object>? Globals { get; set; }
}

internal sealed class ClassData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("superclass")]
    public string? Superclass { get; set; }

    [JsonPropertyName("instanceVariables")]
    public List<string>? InstanceVariables { get; set; }

    [JsonPropertyName("classVariables")]
    public List<string>? ClassVariables { get; set; }

    [JsonPropertyName("methods")]
    public List<MethodData>? Methods { get; set; }

    [JsonPropertyName("classMethods")]
    public List<MethodData>? ClassMethods { get; set; }

    [JsonPropertyName("initializationExpression")]
    public string? InitializationExpression { get; set; }
}

internal sealed class MethodData
{
    [JsonPropertyName("selector")]
    public string? Selector { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("bytecodes")]
    public object? Bytecodes { get; set; }

    [JsonPropertyName("literals")]
    public object? Literals { get; set; }

    [JsonPropertyName("argumentCount")]
    public int? ArgumentCount { get; set; }

    [JsonPropertyName("temporaryCount")]
    public int? TemporaryCount { get; set; }

    [JsonPropertyName("primitiveIndex")]
    public int? PrimitiveIndex { get; set; }

    [JsonPropertyName("useLargeContext")]
    public bool? UseLargeContext { get; set; }

    [JsonPropertyName("initExpression")]
    public string? InitExpression { get; set; }
}