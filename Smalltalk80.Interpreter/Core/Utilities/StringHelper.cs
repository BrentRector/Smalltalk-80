using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.Utilities;

/// <summary>
/// Helper class for converting between C# strings and Smalltalk String objects.
/// Smalltalk Strings are ByteIndexed objects where each indexed slot contains a character code.
///
/// RUNTIME SAFE: This class uses runtime heap access only, no coldstart structures.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Creates a Smalltalk String object from a C# string.
    /// Allocates a new String object on the heap and populates it with character codes.
    /// </summary>
    /// <param name="heap">The heap manager to allocate the string on</param>
    /// <param name="stringClassOOP">The String class OOP (caller must look this up)</param>
    /// <param name="text">The C# string to convert</param>
    /// <returns>OOP of the newly created Smalltalk String</returns>
    public static SmalltalkOOP CreateString(ISmalltalkHeap heap, SmalltalkOOP stringClassOOP, string text)
    {
        // Allocate ByteIndexed object for String
        // String has no instance variables, all data is in indexed portion
        var stringOOP = heap.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: text.Length);
        heap.SetClassOOP(stringOOP, stringClassOOP);

        // Fill indexed bytes with character codes
        for (int i = 0; i < text.Length; i++)
        {
            // Smalltalk-80 uses ASCII/ISO-8859-1 encoding
            byte charCode = (byte)text[i];
            heap.SetIndexedByte(stringOOP, i, charCode);
        }

        return stringOOP;
    }

    /// <summary>
    /// Extracts a C# string from a Smalltalk String object.
    /// Reads the indexed bytes and converts them to a C# string.
    /// </summary>
    /// <param name="heap">The heap manager to read from</param>
    /// <param name="stringOOP">OOP of the Smalltalk String to convert</param>
    /// <returns>C# string containing the characters from the Smalltalk String</returns>
    public static string ExtractString(ISmalltalkHeap heap, SmalltalkOOP stringOOP)
    {
        int length = heap.GetIndexedSize(stringOOP);
        var chars = new char[length];

        for (int i = 0; i < length; i++)
        {
            byte charCode = heap.GetIndexedByte(stringOOP, i);
            chars[i] = (char)charCode;
        }

        return new string(chars);
    }

    /// <summary>
    /// Checks if a Smalltalk object is a String (or Symbol, which inherits from String).
    /// </summary>
    /// <param name="heap">The heap manager to read from</param>
    /// <param name="oop">OOP to check</param>
    /// <returns>True if the object is a String or Symbol</returns>
    public static bool IsString(ISmalltalkHeap heap, SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return false;

        var className = heap.GetClassName(oop);
        return className == "String" || className == "Symbol";
    }
}
