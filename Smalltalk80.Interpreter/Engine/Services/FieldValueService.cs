using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides centralized field value resolution and object allocation services.
/// Consolidates duplicated logic from Phase3/FieldValueResolver and Phase4/LiteralResolver.
/// </summary>
/// <remarks>
/// Handles resolution of all field value types:
/// - Nil, true, false (sentinels)
/// - SmallInteger, LargeInteger, Float (numbers)
/// - Symbol, String (text)
/// - Character (single character)
/// - ByteArray (binary data)
/// - Array (collections)
/// - Object references
/// </remarks>
public class FieldValueService
{
    private readonly ObjectGraphAllocator _allocator;
    private readonly ColdStartBundle _bundle;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly SymbolService _symbolService;
    private readonly IDiagnosticHub? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the FieldValueService class.
    /// </summary>
    /// <param name="allocator">The object graph allocator for creating objects.</param>
    /// <param name="bundle">The cold start bundle containing object data.</param>
    /// <param name="localIdToObjectId">Mapping from local IDs to allocated object OOPs.</param>
    /// <param name="symbolService">The symbol service for symbol lookups.</param>
    /// <param name="diagnostics">Optional diagnostic hub for event emission.</param>
    public FieldValueService(
        ObjectGraphAllocator allocator,
        ColdStartBundle bundle,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        SymbolService symbolService,
        IDiagnosticHub? diagnostics = null)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
        _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Resolves a field value from the discriminated union to an OOP.
    /// </summary>
    /// <param name="value">The field value to resolve.</param>
    /// <returns>The resolved Smalltalk OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when field type is unknown.</exception>
    /// <exception cref="NotImplementedException">Thrown for unsupported types like LargeInteger.</exception>
    public SmalltalkOOP ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => AllocateFloat(GetFloatValue(value.Value!)),
            "symbolRef" => _symbolService.GetSymbolByIndex(value.Index!.Value),
            "stringRef" => InternString(_bundle.Strings[value.Index!.Value]),
            "objectRef" => _localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP)
                ? objOOP
                : SpecialObjectsArray.Instance.Nil,
            "character" => AllocateCharacter(value.CodePoint!.Value),
            "bytes" => AllocateByteArray(value.Encoding!, value.Data!),
            _ => throw new InvalidOperationException($"Unknown field type: {value.Type}")
        };
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when value cannot be converted to int.</exception>
    public int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
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
    /// <exception cref="InvalidOperationException">Thrown when value cannot be converted to double.</exception>
    public double GetFloatValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Allocates a String object (NOT interned - each call creates a new String).
    /// In Smalltalk-80, Strings are mutable and NOT interned.
    /// </summary>
    /// <param name="value">The string value to allocate.</param>
    /// <returns>The allocated String object OOP.</returns>
    public SmalltalkOOP InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        // Set String class reference
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);

        return stringOOP;
    }

    /// <summary>
    /// Allocates a Float object using ObjectGraphAllocator.
    /// Float must be WordIndexed with 4 words (16-bit each) per Blue Book specification.
    /// </summary>
    /// <param name="value">The double value to store.</param>
    /// <returns>The allocated Float OOP.</returns>
    public SmalltalkOOP AllocateFloat(double value)
    {
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "FieldValueService",
            Message = $"Creating Float value={value}",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // Use ObjectGraphAllocator's AllocateFloat which correctly creates WordIndexed Float with 4 words
        var floatOOP = _allocator.AllocateFloat(value);

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "FieldValueService",
            Message = $"Created floatOOP={floatOOP}, format={_allocator.GetObjectFormat(floatOOP)}, size={_allocator.GetIndexedSize(floatOOP)}",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // Set Float class reference
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        return floatOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    /// <param name="codePoint">The Unicode code point for the character.</param>
    /// <returns>The allocated Character OOP.</returns>
    public SmalltalkOOP AllocateCharacter(int codePoint)
    {
        var characterOOP = _allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        // Set Character class reference
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    /// <param name="encoding">The encoding type ("base64" or "hex").</param>
    /// <param name="data">The encoded data string.</param>
    /// <returns>The allocated ByteArray OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when encoding is unsupported.</exception>
    public SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes.Length);

        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _allocator.SetIndexedByte(byteArrayOOP, i, bytes[i]);
        }

        // Set ByteArray class reference
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates an Array object.
    /// </summary>
    /// <param name="elements">The array of element OOPs to store.</param>
    /// <returns>The allocated Array object OOP.</returns>
    public SmalltalkOOP AllocateArray(SmalltalkOOP[] elements)
    {
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = _allocator.AllocateArray(arrayClassOOP, elements.Length);

        // Store the elements
        for (int i = 0; i < elements.Length; i++)
        {
            _allocator.SetIndexedOOP(arrayOOP, i, elements[i]);
        }

        return arrayOOP;
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    /// <param name="hex">The hex string to convert.</param>
    /// <returns>The byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when hex string has odd number of characters.</exception>
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
