using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Simple Object primitives using new IPrimitive interface.
/// Complex primitives (at:, at:put:, new:, etc.) require more dependencies and remain as legacy.
/// </summary>
public static class SimpleObjectPrimitives
{
    /// <summary>Primitive 62: Object size - returns number of indexed elements</summary>
    public class Size : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(size));
        }
    }

    /// <summary>Primitive 75: Object hash - returns identity hash (OOP/2)</summary>
    public class Hash : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Blue Book: hash returns half the object pointer
            // SmallIntegers hash to their value (return self)
            if (context.Receiver.IsSmallInteger)
            {
                // Canonical Smalltalk-80: SmallInteger>>hash ^self
                return PrimitiveResult.Ok(context.Receiver);
            }

            // For objects, hash is half the OOP (Primitive 75 semantics)
            int hash = (int)(context.Receiver.RawValue >> 1);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(hash));
        }
    }

    /// <summary>Primitive 110: Object == (identity equals)</summary>
    public class IdentityEquals : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            bool identical = context.Receiver == context.Arguments[0];
            return PrimitiveResult.FromBool(identical, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 111: Object class</summary>
    public class Class : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP classOOP;
            if (context.Receiver.IsSmallInteger)
            {
                // SmallInteger class - need to look it up
                classOOP = context.RuntimeHeapAccess?.GetClass("SmallInteger")
                    ?? context.HeapManager.GetNil();
            }
            else
            {
                classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            }

            return PrimitiveResult.Ok(classOOP);
        }
    }

    /// <summary>Primitive 60: Array at: - get indexed element</summary>
    public class At : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail(context.Metadata!);

            var format = context.HeapManager.GetObjectFormat(context.Receiver);
            SmalltalkOOP value;

            if (format == ObjectFormat.ByteIndexed)
            {
                byte b = context.HeapManager.GetIndexedByte(context.Receiver, index - 1);
                value = SmalltalkOOP.FromSmallInteger(b);
            }
            else if (format == ObjectFormat.WordIndexed)
            {
                ushort w = context.HeapManager.GetIndexedWord(context.Receiver, index - 1);
                value = SmalltalkOOP.FromSmallInteger(w);
            }
            else
            {
                value = context.HeapManager.GetIndexedOOP(context.Receiver, index - 1);
            }

            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 61: Array at:put: - set indexed element</summary>
    public class AtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail(context.Metadata!);

            var format = context.HeapManager.GetObjectFormat(context.Receiver);

            if (format == ObjectFormat.ByteIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
                int byteValue = value.AsSmallInteger();
                if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail(context.Metadata!);
                context.HeapManager.SetIndexedByte(context.Receiver, index - 1, (byte)byteValue);
            }
            else if (format == ObjectFormat.WordIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
                int wordValue = value.AsSmallInteger();
                if (wordValue < 0 || wordValue > 65535) return PrimitiveResult.Fail(context.Metadata!);
                context.HeapManager.SetIndexedWord(context.Receiver, index - 1, (ushort)wordValue);
            }
            else
            {
                context.HeapManager.SetIndexedOOP(context.Receiver, index - 1, value);
            }

            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 73: Object instVarAt: - get instance variable</summary>
    public class InstVarAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();

            // Always read count from class format - this is the authoritative source
            var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            if (classOOP.IsSmallInteger || classOOP == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Read format from class using GetInstanceVariable
            // Class slot layout: [0]=class, [1]=superclass, [2]=methodDict, [3]=format
            var formatOOP = context.HeapManager.GetInstanceVariable(classOOP, 2);
            if (!formatOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            var formatWord = new FormatWord(formatOOP.AsSmallInteger());
            int count = formatWord.FixedFields;

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail(context.Metadata!);

            // Use GetInstanceVariable to access instance variables
            // Object slot layout: [0]=class, [1..count]=instance variables
            var value = context.HeapManager.GetInstanceVariable(context.Receiver, index - 1);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 74: Object instVarAt:put: - set instance variable</summary>
    public class InstVarAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];

            // Always read count from class format - this is the authoritative source
            var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            if (classOOP.IsSmallInteger || classOOP == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Read format from class using GetInstanceVariable
            // Class slot layout: [0]=class, [1]=superclass, [2]=methodDict, [3]=format
            var formatOOP = context.HeapManager.GetInstanceVariable(classOOP, 2);
            if (!formatOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            var formatWord = new FormatWord(formatOOP.AsSmallInteger());
            int count = formatWord.FixedFields;

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail(context.Metadata!);

            // Use SetInstanceVariable to modify instance variables
            // Object slot layout: [0]=class, [1..count]=instance variables
            context.HeapManager.SetInstanceVariable(context.Receiver, index - 1, value);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 63: String at: - access character from string</summary>
    public class StringAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1)
            {
                Console.WriteLine($"[Primitive 63] FAIL: Wrong number of arguments ({context.Arguments.Length})");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (context.Receiver.IsSmallInteger)
            {
                Console.WriteLine($"[Primitive 63] FAIL: Receiver is SmallInteger ({context.Receiver})");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!context.Arguments[0].IsSmallInteger)
            {
                Console.WriteLine($"[Primitive 63] FAIL: Index is not SmallInteger ({context.Arguments[0]})");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1)
            {
                Console.WriteLine($"[Primitive 63] FAIL: Index < 1 ({index})");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size)
            {
                Console.WriteLine($"[Primitive 63] FAIL: Index {index} > size {size}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            byte byteValue = context.HeapManager.GetIndexedByte(context.Receiver, index - 1);

            // Get Character from CharacterTable (Character class pool variable)
            var characterOOP = GetCharacterForAscii(context, byteValue);
            if (characterOOP == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(characterOOP);
        }

        // Helper to get Character object for ASCII code
        private static SmalltalkOOP GetCharacterForAscii(PrimitiveExecutionContext context, byte asciiValue)
        {
            if (context.RuntimeHeapAccess == null)
                return context.HeapManager.GetNil();

            // Use RuntimeHeapAccessor to look up CharacterTable class variable
            var characterTableOOP = context.RuntimeHeapAccess.LookupClassVariable("Character", "CharacterTable");
            if (characterTableOOP == context.HeapManager.GetNil())
                return context.HeapManager.GetNil();

            // CharacterTable is an Array of 256 Character objects
            // Return the Character at the given ASCII value index
            int tableSize = context.HeapManager.GetIndexedSize(characterTableOOP);

            if (asciiValue >= tableSize)
                return context.HeapManager.GetNil();

            return context.HeapManager.GetIndexedOOP(characterTableOOP, asciiValue);
        }
    }

    /// <summary>Primitive 64: String at:put: - store character in string</summary>
    public class StringAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP characterOOP = context.Arguments[1];
            int charValue;

            // Accept SmallInteger directly (0-255 range)
            if (characterOOP.IsSmallInteger)
            {
                charValue = characterOOP.AsSmallInteger();
                if (charValue < 0 || charValue > 255) return PrimitiveResult.Fail(context.Metadata!);
            }
            else
            {
                // Character object - extract value from instance variable 0
                var asciiOOP = context.HeapManager.GetInstanceVariable(characterOOP, 0);
                if (!asciiOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
                charValue = asciiOOP.AsSmallInteger();
                if (charValue < 0 || charValue > 255) return PrimitiveResult.Fail(context.Metadata!);
            }

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail(context.Metadata!);

            context.HeapManager.SetIndexedByte(context.Receiver, index - 1, (byte)charValue);
            return PrimitiveResult.Ok(characterOOP);
        }
    }

    /// <summary>Primitive 65: Stream next - read next element from stream</summary>
    /// <remarks>
    /// Optional optimization primitive that avoids message dispatch overhead for common cases.
    /// Handles canonical Blue Book collection types:
    /// - String/Symbol (and subclasses): Returns Character objects from CharacterTable
    /// - ByteArray: Returns SmallInteger (raw byte value)
    /// - Array: Returns OOP directly
    /// Subclasses and exotic types are supported via isKindOf: semantics.
    /// </remarks>
    public class Next : IPrimitive
    {
        // Helper to get Character object for ASCII code (shared with Primitive 63)
        private static SmalltalkOOP GetCharacterForAscii(PrimitiveExecutionContext context, byte asciiValue)
        {
            if (context.RuntimeHeapAccess == null)
                return context.HeapManager.GetNil();

            // Use RuntimeHeapAccessor to look up CharacterTable class variable
            var characterTableOOP = context.RuntimeHeapAccess.LookupClassVariable("Character", "CharacterTable");
            if (characterTableOOP == context.HeapManager.GetNil())
                return context.HeapManager.GetNil();

            // CharacterTable is an Array of 256 Character objects
            int tableSize = context.HeapManager.GetIndexedSize(characterTableOOP);
            if (asciiValue >= tableSize)
                return context.HeapManager.GetNil();

            return context.HeapManager.GetIndexedOOP(characterTableOOP, asciiValue);
        }

        // Helper to implement isKindOf: semantics by traversing superclass chain
        // This handles String subclasses and other inheritance relationships correctly
        private static bool IsKindOf(PrimitiveExecutionContext context, SmalltalkOOP objectOOP, SmalltalkOOP targetClassOOP)
        {
            var currentClassOOP = context.HeapManager.GetClassOOP(objectOOP);
            var nilOOP = context.HeapManager.GetNil();

            // Traverse superclass chain
            while (currentClassOOP != nilOOP)
            {
                if (currentClassOOP == targetClassOOP)
                    return true;

                // Move to superclass (instance variable 0 in Behavior)
                currentClassOOP = context.HeapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
            }

            return false;
        }

        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Stream instance variables: collection (0), position (1), readLimit (2)
            SmalltalkOOP collectionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP readLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);

            if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

            if (position > readLimit) return PrimitiveResult.Fail(context.Metadata!);
            if (collectionOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int collectionSize = context.HeapManager.GetIndexedSize(collectionOOP);
            if (position < 1 || position > collectionSize) return PrimitiveResult.Fail(context.Metadata!);

            // Read element from collection
            SmalltalkOOP element;
            var format = context.HeapManager.GetObjectFormat(collectionOOP);
            if (format == ObjectFormat.ByteIndexed)
            {
                byte byteValue = context.HeapManager.GetIndexedByte(collectionOOP, position - 1);

                // For String/Symbol (and subclasses), return Character object from CharacterTable
                // For other byte arrays (ByteArray, etc.), return SmallInteger
                // Use isKindOf: semantics to handle subclasses correctly
                if (IsKindOf(context, collectionOOP, context.Metadata!.StringClassOOP) ||
                    IsKindOf(context, collectionOOP, context.Metadata!.SymbolClassOOP))
                {
                    element = GetCharacterForAscii(context, byteValue);
                    if (element == context.HeapManager.GetNil())
                    {
                        // CharacterTable lookup failed - fail primitive so fallback handles it
                        return PrimitiveResult.Fail(context.Metadata!);
                    }
                }
                else
                {
                    // ByteArray and other byte-indexed types return raw SmallInteger
                    element = SmalltalkOOP.FromSmallInteger(byteValue);
                }
            }
            else  // PointerIndexed (Arrays, etc.)
            {
                element = context.HeapManager.GetIndexedOOP(collectionOOP, position - 1);
            }

            // Increment position
            context.HeapManager.SetInstanceVariable(context.Receiver, 1, SmalltalkOOP.FromSmallInteger(position + 1));

            return PrimitiveResult.Ok(element);
        }
    }

    /// <summary>Primitive 66: Stream nextPut: - write element to stream</summary>
    public class NextPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP value = context.Arguments[0];

            // Stream instance variables: collection (0), position (1), readLimit (2), writeLimit (3)
            SmalltalkOOP collectionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP writeLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 3);

            if (!positionOOP.IsSmallInteger || !writeLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int position = positionOOP.AsSmallInteger();
            int writeLimit = writeLimitOOP.AsSmallInteger();

            if (position > writeLimit) return PrimitiveResult.Fail(context.Metadata!);
            if (collectionOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int collectionSize = context.HeapManager.GetIndexedSize(collectionOOP);
            if (position < 1 || position > collectionSize) return PrimitiveResult.Fail(context.Metadata!);

            // Write element to collection
            var format = context.HeapManager.GetObjectFormat(collectionOOP);
            if (format == ObjectFormat.ByteIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
                int byteValue = value.AsSmallInteger();
                if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail(context.Metadata!);
                context.HeapManager.SetIndexedByte(collectionOOP, position - 1, (byte)byteValue);
            }
            else
            {
                context.HeapManager.SetIndexedOOP(collectionOOP, position - 1, value);
            }

            // Increment position
            context.HeapManager.SetInstanceVariable(context.Receiver, 1, SmalltalkOOP.FromSmallInteger(position + 1));

            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 67: Stream atEnd - check if stream is at end</summary>
    public class AtEnd : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Stream instance variables: collection (0), position (1), readLimit (2)
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP readLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);

            if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

            return PrimitiveResult.FromBool(position > readLimit, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 68: CompiledMethod objectAt: - access method header/literal</summary>
    public class ObjectAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail(context.Metadata!);

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(context.HeapManager.GetIndexedOOP(context.Receiver, index - 1));
        }
    }

    /// <summary>Primitive 69: CompiledMethod objectAt:put: - modify method header/literal</summary>
    public class ObjectAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail(context.Metadata!);

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail(context.Metadata!);

            SmalltalkOOP value = context.Arguments[1];
            context.HeapManager.SetIndexedOOP(context.Receiver, index - 1, value);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 70: Behavior new - create new instance with no indexed fields</summary>
    public class New : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                var result = context.HeapManager.AllocateInstanceOf(context.Receiver, 0);
                return PrimitiveResult.Ok(result);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>Primitive 71: Behavior new: - create new instance with indexed fields</summary>
    public class NewWithSize : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int size = context.Arguments[0].AsSmallInteger();
            if (size < 0) return PrimitiveResult.Fail(context.Metadata!);

            var result = context.HeapManager.AllocateInstanceOf(context.Receiver, size);
            return PrimitiveResult.Ok(result);
        }
    }

    /// <summary>Primitive 72: Object become: - two-way identity swap</summary>
    public class Become : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger || context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            context.HeapManager.Become.BecomeTwoWay(context.Receiver, context.Arguments[0]);
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 77: Behavior someInstance - return first instance of class</summary>
    public class SomeInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var instance = context.HeapManager.FindInstanceOf(context.Receiver);
            if (instance == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(instance);
        }
    }

    /// <summary>Primitive 78: Object nextInstance - return next instance of same class</summary>
    public class NextInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            var nextInstance = context.HeapManager.FindNextInstanceOf(classOOP, context.Receiver);

            if (nextInstance == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(nextInstance);
        }
    }

    /// <summary>Primitive 79: CompiledMethod newMethod:header: - create new CompiledMethod</summary>
    public class NewMethodHeader : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger || !context.Arguments[1].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int numberOfBytes = context.Arguments[0].AsSmallInteger();
            int headerWord = context.Arguments[1].AsSmallInteger();

            if (numberOfBytes < 0) return PrimitiveResult.Fail(context.Metadata!);

            // Get class OOPs
            var compiledMethodClassOOP = context.RuntimeHeapAccess?.GetClass("CompiledMethod");
            var byteArrayClassOOP = context.RuntimeHeapAccess?.GetClass("ByteArray");

            if (compiledMethodClassOOP == null || byteArrayClassOOP == null)
                return PrimitiveResult.Fail(context.Metadata!);

            // Create bytecode array
            byte[] bytecodes = new byte[numberOfBytes];

            // Use shared helper
            var methodOOP = CompiledMethodHelpers.AllocateCompiledMethodInternal(
                context.HeapManager,
                compiledMethodClassOOP.Value,
                byteArrayClassOOP.Value,
                context.HeapManager.GetNil(),
                headerWord,
                Array.Empty<SmalltalkOOP>(),
                bytecodes);

            return PrimitiveResult.Ok(methodOOP);
        }
    }
}

/// <summary>
/// Helper methods for CompiledMethod allocation shared between primitives and cold start.
/// </summary>
public static class CompiledMethodHelpers
{
    /// <summary>
    /// Allocates a CompiledMethod object following our C# design:
    /// Fixed slot 0: homeClass (needed for super sends)
    /// Indexed slots: [header, literal1, literal2, ..., literalN, bytecodeArrayOOP]
    /// + Separate ByteArray for actual bytecodes
    /// </summary>
    public static SmalltalkOOP AllocateCompiledMethodInternal(
        ISmalltalkHeap heapManager,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP,
        SmalltalkOOP homeClassOOP,
        int headerWord,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes)
    {
        // Blue Book: CompiledMethod has heterogeneous format (pointers + bytes)
        // Our implementation: PointerIndexed CompiledMethod + separate ByteArray
        // CompiledMethod has 1 fixed slot (homeClass) plus indexed slots
        int fixedSlots = 1;  // slot 0 = homeClass
        int indexedSlots = 1 + literalOOPs.Length + 1; // header + literals + bytecodeArrayOOP

        // Step 1: Create separate ByteArray for bytecodes
        var bytecodeArrayOOP = heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, bytecodes.Length);
        heapManager.SetClassOOP(bytecodeArrayOOP, byteArrayClassOOP);
        for (int i = 0; i < bytecodes.Length; i++)
        {
            heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // Step 2: Allocate PointerIndexed CompiledMethod
        var methodOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots, indexedSlots);
        heapManager.SetClassOOP(methodOOP, compiledMethodClassOOP);

        // Step 3: Set fixed slot 0 (homeClass) - needed for super sends
        heapManager.SetInstanceVariable(methodOOP, 0, homeClassOOP);

        // Step 4: Populate indexed slots
        // Indexed slot 0: Method header
        heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(headerWord));

        // Slots 1..N: Literals
        for (int i = 0; i < literalOOPs.Length; i++)
        {
            heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
        }

        // Slot N+1: Hidden ByteArray reference
        heapManager.SetIndexedOOP(methodOOP, literalOOPs.Length + 1, bytecodeArrayOOP);

        return methodOOP;
    }
}
