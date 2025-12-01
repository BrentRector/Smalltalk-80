extern alias CompilerLib;

using Bytecode = CompilerLib::Smalltalk80.Core.Bytecodes.Bytecode;
using ExtendedSubOp = CompilerLib::Smalltalk80.Core.Bytecodes.ExtendedSubOp;
using Smalltalk80.Compiler;
using Smalltalk80.Core.Bytecodes;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.Compiler;

/// <summary>
/// Comprehensive unit tests for BytecodeEmitter.
/// Tests bytecode emission, literal management, and jump operations.
/// </summary>
[Trait("Category", "Compiler")]
public class BytecodeEmitterTests
{
    #region Basic Emit Tests

    [Fact]
    public void Emit_SingleByte_AddsToBytecodesArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Emit((byte)Bytecode.PUSH_SELF);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal((byte)Bytecode.PUSH_SELF, bytecodes[0]);
    }

    [Fact]
    public void Emit_BytecodeEnum_AddsCorrectValue()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.Emit((byte)Bytecode.PUSH_SELF);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal((byte)Bytecode.PUSH_SELF, bytecodes[0]);
    }

    [Fact]
    public void EmitBytes_MultipleBytes_AddsAllToArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitBytes((byte)Bytecode.PUSH_TEMP_0, (byte)(Bytecode.PUSH_TEMP_0 + 1), (byte)(Bytecode.PUSH_TEMP_0 + 2));

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Equal(3, bytecodes.Length);
        Assert.Equal((byte)Bytecode.PUSH_TEMP_0, bytecodes[0]);
        Assert.Equal((byte)(Bytecode.PUSH_TEMP_0 + 1), bytecodes[1]);
        Assert.Equal((byte)(Bytecode.PUSH_TEMP_0 + 2), bytecodes[2]);
    }

    [Fact]
    public void EmitByte_SingleValue_AddsToBytecodesArray()
    {
        // Arrange
        var emitter = new BytecodeEmitter();

        // Act
        emitter.EmitByte((byte)Bytecode.PUSH_NIL);

        // Assert
        var bytecodes = emitter.GetBytecodes();
        Assert.Single(bytecodes);
        Assert.Equal((byte)Bytecode.PUSH_NIL, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.PUSH_SELF, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.PUSH_NIL, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.PUSH_TRUE, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.PUSH_FALSE, bytecodes[0]);
    }

    [Theory]
    [InlineData(-1, Bytecode.PUSH_MINUS_ONE)]
    [InlineData(0, Bytecode.PUSH_ZERO)]
    [InlineData(1, Bytecode.PUSH_ONE)]
    [InlineData(2, Bytecode.PUSH_TWO)]
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
        // Should use PUSH_LITERAL_CONST bytecode
        Assert.True((bytecodes[0] >= (byte)Bytecode.PUSH_LITERAL_CONST_0 && bytecodes[0] <= (byte)Bytecode.PUSH_LITERAL_CONST_31) || bytecodes[0] == (byte)Bytecode.EXTENDED_PREFIX);
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
        Assert.Equal((byte)(Bytecode.PUSH_IVAR_0 + 5), bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)(Bytecode.PUSH_TEMP_0 + 3), bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.PUSH_ACTIVE_CONTEXT, bytecodes[0]);
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
        Assert.Equal((byte)(Bytecode.POP_STORE_IVAR_0 + 3), bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)(Bytecode.POP_STORE_TEMP_0 + 5), bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_SELF, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_TOP, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_TOP_POP, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_TRUE, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_FALSE, bytecodes[0]);
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
        Assert.Equal((byte)Bytecode.RETURN_NIL, bytecodes[0]);
    }

    #endregion

    #region Send Operations Tests

    [Theory]
    [InlineData("+", Bytecode.SEND_ARITH_PLUS)]
    [InlineData("-", Bytecode.SEND_ARITH_MINUS)]
    [InlineData("<", Bytecode.SEND_ARITH_LT)]
    [InlineData(">", Bytecode.SEND_ARITH_GT)]
    [InlineData("=", Bytecode.SEND_ARITH_EQ)]
    [InlineData("*", Bytecode.SEND_ARITH_TIMES)]
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
    [InlineData("at:", Bytecode.SEND_SPECIAL_AT)]
    [InlineData("size", Bytecode.SEND_SPECIAL_SIZE)]
    [InlineData("class", Bytecode.SEND_SPECIAL_CLASS)]
    [InlineData("value", Bytecode.SEND_SPECIAL_VALUE)]
    [InlineData("new", Bytecode.SEND_SPECIAL_NEW)]
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
        // Should be in SEND_LITERAL_0 range
        Assert.True(bytecodes[0] >= (byte)Bytecode.SEND_LITERAL_0_BASE && bytecodes[0] <= (byte)Bytecode.SEND_LITERAL_0_MAX);
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
        // Should be in SEND_LITERAL_1 range
        Assert.True(bytecodes[0] >= (byte)Bytecode.SEND_LITERAL_1_BASE && bytecodes[0] <= (byte)Bytecode.SEND_LITERAL_1_MAX);
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
        // Should be in SEND_LITERAL_2 range
        Assert.True(bytecodes[0] >= (byte)Bytecode.SEND_LITERAL_2_BASE && bytecodes[0] <= (byte)Bytecode.SEND_LITERAL_2_MAX);
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
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
        // Blue Book long unconditional jump (0xA0-0xA7) with backward offset
        // jumpOffset = target(0) - current(5) - 2 = -7
        // hiPart = (-7 - 255) / 256 = -1, loByte = -7 - (-256) = 249
        // opcodeBits = -1 + 4 = 3, so opcode = 0xA0 + 3 = 0xA3
        Assert.True(bytecodes[5] >= 0xA0 && bytecodes[5] <= 0xA7,
            $"Expected long unconditional jump (0xA0-0xA7), got 0x{bytecodes[5]:X2}");
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
        Assert.Equal((byte)Bytecode.EXTENDED_PREFIX, bytecodes[0]);
        Assert.Equal((byte)ExtendedSubOp.POP_STORE_LITERAL_VAR, bytecodes[1]);
    }

    #endregion
}
