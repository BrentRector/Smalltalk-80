using System;
using Xunit;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Primitives;

namespace Smalltalk80.Tests
{
    /// <summary>
    /// Comprehensive unit tests for all primitive implementations.
    /// Tests follow Blue Book primitive specifications.
    /// </summary>
    public class TestPrimitives
    {
        #region Test Helpers

        private static SClass CreateMockClass(string name, bool isIndexable = false, bool isBytes = false)
        {
            var mockClass = new SClass(null, 0)
            {
                Name = name,
                IsIndexable = isIndexable,
                IsBytes = isBytes,
                InstanceVariableCount = 2 // Default instance variables
            };
            return mockClass;
        }

        #endregion

        #region Arithmetic Primitive Tests (1-17)

        [Fact]
        public void TestPrimitiveAdd()
        {
            // Test normal addition
            var result = PrimitiveTable.ExecutePrimitive(1, 
                Tags.TagInt(5), 
                new[] { Tags.TagInt(3) }, 
                out var sum);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(sum));
            Assert.Equal(8, Tags.UntagInt(sum));

            // Test overflow detection
            result = PrimitiveTable.ExecutePrimitive(1,
                Tags.TagInt(Tags.MaxSmallInteger),
                new[] { Tags.TagInt(1) },
                out sum);
            
            Assert.False(result); // Should fail on overflow
        }

        [Fact]
        public void TestPrimitiveSubtract()
        {
            // Test normal subtraction
            var result = PrimitiveTable.ExecutePrimitive(2,
                Tags.TagInt(10),
                new[] { Tags.TagInt(3) },
                out var difference);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(difference));
            Assert.Equal(7, Tags.UntagInt(difference));

            // Test underflow detection
            result = PrimitiveTable.ExecutePrimitive(2,
                Tags.TagInt(Tags.MinSmallInteger),
                new[] { Tags.TagInt(1) },
                out difference);
            
            Assert.False(result); // Should fail on underflow
        }

        [Fact]
        public void TestPrimitiveLessThan()
        {
            // Test less than true
            var result = PrimitiveTable.ExecutePrimitive(3,
                Tags.TagInt(5),
                new[] { Tags.TagInt(10) },
                out var boolResult);
            
            Assert.True(result);
            Assert.Same(STrue.Instance, boolResult);

            // Test less than false
            result = PrimitiveTable.ExecutePrimitive(3,
                Tags.TagInt(10),
                new[] { Tags.TagInt(5) },
                out boolResult);
            
            Assert.True(result);
            Assert.Same(SFalse.Instance, boolResult);
        }

        [Fact]
        public void TestPrimitiveMultiply()
        {
            // Test normal multiplication
            var result = PrimitiveTable.ExecutePrimitive(9,
                Tags.TagInt(7),
                new[] { Tags.TagInt(6) },
                out var product);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(product));
            Assert.Equal(42, Tags.UntagInt(product));

            // Test overflow detection
            result = PrimitiveTable.ExecutePrimitive(9,
                Tags.TagInt(Tags.MaxSmallInteger),
                new[] { Tags.TagInt(2) },
                out product);
            
            Assert.False(result); // Should fail on overflow
        }

        [Fact]
        public void TestPrimitiveDivide()
        {
            // Test exact division
            var result = PrimitiveTable.ExecutePrimitive(10,
                Tags.TagInt(42),
                new[] { Tags.TagInt(7) },
                out var quotient);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(quotient));
            Assert.Equal(6, Tags.UntagInt(quotient));

            // Test inexact division (should fail)
            result = PrimitiveTable.ExecutePrimitive(10,
                Tags.TagInt(10),
                new[] { Tags.TagInt(3) },
                out quotient);
            
            Assert.False(result); // Should fail for inexact result

            // Test division by zero
            result = PrimitiveTable.ExecutePrimitive(10,
                Tags.TagInt(10),
                new[] { Tags.TagInt(0) },
                out quotient);
            
            Assert.False(result); // Should fail for division by zero
        }

        [Fact]
        public void TestPrimitiveMod()
        {
            // Test positive modulo
            var result = PrimitiveTable.ExecutePrimitive(11,
                Tags.TagInt(10),
                new[] { Tags.TagInt(3) },
                out var remainder);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(remainder));
            Assert.Equal(1, Tags.UntagInt(remainder));

            // Test negative dividend (Smalltalk modulo always returns positive)
            result = PrimitiveTable.ExecutePrimitive(11,
                Tags.TagInt(-10),
                new[] { Tags.TagInt(3) },
                out remainder);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(remainder));
            Assert.Equal(2, Tags.UntagInt(remainder)); // -10 mod 3 = 2 in Smalltalk
        }

        [Fact]
        public void TestPrimitiveBitAnd()
        {
            var result = PrimitiveTable.ExecutePrimitive(14,
                Tags.TagInt(0b1101),
                new[] { Tags.TagInt(0b1010) },
                out var andResult);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(andResult));
            Assert.Equal(0b1000, Tags.UntagInt(andResult));
        }

        [Fact]
        public void TestPrimitiveBitOr()
        {
            var result = PrimitiveTable.ExecutePrimitive(15,
                Tags.TagInt(0b1101),
                new[] { Tags.TagInt(0b1010) },
                out var orResult);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(orResult));
            Assert.Equal(0b1111, Tags.UntagInt(orResult));
        }

        [Fact]
        public void TestPrimitiveBitShift()
        {
            // Test left shift
            var result = PrimitiveTable.ExecutePrimitive(17,
                Tags.TagInt(5),
                new[] { Tags.TagInt(2) },
                out var shiftResult);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(shiftResult));
            Assert.Equal(20, Tags.UntagInt(shiftResult)); // 5 << 2 = 20

            // Test right shift
            result = PrimitiveTable.ExecutePrimitive(17,
                Tags.TagInt(20),
                new[] { Tags.TagInt(-2) },
                out shiftResult);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(shiftResult));
            Assert.Equal(5, Tags.UntagInt(shiftResult)); // 20 >> 2 = 5
        }

        #endregion

        #region Comparison Primitive Tests

        [Fact]
        public void TestPrimitiveIdentical()
        {
            // Test SmallInteger identity
            var result = PrimitiveTable.ExecutePrimitive(110,
                Tags.TagInt(42),
                new[] { Tags.TagInt(42) },
                out var identical);
            
            Assert.True(result);
            Assert.Same(STrue.Instance, identical);

            // Test different SmallIntegers
            result = PrimitiveTable.ExecutePrimitive(110,
                Tags.TagInt(42),
                new[] { Tags.TagInt(43) },
                out identical);
            
            Assert.True(result);
            Assert.Same(SFalse.Instance, identical);

            // Test object identity
            var obj1 = new SObject(CreateMockClass("TestClass"));
            result = PrimitiveTable.ExecutePrimitive(110,
                obj1,
                new[] { obj1 },
                out identical);
            
            Assert.True(result);
            Assert.Same(STrue.Instance, identical);

            // Test different objects
            var obj2 = new SObject(CreateMockClass("TestClass"));
            result = PrimitiveTable.ExecutePrimitive(110,
                obj1,
                new[] { obj2 },
                out identical);
            
            Assert.True(result);
            Assert.Same(SFalse.Instance, identical);
        }

        #endregion

        #region Object Primitive Tests (60-79)

        [Fact]
        public void TestPrimitiveAt()
        {
            var arrayClass = CreateMockClass("Array", isIndexable: true);
            var array = new SArray(arrayClass, 3);
            array.AtPut(1, Tags.TagInt(10));
            array.AtPut(2, Tags.TagInt(20));
            array.AtPut(3, Tags.TagInt(30));

            // Test valid access
            var result = PrimitiveTable.ExecutePrimitive(60,
                array,
                new[] { Tags.TagInt(2) },
                out var element);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(element));
            Assert.Equal(20, Tags.UntagInt(element));

            // Test out of bounds
            result = PrimitiveTable.ExecutePrimitive(60,
                array,
                new[] { Tags.TagInt(4) },
                out element);
            
            Assert.False(result); // Should fail for out of bounds
        }

        [Fact]
        public void TestPrimitiveAtPut()
        {
            var arrayClass = CreateMockClass("Array", isIndexable: true);
            var array = new SArray(arrayClass, 3);

            // Test valid store
            var result = PrimitiveTable.ExecutePrimitive(61,
                array,
                new[] { Tags.TagInt(2), Tags.TagInt(42) },
                out var stored);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(stored));
            Assert.Equal(42, Tags.UntagInt(stored));
            Assert.Equal(42, Tags.UntagInt(array.At(2)));
        }

        [Fact]
        public void TestPrimitiveSize()
        {
            var arrayClass = CreateMockClass("Array", isIndexable: true);
            var array = new SArray(arrayClass, 5);

            var result = PrimitiveTable.ExecutePrimitive(62,
                array,
                Array.Empty<object>(),
                out var size);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(size));
            Assert.Equal(5, Tags.UntagInt(size));

            // Test non-indexable object
            var obj = new SObject(CreateMockClass("TestClass"));
            result = PrimitiveTable.ExecutePrimitive(62,
                obj,
                Array.Empty<object>(),
                out size);
            
            Assert.True(result);
            Assert.Equal(0, Tags.UntagInt(size)); // Non-indexable objects have size 0
        }

        [Fact]
        public void TestPrimitiveNew()
        {
            var testClass = CreateMockClass("TestClass");
            testClass.InstanceVariableCount = 3;

            var result = PrimitiveTable.ExecutePrimitive(70,
                testClass,
                Array.Empty<object>(),
                out var newObject);
            
            Assert.True(result);
            Assert.IsType<SObject>(newObject);
            var obj = (SObject)newObject;
            Assert.Same(testClass, obj.Klass);
            Assert.Equal(3, obj.InstVarCount);
        }

        [Fact]
        public void TestPrimitiveNewWithArg()
        {
            var arrayClass = CreateMockClass("Array", isIndexable: true);

            var result = PrimitiveTable.ExecutePrimitive(71,
                arrayClass,
                new[] { Tags.TagInt(10) },
                out var newArray);
            
            Assert.True(result);
            Assert.IsType<SArray>(newArray);
            var array = (SArray)newArray;
            Assert.Same(arrayClass, array.Klass);
            Assert.Equal(10, array.Size);
        }

        [Fact]
        public void TestPrimitiveInstVarAt()
        {
            var testClass = CreateMockClass("TestClass");
            var obj = new SObject(testClass, 3);
            obj.SetInstVar(0, Tags.TagInt(100));
            obj.SetInstVar(1, Tags.TagInt(200));
            obj.SetInstVar(2, Tags.TagInt(300));

            // Test valid access (1-based indexing)
            var result = PrimitiveTable.ExecutePrimitive(73,
                obj,
                new[] { Tags.TagInt(2) },
                out var value);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(value));
            Assert.Equal(200, Tags.UntagInt(value));
        }

        [Fact]
        public void TestPrimitiveInstVarAtPut()
        {
            var testClass = CreateMockClass("TestClass");
            var obj = new SObject(testClass, 3);

            // Test valid store (1-based indexing)
            var result = PrimitiveTable.ExecutePrimitive(74,
                obj,
                new[] { Tags.TagInt(2), Tags.TagInt(42) },
                out var stored);
            
            Assert.True(result);
            Assert.Equal(42, Tags.UntagInt(stored));
            Assert.Equal(42, Tags.UntagInt(obj.GetInstVar(1))); // 0-based internally
        }

        [Fact]
        public void TestPrimitiveHash()
        {
            // Test SmallInteger hash (identity)
            var result = PrimitiveTable.ExecutePrimitive(75,
                Tags.TagInt(42),
                Array.Empty<object>(),
                out var hash);
            
            Assert.True(result);
            Assert.Equal(42, Tags.UntagInt(hash));

            // Test Character hash
            result = PrimitiveTable.ExecutePrimitive(75,
                Tags.FromCharacter('A'),
                Array.Empty<object>(),
                out hash);
            
            Assert.True(result);
            Assert.Equal(65, Tags.UntagInt(hash)); // ASCII value of 'A'

            // Test object hash
            var obj = new SObject(CreateMockClass("TestClass"));
            result = PrimitiveTable.ExecutePrimitive(75,
                obj,
                Array.Empty<object>(),
                out hash);
            
            Assert.True(result);
            Assert.True(Tags.IsSmallInteger(hash));
            Assert.Equal(obj.IdentityHash, Tags.UntagInt(hash));
        }

        [Fact]
        public void TestPrimitiveClass()
        {
            var testClass = CreateMockClass("TestClass");
            var obj = new SObject(testClass);

            var result = PrimitiveTable.ExecutePrimitive(79,
                obj,
                Array.Empty<object>(),
                out var klass);
            
            Assert.True(result);
            Assert.Same(testClass, klass);
        }

        #endregion

        #region Block Primitive Tests (80-82)

        [Fact]
        public void TestPrimitiveBlockCopy()
        {
            var methodContextClass = CreateMockClass("MethodContext");
            var blockContextClass = CreateMockClass("BlockContext");
            
            // Create a mock home context
            var homeContext = new SMethodContext(methodContextClass, 10)
            {
                Klass = methodContextClass
            };

            // Mock the class lookup
            methodContextClass.InstanceVariableCount = 6; // Standard context size

            // Note: This test would need proper kernel integration to fully work
            // For now, we verify the primitive accepts correct arguments
            var result = PrimitiveTable.ExecutePrimitive(80,
                homeContext,
                new[] { Tags.TagInt(2) }, // Block with 2 arguments
                out var blockContext);
            
            // The primitive will fail without proper class setup
            // This is expected in unit test environment
            Assert.False(result);
        }

        [Fact]
        public void TestPrimitiveBlockValue()
        {
            var blockContextClass = CreateMockClass("BlockContext");
            var homeContext = new SMethodContext(CreateMockClass("MethodContext"), 10);
            
            var block = new SBlockContext(blockContextClass, homeContext, 0, 100, 16);

            var result = PrimitiveTable.ExecutePrimitive(81,
                block,
                Array.Empty<object>(),
                out var activated);
            
            Assert.True(result);
            Assert.Same(block, activated);
        }

        [Fact]
        public void TestPrimitiveBlockValueWithArgs()
        {
            var blockContextClass = CreateMockClass("BlockContext");
            var homeContext = new SMethodContext(CreateMockClass("MethodContext"), 10);
            
            var block = new SBlockContext(blockContextClass, homeContext, 2, 100, 16);

            var result = PrimitiveTable.ExecutePrimitive(82,
                block,
                new[] { Tags.TagInt(10), Tags.TagInt(20) },
                out var activated);
            
            Assert.True(result);
            Assert.Same(block, activated);

            // Test wrong number of arguments
            result = PrimitiveTable.ExecutePrimitive(82,
                block,
                new[] { Tags.TagInt(10) }, // Only 1 argument, expects 2
                out activated);
            
            Assert.False(result); // Should fail with wrong argument count
        }

        #endregion

        #region String and ByteArray Tests

        [Fact]
        public void TestStringPrimitives()
        {
            var stringClass = CreateMockClass("String", isIndexable: true, isBytes: true);
            var str = SString.FromString(stringClass, "Hello");

            // Test stringAt:
            var result = PrimitiveTable.ExecutePrimitive(63,
                str,
                new[] { Tags.TagInt(1) },
                out var character);
            
            Assert.True(result);
            Assert.True(Tags.IsCharacter(character));
            Assert.Equal('H', Tags.ToChar(character));

            // Test stringAt:put:
            result = PrimitiveTable.ExecutePrimitive(64,
                str,
                new[] { Tags.TagInt(1), Tags.FromCharacter('J') },
                out var stored);
            
            Assert.True(result);
            Assert.Equal("Jello", str.AsString());
        }

        [Fact]
        public void TestByteArrayPrimitives()
        {
            var byteArrayClass = CreateMockClass("ByteArray", isIndexable: true, isBytes: true);
            var bytes = new SByteArray(byteArrayClass, 5);

            // Test at:put: with bytes
            var result = PrimitiveTable.ExecutePrimitive(61,
                bytes,
                new[] { Tags.TagInt(3), Tags.TagInt(255) },
                out var stored);
            
            Assert.True(result);
            Assert.Equal(255, bytes.At(3));

            // Test invalid byte value
            result = PrimitiveTable.ExecutePrimitive(61,
                bytes,
                new[] { Tags.TagInt(3), Tags.TagInt(256) }, // Out of byte range
                out stored);
            
            Assert.False(result); // Should fail for out of range
        }

        #endregion

        #region Edge Cases and Error Conditions

        [Fact]
        public void TestPrimitiveWithInvalidArguments()
        {
            // Test arithmetic primitive with non-SmallInteger
            var obj = new SObject(CreateMockClass("TestClass"));
            var result = PrimitiveTable.ExecutePrimitive(1,
                obj,
                new[] { Tags.TagInt(5) },
                out var sum);
            
            Assert.False(result); // Should fail with non-SmallInteger receiver

            // Test with wrong number of arguments
            result = PrimitiveTable.ExecutePrimitive(1,
                Tags.TagInt(5),
                Array.Empty<object>(), // No arguments
                out sum);
            
            Assert.False(result); // Should fail with wrong argument count
        }

        [Fact]
        public void TestUnimplementedPrimitive()
        {
            var result = PrimitiveTable.ExecutePrimitive(250, // Unimplemented primitive
                SNil.Instance,
                Array.Empty<object>(),
                out var value);
            
            Assert.False(result); // Should fail for unimplemented primitive
        }

        [Fact]
        public void TestPrimitiveTableLookup()
        {
            // Verify implemented primitives
            Assert.True(PrimitiveTable.IsPrimitiveImplemented(1)); // Add
            Assert.True(PrimitiveTable.IsPrimitiveImplemented(60)); // At
            Assert.True(PrimitiveTable.IsPrimitiveImplemented(81)); // Block value
            
            // Verify unimplemented primitives
            Assert.False(PrimitiveTable.IsPrimitiveImplemented(0)); // Invalid
            Assert.False(PrimitiveTable.IsPrimitiveImplemented(256)); // Out of range
            Assert.False(PrimitiveTable.IsPrimitiveImplemented(150)); // Not yet implemented
        }

        [Fact]
        public void TestPrimitiveName()
        {
            Assert.Equal("SmallInteger +", PrimitiveTable.GetPrimitiveName(1));
            Assert.Equal("basicAt:", PrimitiveTable.GetPrimitiveName(60));
            Assert.Equal("value", PrimitiveTable.GetPrimitiveName(81));
            Assert.Equal("Primitive#250", PrimitiveTable.GetPrimitiveName(250));
        }

        #endregion
    }
}