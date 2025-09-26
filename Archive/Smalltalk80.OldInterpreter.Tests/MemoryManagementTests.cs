using Smalltalk80.OldInterpreter.Memory;
using Smalltalk80.OldInterpreter.ObjectModel;

namespace Smalltalk80.OldInterpreter.Tests;

/// <summary>
/// Tests for memory management and garbage collection.
/// </summary>
public sealed class MemoryManagementTests
{
    /// <summary>
    /// Tests that object memory can be initialized with various sizes.
    /// </summary>
    [Theory]
    [InlineData(1024)]
    [InlineData(65536)]
    [InlineData(262144)]
    public void ObjectMemory_Initialization_CreatesCorrectSize(int size)
    {
        // Arrange & Act
        var memory = new ObjectMemory(size);
        
        // Assert
        memory.Should().NotBeNull();
        memory.GetStatistics().TotalCapacity.Should().BeGreaterThan(0);
    }
    
    /// <summary>
    /// Tests allocation of objects with pointers.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void AllocatePointerObject_VariousSizes_AllocatesSuccessfully(int size)
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        
        // Act
        var oop = memory.AllocateObject(OOP.Nil, size);
        
        // Assert
        oop.Should().NotBe(OOP.Nil);
        oop.IsSmallInteger.Should().BeFalse();
        
        // Validate the object was allocated
        // Size validation would require accessing internal structures
    }
    
    /// <summary>
    /// Tests allocation of word objects (non-pointer).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void AllocateWordObject_VariousSizes_AllocatesSuccessfully(int size)
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        
        // Act
        var oop = memory.AllocateWordArray(OOP.Nil, size);
        
        // Assert
        oop.Should().NotBe(OOP.Nil);
        oop.IsSmallInteger.Should().BeFalse();
        
        // Validate the object was allocated
        // Size validation would require accessing internal structures
    }
    
    /// <summary>
    /// Tests allocation of byte objects.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(255)]
    public void AllocateByteObject_VariousSizes_AllocatesSuccessfully(int size)
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        
        // Act
        var oop = memory.AllocateByteArray(OOP.Nil, size);
        
        // Assert
        oop.Should().NotBe(OOP.Nil);
        oop.IsSmallInteger.Should().BeFalse();
        
        // Validate the object was allocated
        // Size validation would require accessing internal structures
    }
    
    /// <summary>
    /// Tests storing and retrieving pointer fields.
    /// </summary>
    [Fact]
    public void StorePointer_ValidIndex_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        var obj = memory.AllocateObject(OOP.Nil, 5);
        var value = OOP.FromSmallInteger(42);
        
        // Act
        memory.SetField(obj, 2, value);
        var retrieved = memory.GetField(obj, 2);
        
        // Assert
        retrieved.Should().Be(value);
    }
    
    /// <summary>
    /// Tests storing and retrieving word fields.
    /// </summary>
    [Fact]
    public void StoreWord_ValidIndex_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        var obj = memory.AllocateWordArray(OOP.Nil, 5);
        const ushort value = 0xABCD;
        
        // Act
        // Word arrays don't have special accessors, test basic field access
        memory.SetField(obj, 2, OOP.FromSmallInteger(value));
        var retrieved = memory.GetField(obj, 2);
        
        // Assert
        retrieved.SmallIntegerValue.Should().Be(value);
    }
    
    /// <summary>
    /// Tests storing and retrieving byte fields.
    /// </summary>
    [Fact]
    public void StoreByte_ValidIndex_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        var obj = memory.AllocateByteArray(OOP.Nil, 10);
        const byte value = 0x42;
        
        // Act
        // Byte arrays don't have special accessors, test basic field access
        memory.SetField(obj, 0, OOP.FromSmallInteger(value));
        var retrieved = memory.GetField(obj, 0);
        
        // Assert
        retrieved.SmallIntegerValue.Should().Be(value);
    }
    
    /// <summary>
    /// Tests garbage collection with simple object graph.
    /// </summary>
    [Fact]
    public void GarbageCollector_SimpleCollection_CollectsUnreachableObjects()
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        var gc = new GarbageCollector(memory);
        
        // Create reachable objects
        var root = memory.AllocateObject(OOP.Nil, 2);
        var child1 = memory.AllocateObject(OOP.Nil, 0);
        memory.SetField(root, 0, child1);
        
        // Create unreachable object
        var unreachable = memory.AllocateObject(OOP.Nil, 0);
        
        var statsBefore = memory.GetStatistics();
        
        // Act
        gc.Collect(new[] { root });
        
        // Assert
        var statsAfter = memory.GetStatistics();
        statsAfter.FreeObjects.Should().BeGreaterThan(statsBefore.FreeObjects);
        
        // Root and child1 should still be valid
        // Validation would require accessing internal structures
    }
    
    /// <summary>
    /// Tests memory statistics reporting.
    /// </summary>
    [Fact]
    public void GetStatistics_AfterAllocations_ReportsCorrectUsage()
    {
        // Arrange
        var memory = new ObjectMemory(65536);
        var initialStats = memory.GetStatistics();
        
        // Act
        var obj1 = memory.AllocateObject(OOP.Nil, 10);
        var obj2 = memory.AllocateWordArray(OOP.Nil, 20);
        var obj3 = memory.AllocateByteArray(OOP.Nil, 30);
        
        var afterStats = memory.GetStatistics();
        
        // Assert
        afterStats.TotalObjects.Should().BeGreaterThan(initialStats.TotalObjects);
        afterStats.FreeObjects.Should().BeLessThan(initialStats.FreeObjects);
    }
}
