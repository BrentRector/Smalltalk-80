using Smalltalk80.Compiler;
using Smalltalk80.Core.Bytecodes;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.Compiler;

/// <summary>
/// Comprehensive unit tests for BytecodeEmitter.
/// Tests bytecode emission, literal management, and jump operations.
/// </summary>
public class BytecodeEmitterTests
{
    #region Basic Emit Tests

    [Fact]
    public void Emit_SingleByte_AddsToBytecodesArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Emit(0x70);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x70, bytecodes[0]);
    }

    [Fact]
    public void Emit_BytecodeEnum_AddsCorrectValue()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Emit(0x70);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x70, bytecodes[0]);
    }

    [Fact]
    public void EmitBytes_MultipleBytes_AddsAllToArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitBytes(0x10, 0x11, 0x12);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x10, bytecodes[0]);
        Assert.Equal(0x11, bytecodes[1]);
        Assert.Equal(0x12, bytecodes[2]);
    }

    [Fact]
    public void EmitByte_SingleValue_AddsToBytecodesArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitByte(0x73);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x73, bytecodes[0]);
    }

    #endregion

    #region Push Operations Tests

    [Fact]
    public void PushSelf_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushSelf();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x70, bytecodes[0]);
    }

    [Fact]
    public void PushNil_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushNil();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x73, bytecodes[0]);
    }

    [Fact]
    public void PushTrue_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushTrue();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x71, bytecodes[0]);
    }

    [Fact]
    public void PushFalse_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushFalse();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x72, bytecodes[0]);
    }

    [Theory]
    [InlineData(-1, 0x74)]
    [InlineData(0, 0x75)]
    [InlineData(1, 0x76)]
    [InlineData(2, 0x77)]
    public void PushSmallInteger_SpecialValues_EmitsOptimizedBytecodes(int value, byte expectedBytecode)
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushSmallInteger(value);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(expectedBytecode, bytecodes[0]);
    }

    [Fact]
    public void PushSmallInteger_LargeValue_PushesAsLiteral()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushSmallInteger(42);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.True(bytecodes.Length >= 1);
        // Should use PUSH_LITERAL_CONST bytecode (0x20-0x3F range)
        Assert.True((bytecodes[0] >= 0x20 && bytecodes[0] <= 0x3F) || bytecodes[0] == 0x80);
    }

    [Fact]
    public void PushReceiverVariable_SmallIndex_EmitsOptimizedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushReceiverVariable(5);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x05, bytecodes[0]);
    }

    [Fact]
    public void PushReceiverVariable_LargeIndex_EmitsExtendedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushReceiverVariable(20);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.PUSH_IVAR, bytecodes[1]);
        Assert.Equal(20, bytecodes[2]);
    }

    [Fact]
    public void PushTemporary_SmallIndex_EmitsOptimizedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushTemporary(3);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x13, bytecodes[0]);
    }

    [Fact]
    public void PushTemporary_LargeIndex_EmitsExtendedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushTemporary(20);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.PUSH_TEMP, bytecodes[1]);
        Assert.Equal(20, bytecodes[2]);
    }

    [Fact]
    public void PushActiveContext_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushActiveContext();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x89, bytecodes[0]);
    }

    #endregion

    #region Store Operations Tests

    [Fact]
    public void StoreReceiverVariable_SmallIndex_EmitsOptimizedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.StoreReceiverVariable(3);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x63, bytecodes[0]);
    }

    [Fact]
    public void StoreReceiverVariable_LargeIndex_EmitsExtendedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.StoreReceiverVariable(15);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.POP_STORE_IVAR, bytecodes[1]);
        Assert.Equal(15, bytecodes[2]);
    }

    [Fact]
    public void StoreTemporary_SmallIndex_EmitsOptimizedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.StoreTemporary(5);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x6D, bytecodes[0]);
    }

    [Fact]
    public void StoreTemporary_LargeIndex_EmitsExtendedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.StoreTemporary(10);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.POP_STORE_TEMP, bytecodes[1]);
        Assert.Equal(10, bytecodes[2]);
    }

    #endregion

    #region Stack Operations Tests

    [Fact]
    public void Pop_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Pop();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(2, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.POP, bytecodes[1]);
    }

    [Fact]
    public void Dup_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Dup();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(2, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.DUP, bytecodes[1]);
    }

    #endregion

    #region Return Operations Tests

    [Fact]
    public void ReturnSelf_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnSelf();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x78, bytecodes[0]);
    }

    [Fact]
    public void ReturnTop_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnTop();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x7C, bytecodes[0]);
    }

    [Fact]
    public void ReturnTopFromBlock_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnTopFromBlock();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x7E, bytecodes[0]);
    }

    [Fact]
    public void ReturnTrue_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnTrue();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x79, bytecodes[0]);
    }

    [Fact]
    public void ReturnFalse_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnFalse();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x7A, bytecodes[0]);
    }

    [Fact]
    public void ReturnNil_EmitsCorrectBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.ReturnNil();

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(0x7B, bytecodes[0]);
    }

    #endregion

    #region Send Operations Tests

    [Theory]
    [InlineData("+", 176)]
    [InlineData("-", 177)]
    [InlineData("<", 178)]
    [InlineData(">", 179)]
    [InlineData("=", 182)]
    [InlineData("*", 184)]
    public void EmitSend_ArithmeticSelectors_EmitsSpecialBytecodes(string selector, int expectedBytecode)
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSend(selector, 1);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(expectedBytecode, bytecodes[0]);
    }

    [Theory]
    [InlineData("at:", 192)]
    [InlineData("size", 194)]
    [InlineData("class", 199)]
    [InlineData("value", 201)]
    [InlineData("new", 204)]
    public void EmitSend_SpecialSelectors_EmitsSpecialBytecodes(string selector, int expectedBytecode)
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSend(selector, selector.Contains(':') ? 1 : 0);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal(expectedBytecode, bytecodes[0]);
    }

    [Fact]
    public void EmitSend_ZeroArgs_SmallIndex_UsesOptimizedRange()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSend("doSomething", 0);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        // Should be in 0xD0-0xDF range
        Assert.True(bytecodes[0] >= 0xD0 && bytecodes[0] <= 0xDF);
    }

    [Fact]
    public void EmitSend_OneArg_SmallIndex_UsesOptimizedRange()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSend("doWith:", 1);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        // Should be in 0xE0-0xEF range
        Assert.True(bytecodes[0] >= 0xE0 && bytecodes[0] <= 0xEF);
    }

    [Fact]
    public void EmitSend_TwoArgs_SmallIndex_UsesOptimizedRange()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSend("do:with:", 2);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        // Should be in 0xF0-0xFF range
        Assert.True(bytecodes[0] >= 0xF0 && bytecodes[0] <= 0xFF);
    }

    [Fact]
    public void EmitSuperSend_ZeroArgs_EmitsExtendedBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitSuperSend("initialize", 0);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.SEND_SUPER_0, bytecodes[1]);
    }

    #endregion

    #region Literal Management Tests

    [Fact]
    public void AddLiteral_Symbol_ReturnsIndex()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        int index = emitter.AddLiteral(new Symbol("test"));

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void AddLiteral_DuplicateSymbol_ReturnsSameIndex()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        int index1 = emitter.AddLiteral(new Symbol("test"));
        int index2 = emitter.AddLiteral(new Symbol("test"));

        // Assert
        Assert.Equal(index1, index2);
    }

    [Fact]
    public void GetLiterals_SelectorsFirst_OtherLiteralsAfter()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Add a non-symbol literal first
        emitter.PushLiteralConstant(42);

        // Add a selector via send
        emitter.EmitSend("myMethod", 0);

        // Act
        var literals = emitter.GetLiterals();

        // Assert - Selectors should be first
        Assert.True(literals.Length >= 2);
        Assert.IsType<Symbol>(literals[0]);
        Assert.Equal("myMethod", ((Symbol)literals[0]).Value);
    }

    [Fact]
    public void PushLiteralVariable_GlobalVariable_AddsToLiterals()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushLiteralVariable("MyGlobal");

        // Assert
        var literals = emitter.GetLiterals();
        Assert.Single(literals);
        Assert.IsType<GlobalVariable>(literals[0]);
        Assert.Equal("MyGlobal", ((GlobalVariable)literals[0]).Key.Value);
    }

    [Fact]
    public void PushLiteralVariable_PoolVariable_AddsWithPoolName()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushLiteralVariable("CR", "TextConstants");

        // Assert
        var literals = emitter.GetLiterals();
        Assert.Single(literals);
        Assert.IsType<PoolVariable>(literals[0]);
        var poolVar = (PoolVariable)literals[0];
        Assert.Equal("CR", poolVar.Key.Value);
        Assert.Equal("TextConstants", poolVar.PoolDictionaryName);
    }

    #endregion

    #region CurrentIP and PatchByteAt Tests

    [Fact]
    public void CurrentIP_Initially_IsZero()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Assert
        Assert.Equal(0, emitter.CurrentIP);
    }

    [Fact]
    public void CurrentIP_AfterEmit_IncrementsCorrectly()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushSelf();
        emitter.PushNil();
        emitter.PushTrue();

        // Assert
        Assert.Equal(3, emitter.CurrentIP);
    }

    [Fact]
    public void CurrentOffset_IsSameAsCurrentIP()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.EmitBytes(1, 2, 3);

        // Assert
        Assert.Equal(emitter.CurrentIP, emitter.CurrentOffset);
    }

    [Fact]
    public void PatchByteAt_ValidOffset_UpdatesBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.EmitBytes(0, 0, 0);

        // Act
        emitter.PatchByteAt(1, 0xFF);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(0xFF, bytecodes[1]);
    }

    [Fact]
    public void PatchByteAt_InvalidOffset_ThrowsException()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.EmitByte(0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.PatchByteAt(5, 0xFF));
    }

    #endregion

    #region Jump Operations Tests

    [Fact]
    public void MarkMethodStart_ReturnsCurrentIP()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.EmitBytes(1, 2, 3);

        // Act
        int start = emitter.MarkMethodStart();

        // Assert
        Assert.Equal(3, start);
    }

    [Fact]
    public void EmitJumpBackward_ValidOffset_EmitsJumpBytecode()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        int start = emitter.MarkMethodStart();
        emitter.EmitBytes(1, 2, 3, 4, 5);

        // Act
        emitter.EmitJumpBackward(start);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(0xA8, bytecodes[5]);  // Long unconditional backward jump
    }

    [Fact]
    public void EmitTailRecursiveJump_StoresArgsAndJumps()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        int start = emitter.MarkMethodStart();
        emitter.EmitBytes(1, 2, 3);

        // Act
        emitter.EmitTailRecursiveJump(start, 2);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        // Should have store operations followed by jump
        Assert.True(bytecodes.Length > 5);
    }

    #endregion

    #region CalculateMaxStackDepth Tests

    [Fact]
    public void CalculateMaxStackDepth_PushOperations_ReturnsCorrectDepth()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.PushSelf();
        emitter.PushNil();
        emitter.PushTrue();

        // Act
        int depth = emitter.CalculateMaxStackDepth();

        // Assert
        Assert.Equal(3, depth);
    }

    [Fact]
    public void CalculateMaxStackDepth_PopReducesDepth()
    {
        // Arrange
        var emitter = new BytecodeEmitter();
        emitter.PushSelf();
        emitter.PushNil();
        emitter.Pop();

        // Act
        int depth = emitter.CalculateMaxStackDepth();

        // Assert - Max was 2 before pop
        Assert.Equal(2, depth);
    }

    [Fact]
    public void CalculateMaxStackDepth_EmptyEmitter_ReturnsZero()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        int depth = emitter.CalculateMaxStackDepth();

        // Assert
        Assert.Equal(0, depth);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetBytecodes_EmptyEmitter_ReturnsEmptyArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        var bytecodes = emitter.GetBytecodes();

        // Assert
        Assert.Empty(bytecodes);
    }

    [Fact]
    public void GetLiterals_EmptyEmitter_ReturnsEmptyArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        var literals = emitter.GetLiterals();

        // Assert
        Assert.Empty(literals);
    }

    [Fact]
    public void MultiplePushLiteralConstant_SameLiteral_ReuseIndex()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.PushLiteralConstant(42);
        emitter.PushLiteralConstant(42);
        emitter.PushLiteralConstant(42);

        // Assert
        var literals = emitter.GetLiterals();
        Assert.Single(literals);  // Only one literal stored
    }

    [Fact]
    public void StoreLiteralVariable_GlobalVariable_EmitsExtendedStore()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.StoreLiteralVariable("MyGlobal");

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal(0x80, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.POP_STORE_LITERAL_VAR, bytecodes[1]);
    }

    #endregion
}
