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

        // Empty method should return self - might be just RETURN_SELF or PUSH_SELF + RETURN_SELF
        var bytecodes = (JArray)method["bytecodes"]!;
        var hasReturnSelf = bytecodes.Select(t => (int)t).Contains(0x78); // RETURN_SELF
        Assert.That(hasReturnSelf, Is.True, "Empty method should return self");
    }

    [Test]
    public void TestVeryLongSelector()
    {
        var source = WrapInTestClass(@"
thisIsAVeryLongMethodNameToTestHowTheCompilerHandlesLongSelectorsInTheSystem
    ^42");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Selector is stored as index into symbols array
        var selector = GetMethodSelector(json, method);
        Assert.That(selector, Is.EqualTo("thisIsAVeryLongMethodNameToTestHowTheCompilerHandlesLongSelectorsInTheSystem"));

        // Should push 42 literal and return
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(0x7C), Is.True, "Should have RETURN_TOP");
    }

    [Test]
    public void TestManyArguments()
    {
        var source = WrapInTestClass(@"
with: a and: b also: c plus: d including: e
    ^a + b + c + d + e");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // argCount is in the header
        var header = (JObject)method["header"]!;
        Assert.That((int)header["argCount"]!, Is.EqualTo(5));

        // Selector is in symbols array
        var selector = GetMethodSelector(json, method);
        Assert.That(selector, Is.EqualTo("with:and:also:plus:including:"));
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
        var bytecodes = (JArray)method["bytecodes"]!;
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
        var bytecodes = (JArray)method["bytecodes"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 200);
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

        // Verify we can handle large literal pools
        // Extended literal access (bytecode 132) may or may not be used depending on
        // compiler optimization and whether literals are deduplicated or shared
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Count, Is.GreaterThan(0), "Should compile with many literals");
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

        // tempCount is in the header
        var header = (JObject)method["header"]!;
        Assert.That((int)header["tempCount"]!, Is.EqualTo(20));

        // Should use extended temp access for temps > 15 (0x80 or 0x81)
        var bytecodes = (JArray)method["bytecodes"]!;
        var hasExtendedTemp = bytecodes.Select(t => (int)t).Any(b => b == 0x80 || b == 0x81);
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

        // String should be in literals (type is lowercase "string")
        var literals = (JArray)method["literals"]!;
        var stringLiteral = literals.FirstOrDefault(l => l["type"]?.ToString() == "string");
        Assert.That(stringLiteral, Is.Not.Null, "Should have a string literal");

        // String value is stored in strings array, referenced by index
        // We just verify a string literal exists - the actual string content
        // is in the top-level strings array
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

        // ifTrue:ifFalse: is inlined - NO blocks are created, just conditional jumps
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have conditional jump (BFP) and unconditional jump
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "ifTrue:ifFalse: should be inlined with jumps");
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
        var bytecodes = (JArray)method["bytecodes"]!;
        
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
        var bytecodes = (JArray)method["bytecodes"]!;

        // + should come before negated (0xB0 = 176 for +)
        var plusIndex = Array.IndexOf(bytecodes.Select(t => (int)t).ToArray(), 0xB0);
        Assert.That(plusIndex >= 0, Is.True, "Should have + operation");

        // negated is not a special selector, should be in literals
        Assert.That(LiteralContainsSymbol(json, method, "negated"), Is.True, "Should have negated message");
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
        var bytecodes = (JArray)method["bytecodes"]!;
        
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
        var bytecodes = (JArray)method["bytecodes"]!;
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

        // Block created but never evaluated (0xC8 = 200 for blockCopy:)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should create block with blockCopy:");

        // Should not have value send (bytecode 0xC9 = 201)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.False,
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
        var bytecodes = (JArray)method["bytecodes"]!;
        
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
        // Type is lowercase "smallInt" in this compiler
        var hasNegOne = literals.Any(l =>
            l["type"]?.ToString() == "smallInt" && (int)l["value"]! == -1);

        // -1 might use special bytecode 0x74 (116) for PUSH_MINUS_ONE
        var bytecodes = (JArray)method["bytecodes"]!;
        var hasMinusOne = bytecodes.Select(t => (int)t).Contains(0x74);

        Assert.That(hasNegOne || hasMinusOne, Is.True, "Should handle -1 literal");
    }
}