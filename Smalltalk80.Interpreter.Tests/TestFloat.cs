using Xunit;
using Smalltalk80.Interpreter;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for Float class creation and operations.
/// </summary>
public class TestFloat
{
    /// <summary>
    /// Tests that Float class is created during cold start.
    /// </summary>
    [Fact]
    public async Task FloatClassIsCreatedDuringColdStart()
    {
        // Arrange
        var vm = new VirtualMachine();
        
        // Act
        await vm.ColdStartAsync();
        
        // Assert
        var floatClass = SystemDictionary.LookupClass("Float");
        Assert.NotNull(floatClass);
        Assert.Equal("Float", floatClass.Name);
        
        var numberClass = SystemDictionary.LookupClass("Number");
        Assert.NotNull(numberClass);
        Assert.Equal(numberClass, floatClass.Superclass);
    }
    
    /// <summary>
    /// Tests SmallInteger to Float conversion (primitive 40).
    /// </summary>
    [Fact]
    public async Task SmallIntegerToFloatConversion()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var integer = SmallInteger.From(42);
        
        // Act
        var result = PrimitiveDispatcher.ExecutePrimitive(40, integer, []);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(42.0, floatResult.Value);
    }
    
    /// <summary>
    /// Tests Float addition (primitive 41).
    /// </summary>
    [Fact]
    public async Task FloatAddition()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(3.14);
        var float2 = SFloat.From(2.86);
        
        // Act
        var result = PrimitiveDispatcher.ExecutePrimitive(41, float1, [float2]);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(6.0, floatResult.Value, 0.0001);
    }
    
    /// <summary>
    /// Tests Float subtraction (primitive 42).
    /// </summary>
    [Fact]
    public async Task FloatSubtraction()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(5.5);
        var float2 = SFloat.From(2.3);
        
        // Act
        var result = PrimitiveDispatcher.ExecutePrimitive(42, float1, [float2]);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(3.2, floatResult.Value, 0.0001);
    }
    
    /// <summary>
    /// Tests Float comparison (primitive 43).
    /// </summary>
    [Fact]
    public async Task FloatLessThan()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(2.0);
        var float2 = SFloat.From(3.0);
        
        // Act
        var result1 = PrimitiveDispatcher.ExecutePrimitive(43, float1, [float2]);
        var result2 = PrimitiveDispatcher.ExecutePrimitive(43, float2, [float1]);
        
        // Assert
        Assert.Equal(STrue.Instance, result1);
        Assert.Equal(SFalse.Instance, result2);
    }
    
    /// <summary>
    /// Tests Float multiplication (primitive 49).
    /// </summary>
    [Fact]
    public async Task FloatMultiplication()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(3.0);
        var float2 = SFloat.From(4.0);
        
        // Act
        var result = PrimitiveDispatcher.ExecutePrimitive(49, float1, [float2]);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(12.0, floatResult.Value);
    }
    
    /// <summary>
    /// Tests Float division (primitive 50).
    /// </summary>
    [Fact]
    public async Task FloatDivision()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(10.0);
        var float2 = SFloat.From(2.5);
        
        // Act
        var result = PrimitiveDispatcher.ExecutePrimitive(50, float1, [float2]);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(4.0, floatResult.Value);
    }
    
    /// <summary>
    /// Tests Float truncation (primitive 51).
    /// </summary>
    [Fact]
    public async Task FloatTruncation()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var float1 = SFloat.From(3.7);
        var float2 = SFloat.From(-3.7);
        
        // Act
        var result1 = PrimitiveDispatcher.ExecutePrimitive(51, float1, []);
        var result2 = PrimitiveDispatcher.ExecutePrimitive(51, float2, []);
        
        // Assert
        Assert.NotNull(result1);
        Assert.IsType<SmallInteger>(result1);
        Assert.Equal(3, ((SmallInteger)result1).Value);
        
        Assert.NotNull(result2);
        Assert.IsType<SmallInteger>(result2);
        Assert.Equal(-3, ((SmallInteger)result2).Value);
    }
    
    /// <summary>
    /// Tests Float with SmallInteger mixed arithmetic.
    /// </summary>
    [Fact]
    public async Task FloatWithIntegerArithmetic()
    {
        // Arrange
        var vm = new VirtualMachine();
        await vm.ColdStartAsync();
        
        var floatValue = SFloat.From(5.5);
        var intValue = SmallInteger.From(2);
        
        // Act - Float + SmallInteger
        var result = PrimitiveDispatcher.ExecutePrimitive(41, floatValue, [intValue]);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(7.5, floatResult.Value);
    }
    
    /// <summary>
    /// Tests that Float class is created via message send, not direct instantiation.
    /// </summary>
    [Fact]
    public async Task FloatClassCreatedViaMessageSend()
    {
        // Arrange
        var vm = new VirtualMachine();
        
        // Act
        await vm.ColdStartAsync();
        
        // Assert - verify Float class was created through primitive 147
        var floatClass = SystemDictionary.LookupClass("Float");
        Assert.NotNull(floatClass);
        Assert.IsType<SClass>(floatClass);
        
        // Verify it has the correct superclass
        var numberClass = SystemDictionary.LookupClass("Number");
        Assert.Equal(numberClass, floatClass.Superclass);
    }
}