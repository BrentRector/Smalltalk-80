using Xunit;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for SObjectRef operations.
/// </summary>
public class SObjectRefTests
{
    /// <summary>
    /// Tests SmallInteger creation and value extraction.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(1000000)]
    [InlineData(-1000000)]
    [InlineData(1073741823)]  // Max SmallInteger (2^30 - 1)
    [InlineData(-1073741824)] // Min SmallInteger (-2^30)
    public void SmallInteger_CreateAndExtract(int value)
    {
        // Act
        var smallInt = SObjectRef.FromSmallInteger(value);

        // Assert
        Assert.True(smallInt.IsSmallInteger, "Should be a SmallInteger");
        Assert.False(smallInt.IsNil, "Should not be nil");
        Assert.False(smallInt.IsObject, "Should not be an object pointer");
        Assert.False(smallInt.IsHeapObject, "Should not be a heap object");
        Assert.True(smallInt.IsImmediate, "Should be an immediate value");
        Assert.Equal(value, smallInt.AsSmallInteger());
    }

    /// <summary>
    /// Tests that AsSmallInteger throws when called on non-SmallInteger.
    /// </summary>
    [Fact]
    public void AsSmallInteger_ThrowsForNonSmallInteger()
    {
        // Arrange
        var nil = SObjectRef.Nil;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => nil.AsSmallInteger());
    }

    /// <summary>
    /// Tests nil checks.
    /// </summary>
    [Fact]
    public void Nil_PropertiesAreCorrect()
    {
        // Arrange
        var nil = SObjectRef.Nil;

        // Assert
        Assert.True(nil.IsNil, "Nil should report IsNil as true");
        Assert.False(nil.IsSmallInteger, "Nil should not be a SmallInteger");
        Assert.False(nil.IsObject, "Nil should not be an object (it's the null pointer)");
        Assert.True(nil.IsHeapObject, "Nil is considered a heap object (not immediate)");
        Assert.False(nil.IsImmediate, "Nil should not be an immediate value");
    }

    /// <summary>
    /// Tests heap object checks.
    /// </summary>
    [Fact]
    public void HeapObject_PropertiesAreCorrect()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(100);
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 2, 0);
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Assert
        Assert.False(objRef.IsNil, "Heap object should not be nil");
        Assert.False(objRef.IsSmallInteger, "Heap object should not be a SmallInteger");
        Assert.True(objRef.IsObject, "Should be an object pointer");
        Assert.True(objRef.IsHeapObject, "Should be a heap object");
        Assert.False(objRef.IsImmediate, "Should not be an immediate value");
    }

    /// <summary>
    /// Tests FitsInSmallInteger for various values.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(1073741823, true)]   // 2^30 - 1 (max)
    [InlineData(-1073741824, true)]  // -2^30 (min)
    [InlineData(1073741824, false)]  // 2^30 (too large)
    [InlineData(-1073741825, false)] // -2^30 - 1 (too small)
    [InlineData(2147483647, false)]  // int.MaxValue (too large)
    [InlineData(-2147483648, false)] // int.MinValue (too small)
    public void FitsInSmallInteger_ChecksRange(long value, bool expectedFits)
    {
        // Act
        bool fits = SObjectRef.FitsInSmallInteger(value);

        // Assert
        Assert.Equal(expectedFits, fits);
    }

    /// <summary>
    /// Tests FastIntegerAdd with no overflow.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(100, 200, 300)]
    [InlineData(-100, -200, -300)]
    [InlineData(1000000, 2000000, 3000000)]
    public void FastIntegerAdd_NoOverflow(int a, int b, int expected)
    {
        // Arrange
        var aRef = SObjectRef.FromSmallInteger(a);
        var bRef = SObjectRef.FromSmallInteger(b);

        // Act
        var result = aRef.FastIntegerAdd(bRef);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.Equal(expected, result.Value.AsSmallInteger());
    }

    /// <summary>
    /// Tests FastIntegerAdd with overflow.
    /// </summary>
    [Fact]
    public void FastIntegerAdd_WithOverflow()
    {
        // Arrange - Values that will overflow SmallInteger range
        var maxSmallInt = SObjectRef.FromSmallInteger(1073741823);  // 2^30 - 1
        var one = SObjectRef.FromSmallInteger(1);

        // Act
        var result = maxSmallInt.FastIntegerAdd(one);

        // Assert
        Assert.Null(result); // Should return null on overflow
    }

    /// <summary>
    /// Tests FastIntegerSubtract with no overflow.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(5, 3, 2)]
    [InlineData(3, 5, -2)]
    [InlineData(-5, -3, -2)]
    [InlineData(100, 50, 50)]
    [InlineData(-100, 50, -150)]
    public void FastIntegerSubtract_NoOverflow(int a, int b, int expected)
    {
        // Arrange
        var aRef = SObjectRef.FromSmallInteger(a);
        var bRef = SObjectRef.FromSmallInteger(b);

        // Act
        var result = aRef.FastIntegerSubtract(bRef);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.Equal(expected, result.Value.AsSmallInteger());
    }

    /// <summary>
    /// Tests FastIntegerSubtract with overflow.
    /// </summary>
    [Fact]
    public void FastIntegerSubtract_WithOverflow()
    {
        // Arrange - Values that will overflow SmallInteger range
        var minSmallInt = SObjectRef.FromSmallInteger(-1073741824);  // -2^30
        var one = SObjectRef.FromSmallInteger(1);

        // Act
        var result = minSmallInt.FastIntegerSubtract(one);

        // Assert
        Assert.Null(result); // Should return null on overflow
    }

    /// <summary>
    /// Tests FastIntegerMultiply with no overflow.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 3, 6)]
    [InlineData(-2, 3, -6)]
    [InlineData(10, 10, 100)]
    [InlineData(100, 100, 10000)]
    [InlineData(-5, -5, 25)]
    public void FastIntegerMultiply_NoOverflow(int a, int b, int expected)
    {
        // Arrange
        var aRef = SObjectRef.FromSmallInteger(a);
        var bRef = SObjectRef.FromSmallInteger(b);

        // Act
        var result = aRef.FastIntegerMultiply(bRef);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.Equal(expected, result.Value.AsSmallInteger());
    }

    /// <summary>
    /// Tests FastIntegerMultiply with overflow.
    /// </summary>
    [Fact]
    public void FastIntegerMultiply_WithOverflow()
    {
        // Arrange - Values that will overflow SmallInteger range
        var largeValue = SObjectRef.FromSmallInteger(100000);
        var multiplier = SObjectRef.FromSmallInteger(100000); // 10^10 > 2^30

        // Act
        var result = largeValue.FastIntegerMultiply(multiplier);

        // Assert
        Assert.Null(result); // Should return null on overflow
    }

    /// <summary>
    /// Tests FastIntegerLessThan comparison.
    /// </summary>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(-1, 0, true)]
    [InlineData(0, -1, false)]
    [InlineData(10, 20, true)]
    [InlineData(20, 10, false)]
    [InlineData(-10, -5, true)]
    [InlineData(-5, -10, false)]
    public void FastIntegerLessThan_ComparesCorrectly(int a, int b, bool expected)
    {
        // Arrange
        var aRef = SObjectRef.FromSmallInteger(a);
        var bRef = SObjectRef.FromSmallInteger(b);

        // Act
        bool result = aRef.FastIntegerLessThan(bRef);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests AreIdentical for SmallIntegers.
    /// </summary>
    [Fact]
    public void AreIdentical_SmallIntegers()
    {
        // Arrange
        var a1 = SObjectRef.FromSmallInteger(42);
        var a2 = SObjectRef.FromSmallInteger(42);
        var b = SObjectRef.FromSmallInteger(43);

        // Act & Assert
        Assert.True(SObjectRef.AreIdentical(a1, a2), "Same SmallInteger values should be identical");
        Assert.False(SObjectRef.AreIdentical(a1, b), "Different SmallInteger values should not be identical");
    }

    /// <summary>
    /// Tests AreIdentical for nil.
    /// </summary>
    [Fact]
    public void AreIdentical_Nil()
    {
        // Arrange
        var nil1 = SObjectRef.Nil;
        var nil2 = SObjectRef.Nil;
        var smallInt = SObjectRef.FromSmallInteger(0);

        // Act & Assert
        Assert.True(SObjectRef.AreIdentical(nil1, nil2), "Nil should be identical to nil");
        Assert.False(SObjectRef.AreIdentical(nil1, smallInt), "Nil should not be identical to SmallInteger(0)");
    }

    /// <summary>
    /// Tests AreIdentical for heap objects.
    /// </summary>
    [Fact]
    public void AreIdentical_HeapObjects()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(100);
        nint oop1 = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);
        nint oop2 = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 1, 0);

        var obj1a = SObjectRef.FromObjectPointer(oop1);
        var obj1b = SObjectRef.FromObjectPointer(oop1); // Same OOP
        var obj2 = SObjectRef.FromObjectPointer(oop2);

        // Act & Assert
        Assert.True(SObjectRef.AreIdentical(obj1a, obj1b), "Same OOP should be identical");
        Assert.False(SObjectRef.AreIdentical(obj1a, obj2), "Different OOPs should not be identical");
    }

    /// <summary>
    /// Tests Equals and GetHashCode implementation.
    /// </summary>
    [Fact]
    public void Equals_AndHashCode()
    {
        // Arrange
        var a1 = SObjectRef.FromSmallInteger(42);
        var a2 = SObjectRef.FromSmallInteger(42);
        var b = SObjectRef.FromSmallInteger(43);

        // Act & Assert - Equals
        Assert.True(a1.Equals(a2), "Same values should be equal");
        Assert.False(a1.Equals(b), "Different values should not be equal");
        Assert.True(a1 == a2, "== operator should work");
        Assert.True(a1 != b, "!= operator should work");

        // Act & Assert - GetHashCode
        Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
        // Different values might have same hash code, so we don't test inequality
    }

    /// <summary>
    /// Tests ToString implementation.
    /// </summary>
    [Fact]
    public void ToString_FormatsCorrectly()
    {
        // Arrange
        var smallInt = SObjectRef.FromSmallInteger(42);
        var nil = SObjectRef.Nil;

        // Act
        string smallIntStr = smallInt.ToString();
        string nilStr = nil.ToString();

        // Assert
        Assert.Contains("42", smallIntStr); // Should contain the integer value
        Assert.Contains("nil", nilStr, StringComparison.OrdinalIgnoreCase); // Should indicate nil
    }

    /// <summary>
    /// Tests that FromObjectPointer creates correct references.
    /// </summary>
    [Fact]
    public void FromObjectPointer_CreatesCorrectReference()
    {
        // Arrange
        var classRef = SObjectRef.FromSmallInteger(200);
        nint oop = ObjectMemoryNew.AllocateObject(classRef, ObjectFormat.FixedPointer, 0, 0);

        // Act
        var objRef = SObjectRef.FromObjectPointer(oop);

        // Assert
        Assert.False(objRef.IsSmallInteger, "Should not be a SmallInteger");
        Assert.False(objRef.IsNil, "Should not be nil");
        Assert.True(objRef.IsObject, "Should be an object pointer");
        Assert.True(objRef.IsHeapObject, "Should be a heap object");
        Assert.Equal(oop, objRef.AsObjectPointer());
    }

    /// <summary>
    /// Tests arithmetic operations return null for non-SmallIntegers.
    /// </summary>
    [Fact]
    public void ArithmeticOperations_ReturnNullForNonSmallIntegers()
    {
        // Arrange
        var smallInt = SObjectRef.FromSmallInteger(10);
        var nil = SObjectRef.Nil;

        // Act & Assert
        Assert.Null(smallInt.FastIntegerAdd(nil));
        Assert.Null(nil.FastIntegerAdd(smallInt));
        Assert.Null(smallInt.FastIntegerSubtract(nil));
        Assert.Null(nil.FastIntegerSubtract(smallInt));
        Assert.Null(smallInt.FastIntegerMultiply(nil));
        Assert.Null(nil.FastIntegerMultiply(smallInt));
    }
}