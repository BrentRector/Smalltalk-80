using System.Collections.Generic;
using Xunit;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Comprehensive tests for SmallInteger arithmetic primitives (1-17).
/// Tests success cases, boundary conditions, and error handling.
/// </summary>
public class ArithmeticPrimitivesTests
{
    private readonly TestPrimitiveContext _ctx;

    public ArithmeticPrimitivesTests()
    {
        _ctx = new TestPrimitiveContext();
    }

    #region Primitive 1: Add (+)

    [Fact]
    public void Add_ValidPositiveIntegers_ReturnsSum()
    {
        // Arrange
        var receiver = SmalltalkOOP.FromSmallInteger(5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        // Act
        var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

        // Assert
        Assert.True(_ctx.PrimitiveSuccess, "Primitive should succeed");
        Assert.True(result.IsSmallInteger, "Result should be SmallInteger");
        Assert.Equal(8, result.AsSmallInteger());
    }

    [Fact]
    public void Add_NegativeNumbers_ReturnsCorrectSum()
    {
        // Arrange
        var receiver = SmalltalkOOP.FromSmallInteger(-5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        // Act
        var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

        // Assert
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-2, result.AsSmallInteger());
    }

    [Fact]
    public void Add_Zero_ReturnsReceiver()
    {
        // Arrange
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        // Act
        var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

        // Assert
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(42, result.AsSmallInteger());
    }

    [Fact]
    public void Add_Overflow_Fails()
    {
        // Arrange - SmallInteger max is 2^30-1 = 1073741823
        var receiver = SmalltalkOOP.FromSmallInteger(1073741823);
        var args = new[] { SmalltalkOOP.FromSmallInteger(1) };

        // Act
        var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

        // Assert - should fail (primitive would promote to LargeInteger in full implementation)
        Assert.False(_ctx.PrimitiveSuccess, "Addition overflow should fail primitive");
    }

    [Fact]
    public void Add_WrongArgCount_Fails()
    {
        // Arrange
        var receiver = SmalltalkOOP.FromSmallInteger(5);
        var args = new SmalltalkOOP[] { }; // Wrong: needs 1 arg

        // Act
        var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

        // Assert
        Assert.False(_ctx.PrimitiveSuccess, "Wrong arg count should fail");
    }

    #endregion

    #region Primitive 2: Subtract (-)

    [Fact]
    public void Subtract_ValidIntegers_ReturnsDifference()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(10);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        var result = ArithmeticPrimitives.Subtract(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(7, result.AsSmallInteger());
    }

    [Fact]
    public void Subtract_ResultNegative_ReturnsNegative()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(3);
        var args = new[] { SmalltalkOOP.FromSmallInteger(10) };

        var result = ArithmeticPrimitives.Subtract(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-7, result.AsSmallInteger());
    }

    [Fact]
    public void Subtract_Underflow_Fails()
    {
        // SmallInteger min is -2^30 = -1073741824
        var receiver = SmalltalkOOP.FromSmallInteger(-1073741824);
        var args = new[] { SmalltalkOOP.FromSmallInteger(1) };

        var result = ArithmeticPrimitives.Subtract(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Subtraction underflow should fail");
    }

    #endregion

    #region Primitive 3-8: Comparisons

    [Fact]
    public void LessThan_ReceiverSmaller_ReturnsTrue()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(3);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.LessThan(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    [Fact]
    public void LessThan_ReceiverLarger_ReturnsFalse()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        var result = ArithmeticPrimitives.LessThan(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.False, result);
    }

    [Fact]
    public void LessThan_Equal_ReturnsFalse()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.LessThan(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.False, result);
    }

    [Fact]
    public void GreaterThan_ReceiverLarger_ReturnsTrue()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(10);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.GreaterThan(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    [Fact]
    public void Equal_SameValues_ReturnsTrue()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(42) };

        var result = ArithmeticPrimitives.Equal(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    [Fact]
    public void Equal_DifferentValues_ReturnsFalse()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(43) };

        var result = ArithmeticPrimitives.Equal(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.False, result);
    }

    [Fact]
    public void NotEqual_DifferentValues_ReturnsTrue()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(10) };

        var result = ArithmeticPrimitives.NotEqual(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    #endregion

    #region Primitive 9: Multiply (*)

    [Fact]
    public void Multiply_ValidIntegers_ReturnsProduct()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(6);
        var args = new[] { SmalltalkOOP.FromSmallInteger(7) };

        var result = ArithmeticPrimitives.Multiply(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(42, result.AsSmallInteger());
    }

    [Fact]
    public void Multiply_ByZero_ReturnsZero()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        var result = ArithmeticPrimitives.Multiply(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result.AsSmallInteger());
    }

    [Fact]
    public void Multiply_Negative_ReturnsNegativeProduct()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(-5);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        var result = ArithmeticPrimitives.Multiply(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-15, result.AsSmallInteger());
    }

    [Fact]
    public void Multiply_Overflow_Fails()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(100000);
        var args = new[] { SmalltalkOOP.FromSmallInteger(100000) };

        var result = ArithmeticPrimitives.Multiply(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Multiplication overflow should fail");
    }

    #endregion

    #region Primitive 10: Divide (/)

    [Fact]
    public void Divide_ExactDivision_ReturnsQuotient()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(15);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        var result = ArithmeticPrimitives.Divide(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(5, result.AsSmallInteger());
    }

    [Fact]
    public void Divide_InexactDivision_Fails()
    {
        // Primitive / requires exact division (no remainder)
        var receiver = SmalltalkOOP.FromSmallInteger(16);
        var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

        var result = ArithmeticPrimitives.Divide(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Inexact division should fail primitive /");
    }

    [Fact]
    public void Divide_ByZero_Fails()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        var result = ArithmeticPrimitives.Divide(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Division by zero should fail");
    }

    [Fact]
    public void Divide_MinIntByNegativeOne_Fails()
    {
        // Special case: SmallIntegerMin / -1 causes overflow (result would be 2^30, outside SmallInteger range)
        var receiver = SmalltalkOOP.FromSmallInteger(-1073741824); // SmallIntegerMin
        var args = new[] { SmalltalkOOP.FromSmallInteger(-1) };

        var result = ArithmeticPrimitives.Divide(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "SmallIntegerMin / -1 overflow should fail");
    }

    #endregion

    #region Primitive 11: Modulo (\\)

    [Fact]
    public void Mod_PositiveNumbers_ReturnsRemainder()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Mod(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        // 17 \\ 5 should return 2 in Smalltalk (floor modulo)
        Assert.Equal(2, result.AsSmallInteger());
    }

    [Fact]
    public void Mod_NegativeDividend_ReturnsPositiveRemainder()
    {
        // In Smalltalk, -17 \\ 5 = 3 (remainder has sign of divisor)
        var receiver = SmalltalkOOP.FromSmallInteger(-17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Mod(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(3, result.AsSmallInteger());
    }

    [Fact]
    public void Mod_ByZero_Fails()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        var result = ArithmeticPrimitives.Mod(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess);
    }

    #endregion

    #region Primitive 12: Floor Division (//)

    [Fact]
    public void Div_PositiveNumbers_ReturnsFloorQuotient()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Div(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(3, result.AsSmallInteger());
    }

    [Fact]
    public void Div_NegativeDividend_FloorsTowardNegativeInfinity()
    {
        // In Smalltalk, -17 // 5 = -4 (floors toward -∞, not truncates)
        var receiver = SmalltalkOOP.FromSmallInteger(-17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Div(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-4, result.AsSmallInteger());
    }

    [Fact]
    public void Div_ByZero_Fails()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        var result = ArithmeticPrimitives.Div(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess);
    }

    #endregion

    #region Primitive 13: Quo (truncating division)

    [Fact]
    public void Quo_PositiveNumbers_ReturnsTruncatedQuotient()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Quo(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(3, result.AsSmallInteger());
    }

    [Fact]
    public void Quo_NegativeDividend_TruncatesTowardZero()
    {
        // quo: truncates toward 0, unlike // which floors
        // -17 quo: 5 = -3 (not -4)
        var receiver = SmalltalkOOP.FromSmallInteger(-17);
        var args = new[] { SmalltalkOOP.FromSmallInteger(5) };

        var result = ArithmeticPrimitives.Quo(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-3, result.AsSmallInteger());
    }

    [Fact]
    public void Quo_ByZero_Fails()
    {
        var receiver = SmalltalkOOP.FromSmallInteger(42);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0) };

        var result = ArithmeticPrimitives.Quo(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess);
    }

    #endregion

    #region Primitive 14-16: Bitwise Operations

    [Fact]
    public void BitAnd_ValidIntegers_ReturnsBitwiseAnd()
    {
        // 0b1101 & 0b1011 = 0b1001 = 9
        var receiver = SmalltalkOOP.FromSmallInteger(0b1101);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0b1011) };

        var result = ArithmeticPrimitives.BitAnd(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0b1001, result.AsSmallInteger());
    }

    [Fact]
    public void BitOr_ValidIntegers_ReturnsBitwiseOr()
    {
        // 0b1100 | 0b1010 = 0b1110 = 14
        var receiver = SmalltalkOOP.FromSmallInteger(0b1100);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0b1010) };

        var result = ArithmeticPrimitives.BitOr(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0b1110, result.AsSmallInteger());
    }

    [Fact]
    public void BitXor_ValidIntegers_ReturnsBitwiseXor()
    {
        // 0b1100 ^ 0b1010 = 0b0110 = 6
        var receiver = SmalltalkOOP.FromSmallInteger(0b1100);
        var args = new[] { SmalltalkOOP.FromSmallInteger(0b1010) };

        var result = ArithmeticPrimitives.BitXor(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0b0110, result.AsSmallInteger());
    }

    #endregion

    #region Primitive 17: Bit Shift

    [Fact]
    public void BitShift_LeftShift_ReturnsShiftedValue()
    {
        // 1 << 4 = 16
        var receiver = SmalltalkOOP.FromSmallInteger(1);
        var args = new[] { SmalltalkOOP.FromSmallInteger(4) };

        var result = ArithmeticPrimitives.BitShift(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(16, result.AsSmallInteger());
    }

    [Fact]
    public void BitShift_RightShift_ReturnsShiftedValue()
    {
        // 16 >> 2 = 4 (represented as bitShift: -2)
        var receiver = SmalltalkOOP.FromSmallInteger(16);
        var args = new[] { SmalltalkOOP.FromSmallInteger(-2) };

        var result = ArithmeticPrimitives.BitShift(_ctx, receiver, args);

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(4, result.AsSmallInteger());
    }

    [Fact]
    public void BitShift_LeftShiftOverflow_Fails()
    {
        // Shift that would overflow SmallInteger range should fail
        var receiver = SmalltalkOOP.FromSmallInteger(1);
        var args = new[] { SmalltalkOOP.FromSmallInteger(31) }; // Would exceed 30-bit range

        var result = ArithmeticPrimitives.BitShift(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Bit shift overflow should fail");
    }

    [Fact]
    public void BitShift_NegativeLeftShift_Fails()
    {
        // Negative number left shift that overflows (-2 << 30 would give -2147483648, outside SmallInteger range)
        var receiver = SmalltalkOOP.FromSmallInteger(-2);
        var args = new[] { SmalltalkOOP.FromSmallInteger(30) };

        var result = ArithmeticPrimitives.BitShift(_ctx, receiver, args);

        Assert.False(_ctx.PrimitiveSuccess, "Negative left shift overflow should fail");
    }

    #endregion
}

/// <summary>
/// Simple test implementation of IPrimitiveContext for primitive testing.
/// </summary>
internal class TestPrimitiveContext : IPrimitiveContext
{
    public bool PrimitiveSuccess { get; set; } = true;
    public ISmalltalkHeapManager HeapManager => throw new NotImplementedException("HeapManager not needed for arithmetic tests");
    public IPlatformBridge? PlatformBridge => null;

    // Not needed for arithmetic primitives - throw NotImplementedException
    public SmalltalkOOP GetFieldValue(SmalltalkOOP oop, string className, string fieldName) => throw new NotImplementedException();
    public void SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value) => throw new NotImplementedException();
    public int GetFieldIndex(string className, string fieldName) => throw new NotImplementedException();
    public SmalltalkOOP GetInstanceVariable(SmalltalkOOP oop, int index) => throw new NotImplementedException();
    public void SetInstanceVariable(SmalltalkOOP oop, int index, SmalltalkOOP value) => throw new NotImplementedException();
    public SmalltalkOOP GetIndexedOOP(SmalltalkOOP oop, int index) => throw new NotImplementedException();
    public void SetIndexedOOP(SmalltalkOOP oop, int index, SmalltalkOOP value) => throw new NotImplementedException();
    public int GetIndexedSize(SmalltalkOOP oop) => throw new NotImplementedException();
    public Memory<byte> GetByteArray(SmalltalkOOP oop) => throw new NotImplementedException();
    public SmalltalkOOP AllocateObject(string className, int indexedSize) => throw new NotImplementedException();
    public SmalltalkOOP GetGlobalObjectByName(string globalName) => throw new NotImplementedException();
    public void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2) => throw new NotImplementedException();
    public void BecomeOneWay(SmalltalkOOP fromOop, SmalltalkOOP toOop) => throw new NotImplementedException();
    public SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args, SmalltalkOOP homeContextOOP, SmalltalkOOP blockClosureOOP) => throw new NotImplementedException();
    public SmalltalkOOP SendMessage(SmalltalkOOP receiverOop, SmalltalkOOP selectorOop, SmalltalkOOP argumentsArray) => throw new NotImplementedException();
    public SmalltalkOOP GetActiveContext() => throw new NotImplementedException();
    public int GetInstructionPointer() => throw new NotImplementedException();
    public SmalltalkOOP CreateBlockContext(SmalltalkOOP homeContextOop, int startPC, int argCount) => throw new NotImplementedException();
    public SmalltalkOOP ActivateBlockContext(SmalltalkOOP blockContextOop, SmalltalkOOP argumentsArray) => throw new NotImplementedException();
    public IEnumerable<SmalltalkOOP> GetAllObjects() => throw new NotImplementedException();
    public SmalltalkOOP InternSymbol(string symbolName) => throw new NotImplementedException();
    public string GetClassNameForObject(SmalltalkOOP oop) => throw new NotImplementedException();
    public void ClearMethodCache() => throw new NotImplementedException();
    public void InvalidateAllRuntimeCaches() => throw new NotImplementedException();
}
