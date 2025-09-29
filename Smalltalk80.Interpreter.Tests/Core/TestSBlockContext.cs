using System;
using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests
{
    /// <summary>
    /// Unit tests for SBlockContext class.
    /// </summary>
    public class TestSBlockContext
    {
        private readonly SClass _methodContextClass;
        private readonly SClass _blockContextClass;
        private readonly SClass _compiledMethodClass;
        private readonly SClass _objectClass;
        
        public TestSBlockContext()
        {
            // Setup basic classes for testing
            _objectClass = new SClass("Object", 0);
            _compiledMethodClass = new SClass("CompiledMethod", 4) { Format = ClassFormat.CompiledMethod };
            _methodContextClass = new SClass("MethodContext", 0) { Format = ClassFormat.MethodContext };
            _blockContextClass = new SClass("BlockContext", 0) { Format = ClassFormat.BlockContext };
        }
        
        private SMethodContext CreateHomeContext()
        {
            var method = new SCompiledMethod(_compiledMethodClass)
            {
                Header = SCompiledMethod.MakeHeader(1, 5, 2, false, 20),
                Selector = SSymbol.Intern("testMethod:")
            };
            
            var receiver = new SObject(_objectClass);
            var arguments = new object[] { Tags.FromSmallInteger(10) };
            
            return new SMethodContext(_methodContextClass, method, receiver, arguments);
        }
        
        #region Field Access Tests
        
        [Fact]
        public void TestFieldIndices()
        {
            // Verify field indices match Blue Book specification
            Assert.Equal(SMethodContext.SenderIndex, SBlockContext.CallerIndex);
            Assert.Equal(3, SBlockContext.BlockArgumentCountIndex);
            Assert.Equal(4, SBlockContext.InitialIPIndex);
            Assert.Equal(5, SBlockContext.HomeIndex);
        }
        
        [Fact]
        public void TestCallerAccess()
        {
            var block = new SBlockContext(_blockContextClass);
            var caller = new SMethodContext(_methodContextClass);
            
            // Initially nil
            Assert.Null(block.Caller);
            
            // Set and retrieve
            block.Caller = caller;
            Assert.Same(caller, block.Caller);
            
            // Verify it's the same as Sender
            Assert.Same(caller, block.Sender);
        }
        
        [Fact]
        public void TestArgumentCountAccess()
        {
            var block = new SBlockContext(_blockContextClass);
            
            // Initially 0
            Assert.Equal(0, block.ArgumentCount);
            
            // Set and retrieve
            block.ArgumentCount = 2;
            Assert.Equal(2, block.ArgumentCount);
            
            // Verify stored as SmallInteger
            var storedValue = block.GetInstVar(SBlockContext.BlockArgumentCountIndex);
            Assert.True(Tags.IsImmediate(storedValue!));
            Assert.True(Tags.TryUnboxSmallInteger(storedValue!, out long value));
            Assert.Equal(2, value);
        }
        
        [Fact]
        public void TestInitialIPAccess()
        {
            var block = new SBlockContext(_blockContextClass);
            
            // Initially 0
            Assert.Equal(0, block.InitialIP);
            
            // Set and retrieve
            block.InitialIP = 25;
            Assert.Equal(25, block.InitialIP);
            
            // Verify stored as SmallInteger
            var storedValue = block.GetInstVar(SBlockContext.InitialIPIndex);
            Assert.True(Tags.IsImmediate(storedValue!));
        }
        
        [Fact]
        public void TestHomeContextAccess()
        {
            var block = new SBlockContext(_blockContextClass);
            var home = CreateHomeContext();
            
            // Initially returns self as fallback
            Assert.Same(block, block.HomeContext);
            
            // Set and retrieve
            block.SetHomeContext(home);
            Assert.Same(home, block.HomeContext);
        }
        
        [Fact]
        public void TestMethodFromHome()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 0, 10);
            
            // Block should get method from home
            Assert.Same(home.Method, block.Method);
            
            // Setting method on block should have no effect
            block.Method = new SCompiledMethod(_compiledMethodClass);
            Assert.Same(home.Method, block.Method);
        }
        
        [Fact]
        public void TestReceiverFromHome()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 0, 10);
            
            // Block should get receiver from home
            Assert.Same(home.Receiver, block.Receiver);
            
            // Setting receiver on block should have no effect
            block.Receiver = new SObject(_objectClass);
            Assert.Same(home.Receiver, block.Receiver);
        }
        
        #endregion
        
        #region Constructor Tests
        
        [Fact]
        public void TestBasicConstructor()
        {
            var block = new SBlockContext(_blockContextClass, 20);
            
            // Check initialization
            Assert.Equal(_blockContextClass, block.Klass);
            Assert.Equal(0, block.ArgumentCount);
            Assert.Equal(0, block.InitialIP);
            Assert.True(block.IsBlockContext);
        }
        
        [Fact]
        public void TestFullConstructor()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 2, 15, 10);
            
            // Check initialization
            Assert.Same(home, block.HomeContext);
            Assert.Equal(2, block.ArgumentCount);
            Assert.Equal(15, block.InitialIP);
            Assert.Equal(15, block.InstructionPointer);
            Assert.Equal(0, block.StackPointer);
        }
        
        #endregion
        
        #region Block Evaluation Tests
        
        [Fact]
        public void TestEvaluateWithArguments()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 2, 20);
            
            var arg1 = Tags.FromSmallInteger(42);
            var arg2 = SSymbol.Intern("test");
            
            block.EvaluateWithArguments(new object[] { arg1, arg2 });
            
            // Arguments should be in temporaries
            Assert.Equal(arg1, block.GetTemporary(0));
            Assert.Equal(arg2, block.GetTemporary(1));
            
            // Stack pointer should be after arguments
            Assert.Equal(2, block.StackPointer);
            
            // IP should be reset to initial
            Assert.Equal(20, block.InstructionPointer);
        }
        
        [Fact]
        public void TestEvaluateWithWrongArgumentCount()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 2, 20);
            
            // Too few arguments
            Assert.Throws<ArgumentException>(() => 
                block.EvaluateWithArguments(new object[] { Tags.FromSmallInteger(1) }));
            
            // Too many arguments
            Assert.Throws<ArgumentException>(() =>
                block.EvaluateWithArguments(new object[] { 
                    Tags.FromSmallInteger(1), 
                    Tags.FromSmallInteger(2), 
                    Tags.FromSmallInteger(3) 
                }));
        }
        
        #endregion
        
        #region Captured Temporary Tests
        
        [Fact]
        public void TestCapturedTemporaryAccess()
        {
            var home = CreateHomeContext();
            
            // Set some temporaries in home
            home.SetTemporary(0, Tags.FromSmallInteger(10)); // Argument
            home.SetTemporary(1, SSymbol.Intern("captured1"));
            home.SetTemporary(2, SSymbol.Intern("captured2"));
            
            var block = new SBlockContext(_blockContextClass, home, 0, 20);
            
            // Block should access home's temporaries
            Assert.Equal(Tags.FromSmallInteger(10), block.GetCapturedTemporary(0));
            Assert.Equal(SSymbol.Intern("captured1"), block.GetCapturedTemporary(1));
            Assert.Equal(SSymbol.Intern("captured2"), block.GetCapturedTemporary(2));
            
            // Modify through block
            block.SetCapturedTemporary(1, SSymbol.Intern("modified"));
            Assert.Equal(SSymbol.Intern("modified"), home.GetTemporary(1));
        }
        
        [Fact]
        public void TestCapturedTemporaryWithoutHome()
        {
            var block = new SBlockContext(_blockContextClass);
            
            // Should return nil when no home
            Assert.Same(SNil.Instance, block.GetCapturedTemporary(0));
            
            // Set should do nothing
            block.SetCapturedTemporary(0, Tags.FromSmallInteger(42));
            Assert.Same(SNil.Instance, block.GetCapturedTemporary(0));
        }
        
        #endregion
        
        #region Non-Local Return Tests
        
        [Fact]
        public void TestNonLocalReturn()
        {
            // Create call chain: sender -> home -> intermediate -> block
            var sender = new SMethodContext(_methodContextClass);
            var home = CreateHomeContext();
            home.Sender = sender;
            
            var intermediate = new SMethodContext(_methodContextClass);
            intermediate.Sender = home;
            
            var block = new SBlockContext(_blockContextClass, home, 0, 20);
            block.Caller = intermediate;
            
            var returnValue = Tags.FromSmallInteger(99);
            var nextContext = block.NonLocalReturn(returnValue);
            
            // Should return to sender of home
            Assert.Same(sender, nextContext);
            
            // Sender should have return value
            Assert.Equal(returnValue, sender.Pop());
            
            // Home should be terminated
            Assert.Null(home.Sender);
            
            // Intermediate should be terminated
            Assert.Null(intermediate.Sender);
        }
        
        [Fact]
        public void TestNonLocalReturnWithoutHome()
        {
            var block = new SBlockContext(_blockContextClass);
            
            Assert.Throws<InvalidOperationException>(() =>
                block.NonLocalReturn(Tags.FromSmallInteger(42)));
        }
        
        [Fact]
        public void TestNonLocalReturnDirectToHome()
        {
            var sender = new SMethodContext(_methodContextClass);
            var home = CreateHomeContext();
            home.Sender = sender;
            
            var block = new SBlockContext(_blockContextClass, home, 0, 20);
            block.Caller = home; // Block called directly from home
            
            var returnValue = SSymbol.Intern("result");
            var nextContext = block.NonLocalReturn(returnValue);
            
            Assert.Same(sender, nextContext);
            Assert.Equal(returnValue, sender.Pop());
        }
        
        #endregion
        
        #region Normal Return Tests
        
        [Fact]
        public void TestNormalReturn()
        {
            var home = CreateHomeContext();
            var caller = new SMethodContext(_methodContextClass);
            
            var block = new SBlockContext(_blockContextClass, home, 0, 20);
            block.Caller = caller;
            
            var returnValue = Tags.FromSmallInteger(42);
            var nextContext = block.Return(returnValue);
            
            // Should return to caller (not home's sender)
            Assert.Same(caller, nextContext);
            
            // Caller should have return value
            Assert.Equal(returnValue, caller.Pop());
            
            // Block should be terminated
            Assert.Null(block.Caller);
        }
        
        #endregion
        
        #region Copy Test
        
        [Fact]
        public void TestCopy()
        {
            var home = CreateHomeContext();
            var original = new SBlockContext(_blockContextClass, home, 2, 25, 15);
            
            var copy = original.Copy(_blockContextClass);
            
            // Copy should have same parameters
            Assert.Same(home, copy.HomeContext);
            Assert.Equal(2, copy.ArgumentCount);
            Assert.Equal(25, copy.InitialIP);
            
            // But be a different instance
            Assert.NotSame(original, copy);
            
            // With fresh stack
            original.Push(Tags.FromSmallInteger(99));
            Assert.Equal(0, copy.StackPointer);
        }
        
        #endregion
        
        #region Terminate Test
        
        [Fact]
        public void TestTerminate()
        {
            var home = CreateHomeContext();
            var caller = new SMethodContext(_methodContextClass);
            
            var block = new SBlockContext(_blockContextClass, home, 1, 20);
            block.Caller = caller;
            block.EvaluateWithArguments(new object[] { Tags.FromSmallInteger(42) });
            block.Push(SSymbol.Intern("stack"));
            
            block.Terminate();
            
            // Check cleared
            Assert.Null(block.Caller);
            Assert.Same(block, block.HomeContext); // Falls back to self when home cleared
            
            // Arguments and stack should be nil
            Assert.Same(SNil.Instance, block.GetInstVar(SMethodContext.TempFrameStart));
            Assert.Same(SNil.Instance, block.GetInstVar(SMethodContext.TempFrameStart + 1));
        }
        
        #endregion
        
        #region ToString Test
        
        [Fact]
        public void TestToString()
        {
            var home = CreateHomeContext();
            var block = new SBlockContext(_blockContextClass, home, 2, 25);
            
            var str = block.ToString();
            Assert.Contains("BlockContext", str);
            Assert.Contains("testMethod:", str); // From home's method
            Assert.Contains("args=2", str);
        }
        
        #endregion
    }
}