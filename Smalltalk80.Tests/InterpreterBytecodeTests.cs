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
            Bytecodes = new byte[] { 0, 124 },
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
            Bytecodes = new byte[] { 16, 124 },
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
            Bytecodes = new byte[] { 32, 124 },
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
            Bytecodes = new byte[] { 124 },
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
            Bytecodes = new byte[] { 32, 33, 130, 124 },
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
            Bytecodes = new byte[] { 118, 96, 0, 124 },
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
            Bytecodes = new byte[] { 118, 104, 16, 124 },
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
            Bytecodes = new byte[] { 115, 124 },
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
            Bytecodes = new byte[] { 121 },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(true, result);
    }
}
