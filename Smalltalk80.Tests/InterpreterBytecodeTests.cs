using System;
using Xunit;

namespace Smalltalk80.Tests;

public class InterpreterBytecodeTests
{
    private static void Trace(string testName) =>
        Console.WriteLine($"[TRACE] {testName}");
    [Fact]
    public void InstanceVariablePush_ReturnsFieldValue()
    {
        Trace(nameof(InstanceVariablePush_ReturnsFieldValue));
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
        Trace(nameof(TemporaryVariablePush_ReturnsTemporaryValue));
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
        Trace(nameof(LiteralPush_ReturnsLiteral));
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
        Trace(nameof(ReturnBytecode_WithEmptyStack_ReturnsReceiver));
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
        Trace(nameof(SendSelector_AddsIntegers));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.SingleExtendedSend, 34,
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
        Trace(nameof(StoreReceiverVariable_UpdatesField));
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
        Trace(nameof(StoreTemporaryVariable_UpdatesArray));
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
        Trace(nameof(PushConstantNil_ReturnsNull));
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

    [Theory]
    [InlineData(Bytecode.PushThree, 3)]
    [InlineData(Bytecode.PushFour, 4)]
    [InlineData(Bytecode.PushFive, 5)]
    [InlineData(Bytecode.PushSix, 6)]
    [InlineData(Bytecode.PushSeven, 7)]
    [InlineData(Bytecode.PushEight, 8)]
    [InlineData(Bytecode.PushNine, 9)]
    [InlineData(Bytecode.PushTen, 10)]
    public void PushSmallIntegerConstants_ReturnExpectedValue(Bytecode code, int expected)
    {
        Trace(nameof(PushSmallIntegerConstants_ReturnExpectedValue));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)code, (byte)Bytecode.ReturnTopFromMethod },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnTrueBytecode_ReturnsTrue()
    {
        Trace(nameof(ReturnTrueBytecode_ReturnsTrue));
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
        Trace(nameof(PushLiteralVariable_ReturnsAssociationValue));
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
        Trace(nameof(StoreLiteralVariable_UpdatesAssociation));
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
    public void ExtendedStore_ReceiverVariable_WritesFieldWithoutPopping()
    {
        Trace(nameof(ExtendedStore_ReceiverVariable_WritesFieldWithoutPopping));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 10);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.ExtendedStore,
                9,
                (byte)Bytecode.PushReceiverVariable9,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1, receiver.Fields[9]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void ExtendedStore_TemporaryVariable_WritesArrayWithoutPopping()
    {
        Trace(nameof(ExtendedStore_TemporaryVariable_WritesArrayWithoutPopping));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[10];
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.ExtendedStore,
                0b01001001,
                (byte)Bytecode.PushTemporaryVariable9,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 10
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver, temps);
        Assert.Equal(1, temps[9]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void ExtendedStore_LiteralConstant_Throws()
    {
        Trace(nameof(ExtendedStore_LiteralConstant_Throws));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.ExtendedStore,
                0b10000000,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 1 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        Assert.Throws<InvalidOperationException>(() => interpreter.Evaluate(method, receiver));
    }


    [Fact]
    public void JumpShort_ForwardSkipsInstruction()
    {
        Trace(nameof(JumpShort_ForwardSkipsInstruction));
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
        Trace(nameof(JumpIfFalseShort_ForwardSkipsInstruction));
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

    [Fact]
    public void JumpIfTrueShort_ForwardSkipsInstruction()
    {
        Trace(nameof(JumpIfTrueShort_ForwardSkipsInstruction));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushTrue,
                (byte)Bytecode.JumpIfTrueShort, 1,
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
    public void JumpIfTrueLong_ForwardSkipsInstruction()
    {
        Trace(nameof(JumpIfTrueLong_ForwardSkipsInstruction));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushTrue,
                (byte)Bytecode.JumpIfTrueLong, 0, 1,
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
    public void SendSelector_UnaryMethod_InvokesMethod()
    {
        Trace(nameof(SendSelector_UnaryMethod_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 7 },
            TemporariesCount = 0
        };
        cls.MethodDictionary[new Symbol("foo")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.SendLiteralSelector,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("foo") },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(7, result);
    }

    [Fact]
    public void SendSelector_KeywordMessageWithTwoArgs_InvokesMethod()
    {
        Trace(nameof(SendSelector_KeywordMessageWithTwoArgs_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var keywordMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 42 },
            TemporariesCount = 0
        };
        cls.MethodDictionary[new Symbol("bar:with:")] = keywordMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.SingleExtendedSend, 66,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 1, 2, new Symbol("bar:with:") },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(42, result);
    }

    [Fact]
    public void SingleExtendedSend_InvokesMethod()
    {
        Trace(nameof(SingleExtendedSend_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 7 },
            TemporariesCount = 0
        };
        cls.MethodDictionary[new Symbol("foo")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.SingleExtendedSend, 0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("foo") },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(7, result);
    }

    [Fact]
    public void SingleExtendedSuper_InvokesSuperclassMethod()
    {
        Trace(nameof(SingleExtendedSuper_InvokesSuperclassMethod));
        var superCls = new SmalltalkClass();
        var subCls = new SmalltalkClass { Superclass = superCls };
        var receiver = new SmalltalkObject(subCls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 3 },
            TemporariesCount = 0
        };
        superCls.MethodDictionary[new Symbol("foo")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.SingleExtendedSuper, 0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("foo"), subCls },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(3, result);
    }

    [Fact]
    public void BlockClosure_Value_ExecutesBlock()
    {
        Trace(nameof(BlockClosure_Value_ExecutesBlock));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushActiveContext,
                (byte)Bytecode.PushZero,
                (byte)Bytecode.SingleExtendedSend, 34,
                (byte)Bytecode.JumpLong, 0, 2,
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromBlock,
                (byte)Bytecode.SingleExtendedSend, 3,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[]
            {
                42,
                0,
                new Symbol("blockCopy:"),
                new Symbol("value")
            },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);

        Assert.Equal(42, result);
    }

    [Fact]
    public void ExtendedStoreAndPop_StoresValueAndPops()
    {
        Trace(nameof(ExtendedStoreAndPop_StoresValueAndPops));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 1);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.ExtendedStoreAndPop, 0,
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
    public void PrimitiveMultiply_MultipliesIntegers()
    {
        Trace(nameof(PrimitiveMultiply_MultipliesIntegers));
        var ctx = new Context
        {
            Sender = null,
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>(),
                TemporariesCount = 0
            },
            Receiver = new SmalltalkObject(new SmalltalkClass(), 0),
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>(),
            PC = 0
        };
        ctx.Stack.Push(3);
        ctx.Stack.Push(4);
        Assert.True(Primitives.Execute(3, ctx));
        Assert.Equal(12, ctx.Stack.Pop());
    }

    [Fact]
    public void PopStack_RemovesTopValue()
    {
        Trace(nameof(PopStack_RemovesTopValue));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.PopStack,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Same(receiver, result);
    }

    [Fact]
    public void DuplicateTop_DuplicatesTopValue()
    {
        Trace(nameof(DuplicateTop_DuplicatesTopValue));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)Bytecode.DuplicateTop,
                (byte)Bytecode.SingleExtendedSend, 32,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("+") },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(2, result);
    }

    [Fact]
    public void PushActiveContext_PushesCurrentContext()
    {
        Trace(nameof(PushActiveContext_PushesCurrentContext));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushActiveContext,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.IsType<Context>(result);
    }

    [Fact]
    public void ReturnSelf_ReturnsReceiver()
    {
        Trace(nameof(ReturnSelf_ReturnsReceiver));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.ReturnSelf },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Same(receiver, result);
    }

    [Fact]
    public void ReturnFalseBytecode_ReturnsFalse()
    {
        Trace(nameof(ReturnFalseBytecode_ReturnsFalse));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.ReturnFalse },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.False((bool)result!);
    }

    [Fact]
    public void ReturnNilBytecode_ReturnsNull()
    {
        Trace(nameof(ReturnNilBytecode_ReturnsNull));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.ReturnNil },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Null(result);
    }

    [Fact]
    public void JumpIfFalseLong_ForwardSkipsInstruction()
    {
        Trace(nameof(JumpIfFalseLong_ForwardSkipsInstruction));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushFalse,
                (byte)Bytecode.JumpIfFalseLong, 0, 1,
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
    public void PopJumpFalseShort_JumpsWhenFalse()
    {
        Trace(nameof(PopJumpFalseShort_JumpsWhenFalse));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushFalse,
                (byte)Bytecode.PopJumpFalseShort1,
                (byte)Bytecode.PushOne,
                (byte)Bytecode.PushTwo,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(2, result);
    }

    [Fact]
    public void LongJump_SkipsInstruction()
    {
        Trace(nameof(LongJump_SkipsInstruction));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.LongJump0, 1,
                (byte)Bytecode.PushOne,
                (byte)Bytecode.PushTwo,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(2, result);
    }

    [Fact]
    public void PopJumpTrueLong_JumpsWhenTrue()
    {
        Trace(nameof(PopJumpTrueLong_JumpsWhenTrue));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushTrue,
                (byte)Bytecode.PopJumpTrueLong0, 1,
                (byte)Bytecode.PushOne,
                (byte)Bytecode.PushTwo,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(2, result);
    }

    [Fact]
    public void SendArithmeticSelector_AddsIntegers()
    {
        Trace(nameof(SendArithmeticSelector_AddsIntegers));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.SendArithmeticSelector0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 2, 3 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(5, result);
    }

    [Fact]
    public void SendSpecialSelector_BlockOperations()
    {
        Trace(nameof(SendSpecialSelector_BlockOperations));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushActiveContext,
                (byte)Bytecode.PushZero,
                (byte)Bytecode.SendSpecialSelector8,
                (byte)Bytecode.JumpLong, 0, 2,
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromBlock,
                (byte)Bytecode.SendSpecialSelector9,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 42 },
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(42, result);
    }

    [Fact]
    public void SendLiteralSelector_NoArgs_InvokesMethod()
    {
        Trace(nameof(SendLiteralSelector_NoArgs_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushLiteralConstant0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = new object[] { 7 },
            TemporariesCount = 0
        };
        cls.MethodDictionary[new Symbol("foo")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.SendLiteral0Args0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("foo") },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(7, result);
    }

    [Fact]
    public void SendLiteralSelector_OneArg_InvokesMethod()
    {
        Trace(nameof(SendLiteralSelector_OneArg_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushLiteralConstant0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = new object[] { 99 },
            TemporariesCount = 0
        };
        cls.MethodDictionary[new Symbol("foo:")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.SendLiteral1Arg0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("foo:"), 99 },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(99, result);
    }

    [Fact]
    public void SendLiteralSelector_TwoArgs_InvokesMethod()
    {
        Trace(nameof(SendLiteralSelector_TwoArgs_InvokesMethod));
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[] { (byte)Bytecode.PushLiteralConstant0, (byte)Bytecode.ReturnTopFromMethod },
            Literals = new object[] { 42 },
            TemporariesCount = 2
        };
        cls.MethodDictionary[new Symbol("bar:with:")] = fooMethod;

        var callMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.PushLiteralConstant1,
                (byte)Bytecode.PushLiteralConstant2,
                (byte)Bytecode.SendLiteral2Args0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { new Symbol("bar:with:"), 1, 2 },
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(callMethod, receiver);

        Assert.Equal(42, result);
    }

    [Fact]
    public void PrimitiveEquivalent_TrueWhenSameObject()
    {
        var obj = new SmalltalkObject(new SmalltalkClass(), 0);
        var ctx = new Context
        {
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>()
            },
            Receiver = obj,
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>()
        };

        ctx.Stack.Push(obj);
        ctx.Stack.Push(obj);

        var executed = Primitives.Execute(110, ctx);

        Assert.True(executed);
        Assert.True((bool)ctx.Stack.Pop()!);
    }

    [Fact]
    public void PrimitiveClass_PushesClassPointer()
    {
        var cls = new SmalltalkClass();
        var obj = new SmalltalkObject(cls, 0);
        var ctx = new Context
        {
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>()
            },
            Receiver = obj,
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>()
        };

        ctx.Stack.Push(obj);

        var executed = Primitives.Execute(111, ctx);

        Assert.True(executed);
        Assert.Same(cls, ctx.Stack.Pop());
    }

    [Fact]
    public void PrimitiveAt_ReadsIndexedField()
    {
        var cls = new SmalltalkClass();
        var obj = new SmalltalkObject(cls, 1);
        obj.Fields[0] = 7;
        var ctx = new Context
        {
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>()
            },
            Receiver = obj,
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>()
        };

        ctx.Stack.Push(obj);
        ctx.Stack.Push(1);

        var executed = Primitives.Execute(60, ctx);

        Assert.True(executed);
        Assert.Equal(7, ctx.Stack.Pop());
    }

    [Fact]
    public void PrimitiveAtPut_WritesIndexedField()
    {
        var cls = new SmalltalkClass();
        var obj = new SmalltalkObject(cls, 1);
        var ctx = new Context
        {
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>()
            },
            Receiver = obj,
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>()
        };

        ctx.Stack.Push(obj);
        ctx.Stack.Push(1);
        ctx.Stack.Push(99);

        var executed = Primitives.Execute(61, ctx);

        Assert.True(executed);
        Assert.Equal(99, obj.Fields[0]);
        Assert.Equal(99, ctx.Stack.Pop());
    }

    [Fact]
    public void PrimitiveSize_ReturnsFieldCount()
    {
        var cls = new SmalltalkClass();
        var obj = new SmalltalkObject(cls, 3);
        var ctx = new Context
        {
            Method = new CompiledMethod
            {
                Bytecodes = Array.Empty<byte>(),
                Literals = Array.Empty<object>()
            },
            Receiver = obj,
            Arguments = Array.Empty<object?>(),
            Temporaries = Array.Empty<object>(),
            Stack = new Stack<object?>()
        };

        ctx.Stack.Push(obj);

        var executed = Primitives.Execute(62, ctx);

        Assert.True(executed);
        Assert.Equal(3, ctx.Stack.Pop());
    }

}

