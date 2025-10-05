using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.ColdStart;

/// <summary>
/// Loads and manages a Smalltalk-80 cold start bundle from JSON format.
/// Provides methods to parse the JSON, validate schema version, and map localIds to interpreter object references.
/// </summary>
public class ColdStartJsonLoader
{
    private readonly ColdStartBundle _bundle;
    private readonly Dictionary<string, SObjectRef> _localIdToOop = new();
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the ColdStartJsonLoader class.
    /// </summary>
    /// <param name="jsonPath">Path to the cold start JSON file.</param>
    /// <exception cref="FileNotFoundException">Thrown when the JSON file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when JSON deserialization fails or schema version is unsupported.</exception>
    public ColdStartJsonLoader(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Cold start JSON file not found: {jsonPath}", jsonPath);

        // Configure JSON options for polymorphic deserialization
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Read and deserialize the JSON
        string json = File.ReadAllText(jsonPath);
        _bundle = JsonSerializer.Deserialize<ColdStartBundle>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize cold start JSON");

        ValidateSchema();
        ValidateCounts();
    }

    /// <summary>
    /// Gets the loaded cold start bundle.
    /// </summary>
    public ColdStartBundle Bundle => _bundle;

    /// <summary>
    /// Gets the collection of symbols from the bundle.
    /// </summary>
    public string[] Symbols => _bundle.Symbols;

    /// <summary>
    /// Gets the collection of strings from the bundle.
    /// </summary>
    public string[] Strings => _bundle.Strings;

    /// <summary>
    /// Gets the collection of classes from the bundle.
    /// </summary>
    public JsonClass[] Classes => _bundle.Classes;

    /// <summary>
    /// Gets the collection of objects from the bundle.
    /// </summary>
    public JsonObject[] Objects => _bundle.Objects;

    /// <summary>
    /// Gets the collection of methods from the bundle.
    /// </summary>
    public JsonMethod[] Methods => _bundle.Methods;

    /// <summary>
    /// Gets the collection of initialization expressions from the bundle.
    /// </summary>
    public JsonInitExpression[] InitExpressions => _bundle.InitExpressions;

    /// <summary>
    /// Gets the installation order specification from the bundle.
    /// </summary>
    public InstallOrder InstallOrder => _bundle.InstallOrder;

    /// <summary>
    /// Gets the collection of patches from the bundle.
    /// </summary>
    public JsonPatch[] Patches => _bundle.Patches;

    /// <summary>
    /// Gets the optimized selector indices from the bundle.
    /// </summary>
    public int[] OptimizedSelectors => _bundle.OptimizedSelectors;

    /// <summary>
    /// Registers a mapping from a localId to an object reference.
    /// </summary>
    /// <param name="localId">The localId from the JSON bundle.</param>
    /// <param name="oop">The interpreter object reference.</param>
    /// <exception cref="ArgumentException">Thrown when the localId is already registered.</exception>
    public void RegisterLocalId(string localId, SObjectRef oop)
    {
        if (_localIdToOop.ContainsKey(localId))
            throw new ArgumentException($"LocalId already registered: {localId}", nameof(localId));

        _localIdToOop[localId] = oop;
    }

    /// <summary>
    /// Attempts to register a mapping from a localId to an object reference.
    /// </summary>
    /// <param name="localId">The localId from the JSON bundle.</param>
    /// <param name="oop">The interpreter object reference.</param>
    /// <returns>True if the registration succeeded; false if the localId was already registered.</returns>
    public bool TryRegisterLocalId(string localId, SObjectRef oop)
    {
        return _localIdToOop.TryAdd(localId, oop);
    }

    /// <summary>
    /// Resolves a localId to its corresponding object reference.
    /// </summary>
    /// <param name="localId">The localId to resolve.</param>
    /// <returns>The corresponding object reference.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the localId is not found.</exception>
    public SObjectRef ResolveLocalId(string localId)
    {
        if (_localIdToOop.TryGetValue(localId, out var oop))
            return oop;

        throw new InvalidOperationException($"LocalId not found: {localId}. Ensure the object has been registered before attempting to resolve.");
    }

    /// <summary>
    /// Attempts to resolve a localId to its corresponding object reference.
    /// </summary>
    /// <param name="localId">The localId to resolve.</param>
    /// <param name="oop">When successful, contains the corresponding object reference.</param>
    /// <returns>True if the localId was found and resolved; false otherwise.</returns>
    public bool TryResolveLocalId(string localId, out SObjectRef oop)
    {
        return _localIdToOop.TryGetValue(localId, out oop);
    }

    /// <summary>
    /// Checks if a localId has been registered.
    /// </summary>
    /// <param name="localId">The localId to check.</param>
    /// <returns>True if the localId has been registered; false otherwise.</returns>
    public bool HasLocalId(string localId)
    {
        return _localIdToOop.ContainsKey(localId);
    }

    /// <summary>
    /// Gets a symbol by its index.
    /// </summary>
    /// <param name="index">The symbol index.</param>
    /// <returns>The symbol string.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is invalid.</exception>
    public string GetSymbol(int index)
    {
        if (index < 0 || index >= _bundle.Symbols.Length)
            throw new IndexOutOfRangeException($"Symbol index {index} is out of range [0, {_bundle.Symbols.Length})");

        return _bundle.Symbols[index];
    }

    /// <summary>
    /// Gets a string by its index.
    /// </summary>
    /// <param name="index">The string index.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is invalid.</exception>
    public string GetString(int index)
    {
        if (index < 0 || index >= _bundle.Strings.Length)
            throw new IndexOutOfRangeException($"String index {index} is out of range [0, {_bundle.Strings.Length})");

        return _bundle.Strings[index];
    }

    /// <summary>
    /// Finds a class by its localId.
    /// </summary>
    /// <param name="localId">The class localId.</param>
    /// <returns>The class descriptor, or null if not found.</returns>
    public JsonClass? FindClass(string localId)
    {
        return _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
    }

    /// <summary>
    /// Finds an object by its localId.
    /// </summary>
    /// <param name="localId">The object localId.</param>
    /// <returns>The object descriptor, or null if not found.</returns>
    public JsonObject? FindObject(string localId)
    {
        return _bundle.Objects.FirstOrDefault(o => o.LocalId == localId);
    }

    /// <summary>
    /// Finds a method by its localId.
    /// </summary>
    /// <param name="localId">The method localId.</param>
    /// <returns>The method descriptor, or null if not found.</returns>
    public JsonMethod? FindMethod(string localId)
    {
        return _bundle.Methods.FirstOrDefault(m => m.LocalId == localId);
    }

    /// <summary>
    /// Gets all methods for a specific class or metaclass.
    /// </summary>
    /// <param name="classLocalId">The class or metaclass localId.</param>
    /// <returns>Collection of methods belonging to the class.</returns>
    public IEnumerable<JsonMethod> GetMethodsForClass(string classLocalId)
    {
        return _bundle.Methods.Where(m => m.HomeClassLocalId == classLocalId);
    }

    /// <summary>
    /// Gets the metadata for the bundle.
    /// </summary>
    public Metadata Metadata => _bundle.Metadata;

    /// <summary>
    /// Clears all localId to OOP mappings.
    /// </summary>
    public void ClearMappings()
    {
        _localIdToOop.Clear();
    }

    /// <summary>
    /// Gets statistics about the loaded bundle.
    /// </summary>
    /// <returns>A dictionary containing bundle statistics.</returns>
    public Dictionary<string, int> GetStatistics()
    {
        return new Dictionary<string, int>
        {
            ["Symbols"] = _bundle.Symbols.Length,
            ["Strings"] = _bundle.Strings.Length,
            ["Classes"] = _bundle.Classes.Length,
            ["Objects"] = _bundle.Objects.Length,
            ["Methods"] = _bundle.Methods.Length,
            ["InitExpressions"] = _bundle.InitExpressions.Length,
            ["Patches"] = _bundle.Patches.Length,
            ["OptimizedSelectors"] = _bundle.OptimizedSelectors.Length,
            ["InstallPhases"] = _bundle.InstallOrder.Phases.Length,
            ["RegisteredLocalIds"] = _localIdToOop.Count
        };
    }

    /// <summary>
    /// Validates the schema version is supported.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the schema version is unsupported.</exception>
    private void ValidateSchema()
    {
        const string supportedVersion = "1.0.0";

        if (_bundle.Metadata.Version != supportedVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported cold start schema version: {_bundle.Metadata.Version}. " +
                $"This loader supports version {supportedVersion} only.");
        }
    }

    /// <summary>
    /// Validates the counts in metadata match the actual arrays.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when counts don't match.</exception>
    private void ValidateCounts()
    {
        var errors = new List<string>();

        if (_bundle.Metadata.SymbolCount != _bundle.Symbols.Length)
            errors.Add($"Symbol count mismatch: metadata={_bundle.Metadata.SymbolCount}, actual={_bundle.Symbols.Length}");

        if (_bundle.Metadata.StringCount != _bundle.Strings.Length)
            errors.Add($"String count mismatch: metadata={_bundle.Metadata.StringCount}, actual={_bundle.Strings.Length}");

        if (_bundle.Metadata.ClassCount != _bundle.Classes.Length)
            errors.Add($"Class count mismatch: metadata={_bundle.Metadata.ClassCount}, actual={_bundle.Classes.Length}");

        if (_bundle.Metadata.MethodCount != _bundle.Methods.Length)
            errors.Add($"Method count mismatch: metadata={_bundle.Metadata.MethodCount}, actual={_bundle.Methods.Length}");

        if (_bundle.Metadata.ObjectCount != _bundle.Objects.Length)
            errors.Add($"Object count mismatch: metadata={_bundle.Metadata.ObjectCount}, actual={_bundle.Objects.Length}");

        if (_bundle.Metadata.InitExpressionCount != _bundle.InitExpressions.Length)
            errors.Add($"Init expression count mismatch: metadata={_bundle.Metadata.InitExpressionCount}, actual={_bundle.InitExpressions.Length}");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cold start bundle validation failed:\n" + string.Join("\n", errors));
        }
    }

    /// <summary>
    /// Performs optional validation based on the validation rules in the bundle.
    /// </summary>
    /// <returns>List of validation errors, empty if all validations pass.</returns>
    public List<string> ValidateBundle()
    {
        var errors = new List<string>();
        var rules = _bundle.ValidationRules;

        if (rules == null)
            return errors;

        if (rules.AllLocalIdsUnique)
        {
            var allLocalIds = new HashSet<string>();

            // Check classes
            foreach (var cls in _bundle.Classes)
            {
                if (!allLocalIds.Add(cls.LocalId))
                    errors.Add($"Duplicate localId found: {cls.LocalId}");
            }

            // Check objects
            foreach (var obj in _bundle.Objects)
            {
                if (!allLocalIds.Add(obj.LocalId))
                    errors.Add($"Duplicate localId found: {obj.LocalId}");
            }

            // Check methods
            foreach (var method in _bundle.Methods)
            {
                if (!allLocalIds.Add(method.LocalId))
                    errors.Add($"Duplicate localId found: {method.LocalId}");
            }

            // Check init expressions
            foreach (var expr in _bundle.InitExpressions)
            {
                if (!allLocalIds.Add(expr.LocalId))
                    errors.Add($"Duplicate localId found: {expr.LocalId}");
            }
        }

        if (rules.AllSymbolIndicesValid)
        {
            // Validate all symbol references
            foreach (var cls in _bundle.Classes)
            {
                if (cls.NameIndex < 0 || cls.NameIndex >= _bundle.Symbols.Length)
                    errors.Add($"Invalid symbol index in class {cls.LocalId}: {cls.NameIndex}");

                foreach (var idx in cls.InstVarNames)
                {
                    if (idx < 0 || idx >= _bundle.Symbols.Length)
                        errors.Add($"Invalid symbol index in class {cls.LocalId} instVarNames: {idx}");
                }

                foreach (var idx in cls.ClassVarNames)
                {
                    if (idx < 0 || idx >= _bundle.Symbols.Length)
                        errors.Add($"Invalid symbol index in class {cls.LocalId} classVarNames: {idx}");
                }
            }
        }

        if (rules.InstallOrderCoversAllEntities)
        {
            var installedLocalIds = new HashSet<string>();

            foreach (var phase in _bundle.InstallOrder.Phases)
            {
                foreach (var localId in phase.LocalIds)
                {
                    installedLocalIds.Add(localId);
                }
            }

            // Check all entities are in install order
            foreach (var cls in _bundle.Classes)
            {
                if (!installedLocalIds.Contains(cls.LocalId))
                    errors.Add($"Class {cls.LocalId} not in install order");
            }

            foreach (var obj in _bundle.Objects)
            {
                if (!installedLocalIds.Contains(obj.LocalId))
                    errors.Add($"Object {obj.LocalId} not in install order");
            }

            foreach (var method in _bundle.Methods)
            {
                if (!installedLocalIds.Contains(method.LocalId))
                    errors.Add($"Method {method.LocalId} not in install order");
            }
        }

        return errors;
    }
}