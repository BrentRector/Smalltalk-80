# Test if jump bytecodes now work correctly

Write-Host "=== Testing Fixed Jump Bytecodes ===" -ForegroundColor Cyan
Write-Host ""

# Create a simple test program that uses conditional jumps
$testCode = @'
using System;
using Smalltalk80;

class TestJumps
{
    static void Main()
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
        
        var result1 = interpreter.Evaluate(method1, new SmalltalkObject(null, 0));
        Console.WriteLine($"  Expected: 2, Got: {result1}");
        
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
        
        var result2 = interpreter.Evaluate(method2, new SmalltalkObject(null, 0));
        Console.WriteLine($"  Expected: 1, Got: {result2}");
        
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
        
        var result3 = interpreter.Evaluate(method3, new SmalltalkObject(null, 0));
        Console.WriteLine($"  Expected: 2, Got: {result3}");
        
        Console.WriteLine("\n=== Summary ===");
        bool test1Pass = result1?.ToString() == "2";
        bool test2Pass = result2?.ToString() == "1";
        bool test3Pass = result3?.ToString() == "2";
        
        Console.WriteLine($"Test 1 (Jump if false): {(test1Pass ? "PASS" : "FAIL")}");
        Console.WriteLine($"Test 2 (No jump on true): {(test2Pass ? "PASS" : "FAIL")}");
        Console.WriteLine($"Test 3 (Unconditional jump): {(test3Pass ? "PASS" : "FAIL")}");
        
        if (test1Pass && test2Pass && test3Pass)
        {
            Console.WriteLine("\nAll tests PASSED! Jump bytecodes are working correctly!");
            return 0;
        }
        else
        {
            Console.WriteLine("\nSome tests FAILED. Jump bytecodes still have issues.");
            return 1;
        }
    }
}
'@

# Write the test file
$testCode | Out-File -FilePath "TestJumpBytecodes.cs" -Encoding UTF8

# Compile and run the test
Write-Host "Compiling test..." -ForegroundColor Yellow
$compileResult = & csc /reference:bin/Debug/net9.0/Smalltalk80.dll TestJumpBytecodes.cs /out:TestJumpBytecodes.exe 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Running test..." -ForegroundColor Yellow
    & .\TestJumpBytecodes.exe
    $exitCode = $LASTEXITCODE
} else {
    Write-Host "Compilation failed:" -ForegroundColor Red
    Write-Host $compileResult
    $exitCode = 1
}

# Clean up
Remove-Item TestJumpBytecodes.cs -ErrorAction SilentlyContinue
Remove-Item TestJumpBytecodes.exe -ErrorAction SilentlyContinue

exit $exitCode