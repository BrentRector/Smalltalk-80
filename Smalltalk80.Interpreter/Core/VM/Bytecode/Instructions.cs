namespace Smalltalk80.Interpreter.Core.VM.Bytecode;

/// <summary>
/// Base type for decoded bytecode instructions per Blue Book Chapter 28.
/// Enables pattern matching and type-safe bytecode handling.
/// </summary>
public abstract record Instruction
{
    /// <summary>Gets the size of this instruction in bytes.</summary>
    public abstract int Size { get; }
}

#region Push Operations (0x00-0x6F)

/// <summary>Push receiver's instance variable (bytecodes 0-15).</summary>
public record PushReceiverVariable(int Index) : Instruction
{
    public override int Size => 1;
}

/// <summary>Push temporary variable (bytecodes 16-31).</summary>
public record PushTemporaryVariable(int Index) : Instruction
{
    public override int Size => 1;
}

/// <summary>Push literal constant (bytecodes 32-63).</summary>
public record PushLiteralConstant(int Index) : Instruction
{
    public override int Size => 1;
}

/// <summary>Push literal variable (Association value) (bytecodes 64-95).</summary>
public record PushLiteralVariable(int Index) : Instruction
{
    public override int Size => 1;
}

#endregion

#region Store Operations (0x60-0x6F)

/// <summary>Pop and store into receiver's instance variable (bytecodes 96-103).</summary>
public record PopAndStoreReceiverVariable(int Index) : Instruction
{
    public override int Size => 1;
}

/// <summary>Pop and store into temporary variable (bytecodes 104-111).</summary>
public record PopAndStoreTemporaryVariable(int Index) : Instruction
{
    public override int Size => 1;
}

#endregion

#region Special Push Operations (0x70-0x77)

/// <summary>Push receiver (bytecode 112).</summary>
public record PushReceiver : Instruction
{
    public override int Size => 1;
}

/// <summary>Push true (bytecode 113).</summary>
public record PushTrue : Instruction
{
    public override int Size => 1;
}

/// <summary>Push false (bytecode 114).</summary>
public record PushFalse : Instruction
{
    public override int Size => 1;
}

/// <summary>Push nil (bytecode 115).</summary>
public record PushNil : Instruction
{
    public override int Size => 1;
}

/// <summary>Push -1 (bytecode 116).</summary>
public record PushMinusOne : Instruction
{
    public override int Size => 1;
}

/// <summary>Push 0 (bytecode 117).</summary>
public record PushZero : Instruction
{
    public override int Size => 1;
}

/// <summary>Push 1 (bytecode 118).</summary>
public record PushOne : Instruction
{
    public override int Size => 1;
}

/// <summary>Push 2 (bytecode 119).</summary>
public record PushTwo : Instruction
{
    public override int Size => 1;
}

#endregion

#region Return Operations (0x78-0x7D)

/// <summary>Return receiver (bytecode 120).</summary>
public record ReturnReceiver : Instruction
{
    public override int Size => 1;
}

/// <summary>Return true (bytecode 121).</summary>
public record ReturnTrue : Instruction
{
    public override int Size => 1;
}

/// <summary>Return false (bytecode 122).</summary>
public record ReturnFalse : Instruction
{
    public override int Size => 1;
}

/// <summary>Return nil (bytecode 123).</summary>
public record ReturnNil : Instruction
{
    public override int Size => 1;
}

/// <summary>Return stack top to sender (bytecode 124).</summary>
public record ReturnStackTop : Instruction
{
    public override int Size => 1;
}

/// <summary>Return stack top to block caller (bytecode 125).</summary>
public record BlockReturn : Instruction
{
    public override int Size => 1;
}

#endregion

#region Stack Operations (0x7E-0x7F, 0x87-0x88)

/// <summary>Pop stack top (bytecode 135).</summary>
public record Pop : Instruction
{
    public override int Size => 1;
}

/// <summary>Duplicate stack top (bytecode 136).</summary>
public record Duplicate : Instruction
{
    public override int Size => 1;
}

/// <summary>Push active context (bytecode 137).</summary>
public record PushActiveContext : Instruction
{
    public override int Size => 1;
}

#endregion

#region Extended Operations (0x80-0x86)

/// <summary>Extended push (bytecode 128).</summary>
public record ExtendedPush(int TypeCode, int Index) : Instruction
{
    public override int Size => 2;
}

/// <summary>Extended store (bytecode 129).</summary>
public record ExtendedStore(int TypeCode, int Index) : Instruction
{
    public override int Size => 2;
}

/// <summary>Extended pop and store (bytecode 130).</summary>
public record ExtendedPopAndStore(int TypeCode, int Index) : Instruction
{
    public override int Size => 2;
}

/// <summary>Single extended send (bytecode 131).</summary>
public record SingleExtendedSend(int LiteralIndex, int ArgumentCount) : Instruction
{
    public override int Size => 2;
}

/// <summary>Double extended send (bytecode 132).</summary>
public record DoubleExtendedSend(int LiteralIndex, int ArgumentCount) : Instruction
{
    public override int Size => 3;
}

/// <summary>Single extended super send (bytecode 133).</summary>
public record SingleExtendedSuperSend(int LiteralIndex, int ArgumentCount) : Instruction
{
    public override int Size => 2;
}

/// <summary>Double extended super send (bytecode 134).</summary>
public record DoubleExtendedSuperSend(int LiteralIndex, int ArgumentCount) : Instruction
{
    public override int Size => 3;
}

#endregion

#region Jump Operations (0x90-0x9F, 0xA0-0xAF)

/// <summary>Short unconditional jump (bytecodes 144-151).</summary>
public record ShortJump(int Offset) : Instruction
{
    public override int Size => 1;
}

/// <summary>Short conditional jump on false (bytecodes 152-159).</summary>
public record ShortJumpOnFalse(int Offset) : Instruction
{
    public override int Size => 1;
}

/// <summary>Long unconditional jump (bytecodes 160-167).</summary>
public record LongJump(int Offset) : Instruction
{
    public override int Size => 2;
}

/// <summary>Long conditional jump on true (bytecodes 168-171).</summary>
public record LongJumpOnTrue(int Offset) : Instruction
{
    public override int Size => 2;
}

/// <summary>Long conditional jump on false (bytecodes 172-175).</summary>
public record LongJumpOnFalse(int Offset) : Instruction
{
    public override int Size => 2;
}

#endregion

#region Send Operations (0xB0-0xDF)

/// <summary>Send arithmetic message (bytecodes 176-191).</summary>
public record SendArithmetic(int SelectorIndex) : Instruction
{
    public override int Size => 1;

    /// <summary>Gets the arithmetic selector name.</summary>
    public string SelectorName => SelectorIndex switch
    {
        0 => "+",
        1 => "-",
        2 => "<",
        3 => ">",
        4 => "<=",
        5 => ">=",
        6 => "=",
        7 => "~=",
        8 => "*",
        9 => "/",
        10 => "\\\\",
        11 => "@",
        12 => "bitShift:",
        13 => "//",
        14 => "bitAnd:",
        15 => "bitOr:",
        _ => "?"
    };
}

/// <summary>Send special message (bytecodes 192-207).</summary>
public record SendSpecial(int SelectorIndex) : Instruction
{
    public override int Size => 1;

    /// <summary>Gets the special selector name and argument count.</summary>
    public (string Name, int ArgCount) SelectorInfo => SelectorIndex switch
    {
        0 => ("at:", 1),
        1 => ("at:put:", 2),
        2 => ("size", 0),
        3 => ("next", 0),
        4 => ("nextPut:", 1),
        5 => ("atEnd", 0),
        6 => ("==", 1),
        7 => ("class", 0),
        8 => ("blockCopy:", 1),
        9 => ("value", 0),
        10 => ("value:", 1),
        11 => ("do:", 1),
        12 => ("new", 0),
        13 => ("new:", 1),
        14 => ("x", 0),
        15 => ("y", 0),
        _ => ("?", 0)
    };
}

/// <summary>Send literal selector with N arguments (bytecodes 208-223).</summary>
public record SendLiteralZeroArgs(int LiteralIndex) : Instruction
{
    public override int Size => 1;
}

/// <summary>Send literal selector with 1 argument (bytecodes 224-239).</summary>
public record SendLiteralOneArg(int LiteralIndex) : Instruction
{
    public override int Size => 1;
}

/// <summary>Send literal selector with 2 arguments (bytecodes 240-255).</summary>
public record SendLiteralTwoArgs(int LiteralIndex) : Instruction
{
    public override int Size => 1;
}

#endregion

#region Unknown/Invalid

/// <summary>Unknown or invalid bytecode.</summary>
public record UnknownBytecode(byte BytecodeValue) : Instruction
{
    public override int Size => 1;
}

#endregion
