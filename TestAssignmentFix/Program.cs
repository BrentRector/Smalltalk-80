using System;
using System.Linq;
using StandaloneCompiler;
using System.Collections.Generic;

/// <summary>
/// Direct test of the assignment bug fix for Point>>setX:setY: method.
/// </summary>
class TestAssignmentFix
{
    static void Main()
    {
        Console.WriteLine("Testing Point>>setX:setY: bytecode generation fix\n");
        Console.WriteLine("=====================================\n");
        
        // The problematic method source
        string methodSource = @"setX: xPoint setY: yPoint 
    x _ xPoint.
    y _ yPoint";
        
        Console.WriteLine("Method source:");
        Console.WriteLine(methodSource);
        Console.WriteLine("\n=====================================\n");
        
        // Instance variables for Point class
        var instanceVars = new List<string> { "x", "y" };
        
        // Compile the method
        var compiler = new BytecodeCompiler();
        var compiledMethod = compiler.CompileMethod(methodSource, instanceVars);
        
        // Display results
        Console.WriteLine("Compilation Results:");
        Console.WriteLine($"  Selector: {compiledMethod.Selector}");
        Console.WriteLine($"  Arguments: {compiledMethod.ArgumentCount}");
        Console.WriteLine($"  Temporaries: {compiledMethod.TemporaryCount}");
        Console.WriteLine($"  Bytecode: [{string.Join(", ", compiledMethod.Bytecode)}]");
        Console.WriteLine($"  Literals: [{string.Join(", ", compiledMethod.Literals.Select(l => l?.ToString() ?? "null"))}]");
        
        Console.WriteLine("\n=====================================\n");
        
        // Check if the bug is fixed
        var expectedBytecode = new byte[] { 16, 96, 17, 97, 120 };
        bool isFixed = compiledMethod.Bytecode.SequenceEqual(expectedBytecode);
        
        Console.WriteLine("Bug Fix Verification:");
        Console.WriteLine($"  Expected bytecode: [{string.Join(", ", expectedBytecode)}]");
        Console.WriteLine($"  Actual bytecode:   [{string.Join(", ", compiledMethod.Bytecode)}]");
        Console.WriteLine($"\n  Status: {(isFixed ? "✓ BUG IS FIXED!" : "✗ BUG STILL EXISTS")}");
        
        // Detailed bytecode explanation
        Console.WriteLine("\n=====================================\n");
        Console.WriteLine("Bytecode Explanation:");
        Console.WriteLine("  16  - Push temp #0 (xPoint)");
        Console.WriteLine("  96  - Pop and store receiver variable #0 (x)");
        Console.WriteLine("  17  - Push temp #1 (yPoint)");
        if (compiledMethod.Bytecode.Length > 3 && compiledMethod.Bytecode[3] == 136)
        {
            Console.WriteLine("  136 - ** DUPLICATE (BUG!) **");
            Console.WriteLine("  97  - Pop and store receiver variable #1 (y)");
        }
        else
        {
            Console.WriteLine("  97  - Pop and store receiver variable #1 (y)");
        }
        Console.WriteLine("  120 - Return self");
        
        if (!isFixed)
        {
            Console.WriteLine("\n=====================================\n");
            Console.WriteLine("ERROR ANALYSIS:");
            if (compiledMethod.Bytecode.Length > expectedBytecode.Length)
            {
                Console.WriteLine($"  Extra bytecodes detected: {compiledMethod.Bytecode.Length - expectedBytecode.Length} extra byte(s)");
            }
            if (compiledMethod.Bytecode.Contains((byte)136))
            {
                int dupIndex = Array.IndexOf(compiledMethod.Bytecode, (byte)136);
                Console.WriteLine($"  Duplicate bytecode (136) found at position {dupIndex}");
                Console.WriteLine("  This causes the stack to be corrupted!");
            }
        }
        else
        {
            Console.WriteLine("\n=====================================\n");
            Console.WriteLine("SUCCESS!");
            Console.WriteLine("The compiler now correctly generates bytecode for");
            Console.WriteLine("assignment statements without the duplicate bug.");
        }
        
        Environment.Exit(isFixed ? 0 : 1);
    }
}
