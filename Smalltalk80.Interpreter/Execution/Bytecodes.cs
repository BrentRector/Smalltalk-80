namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Smalltalk-80 bytecode definitions from the Blue Book.
/// </summary>
public static class Bytecodes
{
    // Stack Bytecodes (0-15)
    public const byte PushReceiverVariablePrefix = 0x00; // 0-15: Push receiver variable 0-15
    public const byte PushTemporaryLocationPrefix = 0x10; // 16-31: Push temp 0-15
    public const byte PushLiteralConstantPrefix = 0x20; // 32-63: Push literal 0-31
    public const byte PushLiteralVariablePrefix = 0x40; // 64-95: Push literal variable 0-31
    public const byte PopAndStoreReceiverVariablePrefix = 0x60; // 96-103: Pop and store into receiver variable 0-7
    public const byte PopAndStoreTemporaryLocationPrefix = 0x68; // 104-111: Pop and store into temp 0-7
    
    // Push Special Constants
    public const byte PushReceiver = 0x70; // 112
    public const byte PushTrue = 0x71; // 113
    public const byte PushFalse = 0x72; // 114
    public const byte PushNil = 0x73; // 115
    public const byte PushMinusOne = 0x74; // 116
    public const byte PushZero = 0x75; // 117
    public const byte PushOne = 0x76; // 118
    public const byte PushTwo = 0x77; // 119
    
    // Return Bytecodes
    public const byte ReturnReceiver = 0x78; // 120
    public const byte ReturnTrue = 0x79; // 121
    public const byte ReturnFalse = 0x7A; // 122
    public const byte ReturnNil = 0x7B; // 123
    public const byte ReturnTopFromMethod = 0x7C; // 124
    public const byte ReturnTopFromBlock = 0x7D; // 125
    
    // Reserved
    public const byte UnusedBytecode126 = 0x7E; // 126
    public const byte UnusedBytecode127 = 0x7F; // 127
    
    // Extended Push/Store/Pop
    public const byte ExtendedPushBytecode = 0x80; // 128
    public const byte ExtendedStoreBytecode = 0x81; // 129
    public const byte ExtendedStoreAndPopBytecode = 0x82; // 130
    
    // Extended Send
    public const byte SingleExtendedSendBytecode = 0x83; // 131
    public const byte DoubleExtendedSendBytecode = 0x84; // 132
    public const byte SingleExtendedSuperSendBytecode = 0x85; // 133
    public const byte SecondExtendedSendBytecode = 0x86; // 134
    
    // Pop
    public const byte PopStackBytecode = 0x87; // 135
    public const byte DuplicateActiveContextBytecode = 0x88; // 136
    public const byte PushActiveContextBytecode = 0x89; // 137
    
    // Unused 138-143
    
    // Jump Bytecodes
    public const byte ShortUnconditionalJumpPrefix = 0x90; // 144-151: Jump 1-8 forward
    public const byte ShortConditionalJumpPrefix = 0x98; // 152-159: Jump 1-8 if false
    public const byte LongUnconditionalJumpPrefix1 = 0xA0; // 160-167: Long jump
    public const byte LongConditionalJumpPrefix1 = 0xA8; // 168-171: Long conditional jump
    public const byte LongUnconditionalJumpPrefix2 = 0xAC; // 172-175: Long jump
    
    // Send Arithmetic/Comparison Selectors
    public const byte SendArithmeticSelectorPrefix = 0xB0; // 176-191
    public const byte SendSpecialSelectorPrefix = 0xC0; // 192-207
    
    // Send Literal Selector
    public const byte SendLiteralSelectorPrefix = 0xD0; // 208-223: Send selector 0-15 with 0-3 args
    public const byte SendLiteralSelectorWithNoArgsPrefix = 0xD0; // 208-223 
    public const byte SendLiteralSelectorWith1ArgPrefix = 0xE0; // 224-239
    public const byte SendLiteralSelectorWith2ArgsPrefix = 0xF0; // 240-255
    
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