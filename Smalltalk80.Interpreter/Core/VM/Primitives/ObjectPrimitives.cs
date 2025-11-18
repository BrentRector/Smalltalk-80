using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(size));
        }
    }

    /// <summary>Primitive 75: Object hash - returns identity hash (OOP/2)</summary>
    public class Hash : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Blue Book: hash returns half the object pointer
            // SmallIntegers hash to their value
            if (context.Receiver.IsSmallInteger)
            {
                int value = context.Receiver.AsSmallInteger();
                // For SmallIntegers, hash is typically the value itself (or abs value)
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(value & 0x3FFFFFFF));
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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            bool identical = context.Receiver == context.Arguments[0];
            return PrimitiveResult.FromBool(identical);
        }
    }

    /// <summary>Primitive 111: Object class</summary>
    public class Class : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            SmalltalkOOP classOOP;
            if (context.Receiver.IsSmallInteger)
            {
                // SmallInteger class - need to look it up
                classOOP = context.RuntimeHeapAccess?.GetClass("SmallInteger")
                    ?? SpecialObjectsArray.Instance.Nil;
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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail();

            var format = context.HeapManager.GetObjectFormat(context.Receiver);

            if (format == ObjectFormat.ByteIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail();
                int byteValue = value.AsSmallInteger();
                if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail();
                context.HeapManager.SetIndexedByte(context.Receiver, index - 1, (byte)byteValue);
            }
            else if (format == ObjectFormat.WordIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail();
                int wordValue = value.AsSmallInteger();
                if (wordValue < 0 || wordValue > 65535) return PrimitiveResult.Fail();
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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            int count = context.HeapManager.GetInstanceVariableCount(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail();

            var value = context.HeapManager.GetInstanceVariable(context.Receiver, index - 1);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 74: Object instVarAt:put: - set instance variable</summary>
    public class InstVarAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];
            int count = context.HeapManager.GetInstanceVariableCount(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail();

            context.HeapManager.SetInstanceVariable(context.Receiver, index - 1, value);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 63: String at: - access character from string</summary>
    public class StringAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail();

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail();

            byte byteValue = context.HeapManager.GetIndexedByte(context.Receiver, index - 1);

            // Get Character from CharacterTable (Character class pool variable)
            var characterOOP = GetCharacterForAscii(context, byteValue);
            if (characterOOP == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(characterOOP);
        }

        // Helper to get Character object for ASCII code
        private static SmalltalkOOP GetCharacterForAscii(PrimitiveExecutionContext context, byte asciiValue)
        {
            if (context.RuntimeHeapAccess == null)
                return SpecialObjectsArray.Instance.Nil;

            // Get Character class from SystemDictionary
            var characterClassOOP = context.RuntimeHeapAccess.GetClass("Character");
            if (characterClassOOP == SpecialObjectsArray.Instance.Nil)
                return SpecialObjectsArray.Instance.Nil;

            // Get classPool from Character class (instance variable index 7)
            var classPoolOOP = context.HeapManager.GetInstanceVariable(characterClassOOP, 7);
            if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
                return SpecialObjectsArray.Instance.Nil;

            // Search for CharacterTable in class pool
            int dictSize = context.HeapManager.GetIndexedSize(classPoolOOP);
            for (int i = 0; i < dictSize; i++)
            {
                var entryOOP = context.HeapManager.GetIndexedOOP(classPoolOOP, i);
                if (entryOOP == SpecialObjectsArray.Instance.Nil)
                    continue;

                var keyOOP = context.HeapManager.GetInstanceVariable(entryOOP, 0);
                int symbolSize = context.HeapManager.GetIndexedSize(keyOOP);
                if (symbolSize != 14) continue; // "CharacterTable" has 14 chars

                byte[] bytes = new byte[symbolSize];
                for (int j = 0; j < symbolSize; j++)
                    bytes[j] = context.HeapManager.GetIndexedByte(keyOOP, j);

                if (System.Text.Encoding.ASCII.GetString(bytes) == "CharacterTable")
                {
                    var characterTableOOP = context.HeapManager.GetInstanceVariable(entryOOP, 1);
                    return context.HeapManager.GetIndexedOOP(characterTableOOP, asciiValue);
                }
            }

            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>Primitive 64: String at:put: - store character in string</summary>
    public class StringAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail();

            SmalltalkOOP characterOOP = context.Arguments[1];
            int charValue;

            // Accept SmallInteger directly (0-255 range)
            if (characterOOP.IsSmallInteger)
            {
                charValue = characterOOP.AsSmallInteger();
                if (charValue < 0 || charValue > 255) return PrimitiveResult.Fail();
            }
            else
            {
                // Character object - extract value from instance variable 0
                var asciiOOP = context.HeapManager.GetInstanceVariable(characterOOP, 0);
                if (!asciiOOP.IsSmallInteger) return PrimitiveResult.Fail();
                charValue = asciiOOP.AsSmallInteger();
                if (charValue < 0 || charValue > 255) return PrimitiveResult.Fail();
            }

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail();

            context.HeapManager.SetIndexedByte(context.Receiver, index - 1, (byte)charValue);
            return PrimitiveResult.Ok(characterOOP);
        }
    }

    /// <summary>Primitive 65: Stream next - read next element from stream</summary>
    public class Next : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Stream instance variables: collection (0), position (1), readLimit (2)
            SmalltalkOOP collectionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP readLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);

            if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

            if (position > readLimit) return PrimitiveResult.Fail();
            if (collectionOOP.IsSmallInteger) return PrimitiveResult.Fail();

            int collectionSize = context.HeapManager.GetIndexedSize(collectionOOP);
            if (position < 1 || position > collectionSize) return PrimitiveResult.Fail();

            // Read element from collection
            SmalltalkOOP element;
            var format = context.HeapManager.GetObjectFormat(collectionOOP);
            if (format == ObjectFormat.ByteIndexed)
            {
                byte byteValue = context.HeapManager.GetIndexedByte(collectionOOP, position - 1);
                element = SmalltalkOOP.FromSmallInteger(byteValue);
            }
            else
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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            SmalltalkOOP value = context.Arguments[0];

            // Stream instance variables: collection (0), position (1), readLimit (2), writeLimit (3)
            SmalltalkOOP collectionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP writeLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 3);

            if (!positionOOP.IsSmallInteger || !writeLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

            int position = positionOOP.AsSmallInteger();
            int writeLimit = writeLimitOOP.AsSmallInteger();

            if (position > writeLimit) return PrimitiveResult.Fail();
            if (collectionOOP.IsSmallInteger) return PrimitiveResult.Fail();

            int collectionSize = context.HeapManager.GetIndexedSize(collectionOOP);
            if (position < 1 || position > collectionSize) return PrimitiveResult.Fail();

            // Write element to collection
            var format = context.HeapManager.GetObjectFormat(collectionOOP);
            if (format == ObjectFormat.ByteIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail();
                int byteValue = value.AsSmallInteger();
                if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail();
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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Stream instance variables: collection (0), position (1), readLimit (2)
            SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            SmalltalkOOP readLimitOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);

            if (!positionOOP.IsSmallInteger || !readLimitOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

            return PrimitiveResult.FromBool(position > readLimit);
        }
    }

    /// <summary>Primitive 68: CompiledMethod objectAt: - access method header/literal</summary>
    public class ObjectAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail();

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(context.HeapManager.GetIndexedOOP(context.Receiver, index - 1));
        }
    }

    /// <summary>Primitive 69: CompiledMethod objectAt:put: - modify method header/literal</summary>
    public class ObjectAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            if (index < 1) return PrimitiveResult.Fail();

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (index > size) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            try
            {
                var result = context.HeapManager.AllocateInstanceOf(context.Receiver, 0);
                return PrimitiveResult.Ok(result);
            }
            catch
            {
                return PrimitiveResult.Fail();
            }
        }
    }

    /// <summary>Primitive 71: Behavior new: - create new instance with indexed fields</summary>
    public class NewWithSize : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int size = context.Arguments[0].AsSmallInteger();
            if (size < 0) return PrimitiveResult.Fail();

            var result = context.HeapManager.AllocateInstanceOf(context.Receiver, size);
            return PrimitiveResult.Ok(result);
        }
    }

    /// <summary>Primitive 72: Object become: - two-way identity swap</summary>
    public class Become : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger || context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            context.HeapManager.BecomeTwoWay(context.Receiver, context.Arguments[0]);
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 77: Behavior someInstance - return first instance of class</summary>
    public class SomeInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            var instance = context.HeapManager.FindInstanceOf(context.Receiver);
            if (instance == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(instance);
        }
    }

    /// <summary>Primitive 78: Object nextInstance - return next instance of same class</summary>
    public class NextInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            var nextInstance = context.HeapManager.FindNextInstanceOf(classOOP, context.Receiver);

            if (nextInstance == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(nextInstance);
        }
    }

    /// <summary>Primitive 79: CompiledMethod newMethod:header: - create new CompiledMethod</summary>
    public class NewMethodHeader : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger || !context.Arguments[1].IsSmallInteger)
                return PrimitiveResult.Fail();

            int numberOfBytes = context.Arguments[0].AsSmallInteger();
            int headerWord = context.Arguments[1].AsSmallInteger();

            if (numberOfBytes < 0) return PrimitiveResult.Fail();

            // Get class OOPs
            var compiledMethodClassOOP = context.RuntimeHeapAccess?.GetClass("CompiledMethod");
            var byteArrayClassOOP = context.RuntimeHeapAccess?.GetClass("ByteArray");

            if (compiledMethodClassOOP == null || byteArrayClassOOP == null)
                return PrimitiveResult.Fail();

            // Create bytecode array
            byte[] bytecodes = new byte[numberOfBytes];

            // Use shared helper
            var methodOOP = CompiledMethodHelpers.AllocateCompiledMethodInternal(
                context.HeapManager,
                compiledMethodClassOOP.Value,
                byteArrayClassOOP.Value,
                SpecialObjectsArray.Instance.Nil,
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
        ISmalltalkHeapManager heapManager,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP,
        SmalltalkOOP homeClassOOP,
        int headerWord,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes)
    {
        // 1. Allocate ByteArray for bytecodes
        var bytecodeArrayOOP = heapManager.AllocateInstanceOf(byteArrayClassOOP, bytecodes.Length);

        // 2. Copy bytecodes to ByteArray
        for (int i = 0; i < bytecodes.Length; i++)
        {
            heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // 3. Allocate CompiledMethod with indexed slots: header + literals + bytecodeArrayOOP
        int indexedSize = literalOOPs.Length + 2; // header + literals + bytecodeArrayOOP
        var methodOOP = heapManager.AllocateInstanceOf(compiledMethodClassOOP, indexedSize);

        // 4. Set homeClass as instance variable 0 (fixed slot)
        heapManager.SetInstanceVariable(methodOOP, 0, homeClassOOP);

        // 5. Set indexed slots
        // Slot 0: header
        heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(headerWord));

        // Slots 1..N: literals
        for (int i = 0; i < literalOOPs.Length; i++)
        {
            heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
        }

        // Final slot: bytecodeArrayOOP
        heapManager.SetIndexedOOP(methodOOP, literalOOPs.Length + 1, bytecodeArrayOOP);

        return methodOOP;
    }
}
