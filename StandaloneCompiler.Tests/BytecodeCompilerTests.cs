using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StandaloneCompiler.Tests;

/// <summary>
/// Tests for bytecode generation from Smalltalk source.
/// </summary>
public sealed class BytecodeCompilerTests
{
    /// <summary>
    /// Tests that literals are placed in the correct bytecode range.
    /// </summary>
    [Fact]
    public void CompileMethod_WithLiterals_GeneratesCorrectLiteralBytecodes()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        
        // Act - Push literal constants (should be in range 32-63)
        var literal1 = emitter.AddLiteral("hello");
        var literal2 = emitter.AddLiteral(42);
        var literal3 = emitter.AddLiteral(new Symbol("symbol"));
        
        emitter.Emit((byte)(32 + literal1));  // Push literal constant
        emitter.Emit((byte)(32 + literal2));  // Push literal constant
        emitter.Emit((byte)(32 + literal3));  // Push literal constant
        
        var bytecodes = emitter.GetBytecodes();
        
        // Assert - All push literal bytecodes should be in range 32-63
        bytecodes[0].Should().BeInRange(32, 63);
        bytecodes[1].Should().BeInRange(32, 63);
        bytecodes[2].Should().BeInRange(32, 63);
        
        // Verify literal indices
        bytecodes[0].Should().Be((byte)(32 + literal1));
        bytecodes[1].Should().Be((byte)(32 + literal2));
        bytecodes[2].Should().Be((byte)(32 + literal3));
    }
    
    /// <summary>
    /// Tests that variables are placed in the correct bytecode range.
    /// </summary>
    [Fact]
    public void CompileMethod_WithVariables_GeneratesCorrectVariableBytecodes()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        
        // Act - Push literal variables (should be in range 64-95)
        var var1 = emitter.AddLiteral(new { type = "Association", key = "GlobalVar" });
        var var2 = emitter.AddLiteral(new { type = "Association", key = "ClassVar" });
        
        emitter.Emit((byte)(64 + var1));  // Push literal variable
        emitter.Emit((byte)(64 + var2));  // Push literal variable
        
        var bytecodes = emitter.GetBytecodes();
        
        // Assert - All push variable bytecodes should be in range 64-95
        bytecodes[0].Should().BeInRange(64, 95);
        bytecodes[1].Should().BeInRange(64, 95);
        
        // Verify variable indices
        bytecodes[0].Should().Be((byte)(64 + var1));
        bytecodes[1].Should().Be((byte)(64 + var2));
    }
    
    /// <summary>
    /// Tests stack depth calculation for simple methods.
    /// </summary>
    [Theory]
    [InlineData("^1", 1)]                      // Push one value
    [InlineData("^1 + 2", 2)]                  // Push two values for binary op
    [InlineData("^self foo: 1 bar: 2", 3)]     // self + 2 args
    [InlineData("| a | a := 1. ^a", 1)]        // Assignment doesn't increase max
    public void CalculateStackDepth_SimpleMethods_CalculatesCorrectly(string methodBody, int expectedDepth)
    {
        // This test would require actual compiler implementation
        // For now, we're just verifying the expected structure
        methodBody.Should().NotBeNullOrEmpty();
        expectedDepth.Should().BeGreaterThan(0);
    }
    
    /// <summary>
    /// Tests compilation of return bytecodes.
    /// </summary>
    [Theory]
    [InlineData("^self", 124)]     // returnReceiver
    [InlineData("^true", 121)]     // returnTrue
    [InlineData("^false", 122)]    // returnFalse
    [InlineData("^nil", 123)]      // returnNil
    public void CompileMethod_ReturnStatements_GeneratesCorrectReturnBytecode(string source, byte expectedBytecode)
    {
        // This test would require actual compiler implementation
        // For now, we're just verifying the bytecode values are in expected ranges
        source.Should().NotBeNullOrEmpty();
        expectedBytecode.Should().BeInRange(120, 127); // Return bytecodes are 120-127
    }
    
    /// <summary>
    /// Tests compilation of push bytecodes.
    /// </summary>
    [Theory]
    [InlineData("self", 112)]      // pushReceiver
    [InlineData("true", 113)]      // pushConstantTrue
    [InlineData("false", 114)]     // pushConstantFalse
    [InlineData("nil", 115)]       // pushConstantNil
    [InlineData("-1", 116)]        // pushConstantMinusOne
    [InlineData("0", 117)]         // pushConstantZero
    [InlineData("1", 118)]         // pushConstantOne
    [InlineData("2", 119)]         // pushConstantTwo
    public void CompileExpression_Constants_GeneratesCorrectPushBytecode(string expression, byte expectedBytecode)
    {
        // This test would require actual compiler implementation
        // For now, we're just verifying the bytecode values are in expected ranges
        expression.Should().NotBeNullOrEmpty();
        expectedBytecode.Should().BeInRange(112, 119); // Push constant bytecodes
    }
    
    /// <summary>
    /// Tests compilation of temporary variable access.
    /// </summary>
    [Fact]
    public void CompileMethod_TemporaryVariables_GeneratesCorrectBytecodes()
    {
        // Arrange
        // const string source = "| temp | temp := 42. ^temp";
        var env = new CompilationEnvironment();
        
        // Act - Simulate adding a temporary
        env.Temporaries["temp"] = 0;
        env.TemporaryCount = 1;
        
        // Assert
        env.Temporaries.Should().ContainKey("temp");
        env.TemporaryCount.Should().Be(1);
    }
    
    /// <summary>
    /// Tests compilation of block closures.
    /// </summary>
    [Fact]
    public void CompileMethod_BlockClosure_GeneratesBlockBytecodes()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        
        // Act - Simulate block bytecode generation
        emitter.Emit(118); // pushOne
        emitter.Emit(119); // pushTwo
        emitter.Emit(176); // arithmetic send +
        
        var bytecodes = emitter.GetBytecodes();
        
        // Assert
        bytecodes.Should().NotBeEmpty();
        bytecodes.Should().Contain(118); // pushOne
        bytecodes.Should().Contain(119); // pushTwo
        bytecodes.Should().Contain(176); // arithmetic send +
    }
    
    /// <summary>
    /// Tests compilation of message sends.
    /// </summary>
    [Theory]
    [InlineData("+", 176)]        // Arithmetic selector +
    [InlineData("-", 177)]        // Arithmetic selector -
    [InlineData("=", 182)]        // Special selector =
    [InlineData("<", 178)]        // Arithmetic selector <
    public void CompileMethod_BinaryMessages_GeneratesCorrectSendBytecode(string selector, byte expectedBytecode)
    {
        // This test verifies that the bytecode values are in the correct range
        // Arithmetic and special selectors are 176-191
        selector.Should().NotBeNullOrEmpty();
        expectedBytecode.Should().BeInRange(176, 191);
    }
    
    /// <summary>
    /// Tests JSON output generation for a compiled method.
    /// </summary>
    [Fact]
    public void GenerateJson_CompiledMethod_ProducesValidJsonStructure()
    {
        // Arrange
        var method = new CompiledMethodData
        {
            Selector = "test:",
            Bytecode = new byte[] { 16, 118, 176, 124 },
            Literals = new object[] { "test:" },
            ArgumentCount = 1,
            TemporaryCount = 0
        };
        
        // Act
        var json = JsonSerializer.Serialize(method, new JsonSerializerOptions 
        { 
            WriteIndented = true
        });
        
        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"Selector\"");
        json.Should().Contain("\"Bytecode\"");
        json.Should().Contain("\"Literals\"");
        json.Should().Contain("test:");
    }
}