using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for edge cases and special scenarios in compilation.
/// </summary>
[TestFixture]
public class EdgeCaseTests : CompilerTestBase
{
    [Test]
    public void TestEmptyMethod()
    {
        var source = WrapInTestClass(@"
emptyMethod
    ""This method does nothing""");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Empty method should return self
        AssertBytecode(method, 120);
    }
    
    [Test]
    public void TestVeryLongSelector()
    {
        var source = WrapInTestClass(@"
thisIsAVeryLongMethodNameToTestHowTheCompilerHandlesLongSelectorsInTheSystem
    ^42");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That(method["selector"]?.ToString(), 
            Is.EqualTo("thisIsAVeryLongMethodNameToTestHowTheCompilerHandlesLongSelectorsInTheSystem"));
        AssertBytecode(method, 32, 124);
    }
    
    [Test]
    public void TestManyArguments()
    {
        var source = WrapInTestClass(@"
with: a and: b also: c plus: d including: e
    ^a + b + c + d + e");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That((int)method["numArgs"]!, Is.EqualTo(5));
        Assert.That(method["selector"]?.ToString(), Is.EqualTo("with:and:also:plus:including:"));
    }
    
    [Test]
    public void TestDeepNesting()
    {
        var source = WrapInTestClass(@"
testDeepNesting
    ^((((((1 + 2) * 3) - 4) + 5) * 6) - 7)");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should handle deep expression nesting
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should have multiple arithmetic operations
        var arithmeticOps = bytecodes.Select(t => (int)t).Count(b => b >= 176 && b <= 191);
        Assert.That(arithmeticOps, Is.GreaterThanOrEqualTo(6), "Should have all arithmetic operations");
    }
    
    [Test]
    public void TestDeepBlockNesting()
    {
        var source = WrapInTestClass(@"
testDeepBlockNesting
    ^[[[[42]]]] value value value value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have multiple nested blocks
        var bytecodes = (JArray)method["bytecode"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.EqualTo(4), "Should have 4 nested blocks");
    }
    
    [Test]
    public void TestLargeLiteralPool()
    {
        var source = WrapInTestClass(@"
testManyLiterals
    | array |
    array := Array new: 20.
    array at: 1 put: 'literal1'.
    array at: 2 put: 'literal2'.
    array at: 3 put: 'literal3'.
    array at: 4 put: 'literal4'.
    array at: 5 put: 'literal5'.
    array at: 6 put: #symbol1.
    array at: 7 put: #symbol2.
    array at: 8 put: #symbol3.
    array at: 9 put: #symbol4.
    array at: 10 put: #symbol5.
    array at: 11 put: 100.
    array at: 12 put: 200.
    array at: 13 put: 300.
    array at: 14 put: 400.
    array at: 15 put: 500.
    array at: 16 put: $A.
    array at: 17 put: $B.
    array at: 18 put: $C.
    array at: 19 put: 3.14.
    array at: 20 put: 2.718.
    ^array");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should handle many literals efficiently
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Count, Is.GreaterThan(10), "Should have many literals");
        
        // Check for extended literal access if needed (>31 literals)
        if (literals.Count > 31)
        {
            var bytecodes = (JArray)method["bytecode"]!;
            var hasExtended = bytecodes.Select(t => (int)t).Any(b => b == 132);
            Assert.That(hasExtended, Is.True, "Should use extended literal access");
        }
    }
    
    [Test]
    public void TestLargeTempCount()
    {
        // Test with more than 16 temps (requires extended temp access)
        var tempVars = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"t{i}"));
        var tempInits = string.Join("\n    ", Enumerable.Range(1, 20).Select(i => $"t{i} := {i}."));
        var tempSum = string.Join(" + ", Enumerable.Range(1, 20).Select(i => $"t{i}"));
        
        var source = WrapInTestClass($@"
testManyTemps
    | {tempVars} |
    {tempInits}
    ^{tempSum}");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That((int)method["numTemps"]!, Is.EqualTo(20));
        
        // Should use extended temp access for temps > 15
        var bytecodes = (JArray)method["bytecode"]!;
        var hasExtendedTemp = bytecodes.Select(t => (int)t).Any(b => b == 128 || b == 129);
        Assert.That(hasExtendedTemp, Is.True, "Should use extended temp access");
    }
    
    [Test]
    public void TestSpecialCharactersInString()
    {
        var source = WrapInTestClass(@"
testSpecialChars
    ^'Line 1
Line 2	Tab
''Single quotes''
Special: !@#$%^&*()'");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // String should preserve special characters
        var literals = (JArray)method["literals"]!;
        var stringLiteral = literals.FirstOrDefault(l => l["type"]?.ToString() == "String");
        Assert.That(stringLiteral, Is.Not.Null);
        
        var value = stringLiteral!["value"]?.ToString();
        Assert.That(value, Does.Contain("\n"), "Should preserve newlines");
        Assert.That(value, Does.Contain("\t"), "Should preserve tabs");
        Assert.That(value, Does.Contain("'"), "Should handle escaped quotes");
    }
    
    [Test]
    public void TestEmptyBlockInConditional()
    {
        var source = WrapInTestClass(@"
testEmptyBlocks
    true ifTrue: [] ifFalse: [].
    ^nil");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should handle empty blocks correctly
        var bytecodes = (JArray)method["bytecode"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.EqualTo(2), "Should have two empty blocks");
    }
    
    [Test]
    public void TestChainedAssignment()
    {
        var source = WrapInTestClass(@"
testChainedAssignment
    | a b c |
    a := b := c := 42.
    ^a + b + c");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Chained assignments should work correctly
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should have multiple store operations
        var storeCount = bytecodes.Select(t => (int)t).Count(b => b >= 104 && b <= 111);
        Assert.That(storeCount, Is.GreaterThanOrEqualTo(3), "Should store to all three temps");
    }
    
    [Test]
    public void TestParenthesizedReceiver()
    {
        var source = WrapInTestClass(@"
testParenReceiver
    ^(3 + 4) negated");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should evaluate parenthesized expression first
        var bytecodes = (JArray)method["bytecode"]!;
        
        // + should come before negated
        var plusIndex = Array.IndexOf(bytecodes.Select(t => (int)t).ToArray(), 176);
        var literals = (JArray)method["literals"]!;
        var hasNegated = literals.Any(l => l["value"]?.ToString() == "negated");
        
        Assert.That(plusIndex >= 0, Is.True, "Should have + operation");
        Assert.That(hasNegated, Is.True, "Should have negated message");
    }
    
    [Test]
    public void TestArrayLiteralWithExpressions()
    {
        var source = WrapInTestClass(@"
testArrayExpressions
    ^{1 + 2. 3 * 4. 'string'. #symbol}");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Dynamic array with expressions (uses braces)
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should evaluate expressions and build array
        var hasArithmetic = bytecodes.Select(t => (int)t).Any(b => b >= 176 && b <= 191);
        Assert.That(hasArithmetic, Is.True, "Should evaluate arithmetic expressions");
    }
    
    [Test]
    public void TestCascadeOnLiteral()
    {
        var source = WrapInTestClass(@"
testLiteralCascade
    ^'hello' size; yourself");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Cascade on literal string
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(136), Is.True, "Should have dup for cascade");
    }
    
    [Test]
    public void TestBlockWithoutValue()
    {
        var source = WrapInTestClass(@"
testUnusedBlock
    | block |
    block := [42].
    ^'ignoring block'");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Block created but never evaluated
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should create block");
        
        // Should not have value send
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.False, 
            "Should not send value to block");
    }
    
    [Test]
    public void TestAssignmentAsExpression()
    {
        var source = WrapInTestClass(@"
testAssignmentExpression
    | a b |
    b := (a := 5) + 10.
    ^b");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Assignment returns the assigned value
        var bytecodes = (JArray)method["bytecode"]!;
        
        // Should store a and use its value in addition
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store to first temp");
        Assert.That(bytecodes.Select(t => (int)t).Contains(176), Is.True, "Should have + operation");
    }
    
    [Test]
    public void TestNegativeArrayIndex()
    {
        var source = WrapInTestClass(@"
testNegativeIndex
    ^#(1 2 3) at: -1");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should compile negative literal correctly
        var literals = (JArray)method["literals"]!;
        var hasNegOne = literals.Any(l => 
            l["type"]?.ToString() == "SmallInteger" && (int)l["value"]! == -1);
        
        // -1 might use special bytecode 116
        var bytecodes = (JArray)method["bytecode"]!;
        var hasMinusOne = bytecodes.Select(t => (int)t).Contains(116);
        
        Assert.That(hasNegOne || hasMinusOne, Is.True, "Should handle -1 literal");
    }
}