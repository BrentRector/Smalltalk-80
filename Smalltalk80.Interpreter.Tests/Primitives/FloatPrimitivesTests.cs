using Moq;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for Float primitives (40-54).
/// </summary>
[Trait("Category", "Primitives")]
public class FloatPrimitivesTests : PrimitiveTestBase
{
    // FloatClassOOP used consistently across all tests
    private static readonly SmalltalkOOP FloatClassOOP = ObjectOOP(50);

    // Shared heap for creating static test metadata
    private static readonly SmalltalkHeapManager _testHeap = new();

    // Create metadata with proper FloatClassOOP for float operations
    private static readonly VMMetadata _testMetadata = CreateFloatMetadataWithRealHeap(_testHeap);
    private static readonly SmalltalkOOP _trueOOP = _testMetadata.KnownObjects.True;
    private static readonly SmalltalkOOP _falseOOP = _testMetadata.KnownObjects.False;

    /// <summary>
    /// Creates test metadata with both real heap objects (for true/false) and proper FloatClassOOP.
    /// Used for comparison tests that need actual true/false objects.
    /// </summary>
    private static VMMetadata CreateFloatMetadataWithRealHeap(SmalltalkHeapManager heap)
    {
        // Allocate actual objects in the heap for true/false
        var nilOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var trueOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var falseOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var smalltalkOOP = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        return new VMMetadata
        {
            KnownObjects = new KnownObjects(
                nil: nilOOP,
                @true: trueOOP,
                @false: falseOOP,
                smalltalk: smalltalkOOP,
                display: nilOOP
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
            OrderedCollectionClassOOP = ObjectOOP(21),
            ProcessClassOOP = ObjectOOP(22),
            PointClassOOP = nilOOP,
            FloatClassOOP = FloatClassOOP,  // Proper FloatClassOOP for tests
            CharacterClassOOP = nilOOP
        };
    }

    /// <summary>
    /// Creates test metadata with a proper FloatClassOOP for Float primitive tests.
    /// Uses mock heap's nil OOP.
    /// </summary>
    private static VMMetadata CreateFloatTestMetadata(ISmalltalkHeap heap)
    {
        var nilOOP = heap.GetNil();

        return new VMMetadata
        {
            KnownObjects = new KnownObjects(
                nil: nilOOP,
                @true: ObjectOOP(1),
                @false: ObjectOOP(2),
                smalltalk: ObjectOOP(3),
                display: nilOOP
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
            OrderedCollectionClassOOP = ObjectOOP(21),
            ProcessClassOOP = ObjectOOP(22),
            PointClassOOP = nilOOP,
            FloatClassOOP = FloatClassOOP,  // Proper FloatClassOOP for tests
            CharacterClassOOP = nilOOP
        };
    }

    /// <summary>
    /// Sets up a mock heap manager to return Float object data from a double value.
    /// Uses GetFloatAsDouble which is the actual method called by the primitive implementation.
    /// </summary>
    private static void SetupFloatObject(Mock<ISmalltalkHeap> mock, SmalltalkOOP floatOOP, double value)
    {
        SetupObjectFormat(mock, floatOOP, ObjectFormat.WordIndexed);
        SetupIndexedSize(mock, floatOOP, 2);  // 2 words (32-bit each) for IEEE 754 double
        SetupClassOOP(mock, floatOOP, FloatClassOOP);

        // Mock GetFloatAsDouble - this is what the primitive actually calls
        mock.Setup(h => h.GetFloatAsDouble(floatOOP)).Returns(value);
    }

    #region Primitive 40: AsFloat

    [Fact]
    public void AsFloat_SmallInteger_ReturnsFloat()
    {
        var primitive = new FloatArithmetic.AsFloat();
        var mockHeap = CreateMockHeapManager();
        var newFloatOOP = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        // Mock allocation with the FloatClassOOP from metadata
        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(newFloatOOP);

        var context = CreateContext(
            SmallInt(42),
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, newFloatOOP);
    }

    [Fact]
    public void AsFloat_NegativeSmallInteger_ReturnsFloat()
    {
        var primitive = new FloatArithmetic.AsFloat();
        var mockHeap = CreateMockHeapManager();
        var newFloatOOP = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        // Mock allocation with the FloatClassOOP from metadata
        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(newFloatOOP);

        var context = CreateContext(
            SmallInt(-100),
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, newFloatOOP);
    }

    [Fact]
    public void AsFloat_ObjectReceiver_Fails()
    {
        var primitive = new FloatArithmetic.AsFloat();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(ObjectOOP(100), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsFloat_WithArguments_Fails()
    {
        var primitive = new FloatArithmetic.AsFloat();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 41: Float Add

    [Fact]
    public void FloatAdd_TwoFloats_ReturnsSum()
    {
        var primitive = new FloatArithmetic.Add();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, float1, 2.5);
        SetupFloatObject(mockHeap, float2, 3.5);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    [Fact]
    public void FloatAdd_FloatAndSmallInteger_ReturnsSum()
    {
        var primitive = new FloatArithmetic.Add();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 2.5);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            floatOOP,
            [SmallInt(3)],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    #endregion

    #region Primitive 42: Float Subtract

    [Fact]
    public void FloatSubtract_TwoFloats_ReturnsDifference()
    {
        var primitive = new FloatArithmetic.Subtract();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, float1, 5.5);
        SetupFloatObject(mockHeap, float2, 2.0);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    #endregion

    #region Primitive 43-48: Float Comparisons

    [Fact]
    public void FloatLessThan_SmallerValue_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.LessThan();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 2.5);
        SetupFloatObject(mockHeap, float2, 3.5);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatLessThan_LargerValue_ReturnsFalse()
    {
        var primitive = new FloatArithmetic.LessThan();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.5);
        SetupFloatObject(mockHeap, float2, 2.5);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, false, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatGreaterThan_LargerValue_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.GreaterThan();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.5);
        SetupFloatObject(mockHeap, float2, 2.5);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.Equal();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.14);
        SetupFloatObject(mockHeap, float2, 3.14);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatNotEqual_DifferentValues_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.NotEqual();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.14);
        SetupFloatObject(mockHeap, float2, 2.71);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatLessOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.LessOrEqual();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.14);
        SetupFloatObject(mockHeap, float2, 3.14);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    [Fact]
    public void FloatGreaterOrEqual_EqualValues_ReturnsTrue()
    {
        var primitive = new FloatArithmetic.GreaterOrEqual();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);

        SetupFloatObject(mockHeap, float1, 3.14);
        SetupFloatObject(mockHeap, float2, 3.14);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: _testMetadata);

        var result = primitive.Execute(context);

        AssertSuccessWithBool(result, true, _trueOOP, _falseOOP);
    }

    #endregion

    #region Primitive 49: Float Multiply

    [Fact]
    public void FloatMultiply_TwoFloats_ReturnsProduct()
    {
        var primitive = new FloatArithmetic.Multiply();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, float1, 2.5);
        SetupFloatObject(mockHeap, float2, 4.0);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    #endregion

    #region Primitive 50: Float Divide

    [Fact]
    public void FloatDivide_ValidDivisor_ReturnsQuotient()
    {
        var primitive = new FloatArithmetic.Divide();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, float1, 10.0);
        SetupFloatObject(mockHeap, float2, 2.5);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    [Fact]
    public void FloatDivide_ByZero_Fails()
    {
        var primitive = new FloatArithmetic.Divide();
        var mockHeap = CreateMockHeapManager();
        var float1 = ObjectOOP(100);
        var float2 = ObjectOOP(150);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, float1, 10.0);
        SetupFloatObject(mockHeap, float2, 0.0);

        var context = CreateContext(
            float1,
            [float2],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 51: Float Truncated

    [Fact]
    public void FloatTruncated_PositiveValue_ReturnsTruncatedSmallInteger()
    {
        var primitive = new FloatArithmetic.Truncated();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 3.7);

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 3);
    }

    [Fact]
    public void FloatTruncated_NegativeValue_TruncatesTowardZero()
    {
        var primitive = new FloatArithmetic.Truncated();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, -3.7);

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, -3);
    }

    [Fact]
    public void FloatTruncated_OutOfSmallIntegerRange_Fails()
    {
        var primitive = new FloatArithmetic.Truncated();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 2e10); // Too large

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 52: Float FractionPart

    [Fact]
    public void FloatFractionPart_PositiveValue_ReturnsFractionalPart()
    {
        var primitive = new FloatArithmetic.FractionPart();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 3.75);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    #endregion

    #region Primitive 53: Float Exponent

    [Fact]
    public void FloatExponent_PowerOfTwo_ReturnsExponent()
    {
        var primitive = new FloatArithmetic.Exponent();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 8.0); // 2^3

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        Assert.True(result.Value.IsSmallInteger);
    }

    [Fact]
    public void FloatExponent_Zero_ReturnsZero()
    {
        var primitive = new FloatArithmetic.Exponent();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 0.0);

        var context = CreateContext(
            floatOOP,
            [],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithSmallInt(result, 0);
    }

    #endregion

    #region Primitive 54: Float TimesTwoPower

    [Fact]
    public void FloatTimesTwoPower_PositivePower_MultipliesByPowerOfTwo()
    {
        var primitive = new FloatArithmetic.TimesTwoPower();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 1.5);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            floatOOP,
            [SmallInt(3)], // 1.5 * 2^3 = 12.0
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    [Fact]
    public void FloatTimesTwoPower_NegativePower_DividesByPowerOfTwo()
    {
        var primitive = new FloatArithmetic.TimesTwoPower();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var resultFloat = ObjectOOP(200);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 8.0);

        mockHeap.Setup(h => h.AllocateInstanceOf(FloatClassOOP, 2)).Returns(resultFloat);

        var context = CreateContext(
            floatOOP,
            [SmallInt(-2)], // 8.0 * 2^-2 = 2.0
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultFloat);
    }

    [Fact]
    public void FloatTimesTwoPower_NonSmallIntegerPower_Fails()
    {
        var primitive = new FloatArithmetic.TimesTwoPower();
        var mockHeap = CreateMockHeapManager();
        var floatOOP = ObjectOOP(100);
        var metadata = CreateFloatTestMetadata(mockHeap.Object);

        SetupFloatObject(mockHeap, floatOOP, 8.0);

        var context = CreateContext(
            floatOOP,
            [ObjectOOP(200)],
            mockHeap.Object,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
