using Moq;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Base class providing common test utilities and mock setup for primitive tests.
/// </summary>
public abstract class PrimitiveTestBase
{
    /// <summary>
    /// Creates a mock heap manager with common default behaviors.
    /// </summary>
    protected static Mock<ISmalltalkHeapManager> CreateMockHeapManager()
    {
        var mock = new Mock<ISmalltalkHeapManager>(MockBehavior.Loose);

        // Default behavior for class name lookup
        mock.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>()))
            .Returns("Object");

        // Default behavior for object format
        mock.Setup(h => h.GetObjectFormat(It.IsAny<SmalltalkOOP>()))
            .Returns(ObjectFormat.PointerIndexed);

        return mock;
    }

    /// <summary>
    /// Creates a PrimitiveExecutionContext with the specified receiver and arguments.
    /// </summary>
    protected static PrimitiveExecutionContext CreateContext(
        SmalltalkOOP receiver,
        SmalltalkOOP[] arguments,
        ISmalltalkHeapManager heapManager,
        IRuntimeHeapAccessor? runtimeHeapAccess = null,
        IContextManager? contextManager = null,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null,
        int instructionPointer = 0)
    {
        return new PrimitiveExecutionContext(
            receiver,
            arguments,
            heapManager,
            stackManager: null,
            contextManager: contextManager,
            layoutResolver: null,
            platform: null,
            runtimeHeapAccess: runtimeHeapAccess,
            instructionPointer: instructionPointer,
            cacheManager: cacheManager,
            messageSender: messageSender);
    }

    /// <summary>
    /// Creates a SmallInteger OOP from an integer value.
    /// </summary>
    protected static SmalltalkOOP SmallInt(int value) => SmalltalkOOP.FromSmallInteger(value);

    /// <summary>
    /// Creates a non-SmallInteger OOP (object pointer) from a raw value.
    /// </summary>
    protected static SmalltalkOOP ObjectOOP(int rawValue) => new SmalltalkOOP(rawValue << 1);

    /// <summary>
    /// Gets the Nil OOP from SpecialObjectsArray.
    /// </summary>
    protected static SmalltalkOOP Nil => SpecialObjectsArray.Instance.Nil;

    /// <summary>
    /// Gets the True OOP from SpecialObjectsArray.
    /// </summary>
    protected static SmalltalkOOP True => SpecialObjectsArray.Instance.True;

    /// <summary>
    /// Gets the False OOP from SpecialObjectsArray.
    /// </summary>
    protected static SmalltalkOOP False => SpecialObjectsArray.Instance.False;

    /// <summary>
    /// Asserts that a PrimitiveResult is successful.
    /// </summary>
    protected static void AssertSuccess(PrimitiveResult result)
    {
        Assert.True(result.Success, "Expected primitive to succeed but it failed");
    }

    /// <summary>
    /// Asserts that a PrimitiveResult failed.
    /// </summary>
    protected static void AssertFailure(PrimitiveResult result)
    {
        Assert.False(result.Success, "Expected primitive to fail but it succeeded");
    }

    /// <summary>
    /// Asserts that a PrimitiveResult is successful and returns the expected SmallInteger value.
    /// </summary>
    protected static void AssertSuccessWithSmallInt(PrimitiveResult result, int expectedValue)
    {
        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger, "Expected SmallInteger result");
        Assert.Equal(expectedValue, result.Value.AsSmallInteger());
    }

    /// <summary>
    /// Asserts that a PrimitiveResult is successful and returns the expected boolean value.
    /// </summary>
    protected static void AssertSuccessWithBool(PrimitiveResult result, bool expectedValue)
    {
        AssertSuccess(result);
        if (expectedValue)
            Assert.Equal(True, result.Value);
        else
            Assert.Equal(False, result.Value);
    }

    /// <summary>
    /// Asserts that a PrimitiveResult is successful and returns the expected OOP.
    /// </summary>
    protected static void AssertSuccessWithOOP(PrimitiveResult result, SmalltalkOOP expectedOOP)
    {
        AssertSuccess(result);
        Assert.Equal(expectedOOP, result.Value);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific class OOP for an object.
    /// </summary>
    protected static void SetupClassOOP(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
    {
        mock.Setup(h => h.GetClassOOP(objectOOP)).Returns(classOOP);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific class name for an object.
    /// </summary>
    protected static void SetupClassName(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, string className)
    {
        mock.Setup(h => h.GetClassName(objectOOP)).Returns(className);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific indexed size for an object.
    /// </summary>
    protected static void SetupIndexedSize(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, int size)
    {
        mock.Setup(h => h.GetIndexedSize(objectOOP)).Returns(size);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific object format.
    /// </summary>
    protected static void SetupObjectFormat(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, ObjectFormat format)
    {
        mock.Setup(h => h.GetObjectFormat(objectOOP)).Returns(format);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific instance variable values.
    /// </summary>
    protected static void SetupInstanceVariable(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        mock.Setup(h => h.GetInstanceVariable(objectOOP, index)).Returns(value);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific indexed OOP values.
    /// </summary>
    protected static void SetupIndexedOOP(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        mock.Setup(h => h.GetIndexedOOP(objectOOP, index)).Returns(value);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific indexed byte values.
    /// </summary>
    protected static void SetupIndexedByte(Mock<ISmalltalkHeapManager> mock, SmalltalkOOP objectOOP, int index, byte value)
    {
        mock.Setup(h => h.GetIndexedByte(objectOOP, index)).Returns(value);
    }
}
