using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for message sending including unary, binary, keyword messages, precedence, and cascades.
/// </summary>
[TestFixture]
public class MessageTests : CompilerTestBase
{
    [Test]
    public void TestUnaryMessage()
    {
        var source = WrapInTestClass(@"
testUnaryMessage
    ^3 negated");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push 3, send negated, return top
        // Bytecodes: 118 (push 1), send literal selector 0 args
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should have unary send - negated is not a special selector, so it's in literals
        Assert.That(LiteralContainsSymbol(json, method, "negated"), Is.True);
    }
    
    [Test]
    public void TestBinaryMessage()
    {
        var source = WrapInTestClass(@"
testBinaryMessage
    ^3 + 4");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push 3, push 4, send +, return top
        // Special arithmetic sends use bytecodes 176-191
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // + is a special selector, should use bytecode 176
        Assert.That(bytecodes.Select(t => (int)t).Contains(176), Is.True, "Should use special + send");
    }
    
    [Test]
    public void TestKeywordMessage()
    {
        var source = WrapInTestClass(@"
testKeywordMessage
    ^Array new: 10");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Push Array, push 10, send new:, return top
        // new: is a SPECIAL selector with bytecode 205 (0xCD)
        // It does NOT appear in literals - it uses dedicated bytecode
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(205), Is.True,
            "Should use special new: bytecode (205)");
    }
    
    [Test]
    public void TestMultipleKeywordMessage()
    {
        var source = WrapInTestClass(@"
testMultipleKeywords
    ^self at: 1 put: 'value'");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push self, push 1, push 'value', send at:put:, return top
        // at:put: IS a special selector - uses bytecode 193 (0xC1)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(193), Is.True, "Should use special at:put: bytecode");
    }
    
    [Test]
    public void TestMessagePrecedenceUnaryFirst()
    {
        var source = WrapInTestClass(@"
testUnaryPrecedence
    ^3 negated + 4");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should evaluate as (3 negated) + 4
        // Push 3, send negated, push 4, send +, return
        var literals = (JArray)method["literals"]!;
        
        // Check that negated is in literals (+ uses special bytecode 176)
        Assert.That(LiteralContainsSymbol(json, method, "negated"), Is.True, "Should have negated in literals");

        // In bytecode, negated should be sent before +
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        // + is special selector 0, bytecode 176
        Assert.That(bytecodes.Select(t => (int)t).Contains(176), Is.True, "Should use special + bytecode");
    }
    
    [Test]
    public void TestMessagePrecedenceBinaryBeforeKeyword()
    {
        var source = WrapInTestClass(@"
testBinaryPrecedence
    ^Array new: 2 + 3");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Should evaluate as Array new: (2 + 3)
        // Binary + has higher precedence than keyword new:
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);

        // + is special selector bytecode 176
        Assert.That(bytecodes.Select(t => (int)t).Contains(176), Is.True,
            "Should use special + bytecode (176)");

        // new: is a SPECIAL selector with bytecode 205 (0xCD)
        Assert.That(bytecodes.Select(t => (int)t).Contains(205), Is.True,
            "Should use special new: bytecode (205)");
    }
    
    [Test]
    public void TestCascadeSimple()
    {
        var source = WrapInTestClass(@"
testCascade
    ^Array new: 3;
        at: 1 put: 'a';
        at: 2 put: 'b';
        yourself");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Cascades use bytecode 136 (duplicate stack top)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(136), Is.True, "Should contain dup for cascade");
    }
    
    [Test]
    public void TestCascadeWithPop()
    {
        var source = WrapInTestClass(@"
testCascadeWithPop
    | array |
    array := Array new: 2.
    array at: 1 put: 'first';
          at: 2 put: 'second'.
    ^array");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Cascades with semicolon need dup and pop
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(136), Is.True, "Should contain dup for cascade");
        Assert.That(bytecodes.Select(t => (int)t).Contains(135), Is.True, "Should contain pop for cascade");
    }
    
    [Test]
    public void TestSpecialSelectorPlus()
    {
        var source = WrapInTestClass(@"
testPlus
    ^5 + 7");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // + is special selector 0, bytecode 176
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(176), Is.True, "Should use special + bytecode");
    }
    
    [Test]
    public void TestSpecialSelectorMinus()
    {
        var source = WrapInTestClass(@"
testMinus
    ^10 - 3");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // - is special selector 1, bytecode 177
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(177), Is.True, "Should use special - bytecode");
    }
    
    [Test]
    public void TestSpecialSelectorLessThan()
    {
        var source = WrapInTestClass(@"
testLessThan
    ^3 < 5");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // < is special selector 2, bytecode 178
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(178), Is.True, "Should use special < bytecode");
    }
    
    [Test]
    public void TestSpecialSelectorEquals()
    {
        var source = WrapInTestClass(@"
testEquals
    ^5 = 5");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // = is special selector 6, bytecode 182
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(182), Is.True, "Should use special = bytecode");
    }
    
    [Test]
    public void TestSpecialSelectorAt()
    {
        var source = WrapInTestClass(@"
testAt
    ^#(1 2 3) at: 2");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // at: IS a special selector - uses bytecode 192 (0xC0)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(192), Is.True, "Should use special at: bytecode");
    }
    
    [Test]
    public void TestSpecialSelectorSize()
    {
        var source = WrapInTestClass(@"
testSize
    ^#(1 2 3) size");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // size IS a special selector - uses bytecode 194 (0xC2), NOT in literals
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(194), Is.True, "Should use special size bytecode");
    }
    
    [Test]
    public void TestComplexMessageChain()
    {
        var source = WrapInTestClass(@"
testComplexChain
    ^((3 + 4) * 2) negated abs");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should evaluate left to right with proper precedence
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain +, *, negated, abs - + and * use special bytecodes
        Assert.That(LiteralContainsSymbol(json, method, "negated"), Is.True);
        Assert.That(LiteralContainsSymbol(json, method, "abs"), Is.True);
    }
    
    [Test]
    public void TestSelfSend()
    {
        var source = WrapInTestClass(@"
testSelfSend
    ^self size");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push self (112), send size, return
        var bytecodes = (JArray)method["bytecodes"]!;
        AssertBytecodeAt(bytecodes, 0, 112, "Push self");
    }
    
    [Test]
    public void TestSuperSend()
    {
        var source = WrapInTestClass(@"
testSuperSend
    ^super new");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push self (for super), super send new, return
        // Super sends use special bytecodes 133-134
        var bytecodes = (JArray)method["bytecodes"]!;
        AssertBytecodeAt(bytecodes, 0, 112, "Push self for super");
        
        // Should have super send bytecode
        var hasSuperSend = bytecodes.Select(t => (int)t).Any(b => b == 133 || b == 134);
        Assert.That(hasSuperSend, Is.True, "Should contain super send");
    }
    
    [Test]
    public void TestLongMessageChain()
    {
        var source = WrapInTestClass(@"
testLongChain
    ^1 + 2 * 3 - 4 + 5");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Binary messages evaluate left to right
        // Should be ((((1 + 2) * 3) - 4) + 5)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should have multiple binary sends
        var binarySends = bytecodes.Select(t => (int)t).Count(b => b >= 176 && b <= 191);
        Assert.That(binarySends, Is.GreaterThanOrEqualTo(4), "Should have multiple binary sends");
    }
}