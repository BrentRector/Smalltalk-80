using System;
using System.IO;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests ensuring that <see cref="Tracer"/> correctly reports instance variable
/// information when parsing methods.
/// </summary>
public class TracerTests
{
    [Fact(Skip = "Tracer not used in current implementation")]
    public void ParseMethod_LogsInstanceVariables()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            Tracer.Enabled = true;
            var parser = new SmalltalkParser("selectors at: index!");
            parser.ParseMethod(new[] { "index" }, new[] { "selectors" }, "selectorAt:");
            var output = writer.ToString();
            Assert.Contains("Instance variables in scope: selectors", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
