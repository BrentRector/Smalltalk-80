namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Smalltalk-80 bytecode definitions from the Blue Book.
/// </summary>
public static class Bytecodes
{
    // Stack Bytecodes (0-15)
    /// <summary>Push receiver variable 0-15 (bytecodes 0-15).</summary>
    public const byte PushReceiverVariablePrefix = 0x00;
    /// <summary>Push temporary variable 0-15 (bytecodes 16-31).</summary>
    public const byte PushTemporaryLocationPrefix = 0x10;
    /// <summary>Push literal constant 0-31 (bytecodes 32-63).</summary>
    public const byte PushLiteralConstantPrefix = 0x20;
    /// <summary>Push literal variable 0-31 (bytecodes 64-95).</summary>
    public const byte PushLiteralVariablePrefix = 0x40;
    /// <summary>Pop and store into receiver variable 0-7 (bytecodes 96-103).</summary>
    public const byte PopAndStoreReceiverVariablePrefix = 0x60;
    /// <summary>Pop and store into temporary variable 0-7 (bytecodes 104-111).</summary>
    public const byte PopAndStoreTemporaryLocationPrefix = 0x68;
    
    // Push Special Constants
    /// <summary>Push receiver (bytecode 112).</summary>
    public const byte PushReceiver = 0x70;
    /// <summary>Push true (bytecode 113).</summary>
    public const byte PushTrue = 0x71;
    /// <summary>Push false (bytecode 114).</summary>
    public const byte PushFalse = 0x72;
    /// <summary>Push nil (bytecode 115).</summary>
    public const byte PushNil = 0x73;
    /// <summary>Push -1 (bytecode 116).</summary>
    public const byte PushMinusOne = 0x74;
    /// <summary>Push 0 (bytecode 117).</summary>
    public const byte PushZero = 0x75;
    /// <summary>Push 1 (bytecode 118).</summary>
    public const byte PushOne = 0x76;
    /// <summary>Push 2 (bytecode 119).</summary>
    public const byte PushTwo = 0x77;
    
    // Return Bytecodes
    /// <summary>Return receiver from method (bytecode 120).</summary>
    public const byte ReturnReceiver = 0x78;
    /// <summary>Return true from method (bytecode 121).</summary>
    public const byte ReturnTrue = 0x79;
    /// <summary>Return false from method (bytecode 122).</summary>
    public const byte ReturnFalse = 0x7A;
    /// <summary>Return nil from method (bytecode 123).</summary>
    public const byte ReturnNil = 0x7B;
    /// <summary>Return top of stack from method (bytecode 124).</summary>
    public const byte ReturnTopFromMethod = 0x7C;
    /// <summary>Return top of stack from block (bytecode 125).</summary>
    public const byte ReturnTopFromBlock = 0x7D;
    
    // Reserved
    /// <summary>Unused bytecode 126.</summary>
    public const byte UnusedBytecode126 = 0x7E;
    /// <summary>Unused bytecode 127.</summary>
    public const byte UnusedBytecode127 = 0x7F;
    
    // Extended Push/Store/Pop
    /// <summary>Extended push bytecode (bytecode 128).</summary>
    public const byte ExtendedPushBytecode = 0x80;
    /// <summary>Extended store bytecode (bytecode 129).</summary>
    public const byte ExtendedStoreBytecode = 0x81;
    /// <summary>Extended store and pop bytecode (bytecode 130).</summary>
    public const byte ExtendedStoreAndPopBytecode = 0x82;
    
    // Extended Send
    /// <summary>Single extended send bytecode (bytecode 131).</summary>
    public const byte SingleExtendedSendBytecode = 0x83;
    /// <summary>Double extended send bytecode (bytecode 132).</summary>
    public const byte DoubleExtendedSendBytecode = 0x84;
    /// <summary>Single extended super send bytecode (bytecode 133).</summary>
    public const byte SingleExtendedSuperSendBytecode = 0x85;
    /// <summary>Second extended send bytecode (bytecode 134).</summary>
    public const byte SecondExtendedSendBytecode = 0x86;
    
    // Pop
    /// <summary>Pop stack bytecode (bytecode 135).</summary>
    public const byte PopStackBytecode = 0x87;
    /// <summary>Duplicate top of stack bytecode (bytecode 136).</summary>
    public const byte DuplicateActiveContextBytecode = 0x88;
    /// <summary>Push active context bytecode (bytecode 137).</summary>
    public const byte PushActiveContextBytecode = 0x89;
    
    // Unused 138-143
    
    // Jump Bytecodes
    /// <summary>Short unconditional jump 1-8 forward (bytecodes 144-151).</summary>
    public const byte ShortUnconditionalJumpPrefix = 0x90;
    /// <summary>Short conditional jump 1-8 if false (bytecodes 152-159).</summary>
    public const byte ShortConditionalJumpPrefix = 0x98;
    /// <summary>Long unconditional jump (bytecodes 160-167).</summary>
    public const byte LongUnconditionalJumpPrefix1 = 0xA0;
    /// <summary>Long conditional jump (bytecodes 168-171).</summary>
    public const byte LongConditionalJumpPrefix1 = 0xA8;
    /// <summary>Long unconditional jump alternative (bytecodes 172-175).</summary>
    public const byte LongUnconditionalJumpPrefix2 = 0xAC;
    
    // Send Arithmetic/Comparison Selectors
    /// <summary>Send arithmetic selector (bytecodes 176-191).</summary>
    public const byte SendArithmeticSelectorPrefix = 0xB0;
    /// <summary>Send special selector (bytecodes 192-207).</summary>
    public const byte SendSpecialSelectorPrefix = 0xC0;
    
    // Send Literal Selector
    /// <summary>Send literal selector with 0-3 args (bytecodes 208-223).</summary>
    public const byte SendLiteralSelectorPrefix = 0xD0;
    /// <summary>Send literal selector with no args (bytecodes 208-223).</summary>
    public const byte SendLiteralSelectorWithNoArgsPrefix = 0xD0;
    /// <summary>Send literal selector with 1 arg (bytecodes 224-239).</summary>
    public const byte SendLiteralSelectorWith1ArgPrefix = 0xE0;
    /// <summary>Send literal selector with 2 args (bytecodes 240-255).</summary>
    public const byte SendLiteralSelectorWith2ArgsPrefix = 0xF0;
    
    /// <summary>
    /// Special arithmetic and comparison selectors.
    /// </summary>
    public static readonly string[] ArithmeticSelectors =
    [
        "+", "-", "<", ">", "<=", ">=", "=", "~=",
        "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
    ];
    
    /// <summary>
    /// Special selectors.
    /// </summary>
    public static readonly string[] SpecialSelectors =
    [
        "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "==", "class",
        "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y"
    ];
    
    /// <summary>
    /// Gets the number of arguments for a send bytecode.
    /// </summary>
    public static int GetSendArgCount(byte bytecode)
    {
        if (bytecode >= 0xD0 && bytecode <= 0xDF) return 0;
        if (bytecode >= 0xE0 && bytecode <= 0xEF) return 1;
        if (bytecode >= 0xF0 && bytecode <= 0xFF) return 2;
        
        // For arithmetic/special selectors, count colons in selector
        if (bytecode >= 0xB0 && bytecode <= 0xBF)
        {
            var selector = ArithmeticSelectors[bytecode - 0xB0];
            return selector.Count(c => c == ':');
        }
        if (bytecode >= 0xC0 && bytecode <= 0xCF)
        {
            var selector = SpecialSelectors[bytecode - 0xC0];
            return selector.Count(c => c == ':');
        }
        
        return 0;
    }
}