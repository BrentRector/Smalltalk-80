namespace Smalltalk80.Interpreter.Core.VM.BytecodeTypes;

/// <summary>
/// Interface for decoding raw bytecodes into typed instructions.
/// </summary>
public interface IBytecodeDecoder
{
    /// <summary>
    /// Decodes a single instruction at the given PC.
    /// </summary>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <param name="pc">The program counter (modified to point past the decoded instruction).</param>
    /// <returns>The decoded instruction.</returns>
    Instruction Decode(byte[] bytecodes, ref int pc);

    /// <summary>
    /// Decodes all instructions in a bytecode array.
    /// </summary>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <returns>Sequence of decoded instructions with their PCs.</returns>
    IEnumerable<(int PC, Instruction Instruction)> DecodeAll(byte[] bytecodes);
}

/// <summary>
/// Decodes Smalltalk-80 bytecodes into typed instruction records per Blue Book Chapter 28.
/// </summary>
public class BytecodeDecoder : IBytecodeDecoder
{
    /// <inheritdoc/>
    public Instruction Decode(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0);

        byte b = bytecodes[pc++];

        return b switch
        {
            // Push receiver variable (0-15)
            <= 0x0F => new PushReceiverVariable(b),

            // Push temporary variable (16-31)
            <= 0x1F => new PushTemporaryVariable(b - 16),

            // Push literal constant (32-63)
            <= 0x3F => new PushLiteralConstant(b - 32),

            // Push literal variable (64-95)
            <= 0x5F => new PushLiteralVariable(b - 64),

            // Pop and store into receiver variable (96-103)
            <= 0x67 => new PopAndStoreReceiverVariable(b - 96),

            // Pop and store into temporary variable (104-111)
            <= 0x6F => new PopAndStoreTemporaryVariable(b - 104),

            // Push receiver (112)
            0x70 => new PushReceiver(),

            // Push true (113)
            0x71 => new PushTrue(),

            // Push false (114)
            0x72 => new PushFalse(),

            // Push nil (115)
            0x73 => new PushNil(),

            // Push -1 (116)
            0x74 => new PushMinusOne(),

            // Push 0 (117)
            0x75 => new PushZero(),

            // Push 1 (118)
            0x76 => new PushOne(),

            // Push 2 (119)
            0x77 => new PushTwo(),

            // Return receiver (120)
            0x78 => new ReturnReceiver(),

            // Return true (121)
            0x79 => new ReturnTrue(),

            // Return false (122)
            0x7A => new ReturnFalse(),

            // Return nil (123)
            0x7B => new ReturnNil(),

            // Return stack top (124)
            0x7C => new ReturnStackTop(),

            // Block return (125)
            0x7D => new BlockReturn(),

            // Extended push (128)
            0x80 => DecodeExtendedPush(bytecodes, ref pc),

            // Extended store (129)
            0x81 => DecodeExtendedStore(bytecodes, ref pc),

            // Extended pop and store (130)
            0x82 => DecodeExtendedPopAndStore(bytecodes, ref pc),

            // Single extended send (131)
            0x83 => DecodeSingleExtendedSend(bytecodes, ref pc),

            // Double extended send (132)
            0x84 => DecodeDoubleExtendedSend(bytecodes, ref pc),

            // Single extended super send (133)
            0x85 => DecodeSingleExtendedSuperSend(bytecodes, ref pc),

            // Double extended super send (134)
            0x86 => DecodeDoubleExtendedSuperSend(bytecodes, ref pc),

            // Pop stack top (135)
            0x87 => new Pop(),

            // Duplicate stack top (136)
            0x88 => new Duplicate(),

            // Push active context (137)
            0x89 => new PushActiveContext(),

            // Short unconditional jump (144-151)
            >= 0x90 and <= 0x97 => new ShortJump((b & 0x07) + 1),

            // Short conditional jump on false (152-159)
            >= 0x98 and <= 0x9F => new ShortJumpOnFalse((b & 0x07) + 1),

            // Long unconditional jump (160-167)
            >= 0xA0 and <= 0xA7 => DecodeLongJump(b, bytecodes, ref pc),

            // Long conditional jump on true (168-171)
            >= 0xA8 and <= 0xAB => DecodeLongJumpOnTrue(b, bytecodes, ref pc),

            // Long conditional jump on false (172-175)
            >= 0xAC and <= 0xAF => DecodeLongJumpOnFalse(b, bytecodes, ref pc),

            // Send arithmetic message (176-191)
            >= 0xB0 and <= 0xBF => new SendArithmetic(b - 0xB0),

            // Send special message (192-207)
            >= 0xC0 and <= 0xCF => new SendSpecial(b - 0xC0),

            // Send literal 0 args (208-223)
            >= 0xD0 and <= 0xDF => new SendLiteralZeroArgs(b - 0xD0),

            // Send literal 1 arg (224-239)
            >= 0xE0 and <= 0xEF => new SendLiteralOneArg(b - 0xE0),

            // Send literal 2 args (240-255)
            >= 0xF0 => new SendLiteralTwoArgs(b - 0xF0),

            // Unknown
            _ => new UnknownBytecode(b)
        };
    }

    /// <inheritdoc/>
    public IEnumerable<(int PC, Instruction Instruction)> DecodeAll(byte[] bytecodes)
    {
        int pc = 0;
        while (pc < bytecodes.Length)
        {
            int startPC = pc;
            var instruction = Decode(bytecodes, ref pc);
            yield return (startPC, instruction);
        }
    }

    #region Extended Instruction Decoders

    private static Instruction DecodeExtendedPush(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0x80);

        byte ext = bytecodes[pc++];
        int typeCode = (ext >> 6) & 0x03;
        int index = ext & 0x3F;
        return new ExtendedPush(typeCode, index);
    }

    private static Instruction DecodeExtendedStore(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0x81);

        byte ext = bytecodes[pc++];
        int typeCode = (ext >> 6) & 0x03;
        int index = ext & 0x3F;
        return new ExtendedStore(typeCode, index);
    }

    private static Instruction DecodeExtendedPopAndStore(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0x82);

        byte ext = bytecodes[pc++];
        int typeCode = (ext >> 6) & 0x03;
        int index = ext & 0x3F;
        return new ExtendedPopAndStore(typeCode, index);
    }

    private static Instruction DecodeSingleExtendedSend(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0x83);

        byte ext = bytecodes[pc++];
        int literalIndex = ext & 0x1F;
        int argCount = (ext >> 5) & 0x07;
        return new SingleExtendedSend(literalIndex, argCount);
    }

    private static Instruction DecodeDoubleExtendedSend(byte[] bytecodes, ref int pc)
    {
        if (pc + 1 >= bytecodes.Length)
            return new UnknownBytecode(0x84);

        byte ext1 = bytecodes[pc++];
        byte ext2 = bytecodes[pc++];
        int argCount = ext1;
        int literalIndex = ext2;
        return new DoubleExtendedSend(literalIndex, argCount);
    }

    private static Instruction DecodeSingleExtendedSuperSend(byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(0x85);

        byte ext = bytecodes[pc++];
        int literalIndex = ext & 0x1F;
        int argCount = (ext >> 5) & 0x07;
        return new SingleExtendedSuperSend(literalIndex, argCount);
    }

    private static Instruction DecodeDoubleExtendedSuperSend(byte[] bytecodes, ref int pc)
    {
        if (pc + 1 >= bytecodes.Length)
            return new UnknownBytecode(0x86);

        byte ext1 = bytecodes[pc++];
        byte ext2 = bytecodes[pc++];
        int argCount = ext1;
        int literalIndex = ext2;
        return new DoubleExtendedSuperSend(literalIndex, argCount);
    }

    private static Instruction DecodeLongJump(byte b, byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(b);

        byte ext = bytecodes[pc++];
        int offset = ((b & 0x07) - 4) * 256 + ext;
        return new LongJump(offset);
    }

    private static Instruction DecodeLongJumpOnTrue(byte b, byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(b);

        byte ext = bytecodes[pc++];
        int offset = (b & 0x03) * 256 + ext;
        return new LongJumpOnTrue(offset);
    }

    private static Instruction DecodeLongJumpOnFalse(byte b, byte[] bytecodes, ref int pc)
    {
        if (pc >= bytecodes.Length)
            return new UnknownBytecode(b);

        byte ext = bytecodes[pc++];
        int offset = (b & 0x03) * 256 + ext;
        return new LongJumpOnFalse(offset);
    }

    #endregion
}
