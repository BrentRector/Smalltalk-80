using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Provides high level completeness checks for the interpreter's opcode and primitive tables.
/// </summary>
public class CompletenessTests
{
    /// <summary>
    /// Logs the given test name to aid in tracing execution.
    /// </summary>
    /// <param name="testName">The name of the current test.</param>
    private static void Trace(string testName) =>
        Console.WriteLine($"[TRACE] {testName}");
    /// <summary>
    /// Verifies that the <see cref="Bytecode"/> enumeration defines exactly 256 distinct values.
    /// </summary>
    [Fact]
    public void Bytecode_Enum_Has256Entries()
    {
        Trace(nameof(Bytecode_Enum_Has256Entries));
        var values = Enum.GetValues<Bytecode>();
        Assert.Equal(256, values.Length);
    }

    /// <summary>
    /// Ensures that the primitive dispatcher defines handlers for indices 1 through 116.
    /// </summary>
    [Fact]
    public void PrimitiveDispatcher_Has116Handlers()
    {
        Trace(nameof(PrimitiveDispatcher_Has116Handlers));
        var field = typeof(Primitives).GetField("Handlers", BindingFlags.NonPublic | BindingFlags.Static)! 
                   ?? throw new InvalidOperationException("Handlers field not found");
        var handlers = (System.Collections.IDictionary)field.GetValue(null)!;

        Assert.Equal(116, handlers.Count);
    }

    /// <summary>
    /// Verifies that each expected primitive index is present in the dispatcher.
    /// </summary>
    public static IEnumerable<object[]> PrimitiveIndices =>
        Enumerable.Range(1, 116)
                  .Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(PrimitiveIndices))]
    public void PrimitiveDispatcher_DefinesHandler(int index)
    {
        Trace($"{nameof(PrimitiveDispatcher_DefinesHandler)}_{index}");
        var field = typeof(Primitives).GetField("Handlers", BindingFlags.NonPublic | BindingFlags.Static)! 
                   ?? throw new InvalidOperationException("Handlers field not found");
        var handlers = (System.Collections.IDictionary)field.GetValue(null)!;
        Assert.True(handlers.Contains(index));
    }
}
