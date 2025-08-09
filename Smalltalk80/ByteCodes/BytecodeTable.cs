using System;
using System.Collections.Generic;

namespace Smalltalk80.ByteCodes;

/// <summary>
///     Provides access to executable implementations for all 256 Smalltalk-80
///     bytecodes defined by the interpreter documentation.
/// </summary>
public static class BytecodeTable
{
    /// <summary>
    ///     Map from bytecode value to the operation that implements it.
    /// </summary>
    public static IReadOnlyList<IBytecodeOperation> Operations { get; } = CreateTable();

    private static IReadOnlyList<IBytecodeOperation> CreateTable()
    {
        var ops = new IBytecodeOperation[256];

        // 0-15: push receiver variable
        for (byte i = 0; i < 16; i++)
        {
            ops[i] = new Stack.PushReceiverVariableOperation(i);
        }

        // 16-31: push temporary location
        for (byte i = 0; i < 16; i++)
        {
            ops[16 + i] = new Stack.PushTemporaryVariableOperation(i);
        }

        // 32-63: push literal constant
        for (byte i = 0; i < 32; i++)
        {
            ops[32 + i] = new Stack.PushLiteralConstantOperation(i);
        }

        // 64-95: push literal variable
        for (byte i = 0; i < 32; i++)
        {
            ops[64 + i] = new Stack.PushLiteralVariableOperation(i);
        }

        // 96-103: store and pop receiver variable
        for (byte i = 0; i < 8; i++)
        {
            ops[96 + i] = new Stack.StoreAndPopReceiverVariableOperation(i);
        }

        // 104-111: store and pop temporary variable
        for (byte i = 0; i < 8; i++)
        {
            ops[104 + i] = new Stack.StoreAndPopTemporaryVariableOperation(i);
        }

        // 112-119: push receiver and special constants (canonical Blue Book)
        ops[112] = new Stack.PushSelfOperation();
        // 113-119 are NOT push true/false/nil in canonical Blue Book!
        // Those must be accessed via literal frame (32-63)
        // These are actually undefined in canonical Blue Book
        ops[113] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[114] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[115] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[116] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[117] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[118] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[119] = new NoOpOperation(); // Not defined in canonical Blue Book

        // 120-123: return operations (canonical Blue Book)
        ops[120] = new Return.ReturnConstantOperation(Return.ReturnConstantOperation.Kind.Self);
        // 121-123 are NOT returnTrue/False/Nil in canonical Blue Book!
        ops[121] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[122] = new NoOpOperation(); // Not defined in canonical Blue Book
        ops[123] = new NoOpOperation(); // Not defined in canonical Blue Book

        // 124-125: return stack top from message or block
        ops[124] = new Return.ReturnTopOperation(isBlock: false);
        ops[125] = new Return.ReturnTopOperation(isBlock: true);

        // 126-127 unused
        ops[126] = new NoOpOperation();
        ops[127] = new NoOpOperation();

        // 128: extended push
        ops[128] = new Stack.ExtendedPushOperation();

        // 129: extended store
        ops[129] = new Stack.ExtendedStoreOperation(popAfter: false);

        // 130: extended store and pop
        ops[130] = new Stack.ExtendedStoreOperation(popAfter: true);

        // 131: single extended send
        ops[131] = new Send.SingleExtendedSendOperation(superSend: false);

        // 132: double extended send
        ops[132] = new Send.DoubleExtendedSendOperation(superSend: false);

        // 133: single extended super
        ops[133] = new Send.SingleExtendedSendOperation(superSend: true);

        // 134: double extended super
        ops[134] = new Send.DoubleExtendedSendOperation(superSend: true);

        // 135: pop stack
        ops[135] = new Stack.PopStackOperation();

        // 136: duplicate stack top
        ops[136] = new Stack.DuplicateTopOperation();

        // 137: push active context
        ops[137] = new Stack.PushActiveContextOperation();

        // 138-143: unused
        for (byte i = 138; i <= 143; i++)
        {
            ops[i] = new NoOpOperation();
        }

        // 144-151: short jumps
        for (byte i = 0; i < 8; i++)
        {
            ops[144 + i] = new Jump.ShortJumpOperation((byte)(i + 1));
        }

        // 152-159: pop and jump on false short
        for (byte i = 0; i < 8; i++)
        {
            ops[152 + i] = new Jump.PopJumpFalseShortOperation((byte)(i + 1));
        }

        // 160-167: long jump
        for (byte i = 0; i < 8; i++)
        {
            ops[160 + i] = new Jump.LongJumpOperation(i);
        }

        // 168-171: pop and jump on true long
        for (byte i = 0; i < 4; i++)
        {
            ops[168 + i] = new Jump.PopJumpLongOperation(true, i);
        }

        // 172-175: pop and jump on false long
        for (byte i = 0; i < 4; i++)
        {
            ops[172 + i] = new Jump.PopJumpLongOperation(false, i);
        }

        // 176-191: send arithmetic selector
        for (byte i = 0; i < 16; i++)
        {
            ops[176 + i] = new Send.ArithmeticSelectorSendOperation(i);
        }

        // 192-207: send special selector
        for (byte i = 0; i < 16; i++)
        {
            ops[192 + i] = new Send.SpecialSelectorSendOperation(i);
        }

        // 208-223: send literal selector with 0 args
        for (byte i = 0; i < 16; i++)
        {
            ops[208 + i] = new Send.LiteralSelectorSendOperation(i, 0);
        }

        // 224-239: send literal selector with 1 arg
        for (byte i = 0; i < 16; i++)
        {
            ops[224 + i] = new Send.LiteralSelectorSendOperation(i, 1);
        }

        // 240-255: send literal selector with 2 args
        for (byte i = 0; i < 16; i++)
        {
            ops[240 + i] = new Send.LiteralSelectorSendOperation(i, 2);
        }

        return ops;
    }

    private sealed class NoOpOperation : IBytecodeOperation
    {
        public void Execute(Interpreter interpreter, byte opcode) { }
    }
}
