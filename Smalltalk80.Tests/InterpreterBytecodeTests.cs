using System;
using Xunit;

namespace Smalltalk80.Tests;

public class InterpreterBytecodeTests
{
    [Fact]
    public void InstanceVariablePush_ReturnsFieldValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 1);
        receiver.Fields[0] = 42;
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushReceiverVariable0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(42, result);
    }

    [Fact]
    public void TemporaryVariablePush_ReturnsTemporaryValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushTemporaryVariable0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = Array.Empty<object>(),
            TemporariesCount = 1
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver, new object?[] { 99 });
        Assert.Equal(99, result);
    }

    [Fact]
    public void LiteralPush_ReturnsLiteral()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushLiteralConstant0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = new object[] { 1337 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1337, result);
    }

    [Fact]
    public void ReturnBytecode_WithEmptyStack_ReturnsReceiver()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.ReturnTopFromMethod },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Same(receiver, result);
    }

    [Fact]
    public void SendSelector_AddsIntegers()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushLiteralConstant1,
                130,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 2, 3, new Symbol("+") },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(5, result);
    }

    [Fact]
    public void StoreReceiverVariable_UpdatesField()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 1);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.StoreAndPopReceiverVariable0,
                (byte)Bytecode.PushReceiverVariable0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1, receiver.Fields[0]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void StoreTemporaryVariable_UpdatesArray()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[] { null };
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.StoreAndPopTemporaryVariable0,
                (byte)Bytecode.PushTemporaryVariable0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 1
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver, temps);
        Assert.Equal(1, temps[0]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void PushConstantNil_ReturnsNull()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushNil, (byte)Bytecode.ReturnTopFromMethod },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Null(result);
    }

    [Fact]
    public void ReturnTrueBytecode_ReturnsTrue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.ReturnTrue },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(true, result);
    }

    [Fact]
    public void PushLiteralVariable_ReturnsAssociationValue()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var assoc = new Association(new Symbol("X"), 42);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushLiteralVariable0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = new object[] { assoc },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(42, result);
    }

    [Fact]
    public void StoreLiteralVariable_UpdatesAssociation()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var assoc = new Association(new Symbol("G"), 1);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushTwo,
                (byte)Bytecode.ExtendedStore,
                0b11000000,
                (byte)Bytecode.PushLiteralVariable0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { assoc },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(2, assoc.Value);
        Assert.Equal(2, result);
    }

    [Fact]
    public void JumpShort_ForwardSkipsInstruction()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.JumpShort, 1,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 1, 2 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1, result);
    }

    [Fact]
    public void JumpIfFalseShort_ForwardSkipsInstruction()
    {
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushFalse,
                (byte)Bytecode.JumpIfFalseShort, 1,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 1, 2 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1, result);
    }

}
