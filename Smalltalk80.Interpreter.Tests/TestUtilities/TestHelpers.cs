using Moq;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Tests.TestUtilities;

/// <summary>
/// Common test helper methods and utilities for Smalltalk-80 interpreter tests.
/// </summary>
public static class TestHelpers
{
    #region OOP Creation Helpers

    /// <summary>
    /// Creates a SmallInteger OOP from an integer value.
    /// </summary>
    public static SmalltalkOOP SmallInt(int value) => SmalltalkOOP.FromSmallInteger(value);

    /// <summary>
    /// Creates an object OOP (non-SmallInteger) from an index.
    /// </summary>
    public static SmalltalkOOP ObjectOOP(int index) => SmalltalkOOP.FromObjectTableIndex(index);

    /// <summary>
    /// Creates a nil-like OOP for testing (index 0).
    /// </summary>
    public static SmalltalkOOP NilOOP() => ObjectOOP(0);

    /// <summary>
    /// Creates a true-like OOP for testing (index 1).
    /// </summary>
    public static SmalltalkOOP TrueOOP() => ObjectOOP(1);

    /// <summary>
    /// Creates a false-like OOP for testing (index 2).
    /// </summary>
    public static SmalltalkOOP FalseOOP() => ObjectOOP(2);

    #endregion

    #region Mock Factory Helpers

    /// <summary>
    /// Creates a mock ISmalltalkHeapManager with common default setups.
    /// </summary>
    public static Mock<ISmalltalkHeapManager> CreateMockHeapManager()
    {
        var mock = new Mock<ISmalltalkHeapManager>();

        // Default: allocations return sequential OOPs
        var nextOopIndex = 100;
        mock.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => ObjectOOP(nextOopIndex++));

        // Default: GetObjectFormat returns Pointer
        mock.Setup(h => h.GetObjectFormat(It.IsAny<SmalltalkOOP>()))
            .Returns(ObjectFormat.Pointer);

        // Default: GetClassName returns generic name
        mock.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>()))
            .Returns("Object");

        return mock;
    }

    /// <summary>
    /// Creates a real SmalltalkHeapManager with SpecialObjectsArray initialized.
    /// </summary>
    public static (SmalltalkHeapManager heap, SmalltalkOOP nil, SmalltalkOOP trueOop, SmalltalkOOP falseOop)
        CreateInitializedHeap()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate special objects
        var nilOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var falseOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var trueOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Initialize SpecialObjectsArray if not already done
        try
        {
            var soa = new SpecialObjectsArray(nilOOP, falseOOP, trueOOP,
                SmalltalkOOP.Null, SmalltalkOOP.Null);
            SpecialObjectsArray.Initialize(soa);
        }
        catch
        {
            // Already initialized, ignore
        }

        return (heap, nilOOP, trueOOP, falseOOP);
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Asserts that a PrimitiveResult indicates success.
    /// </summary>
    public static void AssertPrimitiveSuccess(PrimitiveResult result)
    {
        Assert.True(result.Success, "Expected primitive to succeed");
    }

    /// <summary>
    /// Asserts that a PrimitiveResult indicates failure.
    /// </summary>
    public static void AssertPrimitiveFailure(PrimitiveResult result)
    {
        Assert.False(result.Success, "Expected primitive to fail");
    }

    /// <summary>
    /// Asserts that a PrimitiveResult succeeds with a specific SmallInteger value.
    /// </summary>
    public static void AssertPrimitiveSuccessWithValue(PrimitiveResult result, int expectedValue)
    {
        Assert.True(result.Success, "Expected primitive to succeed");
        Assert.True(result.Value.IsSmallInteger, "Expected result to be SmallInteger");
        Assert.Equal(expectedValue, result.Value.AsSmallInteger());
    }

    /// <summary>
    /// Asserts that a PrimitiveResult succeeds with a specific OOP.
    /// </summary>
    public static void AssertPrimitiveSuccessWithOOP(PrimitiveResult result, SmalltalkOOP expectedOOP)
    {
        Assert.True(result.Success, "Expected primitive to succeed");
        Assert.Equal(expectedOOP, result.Value);
    }

    /// <summary>
    /// Asserts that an OOP is a SmallInteger with a specific value.
    /// </summary>
    public static void AssertSmallInteger(SmalltalkOOP oop, int expectedValue)
    {
        Assert.True(oop.IsSmallInteger, $"Expected SmallInteger, got object OOP");
        Assert.Equal(expectedValue, oop.AsSmallInteger());
    }

    /// <summary>
    /// Asserts that an OOP is not a SmallInteger (is an object reference).
    /// </summary>
    public static void AssertObjectOOP(SmalltalkOOP oop)
    {
        Assert.False(oop.IsSmallInteger, "Expected object OOP, got SmallInteger");
    }

    #endregion

    #region Test Data Helpers

    /// <summary>
    /// Creates an array of SmallInteger OOPs for testing.
    /// </summary>
    public static SmalltalkOOP[] CreateSmallIntArray(params int[] values)
    {
        return values.Select(SmallInt).ToArray();
    }

    /// <summary>
    /// Creates an array of Object OOPs for testing.
    /// </summary>
    public static SmalltalkOOP[] CreateObjectArray(int startIndex, int count)
    {
        return Enumerable.Range(startIndex, count).Select(ObjectOOP).ToArray();
    }

    #endregion

    #region Bytecode Test Helpers

    /// <summary>
    /// Creates a bytecode array from byte values.
    /// </summary>
    public static byte[] Bytecodes(params byte[] bytes) => bytes;

    /// <summary>
    /// Creates a bytecode for pushing a temporary variable (unified temp frame).
    /// </summary>
    public static byte PushTempBytecode(int index)
    {
        if (index < 16) return (byte)(0x10 + index);
        throw new ArgumentOutOfRangeException(nameof(index), "Use extended bytecode for index >= 16");
    }

    /// <summary>
    /// Creates a bytecode for storing to a temporary variable (unified temp frame).
    /// </summary>
    public static byte StoreTempBytecode(int index)
    {
        if (index < 8) return (byte)(0x68 + index);
        throw new ArgumentOutOfRangeException(nameof(index), "Use extended bytecode for index >= 8");
    }

    /// <summary>
    /// Creates a bytecode for pushing an instance variable.
    /// </summary>
    public static byte PushInstVarBytecode(int index)
    {
        if (index < 16) return (byte)(0x00 + index);
        throw new ArgumentOutOfRangeException(nameof(index), "Use extended bytecode for index >= 16");
    }

    #endregion
}

/// <summary>
/// Extension methods for test assertions.
/// </summary>
public static class TestExtensions
{
    /// <summary>
    /// Verifies that a mock was called exactly once with specific arguments.
    /// </summary>
    public static void VerifyCalledOnce<T>(this Mock<T> mock, System.Linq.Expressions.Expression<Action<T>> expression)
        where T : class
    {
        mock.Verify(expression, Times.Once);
    }

    /// <summary>
    /// Verifies that a mock was never called with specific arguments.
    /// </summary>
    public static void VerifyNeverCalled<T>(this Mock<T> mock, System.Linq.Expressions.Expression<Action<T>> expression)
        where T : class
    {
        mock.Verify(expression, Times.Never);
    }
}
