using System;
using System.IO;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests covering the <see cref="VirtualMachine.RunRepl"/> interactive loop.
/// </summary>
public class ReplTests
{
    private static string RunReplWithInput(string input)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            using var reader = new StringReader(input);
            using var writer = new StringWriter();
            Console.SetIn(reader);
            Console.SetOut(writer);
            var vm = new VirtualMachine();
            vm.RunRepl();
            return writer.ToString();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void RunRepl_EvaluatesSimpleExpression()
    {
        var output = RunReplWithInput("1 + 2\nexit\n");
        Assert.Contains("=> 3", output);
    }

    [Fact]
    public void RunRepl_HandlesExitCommand()
    {
        var output = RunReplWithInput("exit\n");
        Assert.DoesNotContain("=>", output);
    }

    [Fact]
    public void RunRepl_PrintsErrorOnInvalidInput()
    {
        var output = RunReplWithInput("1 +\nexit\n");
        Assert.Contains("!", output);
    }
}
