using System;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Test program for the bytecode generator.
    /// </summary>
    public class TestBytecodeGen
    {
        public static void TestBytecodeGeneration()
        {
            Console.WriteLine("Testing Bytecode Generation");
            Console.WriteLine("===========================\n");
            
            // Test cases with expected bytecode
            var testCases = new[]
            {
                new
                {
                    Name = "Simple accessor",
                    Source = "name ^name!",
                    ExpectedBytecode = new byte[] { 0, 180 }, // PushReceiverVariable0, ReturnTop
                    InstanceVars = new[] { "name" }
                },
                new
                {
                    Name = "Simple setter",
                    Source = "setName: aString name _ aString!",
                    ExpectedBytecode = new byte[] { 16, 96, 176 }, // PushTemporaryVariable0, StoreIntoReceiverVariable0, ReturnReceiver
                    InstanceVars = new[] { "name" }
                },
                new
                {
                    Name = "Return self",
                    Source = "yourself ^self!",
                    ExpectedBytecode = new byte[] { 0, 180 }, // PushReceiver (bytecode 0), ReturnTop
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Return literal",
                    Source = "greeting ^'Hello'!",
                    ExpectedBytecode = new byte[] { 32, 180 }, // PushLiteralConstant0, ReturnTop
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Return true",
                    Source = "isReady ^true!",
                    ExpectedBytecode = new byte[] { 177, 180 }, // Push true, ReturnTop
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Return false",  
                    Source = "isNotReady ^false!",
                    ExpectedBytecode = new byte[] { 178, 180 }, // Push false, ReturnTop
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Return nil",
                    Source = "nothing ^nil!",
                    ExpectedBytecode = new byte[] { 179, 180 }, // Push nil, ReturnTop
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Primitive only method",
                    Source = "basicAt: index <primitive: 60>!",
                    ExpectedBytecode = new byte[] { 176 }, // ReturnReceiver (fallback when primitive fails)
                    InstanceVars = new string[0]
                },
                new
                {
                    Name = "Primitive with fallback",
                    Source = "at: index <primitive: 60> ^self basicAt: index!",
                    ExpectedBytecode = new byte[] { 0, 16, 192, 180 }, // pushReceiver, pushTemp, send, return
                    InstanceVars = new string[0]
                }
            };
            
            var generator = new SimpleBytecodeGenerator();
            int passed = 0;
            int failed = 0;
            
            foreach (var testCase in testCases)
            {
                Console.WriteLine($"Test: {testCase.Name}");
                Console.WriteLine($"Source: {testCase.Source}");
                
                try
                {
                    // Generate bytecode
                    var result = generator.CompileMethod(
                        testCase.Source,
                        testCase.InstanceVars.ToList()
                    );
                    
                    // Compare bytecode
                    bool match = result.Bytecode.SequenceEqual(testCase.ExpectedBytecode);
                    
                    if (match)
                    {
                        Console.WriteLine($"✓ PASSED - Bytecode: [{string.Join(", ", result.Bytecode)}]");
                        passed++;
                    }
                    else
                    {
                        Console.WriteLine($"✗ FAILED");
                        Console.WriteLine($"  Expected: [{string.Join(", ", testCase.ExpectedBytecode)}]");
                        Console.WriteLine($"  Got:      [{string.Join(", ", result.Bytecode)}]");
                        failed++;
                    }
                    
                    // Show literals if any
                    if (result.Literals.Length > 0)
                    {
                        Console.WriteLine($"  Literals: [{string.Join(", ", result.Literals.Select(l => l.ToString()))}]");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ ERROR: {ex.Message}");
                    failed++;
                }
                
                Console.WriteLine();
            }
            
            Console.WriteLine($"\nResults: {passed} passed, {failed} failed");
        }
    }
}