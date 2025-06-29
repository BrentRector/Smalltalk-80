namespace Smalltalk80;

/// <summary>
/// Enumeration of a small subset of Smalltalk-80 bytecodes.
/// </summary>
public enum Bytecode : byte
{
    /// <summary>Push constant 0 onto the stack.</summary>
    PushConstantZero = 0x00,

    /// <summary>Push constant 1 onto the stack.</summary>
    PushConstantOne = 0x01,

    /// <summary>Add the top two stack values.</summary>
    Add = 0x10,

    /// <summary>Subtract the top two stack values.</summary>
    Subtract = 0x11,

    /// <summary>Return the value at the top of the stack.</summary>
    ReturnTop = 0x20
}

