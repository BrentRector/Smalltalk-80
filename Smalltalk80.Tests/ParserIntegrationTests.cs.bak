using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Integration tests verifying that Smalltalk source is compiled
/// and executed correctly via <see cref="SmalltalkParser"/> and
/// <see cref="VirtualMachine"/>.
/// </summary>
public class ParserIntegrationTests
{
    /// <summary>
    /// Confirms that a simple arithmetic expression is compiled and
    /// evaluated to the expected result.
    /// </summary>
    [Fact]
    public void EvaluateSource_SimpleAddition_ReturnsSum()
    {
        var vm = new VirtualMachine();
        var result = vm.EvaluateSource("1 + 2!");
        Assert.Equal(3, result);
    }

    /// <summary>
    /// Verifies that temporary variable assignment and usage works
    /// when compiled from source code.
    /// </summary>
    [Fact(Skip = "Temporary variable parsing not yet implemented")]
    public void EvaluateSource_TemporaryAssignment_ReturnsExpectedValue()
    {
        var vm = new VirtualMachine();
        var result = vm.EvaluateSource("| x | x := 5. x + 2!");
        Assert.Equal(7, result);
    }

    /// <summary>
    /// Ensures that referencing an unknown variable results in an
    /// exception during compilation.
    /// </summary>
    [Fact]
    public void Compile_UnknownVariable_ThrowsException()
    {
        Assert.Throws<InvalidOperationException>(() => new SmalltalkParser("x").ParseMethod());
    }

    /// <summary>
    /// Validates that chained binary messages are evaluated in
    /// left-to-right order.
    /// </summary>
    [Fact]
    public void EvaluateSource_ChainedBinarySend_ReturnsSum()
    {
        var vm = new VirtualMachine();
        var result = vm.EvaluateSource("1 + 2 + 3!");
        Assert.Equal(6, result);
    }

    /// <summary>
    /// Validates that unary cascades are folded onto the receiver.
    /// </summary>
    [Fact(Skip = "Cascade parsing not yet implemented")]
    public void ParseMethod_UnaryCascade_DoesNotThrow()
    {
        var parser = new SmalltalkParser("1 doThis; doThat!");
        parser.ParseMethod();
    }

    /// <summary>
    /// Validates folding cascades following a binary message.
    /// </summary>
    [Fact(Skip = "Cascade parsing not yet implemented")]
    public void ParseMethod_BinaryCascade_DoesNotThrow()
    {
        var parser = new SmalltalkParser("1 + 1; doThat!");
        parser.ParseMethod();
    }

    /// <summary>
    /// Ensures cascades work with keyword messages.
    /// </summary>
    [Fact(Skip = "Cascade parsing not yet implemented")]
    public void ParseMethod_KeywordCascade_DoesNotThrow()
    {
        var parser = new SmalltalkParser("1 doThat: 42; doSomethingElse!");
        parser.ParseMethod();
    }

    /// <summary>
    /// Confirms cascades are folded correctly after mixed message sends.
    /// </summary>
    [Fact(Skip = "Cascade parsing not yet implemented")]
    public void ParseMethod_MixedCascade_DoesNotThrow()
    {
        var parser = new SmalltalkParser("1 + 1 doThat: 42; doSomethingElse!");
        parser.ParseMethod();
    }

    /// <summary>
    /// Verifies that a return expression can be supplied directly as a keyword
    /// argument without brackets.
    /// </summary>
    [Fact(Skip = "Return as keyword argument not supported")]
    public void ParseMethod_KeywordArgumentWithReturn_DoesNotThrow()
    {
        var parser = new SmalltalkParser("| result descr | result := false. result = descr ifFalse: ^1. ^0!");
        parser.ParseMethod();
    }
}
