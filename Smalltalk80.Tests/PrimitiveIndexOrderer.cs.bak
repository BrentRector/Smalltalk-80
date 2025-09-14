using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Smalltalk80.Tests;

/// <summary>
/// Orders <see cref="ITestCase"/> instances by their first integer argument to
/// ensure primitive tests execute in deterministic numeric order.
/// </summary>
public sealed class PrimitiveIndexOrderer : ITestCaseOrderer
{
    /// <inheritdoc/>
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases
            .OrderBy(tc => tc.TestMethodArguments is { Length: > 0 } && tc.TestMethodArguments[0] is int index
                             ? index
                             : int.MaxValue);
}
