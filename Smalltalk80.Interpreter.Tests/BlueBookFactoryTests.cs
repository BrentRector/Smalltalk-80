using Xunit;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for BlueBookFactory object creation methods.
/// </summary>
public class BlueBookFactoryTests
{
    /// <summary>
    /// Helper method to create a mock class for testing.
    /// </summary>
    private static SObjectRef CreateMockClass(string name)
    {
        var classClass = SObjectRef.FromSmallInteger(1); // Mock Class class
        var oop = ObjectMemoryNew.AllocateObject(
            classClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: 10, // Typical class instance variable count
            indexedSize: 0
        );
        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Tests CreateMethodContext with correct field layout.
    /// </summary>
    [Fact]
    public void CreateMethodContext_HasCorrectLayout()
    {
        // Arrange
        var methodContextClass = CreateMockClass("MethodContext");
        var method = SObjectRef.FromSmallInteger(999); // Mock method
        var receiver = SObjectRef.FromSmallInteger(100);
        const int tempCount = 3;
        const int stackSize = 10;

        // Act
        var context = BlueBookFactory.CreateMethodContext(
            methodContextClass,
            method,
            receiver,
            tempCount,
            stackSize
        );

        // Assert
        Assert.False(context.IsNil, "Context should not be nil");
        Assert.True(context.IsHeapObject, "Context should be a heap object");
        Assert.Equal(methodContextClass, context.GetClassRef());

        // Check fixed fields
        Assert.True(context.GetInstanceVariable(BlueBookLayouts.MethodContext.SenderIndex).IsNil);
        Assert.Equal(0, context.GetInstanceVariable(BlueBookLayouts.MethodContext.PcIndex).AsSmallInteger());
        Assert.Equal(
            BlueBookLayouts.MethodContext.FixedFieldCount + tempCount,
            context.GetInstanceVariable(BlueBookLayouts.MethodContext.StackpIndex).AsSmallInteger()
        );
        Assert.Equal(method, context.GetInstanceVariable(BlueBookLayouts.MethodContext.MethodIndex));
        Assert.True(context.GetInstanceVariable(BlueBookLayouts.MethodContext.ReceiverMapIndex).IsNil);
        Assert.Equal(receiver, context.GetInstanceVariable(BlueBookLayouts.MethodContext.ReceiverIndex));

        // Check that temps and stack are initialized to nil
        for (int i = BlueBookLayouts.MethodContext.FixedFieldCount;
             i < BlueBookLayouts.MethodContext.FixedFieldCount + tempCount + stackSize;
             i++)
        {
            Assert.True(context.GetInstanceVariable(i).IsNil,
                $"Temp/stack slot {i} should be initialized to nil");
        }
    }

    /// <summary>
    /// Tests CreateBlockContext with correct field layout.
    /// </summary>
    [Fact]
    public void CreateBlockContext_HasCorrectLayout()
    {
        // Arrange
        var blockContextClass = CreateMockClass("BlockContext");
        var home = SObjectRef.FromSmallInteger(888); // Mock home context
        const int nargs = 2;
        const int startpc = 10;
        const int stackSize = 5;

        // Act
        var context = BlueBookFactory.CreateBlockContext(
            blockContextClass,
            home,
            nargs,
            startpc,
            stackSize
        );

        // Assert
        Assert.False(context.IsNil, "Context should not be nil");
        Assert.True(context.IsHeapObject, "Context should be a heap object");
        Assert.Equal(blockContextClass, context.GetClassRef());

        // Check fixed fields
        Assert.True(context.GetInstanceVariable(BlueBookLayouts.BlockContext.SenderIndex).IsNil);
        Assert.Equal(startpc, context.GetInstanceVariable(BlueBookLayouts.BlockContext.PcIndex).AsSmallInteger());
        Assert.Equal(
            BlueBookLayouts.BlockContext.FixedFieldCount,
            context.GetInstanceVariable(BlueBookLayouts.BlockContext.StackpIndex).AsSmallInteger()
        );
        Assert.Equal(nargs, context.GetInstanceVariable(BlueBookLayouts.BlockContext.NargsIndex).AsSmallInteger());
        Assert.Equal(startpc, context.GetInstanceVariable(BlueBookLayouts.BlockContext.StartpcIndex).AsSmallInteger());
        Assert.Equal(home, context.GetInstanceVariable(BlueBookLayouts.BlockContext.HomeIndex));

        // Check that stack is initialized to nil
        for (int i = BlueBookLayouts.BlockContext.FixedFieldCount;
             i < BlueBookLayouts.BlockContext.FixedFieldCount + stackSize;
             i++)
        {
            Assert.True(context.GetInstanceVariable(i).IsNil,
                $"Stack slot {i} should be initialized to nil");
        }
    }

    /// <summary>
    /// Tests CreateCompiledMethod with correct layout.
    /// </summary>
    [Fact]
    public void CreateCompiledMethod_HasCorrectLayout()
    {
        // Arrange
        var compiledMethodClass = CreateMockClass("CompiledMethod");
        var header = SObjectRef.FromSmallInteger(0x1234); // Mock header
        var literals = new[]
        {
            SObjectRef.FromSmallInteger(100),
            SObjectRef.FromSmallInteger(200),
            SObjectRef.FromSmallInteger(300)
        };
        var bytecodes = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };

        // Act
        var method = BlueBookFactory.CreateCompiledMethod(
            compiledMethodClass,
            header,
            literals,
            bytecodes
        );

        // Assert
        Assert.False(method.IsNil, "Method should not be nil");
        Assert.True(method.IsHeapObject, "Method should be a heap object");
        Assert.Equal(compiledMethodClass, method.GetClassRef());
        Assert.Equal(ObjectFormat.CompiledMethod, method.GetFormat());

        // Check header
        Assert.Equal(header.AsSmallInteger(), ObjectMemoryNew.GetCompiledMethodHeader(method.AsObjectPointer()));

        // Check literals
        var methodLiterals = ObjectMemoryNew.GetCompiledMethodLiterals(method.AsObjectPointer());
        Assert.Equal(literals.Length, methodLiterals.Length);
        for (int i = 0; i < literals.Length; i++)
        {
            Assert.Equal(literals[i], methodLiterals[i]);
        }

        // Check bytecodes
        var methodBytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(method.AsObjectPointer());
        Assert.Equal(bytecodes.Length, methodBytecodes.Length);
        for (int i = 0; i < bytecodes.Length; i++)
        {
            Assert.Equal(bytecodes[i], methodBytecodes[i]);
        }
    }

    /// <summary>
    /// Tests CreateArray with correct format and size.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void CreateArray_HasCorrectSize(int size)
    {
        // Arrange
        var arrayClass = CreateMockClass("Array");

        // Act
        var array = BlueBookFactory.CreateArray(arrayClass, size);

        // Assert
        Assert.False(array.IsNil, "Array should not be nil");
        Assert.Equal(arrayClass, array.GetClassRef());
        Assert.Equal(ObjectFormat.PointerIndexed, array.GetFormat());
        Assert.Equal(size, array.GetIndexedSize());

        // All elements should be initialized to nil
        for (int i = 0; i < size; i++)
        {
            Assert.True(array.GetIndexedPointer(i).IsNil,
                $"Array element {i} should be initialized to nil");
        }
    }

    /// <summary>
    /// Tests CreateByteArray with correct format and size.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(256)]
    public void CreateByteArray_HasCorrectSize(int size)
    {
        // Arrange
        var byteArrayClass = CreateMockClass("ByteArray");

        // Act
        var byteArray = BlueBookFactory.CreateByteArray(byteArrayClass, size);

        // Assert
        Assert.False(byteArray.IsNil, "ByteArray should not be nil");
        Assert.Equal(byteArrayClass, byteArray.GetClassRef());
        Assert.Equal(ObjectFormat.ByteIndexed, byteArray.GetFormat());
        Assert.Equal(size, byteArray.GetIndexedSize());

        // All bytes should be initialized to 0
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(0, byteArray.GetIndexedByte(i));
        }
    }

    /// <summary>
    /// Tests CreateString with ASCII content.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("Hello, World!")]
    [InlineData("Smalltalk-80")]
    public void CreateString_StoresAsciiCorrectly(string content)
    {
        // Arrange
        var stringClass = CreateMockClass("String");

        // Act
        var str = BlueBookFactory.CreateStringFromCSharp(stringClass, content);

        // Assert
        Assert.False(str.IsNil, "String should not be nil");
        Assert.Equal(stringClass, str.GetClassRef());
        Assert.Equal(ObjectFormat.ByteIndexed, str.GetFormat());
        Assert.Equal(content.Length, str.GetIndexedSize());

        // Check each character
        for (int i = 0; i < content.Length; i++)
        {
            Assert.Equal((byte)content[i], str.GetIndexedByte(i));
        }
    }

    /// <summary>
    /// Tests CreateSymbol creates symbols.
    /// </summary>
    [Fact]
    public void CreateSymbol_CreatesSymbols()
    {
        // Arrange
        var symbolClass = CreateMockClass("Symbol");

        // Act - Create symbols
        var symbol1 = BlueBookFactory.CreateSymbol(symbolClass, "testSymbol");
        var symbol2 = BlueBookFactory.CreateSymbol(symbolClass, "testSymbol");
        var symbol3 = BlueBookFactory.CreateSymbol(symbolClass, "differentSymbol");

        // Assert
        Assert.False(symbol1.IsNil, "Symbol1 should not be nil");
        Assert.False(symbol2.IsNil, "Symbol2 should not be nil");
        Assert.False(symbol3.IsNil, "Symbol3 should not be nil");

        // Note: Without a symbol table, these won't be interned, so they won't be identical
        // But they should still have correct format and class
        Assert.Equal(ObjectFormat.ByteIndexed, symbol1.GetFormat());
        Assert.Equal(symbolClass, symbol1.GetClassRef());
        Assert.Equal(ObjectFormat.ByteIndexed, symbol3.GetFormat());
        Assert.Equal(symbolClass, symbol3.GetClassRef());

        // Check content
        Assert.Equal(10, symbol1.GetIndexedSize()); // "testSymbol" length
        Assert.Equal(15, symbol3.GetIndexedSize()); // "differentSymbol" length
    }

    /// <summary>
    /// Tests CreateCharacter with ASCII value.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(65)]  // 'A'
    [InlineData(97)]  // 'a'
    [InlineData(32)]  // Space
    [InlineData(255)] // Max ASCII
    public void CreateCharacter_HasCorrectAsciiValue(int asciiValue)
    {
        // Arrange
        var characterClass = CreateMockClass("Character");

        // Act
        var character = BlueBookFactory.CreateCharacter(characterClass, asciiValue);

        // Assert
        Assert.False(character.IsNil, "Character should not be nil");
        Assert.Equal(characterClass, character.GetClassRef());
        Assert.Equal(ObjectFormat.FixedPointer, character.GetFormat());

        // Check that the ASCII value is stored correctly
        var storedValue = character.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex);
        Assert.True(storedValue.IsSmallInteger);
        Assert.Equal(asciiValue, storedValue.AsSmallInteger());
    }

    /// <summary>
    /// Tests CreateAssociation with key and value.
    /// </summary>
    [Fact]
    public void CreateAssociation_HasKeyAndValue()
    {
        // Arrange
        var associationClass = CreateMockClass("Association");
        var key = SObjectRef.FromSmallInteger(42);
        var value = SObjectRef.FromSmallInteger(84);

        // Act
        var association = BlueBookFactory.CreateAssociation(associationClass, key, value);

        // Assert
        Assert.False(association.IsNil, "Association should not be nil");
        Assert.Equal(associationClass, association.GetClassRef());
        Assert.Equal(ObjectFormat.FixedPointer, association.GetFormat());

        // Check key and value
        Assert.Equal(key, association.GetInstanceVariable(BlueBookLayouts.Association.KeyIndex));
        Assert.Equal(value, association.GetInstanceVariable(BlueBookLayouts.Association.ValueIndex));
    }

    /// <summary>
    /// Tests CreateProcess with correct layout.
    /// </summary>
    [Fact]
    public void CreateProcess_HasCorrectLayout()
    {
        // Arrange
        var processClass = CreateMockClass("Process");
        var context = SObjectRef.FromSmallInteger(777); // Mock context
        const int priority = 5;

        // Act
        var process = BlueBookFactory.CreateProcess(processClass, context, priority);

        // Assert
        Assert.False(process.IsNil, "Process should not be nil");
        Assert.Equal(processClass, process.GetClassRef());
        Assert.Equal(ObjectFormat.FixedPointer, process.GetFormat());

        // Check fields
        Assert.True(process.GetInstanceVariable(BlueBookLayouts.Process.NextLinkIndex).IsNil);
        Assert.Equal(context, process.GetInstanceVariable(BlueBookLayouts.Process.SuspendedContextIndex));
        Assert.Equal(priority, process.GetInstanceVariable(BlueBookLayouts.Process.PriorityIndex).AsSmallInteger());
        Assert.True(process.GetInstanceVariable(BlueBookLayouts.Process.MyListIndex).IsNil);
    }

    /// <summary>
    /// Tests CreateSemaphore with correct layout.
    /// </summary>
    [Fact]
    public void CreateSemaphore_HasCorrectLayout()
    {
        // Arrange
        var semaphoreClass = CreateMockClass("Semaphore");
        const int initialCount = 0;

        // Act
        var semaphore = BlueBookFactory.CreateSemaphore(semaphoreClass, initialCount);

        // Assert
        Assert.False(semaphore.IsNil, "Semaphore should not be nil");
        Assert.Equal(semaphoreClass, semaphore.GetClassRef());
        Assert.Equal(ObjectFormat.FixedPointer, semaphore.GetFormat());

        // Check fields - Semaphore doesn't have NextLinkIndex, it has FirstLinkIndex and LastLinkIndex
        Assert.True(semaphore.GetInstanceVariable(BlueBookLayouts.Semaphore.FirstLinkIndex).IsNil);
        Assert.True(semaphore.GetInstanceVariable(BlueBookLayouts.Semaphore.LastLinkIndex).IsNil);
        Assert.Equal(initialCount,
            semaphore.GetInstanceVariable(BlueBookLayouts.Semaphore.ExcessSignalsIndex).AsSmallInteger());
    }

    /// <summary>
    /// Tests CreateDictionary with correct layout.
    /// </summary>
    [Fact]
    public void CreateDictionary_HasCorrectLayout()
    {
        // Arrange
        var dictionaryClass = CreateMockClass("Dictionary");
        const int initialSize = 16;

        // Act - CreateDictionary takes only class and size, not arrayClass
        var dictionary = BlueBookFactory.CreateDictionary(dictionaryClass, initialSize);

        // Assert
        Assert.False(dictionary.IsNil, "Dictionary should not be nil");
        Assert.Equal(dictionaryClass, dictionary.GetClassRef());
        Assert.Equal(ObjectFormat.PointerIndexed, dictionary.GetFormat()); // Dictionary is pointer indexed

        // Dictionary in Blue Book is a Set subclass with Association objects
        // Check tally
        Assert.Equal(0,
            dictionary.GetInstanceVariable(BlueBookLayouts.Dictionary.TallyIndex).AsSmallInteger());

        // Dictionary should have indexed size for associations
        Assert.Equal(initialSize, dictionary.GetIndexedSize());
    }
}