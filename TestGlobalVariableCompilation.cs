using System;
using System.Linq;
using StandaloneCompiler;

namespace TestCompiler
{
    /// <summary>
    /// Test program to verify that the StandaloneCompiler correctly generates
    /// bytecode for global variable references according to the Smalltalk-80 Blue Book.
    /// </summary>
    public class TestGlobalVariableCompilation
    {
        public static void Main()
        {
            Console.WriteLine("Testing Global Variable Compilation");
            Console.WriteLine("====================================");
            
            // Test case: "Object initialize"
            TestObjectInitialize();
            
            Console.WriteLine("\nAll tests completed!");
        }
        
        /// <summary>
        /// Tests compilation of "Object initialize" which should generate:
        /// - Literal[0]: Association(key: #Object, value: null)
        /// - Literal[1]: Symbol(#initialize)
        /// - Bytecode: [72, 129, 124]
        /// </summary>
        private static void TestObjectInitialize()
        {
            Console.WriteLine("\nTest: Object initialize");
            Console.WriteLine("-----------------------");
            
            var compiler = new BytecodeCompiler();
            var parseTree = ParseExpression("Object initialize");
            var result = compiler.CompileExpressionFromTree(parseTree);
            
            // Verify literals
            Console.WriteLine($"Literals count: {result.Literals.Length}");
            for (int i = 0; i < result.Literals.Length; i++)
            {
                var literal = result.Literals[i];
                Console.WriteLine($"  Literal[{i}]: {literal}");
                
                if (i == 0)
                {
                    if (literal is Association assoc)
                    {
                        Console.WriteLine($"    ✓ Literal 0 is an Association");
                        Console.WriteLine($"    Key: {assoc.Key}");
                        Console.WriteLine($"    Value: {assoc.Value ?? "null"}");
                        
                        if (assoc.Key.Value == "Object" && assoc.Value == null)
                        {
                            Console.WriteLine($"    ✓ Association has correct key 'Object' and null value");
                        }
                        else
                        {
                            Console.WriteLine($"    ✗ Association has incorrect key or value");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    ✗ Expected Association, got {literal?.GetType().Name ?? "null"}");
                    }
                }
                else if (i == 1)
                {
                    if (literal is Symbol sym && sym.Value == "initialize")
                    {
                        Console.WriteLine($"    ✓ Literal 1 is Symbol(#initialize)");
                    }
                    else
                    {
                        Console.WriteLine($"    ✗ Expected Symbol(#initialize), got {literal}");
                    }
                }
            }
            
            // Verify bytecode
            Console.WriteLine($"\nBytecode: [{string.Join(", ", result.Bytecode)}]");
            Console.WriteLine($"Hex: [{string.Join(", ", result.Bytecode.Select(b => $"0x{b:X2}"))}]");
            
            var expectedBytecode = new byte[] { 72, 129, 124 };
            var expectedHex = new string[] { "0x48", "0x81", "0x7C" };
            
            if (result.Bytecode.SequenceEqual(expectedBytecode))
            {
                Console.WriteLine("✓ Bytecode matches expected: [72, 129, 124]");
                Console.WriteLine("  72 (0x48): PushLiteralVariable #0 (pushes Association.value)");
                Console.WriteLine("  129 (0x81): Send #1 with 0 args (sends #initialize)");
                Console.WriteLine("  124 (0x7C): Return top");
            }
            else
            {
                Console.WriteLine("✗ Bytecode does not match expected");
                Console.WriteLine($"  Expected: [{string.Join(", ", expectedBytecode)}]");
                Console.WriteLine($"  Got: [{string.Join(", ", result.Bytecode)}]");
                
                // Decode the bytecode for debugging
                Console.WriteLine("\nBytecode decoding:");
                for (int i = 0; i < result.Bytecode.Length; i++)
                {
                    var b = result.Bytecode[i];
                    Console.WriteLine($"  [{i}] {b} (0x{b:X2}): {DecodeBytecode(b, result.Bytecode, ref i)}");
                }
            }
        }
        
        /// <summary>
        /// Parse an expression string into an AST.
        /// </summary>
        private static Smalltalk80Parser.ExpressionContext ParseExpression(string expression)
        {
            var inputStream = new Antlr4.Runtime.AntlrInputStream(expression);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            parser.RemoveErrorListeners();
            
            return parser.expression();
        }
        
        /// <summary>
        /// Simple bytecode decoder for debugging.
        /// </summary>
        private static string DecodeBytecode(byte bytecode, byte[] bytecodes, ref int index)
        {
            if (bytecode >= 0 && bytecode <= 15)
                return $"PushReceiverVariable #{bytecode}";
            if (bytecode >= 16 && bytecode <= 31)
                return $"PushTemporary #{bytecode - 16}";
            if (bytecode >= 48 && bytecode <= 63)
                return $"PushArgument #{bytecode - 48}";
            if (bytecode >= 64 && bytecode <= 71)
                return $"PushLiteralConstant #{bytecode - 64}";
            if (bytecode >= 72 && bytecode <= 79)
                return $"PushLiteralVariable #{bytecode - 72}";
            if (bytecode >= 96 && bytecode <= 103)
                return $"StoreAndPopReceiverVariable #{bytecode - 96}";
            if (bytecode >= 104 && bytecode <= 111)
                return $"StoreAndPopTemporary #{bytecode - 104}";
            if (bytecode == 112)
                return "PushSelf";
            if (bytecode == 120)
                return "ReturnSelf";
            if (bytecode == 124)
                return "ReturnTop";
            if (bytecode == 125)
                return "ReturnTopFromBlock";
            if (bytecode >= 128 && bytecode <= 191)
            {
                var argCount = (bytecode - 128) >> 5;
                var selectorIndex = (bytecode - 128) & 0x1F;
                return $"Send #{selectorIndex} with {argCount} args";
            }
            if (bytecode >= 224 && bytecode <= 255)
                return $"SendSpecialSelector #{bytecode - 224}";
            
            return $"Unknown bytecode";
        }
    }
}