using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Shared helper methods for file primitives.
/// </summary>
internal static class FilePrimitiveHelpers
{
    /// <summary>
    /// Extracts a C# string from a Smalltalk String object.
    /// </summary>
    public static string? ExtractString(PrimitiveExecutionContext context, SmalltalkOOP stringOOP)
    {
        if (stringOOP.IsSmallInteger) return null;

        try
        {
            int length = context.HeapManager.GetIndexedSize(stringOOP);
            char[] chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = (char)context.HeapManager.GetIndexedByte(stringOOP, i);
            }

            return new string(chars);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a Smalltalk String object from a C# string.
    /// </summary>
    public static SmalltalkOOP CreateString(PrimitiveExecutionContext context, string value)
    {
        // Get String class
        var stringClassOOP = context.RuntimeServices?.GetClass("String")
            ?? throw new InvalidOperationException("Cannot find String class");

        // Allocate String with indexed bytes
        var stringOOP = context.HeapManager.AllocateInstanceOf(stringClassOOP, value.Length);

        // Copy characters as bytes
        for (int i = 0; i < value.Length; i++)
        {
            context.HeapManager.SetIndexedByte(stringOOP, i, (byte)value[i]);
        }

        return stringOOP;
    }
}
