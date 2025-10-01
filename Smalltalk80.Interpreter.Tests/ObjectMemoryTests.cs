using Xunit;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for the ObjectMemoryNew functionality.
/// </summary>
public class ObjectMemoryTests
{
    /// <summary>
    /// Tests that AllocateObject creates objects with correct format and class.
    /// </summary>
    [Fact]
    public void AllocateObject_CreatesObjectWithCorrectFormat()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(42); // Mock class reference
        const ObjectFormat format = ObjectFormat.FixedPointer;
        const int instanceVarCount = 3;
        const int indexedSize = 0;

        // Act
        nint oop = ObjectMemoryNew.AllocateObject(classRef, format, instanceVarCount, indexedSize);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Assert
        Assert.False(objRef.IsNil, "Allocated object should not be nil");
        Assert.True(objRef.IsHeapObject, "Allocated object should be a heap object");
        Assert.False(objRef.IsSmallInteger, "Allocated object should not be a SmallInteger");
        Assert.Equal(classRef, objRef.GetClassRef());
        Assert.Equal(format, objRef.GetFormat());
    }

    /// <summary>
    /// Tests that AllocateObject creates objects with different formats correctly.
    /// </summary>
    [Theory]
    [InlineData(ObjectFormat.FixedPointer, 2, 0)]
    [InlineData(ObjectFormat.PointerIndexed, 2, 5)]
    [InlineData(ObjectFormat.ByteIndexed, 0, 10)]
    [InlineData(ObjectFormat.WordIndexed, 0, 8)]
    public void AllocateObject_HandlesAllFormats(ObjectFormat format, int instanceVarCount, int indexedSize)
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(100);

        // Act
        nint oop = ObjectMemoryNew.AllocateObject(classRef, format, instanceVarCount, indexedSize);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Assert
        Assert.Equal(format, objRef.GetFormat());
        Assert.Equal(classRef, objRef.GetClassRef());
    }

    /// <summary>
    /// Tests instance variable get and set operations.
    /// </summary>
    [Fact]
    public void InstanceVariables_GetAndSet()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(50);
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 3, 0);
        var objRef = SObjectRef.FromObjectPointer(oop);
        var testValue = SObjectRef.FromSmallInteger(123);

        // Act
        objRef.SetInstanceVariable(0, testValue);
        objRef.SetInstanceVariable(1, SObjectRef.Nil);
        objRef.SetInstanceVariable(2, SObjectRef.FromSmallInteger(456));

        // Assert
        Assert.Equal(testValue, objRef.GetInstanceVariable(0));
        Assert.True(objRef.GetInstanceVariable(1).IsNil);
        Assert.Equal(456, objRef.GetInstanceVariable(2).AsSmallInteger());
    }

    /// <summary>
    /// Tests that new objects have instance variables initialized to nil.
    /// </summary>
    [Fact]
    public void AllocateObject_InitializesInstanceVariablesToNil()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(60);
        const int instanceVarCount = 5;

        // Act
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, instanceVarCount, 0);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Assert
        for (int i = 0; i < instanceVarCount; i++)
        {
            Assert.True(objRef.GetInstanceVariable(i).IsNil,
                $"Instance variable {i} should be initialized to nil");
        }
    }

    /// <summary>
    /// Tests indexed byte access for byte-indexed objects.
    /// </summary>
    [Fact]
    public void IndexedBytes_GetAndSet()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(70);
        const int byteCount = 10;
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.ByteIndexed, 0, byteCount);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Act
        for (int i = 0; i < byteCount; i++)
        {
            objRef.SetIndexedByte(i, (byte)(i * 10));
        }

        // Assert
        for (int i = 0; i < byteCount; i++)
        {
            Assert.Equal((byte)(i * 10), objRef.GetIndexedByte(i));
        }
        Assert.Equal(byteCount, objRef.GetIndexedSize());
    }

    /// <summary>
    /// Tests indexed word access for word-indexed objects.
    /// </summary>
    [Fact]
    public void IndexedWords_GetAndSet()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(80);
        const int wordCount = 8;
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.WordIndexed, 0, wordCount);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Act
        for (int i = 0; i < wordCount; i++)
        {
            objRef.SetIndexedWord(i, (ushort)(i * 100 + 1000));
        }

        // Assert
        for (int i = 0; i < wordCount; i++)
        {
            Assert.Equal((ushort)(i * 100 + 1000), objRef.GetIndexedWord(i));
        }
        Assert.Equal(wordCount, objRef.GetIndexedSize());
    }

    /// <summary>
    /// Tests indexed pointer access for pointer-indexed objects.
    /// </summary>
    [Fact]
    public void IndexedPointers_GetAndSet()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(90);
        const int pointerCount = 5;
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.PointerIndexed, 2, pointerCount);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Create some test objects to store as pointers
        var testObjects = new SObjectRef[pointerCount];
        for (int i = 0; i < pointerCount; i++)
        {
            testObjects[i] = SObjectRef.FromSmallInteger(i * 11);
        }

        // Act
        for (int i = 0; i < pointerCount; i++)
        {
            objRef.SetIndexedPointer(i, testObjects[i]);
        }

        // Assert
        for (int i = 0; i < pointerCount; i++)
        {
            Assert.Equal(testObjects[i], objRef.GetIndexedPointer(i));
        }
        Assert.Equal(pointerCount, objRef.GetIndexedSize());
    }

    /// <summary>
    /// Tests the become primitive operation.
    /// </summary>
    [Fact]
    public void Become_SwapsObjectIdentities()
    {
        // Arrange
        var classRef1 = SObjectRef.FromSmallInteger(100);
        var classRef2 = SObjectRef.FromSmallInteger(200);

        nint oop1 = ObjectMemoryNew.AllocateObject(classRef1, ObjectFormat.FixedPointer, 2, 0);
        nint oop2 = ObjectMemoryNew.AllocateObject(classRef2, ObjectFormat.FixedPointer, 3, 0);

        var obj1 = SObjectRef.FromObjectPointer(oop1);
        var obj2 = SObjectRef.FromObjectPointer(oop2);

        // Set different values
        obj1.SetInstanceVariable(0, SObjectRef.FromSmallInteger(111));
        obj2.SetInstanceVariable(0, SObjectRef.FromSmallInteger(222));

        // Act
        ObjectMemoryNew.Become(obj1.AsObjectPointer(), obj2.AsObjectPointer());

        // Assert - Objects should have swapped identities
        // obj1 now has obj2's data
        Assert.Equal(classRef2, obj1.GetClassRef());
        Assert.Equal(SObjectRef.FromSmallInteger(222), obj1.GetInstanceVariable(0));

        // obj2 now has obj1's data
        Assert.Equal(classRef1, obj2.GetClassRef());
        Assert.Equal(SObjectRef.FromSmallInteger(111), obj2.GetInstanceVariable(0));
    }

    /// <summary>
    /// Tests garbage collection marking.
    /// </summary>
    [Fact]
    public void GarbageCollection_MarkingWorks()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(300);
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Act & Assert
        Assert.False(ObjectMemoryNew.IsMarked(objRef.AsObjectPointer()), "Object should not be marked initially");

        ObjectMemoryNew.MarkObject(objRef.AsObjectPointer());
        Assert.True(ObjectMemoryNew.IsMarked(objRef.AsObjectPointer()), "Object should be marked after MarkObject");

        ObjectMemoryNew.ClearMark(objRef.AsObjectPointer());
        Assert.False(ObjectMemoryNew.IsMarked(objRef.AsObjectPointer()), "Object should not be marked after ClearMark");
    }

    /// <summary>
    /// Tests that marking follows object references recursively.
    /// </summary>
    [Fact]
    public void GarbageCollection_MarkingFollowsReferences()
    {
        // Arrange - Create a chain of objects
        var classRef = SObjectRef.FromSmallInteger(400);

        nint oop1 = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);
        nint oop2 = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);
        nint oop3 = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);

        var obj1 = SObjectRef.FromObjectPointer(oop1);
        var obj2 = SObjectRef.FromObjectPointer(oop2);
        var obj3 = SObjectRef.FromObjectPointer(oop3);

        // Create chain: obj1 -> obj2 -> obj3
        obj1.SetInstanceVariable(0, obj2);
        obj2.SetInstanceVariable(0, obj3);
        obj3.SetInstanceVariable(0, SObjectRef.Nil);

        // Act - Mark from root should mark entire chain
        ObjectMemoryNew.MarkObject(obj1.AsObjectPointer());

        // Assert
        Assert.True(ObjectMemoryNew.IsMarked(obj1.AsObjectPointer()), "Root object should be marked");
        Assert.True(ObjectMemoryNew.IsMarked(obj2.AsObjectPointer()), "Referenced object should be marked");
        Assert.True(ObjectMemoryNew.IsMarked(obj3.AsObjectPointer()), "Transitively referenced object should be marked");
    }

    /// <summary>
    /// Tests that allocated OOPs are properly aligned.
    /// </summary>
    [Fact]
    public void AllocateObject_ProducesAlignedOops()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(500);

        // Act - Allocate multiple objects
        for (int i = 0; i < 10; i++)
        {
            nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);

            // Assert - OOP should be 4-byte aligned (LSB should be 0 for object pointers)
            Assert.Equal(0, oop & 0b1);
            Assert.Equal(0, oop & 0b11); // Should be 4-byte aligned
        }
    }
}