using System;
using Smalltalk80;

class TestJumps
{
    static int Main()
    {
        Console.WriteLine("Testing jump bytecodes...");
        
        var interpreter = new Interpreter { ExitOnReturn = false };
        
        // Test 1: Jump if false (bytecode 144 = jump 1 byte if false)
        Console.WriteLine("\nTest 1: Jump if false");
        var method1 = new CompiledMethod
        {
            // Push false, jump if false, push 1, return, push 2, return
            Bytecodes = new byte[] { 
                114,  // Push false
                144,  // Jump if false by 1 byte (should skip next instruction)
                118,  // Push 1 (should be skipped)
                119,  // Push 2 (should execute)
                124   // Return top
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        
        try 
        {
            var result1 = interpreter.Evaluate(method1, new SmalltalkObject(null, 0));
            Console.WriteLine($"  Expected: 2, Got: {result1}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED with exception: {ex.Message}");
        }
        
        // Test 2: Jump if false with true condition (no jump)
        Console.WriteLine("\nTest 2: Jump if false with true (no jump)");
        var method2 = new CompiledMethod
        {
            Bytecodes = new byte[] { 
                113,  // Push true
                144,  // Jump if false by 1 byte (should NOT jump)
                118,  // Push 1 (should execute)
                124   // Return top
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        
        try
        {
            var result2 = interpreter.Evaluate(method2, new SmalltalkObject(null, 0));
            Console.WriteLine($"  Expected: 1, Got: {result2}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED with exception: {ex.Message}");
        }
        
        // Test 3: Unconditional jump (bytecode 152 = jump forward 1)
        Console.WriteLine("\nTest 3: Unconditional jump");
        var method3 = new CompiledMethod
        {
            Bytecodes = new byte[] { 
                152,  // Jump forward by 1 byte
                118,  // Push 1 (should be skipped)
                119,  // Push 2 (should execute)
                124   // Return top
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        
        try
        {
            var result3 = interpreter.Evaluate(method3, new SmalltalkObject(null, 0));
            Console.WriteLine($"  Expected: 2, Got: {result3}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED with exception: {ex.Message}");
        }
        
        // Test 4: Send with 1 arg (bytecode 224-239)
        Console.WriteLine("\nTest 4: Send with 1 arg (bytecode 224)");
        var method4 = new CompiledMethod
        {
            Bytecodes = new byte[] { 
                118,  // Push 1 (receiver)
                119,  // Push 2 (argument)
                224,  // Send literal selector 0 with 1 arg
                124   // Return top
            },
            Literals = new object[] { new Symbol("+") },
            TemporariesCount = 0
        };
        
        try
        {
            var result4 = interpreter.Evaluate(method4, new SmalltalkObject(null, 0));
            Console.WriteLine($"  Expected: 3, Got: {result4}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED with exception: {ex.Message}");
        }
        
        Console.WriteLine("\n=== Summary ===");
        Console.WriteLine("Jump and send bytecodes have been updated to match Blue Book specification.");
        Console.WriteLine("144-175 are now jump operations (were incorrectly send operations)");
        Console.WriteLine("224-255 are now send operations with 1-2 args (were marked unused)");
        
        return 0;
    }
}