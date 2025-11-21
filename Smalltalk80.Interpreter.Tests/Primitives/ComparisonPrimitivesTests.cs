using Moq;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for comparison primitives (SmallInteger 3-8, and Object identity 110).
/// </summary>
[Trait("Category", "Primitives")]
public class ComparisonPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 3: LessThan

    [Fact]
    public void LessThan_SmallerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LessThan_LargerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void LessThan_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void LessThan_NegativeValues_ReturnsCorrectResult()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-10), [SmallInt(-5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LessThan_NonSmallIntegerReceiver_Fails()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 4: GreaterThan

    [Fact]
    public void GreaterThan_LargerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void GreaterThan_SmallerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void GreaterThan_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void GreaterThan_NegativeVsPositive_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    #endregion

    #region Primitive 5: LessOrEqual

    [Fact]
    public void LessOrEqual_SmallerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LessOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void LessOrEqual_LargerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    #endregion

    #region Primitive 6: GreaterOrEqual

    [Fact]
    public void GreaterOrEqual_LargerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void GreaterOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void GreaterOrEqual_SmallerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    #endregion

    #region Primitive 7: Equal

    [Fact]
    public void Equal_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void Equal_DifferentValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void Equal_ZeroValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(0), [SmallInt(0)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void Equal_NegativeValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(-100), [SmallInt(-100)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    #endregion

    #region Primitive 8: NotEqual

    [Fact]
    public void NotEqual_DifferentValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void NotEqual_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void NotEqual_PositiveAndNegative_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(5), [SmallInt(-5)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    #endregion

    #region Primitive 110: IdentityEquals

    [Fact]
    public void IdentityEquals_SameObject_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var obj = ObjectOOP(100);
        var context = CreateContext(obj, [obj], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void IdentityEquals_DifferentObjects_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [ObjectOOP(200)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void IdentityEquals_SameSmallIntegers_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void IdentityEquals_DifferentSmallIntegers_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void IdentityEquals_SmallIntegerVsObject_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [ObjectOOP(84)], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void IdentityEquals_NilVsNil_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(Nil, [Nil], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void IdentityEquals_TrueVsTrue_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(True, [True], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true);
    }

    [Fact]
    public void IdentityEquals_TrueVsFalse_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(True, [False], heapManager);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false);
    }

    [Fact]
    public void IdentityEquals_NoArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
