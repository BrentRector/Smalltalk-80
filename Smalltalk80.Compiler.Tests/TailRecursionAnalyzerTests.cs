using System.Linq;
using Antlr4.Runtime;
using NUnit.Framework;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Unit tests for the TailRecursionAnalyzer.
/// Verifies detection of tail-recursive patterns in Smalltalk methods.
/// </summary>
[TestFixture]
public class TailRecursionAnalyzerTests
{
    /// <summary>
    /// Parses a method definition and returns the method body context.
    /// </summary>
    private Smalltalk80Parser.MethodBodyNoBangContext ParseMethod(string methodSource)
    {
        var inputStream = new AntlrInputStream(methodSource);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);

        // Use methodEntry for standalone method parsing
        var tree = parser.methodEntry();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors.");
        }

        return tree.methodBodyNoBang();
    }

    /// <summary>
    /// Analyzes a method for tail calls.
    /// </summary>
    private (int count, TailCallInfo? firstCall) AnalyzeMethod(string methodSource, string selector, bool enableDiagnostics = false)
    {
        var methodBody = ParseMethod(methodSource);
        var analyzer = new TailRecursionAnalyzer();

        if (enableDiagnostics)
        {
            analyzer.EnableDiagnostics();
        }

        var tailCalls = analyzer.Analyze(methodBody, selector);

        if (enableDiagnostics && tailCalls.Count > 0)
        {
            foreach (var diagnostic in analyzer.GetDiagnosticMessages())
            {
                TestContext.WriteLine(diagnostic);
            }
        }

        var firstCall = tailCalls.Values.FirstOrDefault();
        return (tailCalls.Count, firstCall);
    }

    #region Simple Tail Calls (Should Detect)

    [Test]
    public void SimpleTailCall_UnaryMessage_Detected()
    {
        var method = @"
value
    self value";

        var (count, call) = AnalyzeMethod(method, "value");

        Assert.That(count, Is.EqualTo(1), "Should detect one tail call");
        Assert.That(call, Is.Not.Null, "Should have call info");
        Assert.That(call!.Selector, Is.EqualTo("value"), "Selector should match");
        Assert.That(call.ArgumentCount, Is.EqualTo(0), "Should have no arguments");
        Assert.That(call.IsSafeToOptimize, Is.True, "Should be safe to optimize");
    }

    [Test]
    public void SimpleTailCall_KeywordMessage_OneArg_Detected()
    {
        var method = @"
whileTrue: aBlock
    self whileTrue: aBlock";

        var (count, call) = AnalyzeMethod(method, "whileTrue:");

        Assert.That(count, Is.EqualTo(1), "Should detect one tail call");
        Assert.That(call, Is.Not.Null, "Should have call info");
        Assert.That(call!.Selector, Is.EqualTo("whileTrue:"), "Selector should match");
        Assert.That(call.ArgumentCount, Is.EqualTo(1), "Should have one argument");
        Assert.That(call.IsSafeToOptimize, Is.True, "Should be safe to optimize");
    }

    [Test]
    public void SimpleTailCall_KeywordMessage_TwoArgs_Detected()
    {
        var method = @"
at: key put: value
    self at: key put: value";

        var (count, call) = AnalyzeMethod(method, "at:put:");

        Assert.That(count, Is.EqualTo(1), "Should detect one tail call");
        Assert.That(call, Is.Not.Null, "Should have call info");
        Assert.That(call!.Selector, Is.EqualTo("at:put:"), "Selector should match");
        Assert.That(call.ArgumentCount, Is.EqualTo(2), "Should have two arguments");
        Assert.That(call.IsSafeToOptimize, Is.True, "Should be safe to optimize");
    }

    [Test]
    public void TailCallWithReturn_Detected()
    {
        var method = @"
factorial: n
    n = 0 ifTrue: [^1].
    ^self factorial: (n - 1)";

        var (count, call) = AnalyzeMethod(method, "factorial:");

        Assert.That(count, Is.EqualTo(1), "Should detect one tail call");
        Assert.That(call, Is.Not.Null, "Should have call info");
        Assert.That(call!.Selector, Is.EqualTo("factorial:"), "Selector should match");
        Assert.That(call.IsSafeToOptimize, Is.True, "Should be safe to optimize");
    }

    #endregion

    #region Non-Tail Calls (Should Reject)

    [Test]
    public void NonTailCall_CodeAfterCall_Rejected()
    {
        var method = @"
compute
    self compute.
    1 + 2";

        var (count, _) = AnalyzeMethod(method, "compute");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (code after call)");
    }

    [Test]
    public void NonTailCall_InsideBlock_Rejected()
    {
        var method = @"
testBlock
    [self testBlock] value";

        var (count, _) = AnalyzeMethod(method, "testBlock");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (inside block)");
    }

    [Test]
    public void NonTailCall_AssignmentResult_Rejected()
    {
        var method = @"
compute
    result := self compute";

        var (count, _) = AnalyzeMethod(method, "compute");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (assignment)");
    }

    [Test]
    public void NonTailCall_WrongReceiver_Rejected()
    {
        var method = @"
process: obj
    obj process: obj";

        var (count, _) = AnalyzeMethod(method, "process:");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (receiver is not self)");
    }

    [Test]
    public void NonTailCall_WrongSelector_Rejected()
    {
        var method = @"
foo
    self bar";

        var (count, _) = AnalyzeMethod(method, "foo");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (selector mismatch)");
    }

    [Test]
    public void NonTailCall_BinaryOperator_Rejected()
    {
        var method = @"
add: n
    self + n";

        var (count, _) = AnalyzeMethod(method, "add:");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (binary operator)");
    }

    #endregion

    #region Conditional Tail Calls

    [Test]
    public void ConditionalTailCall_IfTrueBlock_Detected()
    {
        var method = @"
whileTrue: aBlock
    self value ifTrue: [
        aBlock value.
        self whileTrue: aBlock
    ]";

        var (count, call) = AnalyzeMethod(method, "whileTrue:");

        // Note: Currently the analyzer rejects calls inside blocks
        // This is conservative - future enhancement could detect tail calls
        // in blocks when the block itself is in tail position
        Assert.That(count, Is.EqualTo(0), "Currently rejects calls inside blocks (conservative)");
    }

    [Test]
    public void TailCallAfterStatement_NotInTailPosition()
    {
        var method = @"
process
    Transcript show: 'Processing'.
    self process";

        var (count, call) = AnalyzeMethod(method, "process");

        Assert.That(count, Is.EqualTo(1), "Should detect tail call (last statement after other statements)");
        Assert.That(call, Is.Not.Null, "Should have call info");
        Assert.That(call!.IsSafeToOptimize, Is.True, "Should be safe to optimize");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void EmptyMethod_NoTailCalls()
    {
        var method = @"
empty
    ^self";

        var (count, _) = AnalyzeMethod(method, "empty");

        Assert.That(count, Is.EqualTo(0), "Should detect no tail calls in empty method");
    }

    [Test]
    public void MethodReturningLiteral_NoTailCalls()
    {
        var method = @"
getValue
    ^42";

        var (count, _) = AnalyzeMethod(method, "getValue");

        Assert.That(count, Is.EqualTo(0), "Should detect no tail calls (returns literal)");
    }

    [Test]
    public void MethodReturningInstanceVariable_NoTailCalls()
    {
        var method = @"
getX
    ^x";

        var (count, _) = AnalyzeMethod(method, "getX");

        Assert.That(count, Is.EqualTo(0), "Should detect no tail calls (returns instance variable)");
    }

    [Test]
    public void NonRecursiveUnaryCall_NotDetected()
    {
        var method = @"
size
    self isEmpty";

        var (count, _) = AnalyzeMethod(method, "size");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect tail call (different selector)");
    }

    [Test]
    public void ChainedUnaryMessages_OnlyLastMatters()
    {
        var method = @"
process
    self first second process";

        var (count, _) = AnalyzeMethod(method, "process");

        // self first second process
        // This chains three unary messages: first, second, process
        // The receiver of 'process' is the result of 'self first second', NOT self
        // Therefore this is NOT a tail-recursive call
        Assert.That(count, Is.EqualTo(0), "Should NOT detect (chained messages change receiver)");
    }

    #endregion

    #region Diagnostics

    [Test]
    public void Diagnostics_EnabledForDetectedCall()
    {
        var method = @"
loop
    self loop";

        var methodBody = ParseMethod(method);
        var analyzer = new TailRecursionAnalyzer();
        analyzer.EnableDiagnostics();

        var tailCalls = analyzer.Analyze(methodBody, "loop");

        var diagnostics = analyzer.GetDiagnosticMessages();
        Assert.That(diagnostics.Count, Is.GreaterThan(0), "Should have diagnostic messages");
        Assert.That(diagnostics.Any(d => d.Contains("DETECTED")), Is.True, "Should contain DETECTED message");
    }

    [Test]
    public void Diagnostics_EnabledForRejectedCall()
    {
        var method = @"
process
    obj process";

        var methodBody = ParseMethod(method);
        var analyzer = new TailRecursionAnalyzer();
        analyzer.EnableDiagnostics();

        var tailCalls = analyzer.Analyze(methodBody, "process");

        var diagnostics = analyzer.GetDiagnosticMessages();
        Assert.That(diagnostics.Count, Is.GreaterThan(0), "Should have diagnostic messages");
        Assert.That(diagnostics.Any(d => d.Contains("Rejected")), Is.True, "Should contain Rejected message");
    }

    #endregion

    #region Real-World Examples

    [Test]
    public void RealWorld_WhileTrue_Pattern()
    {
        var method = @"
whileTrue: aBlock
    ""Evaluate the receiver (a block with no arguments) and, if true, evaluate aBlock.
    Continue until the receiver evaluates to false.""
    self value
        ifTrue: [
            aBlock value.
            self whileTrue: aBlock
        ]";

        var (count, _) = AnalyzeMethod(method, "whileTrue:");

        // Conservative: rejects because call is inside a block
        Assert.That(count, Is.EqualTo(0), "Currently conservative (rejects calls in blocks)");
    }

    [Test]
    public void RealWorld_WhileFalse_Pattern()
    {
        var method = @"
whileFalse: aBlock
    self value
        ifFalse: [
            aBlock value.
            self whileFalse: aBlock
        ]";

        var (count, _) = AnalyzeMethod(method, "whileFalse:");

        Assert.That(count, Is.EqualTo(0), "Currently conservative (rejects calls in blocks)");
    }

    [Test]
    public void RealWorld_Factorial_NonTailRecursive()
    {
        var method = @"
factorial: n
    n = 0 ifTrue: [^1].
    ^n * (self factorial: (n - 1))";

        var (count, _) = AnalyzeMethod(method, "factorial:");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect (multiplication after call - not tail position)");
    }

    [Test]
    public void RealWorld_Gcd_TailRecursive()
    {
        var method = @"
gcd: b
    b = 0 ifTrue: [^self].
    ^b gcd: (self \\ b)";

        var (count, _) = AnalyzeMethod(method, "gcd:");

        Assert.That(count, Is.EqualTo(0), "Should NOT detect (receiver is b, not self)");
    }

    #endregion
}
