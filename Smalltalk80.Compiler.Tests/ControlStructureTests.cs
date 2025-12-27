using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for control structures including conditionals, loops, and boolean operations.
/// </summary>
[TestFixture]
public class ControlStructureTests : CompilerTestBase
{
    [Test]
    public void TestIfTrue()
    {
        var source = WrapInTestClass(@"
testIfTrue
    true ifTrue: [^1].
    ^2");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // ifTrue: is optimized with jump bytecodes
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);

        // Blue Book conditional jumps:
        // 0x98-0x9F (152-159): Short BFP (jump on false)
        // 0xAC-0xAF (172-175): Long BFP (jump on false)
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "Should contain conditional jump on false (BFP)");
    }
    
    [Test]
    public void TestIfFalse()
    {
        var source = WrapInTestClass(@"
testIfFalse
    false ifFalse: [^1].
    ^2");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // ifFalse: is optimized with jump bytecodes
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);

        // Blue Book conditional jumps:
        // 0xA8-0xAB (168-171): Long BTP (jump on true)
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            b >= 0xA8 && b <= 0xAB);
        Assert.That(hasConditionalJump, Is.True, "Should contain conditional jump on true (BTP)");
    }
    
    [Test]
    public void TestIfTrueIfFalse()
    {
        var source = WrapInTestClass(@"
testIfTrueIfFalse
    ^x > 0
        ifTrue: [1]
        ifFalse: [-1]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // ifTrue:ifFalse: is optimized with conditional jumps and unconditional jump
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);

        // Should have conditional jump bytecodes (BFP: 0x98-0x9F or 0xAC-0xAF)
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "Should contain conditional jump for if-then-else");
    }
    
    [Test]
    public void TestNestedIfStatements()
    {
        var source = WrapInTestClass(@"
testNestedIf
    ^x > 0
        ifTrue: [
            y > 0
                ifTrue: [1]
                ifFalse: [2]]
        ifFalse: [-1]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Nested conditionals require multiple jumps
        var bytecodes = (JArray)method["bytecodes"]!;
        // Blue Book jump bytecodes: 0x90-0xAF (144-175)
        var jumpCount = bytecodes.Select(t => (int)t).Count(b =>
            b >= 0x90 && b <= 0xAF);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2), "Should have multiple jumps for nested ifs");
    }
    
    [Test]
    public void TestWhileTrue()
    {
        var source = WrapInTestClass(@"
testWhileTrue
    | i |
    i := 0.
    [i < 10] whileTrue: [i := i + 1].
    ^i");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // whileTrue: should be sent as message (not inlined in this compiler)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);

        // whileTrue: selector should be in literals OR blocks should exist
        var hasSelector = LiteralContainsSymbol(json, method, "whileTrue:");
        var hasBlock = bytecodes.Select(t => (int)t).Any(b => b == 200);
        var hasJumpLoop = bytecodes.Select(t => (int)t).Any(b => b >= 0x90 && b <= 0xAF); // Jump bytecodes

        // Either sent as message with blocks, or inlined with jumps
        Assert.That(hasSelector || hasBlock || hasJumpLoop, Is.True,
            "whileTrue: should either have selector in literals, create blocks, or be inlined with jumps");
    }

    [Test]
    public void TestWhileFalse()
    {
        var source = WrapInTestClass(@"
testWhileFalse
    | i |
    i := 10.
    [i > 0] whileFalse: [i := i - 1].
    ^i");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // whileFalse: should be sent as message (not inlined in this compiler)
        var bytecodes = (JArray)method["bytecodes"]!;

        // whileFalse: selector should be in literals OR blocks should exist
        var hasSelector = LiteralContainsSymbol(json, method, "whileFalse:");
        var hasBlock = bytecodes.Select(t => (int)t).Any(b => b == 200);
        var hasJumpLoop = bytecodes.Select(t => (int)t).Any(b => b >= 0x90 && b <= 0xAF); // Jump bytecodes

        // Either sent as message with blocks, or inlined with jumps
        Assert.That(hasSelector || hasBlock || hasJumpLoop, Is.True,
            "whileFalse: should either have selector in literals, create blocks, or be inlined with jumps");
    }

    [Test]
    public void TestTimesRepeat()
    {
        var source = WrapInTestClass(@"
testTimesRepeat
    | sum |
    sum := 0.
    5 timesRepeat: [sum := sum + 1].
    ^sum");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // timesRepeat: is sent as regular message (not optimized)
        Assert.That(LiteralContainsSymbol(json, method, "timesRepeat:"), Is.True, "Should have timesRepeat: in literals");
    }

    [Test]
    public void TestToDo()
    {
        var source = WrapInTestClass(@"
testToDo
    | sum |
    sum := 0.
    1 to: 10 do: [:i | sum := sum + i].
    ^sum");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // to:do: is sent as message (not optimized in this compiler)
        Assert.That(LiteralContainsSymbol(json, method, "to:do:"), Is.True, "Should have to:do: in literals");

        // Block should be created for the loop body
        var bytecodes = (JArray)method["bytecodes"]!;
        var hasBlock = bytecodes.Select(t => (int)t).Any(b => b == 200);
        Assert.That(hasBlock, Is.True, "Should create block for to:do:");
    }

    [Test]
    public void TestToByDo()
    {
        var source = WrapInTestClass(@"
testToByDo
    | sum |
    sum := 0.
    1 to: 10 by: 2 do: [:i | sum := sum + i].
    ^sum");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // to:by:do: sent as regular message
        Assert.That(LiteralContainsSymbol(json, method, "to:by:do:"), Is.True, "Should have to:by:do: in literals");
    }
    
    [Test]
    public void TestAnd()
    {
        var source = WrapInTestClass(@"
testAnd
    ^(x > 0) and: [y > 0]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // and: is inlined with conditional jumps - NO block is created
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have conditional jump (BFP: 0x98-0x9F or 0xAC-0xAF)
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "and: should generate jump-on-false for short-circuit");
    }

    [Test]
    public void TestOr()
    {
        var source = WrapInTestClass(@"
testOr
    ^(x < 0) or: [x > 10]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // or: is inlined with conditional jumps - NO block is created
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have conditional jump (BTP: 0xA8-0xAB)
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            b >= 0xA8 && b <= 0xAB);
        Assert.That(hasConditionalJump, Is.True, "or: should generate jump-on-true for short-circuit");
    }

    [Test]
    public void TestShortCircuitAnd()
    {
        var source = WrapInTestClass(@"
testShortCircuitAnd
    ^false and: [1/0]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // and: is inlined - conditional jump skips the block body if false
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have conditional jump for short-circuit
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "and: should generate jump-on-false");
    }

    [Test]
    public void TestShortCircuitOr()
    {
        var source = WrapInTestClass(@"
testShortCircuitOr
    ^true or: [1/0]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // or: is inlined - conditional jump skips the block body if true
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have conditional jump for short-circuit
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            b >= 0xA8 && b <= 0xAB);
        Assert.That(hasConditionalJump, Is.True, "or: should generate jump-on-true");
    }

    [Test]
    public void TestComplexConditional()
    {
        var source = WrapInTestClass(@"
testComplexCondition
    ^(x > 0 and: [y > 0]) or: [z = 0]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Complex boolean expressions with inlined and:/or:
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have multiple conditional jumps (both BFP and BTP)
        var hasJumpOnFalse = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        var hasJumpOnTrue = bytecodes.Select(t => (int)t).Any(b =>
            b >= 0xA8 && b <= 0xAB);
        Assert.That(hasJumpOnFalse, Is.True, "and: should generate jump-on-false");
        Assert.That(hasJumpOnTrue, Is.True, "or: should generate jump-on-true");
    }
    
    [Test]
    public void TestCaseAnalysis()
    {
        var source = WrapInTestClass(@"
testCaseAnalysis
    x = 1 ifTrue: [^'one'].
    x = 2 ifTrue: [^'two'].
    x = 3 ifTrue: [^'three'].
    ^'other'");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Multiple sequential conditionals
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have multiple conditional jumps (0x90-0xAF)
        var jumpCount = bytecodes.Select(t => (int)t).Count(b =>
            b >= 0x90 && b <= 0xAF);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(3), "Should have multiple conditional jumps");
    }

    [Test]
    public void TestEarlyReturn()
    {
        var source = WrapInTestClass(@"
testEarlyReturn
    x < 0 ifTrue: [^-1].
    x = 0 ifTrue: [^0].
    ^1");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Early returns in conditionals
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have multiple return bytecodes (0x7C = 124 for RETURN_TOP)
        var returnCount = bytecodes.Select(t => (int)t).Count(b => b == 0x7C);
        Assert.That(returnCount, Is.GreaterThanOrEqualTo(3), "Should have multiple returns");
    }

    [Test]
    public void TestInfiniteLoop()
    {
        var source = WrapInTestClass(@"
testInfiniteLoop
    [true] whileTrue: [x := x + 1]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // whileTrue: should be sent as message (not inlined in this compiler)
        var bytecodes = (JArray)method["bytecodes"]!;

        // whileTrue: selector should be in literals OR blocks should exist
        var hasSelector = LiteralContainsSymbol(json, method, "whileTrue:");
        var hasBlock = bytecodes.Select(t => (int)t).Any(b => b == 200);
        var hasJumpLoop = bytecodes.Select(t => (int)t).Any(b => b >= 0x90 && b <= 0xAF); // Jump bytecodes

        // Either sent as message with blocks, or inlined with jumps
        Assert.That(hasSelector || hasBlock || hasJumpLoop, Is.True,
            "whileTrue: should either have selector in literals, create blocks, or be inlined with jumps");
    }
}