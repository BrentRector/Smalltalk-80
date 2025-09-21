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
        
        // ifTrue: typically optimized with jump bytecodes
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain conditional jump bytecode (152-159 or 168-171)
        var hasConditionalJump = bytecodes.Cast<int>().Any(b => 
            (b >= 152 && b <= 159) || (b >= 168 && b <= 171));
        Assert.That(hasConditionalJump, Is.True, "Should contain conditional jump");
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
        
        // ifFalse: typically optimized with jump bytecodes
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain conditional jump
        var hasConditionalJump = bytecodes.Cast<int>().Any(b => 
            (b >= 152 && b <= 159) || (b >= 168 && b <= 171));
        Assert.That(hasConditionalJump, Is.True, "Should contain conditional jump");
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
        
        // ifTrue:ifFalse: is optimized with conditional jumps
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should have conditional jump bytecodes
        var jumpCount = bytecodes.Cast<int>().Count(b => 
            (b >= 152 && b <= 159) || (b >= 168 && b <= 175));
        Assert.That(jumpCount, Is.GreaterThan(0), "Should contain jumps for if-then-else");
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
        var bytecodes = (JArray)method["bytecode"]!;
        var jumpCount = bytecodes.Cast<int>().Count(b => 
            (b >= 152 && b <= 175));
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
        
        // whileTrue: uses backward jumps for looping
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain jump bytecodes for loop
        var hasJump = bytecodes.Cast<int>().Any(b => 
            (b >= 144 && b <= 175));
        Assert.That(hasJump, Is.True, "Should contain jumps for while loop");
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
        
        // whileFalse: uses backward jumps for looping
        var bytecodes = (JArray)method["bytecode"]!;
        var hasJump = bytecodes.Cast<int>().Any(b => 
            (b >= 144 && b <= 175));
        Assert.That(hasJump, Is.True, "Should contain jumps for while loop");
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
        
        // timesRepeat: is usually not optimized, sent as regular message
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "timesRepeat:"), Is.True);
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
        
        // to:do: might be optimized or sent as message
        var literals = (JArray)method["literals"]!;
        var hasToDoMessage = literals.Any(l => l["value"]?.ToString() == "to:do:");
        
        // Block should be created for the loop body
        var bytecodes = (JArray)method["bytecode"]!;
        var hasBlock = bytecodes.Cast<int>().Any(b => b == 143);
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
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "to:by:do:"), Is.True);
    }
    
    [Test]
    public void TestAnd()
    {
        var source = WrapInTestClass(@"
testAnd
    ^(x > 0) and: [y > 0]");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // and: might be optimized with conditional jumps
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should create a block for the second condition
        var hasBlock = bytecodes.Cast<int>().Any(b => b == 143);
        Assert.That(hasBlock, Is.True, "Should create block for and:");
    }
    
    [Test]
    public void TestOr()
    {
        var source = WrapInTestClass(@"
testOr
    ^(x < 0) or: [x > 10]");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // or: might be optimized with conditional jumps
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should create a block for the second condition
        var hasBlock = bytecodes.Cast<int>().Any(b => b == 143);
        Assert.That(hasBlock, Is.True, "Should create block for or:");
    }
    
    [Test]
    public void TestShortCircuitAnd()
    {
        var source = WrapInTestClass(@"
testShortCircuitAnd
    ^false and: [1/0]");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // and: should short-circuit, not evaluating the block if receiver is false
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have block creation
        var hasBlock = bytecodes.Cast<int>().Any(b => b == 143);
        Assert.That(hasBlock, Is.True, "Should create block even if not evaluated");
    }
    
    [Test]
    public void TestShortCircuitOr()
    {
        var source = WrapInTestClass(@"
testShortCircuitOr
    ^true or: [1/0]");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // or: should short-circuit, not evaluating the block if receiver is true
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have block creation
        var hasBlock = bytecodes.Cast<int>().Any(b => b == 143);
        Assert.That(hasBlock, Is.True, "Should create block even if not evaluated");
    }
    
    [Test]
    public void TestComplexConditional()
    {
        var source = WrapInTestClass(@"
testComplexCondition
    ^(x > 0 and: [y > 0]) or: [z = 0]");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Complex boolean expressions with multiple blocks
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have multiple blocks
        var blockCount = bytecodes.Cast<int>().Count(b => b == 143);
        Assert.That(blockCount, Is.GreaterThanOrEqualTo(2), "Should have multiple blocks");
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
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have multiple conditional jumps
        var jumpCount = bytecodes.Cast<int>().Count(b => 
            (b >= 152 && b <= 175));
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
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have multiple return bytecodes (124)
        var returnCount = bytecodes.Cast<int>().Count(b => b == 124);
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
        
        // Infinite loop with backward jump
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have jump bytecodes for infinite loop
        var hasJump = bytecodes.Cast<int>().Any(b => 
            (b >= 144 && b <= 175));
        Assert.That(hasJump, Is.True, "Should contain backward jump for infinite loop");
    }
}