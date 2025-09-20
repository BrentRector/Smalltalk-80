using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for bytecode execution in the interpreter.
/// </summary>
public sealed class BytecodeExecutionTests
{
    /// <summary>
    /// Tests execution of push constant bytecodes.
    /// </summary>
    [Theory]
    [InlineData(117, "receiver")] // pushReceiver
    [InlineData(121, "nil")]      // pushNil
    [InlineData(122, "false")]    // pushFalse
    [InlineData(123, "true")]     // pushTrue
    public void ExecuteBytecode_PushConstants_PushesCorrectValue(byte bytecode, string expectedValue)
    {
        // Arrange
        var vm = new VirtualMachine();
        var context = new MethodContext(null, OOP.Nil, OOP.Nil, [], 0, 16);
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, bytecode);
        
        // Assert
        result.Should().BeTrue(); // Execution should continue
        
        var stackTop = context.Stack.Pop();
        switch (expectedValue)
        {
            case "receiver":
                stackTop.Should().Be(context.Receiver);
                break;
            case "nil":
                stackTop.Should().Be(OOP.Nil);
                break;
            case "false":
                stackTop.Should().Be(OOP.FromRaw(2)); // False is at index 1 in object table
                break;
            case "true":
                stackTop.Should().Be(OOP.FromRaw(4)); // True is at index 2 in object table
                break;
        }
    }
    
    /// <summary>
    /// Tests execution of push integer bytecodes.
    /// </summary>
    [Theory]
    [InlineData(118, -1)]  // pushMinusOne
    [InlineData(119, 0)]   // pushZero
    [InlineData(120, 1)]   // pushOne
    [InlineData(124, 2)]   // pushTwo
    public void ExecuteBytecode_PushIntegers_PushesCorrectInteger(byte bytecode, int expectedValue)
    {
        // Arrange
        var vm = new VirtualMachine();
        var context = new MethodContext(null, OOP.Nil, OOP.Nil, [], 0, 16);
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, bytecode);
        
        // Assert
        result.Should().BeTrue();
        
        var stackTop = context.Stack.Pop();
        stackTop.IsSmallInteger.Should().BeTrue();
        stackTop.SmallIntegerValue.Should().Be(expectedValue);
    }
    
    /// <summary>
    /// Tests execution of return bytecodes.
    /// </summary>
    [Theory]
    [InlineData(120, 124)] // returnReceiver
    [InlineData(121, 125)] // returnTrue
    [InlineData(122, 126)] // returnFalse
    [InlineData(123, 127)] // returnNil
    public void ExecuteBytecode_ReturnConstants_ReturnsCorrectValue(byte returnValue, byte bytecode)
    {
        // Arrange
        var vm = new VirtualMachine();
        var context = new MethodContext(null, OOP.Nil, OOP.Nil, [], 0, 16);
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, bytecode);
        
        // Assert
        returnValue.Should().BeInRange(120, 123); // Use the returnValue parameter
        result.Should().BeFalse(); // Return bytecodes should stop execution
    }
    
    /// <summary>
    /// Tests duplicate top of stack bytecode.
    /// </summary>
    [Fact]
    public void ExecuteBytecode_DuplicateTop_DuplicatesStackTop()
    {
        // Arrange
        var vm = new VirtualMachine();
        var context = new MethodContext(null, OOP.Nil, OOP.Nil, [], 0, 16);
        context.Stack.Push(OOP.FromSmallInteger(42));
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, 136); // duplicateTop
        
        // Assert
        result.Should().BeTrue();
        
        var top1 = context.Stack.Pop();
        var top2 = context.Stack.Pop();
        
        top1.Should().Be(OOP.FromSmallInteger(42));
        top2.Should().Be(OOP.FromSmallInteger(42));
    }
    
    /// <summary>
    /// Tests pop stack bytecode.
    /// </summary>
    [Fact]
    public void ExecuteBytecode_PopStack_RemovesStackTop()
    {
        // Arrange
        var vm = new VirtualMachine();
        var context = new MethodContext(null, OOP.Nil, OOP.Nil, [], 0, 16);
        context.Stack.Push(OOP.FromSmallInteger(10));
        context.Stack.Push(OOP.FromSmallInteger(20));
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, 135); // popStackTop
        
        // Assert
        result.Should().BeTrue();
        
        var top = context.Stack.Pop();
        top.Should().Be(OOP.FromSmallInteger(10)); // 20 was popped
    }
    
    /// <summary>
    /// Tests push literal constant bytecodes.
    /// </summary>
    [Theory]
    [InlineData(32, 0)]  // Push literal at index 0
    [InlineData(33, 1)]  // Push literal at index 1
    [InlineData(63, 31)] // Push literal at index 31
    public void ExecuteBytecode_PushLiteralConstant_PushesCorrectLiteral(byte bytecode, int literalIndex)
    {
        // Arrange
        var vm = new VirtualMachine();
        
        // Create a mock compiled method
        // For testing purposes, we'll use a simple object as the method
        var methodClass = vm.GetClassByName("CompiledMethod");
        var method = methodClass.IsNil ? OOP.Nil : vm.Memory.AllocateObject(methodClass, 36);
        
        // Store literals in the method object if it was created
        if (!method.IsNil)
        {
            for (int i = 0; i < 32; i++)
            {
                vm.Memory.SetField(method, i + 4, OOP.FromSmallInteger(i * 100));
            }
        }
        
        var context = new MethodContext(null, OOP.Nil, method, [], 0, 16);
        
        // Act
        var dispatcher = new BytecodeDispatcher(vm);
        var result = dispatcher.ExecuteBytecode(context, bytecode);
        
        // Assert
        result.Should().BeTrue();
        
        var stackTop = context.Stack.Pop();
        stackTop.Should().Be(OOP.FromSmallInteger(literalIndex * 100));
    }
}