using System;
using System.Collections.Generic;
using Xunit;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Comprehensive exhaustive tests for SmallInteger arithmetic primitives (1-17).
/// Based on Smalltalk-80 VM specification for 32-bit tagged SmallInteger (31-bit payload).
/// SmallInteger range: -2^30 to 2^30-1 (-1073741824 to 1073741823).
///
/// Test coverage includes:
/// - Identity operations
/// - Boundary conditions (MIN_SI, MAX_SI)
/// - Overflow/underflow (promotion to LargeInteger)
/// - Division semantics (exact division, quotient, remainder)
/// - Sign rules and edge cases
/// - Bitwise operations and shifts
/// - Comparison operations
/// - Invalid inputs and type errors
/// - Algebraic properties
/// </summary>
public class ComprehensiveArithmeticTests
{
    private readonly TestPrimitiveContext _ctx;

    // SmallInteger constants: 30-bit signed range
    private const int MIN_SI = -(1 << 30);      // -1073741824
    private const int MAX_SI = (1 << 30) - 1;   // 1073741823

    public ComprehensiveArithmeticTests()
    {
        _ctx = new TestPrimitiveContext();
    }

    #region Core Identity and Simple Cases

    [Fact]
    public void SI_ADD_IDENTITY()
    {
        // ID: SI_ADD_IDENTITY - additive identity
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result.AsSmallInteger());
    }

    [Fact]
    public void SI_ADD_SIMPLE()
    {
        // ID: SI_ADD_SIMPLE - simple addition
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(2),
            new[] { SmalltalkOOP.FromSmallInteger(3) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(5, result.AsSmallInteger());
    }

    [Fact]
    public void SI_SUB_SIMPLE()
    {
        // ID: SI_SUB_SIMPLE - simple subtraction
        var result = ArithmeticPrimitives.Subtract(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(4) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(3, result.AsSmallInteger());
    }

    [Fact]
    public void SI_MUL_SIMPLE()
    {
        // ID: SI_MUL_SIMPLE - simple multiplication
        var result = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(6),
            new[] { SmalltalkOOP.FromSmallInteger(5) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(30, result.AsSmallInteger());
    }

    [Fact]
    public void SI_NEG_SIMPLE()
    {
        // ID: SI_NEG_SIMPLE - negation simple (using subtraction: 0 - 42)
        var result = ArithmeticPrimitives.Subtract(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(42) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-42, result.AsSmallInteger());
    }

    #endregion

    #region Boundary Tests - MIN_SI and MAX_SI

    [Fact]
    public void SI_ADD_MAX_ZERO()
    {
        // ID: SI_ADD_MAX_ZERO - MAX_SI + 0
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(MAX_SI, result.AsSmallInteger());
    }

    [Fact]
    public void SI_ADD_MIN_ZERO()
    {
        // ID: SI_ADD_MIN_ZERO - MIN_SI + 0
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MIN_SI),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(MIN_SI, result.AsSmallInteger());
    }

    [Fact]
    public void SI_ADD_MAX_PLUS_ONE_PROMOTE()
    {
        // ID: SI_ADD_MAX_PLUS_ONE_PROMOTE
        // MAX_SI + 1 must fail primitive (would promote to LargeInteger in image)
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(1) });

        Assert.False(_ctx.PrimitiveSuccess, "MAX_SI + 1 should fail (overflow)");
    }

    [Fact]
    public void SI_SUB_MIN_MINUS_ONE_PROMOTE()
    {
        // ID: SI_SUB_MIN_MINUS_ONE_PROMOTE
        // MIN_SI - 1 must fail primitive (would promote to LargeInteger)
        var result = ArithmeticPrimitives.Subtract(_ctx,
            SmalltalkOOP.FromSmallInteger(MIN_SI),
            new[] { SmalltalkOOP.FromSmallInteger(1) });

        Assert.False(_ctx.PrimitiveSuccess, "MIN_SI - 1 should fail (underflow)");
    }

    [Fact]
    public void SI_MUL_HALF_OVERFLOW()
    {
        // ID: SI_MUL_HALF_OVERFLOW - MAX_SI * 2 must fail
        var result = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(2) });

        Assert.False(_ctx.PrimitiveSuccess, "MAX_SI * 2 should fail (overflow)");
    }

    [Fact]
    public void SI_NEG_MIN_OVERFLOW()
    {
        // ID: SI_NEG_MIN_OVERFLOW
        // Negation of MIN_SI (0 - MIN_SI) must fail - result would be 2^30 (outside range)
        var result = ArithmeticPrimitives.Subtract(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(MIN_SI) });

        Assert.False(_ctx.PrimitiveSuccess, "0 - MIN_SI should fail (result = 2^30, outside SmallInteger range)");
    }

    [Fact]
    public void SI_ADD_MAX_NEGATIVE_PAIR()
    {
        // ID: SI_ADD_MAX_NEGATIVE_PAIR - MAX_SI + (-MAX_SI) = 0
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(-MAX_SI) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result.AsSmallInteger());
    }

    [Fact]
    public void SI_ADD_MAX_PLUS_MAX_OVERFLOW()
    {
        // ID: SI_SUM_EDGE_COMBINATIONS (part 1) - MAX_SI + MAX_SI overflows
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(MAX_SI) });

        Assert.False(_ctx.PrimitiveSuccess, "MAX_SI + MAX_SI should overflow");
    }

    [Fact]
    public void SI_ADD_MIN_PLUS_MIN_UNDERFLOW()
    {
        // ID: SI_SUM_EDGE_COMBINATIONS (part 2) - MIN_SI + MIN_SI underflows
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MIN_SI),
            new[] { SmalltalkOOP.FromSmallInteger(MIN_SI) });

        Assert.False(_ctx.PrimitiveSuccess, "MIN_SI + MIN_SI should underflow");
    }

    [Fact]
    public void SI_ADD_MAX_PLUS_MIN()
    {
        // ID: SI_SUM_EDGE_COMBINATIONS (part 3) - MAX_SI + MIN_SI = -1
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(MIN_SI) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-1, result.AsSmallInteger());
    }

    #endregion

    #region Division Semantics

    [Fact]
    public void SI_DIV_INTEGER_EXACT()
    {
        // ID: SI_DIV_INTEGER_EXACT - 20 / 4 = 5 (exact division)
        var result = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(20),
            new[] { SmalltalkOOP.FromSmallInteger(4) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(5, result.AsSmallInteger());
    }

    [Fact]
    public void SI_DIV_INTEGER_NONEXACT()
    {
        // ID: SI_DIV_INTEGER_NONEXACT - 7 / 2 should fail (not exact)
        var result = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(2) });

        Assert.False(_ctx.PrimitiveSuccess, "Inexact division should fail primitive /");
    }

    [Fact]
    public void SI_QUOTIENT_REMAINDER_POSITIVE()
    {
        // ID: SI_QUOTIENT_REMAINDER_POSITIVE - 7 // 3 = 2, 7 \\ 3 = 1
        var quotient = ArithmeticPrimitives.Quo(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });

        _ctx.PrimitiveSuccess = true; // Reset
        var remainder = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });

        Assert.Equal(2, quotient.AsSmallInteger());
        Assert.Equal(1, remainder.AsSmallInteger());

        // Verify invariant: 7 = 3*2 + 1
        Assert.Equal(7, 3 * quotient.AsSmallInteger() + remainder.AsSmallInteger());
    }

    [Fact]
    public void SI_QUOTIENT_REMAINDER_NEG_DIVIDEND()
    {
        // ID: SI_QUOTIENT_REMAINDER_NEG_DIVIDEND
        // Verify dividend = divisor * quotient + remainder invariant holds
        // Use div: and \\ (mod) which are matching floor division operations
        var quotient = ArithmeticPrimitives.Div(_ctx,
            SmalltalkOOP.FromSmallInteger(-7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });

        _ctx.PrimitiveSuccess = true;
        var remainder = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(-7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });

        // Assert invariant: -7 = 3 * quo + rem
        // For -7 div: 3, quotient = -3 (floor division)
        // For -7 \\ 3, remainder = 2 (follows divisor sign)
        // Check: 3 * -3 + 2 = -9 + 2 = -7 ✓
        int quo = quotient.AsSmallInteger();
        int rem = remainder.AsSmallInteger();
        Assert.Equal(-7, 3 * quo + rem);
    }

    [Fact]
    public void SI_DIV_BY_ZERO_INTEGER_NEGATIVE()
    {
        // ID: SI_DIV_BY_ZERO_INTEGER_NEGATIVE - division by zero must fail
        var result = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(5),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.False(_ctx.PrimitiveSuccess, "Division by zero should fail");
    }

    [Fact]
    public void SI_INT_MIN_DIV_NEG1_OVERFLOW()
    {
        // ID: SI_INT_MIN_DIV_NEG1_OVERFLOW
        // MIN_SI / -1 would give 2^30 (outside SmallInteger range)
        var result = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(MIN_SI),
            new[] { SmalltalkOOP.FromSmallInteger(-1) });

        Assert.False(_ctx.PrimitiveSuccess, "MIN_SI / -1 should fail (result outside SmallInteger range)");
    }

    [Fact]
    public void SI_DIV_BY_ZERO_QUOTIENT_REMAINDER()
    {
        // Quotient by zero
        var quotient = ArithmeticPrimitives.Quo(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(0) });
        Assert.False(_ctx.PrimitiveSuccess, "Quotient by zero should fail");

        _ctx.PrimitiveSuccess = true;

        // Remainder by zero
        var remainder = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(0) });
        Assert.False(_ctx.PrimitiveSuccess, "Remainder by zero should fail");
    }

    #endregion

    #region Modulus/Remainder Sign Semantics

    [Fact]
    public void SI_REM_POS_POS()
    {
        // ID: SI_REM_POS_POS - 17 \\ 5 = 2
        var result = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(17),
            new[] { SmalltalkOOP.FromSmallInteger(5) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(2, result.AsSmallInteger());
    }

    [Fact]
    public void SI_REM_NEG_POS()
    {
        // ID: SI_REM_NEG_POS - verify invariant for -17 \\ 5
        var quotient = ArithmeticPrimitives.Div(_ctx,
            SmalltalkOOP.FromSmallInteger(-17),
            new[] { SmalltalkOOP.FromSmallInteger(5) });

        _ctx.PrimitiveSuccess = true;
        var remainder = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(-17),
            new[] { SmalltalkOOP.FromSmallInteger(5) });

        int quo = quotient.AsSmallInteger();
        int rem = remainder.AsSmallInteger();

        // Verify invariant: -17 = 5 * quo + rem
        Assert.Equal(-17, 5 * quo + rem);

        // Verify remainder is positive (follows divisor sign in Smalltalk)
        Assert.True(rem >= 0, "Remainder should be non-negative when divisor is positive");
    }

    [Fact]
    public void SI_REM_POS_NEG()
    {
        // ID: SI_REM_POS_NEG - verify invariant for 17 \\ -5
        var quotient = ArithmeticPrimitives.Div(_ctx,
            SmalltalkOOP.FromSmallInteger(17),
            new[] { SmalltalkOOP.FromSmallInteger(-5) });

        _ctx.PrimitiveSuccess = true;
        var remainder = ArithmeticPrimitives.Mod(_ctx,
            SmalltalkOOP.FromSmallInteger(17),
            new[] { SmalltalkOOP.FromSmallInteger(-5) });

        int quo = quotient.AsSmallInteger();
        int rem = remainder.AsSmallInteger();

        // Verify invariant: 17 = -5 * quo + rem
        Assert.Equal(17, -5 * quo + rem);
    }

    #endregion

    #region Bitwise Operations

    [Fact]
    public void SI_BIT_AND_ZERO()
    {
        // ID: SI_BIT_AND_ZERO - x & 0 = 0
        var result = ArithmeticPrimitives.BitAnd(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result.AsSmallInteger());
    }

    [Fact]
    public void SI_BIT_OR_ZERO()
    {
        // ID: SI_BIT_OR_ZERO - x | 0 = x
        var result = ArithmeticPrimitives.BitOr(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(12345, result.AsSmallInteger());
    }

    [Fact]
    public void SI_BIT_XOR_ZERO()
    {
        // ID: SI_BIT_XOR_ZERO - x ^ 0 = x
        var result = ArithmeticPrimitives.BitXor(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(12345, result.AsSmallInteger());
    }

    [Fact]
    public void SI_BIT_AND_SELF()
    {
        // x & x = x (idempotent)
        var result = ArithmeticPrimitives.BitAnd(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(12345) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(12345, result.AsSmallInteger());
    }

    [Fact]
    public void SI_BIT_OR_SELF()
    {
        // x | x = x (idempotent)
        var result = ArithmeticPrimitives.BitOr(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(12345) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(12345, result.AsSmallInteger());
    }

    [Fact]
    public void SI_BIT_XOR_SELF()
    {
        // x ^ x = 0
        var result = ArithmeticPrimitives.BitXor(_ctx,
            SmalltalkOOP.FromSmallInteger(12345),
            new[] { SmalltalkOOP.FromSmallInteger(12345) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result.AsSmallInteger());
    }

    #endregion

    #region Bit Shift Operations

    [Fact]
    public void SI_SHIFT_LEFT_BASIC()
    {
        // ID: SI_SHIFT_LEFT_BASIC - 1 << 4 = 16
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(1),
            new[] { SmalltalkOOP.FromSmallInteger(4) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(16, result.AsSmallInteger());
    }

    [Fact]
    public void SI_SHIFT_RIGHT_ARITHMETIC()
    {
        // ID: SI_SHIFT_RIGHT_ARITHMETIC - (-8) >> 2 = -2 (sign-preserving)
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(-8),
            new[] { SmalltalkOOP.FromSmallInteger(-2) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-2, result.AsSmallInteger());
    }

    [Fact]
    public void SI_SHIFT_BY_WIDTH_OVERFLOW()
    {
        // ID: SI_SHIFT_BY_WIDTH_INVALID - shift by 30 bits should fail or handle gracefully
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(2),
            new[] { SmalltalkOOP.FromSmallInteger(30) });

        // Accept either success with overflow or primitive failure
        // If success, result should overflow SmallInteger range
        if (_ctx.PrimitiveSuccess)
        {
            // Result is 2^31 which overflows - primitive should have failed
            Assert.Fail("Shift by 30 causing overflow should fail");
        }
    }

    [Fact]
    public void SI_SHIFT_LEFT_OVERFLOW()
    {
        // Large value shifted left overflows
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(1) });

        Assert.False(_ctx.PrimitiveSuccess, "Shifting MAX_SI left should overflow");
    }

    [Fact]
    public void SI_SHIFT_NEGATIVE_LEFT_OVERFLOW()
    {
        // -2 << 30 overflows SmallInteger range
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(-2),
            new[] { SmalltalkOOP.FromSmallInteger(30) });

        Assert.False(_ctx.PrimitiveSuccess, "Negative shift left overflow should fail");
    }

    [Fact]
    public void SI_SHIFT_ZERO_BY_ANYTHING()
    {
        // 0 << n = 0 (always safe)
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(100) });

        // Should fail because shift amount > 30
        Assert.False(_ctx.PrimitiveSuccess);
    }

    [Fact]
    public void SI_SHIFT_BY_ZERO()
    {
        // x << 0 = x
        var result = ArithmeticPrimitives.BitShift(_ctx,
            SmalltalkOOP.FromSmallInteger(42),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(42, result.AsSmallInteger());
    }

    #endregion

    #region Comparison Operations

    [Fact]
    public void SI_COMPARISON_LESS_THAN()
    {
        // Basic less than
        var result = ArithmeticPrimitives.LessThan(_ctx,
            SmalltalkOOP.FromSmallInteger(3),
            new[] { SmalltalkOOP.FromSmallInteger(5) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    [Fact]
    public void SI_COMPARISON_BOUNDARIES()
    {
        // ID: SI_COMPARISON_BOUNDARIES - MIN_SI < (MIN_SI + 1)
        var result1 = ArithmeticPrimitives.LessThan(_ctx,
            SmalltalkOOP.FromSmallInteger(MIN_SI),
            new[] { SmalltalkOOP.FromSmallInteger(MIN_SI + 1) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result1);

        _ctx.PrimitiveSuccess = true;

        // MAX_SI >= MAX_SI
        var result2 = ArithmeticPrimitives.GreaterOrEqual(_ctx,
            SmalltalkOOP.FromSmallInteger(MAX_SI),
            new[] { SmalltalkOOP.FromSmallInteger(MAX_SI) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result2);
    }

    [Fact]
    public void SI_EQ_ZERO_IDENTITY()
    {
        // ID: SI_EQ_NEG_ZERO_NOT_APPLICABLE - 0 = 0 (single zero representation)
        var result = ArithmeticPrimitives.Equal(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(0) });

        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(SpecialObjectsArray.Instance.True, result);
    }

    #endregion

    #region Invalid Inputs and Type Errors

    [Fact]
    public void SI_ADD_WRONG_ARG_COUNT()
    {
        // Wrong number of arguments
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(5),
            new SmalltalkOOP[] { });

        Assert.False(_ctx.PrimitiveSuccess, "Wrong arg count should fail");
    }

    [Fact]
    public void SI_ADD_NON_INTEGER_RECEIVER()
    {
        // Non-SmallInteger receiver (use nil OOP)
        var result = ArithmeticPrimitives.Add(_ctx,
            SpecialObjectsArray.Instance.Nil,
            new[] { SmalltalkOOP.FromSmallInteger(1) });

        Assert.False(_ctx.PrimitiveSuccess, "Non-integer receiver should fail");
    }

    [Fact]
    public void SI_ADD_NON_INTEGER_ARGUMENT()
    {
        // Non-SmallInteger argument
        var result = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(5),
            new[] { SpecialObjectsArray.Instance.Nil });

        Assert.False(_ctx.PrimitiveSuccess, "Non-integer argument should fail");
    }

    #endregion

    #region Algebraic Properties

    [Fact]
    public void SI_COMMUTATIVE_ADD()
    {
        // ID: SI_COMMUTATIVE_ADD - a + b = b + a
        int a = 12345;
        int b = -6789;

        var result1 = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { SmalltalkOOP.FromSmallInteger(b) });

        _ctx.PrimitiveSuccess = true;

        var result2 = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(b),
            new[] { SmalltalkOOP.FromSmallInteger(a) });

        Assert.Equal(result1.AsSmallInteger(), result2.AsSmallInteger());
    }

    [Fact]
    public void SI_ASSOCIATIVE_ADD()
    {
        // ID: SI_ASSOCIATIVE_ADD_OVERFLOW_SENSITIVITY
        // (a + b) + c = a + (b + c) when no overflow
        int a = 100;
        int b = 200;
        int c = 300;

        // (a + b) + c
        var temp1 = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { SmalltalkOOP.FromSmallInteger(b) });
        _ctx.PrimitiveSuccess = true;
        var result1 = ArithmeticPrimitives.Add(_ctx,
            temp1,
            new[] { SmalltalkOOP.FromSmallInteger(c) });

        _ctx.PrimitiveSuccess = true;

        // a + (b + c)
        var temp2 = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(b),
            new[] { SmalltalkOOP.FromSmallInteger(c) });
        _ctx.PrimitiveSuccess = true;
        var result2 = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { temp2 });

        Assert.Equal(result1.AsSmallInteger(), result2.AsSmallInteger());
    }

    [Fact]
    public void SI_DISTRIBUTIVE_TEST()
    {
        // ID: SI_DISTRIBUTIVE_TEST - a * (b + c) = a*b + a*c
        int a = 7;
        int b = 11;
        int c = 13;

        // a * (b + c)
        var sum = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(b),
            new[] { SmalltalkOOP.FromSmallInteger(c) });
        _ctx.PrimitiveSuccess = true;
        var result1 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { sum });

        _ctx.PrimitiveSuccess = true;

        // a*b + a*c
        var prod1 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { SmalltalkOOP.FromSmallInteger(b) });
        _ctx.PrimitiveSuccess = true;
        var prod2 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(a),
            new[] { SmalltalkOOP.FromSmallInteger(c) });
        _ctx.PrimitiveSuccess = true;
        var result2 = ArithmeticPrimitives.Add(_ctx,
            prod1,
            new[] { prod2 });

        Assert.Equal(result1.AsSmallInteger(), result2.AsSmallInteger());
    }

    #endregion

    #region Stress and Regression Tests

    [Fact]
    public void SI_REPEATED_INCREMENT_CROSSING_BOUNDARY()
    {
        // ID: SI_REPEATED_INCREMENT_DRIFT
        // Start near MAX_SI and increment - should fail when crossing boundary
        int counter = MAX_SI - 5;

        for (int i = 0; i < 5; i++)
        {
            _ctx.PrimitiveSuccess = true;
            var result = ArithmeticPrimitives.Add(_ctx,
                SmalltalkOOP.FromSmallInteger(counter),
                new[] { SmalltalkOOP.FromSmallInteger(1) });

            Assert.True(_ctx.PrimitiveSuccess, $"Increment {i} should succeed");
            counter = result.AsSmallInteger();
        }

        // This should fail (counter is now MAX_SI, adding 1 overflows)
        _ctx.PrimitiveSuccess = true;
        var overflow = ArithmeticPrimitives.Add(_ctx,
            SmalltalkOOP.FromSmallInteger(counter),
            new[] { SmalltalkOOP.FromSmallInteger(1) });

        Assert.False(_ctx.PrimitiveSuccess, "Increment past MAX_SI should fail");
    }

    [Fact]
    public void SI_MULTIPLY_BOUNDARY_CASES()
    {
        // Test various multiply boundary cases

        // 0 * MAX_SI = 0
        var result1 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(0),
            new[] { SmalltalkOOP.FromSmallInteger(MAX_SI) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(0, result1.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // 1 * MAX_SI = MAX_SI
        var result2 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(1),
            new[] { SmalltalkOOP.FromSmallInteger(MAX_SI) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(MAX_SI, result2.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // -1 * MAX_SI = -MAX_SI (within range)
        var result3 = ArithmeticPrimitives.Multiply(_ctx,
            SmalltalkOOP.FromSmallInteger(-1),
            new[] { SmalltalkOOP.FromSmallInteger(MAX_SI) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-MAX_SI, result3.AsSmallInteger());
    }

    #endregion

    #region Division Edge Cases

    [Fact]
    public void SI_DIVISION_NEGATIVE_OPERANDS()
    {
        // Test division with various sign combinations

        // -20 / -4 = 5 (exact)
        var result1 = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(-20),
            new[] { SmalltalkOOP.FromSmallInteger(-4) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(5, result1.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // -20 / 4 = -5 (exact)
        var result2 = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(-20),
            new[] { SmalltalkOOP.FromSmallInteger(4) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-5, result2.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // 20 / -4 = -5 (exact)
        var result3 = ArithmeticPrimitives.Divide(_ctx,
            SmalltalkOOP.FromSmallInteger(20),
            new[] { SmalltalkOOP.FromSmallInteger(-4) });
        Assert.True(_ctx.PrimitiveSuccess);
        Assert.Equal(-5, result3.AsSmallInteger());
    }

    [Fact]
    public void SI_QUO_TRUNCATION_TOWARD_ZERO()
    {
        // ID: SI_CAST_FLOAT_TO_INT_TRUNCATION (adapted for quo)
        // quo: truncates toward zero

        // 7 quo: 3 = 2
        var result1 = ArithmeticPrimitives.Quo(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });
        Assert.Equal(2, result1.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // -7 quo: 3 = -2 (truncate toward zero, not floor)
        var result2 = ArithmeticPrimitives.Quo(_ctx,
            SmalltalkOOP.FromSmallInteger(-7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });
        Assert.Equal(-2, result2.AsSmallInteger());
    }

    [Fact]
    public void SI_DIV_FLOOR_TOWARD_NEG_INFINITY()
    {
        // div: (floor division) rounds toward negative infinity

        // 7 div: 3 = 2
        var result1 = ArithmeticPrimitives.Div(_ctx,
            SmalltalkOOP.FromSmallInteger(7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });
        Assert.Equal(2, result1.AsSmallInteger());

        _ctx.PrimitiveSuccess = true;

        // -7 div: 3 = -3 (floor toward -∞)
        var result2 = ArithmeticPrimitives.Div(_ctx,
            SmalltalkOOP.FromSmallInteger(-7),
            new[] { SmalltalkOOP.FromSmallInteger(3) });
        Assert.Equal(-3, result2.AsSmallInteger());
    }

    #endregion

    #region Comprehensive Boundary Sweeps

    [Fact]
    public void SI_ADD_NEAR_MAX_BOUNDARY_SWEEP()
    {
        // Test adding small values near MAX_SI boundary
        for (int i = -5; i <= 5; i++)
        {
            _ctx.PrimitiveSuccess = true;
            var result = ArithmeticPrimitives.Add(_ctx,
                SmalltalkOOP.FromSmallInteger(MAX_SI - 10),
                new[] { SmalltalkOOP.FromSmallInteger(i) });

            int expected = MAX_SI - 10 + i;
            bool shouldSucceed = (expected >= MIN_SI && expected <= MAX_SI);

            Assert.Equal(shouldSucceed, _ctx.PrimitiveSuccess);
            if (shouldSucceed)
            {
                Assert.Equal(expected, result.AsSmallInteger());
            }
        }
    }

    [Fact]
    public void SI_SUBTRACT_NEAR_MIN_BOUNDARY_SWEEP()
    {
        // Test subtracting small values near MIN_SI boundary
        for (int i = -5; i <= 5; i++)
        {
            _ctx.PrimitiveSuccess = true;
            var result = ArithmeticPrimitives.Subtract(_ctx,
                SmalltalkOOP.FromSmallInteger(MIN_SI + 10),
                new[] { SmalltalkOOP.FromSmallInteger(i) });

            long expected = (long)MIN_SI + 10 - i;
            bool shouldSucceed = (expected >= MIN_SI && expected <= MAX_SI);

            Assert.Equal(shouldSucceed, _ctx.PrimitiveSuccess);
            if (shouldSucceed)
            {
                Assert.Equal((int)expected, result.AsSmallInteger());
            }
        }
    }

    #endregion
}
