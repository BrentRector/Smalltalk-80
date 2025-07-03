using System;
using System.Linq;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for <see cref="ByteCodes.BytecodeTable"/> ensuring every bytecode is mapped.
/// </summary>
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
    /// Ensures each <see cref="Bytecode"/> has a corresponding operation in
    /// <see cref="ByteCodes.BytecodeTable.Operations"/>. When all bytecodes are
    /// supported, a single trace message is emitted. Otherwise a trace message is
    /// written for every missing bytecode and the test fails.
    /// </summary>
    [Fact]
    public void EveryBytecodeHasAnOperation()
    {
        Trace(nameof(EveryBytecodeHasAnOperation));
        var operations = ByteCodes.BytecodeTable.Operations;
        Assert.Equal(256, operations.Count);

        var missing = Enum.GetValues<Bytecode>()
                          .Where(bc => operations[(byte)bc] is null)
                          .ToArray();

        if (missing.Length == 0)
        {
            Console.WriteLine("[TRACE] All bytecodes are supported.");
        }
        else
        {
            foreach (var bc in missing)
            {
                Console.WriteLine($"[TRACE] Operation missing for bytecode {bc}.");
            }
        }

        Assert.True(missing.Length == 0,
            $"Missing operations for: {string.Join(", ", missing)}");
    }
}
