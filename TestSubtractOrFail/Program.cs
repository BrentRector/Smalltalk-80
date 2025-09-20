using System;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Primitives;
using Smalltalk80.Interpreter.VM;

class TestSubtractOrFail
{
    // SmallIntegers in Smalltalk-80 use 30 bits for value (one bit is the tag)
    const int MinSmallInteger = -1073741824;  // -2^30
    const int MaxSmallInteger = 1073741823;   // 2^30 - 1
    
    static void Main()
    {
        Console.WriteLine("Testing subtractOrFail: primitive behavior");
        Console.WriteLine("SmallInteger range: {0} to {1}", MinSmallInteger, MaxSmallInteger);
        Console.WriteLine();
        
        // Test normal subtraction
        Console.WriteLine("Test 1: Normal subtraction (5 - 3)");
        var result1 = TestSubtraction(5, 3);
        Console.WriteLine($"  Result: {(result1.Success ? result1.Value.SmallIntegerValue.ToString() : "Failed - " + result1.FailureReason)}");
        Console.WriteLine();
        
        // Test subtraction at minimum boundary
        int minValue = MinSmallInteger;
        Console.WriteLine($"Test 2: At minimum boundary ({minValue} - 0)");
        var result2 = TestSubtraction(minValue, 0);
        Console.WriteLine($"  Result: {(result2.Success ? result2.Value.SmallIntegerValue.ToString() : "Failed - " + result2.FailureReason)}");
        Console.WriteLine();
        
        // Test underflow
        Console.WriteLine($"Test 3: Underflow ({minValue} - 1)");
        var result3 = TestSubtraction(minValue, 1);
        Console.WriteLine($"  Result: {(result3.Success ? result3.Value.SmallIntegerValue.ToString() : "Failed - " + result3.FailureReason)}");
        Console.WriteLine($"  Expected: Failure (should return nil in Smalltalk)");
        Console.WriteLine();
        
        // Test the SmallInteger initialize pattern
        Console.WriteLine("Test 4: SmallInteger initialize pattern (finding max value)");
        int tester = 1;
        int iterations = 0;
        const int maxIterations = 100; // Prevent infinite loop in test
        
        while (iterations < maxIterations)
        {
            iterations++;
            
            // Try to double tester
            long nextLong = (long)tester * 2;
            
            // Check if doubling would exceed SmallInteger range
            if (nextLong > MaxSmallInteger)
            {
                Console.WriteLine($"  Overflow detected at iteration {iterations}");
                Console.WriteLine($"  tester = {tester}");
                Console.WriteLine($"  tester * 2 would be {nextLong} (exceeds max {MaxSmallInteger})");
                break;
            }
            
            tester = (int)nextLong;
            
            if (iterations % 5 == 0)
            {
                Console.WriteLine($"  Iteration {iterations}: tester = {tester}");
            }
        }
        
        Console.WriteLine($"  Final tester value: {tester}");
        Console.WriteLine($"  MaxVal should be: {tester - 1}");
        
        // Test subtracting 1 from the max value
        var result4 = TestSubtraction(tester, 1);
        Console.WriteLine($"  tester - 1 = {(result4.Success ? result4.Value.SmallIntegerValue.ToString() : "Failed")}");
        
        // Now test the actual subtractOrFail behavior near the limits
        Console.WriteLine();
        Console.WriteLine("Test 5: Testing subtractOrFail near overflow");
        int testVal = tester;
        for (int i = 0; i < 5; i++)
        {
            var result = TestSubtraction(testVal, -1); // Subtracting -1 is adding 1
            Console.WriteLine($"  {testVal} - (-1) = {(result.Success ? result.Value.SmallIntegerValue.ToString() : "Failed (overflow)")}");
            if (!result.Success) break;
            testVal = result.Value.SmallIntegerValue;
        }
    }
    
    static PrimitiveResult TestSubtraction(int a, int b)
    {
        var receiver = OOP.FromSmallInteger(a);
        var args = new[] { OOP.FromSmallInteger(b) };
        
        // Create a dummy VM for the context
        var vm = new VirtualMachine();
        return ArithmeticPrimitives.PrimitiveSubtract(receiver, args, vm);
    }
}