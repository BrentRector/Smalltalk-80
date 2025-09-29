// Simple standalone test of primitive implementations
// Compile with: csc TestPrimitivesSimple.cs /r:System.Runtime.dll

using System;
using System.Runtime.CompilerServices;

// Minimal Tags implementation for testing
public static class Tags
{
    private const long SmallIntTag = 0x1;
    private const long CharacterTag = 0x2;
    private const long TagMask = 0x3;
    private const int TagShift = 2;
    
    public const long MinSmallInteger = long.MinValue >> TagShift;
    public const long MaxSmallInteger = long.MaxValue >> TagShift;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TagInt(long value) => (value << TagShift) | SmallIntTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UntagInt(object tagged) => ((long)tagged) >> TagShift;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmallInteger(object v) => v is long lv && (lv & TagMask) == SmallIntTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnboxSmallInteger(object v, out long value)
    {
        if (v is long lv && (lv & TagMask) == SmallIntTag)
        {
            value = lv >> TagShift;
            return true;
        }
        value = 0;
        return false;
    }
}

public class SimplePrimitiveTests
{
    public static void Main()
    {
        Console.WriteLine("Testing SmallInteger Primitives");
        Console.WriteLine("================================\n");
        
        TestAddition();
        TestSubtraction();
        TestMultiplication();
        TestComparison();
        TestBitOperations();
        
        Console.WriteLine("\nAll tests completed!");
    }
    
    static void TestAddition()
    {
        Console.Write("Testing addition (5 + 3)... ");
        
        var left = Tags.TagInt(5);
        var right = Tags.TagInt(3);
        
        if (Tags.TryUnboxSmallInteger(left, out var a) && 
            Tags.TryUnboxSmallInteger(right, out var b))
        {
            var result = a + b;
            if (result >= Tags.MinSmallInteger && result <= Tags.MaxSmallInteger)
            {
                var tagged = Tags.TagInt(result);
                var untagged = Tags.UntagInt(tagged);
                if (untagged == 8)
                {
                    Console.WriteLine("PASS");
                }
                else
                {
                    Console.WriteLine($"FAIL: Expected 8, got {untagged}");
                }
            }
            else
            {
                Console.WriteLine("FAIL: Overflow");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
    }
    
    static void TestSubtraction()
    {
        Console.Write("Testing subtraction (10 - 3)... ");
        
        var left = Tags.TagInt(10);
        var right = Tags.TagInt(3);
        
        if (Tags.TryUnboxSmallInteger(left, out var a) && 
            Tags.TryUnboxSmallInteger(right, out var b))
        {
            var result = a - b;
            var tagged = Tags.TagInt(result);
            var untagged = Tags.UntagInt(tagged);
            if (untagged == 7)
            {
                Console.WriteLine("PASS");
            }
            else
            {
                Console.WriteLine($"FAIL: Expected 7, got {untagged}");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
    }
    
    static void TestMultiplication()
    {
        Console.Write("Testing multiplication (7 * 6)... ");
        
        var left = Tags.TagInt(7);
        var right = Tags.TagInt(6);
        
        if (Tags.TryUnboxSmallInteger(left, out var a) && 
            Tags.TryUnboxSmallInteger(right, out var b))
        {
            var result = a * b;
            var tagged = Tags.TagInt(result);
            var untagged = Tags.UntagInt(tagged);
            if (untagged == 42)
            {
                Console.WriteLine("PASS");
            }
            else
            {
                Console.WriteLine($"FAIL: Expected 42, got {untagged}");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
    }
    
    static void TestComparison()
    {
        Console.Write("Testing comparison (5 < 10)... ");
        
        var left = Tags.TagInt(5);
        var right = Tags.TagInt(10);
        
        if (Tags.IsSmallInteger(left) && Tags.IsSmallInteger(right))
        {
            // Can compare tagged values directly since they preserve ordering
            bool lessThan = (long)left < (long)right;
            if (lessThan)
            {
                Console.WriteLine("PASS");
            }
            else
            {
                Console.WriteLine("FAIL: Expected true");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
    }
    
    static void TestBitOperations()
    {
        Console.Write("Testing bit AND (0b1101 & 0b1010)... ");
        
        var left = Tags.TagInt(0b1101);
        var right = Tags.TagInt(0b1010);
        
        if (Tags.TryUnboxSmallInteger(left, out var a) && 
            Tags.TryUnboxSmallInteger(right, out var b))
        {
            var result = a & b;
            var tagged = Tags.TagInt(result);
            var untagged = Tags.UntagInt(tagged);
            if (untagged == 0b1000)
            {
                Console.WriteLine("PASS");
            }
            else
            {
                Console.WriteLine($"FAIL: Expected {0b1000}, got {untagged}");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
        
        Console.Write("Testing bit shift (5 << 2)... ");
        
        left = Tags.TagInt(5);
        var shift = Tags.TagInt(2);
        
        if (Tags.TryUnboxSmallInteger(left, out var v) && 
            Tags.TryUnboxSmallInteger(shift, out var s))
        {
            if (s >= 0 && s < 63)
            {
                var result = v << (int)s;
                if (result >= Tags.MinSmallInteger && result <= Tags.MaxSmallInteger)
                {
                    var tagged = Tags.TagInt(result);
                    var untagged = Tags.UntagInt(tagged);
                    if (untagged == 20)
                    {
                        Console.WriteLine("PASS");
                    }
                    else
                    {
                        Console.WriteLine($"FAIL: Expected 20, got {untagged}");
                    }
                }
                else
                {
                    Console.WriteLine("FAIL: Overflow");
                }
            }
            else
            {
                Console.WriteLine("FAIL: Invalid shift amount");
            }
        }
        else
        {
            Console.WriteLine("FAIL: Not SmallIntegers");
        }
    }
}