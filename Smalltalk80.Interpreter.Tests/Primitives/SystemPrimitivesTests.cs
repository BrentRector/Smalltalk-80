using Moq;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.VM.Scheduling;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for System primitives (112-139).
/// </summary>
[Trait("Category", "Primitives")]
public class SystemPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 112: CoreLeft

    [Fact]
    public void CoreLeft_ReturnsEstimatedFreeMemory()
    {
        var primitive = new SystemOperations.CoreLeft();
        var mockHeap = CreateMockHeapManager();
        var mockGC = new Mock<IGarbageCollector>();

        mockGC.Setup(gc => gc.GetStatistics()).Returns(new GCStatistics
        {
            TotalObjects = 1000,
            CollectedObjects = 0,
            FreeObjectTableEntries = 0
        });
        mockHeap.Setup(h => h.GC).Returns(mockGC.Object);
        mockHeap.Setup(h => h.GetObjectTableSize()).Returns(10000);

        var context = CreateContext(ObjectOOP(100), [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.True(result.Value.AsSmallInteger() > 0);
    }

    [Fact]
    public void CoreLeft_WithArguments_Fails()
    {
        var primitive = new SystemOperations.CoreLeft();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 115: OopsLeft

    // OopsLeft uses MaxTheoreticalOops (int.MaxValue >> 1) minus active objects.
    // On modern systems with growable object tables, this reports a large value
    // to prevent Smalltalk's low memory monitoring from triggering spuriously.
    private const int MaxTheoreticalOops = int.MaxValue >> 1; // 1,073,741,823
    private const int SmallIntegerMax = 0x3FFFFFFF; // 1,073,741,823

    [Fact]
    public void OopsLeft_ReturnsTheoreticalMaxMinusActiveObjects()
    {
        var primitive = new SystemOperations.OopsLeft();
        var mockHeap = CreateMockHeapManager();

        mockHeap.Setup(h => h.GetActiveObjectCount()).Returns(3000);

        var context = CreateContext(ObjectOOP(100), [], mockHeap.Object);

        var result = primitive.Execute(context);

        // Expected: MaxTheoreticalOops - 3000 = 1,073,738,823
        // This value is < SmallIntegerMax so no capping needed
        AssertSuccessWithSmallInt(result, MaxTheoreticalOops - 3000);
    }

    [Fact]
    public void OopsLeft_WithManyActiveObjects_StillReturnsLargeValue()
    {
        var primitive = new SystemOperations.OopsLeft();
        var mockHeap = CreateMockHeapManager();

        // Even with 10000 active objects, still have ~1 billion free OOPs
        mockHeap.Setup(h => h.GetActiveObjectCount()).Returns(10000);

        var context = CreateContext(ObjectOOP(100), [], mockHeap.Object);

        var result = primitive.Execute(context);

        // Result should be MaxTheoreticalOops - 10000 = 1,073,731,823
        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.True(result.Value.AsSmallInteger() > 1_000_000_000); // Still over 1 billion
    }

    [Fact]
    public void OopsLeft_WithArguments_Fails()
    {
        var primitive = new SystemOperations.OopsLeft();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 117: InstSize

    [Fact]
    public void InstSize_ReturnsFixedFieldCount()
    {
        var primitive = new SystemOperations.InstSize();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);

        // Format word with 5 fixed fields
        SetupInstanceVariable(mockHeap, classOOP, 2, SmallInt(5));

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 5);
    }

    [Fact]
    public void InstSize_ZeroFixedFields_ReturnsZero()
    {
        var primitive = new SystemOperations.InstSize();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);

        SetupInstanceVariable(mockHeap, classOOP, 2, SmallInt(0));

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    [Fact]
    public void InstSize_SmallIntegerReceiver_Fails()
    {
        var primitive = new SystemOperations.InstSize();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void InstSize_WithArguments_Fails()
    {
        var primitive = new SystemOperations.InstSize();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 128: SomeInstance (System version)

    [Fact]
    public void SystemSomeInstance_FindsInstance()
    {
        var primitive = new SystemOperations.SomeInstance();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var instanceOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.FindInstanceOf(classOOP)).Returns(instanceOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, instanceOOP);
    }

    [Fact]
    public void SystemSomeInstance_NoInstance_Fails()
    {
        var primitive = new SystemOperations.SomeInstance();
        var mockHeap = CreateMockHeapManager();
        var metadata = CreateDefaultTestMetadata(mockHeap.Object);
        var classOOP = ObjectOOP(100);
        var nilOOP = metadata.KnownObjects.Nil;  // Use actual nil from metadata

        mockHeap.Setup(h => h.FindInstanceOf(classOOP)).Returns(nilOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object, metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 129: NextInstance (System version)

    [Fact]
    public void SystemNextInstance_FindsNextInstance()
    {
        var primitive = new SystemOperations.NextInstance();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);
        var nextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.FindNextInstanceOf(classOOP, objectOOP)).Returns(nextOOP);

        var context = CreateContext(objectOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, nextOOP);
    }

    [Fact]
    public void SystemNextInstance_NoMoreInstances_Fails()
    {
        var primitive = new SystemOperations.NextInstance();
        var mockHeap = CreateMockHeapManager();
        var metadata = CreateDefaultTestMetadata(mockHeap.Object);
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);
        var nilOOP = metadata.KnownObjects.Nil;  // Use actual nil from metadata

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.FindNextInstanceOf(classOOP, objectOOP)).Returns(nilOOP);

        var context = CreateContext(objectOOP, [], mockHeap.Object, metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 130: BecomeForward

    [Fact]
    public void BecomeForward_SwapsObjects()
    {
        var primitive = new SystemOperations.BecomeForward();
        var mockHeap = CreateMockHeapManager();
        var obj1 = ObjectOOP(100);
        var obj2 = ObjectOOP(200);

        var context = CreateContext(obj1, [obj2], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, obj2);
        mockHeap.Verify(h => h.Become.BecomeTwoWay(obj1, obj2), Times.Once);
    }

    [Fact]
    public void BecomeForward_SmallIntegerReceiver_Fails()
    {
        var primitive = new SystemOperations.BecomeForward();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [ObjectOOP(200)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void BecomeForward_SmallIntegerArgument_Fails()
    {
        var primitive = new SystemOperations.BecomeForward();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 131: InstanceCount

    [Fact]
    public void InstanceCount_CountsInstances()
    {
        var primitive = new SystemOperations.InstanceCount();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetObjectTableSize()).Returns(5);
        mockHeap.Setup(h => h.IsValidObjectOOP(It.IsAny<SmalltalkOOP>())).Returns(true);

        // 3 out of 5 objects are instances of classOOP
        mockHeap.Setup(h => h.GetClassOOP(SmalltalkOOP.FromObjectTableIndex(0))).Returns(classOOP);
        mockHeap.Setup(h => h.GetClassOOP(SmalltalkOOP.FromObjectTableIndex(1))).Returns(ObjectOOP(200));
        mockHeap.Setup(h => h.GetClassOOP(SmalltalkOOP.FromObjectTableIndex(2))).Returns(classOOP);
        mockHeap.Setup(h => h.GetClassOOP(SmalltalkOOP.FromObjectTableIndex(3))).Returns(ObjectOOP(300));
        mockHeap.Setup(h => h.GetClassOOP(SmalltalkOOP.FromObjectTableIndex(4))).Returns(classOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 3);
    }

    [Fact]
    public void InstanceCount_NoInstances_ReturnsZero()
    {
        var primitive = new SystemOperations.InstanceCount();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetObjectTableSize()).Returns(3);
        mockHeap.Setup(h => h.IsValidObjectOOP(It.IsAny<SmalltalkOOP>())).Returns(true);
        mockHeap.Setup(h => h.GetClassOOP(It.IsAny<SmalltalkOOP>())).Returns(ObjectOOP(200));

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    [Fact]
    public void InstanceCount_SmallIntegerReceiver_Fails()
    {
        var primitive = new SystemOperations.InstanceCount();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 133: ForceGC

    [Fact]
    public void ForceGC_ReturnsReceiver()
    {
        var primitive = new SystemOperations.ForceGC();
        var mockHeap = CreateMockHeapManager();
        var receiverOOP = ObjectOOP(100);

        var context = CreateContext(receiverOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, receiverOOP);
    }

    [Fact]
    public void ForceGC_WithArguments_Fails()
    {
        var primitive = new SystemOperations.ForceGC();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 139: NextObject

    [Fact]
    public void NextObject_FindsNextValidObject()
    {
        var primitive = new SystemOperations.NextObject();
        var mockHeap = CreateMockHeapManager();
        var receiverOOP = ObjectOOP(100); // Table index = 100
        var nextOOP = SmalltalkOOP.FromObjectTableIndex(101);

        mockHeap.Setup(h => h.GetObjectTableSize()).Returns(200);
        mockHeap.Setup(h => h.IsValidObjectOOP(SmalltalkOOP.FromObjectTableIndex(101))).Returns(true);

        var context = CreateContext(receiverOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, nextOOP);
    }

    [Fact]
    public void NextObject_NoMoreObjects_ReturnsNil()
    {
        var primitive = new SystemOperations.NextObject();
        var heap = new SmalltalkHeapManager();
        var metadata = CreateTestMetadata(heap);
        var nilOOP = metadata.KnownObjects.Nil;

        var mockHeap = CreateMockHeapManager();
        var receiverOOP = SmalltalkOOP.FromObjectTableIndex(98);

        mockHeap.Setup(h => h.GetObjectTableSize()).Returns(100);
        // Mark remaining slots as invalid
        mockHeap.Setup(h => h.IsValidObjectOOP(It.IsAny<SmalltalkOOP>())).Returns(false);

        var context = CreateContext(receiverOOP, [], mockHeap.Object, metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, nilOOP);
    }

    [Fact]
    public void NextObject_SmallIntegerReceiver_Fails()
    {
        var primitive = new SystemOperations.NextObject();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void NextObject_WithArguments_Fails()
    {
        var primitive = new SystemOperations.NextObject();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 116: SignalAtOopsLeftWordsLeft

    // Note: Primitive 116 signature is signal: aSemaphore atOopsLeft: numOops wordsLeft: numWords
    // Arguments: [0]=semaphore, [1]=oopsThreshold, [2]=wordsThreshold

    [Fact]
    public void SignalAtOopsLeftWordsLeft_ValidArguments_Succeeds()
    {
        var primitive = new SystemOperations.SignalAtOopsLeftWordsLeft();
        var mockHeap = CreateMockHeapManager();
        var receiverOOP = ObjectOOP(1); // SystemDictionary or similar
        var semaphoreOOP = ObjectOOP(100);

        // Mock the low memory manager
        var mockLowMemoryManager = new Mock<ILowMemoryManager>();

        var context = CreateContext(
            receiverOOP,
            [semaphoreOOP, SmallInt(1000), SmallInt(2000)], // semaphore, oopsThreshold, wordsThreshold
            mockHeap.Object,
            lowMemoryManager: mockLowMemoryManager.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, receiverOOP);
        mockLowMemoryManager.Verify(m => m.SetThresholds(semaphoreOOP, 1000, 2000), Times.Once);
    }

    [Fact]
    public void SignalAtOopsLeftWordsLeft_NilSemaphore_DisablesMonitoring()
    {
        var primitive = new SystemOperations.SignalAtOopsLeftWordsLeft();
        var mockHeap = CreateMockHeapManager();
        var metadata = CreateDefaultTestMetadata(mockHeap.Object);
        var nilOOP = metadata.KnownObjects.Nil;
        var receiverOOP = ObjectOOP(1);

        // Mock the low memory manager
        var mockLowMemoryManager = new Mock<ILowMemoryManager>();

        var context = CreateContext(
            receiverOOP,
            [nilOOP, SmallInt(1000), SmallInt(2000)], // nil semaphore disables monitoring
            mockHeap.Object,
            metadata: metadata,
            lowMemoryManager: mockLowMemoryManager.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, receiverOOP);
        mockLowMemoryManager.Verify(m => m.Disable(), Times.Once);
    }

    [Fact]
    public void SignalAtOopsLeftWordsLeft_WrongArgumentCount_Fails()
    {
        var primitive = new SystemOperations.SignalAtOopsLeftWordsLeft();
        var mockHeap = CreateMockHeapManager();
        var mockLowMemoryManager = new Mock<ILowMemoryManager>();

        var context = CreateContext(
            ObjectOOP(100),
            [SmallInt(1000)], // Only 1 argument, needs 3
            mockHeap.Object,
            lowMemoryManager: mockLowMemoryManager.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void SignalAtOopsLeftWordsLeft_NoLowMemoryManager_Fails()
    {
        var primitive = new SystemOperations.SignalAtOopsLeftWordsLeft();
        var mockHeap = CreateMockHeapManager();
        var semaphoreOOP = ObjectOOP(100);

        // No low memory manager provided
        var context = CreateContext(
            ObjectOOP(1),
            [semaphoreOOP, SmallInt(1000), SmallInt(2000)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
