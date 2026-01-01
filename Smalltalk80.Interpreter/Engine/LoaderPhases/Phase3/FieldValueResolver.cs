using System.Diagnostics;
using System.Text;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase3;

/// <summary>
/// Resolves field values from the coldstart bundle to Smalltalk OOPs.
/// Handles all value types: nil, booleans, integers, floats, symbols, strings,
/// characters, byte arrays, and object references.
/// </summary>
internal class FieldValueResolver(
    ObjectGraphAllocator allocator,
    ColdStartBundle bundle,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    IDiagnosticHub? diagnostics,
    FieldValueService fieldValueService)
{
    private readonly IReadOnlyList<string> _symbols = bundle.Symbols;

    /// <summary>
    /// Resolves a field value from the discriminated union to an OOP.
    /// </summary>
    /// <param name="value">The field value to resolve.</param>
    /// <returns>The resolved Smalltalk OOP.</returns>
    public SmalltalkOOP ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => allocator.GetNil(),
            "true" => GlobalRegistry.Get("true"),
            "false" => GlobalRegistry.Get("false"),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => AllocateFloat(GetFloatValue(value.Value!)),
            "symbolRef" => InternSymbol(_symbols[value.Index!.Value]),
            "stringRef" => InternString(bundle.Strings[value.Index!.Value]),
            "objectRef" => localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP) ? objOOP : allocator.GetNil(),
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
    public int GetIntValue(object value)
    {
        return fieldValueService.GetIntValue(value);
    }

    /// <summary>
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The double value.</returns>
    public double GetFloatValue(object value)
    {
        return fieldValueService.GetFloatValue(value);
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    /// <param name="name">The symbol name to intern.</param>
    /// <returns>The interned symbol OOP.</returns>
    public SmalltalkOOP InternSymbol(string name)
    {
        return fieldValueService.GetSymbolOOP(name);
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    /// <param name="value">The string value to intern.</param>
    /// <returns>The allocated String OOP.</returns>
    public SmalltalkOOP InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object to hold the string text
        var stringOOP = allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        // Set String class reference (since we're after class reference init)
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        allocator.SetClassOOP(stringOOP, stringClassOOP);

        return stringOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    /// <param name="codePoint">The Unicode code point for the character.</param>
    /// <returns>The allocated Character OOP.</returns>
    public SmalltalkOOP AllocateCharacter(int codePoint)
    {
        var characterOOP = allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        // Set Character class reference
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        allocator.SetClassOOP(characterOOP, characterClassOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    /// <param name="encoding">The encoding type ("base64" or "hex").</param>
    /// <param name="data">The encoded data string.</param>
    /// <returns>The allocated ByteArray OOP.</returns>
    public SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = allocator.AllocateByteArray("ByteArray", bytes.Length);

        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            allocator.SetIndexedByte(byteArrayOOP, i, bytes[i]);
        }

        // Set ByteArray class reference
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        allocator.SetClassOOP(byteArrayOOP, byteArrayClassOOP);

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates a Float object using ObjectGraphAllocator.
    /// Float must be WordIndexed with 2 words (32-bit each) for IEEE 754 double precision.
    /// </summary>
    /// <param name="value">The double value to store.</param>
    /// <returns>The allocated Float OOP.</returns>
    public SmalltalkOOP AllocateFloat(double value)
    {
        diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Creating Float value={value}",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // Use ObjectGraphAllocator's AllocateFloat which correctly creates WordIndexed Float with 2 words
        var floatOOP = allocator.AllocateFloat(value);

        diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Created floatOOP={floatOOP}, format={allocator.GetObjectFormat(floatOOP)}, size={allocator.GetIndexedSize(floatOOP)}",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // Set Float class reference
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        allocator.SetClassOOP(floatOOP, floatClassOOP);

        return floatOOP;
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    /// <param name="hex">The hex string to convert.</param>
    /// <returns>The byte array.</returns>
    public byte[] ConvertHexStringToBytes(string hex)
    {
        return fieldValueService.ConvertHexStringToBytes(hex);
    }
}
