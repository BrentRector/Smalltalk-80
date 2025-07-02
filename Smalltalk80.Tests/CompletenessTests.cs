using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Provides high level completeness checks for the interpreter's opcode and primitive tables.
/// </summary>
public class CompletenessTests
{
    /// <summary>
    /// Verifies that the <see cref="Bytecode"/> enumeration defines exactly 256 distinct values.
    /// </summary>
    [Fact]
    public void Bytecode_Enum_Has256Entries()
    {
        var values = Enum.GetValues<Bytecode>();
        Assert.Equal(256, values.Length);
    }

    /// <summary>
    /// Ensures that the primitive dispatcher defines handlers for indices 1 through 116.
    /// </summary>
    [Fact]
    public void PrimitiveDispatcher_Defines116Handlers()
    {
        var field = typeof(Primitives).GetField("Handlers", BindingFlags.NonPublic | BindingFlags.Static)!
                   ?? throw new InvalidOperationException("Handlers field not found");
        var handlers = (System.Collections.IDictionary)field.GetValue(null)!;

        Assert.Equal(116, handlers.Count);
        for (int i = 1; i <= 116; i++)
        {
            Assert.True(handlers.Contains(i));
        }
    }
}
