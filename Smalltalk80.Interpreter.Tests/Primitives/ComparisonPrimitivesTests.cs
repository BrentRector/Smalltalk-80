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
    // Shared heap and metadata for all comparison tests
    private static readonly SmalltalkHeapManager _testHeap = new();
    private static readonly VMMetadata _testMetadata = CreateTestMetadata(_testHeap);
    private static readonly SmalltalkOOP _trueOOP = _testMetadata.KnownObjects.True;
    private static readonly SmalltalkOOP _falseOOP = _testMetadata.KnownObjects.False;

    #region Primitive 3: LessThan

    [Fact]
    public void LessThan_SmallerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessThan_LargerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessThan_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessThan_NegativeValues_ReturnsCorrectResult()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(-10), [SmallInt(-5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessThan_NonSmallIntegerReceiver_Fails()
    {
        var primitive = new SmallIntegerArithmetic.LessThan();
        var heapManager = _testHeap;
        var context = CreateContext(ObjectOOP(100), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 4: GreaterThan

    [Fact]
    public void GreaterThan_LargerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void GreaterThan_SmallerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void GreaterThan_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void GreaterThan_NegativeVsPositive_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterThan();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(-5), [SmallInt(3)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 5: LessOrEqual

    [Fact]
    public void LessOrEqual_SmallerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void LessOrEqual_LargerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.LessOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 6: GreaterOrEqual

    [Fact]
    public void GreaterOrEqual_LargerValue_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(3)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void GreaterOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void GreaterOrEqual_SmallerValue_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.GreaterOrEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(3), [SmallInt(5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 7: Equal

    [Fact]
    public void Equal_EqualValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void Equal_DifferentValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void Equal_ZeroValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(0), [SmallInt(0)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void Equal_NegativeValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.Equal();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(-100), [SmallInt(-100)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 8: NotEqual

    [Fact]
    public void NotEqual_DifferentValues_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void NotEqual_EqualValues_ReturnsFalse()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void NotEqual_PositiveAndNegative_ReturnsTrue()
    {
        var primitive = new SmallIntegerArithmetic.NotEqual();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(5), [SmallInt(-5)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 110: IdentityEquals

    [Fact]
    public void IdentityEquals_SameObject_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var obj = ObjectOOP(100);
        var context = CreateContext(obj, [obj], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void IdentityEquals_DifferentObjects_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var context = CreateContext(ObjectOOP(100), [ObjectOOP(200)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void IdentityEquals_SameSmallIntegers_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(42)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void IdentityEquals_DifferentSmallIntegers_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [SmallInt(43)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void IdentityEquals_SmallIntegerVsObject_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var context = CreateContext(SmallInt(42), [ObjectOOP(84)], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void IdentityEquals_NilVsNil_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heap = new SmalltalkHeapManager();
        var metadata = CreateTestMetadata(heap);
        var nilOOP = metadata.KnownObjects.Nil;
        var trueOOP = metadata.KnownObjects.True;
        var falseOOP = metadata.KnownObjects.False;

        var context = CreateContext(nilOOP, [nilOOP], heap, metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, trueOOP, falseOOP);
    }

    [Fact]
    public void IdentityEquals_TrueVsTrue_ReturnsTrue()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heap = new SmalltalkHeapManager();
        var metadata = CreateTestMetadata(heap);
        var trueOOP = metadata.KnownObjects.True;
        var falseOOP = metadata.KnownObjects.False;

        var context = CreateContext(trueOOP, [trueOOP], heap, metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, trueOOP, falseOOP);
    }

    [Fact]
    public void IdentityEquals_TrueVsFalse_ReturnsFalse()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heap = new SmalltalkHeapManager();
        var metadata = CreateTestMetadata(heap);
        var trueOOP = metadata.KnownObjects.True;
        var falseOOP = metadata.KnownObjects.False;

        var context = CreateContext(trueOOP, [falseOOP], heap, metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, trueOOP, falseOOP);
    }

    [Fact]
    public void IdentityEquals_NoArguments_Fails()
    {
        var primitive = new SimpleObjectPrimitives.IdentityEquals();
        var heapManager = _testHeap;
        var context = CreateContext(ObjectOOP(100), [], heapManager, metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
