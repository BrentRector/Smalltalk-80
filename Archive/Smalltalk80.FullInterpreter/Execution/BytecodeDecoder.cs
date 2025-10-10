using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// High-performance bytecode decoder for Smalltalk-80 bytecode format.
/// Uses ref struct for zero-allocation decoding and aggressive inlining.
/// </summary>
/// <remarks>
/// Bytecode format (Blue Book specification):
/// - 0-15: Push receiver variable
/// - 16-31: Push temporary variable  
/// - 32-63: Push literal constant
/// - 64-95: Push literal variable
/// - 96-103: Stack operations
/// - 104-111: Return operations
/// - 112-119: Push special constants
/// - 120-127: Send special selectors
/// - 128-175: Extended operations
/// - 176-255: Send operations
/// </remarks>
public ref struct BytecodeDecoder
{
    /// <summary>
    /// The bytecode span being decoded.
    /// </summary>
    private readonly ReadOnlySpan<byte> _bytecode;
    
    /// <summary>
    /// Current instruction pointer (0-based).
    /// </summary>
    private int _ip;
    
    /// <summary>
    /// Initializes a new bytecode decoder.
    /// </summary>
    /// <param name="bytecode">The bytecode to decode.</param>
    /// <param name="startIp">Starting instruction pointer (0-based).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BytecodeDecoder(ReadOnlySpan<byte> bytecode, int startIp = 0)
    {
        _bytecode = bytecode;
        _ip = startIp;
    }
    
    /// <summary>
    /// Gets the current instruction pointer.
    /// </summary>
    public readonly int IP => _ip;
    
    /// <summary>
    /// Gets whether there are more bytecodes to decode.
    /// </summary>
    public readonly bool HasMore => _ip < _bytecode.Length;
    
    /// <summary>
    /// Fetches the next bytecode without decoding it.
    /// </summary>
    /// <returns>The next bytecode value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte PeekByte()
    {
        if (_ip >= _bytecode.Length)
            throw new IndexOutOfRangeException("Bytecode stream exhausted");
        return _bytecode[_ip];
    }
    
    /// <summary>
    /// Fetches and advances past the next bytecode.
    /// </summary>
    /// <returns>The fetched bytecode.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte FetchByte()
    {
        if (_ip >= _bytecode.Length)
            throw new IndexOutOfRangeException("Bytecode stream exhausted");
        return _bytecode[_ip++];
    }
    
    /// <summary>
    /// Fetches a two-byte extended argument.
    /// </summary>
    /// <returns>The 16-bit argument value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FetchExtendedArgument()
    {
        var high = FetchByte();
        var low = FetchByte();
        return (high << 8) | low;
    }
    
    /// <summary>
    /// Jumps to a specific instruction pointer.
    /// </summary>
    /// <param name="newIp">The new instruction pointer (0-based).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Jump(int newIp)
    {
        if (newIp < 0 || newIp > _bytecode.Length)
            throw new ArgumentOutOfRangeException(nameof(newIp), "Jump target out of range");
        _ip = newIp;
    }
    
    /// <summary>
    /// Jumps relative to the current position.
    /// </summary>
    /// <param name="offset">The relative offset to jump.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void JumpRelative(int offset)
    {
        Jump(_ip + offset);
    }
    
    /// <summary>
    /// Decodes the instruction type from a bytecode.
    /// </summary>
    /// <param name="bytecode">The bytecode to decode.</param>
    /// <returns>The instruction type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BytecodeType GetInstructionType(byte bytecode)
    {
        return bytecode switch
        {
            <= 15 => BytecodeType.PushReceiverVariable,
            <= 31 => BytecodeType.PushTemporaryVariable,
            <= 63 => BytecodeType.PushLiteralConstant,
            <= 95 => BytecodeType.PushLiteralVariable,
            <= 103 => BytecodeType.StackOperation,
            <= 111 => BytecodeType.Return,
            <= 119 => BytecodeType.PushSpecial,
            <= 127 => BytecodeType.SendSpecial,
            <= 175 => BytecodeType.Extended,
            _ => BytecodeType.Send
        };
    }
    
    /// <summary>
    /// Decodes a push receiver variable instruction.
    /// </summary>
    /// <param name="bytecode">The bytecode (0-15).</param>
    /// <returns>The receiver variable index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodePushReceiverVariable(byte bytecode) => bytecode & 0x0F;
    
    /// <summary>
    /// Decodes a push temporary variable instruction.
    /// </summary>
    /// <param name="bytecode">The bytecode (16-31).</param>
    /// <returns>The temporary variable index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodePushTemporaryVariable(byte bytecode) => bytecode & 0x0F;
    
    /// <summary>
    /// Decodes a push literal constant instruction.
    /// </summary>
    /// <param name="bytecode">The bytecode (32-63).</param>
    /// <returns>The literal index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodePushLiteralConstant(byte bytecode) => bytecode & 0x1F;
    
    /// <summary>
    /// Decodes a push literal variable instruction.
    /// </summary>
    /// <param name="bytecode">The bytecode (64-95).</param>
    /// <returns>The literal variable index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodePushLiteralVariable(byte bytecode) => bytecode & 0x1F;
    
    /// <summary>
    /// Decodes a stack operation.
    /// </summary>
    /// <param name="bytecode">The bytecode (96-103).</param>
    /// <returns>The stack operation type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StackOperation DecodeStackOperation(byte bytecode)
    {
        return (StackOperation)(bytecode - 96);
    }
    
    /// <summary>
    /// Decodes a return operation.
    /// </summary>
    /// <param name="bytecode">The bytecode (104-111).</param>
    /// <returns>The return operation type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReturnOperation DecodeReturnOperation(byte bytecode)
    {
        return (ReturnOperation)(bytecode - 104);
    }
    
    /// <summary>
    /// Decodes a push special constant operation.
    /// </summary>
    /// <param name="bytecode">The bytecode (112-119).</param>
    /// <returns>The special constant type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SpecialConstant DecodePushSpecial(byte bytecode)
    {
        return (SpecialConstant)(bytecode - 112);
    }
    
    /// <summary>
    /// Decodes a special selector send.
    /// </summary>
    /// <param name="bytecode">The bytecode (120-127).</param>
    /// <returns>The special selector index and argument count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int selectorIndex, int argCount) DecodeSpecialSend(byte bytecode)
    {
        var index = (bytecode - 120) * 2;
        return (index / 2, SpecialSelectorArgCounts[bytecode - 120]);
    }
    
    /// <summary>
    /// Argument counts for special selector sends (120-127).
    /// </summary>
    private static ReadOnlySpan<byte> SpecialSelectorArgCounts => [
        1, 1, 1, 1, 1, 1, 1, 1  // All special selectors take 1 argument
    ];
    
    /// <summary>
    /// Decodes a regular send operation.
    /// </summary>
    /// <param name="bytecode">The bytecode (176-255).</param>
    /// <returns>The selector index and argument count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int selectorIndex, int argCount) DecodeSend(byte bytecode)
    {
        if (bytecode < 208) // Single extended send
        {
            var selector = (bytecode - 176) & 0x0F;
            var argCount = ((bytecode - 176) >> 4) & 0x01;
            return (selector, argCount);
        }
        else if (bytecode < 224) // Double extended send  
        {
            var selector = (bytecode - 208) & 0x07;
            var argCount = ((bytecode - 208) >> 3) & 0x01;
            return (selector, argCount);
        }
        else // Super send
        {
            var selector = (bytecode - 224) & 0x07;
            var argCount = ((bytecode - 224) >> 3) & 0x03;
            return (selector, argCount);
        }
    }
    
    /// <summary>
    /// Decodes an extended operation.
    /// </summary>
    /// <param name="bytecode">The extended operation bytecode (128-175).</param>
    /// <param name="nextByte">The next byte for extended operations.</param>
    /// <returns>The extended operation details.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ExtendedOperation DecodeExtended(byte bytecode, byte nextByte)
    {
        return bytecode switch
        {
            128 => new ExtendedOperation(ExtendedType.PushReceiverVariable, nextByte),
            129 => new ExtendedOperation(ExtendedType.PushTemporaryVariable, nextByte),
            130 => new ExtendedOperation(ExtendedType.PushLiteralConstant, nextByte),
            131 => new ExtendedOperation(ExtendedType.PushLiteralVariable, nextByte),
            132 => new ExtendedOperation(ExtendedType.StoreReceiverVariable, nextByte),
            133 => new ExtendedOperation(ExtendedType.StoreTemporaryVariable, nextByte),
            134 => new ExtendedOperation(ExtendedType.PopStoreReceiverVariable, nextByte),
            135 => new ExtendedOperation(ExtendedType.PopStoreTemporaryVariable, nextByte),
            136 => DecodeDoubleExtendedOperation(nextByte),
            137 => new ExtendedOperation(ExtendedType.Pop, nextByte),
            138 => new ExtendedOperation(ExtendedType.Duplicate, nextByte),
            139 => new ExtendedOperation(ExtendedType.PushActiveContext, 0),
            >= 144 and <= 151 => new ExtendedOperation(ExtendedType.Jump, ((bytecode - 144) << 8) | nextByte),
            >= 152 and <= 159 => new ExtendedOperation(ExtendedType.ConditionalJumpFalse, ((bytecode - 152) << 8) | nextByte),
            >= 160 and <= 167 => new ExtendedOperation(ExtendedType.ConditionalJumpTrue, (((bytecode - 160) + 8) << 8) | nextByte),
            >= 168 and <= 171 => new ExtendedOperation(ExtendedType.Jump, bytecode - 168 + 1),
            172 => new ExtendedOperation(ExtendedType.JumpFalse, nextByte),
            173 => new ExtendedOperation(ExtendedType.JumpTrue, nextByte),
            _ => new ExtendedOperation(ExtendedType.Unknown, 0)
        };
    }
    
    /// <summary>
    /// Decodes a double-extended operation (bytecode 136).
    /// </summary>
    private static ExtendedOperation DecodeDoubleExtendedOperation(byte descriptor)
    {
        var type = (descriptor >> 5) & 0x07;
        var index = descriptor & 0x1F;
        
        return type switch
        {
            0 => new ExtendedOperation(ExtendedType.SendSelector, index),
            1 => new ExtendedOperation(ExtendedType.SendSuperSelector, index),
            2 => new ExtendedOperation(ExtendedType.PushReceiverVariable, index),
            3 => new ExtendedOperation(ExtendedType.PushLiteralConstant, index),
            4 => new ExtendedOperation(ExtendedType.PushLiteralVariable, index),
            5 => new ExtendedOperation(ExtendedType.StoreReceiverVariable, index),
            6 => new ExtendedOperation(ExtendedType.PopStoreReceiverVariable, index),
            7 => new ExtendedOperation(ExtendedType.StoreLiteralVariable, index),
            _ => new ExtendedOperation(ExtendedType.Unknown, 0)
        };
    }
}

/// <summary>
/// Bytecode instruction types.
/// </summary>
internal enum BytecodeType : byte
{
    PushReceiverVariable,
    PushTemporaryVariable,
    PushLiteralConstant,
    PushLiteralVariable,
    StackOperation,
    Return,
    PushSpecial,
    SendSpecial,
    Extended,
    Send
}

/// <summary>
/// Stack operations (bytecodes 96-103).
/// </summary>
internal enum StackOperation : byte
{
    PopIntoReceiverVariable0 = 0,
    PopIntoReceiverVariable1 = 1,
    PopIntoReceiverVariable2 = 2,
    PopIntoReceiverVariable3 = 3,
    PopIntoReceiverVariable4 = 4,
    PopIntoReceiverVariable5 = 5,
    PopIntoReceiverVariable6 = 6,
    PopIntoReceiverVariable7 = 7
}

/// <summary>
/// Return operations (bytecodes 104-111).
/// </summary>
internal enum ReturnOperation : byte
{
    ReturnReceiver = 0,
    ReturnTrue = 1,
    ReturnFalse = 2,
    ReturnNil = 3,
    ReturnStackTop = 4,
    ReturnStackTopFromBlock = 5,
    Unused1 = 6,
    Unused2 = 7
}

/// <summary>
/// Special constants (bytecodes 112-119).
/// </summary>
internal enum SpecialConstant : byte
{
    PushReceiver = 0,
    PushTrue = 1,
    PushFalse = 2,
    PushNil = 3,
    PushMinusOne = 4,
    PushZero = 5,
    PushOne = 6,
    PushTwo = 7
}

/// <summary>
/// Extended operation types.
/// </summary>
internal enum ExtendedType : byte
{
    Unknown,
    PushReceiverVariable,
    PushTemporaryVariable,
    PushLiteralConstant,
    PushLiteralVariable,
    StoreReceiverVariable,
    StoreTemporaryVariable,
    PopStoreReceiverVariable,
    PopStoreTemporaryVariable,
    SendSelector,
    SendSuperSelector,
    StoreLiteralVariable,
    Pop,
    Duplicate,
    PushActiveContext,
    Jump,
    ConditionalJumpFalse,
    ConditionalJumpTrue,
    JumpFalse,
    JumpTrue
}

/// <summary>
/// Represents a decoded extended operation.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct ExtendedOperation
{
    /// <summary>
    /// The type of extended operation.
    /// </summary>
    public readonly ExtendedType Type;
    
    /// <summary>
    /// The operation parameter (index or offset).
    /// </summary>
    public readonly int Parameter;
    
    /// <summary>
    /// Initializes a new extended operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExtendedOperation(ExtendedType type, int parameter)
    {
        Type = type;
        Parameter = parameter;
    }
}