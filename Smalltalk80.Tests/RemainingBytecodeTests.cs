using System;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Additional opcode coverage tests exercising groups not explicitly validated
/// elsewhere.
/// </summary>
public class RemainingBytecodeTests
{
    [Fact]
    public void PushReceiverVariable_AllIndices_PushesFieldValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 16);
        for (int i = 0; i < 16; i++)
        {
            receiver.Fields[i] = i;
            var method = new CompiledMethod
            {
                Bytecodes = new byte[] { (byte)((int)Bytecode.PushReceiverVariable0 + i), (byte)Bytecode.ReturnTopFromMethod },
                Literals = Array.Empty<object>(),
                TemporariesCount = 0
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver);
            Assert.Equal(i, result);
        }
    }

    [Fact]
    public void PushTemporaryVariable_AllIndices_PushesTempValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[16];
        for (int i = 0; i < 16; i++)
        {
            temps[i] = i + 10;
            var method = new CompiledMethod
            {
                Bytecodes = new byte[] { (byte)((int)Bytecode.PushTemporaryVariable0 + i), (byte)Bytecode.ReturnTopFromMethod },
                Literals = Array.Empty<object>(),
                TemporariesCount = 16
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver, temps);
            Assert.Equal(i + 10, result);
        }
    }

    [Fact]
    public void PushLiteralConstant_AllIndices_PushesLiteralValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var literals = new object[32];
        for (int i = 0; i < 32; i++)
        {
            literals[i] = i * 2;
            var method = new CompiledMethod
            {
                Bytecodes = new byte[] { (byte)((int)Bytecode.PushLiteralConstant0 + i), (byte)Bytecode.ReturnTopFromMethod },
                Literals = literals,
                TemporariesCount = 0
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver);
            Assert.Equal(i * 2, result);
        }
    }

    [Fact]
    public void PushLiteralVariable_AllIndices_PushesAssociationValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var literals = new object[32];
        for (int i = 0; i < 32; i++)
        {
            literals[i] = new Association(new Symbol($"A{i}"), i);
            var method = new CompiledMethod
            {
                Bytecodes = new byte[] { (byte)((int)Bytecode.PushLiteralVariable0 + i), (byte)Bytecode.ReturnTopFromMethod },
                Literals = literals,
                TemporariesCount = 0
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver);
            Assert.Equal(i, result);
        }
    }

    [Fact]
    public void StoreAndPopReceiverVariable_AllIndices_StoresFieldAndPops()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 8);
        for (int i = 0; i < 8; i++)
        {
            var method = new CompiledMethod
            {
                Bytecodes = new byte[]
                {
                    (byte)Bytecode.PushOne,
                    (byte)((int)Bytecode.StoreAndPopReceiverVariable0 + i),
                    (byte)Bytecode.PushReceiverVariable0,
                    (byte)Bytecode.ReturnTopFromMethod
                },
                Literals = Array.Empty<object>(),
                TemporariesCount = 0
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver);
            Assert.Equal(1, receiver.Fields[i]);
            Assert.Equal(1, result);
        }
    }

    [Fact]
    public void StoreAndPopTemporaryVariable_AllIndices_StoresTempAndPops()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[8];
        for (int i = 0; i < 8; i++)
        {
            var method = new CompiledMethod
            {
                Bytecodes = new byte[]
                {
                    (byte)Bytecode.PushOne,
                    (byte)((int)Bytecode.StoreAndPopTemporaryVariable0 + i),
                    (byte)((int)Bytecode.PushTemporaryVariable0 + i),
                    (byte)Bytecode.ReturnTopFromMethod
                },
                Literals = Array.Empty<object>(),
                TemporariesCount = 8
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver, temps);
            Assert.Equal(1, temps[i]);
            Assert.Equal(1, result);
        }
    }

    [Fact]
    public void PushConstantBytecodes_PushExpectedConstants()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var expected = new (Bytecode code, object? value)[]
        {
            (Bytecode.PushSelf, receiver),
            (Bytecode.PushTrue, true),
            (Bytecode.PushFalse, false),
            (Bytecode.PushNil, null),
            (Bytecode.PushMinusOne, -1),
            (Bytecode.PushZero, 0),
            (Bytecode.PushOne, 1),
            (Bytecode.PushTwo, 2)
        };
        foreach (var (code, value) in expected)
        {
            var method = new CompiledMethod
            {
                Bytecodes = new byte[] { (byte)code, (byte)Bytecode.ReturnTopFromMethod },
                Literals = Array.Empty<object>(),
                TemporariesCount = 0
            };
            var interpreter = new Interpreter { ExitOnReturn = false };
            var result = interpreter.Evaluate(method, receiver);
            Assert.Equal(value, result);
        }
    }

    [Fact]
    public void UnusedBytecodes_DoNothing()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.Unused126,
                (byte)Bytecode.Unused127
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };

        Assert.Throws<NotImplementedException>(() => interpreter.Evaluate(method, receiver));
    }
}
