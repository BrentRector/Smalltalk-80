using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Additional opcode coverage tests exercising groups not explicitly validated
/// elsewhere.
/// </summary>
[TestCaseOrderer("Smalltalk80.Tests.BytecodeIndexOrderer", "Smalltalk80.Tests")]
public class RemainingBytecodeTests
{
    /// <summary>
    ///     Outputs the provided test name to the console so that bytecode
    ///     dispatch can be correlated with individual test execution.
    /// </summary>
    /// <param name="testName">The name of the running test.</param>
    private static void Trace(string testName) =>
        Console.WriteLine($"[TRACE] {testName}");

    /// <summary>
    ///     Provides indices for <see cref="Bytecode.PushReceiverVariable0"/> through
    ///     <see cref="Bytecode.PushReceiverVariable15"/> so that each bytecode is
    ///     tested individually.
    /// </summary>
    public static IEnumerable<object[]> ReceiverVariableIndices =>
        Enumerable.Range(0, 16).Select(i => new object[] { i });

    /// <summary>
    ///     Verifies that the <see cref="Bytecode"/> instructions for pushing
    ///     receiver variables correctly push each field value onto the stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReceiverVariableIndices))]
    public void PushReceiverVariable_PushesFieldValue(int index)
    {
        Trace($"{nameof(PushReceiverVariable_PushesFieldValue)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 16);
        receiver.Fields[index] = index;
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)((int)Bytecode.PushReceiverVariable0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(index, result);
    }

    /// <summary>
    ///     Supplies indices for <see cref="Bytecode.PushTemporaryVariable0"/> through
    ///     <see cref="Bytecode.PushTemporaryVariable15"/>.
    /// </summary>
    public static IEnumerable<object[]> TemporaryVariableIndices =>
        Enumerable.Range(0, 16).Select(i => new object[] { i });

    /// <summary>
    ///     Validates that temporary variable push bytecodes correctly push each
    ///     temporary value onto the stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(TemporaryVariableIndices))]
    public void PushTemporaryVariable_PushesTempValue(int index)
    {
        Trace($"{nameof(PushTemporaryVariable_PushesTempValue)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[16];
        temps[index] = index + 10;
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)((int)Bytecode.PushTemporaryVariable0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 16
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver, temps);
        Assert.Equal(index + 10, result);
    }

    /// <summary>
    ///     Supplies indices for <see cref="Bytecode.PushLiteralConstant0"/> through
    ///     <see cref="Bytecode.PushLiteralConstant31"/>.
    /// </summary>
    public static IEnumerable<object[]> LiteralConstantIndices =>
        Enumerable.Range(0, 32).Select(i => new object[] { i });

    /// <summary>
    ///     Ensures that literal constant push instructions retrieve the
    ///     appropriate literal value for every valid index.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiteralConstantIndices))]
    public void PushLiteralConstant_PushesLiteralValue(int index)
    {
        Trace($"{nameof(PushLiteralConstant_PushesLiteralValue)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var literals = new object[32];
        literals[index] = index * 2;
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)((int)Bytecode.PushLiteralConstant0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = literals,
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(index * 2, result);
    }

    /// <summary>
    ///     Supplies indices for <see cref="Bytecode.PushLiteralVariable0"/> through
    ///     <see cref="Bytecode.PushLiteralVariable31"/>.
    /// </summary>
    public static IEnumerable<object[]> LiteralVariableIndices =>
        Enumerable.Range(0, 32).Select(i => new object[] { i });

    /// <summary>
    ///     Confirms that pushing literal variables retrieves values from each
    ///     <see cref="Association"/> stored in the literal frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiteralVariableIndices))]
    public void PushLiteralVariable_PushesAssociationValue(int index)
    {
        Trace($"{nameof(PushLiteralVariable_PushesAssociationValue)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var literals = new object[32];
        literals[index] = new Association(new Symbol($"A{index}"), index);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)((int)Bytecode.PushLiteralVariable0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = literals,
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(index, result);
    }

    /// <summary>
    ///     Provides indices for <see cref="Bytecode.StoreAndPopReceiverVariable0"/> through
    ///     <see cref="Bytecode.StoreAndPopReceiverVariable7"/>.
    /// </summary>
    public static IEnumerable<object[]> StoreReceiverIndices =>
        Enumerable.Range(0, 8).Select(i => new object[] { i });

    /// <summary>
    ///     Checks that the store-and-pop receiver variable bytecodes update the
    ///     correct field and remove the value from the stack for each index.
    /// </summary>
    [Theory]
    [MemberData(nameof(StoreReceiverIndices))]
    public void StoreAndPopReceiverVariable_StoresFieldAndPops(int index)
    {
        Trace($"{nameof(StoreAndPopReceiverVariable_StoresFieldAndPops)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 8);
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)((int)Bytecode.StoreAndPopReceiverVariable0 + index),
                (byte)((int)Bytecode.PushReceiverVariable0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver);
        Assert.Equal(1, receiver.Fields[index]);
        Assert.Equal(1, result);
    }

    /// <summary>
    ///     Supplies indices for <see cref="Bytecode.StoreAndPopTemporaryVariable0"/> through
    ///     <see cref="Bytecode.StoreAndPopTemporaryVariable7"/>.
    /// </summary>
    public static IEnumerable<object[]> StoreTempIndices =>
        Enumerable.Range(0, 8).Select(i => new object[] { i });

    /// <summary>
    ///     Verifies that each store-and-pop temporary variable bytecode writes
    ///     to the correct temporary slot and removes the value from the stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(StoreTempIndices))]
    public void StoreAndPopTemporaryVariable_StoresTempAndPops(int index)
    {
        Trace($"{nameof(StoreAndPopTemporaryVariable_StoresTempAndPops)}_{index}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var temps = new object?[8];
        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushOne,
                (byte)((int)Bytecode.StoreAndPopTemporaryVariable0 + index),
                (byte)((int)Bytecode.PushTemporaryVariable0 + index),
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 8
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(method, receiver, temps);
        Assert.Equal(1, temps[index]);
        Assert.Equal(1, result);
    }

    /// <summary>
    ///     Provides constant bytecodes and their expected values.
    /// </summary>
    public static IEnumerable<object?[]> ConstantBytecodes => new (Bytecode code, object? value)[]
    {
        (Bytecode.PushSelf, null),
        (Bytecode.PushTrue, true),
        (Bytecode.PushFalse, false),
        (Bytecode.PushNil, null),
        (Bytecode.PushMinusOne, -1),
        (Bytecode.PushZero, 0),
        (Bytecode.PushOne, 1),
        (Bytecode.PushTwo, 2)
    }.Select(tuple => new object?[] { tuple.code, tuple.value });

    /// <summary>
    ///     Validates that constant push bytecodes place the expected literal
    ///     values on the stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConstantBytecodes))]
    public void PushConstantBytecodes_PushExpectedConstants(Bytecode code, object? expected)
    {
        Trace($"{nameof(PushConstantBytecodes_PushExpectedConstants)}_{code}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        if (code == Bytecode.PushSelf)
        {
            expected = receiver;
        }
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

    /// <summary>
    ///     Provides the unused bytecode values expected to throw when executed.
    /// </summary>
    public static IEnumerable<object[]> UnusedBytecodeValues => new object[]
    {
        Bytecode.Unused126,
        Bytecode.Unused127
    }.Select(bc => new object[] { bc });

    /// <summary>
    ///     Ensures that unimplemented bytecodes result in an exception,
    ///     highlighting missing interpreter functionality.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnusedBytecodeValues))]
    public void UnusedBytecodes_DoNothing(Bytecode code)
    {
        Trace($"{nameof(UnusedBytecodes_DoNothing)}_{code}");
        var cls = new SmalltalkClass();
        var receiver = new SmalltalkObject(cls, 0);
        var method = new CompiledMethod
        {
            Bytecodes = new[] { (byte)code },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };

        var interpreter = new Interpreter { ExitOnReturn = false };

        Assert.Throws<NotImplementedException>(() => interpreter.Evaluate(method, receiver));
    }
}
