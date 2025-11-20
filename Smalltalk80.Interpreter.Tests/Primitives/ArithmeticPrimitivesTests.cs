using Moq;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for SmallInteger arithmetic primitives (1-17).
/// </summary>
[Trait("Category", "Primitives")]
public class ArithmeticPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 1: Add

    [Fact]
    public void Add_TwoPositiveSmallIntegers_ReturnsSum()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 8);
    }

    [Fact]
    public void Add_PositiveAndNegative_ReturnsCorrectSum()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(10), [SmallInt(-3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 7);
    }

    [Fact]
    public void Add_TwoNegatives_ReturnsNegativeSum()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-5), [SmallInt(-3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -8);
    }

    [Fact]
    public void Add_WithZero_ReturnsOriginalValue()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    [Fact]
    public void Add_Overflow_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        int maxSmallInt = (1 << 30) - 1;
        var context = CreateContext(SmallInt(maxSmallInt), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Add_NonSmallIntegerReceiver_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Add_NonSmallIntegerArgument_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [ObjectOOP(100)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Add_NoArguments_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Add_TooManyArguments_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Add();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3), SmallInt(2)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 2: Subtract

    [Fact]
    public void Subtract_TwoPositiveSmallIntegers_ReturnsDifference()
    {
        var primitive = new SmallIntegerArithmetic.Subtract();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(10), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 7);
    }

    [Fact]
    public void Subtract_ResultNegative_ReturnsNegative()
    {
        var primitive = new SmallIntegerArithmetic.Subtract();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(3), [SmallInt(10)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -7);
    }

    [Fact]
    public void Subtract_SubtractNegative_AddsValue()
    {
        var primitive = new SmallIntegerArithmetic.Subtract();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(-3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 8);
    }

    [Fact]
    public void Subtract_Underflow_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Subtract();
        var heapManager = CreateMockHeapManager().Object;
        int minSmallInt = -(1 << 30);
        var context = CreateContext(SmallInt(minSmallInt), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 9: Multiply

    [Fact]
    public void Multiply_TwoPositives_ReturnsProduct()
    {
        var primitive = new SmallIntegerArithmetic.Multiply();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(6), [SmallInt(7)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    [Fact]
    public void Multiply_PositiveAndNegative_ReturnsNegativeProduct()
    {
        var primitive = new SmallIntegerArithmetic.Multiply();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(6), [SmallInt(-7)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -42);
    }

    [Fact]
    public void Multiply_TwoNegatives_ReturnsPositiveProduct()
    {
        var primitive = new SmallIntegerArithmetic.Multiply();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-6), [SmallInt(-7)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    [Fact]
    public void Multiply_ByZero_ReturnsZero()
    {
        var primitive = new SmallIntegerArithmetic.Multiply();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(12345), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    [Fact]
    public void Multiply_Overflow_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Multiply();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(100000), [SmallInt(100000)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 10: Divide (Exact)

    [Fact]
    public void Divide_ExactDivision_ReturnsQuotient()
    {
        var primitive = new SmallIntegerArithmetic.Divide();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(6)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 7);
    }

    [Fact]
    public void Divide_NotExact_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Divide();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(10), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Divide_ByZero_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Divide();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Divide_NegativeExactDivision_ReturnsNegativeQuotient()
    {
        var primitive = new SmallIntegerArithmetic.Divide();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-42), [SmallInt(6)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -7);
    }

    #endregion

    #region Primitive 11: Mod (Floored Modulo)

    [Fact]
    public void Mod_PositiveMod_ReturnsRemainder()
    {
        var primitive = new SmallIntegerArithmetic.Mod();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 2);
    }

    [Fact]
    public void Mod_NegativeDividendPositiveDivisor_SmalltalkSemantics()
    {
        // Smalltalk uses floored division: -17 \\ 5 = 3 (not -2 like C#)
        var primitive = new SmallIntegerArithmetic.Mod();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 3);
    }

    [Fact]
    public void Mod_ByZero_Fails()
    {
        var primitive = new SmallIntegerArithmetic.Mod();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(17), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 12: Div (Floored Division)

    [Fact]
    public void Div_PositiveDiv_ReturnsFlooredQuotient()
    {
        var primitive = new SmallIntegerArithmetic.Div();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 3);
    }

    [Fact]
    public void Div_NegativeDividend_FloorSemantics()
    {
        // -17 // 5 = -4 (floored), not -3 (truncated)
        var primitive = new SmallIntegerArithmetic.Div();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -4);
    }

    #endregion

    #region Primitive 13: Quo (Truncated Division)

    [Fact]
    public void Quo_PositiveDiv_ReturnsTruncatedQuotient()
    {
        var primitive = new SmallIntegerArithmetic.Quo();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 3);
    }

    [Fact]
    public void Quo_NegativeDividend_TruncateTowardZero()
    {
        // -17 quo: 5 = -3 (truncated toward zero)
        var primitive = new SmallIntegerArithmetic.Quo();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-17), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -3);
    }

    #endregion

    #region Primitive 14: BitAnd

    [Fact]
    public void BitAnd_TwoValues_ReturnsAndResult()
    {
        var primitive = new SmallIntegerArithmetic.BitAnd();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(0b1010), [SmallInt(0b1100)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0b1000);
    }

    [Fact]
    public void BitAnd_WithZero_ReturnsZero()
    {
        var primitive = new SmallIntegerArithmetic.BitAnd();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(255), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    [Fact]
    public void BitAnd_WithAllOnes_ReturnsSameValue()
    {
        var primitive = new SmallIntegerArithmetic.BitAnd();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(-1)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    #endregion

    #region Primitive 15: BitOr

    [Fact]
    public void BitOr_TwoValues_ReturnsOrResult()
    {
        var primitive = new SmallIntegerArithmetic.BitOr();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(0b1010), [SmallInt(0b1100)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0b1110);
    }

    [Fact]
    public void BitOr_WithZero_ReturnsSameValue()
    {
        var primitive = new SmallIntegerArithmetic.BitOr();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    #endregion

    #region Primitive 16: BitXor

    [Fact]
    public void BitXor_TwoValues_ReturnsXorResult()
    {
        var primitive = new SmallIntegerArithmetic.BitXor();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(0b1010), [SmallInt(0b1100)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0b0110);
    }

    [Fact]
    public void BitXor_SameValue_ReturnsZero()
    {
        var primitive = new SmallIntegerArithmetic.BitXor();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    #endregion

    #region Primitive 17: BitShift

    [Fact]
    public void BitShift_LeftShift_ReturnsShiftedValue()
    {
        var primitive = new SmallIntegerArithmetic.BitShift();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(1), [SmallInt(4)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 16);
    }

    [Fact]
    public void BitShift_RightShift_ReturnsShiftedValue()
    {
        var primitive = new SmallIntegerArithmetic.BitShift();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(16), [SmallInt(-2)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 4);
    }

    [Fact]
    public void BitShift_ZeroShift_ReturnsSameValue()
    {
        var primitive = new SmallIntegerArithmetic.BitShift();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 42);
    }

    [Fact]
    public void BitShift_OverflowLeftShift_Fails()
    {
        var primitive = new SmallIntegerArithmetic.BitShift();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(1), [SmallInt(31)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void BitShift_NegativeValue_ArithmeticRightShift()
    {
        var primitive = new SmallIntegerArithmetic.BitShift();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-16), [SmallInt(-2)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -4);
    }

    #endregion

    #region Primitive 76: AsObject

    [Fact]
    public void AsObject_ValidSmallInteger_ReturnsObjectPointer()
    {
        var primitive = new MiscArithmetic.AsObject();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(50), [], heapManager);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        Assert.False(result.Value.IsSmallInteger);
        Assert.Equal(100, result.Value.RawValue); // 50 * 2 = 100
    }

    [Fact]
    public void AsObject_NonSmallIntegerReceiver_Fails()
    {
        var primitive = new MiscArithmetic.AsObject();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsObject_WithArguments_Fails()
    {
        var primitive = new MiscArithmetic.AsObject();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(50), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsObject_NegativeValue_Fails()
    {
        var primitive = new MiscArithmetic.AsObject();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-10), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
