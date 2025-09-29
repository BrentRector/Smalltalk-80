namespace Tests;

using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using Xunit;

/// <summary>
/// Tests for the Float implementation to ensure Blue Book compliance.
/// </summary>
public class TestFloat
{
    /// <summary>
    /// Initializes a new instance of the TestFloat class and sets up the system.
    /// </summary>
    public TestFloat()
    {
        SystemDictionary.Reset();
        SystemDictionary.Initialize();
        var interpreter = new BytecodeInterpreter();
        ColdStartBootstrap.PostColdStartInitialization(interpreter);
    }
    
    /// <summary>
    /// Tests that Float class is properly created without primitives 146/147.
    /// </summary>
    [Fact]
    public void TestFloatClassCreation()
    {
        // Float class should exist
        var floatClass = SystemDictionary.LookupClass("Float");
        Assert.NotNull(floatClass);
        Assert.Equal("Float", floatClass.Name);
        
        // Float should be a subclass of Number
        var numberClass = SystemDictionary.LookupClass("Number");
        Assert.NotNull(numberClass);
        Assert.Equal(numberClass, floatClass.Superclass);
        
        // Number should be a subclass of Object (or Magnitude if it exists)
        var magnitudeClass = SystemDictionary.LookupClass("Magnitude");
        if (magnitudeClass != null)
        {
            Assert.Equal(magnitudeClass, numberClass.Superclass);
        }
        else
        {
            Assert.Equal(SystemDictionary.ObjectClass, numberClass.Superclass);
        }
    }
    
    /// <summary>
    /// Tests that primitive 40 (asFloat) works for SmallInteger.
    /// </summary>
    [Fact]
    public void TestSmallIntegerAsFloat()
    {
        var integer = SmallInteger.From(42);
        var result = PrimitiveDispatcher.ExecutePrimitive(40, integer, []);
        
        Assert.NotNull(result);
        Assert.IsType<SFloat>(result);
        var floatResult = (SFloat)result;
        Assert.Equal(42.0, floatResult.Value);
    }
    
    /// <summary>
    /// Tests Float arithmetic primitives (41-50).
    /// </summary>
    [Fact]
    public void TestFloatArithmetic()
    {
        var float1 = SFloat.From(3.14);
        var float2 = SFloat.From(2.86);
        
        // Test addition (primitive 41)
        var addResult = PrimitiveDispatcher.ExecutePrimitive(41, float1, [float2]);
        Assert.NotNull(addResult);
        Assert.IsType<SFloat>(addResult);
        Assert.Equal(6.0, ((SFloat)addResult).Value, 0.001);
        
        // Test subtraction (primitive 42)
        var subResult = PrimitiveDispatcher.ExecutePrimitive(42, float1, [float2]);
        Assert.NotNull(subResult);
        Assert.IsType<SFloat>(subResult);
        Assert.Equal(0.28, ((SFloat)subResult).Value, 0.001);
        
        // Test multiplication (primitive 49)
        var mulResult = PrimitiveDispatcher.ExecutePrimitive(49, float1, [float2]);
        Assert.NotNull(mulResult);
        Assert.IsType<SFloat>(mulResult);
        Assert.Equal(8.9804, ((SFloat)mulResult).Value, 0.0001);
        
        // Test division (primitive 50)
        var divResult = PrimitiveDispatcher.ExecutePrimitive(50, float1, [float2]);
        Assert.NotNull(divResult);
        Assert.IsType<SFloat>(divResult);
        Assert.Equal(1.0979, ((SFloat)divResult).Value, 0.0001);
    }
    
    /// <summary>
    /// Tests Float comparison primitives (43-48).
    /// </summary>
    [Fact]
    public void TestFloatComparison()
    {
        var float1 = SFloat.From(3.14);
        var float2 = SFloat.From(2.86);
        var float3 = SFloat.From(3.14);
        
        // Test less than (primitive 43)
        var ltResult = PrimitiveDispatcher.ExecutePrimitive(43, float2, [float1]);
        Assert.Equal(STrue.Instance, ltResult);
        
        // Test greater than (primitive 44)
        var gtResult = PrimitiveDispatcher.ExecutePrimitive(44, float1, [float2]);
        Assert.Equal(STrue.Instance, gtResult);
        
        // Test equality (primitive 47)
        var eqResult = PrimitiveDispatcher.ExecutePrimitive(47, float1, [float3]);
        Assert.Equal(STrue.Instance, eqResult);
        
        // Test inequality (primitive 48)
        var neResult = PrimitiveDispatcher.ExecutePrimitive(48, float1, [float2]);
        Assert.Equal(STrue.Instance, neResult);
    }
    
    /// <summary>
    /// Tests Float truncate primitive (51).
    /// </summary>
    [Fact]
    public void TestFloatTruncate()
    {
        var float1 = SFloat.From(3.14);
        var truncResult = PrimitiveDispatcher.ExecutePrimitive(51, float1, []);
        
        Assert.NotNull(truncResult);
        Assert.IsType<SmallInteger>(truncResult);
        Assert.Equal(3, ((SmallInteger)truncResult).Value);
    }
    
    /// <summary>
    /// Tests that no primitive 146 or 147 exists.
    /// </summary>
    [Fact]
    public void TestNoPrimitives146Or147()
    {
        // Primitive 146 should not exist
        var dummyClass = SystemDictionary.ObjectClass;
        var result146 = PrimitiveDispatcher.ExecutePrimitive(146, dummyClass, 
            [SSymbol.Intern("Test"), new SString(""), new SString(""), new SString(""), new SString("")]);
        Assert.Null(result146);
        
        // Primitive 147 should not exist
        var result147 = PrimitiveDispatcher.ExecutePrimitive(147, dummyClass,
            [SSymbol.Intern("Test"), new SString(""), new SString(""), new SString(""), new SString("")]);
        Assert.Null(result147);
    }
    
    /// <summary>
    /// Tests that Float class has correct methods.
    /// </summary>
    [Fact]
    public void TestFloatMethods()
    {
        var floatClass = SystemDictionary.LookupClass("Float");
        Assert.NotNull(floatClass);
        
        // Check that Float has arithmetic methods
        Assert.NotNull(floatClass.LookupMethod("+"));
        Assert.NotNull(floatClass.LookupMethod("-"));
        Assert.NotNull(floatClass.LookupMethod("*"));
        Assert.NotNull(floatClass.LookupMethod("/"));
        
        // Check that Float has comparison methods
        Assert.NotNull(floatClass.LookupMethod("<"));
        Assert.NotNull(floatClass.LookupMethod(">"));
        Assert.NotNull(floatClass.LookupMethod("="));
        
        // Check that Float has conversion methods
        Assert.NotNull(floatClass.LookupMethod("asFloat"));
        Assert.NotNull(floatClass.LookupMethod("truncate"));
    }
    
    /// <summary>
    /// Tests that SmallInteger has asFloat method.
    /// </summary>
    [Fact]
    public void TestSmallIntegerHasAsFloat()
    {
        var smallIntegerClass = SystemDictionary.SmallIntegerClass;
        Assert.NotNull(smallIntegerClass);
        
        var asFloatMethod = smallIntegerClass.LookupMethod("asFloat");
        Assert.NotNull(asFloatMethod);
        Assert.Equal(40, asFloatMethod.PrimitiveIndex); // Should use primitive 40
    }
}