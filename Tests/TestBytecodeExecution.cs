using System;
using Xunit;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Primitives;

namespace Tests;

/// <summary>
/// Comprehensive tests for bytecode execution scenarios.
/// </summary>
public sealed class TestBytecodeExecution
{
    private readonly Kernel _kernel;
    private readonly MethodLookupCache _methodCache;
    private readonly BytecodeInterpreter _interpreter;
    
    public TestBytecodeExecution()
    {
        _kernel = new Kernel();
        _kernel.Initialize();
        _methodCache = new MethodLookupCache(256);
        _interpreter = new BytecodeInterpreter(_kernel, _methodCache);
    }
    
    /// <summary>
    /// Tests push receiver variable bytecode (0-15).
    /// </summary>
    [Fact]
    public void TestPushReceiverVariable()
    {
        // Create a test object with instance variables
        var testClass = new SClass(_kernel.ObjectClass, "TestClass", 3);
        var receiver = new SObject(testClass, 3);
        receiver.InstVars![0] = Tags.FromSmallInteger(42);
        receiver.InstVars![1] = STrue.Instance;
        receiver.InstVars![2] = SNil.Instance;
        
        // Create method that pushes receiver variables
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 3),
            Bytecode = [
                0,   // Push receiver variable 0 (42)
                1,   // Push receiver variable 1 (true)
                2,   // Push receiver variable 2 (nil)
                124  // Return stack top
            ],
            Literals = []
        };
        
        // Create context and execute
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return nil (last pushed value)
        Assert.Same(SNil.Instance, result);
        
        // Check stack contents
        Assert.Equal(2, context.StackPointer);
        Assert.True(Tags.IsSmallInteger(context.StackValue(1)));
        Assert.Equal(42, Tags.UntagInt(context.StackValue(1)));
        Assert.Same(STrue.Instance, context.StackValue(0));
    }
    
    /// <summary>
    /// Tests push temporary variable bytecode (16-31).
    /// </summary>
    [Fact]
    public void TestPushTemporaryVariable()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(2, 3, 0, false, 3), // 2 args, 3 temps
            Bytecode = [
                16,  // Push temp 0 (first arg)
                17,  // Push temp 1 (second arg)
                18,  // Push temp 2 (local temp)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var receiver = SNil.Instance;
        var args = new[] { Tags.FromSmallInteger(10), Tags.FromSmallInteger(20) };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, args);
        context.SetTemporary(2, Tags.FromSmallInteger(30)); // Set local temp
        
        _interpreter.ActiveContext = context;
        var result = _interpreter.Execute();
        
        // Should return 30 (last pushed value)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(30, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests push literal constant bytecode (32-63).
    /// </summary>
    [Fact]
    public void TestPushLiteralConstant()
    {
        var symbol = _kernel.InternSymbol("test");
        var array = new SArray(_kernel.ArrayClass, 2);
        array.Elements![0] = Tags.FromSmallInteger(100);
        array.Elements![1] = symbol;
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 3, false, 3),
            Bytecode = [
                32,  // Push literal 0 (symbol)
                33,  // Push literal 1 (array)
                34,  // Push literal 2 (nil - out of bounds)
                124  // Return stack top
            ],
            Literals = [symbol, array]
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return nil (out of bounds literal)
        Assert.Same(SNil.Instance, result);
        
        // Check stack
        Assert.Equal(2, context.StackPointer);
        Assert.Same(symbol, context.StackValue(1));
        Assert.Same(array, context.StackValue(0));
    }
    
    /// <summary>
    /// Tests special constant push bytecodes (112-119).
    /// </summary>
    [Fact]
    public void TestPushSpecialConstants()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 8),
            Bytecode = [
                112, // Push receiver
                113, // Push true
                114, // Push false
                115, // Push nil
                116, // Push -1
                117, // Push 0
                118, // Push 1
                119, // Push 2
                124  // Return stack top
            ],
            Literals = []
        };
        
        var receiver = _kernel.InternSymbol("receiver");
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return 2 (last pushed value)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(2, Tags.UntagInt(result));
        
        // Check all pushed values
        Assert.Equal(7, context.StackPointer);
        Assert.Same(receiver, context.StackValue(6));
        Assert.Same(STrue.Instance, context.StackValue(5));
        Assert.Same(SFalse.Instance, context.StackValue(4));
        Assert.Same(SNil.Instance, context.StackValue(3));
        Assert.Equal(-1, Tags.UntagInt(context.StackValue(2)));
        Assert.Equal(0, Tags.UntagInt(context.StackValue(1)));
        Assert.Equal(1, Tags.UntagInt(context.StackValue(0)));
    }
    
    /// <summary>
    /// Tests return bytecodes (104-111).
    /// </summary>
    [Fact]
    public void TestReturnOperations()
    {
        // Test return receiver (104)
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 1),
            Bytecode = [104], // Return receiver
            Literals = []
        };
        
        var receiver = Tags.FromSmallInteger(42);
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        Assert.Equal(42, Tags.UntagInt(result));
        
        // Test return true (105)
        method.Bytecode = [105];
        context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        Assert.Same(STrue.Instance, result);
        
        // Test return false (106)
        method.Bytecode = [106];
        context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        Assert.Same(SFalse.Instance, result);
        
        // Test return nil (107)
        method.Bytecode = [107];
        context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        Assert.Same(SNil.Instance, result);
        
        // Test return stack top (108)
        method.Bytecode = [118, 108]; // Push 1, return stack top
        context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        Assert.Equal(1, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests stack operations (96-103).
    /// </summary>
    [Fact]
    public void TestStackOperations()
    {
        var testClass = new SClass(_kernel.ObjectClass, "TestClass", 8);
        var receiver = new SObject(testClass, 8);
        
        // Initialize receiver variables
        for (int i = 0; i < 8; i++)
        {
            receiver.InstVars![i] = Tags.FromSmallInteger(i * 10);
        }
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 8),
            Bytecode = [
                118, // Push 1
                96,  // Pop into receiver variable 0
                119, // Push 2
                97,  // Pop into receiver variable 1
                0,   // Push receiver variable 0 (should be 1 now)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return 1 (value stored in receiver var 0)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(1, Tags.UntagInt(result));
        
        // Check receiver variables were modified
        Assert.Equal(1, Tags.UntagInt(receiver.InstVars![0]));
        Assert.Equal(2, Tags.UntagInt(receiver.InstVars![1]));
    }
    
    /// <summary>
    /// Tests special selector sends for SmallInteger arithmetic (120-127).
    /// </summary>
    [Fact]
    public void TestSpecialSelectorArithmetic()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 10),
            Bytecode = [
                118, // Push 1
                119, // Push 2
                120, // Send + (special selector 0)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // 1 + 2 = 3
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(3, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests special selector comparison (120-127).
    /// </summary>
    [Fact]
    public void TestSpecialSelectorComparison()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 10),
            Bytecode = [
                118, // Push 1
                119, // Push 2
                122, // Send < (special selector 2)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // 1 < 2 = true
        Assert.Same(STrue.Instance, result);
        
        // Test greater than
        method.Bytecode = [
            119, // Push 2
            118, // Push 1
            123, // Send > (special selector 3)
            124  // Return stack top
        ];
        
        context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        
        // 2 > 1 = true
        Assert.Same(STrue.Instance, result);
    }
    
    /// <summary>
    /// Tests extended jump operations.
    /// </summary>
    [Fact]
    public void TestJumpOperations()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 5),
            Bytecode = [
                117,      // Push 0
                168,      // Jump forward 1
                118,      // Push 1 (skipped)
                119,      // Push 2
                124       // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return 2 (jumped over push 1)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(2, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests conditional jump operations.
    /// </summary>
    [Fact]
    public void TestConditionalJumps()
    {
        // Test jump if false
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 5),
            Bytecode = [
                114,      // Push false
                172, 2,   // Jump if false +2
                118,      // Push 1 (skipped)
                119,      // Push 2
                124       // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return 2 (jumped over push 1)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(2, Tags.UntagInt(result));
        
        // Test jump if true
        method.Bytecode = [
            113,      // Push true
            173, 2,   // Jump if true +2
            118,      // Push 1 (skipped)
            119,      // Push 2
            124       // Return stack top
        ];
        
        context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        result = _interpreter.Execute();
        
        // Should return 2 (jumped over push 1)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(2, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests duplicate and pop stack operations.
    /// </summary>
    [Fact]
    public void TestDuplicateAndPop()
    {
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 5),
            Bytecode = [
                118,      // Push 1
                138, 0,   // Duplicate
                137, 0,   // Pop
                124       // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should return 1 (duplicated then popped one)
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(1, Tags.UntagInt(result));
    }
    
    /// <summary>
    /// Tests execution statistics tracking.
    /// </summary>
    [Fact]
    public void TestExecutionStatistics()
    {
        _interpreter.Statistics.Reset();
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 5),
            Bytecode = [
                118, // Push 1
                119, // Push 2  
                120, // Send + (special selector)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var context = new SMethodContext(_kernel.MethodContextClass, method, SNil.Instance, []);
        _interpreter.ActiveContext = context;
        
        _interpreter.Execute();
        
        // Should have executed 4 bytecodes
        Assert.True(_interpreter.Statistics.BytecodeCount >= 4);
        
        // Should have one special send hit
        Assert.True(_interpreter.Statistics.SpecialSendHits >= 1);
    }
    
    /// <summary>
    /// Tests method with primitive that succeeds.
    /// </summary>
    [Fact]
    public void TestPrimitiveSuccess()
    {
        // Register a test primitive
        PrimitiveTable.RegisterPrimitive(250, (receiver, args, out result) =>
        {
            if (Tags.TryUnboxSmallInteger(receiver, out var value))
            {
                result = Tags.FromSmallInteger(value * 2);
                return true;
            }
            result = SNil.Instance;
            return false;
        });
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, true, 1),
            PrimitiveIndex = 250,
            Bytecode = [
                118, // Push 1 (not executed if primitive succeeds)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var receiver = Tags.FromSmallInteger(21);
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Primitive should have doubled the receiver
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(42, Tags.UntagInt(result));
        
        // Statistics should show primitive hit
        Assert.True(_interpreter.Statistics.PrimitiveHits > 0);
    }
    
    /// <summary>
    /// Tests method with primitive that fails and falls through to bytecode.
    /// </summary>
    [Fact]
    public void TestPrimitiveFallthrough()
    {
        // Register a test primitive that always fails
        PrimitiveTable.RegisterPrimitive(251, (receiver, args, out result) =>
        {
            result = SNil.Instance;
            return false; // Always fail
        });
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, true, 1),
            PrimitiveIndex = 251,
            Bytecode = [
                118, // Push 1 (executed since primitive fails)
                124  // Return stack top
            ],
            Literals = []
        };
        
        var receiver = Tags.FromSmallInteger(42);
        var context = new SMethodContext(_kernel.MethodContextClass, method, receiver, []);
        _interpreter.ActiveContext = context;
        
        var result = _interpreter.Execute();
        
        // Should execute bytecode and return 1
        Assert.True(Tags.IsSmallInteger(result));
        Assert.Equal(1, Tags.UntagInt(result));
        
        // Statistics should show primitive fail
        Assert.True(_interpreter.Statistics.PrimitiveFails > 0);
    }
}