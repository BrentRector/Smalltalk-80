using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Object manipulation primitives (Primitives 60-79, 110-127).
/// Handles array access, object creation, identity, and class operations.
/// </summary>
public static class ObjectPrimitives
{
    // Primitive 60: Array at:
    public static SmalltalkOOP At(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate arguments
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to 0-based index and access
        // Arrays use PointerIndexed format - get OOP directly
        return ctx.HeapManager.GetIndexedOOP(receiver, index - 1);
    }

    // Primitive 61: Array at:put:
    public static SmalltalkOOP AtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate receiver is not a SmallInteger
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP value = args[1];

        // Convert to 0-based index
        int zeroBasedIndex = index - 1;

        // Check object format and use appropriate setter
        var format = ctx.HeapManager.GetObjectFormat(receiver);

        if (format == ObjectFormat.PointerIndexed)
        {
            // Arrays, OrderedCollections, etc. - indexed OOP slots
            ctx.HeapManager.SetIndexedOOP(receiver, zeroBasedIndex, value);
        }
        else if (format == ObjectFormat.ByteIndexed)
        {
            // Strings, Symbols, ByteArrays - indexed byte slots
            if (!value.IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            int byteValue = value.AsSmallInteger();
            if (byteValue < 0 || byteValue > 255)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            ctx.HeapManager.SetIndexedByte(receiver, zeroBasedIndex, (byte)byteValue);
        }
        else if (format == ObjectFormat.WordIndexed)
        {
            // WordArrays - indexed 16-bit word slots
            if (!value.IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            int wordValue = value.AsSmallInteger();
            if (wordValue < 0 || wordValue > 65535)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            ctx.HeapManager.SetIndexedWord(receiver, zeroBasedIndex, (ushort)wordValue);
        }
        else
        {
            // Unsupported format for at:put:
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return value;
    }

    // Primitive 62: Object size
    public static SmalltalkOOP Size(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // SmallIntegers don't have a size

        int indexedSize = ctx.HeapManager.GetIndexedSize(receiver);
        return SmalltalkOOP.FromSmallInteger(indexedSize);
    }

    // Primitive 70: Object new
    public static SmalltalkOOP New(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Allocate a new instance of the receiver class with no indexed fields
        // Uses heap data only - reads the class's format and instance variable count
        // AllocateInstanceOf handles validation internally, let any real errors propagate
        return ctx.HeapManager.AllocateInstanceOf(receiver, 0);
    }

    // DEBUG: Primitive 71 call counter
    private static int _prim71CallCount = 0;

    // Primitive 71: Object new:
    public static SmalltalkOOP NewWithSize(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // DEBUG: Log first 50 calls (only when verbose debug mode enabled)
        System.Threading.Interlocked.Increment(ref _prim71CallCount);
        bool shouldLog = ctx.HeapManager.VerboseDebugMode && _prim71CallCount <= 50;

        if (args.Length != 1)
        {
            if (shouldLog) Console.WriteLine($"[Prim71 #{_prim71CallCount}] FAIL: args.Length != 1");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            if (shouldLog) Console.WriteLine($"[Prim71 #{_prim71CallCount}] FAIL: arg not SmallInteger, arg={args[0]}");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int size = args[0].AsSmallInteger();

        if (size < 0)
        {
            if (shouldLog) Console.WriteLine($"[Prim71 #{_prim71CallCount}] FAIL: size < 0");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            if (shouldLog) Console.WriteLine($"[Prim71 #{_prim71CallCount}] FAIL: receiver is SmallInteger");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Allocate indexed object of the receiver class with given size
        // Uses heap data only - reads the class's format and instance variable count
        // Used for Array new: n, ByteArray new: n, etc.
        // AllocateInstanceOf handles validation internally, let any real errors propagate
        try
        {
            var result = ctx.HeapManager.AllocateInstanceOf(receiver, size);
            if (shouldLog)
                Console.WriteLine($"[Prim71 #{_prim71CallCount}] {receiver} new: {size} → {result}");
            return result;
        }
        catch (Exception ex)
        {
            if (shouldLog)
                Console.WriteLine($"[Prim71 #{_prim71CallCount}] EXCEPTION: {ex.Message}");
            throw;
        }
    }

    // Primitive 110: Object ==
    public static SmalltalkOOP IdentityEquals(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return receiver == args[0]
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 111: Object class
    public static SmalltalkOOP Class(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // SmallIntegers have a special class (SmallInteger)
        // GetClassOOP handles this internally, let any real errors propagate
        return ctx.HeapManager.GetClassOOP(receiver);
    }

    // Primitive 70-77: Instance variable access
    // These are optimized for getting/setting instance variables

    public static SmalltalkOOP InstVarAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate receiver is not a SmallInteger
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate index is within instance variable bounds
        // Note: GetInstanceVariableCount returns the count of instance variables
        int instVarCount = ctx.HeapManager.GetInstanceVariableCount(receiver);
        if (index > instVarCount)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to 0-based index
        // Get instance variable as OOP
        return ctx.HeapManager.GetInstanceVariable(receiver, index - 1);
    }

    public static SmalltalkOOP InstVarAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate receiver is not a SmallInteger
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate index is within instance variable bounds
        int instVarCount = ctx.HeapManager.GetInstanceVariableCount(receiver);
        if (index > instVarCount)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP value = args[1];

        // Convert to 0-based index
        // Set instance variable as OOP
        ctx.HeapManager.SetInstanceVariable(receiver, index - 1, value);
        return value;
    }

    // Primitive 63: String at: (character access for byte-indexed objects)
    public static SmalltalkOOP StringAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            Console.WriteLine($"\n❌ Primitive 63 (at:) TYPE ERROR:");
            Console.WriteLine($"   Receiver: {receiver}");
            string? receiverClass = ctx.GetClassNameForObject(receiver);
            Console.WriteLine($"   Receiver class: {receiverClass}");
            Console.WriteLine($"   Index argument: {args[0]}");
            string? indexClass = ctx.GetClassNameForObject(args[0]);
            Console.WriteLine($"   Index class: {indexClass} (expected SmallInteger)");

            // If index is a String, show its contents
            if (indexClass == "String" && ctx.HeapManager.GetObjectFormat(args[0]) == ObjectFormat.ByteIndexed)
            {
                var stringValue = GetSymbolName(ctx, args[0]); // Works for Strings too
                Console.WriteLine($"   String value: '{stringValue}'");
            }

            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // Smalltalk arrays are 1-indexed

        // Validate receiver is not a SmallInteger
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            Console.WriteLine($"\n❌ Primitive 63 (at:) BOUNDS ERROR:");
            Console.WriteLine($"   Receiver: {receiver} (size={size})");
            Console.WriteLine($"   Index: {index} (1-based)");
            Console.WriteLine($"   Receiver format: {ctx.HeapManager.GetObjectFormat(receiver)}");
            string? className = ctx.GetClassNameForObject(receiver);
            Console.WriteLine($"   Receiver class: {className}");

            // Try to get symbol name if it's a Symbol
            if (ctx.HeapManager.GetObjectFormat(receiver) == ObjectFormat.ByteIndexed)
            {
                var symbolName = GetSymbolName(ctx, receiver);
                Console.WriteLine($"   Symbol name: '{symbolName}' (length={symbolName?.Length ?? 0})");
            }

            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to 0-based index
        // Strings use ByteIndexed format
        byte byteValue = ctx.HeapManager.GetIndexedByte(receiver, index - 1);

        // CRITICAL: String>>at: must return Character objects, not SmallIntegers!
        // Get Character from CharacterTable (Character class pool variable)
        return GetCharacterForAscii(ctx, byteValue);
    }

    /// <summary>
    /// Returns the Character object for a given ASCII code.
    /// Accesses CharacterTable from Character class pool.
    /// </summary>
    private static SmalltalkOOP GetCharacterForAscii(IPrimitiveContext ctx, byte asciiValue)
    {
        // Get Smalltalk dictionary from SpecialObjectsArray (heap-only access)
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;
        if (smalltalkDictOOP == SpecialObjectsArray.Instance.Nil)
        {
            Console.WriteLine($"[GetCharacterForAscii] ERROR: Smalltalk dictionary is nil in SpecialObjectsArray");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Look up Character class from Smalltalk dictionary
        var characterClassOOP = LookupInDictionary(ctx, smalltalkDictOOP, "Character");
        if (characterClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            Console.WriteLine($"[GetCharacterForAscii] ERROR: Character class not found in Smalltalk dictionary");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get classPool from Character class (instance variable index 7 - classPool)
        var classPoolOOP = ctx.HeapManager.GetInstanceVariable(characterClassOOP, 7);

        // DEFENSIVE: Check if classPool is nil
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
        {
            Console.WriteLine($"[GetCharacterForAscii] ERROR: Character class classPool is nil!");
            Console.WriteLine($"  Character class oop={characterClassOOP}");
            Console.WriteLine($"  Character initialize must run BEFORE ClassOrganizer initialize");
            Console.WriteLine($"  But Character initialize ran at [52] and completed successfully!");
            Console.WriteLine($"  This suggests classPool was never populated or index 7 is wrong");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // classPool is a Dictionary, which stores associations in indexed slots
        // (same structure as SystemDictionary - variableSubclass with 'tally' instance var)
        int dictSize = ctx.HeapManager.GetIndexedSize(classPoolOOP);

        Console.WriteLine($"[GetCharacterForAscii] Searching classPool oop={classPoolOOP} for CharacterTable");
        Console.WriteLine($"  classPool has {dictSize} indexed slots (associations)");

        for (int i = 0; i < dictSize; i++)
        {
            var entryOOP = ctx.HeapManager.GetIndexedOOP(classPoolOOP, i);
            if (entryOOP == SpecialObjectsArray.Instance.Nil)
                continue;

            var keyOOP = ctx.HeapManager.GetInstanceVariable(entryOOP, 0);
            string symbolName = GetSymbolName(ctx, keyOOP);
            if (symbolName == "CharacterTable")
            {
                Console.WriteLine($"  Found CharacterTable at index {i}");
                var characterTableOOP = ctx.HeapManager.GetInstanceVariable(entryOOP, 1);
                if (asciiValue < 0 || asciiValue > 255)
                {
                    ctx.PrimitiveSuccess = false;
                    return SpecialObjectsArray.Instance.Nil;
                }
                return ctx.HeapManager.GetIndexedOOP(characterTableOOP, asciiValue);
            }
        }

        Console.WriteLine($"[GetCharacterForAscii] ERROR: CharacterTable not found in Character classPool");
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Helper to get symbol name from symbol OOP.
    /// </summary>
    private static string GetSymbolName(IPrimitiveContext ctx, SmalltalkOOP symbolOOP)
    {
        int size = ctx.HeapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            bytes[i] = ctx.HeapManager.GetIndexedByte(symbolOOP, i);
        }
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    /// <summary>
    /// Looks up a key in a Dictionary by walking the association array.
    /// Dictionary structure (from Smalltalk-80.st):
    ///   Set variableSubclass: #Dictionary
    ///   - Instance variable 0: 'tally' (inherited from Set)
    ///   - Indexed slots: Association objects
    /// Returns the value OOP if found, nil otherwise.
    /// RUNTIME SAFE: Only accesses heap data.
    /// </summary>
    private static SmalltalkOOP LookupInDictionary(IPrimitiveContext ctx, SmalltalkOOP dictionaryOOP, string key)
    {
        // Dictionary stores associations in indexed slots (variableSubclass)
        // Instance variable 0 is 'tally', but we don't need it
        int arraySize = ctx.HeapManager.GetIndexedSize(dictionaryOOP);

        Console.WriteLine($"[LookupInDictionary] Searching dictionary oop={dictionaryOOP} for key '{key}'");
        Console.WriteLine($"  Dictionary has {arraySize} indexed slots (associations)");

        // Walk the association array (indexed slots of the dictionary)
        for (int i = 0; i < arraySize; i++)
        {
            var associationOOP = ctx.HeapManager.GetIndexedOOP(dictionaryOOP, i);
            if (associationOOP == SpecialObjectsArray.Instance.Nil)
                continue;

            // Association has 2 instance variables: key (index 0), value (index 1)
            var keyOOP = ctx.HeapManager.GetInstanceVariable(associationOOP, 0);

            // Get symbol name from key
            string symbolName = GetSymbolName(ctx, keyOOP);
            if (symbolName == key)
            {
                // Found it - return the value
                return ctx.HeapManager.GetInstanceVariable(associationOOP, 1);
            }
        }

        // Not found
        return SpecialObjectsArray.Instance.Nil;
    }

    // Primitive 64: String at:put: (character store for byte-indexed objects)
    public static SmalltalkOOP StringAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int charValue = args[1].AsSmallInteger();
        if (charValue < 0 || charValue > 255)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate receiver is not a SmallInteger
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to 0-based index
        ctx.HeapManager.SetIndexedByte(receiver, index - 1, (byte)charValue);
        return args[1];
    }

    // Primitive 65: Stream next - Read next element from stream
    // Blue Book: Stream has instance variables: collection, position, readLimit
    public static SmalltalkOOP Next(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get the receiver's actual class name (e.g., "class:ReadStream", "class:WriteStream")
        // This is necessary because Stream has no instance variables - they're defined in subclasses
        string? className = ctx.GetClassNameForObject(receiver);
        if (className == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get Stream instance variables from the receiver's actual class
        // (these fields are inherited from PositionableStream)
        SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, className, "position");
        SmalltalkOOP readLimitOOP = ctx.GetFieldValue(receiver, className, "readLimit");
        SmalltalkOOP collectionOOP = ctx.GetFieldValue(receiver, className, "collection");

        // Validate position and readLimit are SmallIntegers
        if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Decode SmallIntegers (both are 1-based Smalltalk indexes)
        int position = positionOOP.AsSmallInteger();
        int readLimit = readLimitOOP.AsSmallInteger();

        // Check if at end (position is 1-based; when > readLimit, stream is at end)
        if (position > readLimit)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate collection is not a SmallInteger
        if (collectionOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate position is within collection bounds
        int collectionSize = ctx.HeapManager.GetIndexedSize(collectionOOP);
        if (position < 1 || position > collectionSize)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Read element from collection - handle both ByteIndexed and PointerIndexed formats
        // Stored position is 1-based, but heap manager is 0-based
        // Convert: Smalltalk index 1 -> C# index 0
        SmalltalkOOP element;
        var collectionFormat = ctx.HeapManager.GetObjectFormat(collectionOOP);
        if (collectionFormat == ObjectFormat.ByteIndexed)
        {
            // ByteIndexed (String, ByteArray) - read byte and convert to SmallInteger
            byte byteValue = ctx.HeapManager.GetIndexedByte(collectionOOP, position - 1);
            element = SmalltalkOOP.FromSmallInteger(byteValue);
        }
        else if (collectionFormat == ObjectFormat.PointerIndexed)
        {
            // PointerIndexed (Array, OrderedCollection) - read OOP directly
            element = ctx.HeapManager.GetIndexedOOP(collectionOOP, position - 1);
        }
        else
        {
            // Unsupported format for stream reading
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Increment position (remains 1-based for Smalltalk)
        ctx.SetFieldValue(receiver, className, "position", SmalltalkOOP.FromSmallInteger(position + 1));

        return element;
    }

    // Primitive 66: Stream nextPut: - Write element to stream
    public static SmalltalkOOP NextPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP value = args[0];

        // Get the receiver's actual class name (e.g., "class:WriteStream", "class:ReadStream")
        // This is necessary because Stream has no instance variables - they're defined in subclasses
        string? className = ctx.GetClassNameForObject(receiver);
        if (className == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get Stream instance variables from the receiver's actual class
        // (these fields are inherited from PositionableStream and WriteStream)
        SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, className, "position");
        SmalltalkOOP writeLimitOOP = ctx.GetFieldValue(receiver, className, "writeLimit");
        SmalltalkOOP collectionOOP = ctx.GetFieldValue(receiver, className, "collection");

        // Validate position and writeLimit are SmallIntegers
        if (!positionOOP.IsSmallInteger || !writeLimitOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Decode SmallIntegers (both are 1-based)
        int position = positionOOP.AsSmallInteger();
        int writeLimit = writeLimitOOP.AsSmallInteger();

        // Check if at limit (position is 1-based; when > writeLimit, at end)
        if (position > writeLimit)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate collection is not a SmallInteger
        if (collectionOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate position is within collection bounds
        int collectionSize = ctx.HeapManager.GetIndexedSize(collectionOOP);
        if (position < 1 || position > collectionSize)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Write element to collection - handle both ByteIndexed and PointerIndexed formats
        // Stored position is 1-based, but heap manager is 0-based
        // Convert: Smalltalk index 1 -> C# index 0
        var collectionFormat = ctx.HeapManager.GetObjectFormat(collectionOOP);
        if (collectionFormat == ObjectFormat.ByteIndexed)
        {
            // ByteIndexed (String, ByteArray) - value must be SmallInteger (character code)
            if (!value.IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            int charValue = value.AsSmallInteger();
            if (charValue < 0 || charValue > 255)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            ctx.HeapManager.SetIndexedByte(collectionOOP, position - 1, (byte)charValue);
        }
        else if (collectionFormat == ObjectFormat.PointerIndexed)
        {
            // PointerIndexed (Array, OrderedCollection) - write OOP directly
            ctx.HeapManager.SetIndexedOOP(collectionOOP, position - 1, value);
        }
        else
        {
            // Unsupported format for stream writing
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Increment position (remains 1-based)
        ctx.SetFieldValue(receiver, className, "position", SmalltalkOOP.FromSmallInteger(position + 1));

        return value;
    }

    // Primitive 67: Stream atEnd - Check if stream is at end
    public static SmalltalkOOP AtEnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get the receiver's actual class name (e.g., "class:ReadStream", "class:WriteStream")
        // This is necessary because Stream has no instance variables - they're defined in subclasses
        string? className = ctx.GetClassNameForObject(receiver);
        if (className == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get Stream instance variables from the receiver's actual class
        // (these fields are inherited from PositionableStream)
        SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, className, "position");
        SmalltalkOOP readLimitOOP = ctx.GetFieldValue(receiver, className, "readLimit");

        // Validate position and readLimit are SmallIntegers
        if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Decode SmallIntegers (both are 1-based)
        int position = positionOOP.AsSmallInteger();
        int readLimit = readLimitOOP.AsSmallInteger();

        // Return true if past limit (both position and readLimit are 1-based)
        // At position == readLimit, stream is NOT yet at end
        return position > readLimit
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 68: Object become: (two-way identity swap)
    public static SmalltalkOOP Become(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Swap identities bidirectionally
        // BecomeTwoWay handles all validation internally, let any real errors propagate
        ctx.BecomeTwoWay(receiver, args[0]);
        return receiver;
    }

    // Primitive 68: CompiledMethod objectAt: - Access method header/literal
    public static SmalltalkOOP ObjectAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // Smalltalk arrays are 1-indexed

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // For CompiledMethod objects:
        // Index 1 = method header
        // Index 2+ = literals
        // This accesses the PointerIndexed part of the CompiledMethod
        // (header + literals), NOT the bytecode array

        // Get the indexed OOP at the specified position (1-based → 0-based)
        // CompiledMethod uses PointerIndexed format for header and literals
        return ctx.HeapManager.GetIndexedOOP(receiver, index - 1);
    }

    // Primitive 69: CompiledMethod objectAt:put: - Modify method header/literal
    public static SmalltalkOOP ObjectAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate bounds before accessing
        int size = ctx.HeapManager.GetIndexedSize(receiver);
        if (index > size)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP value = args[1];

        // For CompiledMethod objects:
        // Index 1 = method header
        // Index 2+ = literals
        // This modifies the PointerIndexed part of the CompiledMethod
        // (header + literals), NOT the bytecode array

        // Set the indexed OOP at the specified position (1-based → 0-based)
        // CompiledMethod uses PointerIndexed format for header and literals
        ctx.HeapManager.SetIndexedOOP(receiver, index - 1, value);
        return value;
    }

    // Primitive 75: Object asOop - Get object pointer as identity value
    public static SmalltalkOOP AsOop(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // For SmallIntegers, this fails (they don't have object pointers)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // The OOP itself IS the identity - just return it
        return receiver;
    }

    // Primitive 75 alternative: Object hash (for backward compatibility)
    public static SmalltalkOOP Hash(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // The OOP itself IS the hash - just return the receiver
        return receiver;
    }
}
