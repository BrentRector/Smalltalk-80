using System;
using Xunit;
using Smalltalk80.Interpreter.Execution;

namespace Tests;

/// <summary>
/// Tests for the BytecodeDecoder ref struct.
/// </summary>
public sealed class TestBytecodeDecoder
{
    /// <summary>
    /// Tests basic bytecode fetching.
    /// </summary>
    [Fact]
    public void TestByteFetching()
    {
        var bytecode = new byte[] { 1, 2, 3, 4, 5 };
        var decoder = new BytecodeDecoder(bytecode);
        
        Assert.Equal(0, decoder.IP);
        Assert.True(decoder.HasMore);
        
        Assert.Equal(1, decoder.PeekByte());
        Assert.Equal(0, decoder.IP); // Peek doesn't advance
        
        Assert.Equal(1, decoder.FetchByte());
        Assert.Equal(1, decoder.IP); // Fetch advances
        
        Assert.Equal(2, decoder.FetchByte());
        Assert.Equal(3, decoder.FetchByte());
        Assert.Equal(4, decoder.FetchByte());
        Assert.Equal(5, decoder.FetchByte());
        
        Assert.Equal(5, decoder.IP);
        Assert.False(decoder.HasMore);
    }
    
    /// <summary>
    /// Tests extended argument fetching.
    /// </summary>
    [Fact]
    public void TestExtendedArgument()
    {
        var bytecode = new byte[] { 0x12, 0x34, 0x56 };
        var decoder = new BytecodeDecoder(bytecode);
        
        var extended = decoder.FetchExtendedArgument();
        Assert.Equal(0x1234, extended);
        Assert.Equal(2, decoder.IP);
    }
    
    /// <summary>
    /// Tests jump operations.
    /// </summary>
    [Fact]
    public void TestJumps()
    {
        var bytecode = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        var decoder = new BytecodeDecoder(bytecode);
        
        decoder.Jump(3);
        Assert.Equal(3, decoder.IP);
        Assert.Equal(3, decoder.FetchByte());
        
        decoder.JumpRelative(2);
        Assert.Equal(6, decoder.IP);
        Assert.Equal(6, decoder.FetchByte());
        
        decoder.JumpRelative(-3);
        Assert.Equal(4, decoder.IP);
        Assert.Equal(4, decoder.FetchByte());
    }
    
    /// <summary>
    /// Tests instruction type decoding.
    /// </summary>
    [Fact]
    public void TestInstructionTypeDecoding()
    {
        // Push receiver variable (0-15)
        Assert.Equal(BytecodeType.PushReceiverVariable, BytecodeDecoder.GetInstructionType(0));
        Assert.Equal(BytecodeType.PushReceiverVariable, BytecodeDecoder.GetInstructionType(15));
        
        // Push temporary variable (16-31)
        Assert.Equal(BytecodeType.PushTemporaryVariable, BytecodeDecoder.GetInstructionType(16));
        Assert.Equal(BytecodeType.PushTemporaryVariable, BytecodeDecoder.GetInstructionType(31));
        
        // Push literal constant (32-63)
        Assert.Equal(BytecodeType.PushLiteralConstant, BytecodeDecoder.GetInstructionType(32));
        Assert.Equal(BytecodeType.PushLiteralConstant, BytecodeDecoder.GetInstructionType(63));
        
        // Push literal variable (64-95)
        Assert.Equal(BytecodeType.PushLiteralVariable, BytecodeDecoder.GetInstructionType(64));
        Assert.Equal(BytecodeType.PushLiteralVariable, BytecodeDecoder.GetInstructionType(95));
        
        // Stack operations (96-103)
        Assert.Equal(BytecodeType.StackOperation, BytecodeDecoder.GetInstructionType(96));
        Assert.Equal(BytecodeType.StackOperation, BytecodeDecoder.GetInstructionType(103));
        
        // Return operations (104-111)
        Assert.Equal(BytecodeType.Return, BytecodeDecoder.GetInstructionType(104));
        Assert.Equal(BytecodeType.Return, BytecodeDecoder.GetInstructionType(111));
        
        // Push special (112-119)
        Assert.Equal(BytecodeType.PushSpecial, BytecodeDecoder.GetInstructionType(112));
        Assert.Equal(BytecodeType.PushSpecial, BytecodeDecoder.GetInstructionType(119));
        
        // Send special (120-127)
        Assert.Equal(BytecodeType.SendSpecial, BytecodeDecoder.GetInstructionType(120));
        Assert.Equal(BytecodeType.SendSpecial, BytecodeDecoder.GetInstructionType(127));
        
        // Extended (128-175)
        Assert.Equal(BytecodeType.Extended, BytecodeDecoder.GetInstructionType(128));
        Assert.Equal(BytecodeType.Extended, BytecodeDecoder.GetInstructionType(175));
        
        // Send (176-255)
        Assert.Equal(BytecodeType.Send, BytecodeDecoder.GetInstructionType(176));
        Assert.Equal(BytecodeType.Send, BytecodeDecoder.GetInstructionType(255));
    }
    
    /// <summary>
    /// Tests decoding of push instructions.
    /// </summary>
    [Fact]
    public void TestPushDecoding()
    {
        // Push receiver variable
        Assert.Equal(0, BytecodeDecoder.DecodePushReceiverVariable(0));
        Assert.Equal(5, BytecodeDecoder.DecodePushReceiverVariable(5));
        Assert.Equal(15, BytecodeDecoder.DecodePushReceiverVariable(15));
        
        // Push temporary variable
        Assert.Equal(0, BytecodeDecoder.DecodePushTemporaryVariable(16));
        Assert.Equal(5, BytecodeDecoder.DecodePushTemporaryVariable(21));
        Assert.Equal(15, BytecodeDecoder.DecodePushTemporaryVariable(31));
        
        // Push literal constant
        Assert.Equal(0, BytecodeDecoder.DecodePushLiteralConstant(32));
        Assert.Equal(10, BytecodeDecoder.DecodePushLiteralConstant(42));
        Assert.Equal(31, BytecodeDecoder.DecodePushLiteralConstant(63));
        
        // Push literal variable
        Assert.Equal(0, BytecodeDecoder.DecodePushLiteralVariable(64));
        Assert.Equal(10, BytecodeDecoder.DecodePushLiteralVariable(74));
        Assert.Equal(31, BytecodeDecoder.DecodePushLiteralVariable(95));
    }
    
    /// <summary>
    /// Tests stack operation decoding.
    /// </summary>
    [Fact]
    public void TestStackOperationDecoding()
    {
        Assert.Equal(StackOperation.PopIntoReceiverVariable0, BytecodeDecoder.DecodeStackOperation(96));
        Assert.Equal(StackOperation.PopIntoReceiverVariable1, BytecodeDecoder.DecodeStackOperation(97));
        Assert.Equal(StackOperation.PopIntoReceiverVariable7, BytecodeDecoder.DecodeStackOperation(103));
    }
    
    /// <summary>
    /// Tests return operation decoding.
    /// </summary>
    [Fact]
    public void TestReturnOperationDecoding()
    {
        Assert.Equal(ReturnOperation.ReturnReceiver, BytecodeDecoder.DecodeReturnOperation(104));
        Assert.Equal(ReturnOperation.ReturnTrue, BytecodeDecoder.DecodeReturnOperation(105));
        Assert.Equal(ReturnOperation.ReturnFalse, BytecodeDecoder.DecodeReturnOperation(106));
        Assert.Equal(ReturnOperation.ReturnNil, BytecodeDecoder.DecodeReturnOperation(107));
        Assert.Equal(ReturnOperation.ReturnStackTop, BytecodeDecoder.DecodeReturnOperation(108));
        Assert.Equal(ReturnOperation.ReturnStackTopFromBlock, BytecodeDecoder.DecodeReturnOperation(109));
    }
    
    /// <summary>
    /// Tests special constant decoding.
    /// </summary>
    [Fact]
    public void TestSpecialConstantDecoding()
    {
        Assert.Equal(SpecialConstant.PushReceiver, BytecodeDecoder.DecodePushSpecial(112));
        Assert.Equal(SpecialConstant.PushTrue, BytecodeDecoder.DecodePushSpecial(113));
        Assert.Equal(SpecialConstant.PushFalse, BytecodeDecoder.DecodePushSpecial(114));
        Assert.Equal(SpecialConstant.PushNil, BytecodeDecoder.DecodePushSpecial(115));
        Assert.Equal(SpecialConstant.PushMinusOne, BytecodeDecoder.DecodePushSpecial(116));
        Assert.Equal(SpecialConstant.PushZero, BytecodeDecoder.DecodePushSpecial(117));
        Assert.Equal(SpecialConstant.PushOne, BytecodeDecoder.DecodePushSpecial(118));
        Assert.Equal(SpecialConstant.PushTwo, BytecodeDecoder.DecodePushSpecial(119));
    }
    
    /// <summary>
    /// Tests special selector send decoding.
    /// </summary>
    [Fact]
    public void TestSpecialSelectorDecoding()
    {
        var (selector0, args0) = BytecodeDecoder.DecodeSpecialSend(120);
        Assert.Equal(0, selector0); // +
        Assert.Equal(1, args0);
        
        var (selector1, args1) = BytecodeDecoder.DecodeSpecialSend(121);
        Assert.Equal(1, selector1); // -
        Assert.Equal(1, args1);
        
        var (selector2, args2) = BytecodeDecoder.DecodeSpecialSend(122);
        Assert.Equal(2, selector2); // <
        Assert.Equal(1, args2);
        
        var (selector7, args7) = BytecodeDecoder.DecodeSpecialSend(127);
        Assert.Equal(7, selector7); // ~=
        Assert.Equal(1, args7);
    }
    
    /// <summary>
    /// Tests send operation decoding.
    /// </summary>
    [Fact]
    public void TestSendDecoding()
    {
        // Single extended send (176-207)
        var (selector1, args1) = BytecodeDecoder.DecodeSend(176);
        Assert.Equal(0, selector1);
        Assert.Equal(0, args1);
        
        var (selector2, args2) = BytecodeDecoder.DecodeSend(177);
        Assert.Equal(1, selector2);
        Assert.Equal(0, args2);
        
        var (selector3, args3) = BytecodeDecoder.DecodeSend(192);
        Assert.Equal(0, selector3);
        Assert.Equal(1, args3);
        
        // Double extended send (208-223)
        var (selector4, args4) = BytecodeDecoder.DecodeSend(208);
        Assert.Equal(0, selector4);
        Assert.Equal(0, args4);
        
        // Super send (224-255)
        var (selector5, args5) = BytecodeDecoder.DecodeSend(224);
        Assert.Equal(0, selector5);
        Assert.Equal(0, args5);
        
        var (selector6, args6) = BytecodeDecoder.DecodeSend(255);
        Assert.Equal(7, selector6);
        Assert.Equal(3, args6);
    }
    
    /// <summary>
    /// Tests extended operation decoding.
    /// </summary>
    [Fact]
    public void TestExtendedOperationDecoding()
    {
        // Push receiver variable
        var op1 = BytecodeDecoder.DecodeExtended(128, 5);
        Assert.Equal(ExtendedType.PushReceiverVariable, op1.Type);
        Assert.Equal(5, op1.Parameter);
        
        // Push temporary variable
        var op2 = BytecodeDecoder.DecodeExtended(129, 10);
        Assert.Equal(ExtendedType.PushTemporaryVariable, op2.Type);
        Assert.Equal(10, op2.Parameter);
        
        // Push literal constant
        var op3 = BytecodeDecoder.DecodeExtended(130, 15);
        Assert.Equal(ExtendedType.PushLiteralConstant, op3.Type);
        Assert.Equal(15, op3.Parameter);
        
        // Store receiver variable
        var op4 = BytecodeDecoder.DecodeExtended(132, 3);
        Assert.Equal(ExtendedType.StoreReceiverVariable, op4.Type);
        Assert.Equal(3, op4.Parameter);
        
        // Pop
        var op5 = BytecodeDecoder.DecodeExtended(137, 0);
        Assert.Equal(ExtendedType.Pop, op5.Type);
        
        // Duplicate
        var op6 = BytecodeDecoder.DecodeExtended(138, 0);
        Assert.Equal(ExtendedType.Duplicate, op6.Type);
        
        // Push active context
        var op7 = BytecodeDecoder.DecodeExtended(139, 0);
        Assert.Equal(ExtendedType.PushActiveContext, op7.Type);
    }
    
    /// <summary>
    /// Tests jump instruction decoding.
    /// </summary>
    [Fact]
    public void TestJumpDecoding()
    {
        // Jump (144-151)
        var jump1 = BytecodeDecoder.DecodeExtended(144, 10);
        Assert.Equal(ExtendedType.Jump, jump1.Type);
        Assert.Equal(10, jump1.Parameter);
        
        var jump2 = BytecodeDecoder.DecodeExtended(145, 10);
        Assert.Equal(ExtendedType.Jump, jump2.Type);
        Assert.Equal(256 + 10, jump2.Parameter);
        
        // Conditional jump false (152-159)
        var jumpFalse = BytecodeDecoder.DecodeExtended(152, 20);
        Assert.Equal(ExtendedType.ConditionalJumpFalse, jumpFalse.Type);
        Assert.Equal(20, jumpFalse.Parameter);
        
        // Conditional jump true (160-167)
        var jumpTrue = BytecodeDecoder.DecodeExtended(160, 30);
        Assert.Equal(ExtendedType.ConditionalJumpTrue, jumpTrue.Type);
        Assert.Equal((8 << 8) | 30, jumpTrue.Parameter);
        
        // Short jumps (168-171)
        var shortJump = BytecodeDecoder.DecodeExtended(168, 0);
        Assert.Equal(ExtendedType.Jump, shortJump.Type);
        Assert.Equal(1, shortJump.Parameter);
        
        var shortJump2 = BytecodeDecoder.DecodeExtended(171, 0);
        Assert.Equal(ExtendedType.Jump, shortJump2.Type);
        Assert.Equal(4, shortJump2.Parameter);
        
        // Jump if false (172)
        var jf = BytecodeDecoder.DecodeExtended(172, 15);
        Assert.Equal(ExtendedType.JumpFalse, jf.Type);
        Assert.Equal(15, jf.Parameter);
        
        // Jump if true (173)
        var jt = BytecodeDecoder.DecodeExtended(173, 25);
        Assert.Equal(ExtendedType.JumpTrue, jt.Type);
        Assert.Equal(25, jt.Parameter);
    }
    
    /// <summary>
    /// Tests double-extended operation decoding (bytecode 136).
    /// </summary>
    [Fact]
    public void TestDoubleExtendedDecoding()
    {
        // Send selector (type 0)
        var op1 = BytecodeDecoder.DecodeExtended(136, 0b00000101);
        Assert.Equal(ExtendedType.SendSelector, op1.Type);
        Assert.Equal(5, op1.Parameter);
        
        // Send super selector (type 1)
        var op2 = BytecodeDecoder.DecodeExtended(136, 0b00100101);
        Assert.Equal(ExtendedType.SendSuperSelector, op2.Type);
        Assert.Equal(5, op2.Parameter);
        
        // Push receiver variable (type 2)
        var op3 = BytecodeDecoder.DecodeExtended(136, 0b01000101);
        Assert.Equal(ExtendedType.PushReceiverVariable, op3.Type);
        Assert.Equal(5, op3.Parameter);
        
        // Push literal constant (type 3)
        var op4 = BytecodeDecoder.DecodeExtended(136, 0b01100101);
        Assert.Equal(ExtendedType.PushLiteralConstant, op4.Type);
        Assert.Equal(5, op4.Parameter);
        
        // Push literal variable (type 4)
        var op5 = BytecodeDecoder.DecodeExtended(136, 0b10000101);
        Assert.Equal(ExtendedType.PushLiteralVariable, op5.Type);
        Assert.Equal(5, op5.Parameter);
        
        // Store receiver variable (type 5)
        var op6 = BytecodeDecoder.DecodeExtended(136, 0b10100101);
        Assert.Equal(ExtendedType.StoreReceiverVariable, op6.Type);
        Assert.Equal(5, op6.Parameter);
        
        // Pop store receiver variable (type 6)
        var op7 = BytecodeDecoder.DecodeExtended(136, 0b11000101);
        Assert.Equal(ExtendedType.PopStoreReceiverVariable, op7.Type);
        Assert.Equal(5, op7.Parameter);
        
        // Store literal variable (type 7)
        var op8 = BytecodeDecoder.DecodeExtended(136, 0b11100101);
        Assert.Equal(ExtendedType.StoreLiteralVariable, op8.Type);
        Assert.Equal(5, op8.Parameter);
    }
    
    /// <summary>
    /// Tests bytecode stream boundary conditions.
    /// </summary>
    [Fact]
    public void TestBoundaryConditions()
    {
        var bytecode = new byte[] { 1, 2 };
        var decoder = new BytecodeDecoder(bytecode);
        
        decoder.FetchByte();
        decoder.FetchByte();
        
        Assert.False(decoder.HasMore);
        Assert.Throws<IndexOutOfRangeException>(() => decoder.FetchByte());
        Assert.Throws<IndexOutOfRangeException>(() => decoder.PeekByte());
        
        // Test jump bounds
        decoder = new BytecodeDecoder(bytecode);
        Assert.Throws<ArgumentOutOfRangeException>(() => decoder.Jump(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => decoder.Jump(3));
        
        // Jump to end is allowed (just past last byte)
        decoder.Jump(2);
        Assert.False(decoder.HasMore);
    }
}