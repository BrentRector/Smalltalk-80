using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Smalltalk80.Tests;

/// <summary>
/// Orders <see cref="ITestCase"/> instances by their first <see cref="Bytecode"/>
/// argument so that bytecode tests execute in deterministic numeric order.
/// </summary>
public sealed class BytecodeIndexOrderer : ITestCaseOrderer
{
    /// <inheritdoc/>
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases
            .OrderBy(tc => tc.TestMethodArguments switch
            {
                { Length: > 0 } args when args[0] is Bytecode bc => (int)bc,
                { Length: > 0 } args when args[0] is int index => index,
                _ => int.MaxValue
            });
}
