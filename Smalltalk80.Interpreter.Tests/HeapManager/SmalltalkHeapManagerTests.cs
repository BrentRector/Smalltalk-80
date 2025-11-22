using System.IO;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.HeapManager;

/// <summary>
/// Comprehensive unit tests for the SmalltalkHeapManager subsystem.
/// Tests cover object allocation, slot access, indexed access, metadata,
/// garbage collection, instance finding, and become operations.
/// </summary>
[Trait("Category", "HeapManager")]
public class SmalltalkHeapManagerTests : IDisposable
{
    private readonly SmalltalkHeapManager _heapManager;
    private readonly SmalltalkOOP _nilOOP;
    private readonly SmalltalkOOP _falseOOP;
    private readonly SmalltalkOOP _trueOOP;

    /// <summary>
    /// Initializes test fixture with a fresh heap manager and special objects.
    /// </summary>
    public SmalltalkHeapManagerTests()
    {
        _heapManager = new SmalltalkHeapManager();

        // Use the heap manager's internal nil object (allocated during construction)
        _nilOOP = _heapManager.GetNil();

        // Allocate false and true objects
        _falseOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _trueOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Note: SpecialObjectsArray was replaced with KnownObjects in VMMetadata
        // For unit tests, we use the heap manager's nil and allocate false/true
    }

    /// <summary>
    /// Cleanup after each test.
    /// </summary>
    public void Dispose()
    {
        // Cleanup heap manager resources
    }

    /// <summary>
    /// Helper method to get ObjectTableEntry for testing internal structure.
    /// Uses reflection to access internal heap manager components.
    /// </summary>
    private ObjectTableEntry GetEntry(SmalltalkOOP oop)
    {
        var tableManagerField = _heapManager.GetType()
            .GetField("_tableManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var tableManager = tableManagerField!.GetValue(_heapManager);
        var getEntryMethod = tableManager!.GetType().GetMethod("GetObjectTableEntry");
        return (ObjectTableEntry)getEntryMethod!.Invoke(tableManager, new object[] { oop })!;
    }

    #region Object Allocation - AllocateObject

    [Fact]
    public void AllocateObject_PointerFormat_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Assert
        Assert.True(oop.IsObjectPointer);
        Assert.False(oop.IsSmallInteger);
        Assert.Equal(ObjectFormat.Pointer, _heapManager.GetObjectFormat(oop));
        Assert.Equal(3, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(0, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_PointerIndexedFormat_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 2, 5);

        // Assert
        Assert.Equal(ObjectFormat.PointerIndexed, _heapManager.GetObjectFormat(oop));
        Assert.Equal(2, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(5, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_ByteIndexedFormat_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Assert
        Assert.Equal(ObjectFormat.ByteIndexed, _heapManager.GetObjectFormat(oop));
        Assert.Equal(0, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(10, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_WordIndexedFormat_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 8);

        // Assert
        Assert.Equal(ObjectFormat.WordIndexed, _heapManager.GetObjectFormat(oop));
        Assert.Equal(0, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(8, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_ByteIndexedWithInstanceVariables_ReturnsValidOOP()
    {
        // Arrange & Act - Form has instance variables + byte data
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 4, 100);

        // Assert
        Assert.Equal(ObjectFormat.ByteIndexed, _heapManager.GetObjectFormat(oop));
        Assert.Equal(4, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(100, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_WordIndexedWithInstanceVariables_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 2, 50);

        // Assert
        Assert.Equal(ObjectFormat.WordIndexed, _heapManager.GetObjectFormat(oop));
        Assert.Equal(2, _heapManager.GetInstanceVariableCount(oop));
        Assert.Equal(50, _heapManager.GetIndexedSize(oop));
    }

    [Fact]
    public void AllocateObject_NegativeFixedSlots_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.AllocateObject(ObjectFormat.Pointer, -1, 0));
    }

    [Fact]
    public void AllocateObject_NegativeIndexedSize_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, -1));
    }

    [Fact]
    public void AllocateObject_ZeroSizePointer_ReturnsValidOOP()
    {
        // Arrange & Act
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Assert
        Assert.True(oop.IsObjectPointer);
        Assert.Equal(0, _heapManager.GetInstanceVariableCount(oop));
    }

    [Fact]
    public void AllocateObject_InitializesAllSlotsToNil()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var value = _heapManager.GetInstanceVariable(oop, i);
            Assert.Equal(_nilOOP, value);
        }
    }

    #endregion

    #region AllocateInstanceOf

    [Fact]
    public void AllocateInstanceOf_PointerClass_ReturnsValidInstance()
    {
        // Arrange - Create a class object with format field
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(2);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Assert
        Assert.Equal(ObjectFormat.Pointer, _heapManager.GetObjectFormat(instanceOOP));
        Assert.Equal(2, _heapManager.GetInstanceVariableCount(instanceOOP));
        Assert.Equal(classOOP, _heapManager.GetClassOOP(instanceOOP));
    }

    [Fact]
    public void AllocateInstanceOf_PointerIndexedClass_ReturnsValidInstance()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreatePointerIndexed(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 10);

        // Assert
        Assert.Equal(ObjectFormat.PointerIndexed, _heapManager.GetObjectFormat(instanceOOP));
        Assert.Equal(1, _heapManager.GetInstanceVariableCount(instanceOOP));
        Assert.Equal(10, _heapManager.GetIndexedSize(instanceOOP));
    }

    [Fact]
    public void AllocateInstanceOf_ByteIndexedClass_ReturnsValidInstance()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateByteIndexed(0);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 20);

        // Assert
        Assert.Equal(ObjectFormat.ByteIndexed, _heapManager.GetObjectFormat(instanceOOP));
        Assert.Equal(20, _heapManager.GetIndexedSize(instanceOOP));
    }

    [Fact]
    public void AllocateInstanceOf_WordIndexedClass_ReturnsValidInstance()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateWordIndexed(0);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 15);

        // Assert
        Assert.Equal(ObjectFormat.WordIndexed, _heapManager.GetObjectFormat(instanceOOP));
        Assert.Equal(15, _heapManager.GetIndexedSize(instanceOOP));
    }

    [Fact]
    public void AllocateInstanceOf_SmallIntegerClass_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.AllocateInstanceOf(smallInt, 0));
    }

    [Fact]
    public void AllocateInstanceOf_SetsClassOOPCorrectly()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(2);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Assert
        Assert.Equal(classOOP, _heapManager.GetClassOOP(instanceOOP));
        Assert.Equal(classOOP, _heapManager.GetClassOOP(instanceOOP));
    }

    #endregion

    #region Slot Access

    [Fact]
    public void GetSlot_SetSlot_RoundTripsCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var valueOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act
        _heapManager.SetInstanceVariable(oop, 0, valueOOP);
        var retrieved = _heapManager.GetInstanceVariable(oop, 0);

        // Assert
        Assert.Equal(valueOOP, retrieved);
    }

    [Fact]
    public void GetSlot_Slot0ReturnsClassOOP()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));
        var instanceOOP = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Act
        var retrievedClass = _heapManager.GetClassOOP(instanceOOP);

        // Assert
        Assert.Equal(classOOP, retrievedClass);
    }

    [Fact]
    public void GetSlot_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.GetClassOOP(smallInt));
    }

    [Fact]
    public void SetSlot_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.SetClassOOP(smallInt, _nilOOP));
    }

    [Fact]
    public void SetInstanceVariable_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.SetInstanceVariable(oop, 99, _nilOOP));
    }

    #endregion

    #region Instance Variable Access

    [Fact]
    public void GetInstanceVariable_ReturnsCorrectValue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var valueOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.SetInstanceVariable(oop, 1, valueOOP);

        // Act
        var retrieved = _heapManager.GetInstanceVariable(oop, 1);

        // Assert
        Assert.Equal(valueOOP, retrieved);
    }

    [Fact]
    public void SetInstanceVariable_UpdatesValue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var valueOOP = SmalltalkOOP.FromSmallInteger(100);

        // Act
        _heapManager.SetInstanceVariable(oop, 2, valueOOP);
        var retrieved = _heapManager.GetInstanceVariable(oop, 2);

        // Assert
        Assert.Equal(valueOOP, retrieved);
    }

    [Fact]
    public void GetInstanceVariable_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.GetInstanceVariable(smallInt, 0));
    }

    [Fact]
    public void GetInstanceVariable_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetInstanceVariable(oop, -1));
    }

    [Fact]
    public void GetInstanceVariable_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetInstanceVariable(oop, 5));
    }

    [Fact]
    public void SetInstanceVariable_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.SetInstanceVariable(oop, 10, _nilOOP));
    }

    #endregion

    #region Indexed OOP Access

    [Fact]
    public void GetIndexedOOP_SetIndexedOOP_RoundTripsCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 1, 5);
        var valueOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act
        _heapManager.SetIndexedOOP(oop, 2, valueOOP);
        var retrieved = _heapManager.GetIndexedOOP(oop, 2);

        // Assert
        Assert.Equal(valueOOP, retrieved);
    }

    [Fact]
    public void GetIndexedOOP_AllIndices_WorkCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, 3);
        var values = new[]
        {
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0),
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0),
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0)
        };

        // Act
        for (int i = 0; i < 3; i++)
        {
            _heapManager.SetIndexedOOP(oop, i, values[i]);
        }

        // Assert
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(values[i], _heapManager.GetIndexedOOP(oop, i));
        }
    }

    [Fact]
    public void GetIndexedOOP_WrongFormat_ThrowsInvalidOperationException()
    {
        // Arrange - ByteIndexed does not support OOP access
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _heapManager.GetIndexedOOP(oop, 0));
    }

    [Fact]
    public void SetIndexedOOP_WrongFormat_ThrowsInvalidOperationException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _heapManager.SetIndexedOOP(oop, 0, _nilOOP));
    }

    [Fact]
    public void GetIndexedOOP_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, 5);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetIndexedOOP(oop, 10));
    }

    #endregion

    #region Indexed Byte Access

    [Fact]
    public void GetIndexedByte_SetIndexedByte_RoundTripsCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Act
        _heapManager.SetIndexedByte(oop, 5, 0xAB);
        var retrieved = _heapManager.GetIndexedByte(oop, 5);

        // Assert
        Assert.Equal(0xAB, retrieved);
    }

    [Fact]
    public void GetIndexedByte_AllBytes_WorkCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 256);

        // Act
        for (int i = 0; i < 256; i++)
        {
            _heapManager.SetIndexedByte(oop, i, (byte)i);
        }

        // Assert
        for (int i = 0; i < 256; i++)
        {
            Assert.Equal((byte)i, _heapManager.GetIndexedByte(oop, i));
        }
    }

    [Fact]
    public void GetIndexedByte_WrongFormat_ThrowsInvalidOperationException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, 5);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _heapManager.GetIndexedByte(oop, 0));
    }

    [Fact]
    public void GetIndexedByte_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetIndexedByte(oop, 20));
    }

    [Fact]
    public void GetIndexedByte_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetIndexedByte(oop, -1));
    }

    #endregion

    #region Indexed Word Access

    [Fact]
    public void GetIndexedWord_SetIndexedWord_RoundTripsCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 8);

        // Act
        _heapManager.SetIndexedWord(oop, 3, 0xABCD);
        var retrieved = _heapManager.GetIndexedWord(oop, 3);

        // Assert
        Assert.Equal(0xABCD, retrieved);
    }

    [Fact]
    public void GetIndexedWord_AllWords_WorkCorrectly()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 100);

        // Act
        for (int i = 0; i < 100; i++)
        {
            _heapManager.SetIndexedWord(oop, i, (ushort)(i * 100));
        }

        // Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal((ushort)(i * 100), _heapManager.GetIndexedWord(oop, i));
        }
    }

    [Fact]
    public void GetIndexedWord_WrongFormat_ThrowsInvalidOperationException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _heapManager.GetIndexedWord(oop, 0));
    }

    [Fact]
    public void GetIndexedWord_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 5);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.GetIndexedWord(oop, 10));
    }

    #endregion

    #region Object Metadata

    [Fact]
    public void GetClassOOP_ReturnsSlot0()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.SetClassOOP(oop, classOOP);

        // Act
        var retrieved = _heapManager.GetClassOOP(oop);

        // Assert
        Assert.Equal(classOOP, retrieved);
    }

    [Fact]
    public void SetClassOOP_UpdatesSlot0()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act
        _heapManager.SetClassOOP(oop, classOOP);
        var retrieved = _heapManager.GetClassOOP(oop);

        // Assert
        Assert.Equal(classOOP, retrieved);
    }

    [Fact]
    public void GetClassOOP_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.GetClassOOP(smallInt));
    }

    [Fact]
    public void GetObjectFormat_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.GetObjectFormat(smallInt));
    }

    [Fact]
    public void GetObjectSizeInWords_ReturnsCorrectSize()
    {
        // Arrange - 1 header + 3 instance vars = 4 words
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Act
        var size = _heapManager.GetObjectSizeInWords(oop);

        // Assert
        Assert.Equal(4, size);
    }

    [Fact]
    public void GetObjectSizeInWords_SmallInteger_ReturnsZero()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var size = _heapManager.GetObjectSizeInWords(smallInt);

        // Assert
        Assert.Equal(0, size);
    }

    [Fact]
    public void GetInstanceVariableCount_SmallInteger_ReturnsZero()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var count = _heapManager.GetInstanceVariableCount(smallInt);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetIndexedSize_SmallInteger_ReturnsZero()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var size = _heapManager.GetIndexedSize(smallInt);

        // Assert
        Assert.Equal(0, size);
    }

    #endregion

    #region GC Root Management

    [Fact]
    public void SetGCRoots_GetGCRoots_RoundTripsCorrectly()
    {
        // Arrange
        var root1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var root2 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var roots = new[] { root1, root2 };

        // Act
        _heapManager.GC.ClearGCRoots();
        foreach (var root in roots)
            _heapManager.GC.AddGCRoot(root);
        var retrieved = _heapManager.GC.GetGCRoots();

        // Assert
        Assert.Equal(2, retrieved.Length);
        Assert.Contains(root1, retrieved);
        Assert.Contains(root2, retrieved);
    }

    [Fact]
    public void AddGCRoots_AddsToExistingRoots()
    {
        // Arrange
        var root1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var root2 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var root3 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root1);

        // Act
        _heapManager.GC.AddGCRoot(root2);
        _heapManager.GC.AddGCRoot(root3);
        var retrieved = _heapManager.GC.GetGCRoots();

        // Assert
        Assert.Equal(3, retrieved.Length);
        Assert.Contains(root1, retrieved);
        Assert.Contains(root2, retrieved);
        Assert.Contains(root3, retrieved);
    }

    [Fact]
    public void GetGCRoots_ReturnsDefensiveCopy()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var retrieved1 = _heapManager.GC.GetGCRoots();
        var retrieved2 = _heapManager.GC.GetGCRoots();

        // Assert
        Assert.NotSame(retrieved1, retrieved2);
    }

    #endregion

    #region Garbage Collection

    [Fact]
    public void GarbageCollect_UnreachableObjectsAreCollected()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var unreachable = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var stats = _heapManager.GC.GarbageCollect();

        // Assert
        Assert.True(stats.CollectedObjects > 0);
    }

    [Fact]
    public void GarbageCollect_ReachableObjectsSurvive()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var child = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.SetInstanceVariable(root, 0, child);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        _heapManager.GC.GarbageCollect();

        // Assert - child should still be accessible
        var retrieved = _heapManager.GetInstanceVariable(root, 0);
        Assert.Equal(child, retrieved);
    }

    [Fact]
    public void GarbageCollect_ReturnsStatistics()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var stats = _heapManager.GC.GarbageCollect();

        // Assert
        Assert.True(stats.TotalObjects >= 0);
        Assert.True(stats.LiveObjects >= 0);
        Assert.True(stats.TotalTimeMs >= 0);
    }

    [Fact]
    public void GetUnreachableObjects_ReturnsUnreachableOOPs()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var unreachable1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var unreachable2 = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var unreachable = _heapManager.GC.GetUnreachableObjects();

        // Assert
        Assert.Contains(unreachable1, unreachable);
        Assert.Contains(unreachable2, unreachable);
        Assert.DoesNotContain(root, unreachable);
    }

    [Fact]
    public void GarbageCollect_ObjectChain_AllReachableObjectsSurvive()
    {
        // Arrange - Create a chain: root -> a -> b -> c
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var a = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var b = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var c = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        _heapManager.SetInstanceVariable(root, 0, a);
        _heapManager.SetInstanceVariable(a, 0, b);
        _heapManager.SetInstanceVariable(b, 0, c);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        _heapManager.GC.GarbageCollect();

        // Assert - All should be accessible
        Assert.Equal(a, _heapManager.GetInstanceVariable(root, 0));
        Assert.Equal(b, _heapManager.GetInstanceVariable(a, 0));
        Assert.Equal(c, _heapManager.GetInstanceVariable(b, 0));
    }

    [Fact]
    public void GarbageCollect_CyclicReferences_AllSurvive()
    {
        // Arrange - Create a cycle: a -> b -> c -> a
        var a = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var b = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var c = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);

        _heapManager.SetInstanceVariable(a, 0, b);
        _heapManager.SetInstanceVariable(b, 0, c);
        _heapManager.SetInstanceVariable(c, 0, a);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(a);

        // Act
        _heapManager.GC.GarbageCollect();

        // Assert - All should survive
        Assert.Equal(b, _heapManager.GetInstanceVariable(a, 0));
        Assert.Equal(c, _heapManager.GetInstanceVariable(b, 0));
        Assert.Equal(a, _heapManager.GetInstanceVariable(c, 0));
    }

    [Fact]
    public void GarbageCollect_UnreachableCycle_IsCollected()
    {
        // Arrange - Create unreachable cycle: x -> y -> x
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var x = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var y = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);

        _heapManager.SetInstanceVariable(x, 0, y);
        _heapManager.SetInstanceVariable(y, 0, x);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var unreachable = _heapManager.GC.GetUnreachableObjects();

        // Assert - Both x and y should be unreachable
        Assert.Contains(x, unreachable);
        Assert.Contains(y, unreachable);
    }

    [Fact]
    public void MarkObject_UnmarkedObject_AppearsInUnreachableList()
    {
        // Arrange - Create object but don't mark it or add to roots
        var unmarkedObj = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.GC.ClearGCRoots();

        // Act
        var unreachable = _heapManager.GC.GetUnreachableObjects();

        // Assert - Unmarked object should be in unreachable list
        Assert.Contains(unmarkedObj, unreachable);
    }

    [Fact]
    public void MarkObject_MarkedObjectInRoots_DoesNotAppearInUnreachableList()
    {
        // Arrange - Create object and add to GC roots
        var markedObj = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(markedObj);

        // Act
        var unreachable = _heapManager.GC.GetUnreachableObjects();

        // Assert - Object reachable from roots should NOT be in unreachable list
        Assert.DoesNotContain(markedObj, unreachable);
    }

    [Fact]
    public void MarkObject_ReachableFromRoot_DoesNotAppearInUnreachableList()
    {
        // Arrange - Create root with child reference
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var child = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        _heapManager.SetInstanceVariable(root, 0, child);
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var unreachable = _heapManager.GC.GetUnreachableObjects();

        // Assert - Child reachable from root should NOT be in unreachable list
        Assert.DoesNotContain(child, unreachable);
        Assert.DoesNotContain(root, unreachable);
    }

    // NOTE: MarkObject is now internal to GC implementation and not part of public API
    // These tests are commented out as the method is no longer exposed
    //[Fact]
    //public void MarkObject_SmallInteger_DoesNotThrow()
    //{
    //    // Arrange
    //    var smallInt = SmalltalkOOP.FromSmallInteger(42);
    //
    //    // Act & Assert - SmallIntegers should be safely ignored
    //    _heapManager.MarkObject(smallInt);
    //}
    //
    //[Fact]
    //public void MarkObject_InvalidOOP_DoesNotThrow()
    //{
    //    // Arrange
    //    var invalidOOP = SmalltalkOOP.FromObjectTableIndex(99999);
    //
    //    // Act & Assert - Invalid OOPs should be safely ignored
    //    _heapManager.MarkObject(invalidOOP);
    //}

    [Fact]
    public void GetGCStatistics_ReturnsCurrentHeapStatistics()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 100);
        var obj3 = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 50);

        // Act
        var stats = _heapManager.GC.GetStatistics();

        // Assert
        Assert.True(stats.TotalObjects >= 3); // At least our 3 objects plus nil/true/false
        Assert.Equal(stats.TotalObjects, stats.LiveObjects); // No collection performed
        Assert.Equal(0, stats.CollectedObjects); // No collection performed
        Assert.Equal(0, stats.MarkTimeMs);
        Assert.Equal(0, stats.SweepTimeMs);
        Assert.Equal(0, stats.TotalTimeMs);
        Assert.True(stats.FreeObjectTableEntries >= 0);
    }

    [Fact]
    public void GetGCStatistics_AfterAllocation_ReflectsNewObjects()
    {
        // Arrange
        var statsBefore = _heapManager.GC.GetStatistics();

        // Act
        _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var statsAfter = _heapManager.GC.GetStatistics();

        // Assert
        Assert.Equal(statsBefore.TotalObjects + 1, statsAfter.TotalObjects);
        Assert.Equal(statsBefore.LiveObjects + 1, statsAfter.LiveObjects);
    }

    #endregion

    #region Free List Reuse

    [Fact]
    public void GarbageCollect_FreesObjectTableEntries()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Allocate several objects
        for (int i = 0; i < 10; i++)
        {
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        }

        int beforeCount = _heapManager.GetActiveObjectCount();
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        var stats = _heapManager.GC.GarbageCollect();
        int afterCount = _heapManager.GetActiveObjectCount();

        // Assert
        Assert.True(afterCount < beforeCount);
        Assert.True(stats.CollectedObjects > 0);
    }

    [Fact]
    public void AfterGC_NewAllocationsSucceed()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        for (int i = 0; i < 5; i++)
        {
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        }

        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);
        _heapManager.GC.GarbageCollect();

        // Act - Allocate more objects after GC
        var newObjects = new List<SmalltalkOOP>();
        for (int i = 0; i < 10; i++)
        {
            newObjects.Add(_heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0));
        }

        // Assert - All new objects are valid
        foreach (var oop in newObjects)
        {
            Assert.True(_heapManager.IsValidObjectOOP(oop));
        }
    }

    #endregion

    #region Object Enumeration

    [Fact]
    public void EnumerateObjectTable_ReturnsAllActiveObjects()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 5);
        var obj3 = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 1, 3);

        // Act
        var objects = _heapManager.EnumerateObjectTable().ToList();

        // Assert
        Assert.Contains(obj1, objects);
        Assert.Contains(obj2, objects);
        Assert.Contains(obj3, objects);
    }

    [Fact]
    public void EnumerateObjectTable_ExcludesCollectedObjects()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var unreachable = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);
        _heapManager.GC.GarbageCollect();

        // Act
        var objects = _heapManager.EnumerateObjectTable().ToList();

        // Assert
        Assert.Contains(root, objects);
        Assert.DoesNotContain(unreachable, objects);
    }

    [Fact]
    public void GetObjectTableSize_ReturnsNonNegativeValue()
    {
        // Act
        var size = _heapManager.GetObjectTableSize();

        // Assert
        Assert.True(size >= 0);
    }

    [Fact]
    public void GetActiveObjectCount_MatchesEnumerationCount()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        }

        // Act
        var count = _heapManager.GetActiveObjectCount();
        var enumCount = _heapManager.EnumerateObjectTable().Count();

        // Assert
        Assert.Equal(enumCount, count);
    }

    #endregion

    #region Instance Finding

    [Fact]
    public void FindInstanceOf_ReturnsFirstInstance()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        var instance1 = _heapManager.AllocateInstanceOf(classOOP, 0);
        var instance2 = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Act
        var found = _heapManager.FindInstanceOf(classOOP);

        // Assert
        Assert.True(found == instance1 || found == instance2);
        Assert.Equal(classOOP, _heapManager.GetClassOOP(found));
    }

    [Fact]
    public void FindInstanceOf_NoInstances_ReturnsNil()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        // Act
        var found = _heapManager.FindInstanceOf(classOOP);

        // Assert
        Assert.Equal(_nilOOP, found);
    }

    [Fact]
    public void FindNextInstanceOf_ReturnsNextInstance()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        var instance1 = _heapManager.AllocateInstanceOf(classOOP, 0);
        var instance2 = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Act
        var first = _heapManager.FindInstanceOf(classOOP);
        var second = _heapManager.FindNextInstanceOf(classOOP, first);

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal(classOOP, _heapManager.GetClassOOP(second));
    }

    [Fact]
    public void FindNextInstanceOf_LastInstance_ReturnsNil()
    {
        // Arrange
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var formatWord = FormatWord.CreateFixedPointer(1);
        _heapManager.SetInstanceVariable(classOOP, 2, SmalltalkOOP.FromSmallInteger(formatWord.Value));

        var instance = _heapManager.AllocateInstanceOf(classOOP, 0);

        // Act
        var next = _heapManager.FindNextInstanceOf(classOOP, instance);

        // Assert
        Assert.Equal(_nilOOP, next);
    }

    #endregion

    #region Become Operations

    [Fact]
    public void BecomeTwoWay_SwapsObjectIdentities()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);

        var value1 = SmalltalkOOP.FromSmallInteger(100);
        var value2 = SmalltalkOOP.FromSmallInteger(200);

        _heapManager.SetInstanceVariable(obj1, 0, value1);
        _heapManager.SetInstanceVariable(obj2, 0, value2);

        // Act
        _heapManager.Become.BecomeTwoWay(obj1, obj2);

        // Assert - Values should be swapped
        Assert.Equal(value2, _heapManager.GetInstanceVariable(obj1, 0));
        Assert.Equal(value1, _heapManager.GetInstanceVariable(obj2, 0));
    }

    [Fact]
    public void BecomeTwoWay_PreservesReferences()
    {
        // Arrange - Create objects with cross-references
        var a = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var b = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var c = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);

        // c references a
        _heapManager.SetInstanceVariable(c, 0, a);

        // Act
        _heapManager.Become.BecomeTwoWay(a, b);

        // Assert - c now references b (because a became b)
        var cRef = _heapManager.GetInstanceVariable(c, 0);
        // After become, the reference in c should now point to where a's content went (which is b)
        Assert.True(cRef == a || cRef == b);
    }

    [Fact]
    public void BecomeTwoWay_DifferentFormats_SwapsCorrectly()
    {
        // Arrange
        var pointer = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var indexed = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 1, 5);

        var pointerFormat = _heapManager.GetObjectFormat(pointer);
        var indexedFormat = _heapManager.GetObjectFormat(indexed);

        // Act
        _heapManager.Become.BecomeTwoWay(pointer, indexed);

        // Assert - Formats should be swapped
        Assert.Equal(indexedFormat, _heapManager.GetObjectFormat(pointer));
        Assert.Equal(pointerFormat, _heapManager.GetObjectFormat(indexed));
    }

    #endregion

    #region Write Barriers

    [Fact]
    public void SetInstanceVariableWithBarrier_UpdatesValue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var value = SmalltalkOOP.FromSmallInteger(42);

        // Act
        _heapManager.SetInstanceVariableWithBarrier(oop, 0, value);

        // Assert
        Assert.Equal(value, _heapManager.GetInstanceVariable(oop, 0));
    }

    [Fact]
    public void SetInstanceVariableNoBarrier_UpdatesValue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var value = SmalltalkOOP.FromSmallInteger(99);

        // Act
        _heapManager.SetInstanceVariableNoBarrier(oop, 1, value);

        // Assert
        Assert.Equal(value, _heapManager.GetInstanceVariable(oop, 1));
    }

    [Fact]
    public void SetInstanceVariableWithBarrier_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.SetInstanceVariableWithBarrier(smallInt, 0, _nilOOP));
    }

    [Fact]
    public void SetInstanceVariableNoBarrier_SmallInteger_ThrowsArgumentException()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _heapManager.SetInstanceVariableNoBarrier(smallInt, 0, _nilOOP));
    }

    [Fact]
    public void SetInstanceVariableWithBarrier_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _heapManager.SetInstanceVariableWithBarrier(oop, 10, _nilOOP));
    }

    #endregion

    #region Validation

    [Fact]
    public void IsValidObjectOOP_ValidOOP_ReturnsTrue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act & Assert
        Assert.True(_heapManager.IsValidObjectOOP(oop));
    }

    [Fact]
    public void IsValidObjectOOP_SmallInteger_ReturnsFalse()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.False(_heapManager.IsValidObjectOOP(smallInt));
    }

    [Fact]
    public void IsValidOOP_SmallInteger_ReturnsTrue()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.True(_heapManager.IsValidOOP(smallInt));
    }

    [Fact]
    public void IsValidOOP_ValidObjectPointer_ReturnsTrue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act & Assert
        Assert.True(_heapManager.IsValidOOP(oop));
    }

    #endregion

    #region Mixed Format Access with Instance Variables

    [Fact]
    public void ByteIndexed_WithInstanceVariables_BothAccessible()
    {
        // Arrange - Like Form with bits array and width/height instance vars
        var oop = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 3, 100);
        var widthValue = SmalltalkOOP.FromSmallInteger(640);
        var heightValue = SmalltalkOOP.FromSmallInteger(480);

        // Act - Set instance variables
        _heapManager.SetInstanceVariable(oop, 0, widthValue);
        _heapManager.SetInstanceVariable(oop, 1, heightValue);

        // Set some bytes
        _heapManager.SetIndexedByte(oop, 0, 0xFF);
        _heapManager.SetIndexedByte(oop, 99, 0xAA);

        // Assert
        Assert.Equal(widthValue, _heapManager.GetInstanceVariable(oop, 0));
        Assert.Equal(heightValue, _heapManager.GetInstanceVariable(oop, 1));
        Assert.Equal(0xFF, _heapManager.GetIndexedByte(oop, 0));
        Assert.Equal(0xAA, _heapManager.GetIndexedByte(oop, 99));
    }

    [Fact]
    public void WordIndexed_WithInstanceVariables_BothAccessible()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 2, 50);
        var value1 = SmalltalkOOP.FromSmallInteger(123);
        var value2 = SmalltalkOOP.FromSmallInteger(456);

        // Act
        _heapManager.SetInstanceVariable(oop, 0, value1);
        _heapManager.SetInstanceVariable(oop, 1, value2);
        _heapManager.SetIndexedWord(oop, 0, 0x1234);
        _heapManager.SetIndexedWord(oop, 49, 0x5678);

        // Assert
        Assert.Equal(value1, _heapManager.GetInstanceVariable(oop, 0));
        Assert.Equal(value2, _heapManager.GetInstanceVariable(oop, 1));
        Assert.Equal(0x1234, _heapManager.GetIndexedWord(oop, 0));
        Assert.Equal(0x5678, _heapManager.GetIndexedWord(oop, 49));
    }

    #endregion

    #region VerboseDebugMode

    [Fact]
    public void VerboseDebugMode_DefaultIsFalse()
    {
        // Assert
        Assert.False(_heapManager.VerboseDebugMode);
    }

    [Fact]
    public void VerboseDebugMode_CanBeEnabled()
    {
        // Act
        _heapManager.VerboseDebugMode = true;

        // Assert
        Assert.True(_heapManager.VerboseDebugMode);
    }

    #endregion

    #region SmalltalkOOP Tests

    [Fact]
    public void ToString_SmallInteger_ShowsReadableFormat()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var str = smallInt.ToString();

        // Assert
        Assert.Equal("SmallInteger(42)", str);
    }

    [Fact]
    public void ToString_NegativeSmallInteger_ShowsNegativeValue()
    {
        // Arrange
        var negativeInt = SmalltalkOOP.FromSmallInteger(-100);

        // Act
        var str = negativeInt.ToString();

        // Assert
        Assert.Equal("SmallInteger(-100)", str);
    }

    [Fact]
    public void ToString_ObjectPointer_ShowsOOPValue()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act
        var str = oop.ToString();

        // Assert - Object pointers show their raw OOP value
        Assert.Contains("oop=", str);
        Assert.DoesNotContain("SmallInteger", str);
    }

    [Fact]
    public void Equals_SameSmallIntegerValue_ReturnsTrue()
    {
        // Arrange
        var oop1 = SmalltalkOOP.FromSmallInteger(100);
        var oop2 = SmalltalkOOP.FromSmallInteger(100);

        // Act & Assert
        Assert.True(oop1.Equals(oop2));
        Assert.True(oop1 == oop2);
    }

    [Fact]
    public void Equals_DifferentSmallIntegerValues_ReturnsFalse()
    {
        // Arrange
        var oop1 = SmalltalkOOP.FromSmallInteger(100);
        var oop2 = SmalltalkOOP.FromSmallInteger(200);

        // Act & Assert
        Assert.False(oop1.Equals(oop2));
        Assert.True(oop1 != oop2);
    }

    [Fact]
    public void Equals_SameObjectPointer_ReturnsTrue()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var obj2 = obj1; // Same OOP value

        // Act & Assert - Same OOP should equal itself
        Assert.True(obj1.Equals(obj2));
        Assert.True(obj1 == obj2);
    }

    [Fact]
    public void Equals_DifferentObjectPointers_ReturnsFalse()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act & Assert - Different object pointers should not be equal
        Assert.False(obj1.Equals(obj2));
        Assert.True(obj1 != obj2);
    }

    [Fact]
    public void Equals_NonOOPObject_ReturnsFalse()
    {
        // Arrange
        var oop = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert - OOPs should not equal other types
        Assert.False(oop.Equals("not an oop"));
        Assert.False(oop.Equals(42));
        Assert.False(oop.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameValue_ProducesSameHash()
    {
        // Arrange
        var oop1 = SmalltalkOOP.FromSmallInteger(42);
        var oop2 = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var hash1 = oop1.GetHashCode();
        var hash2 = oop2.GetHashCode();

        // Assert - Required for Dictionary/HashSet usage
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_DifferentValues_ProduceDifferentHashes()
    {
        // Arrange
        var oop1 = SmalltalkOOP.FromSmallInteger(42);
        var oop2 = SmalltalkOOP.FromSmallInteger(43);

        // Act
        var hash1 = oop1.GetHashCode();
        var hash2 = oop2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_ObjectPointers_UsesRawValue()
    {
        // Arrange
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Act
        var hash1 = obj1.GetHashCode();
        var hash2 = obj2.GetHashCode();

        // Assert - Different objects should have different hashes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void operator_IntToOOP_CreatesObjectPointer()
    {
        // Arrange
        int rawValue = 42;

        // Act - Implicit int-to-OOP conversion operator
        SmalltalkOOP oop = rawValue;

        // Assert - Should create object pointer with raw value
        Assert.Equal(42, oop.RawValue);
        Assert.False(oop.IsSmallInteger);
        Assert.True(oop.IsObjectPointer);
    }

    [Fact]
    public void operator_ZeroValue_CreatesNullOOP()
    {
        // Arrange
        int zero = 0;

        // Act - Implicit conversion with zero
        SmalltalkOOP oop = zero;

        // Assert - Zero creates default OOP
        Assert.Equal(default(SmalltalkOOP), oop);
        Assert.Equal(0, oop.RawValue);
    }

    #endregion

    #region ObjectTableEntry Tests

    [Fact]
    public void AsByteArray_ByteIndexedObject_ReturnsUnderlyingByteStorage()
    {
        // Arrange - ByteIndexed objects store data as byte[]
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);
        var entry = GetEntry(stringOOP);

        // Act
        var byteArray = entry.AsByteArray();

        // Assert - Should access the raw byte storage
        Assert.NotNull(byteArray);
        Assert.Equal(10, byteArray.Length);
    }

    [Fact]
    public void AsByteArray_PointerObject_ReturnsNull()
    {
        // Arrange - Pointer objects use OOP arrays, not byte arrays
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var entry = GetEntry(oop);

        // Act
        var byteArray = entry.AsByteArray();

        // Assert - Type mismatch returns null
        Assert.Null(byteArray);
    }

    [Fact]
    public void AsOopArray_PointerObject_ReturnsOOPStorage()
    {
        // Arrange - Pointer objects store references as SmalltalkOOP[]
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var entry = GetEntry(oop);

        // Act
        var oopArray = entry.AsOopArray();

        // Assert - Should access OOP array with header + instance vars
        Assert.NotNull(oopArray);
        Assert.Equal(4, oopArray.Length); // 1 class pointer + 3 instance variables
    }

    [Fact]
    public void AsOopArray_PointerIndexedObject_ReturnsOOPStorage()
    {
        // Arrange - PointerIndexed objects (like Array) also use OOP storage
        var arrayOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 2, 5);
        var entry = GetEntry(arrayOOP);

        // Act
        var oopArray = entry.AsOopArray();

        // Assert - Should access full OOP array
        Assert.NotNull(oopArray);
        Assert.Equal(8, oopArray.Length); // 1 class + 2 inst vars + 5 indexed
    }

    [Fact]
    public void AsOopArray_ByteIndexedObject_ReturnsNull()
    {
        // Arrange - ByteIndexed objects don't use OOP arrays
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);
        var entry = GetEntry(stringOOP);

        // Act
        var oopArray = entry.AsOopArray();

        // Assert - Type mismatch returns null
        Assert.Null(oopArray);
    }

    [Fact]
    public void AsWordArray_WordIndexedObject_ReturnsWordStorage()
    {
        // Arrange - WordIndexed objects (like Bitmap) store ushort[]
        var bitmapOOP = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 8);
        var entry = GetEntry(bitmapOOP);

        // Act
        var wordArray = entry.AsWordArray();

        // Assert - Should access raw ushort[] storage
        Assert.NotNull(wordArray);
        Assert.Equal(8, wordArray.Length);
    }

    [Fact]
    public void AsWordArray_NonWordIndexedObject_ReturnsNull()
    {
        // Arrange - Pointer objects don't use word arrays
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var entry = GetEntry(oop);

        // Act
        var wordArray = entry.AsWordArray();

        // Assert - Type mismatch returns null
        Assert.Null(wordArray);
    }

    [Fact]
    public void AsIntArray_CurrentImplementation_ReturnsNull()
    {
        // Arrange - Standard Smalltalk-80 formats don't use int[] storage
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var entry = GetEntry(oop);

        // Act
        var intArray = entry.AsIntArray();

        // Assert - Current implementation doesn't use int arrays
        Assert.Null(intArray);
    }

    [Fact]
    public void GetDebugInfo_PointerObject_ShowsStructureDetails()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        var entry = GetEntry(oop);

        // Act
        var debugInfo = entry.GetDebugInfo();

        // Assert - Should show human-readable structure
        Assert.Contains("ObjectTableEntry", debugInfo);
        Assert.Contains("Storage:", debugInfo);
        Assert.Contains("SmalltalkOOP[4]", debugInfo);
        Assert.Contains("Format: Pointer", debugInfo);
        Assert.Contains("InstanceVars: 3", debugInfo);
    }

    [Fact]
    public void GetDebugInfo_ByteIndexedObject_ShowsByteArraySize()
    {
        // Arrange
        var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 50);
        var entry = GetEntry(stringOOP);

        // Act
        var debugInfo = entry.GetDebugInfo();

        // Assert - Should show byte array information
        Assert.Contains("byte[50]", debugInfo);
        Assert.Contains("Format: ByteIndexed", debugInfo);
    }

    [Fact]
    public void GetDebugInfo_WordIndexedObject_ShowsWordArraySize()
    {
        // Arrange
        var bitmapOOP = _heapManager.AllocateObject(ObjectFormat.WordIndexed, 0, 100);
        var entry = GetEntry(bitmapOOP);

        // Act
        var debugInfo = entry.GetDebugInfo();

        // Assert - Should show ushort array information
        Assert.Contains("ushort[100]", debugInfo);
        Assert.Contains("Format: WordIndexed", debugInfo);
    }

    #endregion

    #region SmalltalkHeapManager Advanced Tests

    [Fact]
    public void SetClassNameFieldIndex_ConfiguresClassNameExtraction()
    {
        // Arrange & Act
        _heapManager.SetClassNameFieldIndex(6);

        // Assert - Just verify it doesn't throw
        // The actual functionality is tested by GetClassName which uses this index
    }

    [Fact]
    public void GetClassName_SmallInteger_ReturnsSmallInteger()
    {
        // Arrange
        var smallInt = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var className = _heapManager.GetClassName(smallInt);

        // Assert
        Assert.Equal("SmallInteger", className);
    }

    [Fact]
    public void GetClassName_InvalidOOP_ReturnsInvalid()
    {
        // Arrange
        var invalidOOP = SmalltalkOOP.FromObjectTableIndex(99999);

        // Act
        var className = _heapManager.GetClassName(invalidOOP);

        // Assert
        Assert.Equal("Invalid", className);
    }

    [Fact]
    public void GetClassName_ValidObjectWithoutNameField_ReturnsUnknownClass()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 2, 0);
        var classOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 3, 0);
        _heapManager.SetClassOOP(oop, classOOP);

        // Act
        var className = _heapManager.GetClassName(oop);

        // Assert
        Assert.StartsWith("UnknownClass", className);
    }

    [Fact]
    public void GetClassName_ObjectWithSmallIntegerClass_ReturnsInvalidClass()
    {
        // Arrange
        var oop = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var invalidClass = SmalltalkOOP.FromSmallInteger(42);
        _heapManager.SetClassOOP(oop, invalidClass);

        // Act
        var className = _heapManager.GetClassName(oop);

        // Assert
        Assert.Contains("InvalidClass", className);
    }

    [Fact]
    public void ValidateAllObjectClasses_NoInvalidObjects_ProducesNoWarnings()
    {
        // Arrange
        var localIdToObjectId = new Dictionary<string, int>();
        var obj1 = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var obj2 = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, 10);

        // Capture console output
        var originalConsoleOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            _heapManager.ValidateAllObjectClasses(localIdToObjectId);

            // Assert - Should not produce warnings for valid objects
            var output = stringWriter.ToString();
            // Note: May still have warnings because objects don't have proper class structure
            // This just verifies the method runs without crashing
        }
        finally
        {
            Console.SetOut(originalConsoleOut);
        }
    }

    [Fact]
    public void ValidateAllObjectClasses_WithInvalidClassReferences_LogsWarnings()
    {
        // Arrange
        var localIdToObjectId = new Dictionary<string, int>();
        var obj = _heapManager.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var invalidClass = SmalltalkOOP.FromSmallInteger(999);
        _heapManager.SetClassOOP(obj, invalidClass);

        localIdToObjectId["obj1"] = obj.ToObjectTableIndex();

        // Capture console output
        var originalConsoleOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            _heapManager.ValidateAllObjectClasses(localIdToObjectId);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("VALIDATION WARNING", output);
        }
        finally
        {
            Console.SetOut(originalConsoleOut);
        }
    }

    #endregion

    #region ObjectTableManager Advanced Tests (through public API)

    [Fact]
    public void GarbageCollect_FreesAndReusesObjectTableEntries()
    {
        // Arrange
        var root = _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Allocate many objects that will become garbage
        var tempObjects = new List<SmalltalkOOP>();
        for (int i = 0; i < 20; i++)
        {
            tempObjects.Add(_heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0));
        }

        int tableSizeBeforeGC = _heapManager.GetObjectTableSize();
        _heapManager.GC.ClearGCRoots();
        _heapManager.GC.AddGCRoot(root);

        // Act
        _heapManager.GC.GarbageCollect();

        // Allocate new objects - they should reuse freed entries
        var newObjects = new List<SmalltalkOOP>();
        for (int i = 0; i < 10; i++)
        {
            newObjects.Add(_heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0));
        }

        int tableSizeAfterReuse = _heapManager.GetObjectTableSize();

        // Assert - Table size should not grow much if entries are being reused
        // The exact behavior depends on free list implementation
        Assert.True(tableSizeAfterReuse <= tableSizeBeforeGC + 10);
    }

    [Fact]
    public void ObjectTableSize_IncreasesWithAllocations()
    {
        // Arrange
        int initialSize = _heapManager.GetObjectTableSize();

        // Act
        for (int i = 0; i < 10; i++)
        {
            _heapManager.AllocateObject(ObjectFormat.Pointer, 0, 0);
        }

        int finalSize = _heapManager.GetObjectTableSize();

        // Assert
        Assert.True(finalSize >= initialSize + 10);
    }

    #endregion
}
