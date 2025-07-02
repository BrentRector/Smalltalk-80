using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for <see cref="ByteCodes.BytecodeTable"/> ensuring every bytecode is mapped.
/// </summary>
[TestCaseOrderer("Smalltalk80.Tests.BytecodeIndexOrderer", "Smalltalk80.Tests")]
public class BytecodeTableTests
{
    /// <summary>
    /// Outputs the provided test name so bytecode dispatch can be easily
    /// correlated with the executing test.
    /// </summary>
    /// <param name="testName">The name of the running test case.</param>
    private static void Trace(string testName) =>
        Console.WriteLine($"[TRACE] {testName}");

    /// <summary>
    /// Provides all <see cref="Bytecode"/> values so that every bytecode is
    /// tested individually.
    /// </summary>
    public static IEnumerable<object[]> AllBytecodes =>
        Enum.GetValues<Bytecode>()
            .Select(bc => new object[] { bc });
    /// <summary>
    /// Ensures each <see cref="Bytecode"/> has a corresponding operation in
    /// <see cref="ByteCodes.BytecodeTable.Operations"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllBytecodes))]
    public void EveryBytecodeHasAnOperation(Bytecode bc)
    {
        Trace($"{nameof(EveryBytecodeHasAnOperation)}_{bc}");
        var operations = ByteCodes.BytecodeTable.Operations;
        Assert.Equal(256, operations.Count);
        var op = operations[(byte)bc];
        Assert.NotNull(op);
    }
}
