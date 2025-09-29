using System;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Primitives;

namespace TestPrimitivesRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing Smalltalk-80 Primitives");
            Console.WriteLine("================================\n");

            TestArithmeticPrimitives();
            TestComparisonPrimitives();
            TestObjectPrimitives();
            TestBlockPrimitives();

            Console.WriteLine("\n================================");
            Console.WriteLine("All tests completed!");
        }

        static void TestArithmeticPrimitives()
        {
            Console.WriteLine("Testing Arithmetic Primitives:");
            Console.WriteLine("------------------------------");

            // Test addition
            TestPrimitive("Addition (5 + 3)", 1, 
                Tags.TagInt(5), 
                new[] { Tags.TagInt(3) }, 
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 8);

            // Test subtraction
            TestPrimitive("Subtraction (10 - 3)", 2,
                Tags.TagInt(10),
                new[] { Tags.TagInt(3) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 7);

            // Test multiplication
            TestPrimitive("Multiplication (7 * 6)", 9,
                Tags.TagInt(7),
                new[] { Tags.TagInt(6) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 42);

            // Test division
            TestPrimitive("Division (42 / 7)", 10,
                Tags.TagInt(42),
                new[] { Tags.TagInt(7) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 6);

            // Test modulo
            TestPrimitive("Modulo (10 % 3)", 11,
                Tags.TagInt(10),
                new[] { Tags.TagInt(3) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 1);

            // Test bitwise AND
            TestPrimitive("BitAnd (0b1101 & 0b1010)", 14,
                Tags.TagInt(0b1101),
                new[] { Tags.TagInt(0b1010) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 0b1000);

            // Test bitwise OR
            TestPrimitive("BitOr (0b1101 | 0b1010)", 15,
                Tags.TagInt(0b1101),
                new[] { Tags.TagInt(0b1010) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 0b1111);

            // Test bit shift
            TestPrimitive("BitShift (5 << 2)", 17,
                Tags.TagInt(5),
                new[] { Tags.TagInt(2) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 20);

            Console.WriteLine();
        }

        static void TestComparisonPrimitives()
        {
            Console.WriteLine("Testing Comparison Primitives:");
            Console.WriteLine("-------------------------------");

            // Test less than
            TestPrimitive("Less than (5 < 10)", 3,
                Tags.TagInt(5),
                new[] { Tags.TagInt(10) },
                result => result == STrue.Instance);

            // Test greater than
            TestPrimitive("Greater than (10 > 5)", 4,
                Tags.TagInt(10),
                new[] { Tags.TagInt(5) },
                result => result == STrue.Instance);

            // Test equality
            TestPrimitive("Equality (42 = 42)", 7,
                Tags.TagInt(42),
                new[] { Tags.TagInt(42) },
                result => result == STrue.Instance);

            // Test identity
            TestPrimitive("Identity (42 == 42)", 110,
                Tags.TagInt(42),
                new[] { Tags.TagInt(42) },
                result => result == STrue.Instance);

            Console.WriteLine();
        }

        static void TestObjectPrimitives()
        {
            Console.WriteLine("Testing Object Primitives:");
            Console.WriteLine("--------------------------");

            // Create test objects
            var arrayClass = CreateMockClass("Array", isIndexable: true);
            var array = new SArray(arrayClass, 3);
            array.AtPut(1, Tags.TagInt(10));
            array.AtPut(2, Tags.TagInt(20));
            array.AtPut(3, Tags.TagInt(30));

            // Test basicAt:
            TestPrimitive("Array at: 2", 60,
                array,
                new[] { Tags.TagInt(2) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 20);

            // Test basicSize
            TestPrimitive("Array size", 62,
                array,
                Array.Empty<object>(),
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 3);

            // Test basicAt:put:
            TestPrimitive("Array at: 2 put: 42", 61,
                array,
                new[] { Tags.TagInt(2), Tags.TagInt(42) },
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 42);

            // Test hash
            TestPrimitive("SmallInteger hash", 75,
                Tags.TagInt(42),
                Array.Empty<object>(),
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 42);

            // Test Character hash
            TestPrimitive("Character hash", 75,
                Tags.FromCharacter('A'),
                Array.Empty<object>(),
                result => Tags.IsSmallInteger(result) && Tags.UntagInt(result) == 65);

            Console.WriteLine();
        }

        static void TestBlockPrimitives()
        {
            Console.WriteLine("Testing Block Primitives:");
            Console.WriteLine("-------------------------");

            var blockContextClass = CreateMockClass("BlockContext");
            var homeContext = new SMethodContext(CreateMockClass("MethodContext"), 10);
            
            // Create a block with no arguments
            var block0 = new SBlockContext(blockContextClass, homeContext, 0, 100, 16);

            // Test value (no arguments)
            TestPrimitive("Block value (0 args)", 81,
                block0,
                Array.Empty<object>(),
                result => result == block0);

            // Create a block with 2 arguments
            var block2 = new SBlockContext(blockContextClass, homeContext, 2, 100, 16);

            // Test value:value: (2 arguments)
            TestPrimitive("Block value:value: (2 args)", 82,
                block2,
                new[] { Tags.TagInt(10), Tags.TagInt(20) },
                result => result == block2);

            Console.WriteLine();
        }

        static void TestPrimitive(string description, int primitiveNumber, object receiver, object[] arguments, Func<object, bool> validator)
        {
            Console.Write($"  {description,-35} ");
            
            var success = PrimitiveTable.ExecutePrimitive(primitiveNumber, receiver, arguments, out var result);
            
            if (success && validator(result))
            {
                Console.WriteLine("[PASS]");
            }
            else if (!success)
            {
                Console.WriteLine($"[FAIL] Primitive returned false");
            }
            else
            {
                Console.WriteLine($"[FAIL] Invalid result");
            }
        }

        static SClass CreateMockClass(string name, bool isIndexable = false, bool isBytes = false)
        {
            var mockClass = new SClass(name, 2)
            {
                Format = isBytes ? ClassFormat.VariableByteSubclass : 
                        (isIndexable ? ClassFormat.VariableWordSubclass : ClassFormat.FixedSize),
                InstSize = 2
            };
            return mockClass;
        }
    }
}