using System;
using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests
{
    /// <summary>
    /// Unit tests for SMethodContext class.
    /// </summary>
    public class TestSMethodContext
    {
        private readonly SClass _methodContextClass;
        private readonly SClass _compiledMethodClass;
        private readonly SClass _objectClass;
        
        public TestSMethodContext()
        {
            // Setup basic classes for testing
            _objectClass = new SClass("Object", 0);
            _compiledMethodClass = new SClass("CompiledMethod", 4) { Format = ClassFormat.CompiledMethod };
            _methodContextClass = new SClass("MethodContext", 0) { Format = ClassFormat.MethodContext };
        }
        
        #region Field Access Tests
        
        [Fact]
        public void TestFieldIndices()
        {
            // Verify field indices match Blue Book specification
            Assert.Equal(0, SMethodContext.SenderIndex);
            Assert.Equal(1, SMethodContext.InstructionPointerIndex);
            Assert.Equal(2, SMethodContext.StackPointerIndex);
            Assert.Equal(3, SMethodContext.MethodIndex);
            Assert.Equal(5, SMethodContext.ReceiverIndex);
            Assert.Equal(6, SMethodContext.TempFrameStart);
        }
        
        [Fact]
        public void TestSenderAccess()
        {
            var context = new SMethodContext(_methodContextClass);
            var senderContext = new SMethodContext(_methodContextClass);
            
            // Initially nil
            Assert.Null(context.Sender);
            
            // Set and retrieve
            context.Sender = senderContext;
            Assert.Same(senderContext, context.Sender);
            
            // Clear
            context.Sender = null;
            Assert.Null(context.Sender);
        }
        
        [Fact]
        public void TestInstructionPointerAccess()
        {
            var context = new SMethodContext(_methodContextClass);
            
            // Initially 0
            Assert.Equal(0, context.InstructionPointer);
            
            // Set and retrieve
            context.InstructionPointer = 42;
            Assert.Equal(42, context.InstructionPointer);
            
            // Verify stored as SmallInteger
            var storedValue = context.GetInstVar(SMethodContext.InstructionPointerIndex);
            Assert.True(Tags.IsImmediate(storedValue!));
            Assert.True(Tags.TryUnboxSmallInteger(storedValue!, out long value));
            Assert.Equal(42, value);
        }
        
        [Fact]
        public void TestStackPointerAccess()
        {
            var context = new SMethodContext(_methodContextClass);
            
            // Initially 0
            Assert.Equal(0, context.StackPointer);
            
            // Set and retrieve
            context.StackPointer = 10;
            Assert.Equal(10, context.StackPointer);
            
            // Verify stored as SmallInteger
            var storedValue = context.GetInstVar(SMethodContext.StackPointerIndex);
            Assert.True(Tags.IsImmediate(storedValue!));
        }
        
        [Fact]
        public void TestMethodAccess()
        {
            var context = new SMethodContext(_methodContextClass);
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Header = SCompiledMethod.MakeHeader(2, 3, 1, false, 10),
                Selector = SSymbol.Intern("test:with:")
            };
            
            // Initially nil
            Assert.Null(context.Method);
            
            // Set and retrieve
            context.Method = method;
            Assert.Same(method, context.Method);
        }
        
        [Fact]
        public void TestReceiverAccess()
        {
            var context = new SMethodContext(_methodContextClass);
            var receiver = new SObject(_objectClass);
            
            // Initially nil
            Assert.Same(SNil.Instance, context.Receiver ?? SNil.Instance);
            
            // Set and retrieve
            context.Receiver = receiver;
            Assert.Same(receiver, context.Receiver);
        }
        
        #endregion
        
        #region Constructor Tests
        
        [Fact]
        public void TestBasicConstructor()
        {
            var context = new SMethodContext(_methodContextClass, 20);
            
            // Check initialization
            Assert.Equal(_methodContextClass, context.Klass);
            Assert.Equal(SMethodContext.TempFrameStart + 20, context.InstVarCount);
            Assert.Equal(0, context.InstructionPointer);
            Assert.Equal(0, context.StackPointer);
            
            // Fixed fields should be nil
            Assert.Null(context.Sender);
            Assert.Null(context.Method);
            Assert.Same(SNil.Instance, context.Receiver ?? SNil.Instance);
        }
        
        [Fact]
        public void TestMethodConstructor()
        {
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Header = SCompiledMethod.MakeHeader(2, 3, 1, false, 10),
                Selector = SSymbol.Intern("test:with:")
            };
            
            var receiver = new SObject(_objectClass);
            var arg1 = Tags.FromSmallInteger(42);
            var arg2 = SSymbol.Intern("hello");
            var arguments = new object[] { arg1, arg2 };
            
            var context = new SMethodContext(_methodContextClass, method, receiver, arguments);
            
            // Check basic setup
            Assert.Same(method, context.Method);
            Assert.Same(receiver, context.Receiver);
            Assert.Equal(1, context.InstructionPointer); // Smalltalk uses 1-based
            Assert.Equal(3, context.StackPointer); // After temporaries
            
            // Check arguments copied to temporaries
            Assert.Equal(arg1, context.GetTemporary(0));
            Assert.Equal(arg2, context.GetTemporary(1));
            
            // Check remaining temporary initialized to nil
            Assert.Same(SNil.Instance, context.GetTemporary(2));
        }
        
        #endregion
        
        #region Stack Operation Tests
        
        [Fact]
        public void TestPushPop()
        {
            var context = new SMethodContext(_methodContextClass);
            var obj1 = Tags.FromSmallInteger(10);
            var obj2 = SSymbol.Intern("test");
            
            // Push values
            context.Push(obj1);
            Assert.Equal(1, context.StackPointer);
            
            context.Push(obj2);
            Assert.Equal(2, context.StackPointer);
            
            // Pop values (LIFO order)
            var popped2 = context.Pop();
            Assert.Equal(obj2, popped2);
            Assert.Equal(1, context.StackPointer);
            
            var popped1 = context.Pop();
            Assert.Equal(obj1, popped1);
            Assert.Equal(0, context.StackPointer);
        }
        
        [Fact]
        public void TestPopFromEmptyStack()
        {
            var context = new SMethodContext(_methodContextClass);
            Assert.Throws<InvalidOperationException>(() => context.Pop());
        }
        
        [Fact]
        public void TestStackTop()
        {
            var context = new SMethodContext(_methodContextClass);
            var obj = Tags.FromSmallInteger(42);
            
            // Empty stack
            Assert.Throws<InvalidOperationException>(() => context.StackTop());
            
            // With value
            context.Push(obj);
            Assert.Equal(obj, context.StackTop());
            Assert.Equal(1, context.StackPointer); // Should not change
        }
        
        [Fact]
        public void TestStackValue()
        {
            var context = new SMethodContext(_methodContextClass);
            var obj1 = Tags.FromSmallInteger(1);
            var obj2 = Tags.FromSmallInteger(2);
            var obj3 = Tags.FromSmallInteger(3);
            
            context.Push(obj1);
            context.Push(obj2);
            context.Push(obj3);
            
            // Access from top
            Assert.Equal(obj3, context.StackValue(0)); // Top
            Assert.Equal(obj2, context.StackValue(1)); // Second
            Assert.Equal(obj1, context.StackValue(2)); // Third
            
            // Out of range
            Assert.Throws<ArgumentOutOfRangeException>(() => context.StackValue(3));
        }
        
        [Fact]
        public void TestPopN()
        {
            var context = new SMethodContext(_methodContextClass);
            
            // Push some values
            for (int i = 0; i < 5; i++)
            {
                context.Push(Tags.FromSmallInteger(i));
            }
            Assert.Equal(5, context.StackPointer);
            
            // Pop multiple
            context.PopN(3);
            Assert.Equal(2, context.StackPointer);
            
            // Verify remaining values
            Assert.Equal(Tags.FromSmallInteger(1), context.Pop());
            Assert.Equal(Tags.FromSmallInteger(0), context.Pop());
            
            // Pop too many
            context.Push(Tags.FromSmallInteger(99));
            Assert.Throws<InvalidOperationException>(() => context.PopN(5));
        }
        
        [Fact]
        public void TestDuplicate()
        {
            var context = new SMethodContext(_methodContextClass);
            var obj = Tags.FromSmallInteger(42);
            
            context.Push(obj);
            context.Duplicate();
            
            Assert.Equal(2, context.StackPointer);
            Assert.Equal(obj, context.Pop());
            Assert.Equal(obj, context.Pop());
        }
        
        #endregion
        
        #region Temporary Variable Tests
        
        [Fact]
        public void TestTemporaryAccess()
        {
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Header = SCompiledMethod.MakeHeader(1, 3, 0, false, 10)
            };
            
            var context = new SMethodContext(_methodContextClass, method, SNil.Instance, new object[] { Tags.FromSmallInteger(42) });
            
            // Check argument (first temporary)
            Assert.Equal(Tags.FromSmallInteger(42), context.GetTemporary(0));
            
            // Check other temporaries initialized to nil
            Assert.Same(SNil.Instance, context.GetTemporary(1));
            Assert.Same(SNil.Instance, context.GetTemporary(2));
            
            // Modify temporary
            var newValue = SSymbol.Intern("modified");
            context.SetTemporary(1, newValue);
            Assert.Equal(newValue, context.GetTemporary(1));
            
            // Temporary count
            Assert.Equal(3, context.TemporaryCount);
        }
        
        #endregion
        
        #region Context Management Tests
        
        [Fact]
        public void TestHomeContext()
        {
            var context = new SMethodContext(_methodContextClass);
            Assert.Same(context, context.HomeContext); // Method context is its own home
        }
        
        [Fact]
        public void TestIsBlockContext()
        {
            var context = new SMethodContext(_methodContextClass);
            Assert.False(context.IsBlockContext);
        }
        
        [Fact]
        public void TestReturn()
        {
            var sender = new SMethodContext(_methodContextClass);
            var context = new SMethodContext(_methodContextClass)
            {
                Sender = sender
            };
            
            var returnValue = Tags.FromSmallInteger(42);
            var nextContext = context.Return(returnValue);
            
            // Should return sender
            Assert.Same(sender, nextContext);
            
            // Sender should have return value pushed
            Assert.Equal(returnValue, sender.Pop());
            
            // Context should be terminated
            Assert.Null(context.Sender);
            Assert.Null(context.Method);
        }
        
        [Fact]
        public void TestReturnWithoutSender()
        {
            var context = new SMethodContext(_methodContextClass);
            var returnValue = Tags.FromSmallInteger(42);
            
            var nextContext = context.Return(returnValue);
            Assert.Null(nextContext); // No sender to return to
        }
        
        [Fact]
        public void TestTerminate()
        {
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 10) // 10 stack slots
            };
            var context = new SMethodContext(_methodContextClass, method, new SObject(_objectClass), Array.Empty<object>())
            {
                Sender = new SMethodContext(_methodContextClass)
            };
            
            // Push some values
            context.Push(Tags.FromSmallInteger(1));
            context.Push(Tags.FromSmallInteger(2));
            
            // Terminate
            context.Terminate();
            
            // Check cleared
            Assert.Null(context.Sender);
            Assert.Null(context.Method);
            Assert.Same(SNil.Instance, context.Receiver);
            
            // Stack should be cleared (all nil)
            Assert.Same(SNil.Instance, context.GetInstVar(SMethodContext.TempFrameStart));
            Assert.Same(SNil.Instance, context.GetInstVar(SMethodContext.TempFrameStart + 1));
        }
        
        #endregion
        
        #region ToString Test
        
        [Fact]
        public void TestToString()
        {
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Selector = SSymbol.Intern("testMethod")
            };
            
            var receiver = new SObject(_objectClass);
            var context = new SMethodContext(_methodContextClass, method, receiver, Array.Empty<object>());
            
            var str = context.ToString();
            Assert.Contains("MethodContext", str);
            Assert.Contains("testMethod", str);
            Assert.Contains("IP=1", str);
            Assert.Contains("SP=0", str);
        }
        
        #endregion
    }
}