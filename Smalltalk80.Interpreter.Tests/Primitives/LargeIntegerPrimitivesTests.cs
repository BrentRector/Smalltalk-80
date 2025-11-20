using Moq;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using System.Numerics;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for LargeInteger primitives (21-37).
/// </summary>
[Trait("Category", "Primitives")]
public class LargeIntegerPrimitivesTests : PrimitiveTestBase
{
    /// <summary>
    /// Sets up a mock heap manager for a LargePositiveInteger object.
    /// </summary>
    private static SmalltalkOOP SetupLargePositiveInteger(Mock<ISmalltalkHeapManager> mock, int rawValue, BigInteger value)
    {
        var largeIntOOP = ObjectOOP(rawValue);
        var largeIntClassOOP = ObjectOOP(1000);

        byte[] bytes = value.ToByteArray();
        SetupClassOOP(mock, largeIntOOP, largeIntClassOOP);
        SetupObjectFormat(mock, largeIntOOP, ObjectFormat.ByteIndexed);
        SetupIndexedSize(mock, largeIntOOP, bytes.Length);

        for (int i = 0; i < bytes.Length; i++)
        {
            SetupIndexedByte(mock, largeIntOOP, i, bytes[i]);
        }

        return largeIntOOP;
    }

    private Mock<RuntimeHeapAccessor> CreateMockRuntimeHeapAccess(Mock<ISmalltalkHeapManager> mockHeap)
    {
        var mock = new Mock<RuntimeHeapAccessor>(mockHeap.Object);
        mock.Setup(r => r.GetClass("LargePositiveInteger")).Returns(ObjectOOP(1000));
        mock.Setup(r => r.GetClass("LargeNegativeInteger")).Returns(ObjectOOP(1001));
        return mock;
    }

    #region Primitive 21: LargeInteger Add

    [Fact]
    public void LargeIntegerAdd_TwoLargeIntegers_ReturnsSum()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(2000000000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.Add();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void LargeIntegerAdd_NonLargeIntegerReceiver_Fails()
    {
        var primitive = new LargeIntegerArithmetic.Add();
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(2000000000));

        var context = CreateContext(SmallInt(42), [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 22: LargeInteger Subtract

    [Fact]
    public void LargeIntegerSubtract_TwoLargeIntegers_ReturnsDifference()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(3000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(1000000000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.Subtract();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    #endregion

    #region Primitive 23-28: LargeInteger Comparisons

    [Fact]
    public void LargeIntegerLessThan_SmallerValue_ReturnsTrue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(2000000000));

        var primitive = new LargeIntegerArithmetic.LessThan();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LargeIntegerGreaterThan_LargerValue_ReturnsTrue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(2000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(1000000000));

        var primitive = new LargeIntegerArithmetic.GreaterThan();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LargeIntegerEqual_EqualValues_ReturnsTrue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(1000000000));

        var primitive = new LargeIntegerArithmetic.Equal();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LargeIntegerNotEqual_DifferentValues_ReturnsTrue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(2000000000));

        var primitive = new LargeIntegerArithmetic.NotEqual();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    #endregion

    #region Primitive 29: LargeInteger Multiply

    [Fact]
    public void LargeIntegerMultiply_TwoLargeIntegers_ReturnsProduct()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(100000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(200000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.Multiply();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    #endregion

    #region Primitive 30: LargeInteger Divide

    [Fact]
    public void LargeIntegerDivide_ExactDivision_ReturnsQuotient()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(100000000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.Divide();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void LargeIntegerDivide_NotExact_Fails()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000001));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(100000000));

        var primitive = new LargeIntegerArithmetic.Divide();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 34-36: LargeInteger Bitwise Operations

    [Fact]
    public void LargeIntegerBitAnd_ReturnsAndResult()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(0xFF00FF00));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(0xFFFF0000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.BitAnd();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void LargeIntegerBitOr_ReturnsOrResult()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(0xFF00FF00));
        var int2 = SetupLargePositiveInteger(mockHeap, 150, new BigInteger(0x00FF00FF));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.BitOr();
        var context = CreateContext(int1, [int2], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    #endregion

    #region Primitive 37: LargeInteger BitShift

    [Fact]
    public void LargeIntegerBitShift_LeftShift_ReturnsShiftedValue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.BitShift();
        var context = CreateContext(int1, [SmallInt(4)], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void LargeIntegerBitShift_RightShift_ReturnsShiftedValue()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));
        var resultOOP = ObjectOOP(200);

        mockHeap.Setup(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>())).Returns(resultOOP);

        var primitive = new LargeIntegerArithmetic.BitShift();
        var context = CreateContext(int1, [SmallInt(-4)], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void LargeIntegerBitShift_NonSmallIntegerShift_Fails()
    {
        var mockHeap = CreateMockHeapManager();
        var mockRuntime = CreateMockRuntimeHeapAccess(mockHeap);

        var int1 = SetupLargePositiveInteger(mockHeap, 100, new BigInteger(1000000000));

        var primitive = new LargeIntegerArithmetic.BitShift();
        var context = CreateContext(int1, [ObjectOOP(200)], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
