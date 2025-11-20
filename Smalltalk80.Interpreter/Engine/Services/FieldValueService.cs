using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides field value resolution services for cold start phases.
/// Handles common value types without object allocation.
/// </summary>
/// <remarks>
/// This service does NOT allocate new objects - it only resolves existing values
/// to OOPs. Object allocation (Float, String, Character, ByteArray) remains in
/// phase-specific code to ensure proper class reference tracking.
/// </remarks>
public class FieldValueService
{
    private readonly ColdStartBundle _bundle;
    private readonly SymbolService _symbolService;

    /// <summary>
    /// Initializes a new instance of the FieldValueService class.
    /// </summary>
    /// <param name="bundle">The cold start bundle for data access.</param>
    /// <param name="symbolService">The symbol service for symbol lookups.</param>
    public FieldValueService(ColdStartBundle bundle, SymbolService symbolService)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
    }

    /// <summary>
    /// Resolves a simple field value (nil, bool, smallInt, symbolRef, objectRef) to an OOP.
    /// Does NOT handle allocation-requiring types (float, string, character, bytes).
    /// </summary>
    /// <param name="value">The field value to resolve.</param>
    /// <param name="localIdToObjectId">The mapping from local IDs to allocated OOPs.</param>
    /// <returns>The resolved OOP, or null if the type requires allocation.</returns>
    public SmalltalkOOP? ResolveSimpleValue(FieldValue value, Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "symbolRef" => _symbolService.GetSymbolOOPByIndex(value.Index!.Value),
            "objectRef" => localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP)
                ? objOOP
                : SpecialObjectsArray.Instance.Nil,
            _ => null // Requires allocation - return null to signal caller should handle
        };
    }

    /// <summary>
    /// Gets a symbol OOP by name using the symbol service.
    /// </summary>
    /// <param name="name">The symbol name.</param>
    /// <returns>The symbol OOP.</returns>
    public SmalltalkOOP GetSymbolOOP(string name)
    {
        return _symbolService.GetSymbolOOP(name);
    }

    /// <summary>
    /// Gets a symbol OOP by index.
    /// </summary>
    /// <param name="index">The symbol index.</param>
    /// <returns>The symbol OOP.</returns>
    public SmalltalkOOP GetSymbolOOPByIndex(int index)
    {
        return _symbolService.GetSymbolOOPByIndex(index);
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The integer value.</returns>
    public int GetIntValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetInt32(),
            int i => i,
            long l => (int)l,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    /// <summary>
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The double value.</returns>
    public double GetFloatValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    /// <param name="hex">The hex string to convert.</param>
    /// <returns>The byte array.</returns>
    public byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove any whitespace or separators
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");

        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even number of characters");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
