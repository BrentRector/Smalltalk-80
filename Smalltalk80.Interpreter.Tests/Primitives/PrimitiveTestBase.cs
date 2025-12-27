using Moq;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Base class providing common test utilities and mock setup for primitive tests.
/// </summary>
public abstract class PrimitiveTestBase
{
    /// <summary>
    /// Creates a mock heap manager with common default behaviors.
    /// </summary>
    protected static Mock<ISmalltalkHeap> CreateMockHeapManager()
    {
        var mock = new Mock<ISmalltalkHeap>(MockBehavior.Loose);

        // Default behavior for GetNil - return a consistent OOP value
        // Use raw OOP value 4 (not ObjectOOP(4) which would be 8)
        // This matches what tests expect for nil
        mock.Setup(h => h.GetNil())
            .Returns(new SmalltalkOOP(4));

        // Default behavior for class name lookup
        mock.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>()))
            .Returns("Object");

        // Default behavior for object format
        mock.Setup(h => h.GetObjectFormat(It.IsAny<SmalltalkOOP>()))
            .Returns(ObjectFormat.PointerIndexed);

        // Setup mock BecomeHandler for become primitives
        var mockBecomeHandler = new Mock<IBecomeHandler>(MockBehavior.Loose);
        mock.Setup(h => h.Become).Returns(mockBecomeHandler.Object);

        return mock;
    }

    /// <summary>
    /// Creates test metadata with allocated objects from a real heap.
    /// Use this when primitives need actual VMMetadata with valid OOPs.
    /// </summary>
    protected static VMMetadata CreateTestMetadata(SmalltalkHeapManager heap)
    {
        // Allocate actual objects in the heap
        var nilOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var trueOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var falseOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var smalltalkOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var processorSchedulerOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Allocate class objects
        var classClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var metaclassClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 6, 0);
        var symbolClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var stringClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var arrayClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var byteArrayClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var wordArrayClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var displayBitmapClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var compiledMethodClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var smallIntegerClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var blockContextClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);
        var orderedCollectionClassOOP = heap.AllocateObject(ObjectFormat.Pointer, 7, 0);

        return new VMMetadata
        {
            KnownObjects = new KnownObjects(
                nil: nilOOP,
                @true: trueOOP,
                @false: falseOOP,
                smalltalk: smalltalkOOP,
                display: nilOOP  // Use nil for test - no Display needed
            ),
            ProcessorScheduler = processorSchedulerOOP,

            // Class field indices (standard Blue Book layout)
            ClassNameFieldIndex = 6,
            ClassSuperclassFieldIndex = 0,
            ClassMethodDictFieldIndex = 1,
            ClassFormatFieldIndex = 2,

            // Metaclass field index
            MetaclassThisClassFieldIndex = 5,

            // MethodDictionary field indices
            MethodDictTallyFieldIndex = 0,
            MethodDictKeysFieldIndex = 1,
            MethodDictValuesFieldIndex = 2,

            // Well-known class OOPs
            ClassClassOOP = classClassOOP,
            MetaclassClassOOP = metaclassClassOOP,
            SymbolClassOOP = symbolClassOOP,
            StringClassOOP = stringClassOOP,
            ArrayClassOOP = arrayClassOOP,
            ByteArrayClassOOP = byteArrayClassOOP,
            WordArrayClassOOP = wordArrayClassOOP,
            DisplayBitmapClassOOP = displayBitmapClassOOP,
            CompiledMethodClassOOP = compiledMethodClassOOP,
            SmallIntegerClassOOP = smallIntegerClassOOP,
            BlockContextClassOOP = blockContextClassOOP,
            OrderedCollectionClassOOP = orderedCollectionClassOOP
        };
    }

    /// <summary>
    /// Creates a PrimitiveExecutionContext with the specified receiver and arguments.
    /// For tests that need metadata, call CreateTestMetadata() first and pass it in.
    /// If no metadata is provided, a default test metadata is created automatically using the heap manager.
    /// </summary>
    protected static PrimitiveExecutionContext CreateContext(
        SmalltalkOOP receiver,
        SmalltalkOOP[] arguments,
        ISmalltalkHeap heapManager,
        RuntimeHeapAccessor? runtimeHeapAccess = null,
        IContextManager? contextManager = null,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null,
        int instructionPointer = 0,
        VMMetadata? metadata = null)
    {
        // Ensure metadata is always provided (create default if not specified)
        // Use the heap manager to get the correct nil OOP
        var actualMetadata = metadata ?? CreateDefaultTestMetadata(heapManager);

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
            messageSender: messageSender,
            metadata: actualMetadata);
    }

    /// <summary>
    /// Creates a SmallInteger OOP from an integer value.
    /// </summary>
    protected static SmalltalkOOP SmallInt(int value) => SmalltalkOOP.FromSmallInteger(value);

    /// <summary>
    /// Creates a non-SmallInteger OOP (object pointer) from a raw value.
    /// Use this for test OOPs when you need valid OOP structures but don't need real heap objects.
    /// </summary>
    protected static SmalltalkOOP ObjectOOP(int rawValue) => new SmalltalkOOP(rawValue << 1);

    // NOTE: Tests that need Nil/True/False should either:
    // 1. Create metadata with CreateTestMetadata() and use metadata.KnownObjects.Nil/True/False
    // 2. Use ObjectOOP(n) to create test OOPs for mocking purposes

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
    /// Callers must provide the true/false OOPs from their test metadata or heap.
    /// </summary>
    protected static void AssertSuccessWithBool(PrimitiveResult result, bool expectedValue, SmalltalkOOP trueOOP, SmalltalkOOP falseOOP)
    {
        AssertSuccess(result);
        if (expectedValue)
            Assert.Equal(trueOOP, result.Value);
        else
            Assert.Equal(falseOOP, result.Value);
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
    protected static void SetupClassOOP(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
    {
        mock.Setup(h => h.GetClassOOP(objectOOP)).Returns(classOOP);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific class name for an object.
    /// </summary>
    protected static void SetupClassName(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, string className)
    {
        mock.Setup(h => h.GetClassName(objectOOP)).Returns(className);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific indexed size for an object.
    /// </summary>
    protected static void SetupIndexedSize(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, int size)
    {
        mock.Setup(h => h.GetIndexedSize(objectOOP)).Returns(size);
    }

    /// <summary>
    /// Sets up a mock heap manager to return a specific object format.
    /// </summary>
    protected static void SetupObjectFormat(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, ObjectFormat format)
    {
        mock.Setup(h => h.GetObjectFormat(objectOOP)).Returns(format);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific instance variable values.
    /// </summary>
    protected static void SetupInstanceVariable(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        mock.Setup(h => h.GetInstanceVariable(objectOOP, index)).Returns(value);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific indexed OOP values.
    /// </summary>
    protected static void SetupIndexedOOP(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
    {
        mock.Setup(h => h.GetIndexedOOP(objectOOP, index)).Returns(value);
    }

    /// <summary>
    /// Sets up a mock heap manager to return specific indexed byte values.
    /// </summary>
    protected static void SetupIndexedByte(Mock<ISmalltalkHeap> mock, SmalltalkOOP objectOOP, int index, byte value)
    {
        mock.Setup(h => h.GetIndexedByte(objectOOP, index)).Returns(value);
    }

    /// <summary>
    /// Creates a basic mock RuntimeHeapAccessor that returns -1 for all field lookups.
    /// Tests can set up specific field indices as needed.
    /// </summary>
    protected static Mock<RuntimeHeapAccessor> CreateMockRuntimeHeapAccessor(Mock<ISmalltalkHeap> mockHeap, VMMetadata? metadata = null)
    {
        // Use provided metadata or create a default one using the heap
        var testMetadata = metadata ?? CreateDefaultTestMetadata(mockHeap.Object);

        var mock = new Mock<RuntimeHeapAccessor>(mockHeap.Object, testMetadata);

        // Default: return -1 (field not found) for any field lookup
        mock.Setup(r => r.GetFieldIndex(It.IsAny<SmalltalkOOP>(), It.IsAny<string>())).Returns(-1);

        return mock;
    }

    /// <summary>
    /// Creates a minimal VMMetadata for tests that don't need specific metadata values.
    /// Gets nil from the heap manager to ensure it matches the heap's actual nil OOP.
    /// </summary>
    protected static VMMetadata CreateDefaultTestMetadata(ISmalltalkHeap heap)
    {
        // Get the actual nil OOP from the heap manager
        var nilOOP = heap.GetNil();

        return new VMMetadata
        {
            KnownObjects = new KnownObjects(
                nil: nilOOP,
                @true: ObjectOOP(1),  // Placeholder - tests that need true/false should provide explicit metadata
                @false: ObjectOOP(2), // Placeholder
                smalltalk: ObjectOOP(3), // Placeholder
                display: nilOOP  // Use nil for test - no Display needed
            ),
            ProcessorScheduler = ObjectOOP(4),
            ClassNameFieldIndex = 6,
            ClassSuperclassFieldIndex = 0,
            ClassMethodDictFieldIndex = 1,
            ClassFormatFieldIndex = 2,
            MetaclassThisClassFieldIndex = 5,
            MethodDictTallyFieldIndex = 0,
            MethodDictKeysFieldIndex = 1,
            MethodDictValuesFieldIndex = 2,
            ClassClassOOP = ObjectOOP(10),
            MetaclassClassOOP = ObjectOOP(11),
            SymbolClassOOP = ObjectOOP(12),
            StringClassOOP = ObjectOOP(13),
            ArrayClassOOP = ObjectOOP(14),
            ByteArrayClassOOP = ObjectOOP(18),
            WordArrayClassOOP = ObjectOOP(19),
            DisplayBitmapClassOOP = ObjectOOP(20),
            CompiledMethodClassOOP = ObjectOOP(15),
            SmallIntegerClassOOP = ObjectOOP(16),
            BlockContextClassOOP = ObjectOOP(17),
            OrderedCollectionClassOOP = ObjectOOP(21)
        };
    }
}
