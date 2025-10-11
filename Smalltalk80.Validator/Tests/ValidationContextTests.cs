using System.Text.Json;
using Smalltalk80.Validator.Core;
using Xunit;

namespace Smalltalk80.Validator.Tests;

public class ValidationContextTests
{
    [Fact]
    public void BuildsFromMinimalJson()
    {
        var json = """
        {
            "metadata": {},
            "symbols": ["Object", "nil"],
            "strings": [],
            "classes": [],
            "methods": [],
            "objects": [],
            "initExpressions": [],
            "patches": [],
            "installOrder": { "phases": [] }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);

        Assert.Equal(2, ctx.Symbols.Count);
        Assert.Equal("Object", ctx.Symbols[0]);
    }
}
