using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.Utilities;

/// <summary>
/// Helper class for converting between C# strings and Smalltalk String objects.
/// Smalltalk Strings are ByteIndexed objects where each indexed slot contains a character code.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Creates a Smalltalk String object from a C# string.
    /// Allocates a new String object on the heap and populates it with character codes.
    /// </summary>
    /// <param name="heap">The heap manager to allocate the string on</param>
    /// <param name="text">The C# string to convert</param>
    /// <returns>OOP of the newly created Smalltalk String</returns>
    public static SmalltalkOOP CreateString(ISmalltalkHeap heap, string text)
    {
        // Get String class OOP from GlobalRegistry
        var stringClassOOP = GlobalRegistry.Get("class:String");

        // Allocate ByteIndexed object for String
        // String has no instance variables, all data is in indexed portion
        var stringOOP = heap.AllocateObject(ObjectFormat.ByteIndexed, instanceVarCount: 0, indexedSize: text.Length);
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
    /// Creates a Smalltalk Symbol object from a C# string.
    /// Symbols are unique interned strings in the Symbol table.
    /// This method looks up or creates a symbol in the system symbol table.
    /// </summary>
    /// <param name="heap">The heap manager to allocate on</param>
    /// <param name="text">The C# string to intern as a Symbol</param>
    /// <returns>OOP of the Symbol</returns>
    public static SmalltalkOOP CreateSymbol(ISmalltalkHeap heap, string text)
    {
        // First try to find existing symbol in SymbolRegistry
        foreach (var (index, symbolOOP) in SymbolRegistry.AllSymbols())
        {
            var symbolString = ExtractString(heap, symbolOOP);
            if (symbolString == text)
            {
                return symbolOOP;
            }
        }

        // Symbol not found - need to create new one
        // Get Symbol class OOP
        var symbolClassOOP = GlobalRegistry.Get("class:Symbol");

        // Allocate ByteIndexed object for Symbol (same structure as String)
        var symbolOOP = heap.AllocateObject(ObjectFormat.ByteIndexed, instanceVarCount: 0, indexedSize: text.Length);
        heap.SetClassOOP(symbolOOP, symbolClassOOP);

        // Fill indexed bytes with character codes
        for (int i = 0; i < text.Length; i++)
        {
            byte charCode = (byte)text[i];
            heap.SetIndexedByte(symbolOOP, i, charCode);
        }

        // Register the new symbol
        SymbolRegistry.RegisterSymbol(symbolOOP);

        return symbolOOP;
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

        var classOOP = heap.GetClassOOP(oop);
        var className = GetClassName(heap, classOOP);

        return className == "String" || className == "Symbol";
    }

    /// <summary>
    /// Gets the class name for a class OOP (helper method).
    /// </summary>
    private static string GetClassName(ISmalltalkHeap heap, SmalltalkOOP classOOP)
    {
        try
        {
            // Get class name from instance variable (Behavior>>name)
            var nameOOP = heap.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_NAME_INDEX);
            if (nameOOP.RawValue == 0)
                return "<unnamed>";

            // Symbol/String is ByteIndexed
            int length = heap.GetIndexedSize(nameOOP);
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = heap.GetIndexedByte(nameOOP, i);
            }

            return System.Text.Encoding.ASCII.GetString(bytes);
        }
        catch
        {
            return "<unknown>";
        }
    }
}
