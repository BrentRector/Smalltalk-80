using System;

class Test {
    static void Main() {
        // SmallInteger range is -2^30 to 2^30-1
        int maxSmallInt = 1073741823;  // 2^30 - 1
        int minSmallInt = -1073741824; // -2^30
        
        // Test overflow for add
        nint maxTagged = ((nint)maxSmallInt << 1) | 1;
        nint oneTagged = ((nint)1 << 1) | 1;
        
        // Add tagged values
        nint result = maxTagged + oneTagged - 1; // Subtract one tag
        
        // Extract value
        int resultValue = (int)(result >> 1);
        
        Console.WriteLine($"Max SmallInt: {maxSmallInt}");
        Console.WriteLine($"Max tagged: 0x{maxTagged:X16} = {maxTagged}");
        Console.WriteLine($"One tagged: 0x{oneTagged:X16} = {oneTagged}");
        Console.WriteLine($"Result: 0x{result:X16} = {result}");
        Console.WriteLine($"Result value: {resultValue}");
        Console.WriteLine($"Should overflow: {resultValue != maxSmallInt + 1}");
        
        // Check overflow detection
        bool overflow = ((maxTagged ^ result) & (oneTagged ^ result)) < 0;
        Console.WriteLine($"Overflow detected: {overflow}");
    }
}
