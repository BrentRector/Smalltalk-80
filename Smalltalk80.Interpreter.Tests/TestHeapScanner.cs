using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Primitives;

namespace Tests
{
    /// <summary>
    /// Comprehensive tests for the HeapScanner and become: primitive implementation.
    /// </summary>
    public class TestHeapScanner
    {
        #region Test Infrastructure

        /// <summary>
        /// Creates a test class for testing.
        /// </summary>
        private static SClass CreateTestClass(string name, SClass? superclass = null)
        {
            var klass = new SClass
            {
                Name = name,
                Superclass = superclass,
                InstSize = 2,
                Format = ClassFormat.PointerNonIndexable(),
                MethodDict = new VersionedMethodDictionary()
            };
            return klass;
        }

        /// <summary>
        /// Creates a test object with the specified class.
        /// </summary>
        private static SObject CreateTestObject(SClass klass)
        {
            return new SObject(klass, klass.InstSize);
        }

        #endregion

        #region Basic Two-Way Become Tests

        [Fact]
        public void TestTwoWayBecome_SwapsObjectReferences()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            // Create a container that holds references
            var container = CreateTestObject(testClass);
            container.SetInstVar(0, obj1);
            container.SetInstVar(1, obj2);
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);
            scanner.RegisterObject(container);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            Assert.Same(obj2, container.GetInstVar(0)); // obj1 reference became obj2
            Assert.Same(obj1, container.GetInstVar(1)); // obj2 reference became obj1
        }

        [Fact]
        public void TestTwoWayBecome_SwapsClassReferences()
        {
            // Arrange
            var scanner = new HeapScanner();
            var class1 = CreateTestClass("Class1");
            var class2 = CreateTestClass("Class2");
            
            var obj1 = CreateTestObject(class1);
            var obj2 = CreateTestObject(class2);
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            Assert.Same(class2, obj1.Klass);
            Assert.Same(class1, obj2.Klass);
        }

        [Fact]
        public void TestTwoWayBecome_SwapsInstanceVariables()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            obj1.SetInstVar(0, Tags.FromSmallInteger(42));
            obj2.SetInstVar(0, Tags.FromSmallInteger(100));
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            Assert.True(Tags.TryUnboxSmallInteger(obj1.GetInstVar(0)!, out var val1));
            Assert.True(Tags.TryUnboxSmallInteger(obj2.GetInstVar(0)!, out var val2));
            Assert.Equal(100, val1);
            Assert.Equal(42, val2);
        }

        #endregion

        #region One-Way Become Tests

        [Fact]
        public void TestOneWayBecome_ReplacesReferences()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            
            var oldObj = CreateTestObject(testClass);
            var newObj = CreateTestObject(testClass);
            
            var container = CreateTestObject(testClass);
            container.SetInstVar(0, oldObj);
            container.SetInstVar(1, oldObj); // Multiple references
            
            scanner.RegisterObject(oldObj);
            scanner.RegisterObject(newObj);
            scanner.RegisterObject(container);

            // Act
            bool result = scanner.PerformOneWayBecome(oldObj, newObj);

            // Assert
            Assert.True(result);
            Assert.Same(newObj, container.GetInstVar(0));
            Assert.Same(newObj, container.GetInstVar(1));
        }

        #endregion

        #region Immediate Value Tests

        [Fact]
        public void TestBecome_FailsWithSmallInteger()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);
            var smallInt = Tags.FromSmallInteger(42);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj, smallInt);

            // Assert
            Assert.False(result); // Cannot become immediates
        }

        [Fact]
        public void TestBecome_FailsWithCharacter()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);
            var character = Tags.FromCharacter(65); // 'A'

            // Act
            bool result = scanner.PerformTwoWayBecome(obj, character);

            // Assert
            Assert.False(result); // Cannot become immediates
        }

        [Fact]
        public void TestBecome_FailsWithNil()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj, SNil.Instance);

            // Assert
            Assert.False(result); // Cannot become special singletons
        }

        [Fact]
        public void TestBecome_FailsWithBooleans()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);

            // Act & Assert
            Assert.False(scanner.PerformTwoWayBecome(obj, STrue.Instance));
            Assert.False(scanner.PerformTwoWayBecome(obj, SFalse.Instance));
        }

        #endregion

        #region Array Tests

        [Fact]
        public void TestBecome_WithArrayElements()
        {
            // Arrange
            var scanner = new HeapScanner();
            var arrayClass = CreateTestClass("Array");
            var testClass = CreateTestClass("TestClass");
            
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            var array = new SArray(arrayClass, 3);
            array.AtPut(1, obj1);
            array.AtPut(2, obj2);
            array.AtPut(3, obj1); // Multiple references
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);
            scanner.RegisterObject(array);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            Assert.Same(obj2, array.At(1)); // obj1 became obj2
            Assert.Same(obj1, array.At(2)); // obj2 became obj1
            Assert.Same(obj2, array.At(3)); // obj1 became obj2
        }

        #endregion

        #region Method Context Tests

        [Fact]
        public void TestBecome_WithMethodContext()
        {
            // Arrange
            var scanner = new HeapScanner();
            var contextClass = CreateTestClass("MethodContext");
            var methodClass = CreateTestClass("CompiledMethod");
            var testClass = CreateTestClass("TestClass");
            
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            var method = new SCompiledMethod(methodClass);
            var context = new SMethodContext(contextClass, method, obj1, new object[0]);
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);
            scanner.RegisterObject(context);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            Assert.Same(obj2, context.Receiver); // Receiver was swapped
        }

        [Fact]
        public void TestBecome_WithContextChain()
        {
            // Arrange
            var scanner = new HeapScanner();
            var contextClass = CreateTestClass("MethodContext");
            var methodClass = CreateTestClass("CompiledMethod");
            
            var method = new SCompiledMethod(methodClass);
            var context1 = new SMethodContext(contextClass, method, SNil.Instance, new object[0]);
            var context2 = new SMethodContext(contextClass, method, SNil.Instance, new object[0]);
            var context3 = new SMethodContext(contextClass, method, SNil.Instance, new object[0]);
            
            context2.Sender = context1;
            context3.Sender = context2;
            
            scanner.RegisterObject(context1);
            scanner.RegisterObject(context2);
            scanner.RegisterObject(context3);

            // Act
            bool result = scanner.PerformTwoWayBecome(context1, context3);

            // Assert
            Assert.True(result);
            Assert.Same(context3, context2.Sender); // context1 became context3
        }

        #endregion

        #region Block Context Tests

        [Fact]
        public void TestBecome_WithBlockContext()
        {
            // Arrange
            var scanner = new HeapScanner();
            var contextClass = CreateTestClass("MethodContext");
            var blockClass = CreateTestClass("BlockContext");
            var methodClass = CreateTestClass("CompiledMethod");
            
            var method = new SCompiledMethod(methodClass);
            var homeContext = new SMethodContext(contextClass, method, SNil.Instance, new object[0]);
            var blockContext = new SBlockContext(blockClass);
            blockContext.Home = homeContext;
            
            var anotherContext = new SMethodContext(contextClass, method, SNil.Instance, new object[0]);
            
            scanner.RegisterObject(homeContext);
            scanner.RegisterObject(blockContext);
            scanner.RegisterObject(anotherContext);

            // Act
            bool result = scanner.PerformTwoWayBecome(homeContext, anotherContext);

            // Assert
            Assert.True(result);
            Assert.Same(anotherContext, blockContext.Home); // Home reference was updated
        }

        #endregion

        #region Class Hierarchy Tests

        [Fact]
        public void TestBecome_WithClassHierarchy()
        {
            // Arrange
            var scanner = new HeapScanner();
            
            var superclass = CreateTestClass("Superclass");
            var subclass1 = CreateTestClass("Subclass1", superclass);
            var subclass2 = CreateTestClass("Subclass2", superclass);
            
            scanner.RegisterObject(superclass);
            scanner.RegisterObject(subclass1);
            scanner.RegisterObject(subclass2);

            // Act
            bool result = scanner.PerformTwoWayBecome(subclass1, subclass2);

            // Assert
            Assert.True(result);
            // After become, the superclass references should be swapped
            Assert.Same(superclass, subclass1.Superclass);
            Assert.Same(superclass, subclass2.Superclass);
        }

        #endregion

        #region Method Dictionary Tests

        [Fact]
        public void TestBecome_WithMethodDictionary()
        {
            // Arrange
            var scanner = new HeapScanner();
            var methodClass = CreateTestClass("CompiledMethod");
            var testClass = CreateTestClass("TestClass");
            
            var method1 = new SCompiledMethod(methodClass);
            var method2 = new SCompiledMethod(methodClass);
            
            var symbol = new SSymbol("testMethod");
            testClass.MethodDict!.AtPut(symbol, method1);
            
            scanner.RegisterObject(method1);
            scanner.RegisterObject(method2);
            scanner.RegisterObject(testClass);

            // Act
            bool result = scanner.PerformTwoWayBecome(method1, method2);

            // Assert
            Assert.True(result);
            Assert.Same(method2, testClass.MethodDict.Lookup(symbol));
        }

        #endregion

        #region Primitive Integration Tests

        [Fact]
        public void TestPrimitiveBecome_WithHeapScanner()
        {
            // Arrange
            var scanner = new HeapScanner();
            ObjectPrimitives.HeapScanner = scanner;
            
            var testClass = CreateTestClass("TestClass");
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);

            // Act
            bool success = ObjectPrimitives.PrimitiveBecome(obj1, new[] { obj2 }, out var result);

            // Assert
            Assert.True(success);
            Assert.Same(obj1, result);
        }

        [Fact]
        public void TestPrimitiveBecome_WithoutHeapScanner_FallbackWorks()
        {
            // Arrange
            ObjectPrimitives.HeapScanner = null; // No scanner, use fallback
            
            var testClass = CreateTestClass("TestClass");
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            var class1 = obj1.Klass;
            var class2 = obj2.Klass;

            // Act
            bool success = ObjectPrimitives.PrimitiveBecome(obj1, new[] { obj2 }, out var result);

            // Assert
            Assert.True(success);
            Assert.Same(obj1, result);
            // Fallback swaps contents
            Assert.Same(class2, obj1.Klass);
            Assert.Same(class1, obj2.Klass);
        }

        [Fact]
        public void TestPrimitiveBecome_FailsWithImmediate()
        {
            // Arrange
            var scanner = new HeapScanner();
            ObjectPrimitives.HeapScanner = scanner;
            
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);
            var smallInt = Tags.FromSmallInteger(42);

            // Act
            bool success = ObjectPrimitives.PrimitiveBecome(obj, new[] { smallInt }, out var result);

            // Assert
            Assert.False(success);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void TestBecome_ThreadSafe()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var objects = new List<SObject>();
            
            for (int i = 0; i < 100; i++)
            {
                var obj = CreateTestObject(testClass);
                objects.Add(obj);
                scanner.RegisterObject(obj);
            }

            var errors = new List<Exception>();
            var tasks = new List<System.Threading.Tasks.Task>();

            // Act - Perform multiple become operations concurrently
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            var idx1 = (index * 10 + j) % objects.Count;
                            var idx2 = (idx1 + 1) % objects.Count;
                            scanner.PerformTwoWayBecome(objects[idx1], objects[idx2]);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add(ex);
                        }
                    }
                });
                tasks.Add(task);
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Empty(errors); // No exceptions during concurrent operations
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void TestBecome_PerformanceWithManyObjects()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var objects = new List<SObject>();
            
            // Create many objects with references
            for (int i = 0; i < 1000; i++)
            {
                var obj = CreateTestObject(testClass);
                objects.Add(obj);
                scanner.RegisterObject(obj);
                
                // Create references between objects
                if (i > 0)
                {
                    obj.SetInstVar(0, objects[i - 1]);
                }
            }

            var obj1 = objects[100];
            var obj2 = objects[500];

            // Act
            var startTime = DateTime.UtcNow;
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(result);
            Assert.True(elapsed.TotalSeconds < 1.0, $"Become operation took too long: {elapsed.TotalSeconds} seconds");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void TestBecome_WithNullArguments()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);

            // Act & Assert
            Assert.False(scanner.PerformTwoWayBecome(null!, obj));
            Assert.False(scanner.PerformTwoWayBecome(obj, null!));
            Assert.False(scanner.PerformTwoWayBecome(null!, null!));
        }

        [Fact]
        public void TestBecome_WithSameObject()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            var obj = CreateTestObject(testClass);
            
            scanner.RegisterObject(obj);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj, obj);

            // Assert
            Assert.True(result); // Should succeed but be a no-op
        }

        [Fact]
        public void TestBecome_WithCircularReferences()
        {
            // Arrange
            var scanner = new HeapScanner();
            var testClass = CreateTestClass("TestClass");
            
            var obj1 = CreateTestObject(testClass);
            var obj2 = CreateTestObject(testClass);
            
            // Create circular references
            obj1.SetInstVar(0, obj2);
            obj2.SetInstVar(0, obj1);
            
            scanner.RegisterObject(obj1);
            scanner.RegisterObject(obj2);

            // Act
            bool result = scanner.PerformTwoWayBecome(obj1, obj2);

            // Assert
            Assert.True(result);
            // After become, circular references should be maintained but swapped
            Assert.Same(obj1, obj1.GetInstVar(0)); // obj2 became obj1
            Assert.Same(obj2, obj2.GetInstVar(0)); // obj1 became obj2
        }

        #endregion
    }
}