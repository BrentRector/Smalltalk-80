using System;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for <see cref="ByteCodes.BytecodeTable"/> ensuring every bytecode is mapped.
/// </summary>
public class BytecodeTableTests
{
    /// <summary>
    /// Ensures each <see cref="Bytecode"/> has a corresponding operation in
    /// <see cref="ByteCodes.BytecodeTable.Operations"/>.
    /// </summary>
    [Fact]
    public void EveryBytecodeHasAnOperation()
    {
        var operations = ByteCodes.BytecodeTable.Operations;
        Assert.Equal(256, operations.Count);
        foreach (var bc in Enum.GetValues<Bytecode>())
        {
            var op = operations[(byte)bc];
            Assert.NotNull(op);
        }
    }
}
