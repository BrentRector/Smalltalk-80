using System;

/// <summary>
///     Enumerates the bytecodes understood by the interpreter.
///     This follows the canonical Blue Book (Goldberg and Robson, 1983) bytecode specification.
/// </summary>
public enum Bytecode : byte
{
    // 0-15: Push receiver instance variables
    /// <summary>Push receiver instance variable.</summary>
    PushReceiverVariable0 = 0,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable1,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable2,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable3,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable4,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable5,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable6,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable7,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable8,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable9,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable10,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable11,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable12,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable13,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable14,
    /// <inheritdoc cref="PushReceiverVariable0" />
    PushReceiverVariable15,

    // 16-31: Push temporary variables
    /// <summary>Push temporary variable.</summary>
    PushTemporaryVariable0 = 16,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable1,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable2,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable3,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable4,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable5,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable6,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable7,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable8,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable9,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable10,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable11,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable12,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable13,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable14,
    /// <inheritdoc cref="PushTemporaryVariable0" />
    PushTemporaryVariable15,

    // 32-63: Push literal constants
    /// <summary>Push literal constant from the method literal frame.</summary>
    PushLiteralConstant0 = 32,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant1,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant2,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant3,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant4,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant5,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant6,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant7,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant8,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant9,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant10,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant11,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant12,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant13,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant14,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant15,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant16,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant17,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant18,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant19,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant20,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant21,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant22,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant23,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant24,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant25,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant26,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant27,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant28,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant29,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant30,
    /// <inheritdoc cref="PushLiteralConstant0" />
    PushLiteralConstant31,

    // 64-95: Push literal variables (Association values)
    /// <summary>Push value from Association in the literal frame.</summary>
    PushLiteralVariable0 = 64,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable1,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable2,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable3,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable4,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable5,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable6,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable7,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable8,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable9,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable10,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable11,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable12,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable13,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable14,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable15,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable16,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable17,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable18,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable19,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable20,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable21,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable22,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable23,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable24,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable25,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable26,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable27,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable28,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable29,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable30,
    /// <inheritdoc cref="PushLiteralVariable0" />
    PushLiteralVariable31,

    // 96-127: Store into receiver variables (no pop)
    /// <summary>Store into receiver variable (no pop).</summary>
    StoreIntoReceiverVariable0 = 96,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable1,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable2,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable3,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable4,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable5,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable6,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable7,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable8,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable9,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable10,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable11,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable12,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable13,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable14,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable15,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable16,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable17,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable18,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable19,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable20,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable21,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable22,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable23,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable24,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable25,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable26,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable27,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable28,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable29,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable30,
    /// <inheritdoc cref="StoreIntoReceiverVariable0" />
    StoreIntoReceiverVariable31,

    // 128-159: Store into temporary variables (no pop)
    /// <summary>Store into temporary variable (no pop).</summary>
    StoreIntoTemporaryVariable0 = 128,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable1,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable2,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable3,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable4,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable5,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable6,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable7,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable8,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable9,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable10,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable11,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable12,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable13,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable14,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable15,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable16,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable17,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable18,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable19,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable20,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable21,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable22,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable23,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable24,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable25,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable26,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable27,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable28,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable29,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable30,
    /// <inheritdoc cref="StoreIntoTemporaryVariable0" />
    StoreIntoTemporaryVariable31,

    // 160-175: Store into literal variables (no pop)
    /// <summary>Store into literal variable (Association) (no pop).</summary>
    StoreIntoLiteralVariable0 = 160,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable1,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable2,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable3,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable4,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable5,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable6,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable7,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable8,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable9,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable10,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable11,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable12,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable13,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable14,
    /// <inheritdoc cref="StoreIntoLiteralVariable0" />
    StoreIntoLiteralVariable15,

    // 176-180: Return operations
    /// <summary>Return receiver (self).</summary>
    ReturnReceiver = 176,
    /// <summary>Return constant true.</summary>
    ReturnTrue = 177,
    /// <summary>Return constant false.</summary>
    ReturnFalse = 178,
    /// <summary>Return constant nil.</summary>
    ReturnNil = 179,
    /// <summary>Return top of stack.</summary>
    ReturnTop = 180,

    // 181-191: Unused in Blue Book
    /// <summary>Reserved for future use.</summary>
    Unused181 = 181,
    /// <summary>Reserved for future use.</summary>
    Unused182,
    /// <summary>Reserved for future use.</summary>
    Unused183,
    /// <summary>Reserved for future use.</summary>
    Unused184,
    /// <summary>Reserved for future use.</summary>
    Unused185,
    /// <summary>Reserved for future use.</summary>
    Unused186,
    /// <summary>Reserved for future use.</summary>
    Unused187,
    /// <summary>Reserved for future use.</summary>
    Unused188,
    /// <summary>Reserved for future use.</summary>
    Unused189,
    /// <summary>Reserved for future use.</summary>
    Unused190,
    /// <summary>Reserved for future use.</summary>
    Unused191,

    // 192-223: Send message
    /// <summary>Send message with selector at literal index 0-31.</summary>
    SendMessage0 = 192,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage1,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage2,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage3,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage4,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage5,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage6,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage7,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage8,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage9,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage10,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage11,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage12,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage13,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage14,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage15,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage16,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage17,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage18,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage19,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage20,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage21,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage22,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage23,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage24,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage25,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage26,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage27,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage28,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage29,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage30,
    /// <inheritdoc cref="SendMessage0" />
    SendMessage31,

    // 224-255: Send super message
    /// <summary>Send super message with selector at literal index 0-31.</summary>
    SendSuperMessage0 = 224,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage1,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage2,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage3,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage4,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage5,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage6,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage7,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage8,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage9,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage10,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage11,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage12,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage13,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage14,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage15,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage16,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage17,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage18,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage19,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage20,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage21,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage22,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage23,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage24,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage25,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage26,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage27,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage28,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage29,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage30,
    /// <inheritdoc cref="SendSuperMessage0" />
    SendSuperMessage31,
}