using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Comprehensive unit tests for the Tags class, testing dual-bit tagging for SmallInteger and Character.
/// </summary>
public class TestTags
{
    #region SmallInteger Tagging Tests

    [Fact]
    public void TestSmallIntegerTagging()
    {
        // Test basic tagging
        var tagged = Tags.FromSmallInteger(42);
        Assert.NotNull(tagged);
        Assert.IsType<long>(tagged);
        
        // Verify tag pattern (should end in 01)
        var bits = (long)tagged;
        Assert.Equal(1, bits & 0x3); // Should have tag 01
    }

    [Fact]
    public void TestSmallIntegerUntagging()
    {
        var value = 12345L;
        var tagged = Tags.FromSmallInteger(value);
        var untagged = Tags.ToSmallInteger(tagged);
        Assert.Equal(value, untagged);
    }

    [Fact]
    public void TestSmallIntegerRoundtrip()
    {
        long[] testValues = [0, 1, -1, 42, -42, 1000, -1000, int.MaxValue, int.MinValue];
        
        foreach (var value in testValues)
        {
            var tagged = Tags.FromSmallInteger(value);
            var untagged = Tags.ToSmallInteger(tagged);
            Assert.Equal(value, untagged);
        }
    }

    [Fact]
    public void TestIsSmallInteger()
    {
        var tagged = Tags.FromSmallInteger(42);
        Assert.True(Tags.IsSmallInteger(tagged));
        
        var charTagged = Tags.FromCharacter('A');
        Assert.False(Tags.IsSmallInteger(charTagged));
        
        var obj = new object();
        Assert.False(Tags.IsSmallInteger(obj));
    }

    [Fact]
    public void TestTryUnboxSmallInteger()
    {
        var tagged = Tags.FromSmallInteger(123);
        
        Assert.True(Tags.TryUnboxSmallInteger(tagged, out var value));
        Assert.Equal(123, value);
        
        var charTagged = Tags.FromCharacter('X');
        Assert.False(Tags.TryUnboxSmallInteger(charTagged, out _));
        
        var obj = new object();
        Assert.False(Tags.TryUnboxSmallInteger(obj, out _));
    }

    [Fact]
    public void TestSmallIntegerBounds()
    {
        // Test minimum value
        var minTagged = Tags.FromSmallInteger(Tags.MinSmallInteger);
        Assert.Equal(Tags.MinSmallInteger, Tags.ToSmallInteger(minTagged));
        
        // Test maximum value
        var maxTagged = Tags.FromSmallInteger(Tags.MaxSmallInteger);
        Assert.Equal(Tags.MaxSmallInteger, Tags.ToSmallInteger(maxTagged));
    }

    [Fact]
    public void TestTagIntWithRangeChecking()
    {
        // Valid range
        var tagged = Tags.TagInt(100);
        Assert.Equal(100, Tags.ToSmallInteger(tagged));
        
        // Out of range - should throw
        Assert.Throws<OverflowException>(() => Tags.TagInt(long.MaxValue));
        Assert.Throws<OverflowException>(() => Tags.TagInt(long.MinValue));
    }

    [Fact]
    public void TestUntagIntWithValidation()
    {
        var tagged = Tags.FromSmallInteger(42);
        Assert.Equal(42, Tags.UntagInt(tagged));
        
        // Not a SmallInteger - should throw
        var charTagged = Tags.FromCharacter('A');
        Assert.Throws<ArgumentException>(() => Tags.UntagInt(charTagged));
        
        var obj = new object();
        Assert.Throws<ArgumentException>(() => Tags.UntagInt(obj));
    }

    #endregion

    #region Character Tagging Tests

    [Fact]
    public void TestCharacterTagging()
    {
        // Test basic tagging
        var tagged = Tags.FromCharacter('A');
        Assert.NotNull(tagged);
        Assert.IsType<long>(tagged);
        
        // Verify tag pattern (should end in 10)
        var bits = (long)tagged;
        Assert.Equal(2, bits & 0x3); // Should have tag 10
    }

    [Fact]
    public void TestCharacterUntagging()
    {
        var c = 'Z';
        var tagged = Tags.FromCharacter(c);
        var untagged = Tags.ToCharacter(tagged);
        Assert.Equal((int)c, untagged);
        
        var asChar = Tags.ToChar(tagged);
        Assert.Equal(c, asChar);
    }

    [Fact]
    public void TestCharacterRoundtrip()
    {
        char[] testChars = ['a', 'Z', '0', '9', ' ', '\n', '\t', '€', '中'];
        
        foreach (var c in testChars)
        {
            var tagged = Tags.FromCharacter(c);
            var untagged = Tags.ToChar(tagged);
            Assert.Equal(c, untagged);
        }
    }

    [Fact]
    public void TestIsCharacter()
    {
        var tagged = Tags.FromCharacter('X');
        Assert.True(Tags.IsCharacter(tagged));
        
        var intTagged = Tags.FromSmallInteger(42);
        Assert.False(Tags.IsCharacter(intTagged));
        
        var obj = new object();
        Assert.False(Tags.IsCharacter(obj));
    }

    [Fact]
    public void TestTryUnboxCharacter()
    {
        var tagged = Tags.FromCharacter('Q');
        
        Assert.True(Tags.TryUnboxCharacter(tagged, out var value));
        Assert.Equal((int)'Q', value);
        
        var intTagged = Tags.FromSmallInteger(42);
        Assert.False(Tags.TryUnboxCharacter(intTagged, out _));
        
        var obj = new object();
        Assert.False(Tags.TryUnboxCharacter(obj, out _));
    }

    [Fact]
    public void TestTagCharWithRangeChecking()
    {
        // Valid character
        var tagged = Tags.TagChar('A');
        Assert.Equal('A', Tags.ToChar(tagged));
        
        // Valid character code
        var taggedCode = Tags.TagChar(65);
        Assert.Equal(65, Tags.ToCharacter(taggedCode));
        
        // Negative value - should throw
        Assert.Throws<OverflowException>(() => Tags.TagChar(-1));
        
        // Too large value - should throw
        Assert.Throws<OverflowException>(() => Tags.TagChar(int.MaxValue));
    }

    [Fact]
    public void TestUntagCharWithValidation()
    {
        var tagged = Tags.FromCharacter('B');
        Assert.Equal((int)'B', Tags.UntagChar(tagged));
        
        // Not a Character - should throw
        var intTagged = Tags.FromSmallInteger(42);
        Assert.Throws<ArgumentException>(() => Tags.UntagChar(intTagged));
        
        var obj = new object();
        Assert.Throws<ArgumentException>(() => Tags.UntagChar(obj));
    }

    #endregion

    #region General Operations Tests

    [Fact]
    public void TestIsImmediate()
    {
        var intTagged = Tags.FromSmallInteger(42);
        Assert.True(Tags.IsImmediate(intTagged));
        
        var charTagged = Tags.FromCharacter('A');
        Assert.True(Tags.IsImmediate(charTagged));
        
        var obj = new object();
        Assert.False(Tags.IsImmediate(obj));
        
        // Test edge case: tagged value of 0 (object pointer)
        var zero = 0L;
        Assert.False(Tags.IsImmediate(zero));
    }

    [Fact]
    public void TestIsObject()
    {
        var obj = new object();
        Assert.True(Tags.IsObject(obj));
        
        var intTagged = Tags.FromSmallInteger(42);
        Assert.False(Tags.IsObject(intTagged));
        
        var charTagged = Tags.FromCharacter('A');
        Assert.False(Tags.IsObject(charTagged));
        
        // Test edge case: tagged value of 0 (object pointer)
        var zero = 0L;
        Assert.True(Tags.IsObject(zero));
    }

    [Fact]
    public void TestAreIdentical()
    {
        // Same SmallInteger
        var int1 = Tags.FromSmallInteger(42);
        var int2 = Tags.FromSmallInteger(42);
        Assert.True(Tags.AreIdentical(int1, int2));
        
        // Different SmallIntegers
        var int3 = Tags.FromSmallInteger(43);
        Assert.False(Tags.AreIdentical(int1, int3));
        
        // Same Character
        var char1 = Tags.FromCharacter('A');
        var char2 = Tags.FromCharacter('A');
        Assert.True(Tags.AreIdentical(char1, char2));
        
        // Different Characters
        var char3 = Tags.FromCharacter('B');
        Assert.False(Tags.AreIdentical(char1, char3));
        
        // Same object reference
        var obj = new object();
        Assert.True(Tags.AreIdentical(obj, obj));
        
        // Different objects
        var obj2 = new object();
        Assert.False(Tags.AreIdentical(obj, obj2));
        
        // Mixed types
        Assert.False(Tags.AreIdentical(int1, char1));
    }

    #endregion

    #region SmallInteger Arithmetic Tests

    [Fact]
    public void TestAddSmallIntegers()
    {
        var a = Tags.FromSmallInteger(10);
        var b = Tags.FromSmallInteger(20);
        var result = Tags.AddSmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(30, Tags.ToSmallInteger(result));
    }

    [Fact]
    public void TestAddSmallIntegersOverflow()
    {
        var a = Tags.FromSmallInteger(Tags.MaxSmallInteger);
        var b = Tags.FromSmallInteger(1);
        var result = Tags.AddSmallIntegers(a, b);
        
        Assert.Null(result); // Should return null on overflow
    }

    [Fact]
    public void TestSubtractSmallIntegers()
    {
        var a = Tags.FromSmallInteger(30);
        var b = Tags.FromSmallInteger(10);
        var result = Tags.SubtractSmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(20, Tags.ToSmallInteger(result));
    }

    [Fact]
    public void TestSubtractSmallIntegersOverflow()
    {
        var a = Tags.FromSmallInteger(Tags.MinSmallInteger);
        var b = Tags.FromSmallInteger(1);
        var result = Tags.SubtractSmallIntegers(a, b);
        
        Assert.Null(result); // Should return null on overflow
    }

    [Fact]
    public void TestMultiplySmallIntegers()
    {
        var a = Tags.FromSmallInteger(7);
        var b = Tags.FromSmallInteger(6);
        var result = Tags.MultiplySmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(42, Tags.ToSmallInteger(result));
    }

    [Fact]
    public void TestMultiplySmallIntegersOverflow()
    {
        var a = Tags.FromSmallInteger(Tags.MaxSmallInteger / 2);
        var b = Tags.FromSmallInteger(3);
        var result = Tags.MultiplySmallIntegers(a, b);
        
        Assert.Null(result); // Should return null on overflow
    }

    [Fact]
    public void TestDivideSmallIntegers()
    {
        // Exact division
        var a = Tags.FromSmallInteger(42);
        var b = Tags.FromSmallInteger(6);
        var result = Tags.DivideSmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(7, Tags.ToSmallInteger(result));
        
        // Non-exact division
        var c = Tags.FromSmallInteger(43);
        var d = Tags.FromSmallInteger(6);
        var result2 = Tags.DivideSmallIntegers(c, d);
        
        Assert.Null(result2); // Should return null for non-integer result
    }

    [Fact]
    public void TestDivideByZero()
    {
        var a = Tags.FromSmallInteger(42);
        var b = Tags.FromSmallInteger(0);
        var result = Tags.DivideSmallIntegers(a, b);
        
        Assert.Null(result); // Should return null for division by zero
    }

    [Fact]
    public void TestQuoSmallIntegers()
    {
        // Integer division (quotient)
        var a = Tags.FromSmallInteger(43);
        var b = Tags.FromSmallInteger(6);
        var result = Tags.QuoSmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(7, Tags.ToSmallInteger(result)); // 43 / 6 = 7 (quotient)
    }

    [Fact]
    public void TestModSmallIntegers()
    {
        var a = Tags.FromSmallInteger(43);
        var b = Tags.FromSmallInteger(6);
        var result = Tags.ModSmallIntegers(a, b);
        
        Assert.NotNull(result);
        Assert.Equal(1, Tags.ToSmallInteger(result)); // 43 % 6 = 1
        
        // Test negative modulo (Smalltalk semantics)
        var c = Tags.FromSmallInteger(-5);
        var d = Tags.FromSmallInteger(3);
        var result2 = Tags.ModSmallIntegers(c, d);
        
        Assert.NotNull(result2);
        Assert.Equal(1, Tags.ToSmallInteger(result2)); // Should return positive result
    }

    [Fact]
    public void TestBitwiseOperations()
    {
        var a = Tags.FromSmallInteger(0b1010);
        var b = Tags.FromSmallInteger(0b1100);
        
        // AND
        var andResult = Tags.BitAndSmallIntegers(a, b);
        Assert.NotNull(andResult);
        Assert.Equal(0b1000, Tags.ToSmallInteger(andResult));
        
        // OR
        var orResult = Tags.BitOrSmallIntegers(a, b);
        Assert.NotNull(orResult);
        Assert.Equal(0b1110, Tags.ToSmallInteger(orResult));
        
        // XOR
        var xorResult = Tags.BitXorSmallIntegers(a, b);
        Assert.NotNull(xorResult);
        Assert.Equal(0b0110, Tags.ToSmallInteger(xorResult));
    }

    [Fact]
    public void TestBitShift()
    {
        var value = Tags.FromSmallInteger(8);
        
        // Left shift
        var leftShift = Tags.FromSmallInteger(2);
        var leftResult = Tags.BitShiftSmallIntegers(value, leftShift);
        Assert.NotNull(leftResult);
        Assert.Equal(32, Tags.ToSmallInteger(leftResult)); // 8 << 2 = 32
        
        // Right shift
        var rightShift = Tags.FromSmallInteger(-2);
        var rightResult = Tags.BitShiftSmallIntegers(value, rightShift);
        Assert.NotNull(rightResult);
        Assert.Equal(2, Tags.ToSmallInteger(rightResult)); // 8 >> 2 = 2
        
        // Overflow on left shift
        var bigShift = Tags.FromSmallInteger(63);
        var overflowResult = Tags.BitShiftSmallIntegers(value, bigShift);
        Assert.Null(overflowResult); // Should return null on overflow
    }

    #endregion

    #region SmallInteger Comparison Tests

    [Fact]
    public void TestCompareSmallIntegers()
    {
        var a = Tags.FromSmallInteger(10);
        var b = Tags.FromSmallInteger(20);
        var c = Tags.FromSmallInteger(10);
        
        Assert.Equal(-1, Tags.CompareSmallIntegers(a, b)); // a < b
        Assert.Equal(1, Tags.CompareSmallIntegers(b, a));  // b > a
        Assert.Equal(0, Tags.CompareSmallIntegers(a, c));  // a == c
    }

    [Fact]
    public void TestSmallIntegerComparisonOperators()
    {
        var a = Tags.FromSmallInteger(10);
        var b = Tags.FromSmallInteger(20);
        var c = Tags.FromSmallInteger(10);
        
        // Less than
        Assert.True(Tags.LessThanSmallIntegers(a, b));
        Assert.False(Tags.LessThanSmallIntegers(b, a));
        Assert.False(Tags.LessThanSmallIntegers(a, c));
        
        // Less than or equal
        Assert.True(Tags.LessOrEqualSmallIntegers(a, b));
        Assert.False(Tags.LessOrEqualSmallIntegers(b, a));
        Assert.True(Tags.LessOrEqualSmallIntegers(a, c));
        
        // Greater than
        Assert.False(Tags.GreaterThanSmallIntegers(a, b));
        Assert.True(Tags.GreaterThanSmallIntegers(b, a));
        Assert.False(Tags.GreaterThanSmallIntegers(a, c));
        
        // Greater than or equal
        Assert.False(Tags.GreaterOrEqualSmallIntegers(a, b));
        Assert.True(Tags.GreaterOrEqualSmallIntegers(b, a));
        Assert.True(Tags.GreaterOrEqualSmallIntegers(a, c));
        
        // Equal
        Assert.False(Tags.EqualSmallIntegers(a, b));
        Assert.True(Tags.EqualSmallIntegers(a, c));
        
        // Not equal
        Assert.True(Tags.NotEqualSmallIntegers(a, b));
        Assert.False(Tags.NotEqualSmallIntegers(a, c));
    }

    [Fact]
    public void TestSmallIntegerComparisonWithNonIntegers()
    {
        var intTagged = Tags.FromSmallInteger(42);
        var charTagged = Tags.FromCharacter('A');
        
        Assert.Null(Tags.LessThanSmallIntegers(intTagged, charTagged));
        Assert.Null(Tags.EqualSmallIntegers(intTagged, charTagged));
    }

    #endregion

    #region Character Comparison Tests

    [Fact]
    public void TestCompareCharacters()
    {
        var a = Tags.FromCharacter('A');
        var b = Tags.FromCharacter('Z');
        var c = Tags.FromCharacter('A');
        
        Assert.Equal(-1, Tags.CompareCharacters(a, b)); // 'A' < 'Z'
        Assert.Equal(1, Tags.CompareCharacters(b, a));  // 'Z' > 'A'
        Assert.Equal(0, Tags.CompareCharacters(a, c));  // 'A' == 'A'
    }

    [Fact]
    public void TestCharacterEquality()
    {
        var a = Tags.FromCharacter('X');
        var b = Tags.FromCharacter('Y');
        var c = Tags.FromCharacter('X');
        
        Assert.False(Tags.EqualCharacters(a, b));
        Assert.True(Tags.EqualCharacters(a, c));
    }

    [Fact]
    public void TestCharacterComparisonWithNonCharacters()
    {
        var charTagged = Tags.FromCharacter('A');
        var intTagged = Tags.FromSmallInteger(65); // ASCII value of 'A'
        
        Assert.Null(Tags.EqualCharacters(charTagged, intTagged));
    }

    #endregion

    #region Edge Cases and Stress Tests

    [Fact]
    public void TestZeroValues()
    {
        // SmallInteger zero
        var intZero = Tags.FromSmallInteger(0);
        Assert.True(Tags.IsSmallInteger(intZero));
        Assert.Equal(0, Tags.ToSmallInteger(intZero));
        
        // Character with code 0 (null character)
        var charZero = Tags.FromCharacter(0);
        Assert.True(Tags.IsCharacter(charZero));
        Assert.Equal(0, Tags.ToCharacter(charZero));
        
        // They should be different
        Assert.False(Tags.AreIdentical(intZero, charZero));
    }

    [Fact]
    public void TestNegativeValues()
    {
        // Negative SmallIntegers
        long[] negatives = [-1, -42, -1000, int.MinValue];
        
        foreach (var value in negatives)
        {
            var tagged = Tags.FromSmallInteger(value);
            Assert.True(Tags.IsSmallInteger(tagged));
            Assert.Equal(value, Tags.ToSmallInteger(tagged));
        }
    }

    [Fact]
    public void TestLargeValues()
    {
        // Test near boundary values
        var nearMax = Tags.MaxSmallInteger - 1;
        var nearMin = Tags.MinSmallInteger + 1;
        
        var maxTagged = Tags.FromSmallInteger(nearMax);
        Assert.Equal(nearMax, Tags.ToSmallInteger(maxTagged));
        
        var minTagged = Tags.FromSmallInteger(nearMin);
        Assert.Equal(nearMin, Tags.ToSmallInteger(minTagged));
    }

    [Fact]
    public void TestUnicodeCharacters()
    {
        // Test various Unicode characters
        char[] unicodeChars = ['中', '文', '♠', '♥', '♦', '♣', 'Ω', 'π'];
        
        foreach (var c in unicodeChars)
        {
            var tagged = Tags.FromCharacter(c);
            Assert.True(Tags.IsCharacter(tagged));
            var untagged = Tags.ToChar(tagged);
            Assert.Equal(c, untagged);
        }
    }

    [Fact]
    public void TestMixedTypeArithmetic()
    {
        var intTagged = Tags.FromSmallInteger(42);
        var charTagged = Tags.FromCharacter('A');
        var obj = new object();
        
        // Arithmetic should return null for non-SmallIntegers
        Assert.Null(Tags.AddSmallIntegers(intTagged, charTagged));
        Assert.Null(Tags.AddSmallIntegers(intTagged, obj));
        Assert.Null(Tags.SubtractSmallIntegers(charTagged, intTagged));
        Assert.Null(Tags.MultiplySmallIntegers(obj, intTagged));
    }

    [Fact]
    public void TestPerformance()
    {
        // This test ensures operations are actually inlined and fast
        // In a real performance test, you'd use benchmarking tools
        
        const int iterations = 1_000_000;
        var sum = 0L;
        
        for (int i = 0; i < iterations; i++)
        {
            var tagged = Tags.FromSmallInteger(i);
            if (Tags.TryUnboxSmallInteger(tagged, out var value))
            {
                sum += value;
            }
        }
        
        // Just verify it completed correctly
        Assert.Equal((long)iterations * (iterations - 1) / 2, sum);
    }

    #endregion
}