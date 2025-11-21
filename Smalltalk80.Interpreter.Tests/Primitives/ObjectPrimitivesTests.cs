using Moq;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for Object primitives (60-79).
/// </summary>
[Trait("Category", "Primitives")]
public class ObjectPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 60: At

    [Fact]
    public void At_ValidIndex_ReturnsElement()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);
        var elementOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, arrayOOP, 5);
        SetupObjectFormat(mockHeap, arrayOOP, ObjectFormat.PointerIndexed);
        SetupIndexedOOP(mockHeap, arrayOOP, 2, elementOOP); // 0-based index

        var context = CreateContext(arrayOOP, [SmallInt(3)], mockHeap.Object); // 1-based index

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, elementOOP);
    }

    [Fact]
    public void At_FirstElement_ReturnsElement()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);
        var elementOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, arrayOOP, 5);
        SetupObjectFormat(mockHeap, arrayOOP, ObjectFormat.PointerIndexed);
        SetupIndexedOOP(mockHeap, arrayOOP, 0, elementOOP);

        var context = CreateContext(arrayOOP, [SmallInt(1)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, elementOOP);
    }

    [Fact]
    public void At_LastElement_ReturnsElement()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);
        var elementOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, arrayOOP, 5);
        SetupObjectFormat(mockHeap, arrayOOP, ObjectFormat.PointerIndexed);
        SetupIndexedOOP(mockHeap, arrayOOP, 4, elementOOP);

        var context = CreateContext(arrayOOP, [SmallInt(5)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, elementOOP);
    }

    [Fact]
    public void At_IndexZero_Fails()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 5);

        var context = CreateContext(arrayOOP, [SmallInt(0)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void At_IndexTooLarge_Fails()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 5);

        var context = CreateContext(arrayOOP, [SmallInt(6)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void At_SmallIntegerReceiver_Fails()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void At_NonSmallIntegerIndex_Fails()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 5);

        var context = CreateContext(arrayOOP, [ObjectOOP(200)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void At_ByteIndexedObject_ReturnsByteAsSmallInteger()
    {
        var primitive = new SimpleObjectPrimitives.At();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, stringOOP, 5);
        SetupObjectFormat(mockHeap, stringOOP, ObjectFormat.ByteIndexed);
        SetupIndexedByte(mockHeap, stringOOP, 0, 65); // 'A'

        var context = CreateContext(stringOOP, [SmallInt(1)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 65);
    }

    #endregion

    #region Primitive 61: AtPut

    [Fact]
    public void AtPut_ValidIndex_StoresAndReturnsValue()
    {
        var primitive = new SimpleObjectPrimitives.AtPut();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);
        var valueOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, arrayOOP, 5);
        SetupObjectFormat(mockHeap, arrayOOP, ObjectFormat.PointerIndexed);

        var context = CreateContext(arrayOOP, [SmallInt(3), valueOOP], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, valueOOP);
        mockHeap.Verify(h => h.SetIndexedOOP(arrayOOP, 2, valueOOP), Times.Once);
    }

    [Fact]
    public void AtPut_IndexZero_Fails()
    {
        var primitive = new SimpleObjectPrimitives.AtPut();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 5);

        var context = CreateContext(arrayOOP, [SmallInt(0), ObjectOOP(200)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AtPut_IndexTooLarge_Fails()
    {
        var primitive = new SimpleObjectPrimitives.AtPut();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 5);

        var context = CreateContext(arrayOOP, [SmallInt(6), ObjectOOP(200)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AtPut_ByteIndexed_StorsByteValue()
    {
        var primitive = new SimpleObjectPrimitives.AtPut();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, stringOOP, 5);
        SetupObjectFormat(mockHeap, stringOOP, ObjectFormat.ByteIndexed);

        var context = CreateContext(stringOOP, [SmallInt(1), SmallInt(65)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 65);
        mockHeap.Verify(h => h.SetIndexedByte(stringOOP, 0, 65), Times.Once);
    }

    [Fact]
    public void AtPut_ByteIndexed_ValueOutOfRange_Fails()
    {
        var primitive = new SimpleObjectPrimitives.AtPut();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, stringOOP, 5);
        SetupObjectFormat(mockHeap, stringOOP, ObjectFormat.ByteIndexed);

        var context = CreateContext(stringOOP, [SmallInt(1), SmallInt(256)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 62: Size

    [Fact]
    public void Size_ReturnsIndexedSize()
    {
        var primitive = new SimpleObjectPrimitives.Size();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 10);

        var context = CreateContext(arrayOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 10);
    }

    [Fact]
    public void Size_EmptyArray_ReturnsZero()
    {
        var primitive = new SimpleObjectPrimitives.Size();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, arrayOOP, 0);

        var context = CreateContext(arrayOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    [Fact]
    public void Size_SmallIntegerReceiver_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Size();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Size_WithArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Size();
        var mockHeap = CreateMockHeapManager();
        var arrayOOP = ObjectOOP(100);

        var context = CreateContext(arrayOOP, [SmallInt(1)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 70: New

    [Fact]
    public void New_AllocatesInstance()
    {
        var primitive = new SimpleObjectPrimitives.New();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var instanceOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateInstanceOf(classOOP, 0)).Returns(instanceOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, instanceOOP);
    }

    [Fact]
    public void New_SmallIntegerReceiver_Fails()
    {
        var primitive = new SimpleObjectPrimitives.New();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void New_WithArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.New();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 71: NewWithSize

    [Fact]
    public void NewWithSize_AllocatesInstanceWithIndexedSize()
    {
        var primitive = new SimpleObjectPrimitives.NewWithSize();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var instanceOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateInstanceOf(classOOP, 10)).Returns(instanceOOP);

        var context = CreateContext(classOOP, [SmallInt(10)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, instanceOOP);
    }

    [Fact]
    public void NewWithSize_ZeroSize_Succeeds()
    {
        var primitive = new SimpleObjectPrimitives.NewWithSize();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var instanceOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateInstanceOf(classOOP, 0)).Returns(instanceOOP);

        var context = CreateContext(classOOP, [SmallInt(0)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, instanceOOP);
    }

    [Fact]
    public void NewWithSize_NegativeSize_Fails()
    {
        var primitive = new SimpleObjectPrimitives.NewWithSize();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(-1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void NewWithSize_NonSmallIntegerSize_Fails()
    {
        var primitive = new SimpleObjectPrimitives.NewWithSize();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [ObjectOOP(200)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 72: Become

    [Fact]
    public void Become_SwapsIdentities()
    {
        var primitive = new SimpleObjectPrimitives.Become();
        var mockHeap = CreateMockHeapManager();
        var obj1 = ObjectOOP(100);
        var obj2 = ObjectOOP(200);

        var context = CreateContext(obj1, [obj2], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, obj1);
        mockHeap.Verify(h => h.BecomeTwoWay(obj1, obj2), Times.Once);
    }

    [Fact]
    public void Become_SmallIntegerReceiver_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Become();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [ObjectOOP(200)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Become_SmallIntegerArgument_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Become();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 73: InstVarAt

    [Fact]
    public void InstVarAt_ValidIndex_ReturnsValue()
    {
        var primitive = new SimpleObjectPrimitives.InstVarAt();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);
        var valueOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        // Format with 3 fixed fields
        mockHeap.Setup(h => h.GetInstanceVariable(classOOP, 2)).Returns(SmallInt(3));
        mockHeap.Setup(h => h.GetInstanceVariable(objectOOP, 1)).Returns(valueOOP);

        var context = CreateContext(objectOOP, [SmallInt(2)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, valueOOP);
    }

    [Fact]
    public void InstVarAt_IndexZero_Fails()
    {
        var primitive = new SimpleObjectPrimitives.InstVarAt();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.GetInstanceVariable(classOOP, 2)).Returns(SmallInt(3));

        var context = CreateContext(objectOOP, [SmallInt(0)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void InstVarAt_IndexTooLarge_Fails()
    {
        var primitive = new SimpleObjectPrimitives.InstVarAt();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.GetInstanceVariable(classOOP, 2)).Returns(SmallInt(3));

        var context = CreateContext(objectOOP, [SmallInt(4)], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 74: InstVarAtPut

    [Fact]
    public void InstVarAtPut_ValidIndex_SetsValue()
    {
        var primitive = new SimpleObjectPrimitives.InstVarAtPut();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);
        var valueOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.GetInstanceVariable(classOOP, 2)).Returns(SmallInt(3));

        var context = CreateContext(objectOOP, [SmallInt(2), valueOOP], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, valueOOP);
        mockHeap.Verify(h => h.SetInstanceVariable(objectOOP, 1, valueOOP), Times.Once);
    }

    #endregion

    #region Primitive 75: Hash

    [Fact]
    public void Hash_Object_ReturnsHalfOOP()
    {
        var primitive = new SimpleObjectPrimitives.Hash();
        var heapManager = CreateMockHeapManager().Object;
        var objectOOP = ObjectOOP(100); // Raw value = 200

        var context = CreateContext(objectOOP, [], heapManager);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.Equal(100, result.Value.AsSmallInteger()); // 200 >> 1 = 100
    }

    [Fact]
    public void Hash_SmallInteger_ReturnsMaskedValue()
    {
        var primitive = new SimpleObjectPrimitives.Hash();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger);
    }

    [Fact]
    public void Hash_WithArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Hash();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 77: SomeInstance

    [Fact]
    public void SomeInstance_FindsInstance()
    {
        var primitive = new SimpleObjectPrimitives.SomeInstance();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var instanceOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.FindInstanceOf(classOOP)).Returns(instanceOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, instanceOOP);
    }

    [Fact]
    public void SomeInstance_NoInstanceExists_Fails()
    {
        var primitive = new SimpleObjectPrimitives.SomeInstance();
        var mockHeap = CreateMockHeapManager();
        var classOOP = ObjectOOP(100);
        var nilOOP = ObjectOOP(0);  // Placeholder for nil in mocked tests

        mockHeap.Setup(h => h.FindInstanceOf(classOOP)).Returns(nilOOP);

        var context = CreateContext(classOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 78: NextInstance

    [Fact]
    public void NextInstance_FindsNextInstance()
    {
        var primitive = new SimpleObjectPrimitives.NextInstance();
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
    public void NextInstance_NoMoreInstances_Fails()
    {
        var primitive = new SimpleObjectPrimitives.NextInstance();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);
        var nilOOP = ObjectOOP(0);  // Placeholder for nil in mocked tests

        SetupClassOOP(mockHeap, objectOOP, classOOP);
        mockHeap.Setup(h => h.FindNextInstanceOf(classOOP, objectOOP)).Returns(nilOOP);

        var context = CreateContext(objectOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 111: Class

    [Fact]
    public void Class_Object_ReturnsClassOOP()
    {
        var primitive = new SimpleObjectPrimitives.Class();
        var mockHeap = CreateMockHeapManager();
        var objectOOP = ObjectOOP(100);
        var classOOP = ObjectOOP(50);

        SetupClassOOP(mockHeap, objectOOP, classOOP);

        var context = CreateContext(objectOOP, [], mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, classOOP);
    }

    [Fact]
    public void Class_SmallInteger_ReturnsSmallIntegerClass()
    {
        var primitive = new SimpleObjectPrimitives.Class();
        var mockHeap = CreateMockHeapManager();
        var smallIntClassOOP = ObjectOOP(42);

        var mockRuntimeAccess = new Mock<RuntimeHeapAccessor>(mockHeap.Object);
        mockRuntimeAccess.Setup(r => r.GetClass("SmallInteger")).Returns(smallIntClassOOP);

        var context = CreateContext(SmallInt(10), [], mockHeap.Object, mockRuntimeAccess.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, smallIntClassOOP);
    }

    [Fact]
    public void Class_WithArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.Class();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
