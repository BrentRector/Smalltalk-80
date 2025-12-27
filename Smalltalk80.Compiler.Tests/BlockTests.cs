using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for block literals including parameters, returns, nested blocks, and block temporaries.
/// </summary>
[TestFixture]
public class BlockTests : CompilerTestBase
{
    [Test]
    public void TestEmptyBlock()
    {
        var source = WrapInTestClass(@"
testEmptyBlock
    ^[] value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Blue Book inline blocks use 0xC8 (200) SEND_SPECIAL_BLOCKCOPY
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value message (0xC9 = 201 SEND_SPECIAL_VALUE)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.True, "Should contain value send");
    }

    [Test]
    public void TestBlockWithReturn()
    {
        var source = WrapInTestClass(@"
testBlockReturn
    ^[42] value");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block creation, block returns 42
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value message (0xC9 = 201 SEND_SPECIAL_VALUE)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.True, "Should contain value send");
    }

    [Test]
    public void TestBlockWithParameter()
    {
        var source = WrapInTestClass(@"
testBlockParameter
    ^[:x | x * 2] value: 5");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block with one parameter
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value: message (0xCA = 202 SEND_SPECIAL_VALUE1)
        Assert.That(bytecodes.Select(t => (int)t).Contains(202), Is.True, "Should contain value: send");
    }

    [Test]
    public void TestBlockWithMultipleParameters()
    {
        var source = WrapInTestClass(@"
testMultipleBlockParams
    ^[:x :y | x + y] value: 3 value: 4");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block with two parameters
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value:value: message (not a special selector, so in literals)
        Assert.That(LiteralContainsSymbol(json, method, "value:value:"), Is.True, "Should have value:value: in literals");
    }
    
    [Test]
    public void TestBlockWithTemporaries()
    {
        var source = WrapInTestClass(@"
testBlockTemps
    ^[| temp | temp := 10. temp * 2] value");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block with temporary variable
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value message (0xC9 = 201 SEND_SPECIAL_VALUE)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.True, "Should contain value send");
    }
    
    [Test]
    public void TestBlockWithParametersAndTemps()
    {
        var source = WrapInTestClass(@"
testBlockParamsAndTemps
    ^[:x | | temp | temp := x * 2. temp + 1] value: 5");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Block with both parameter and temporary
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");
    }
    
    [Test]
    public void TestNestedBlocks()
    {
        var source = WrapInTestClass(@"
testNestedBlocks
    ^[[:x | x * 2] value: 3] value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have two blockCopy: sends for nested blocks
        var bytecodes = (JArray)method["bytecodes"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 200);
        Assert.That(blockCount, Is.EqualTo(2), "Should have two blockCopy: sends");
    }
    
    [Test]
    public void TestBlockContext()
    {
        var source = WrapInTestClass(@"
testBlockContext
    | outer |
    outer := 10.
    ^[outer + 5] value");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block captures outer variable
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // Should send value message (0xC9 = 201 SEND_SPECIAL_VALUE)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.True, "Should contain value send");
    }
    
    [Test]
    public void TestBlockWithNonLocalReturn()
    {
        var source = WrapInTestClass(@"
testNonLocalReturn
    [^42] value.
    ^0");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Non-local return uses bytecode 124 (return top of stack)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");
        
        // Should have both method return and potential block return
        var returnCount = bytecodes.Select(t => (int)t).Count(b => b == 124 || b == 125);
        Assert.That(returnCount, Is.GreaterThan(0), "Should have return bytecodes");
    }
    
    [Test]
    public void TestBlockValueWithoutArgs()
    {
        var source = WrapInTestClass(@"
testBlockValue
    | block |
    block := [42].
    ^block value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Store block in temp, send value
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store block in temp");
    }
    
    [Test]
    public void TestBlockValueWithArgs()
    {
        var source = WrapInTestClass(@"
testBlockValueWith
    | block |
    block := [:x :y | x + y].
    ^block value: 3 value: 4");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Store block, push args, send value:value:
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // value:value: is not a special selector, so it should be in literals
        Assert.That(LiteralContainsSymbol(json, method, "value:value:"), Is.True, "Should have value:value: in literals");
    }
    
    [Test]
    public void TestBlockInCollection()
    {
        var source = WrapInTestClass(@"
testBlockInCollection
    ^#(1 2 3) collect: [:each | each * 2]");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block passed to collect:
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // collect: is not a special selector, so it should be in literals
        Assert.That(LiteralContainsSymbol(json, method, "collect:"), Is.True, "Should have collect: in literals");
    }
    
    [Test]
    public void TestBlockWithComplexExpression()
    {
        var source = WrapInTestClass(@"
testComplexBlock
    ^[:x |
        | temp |
        temp := x * 2.
        temp > 10
            ifTrue: [temp]
            ifFalse: [10]] value: 7");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Complex block with ifTrue:ifFalse: (ifTrue:ifFalse: is inlined, not separate blocks)
        var bytecodes = (JArray)method["bytecodes"]!;
        // Main block uses blockCopy: (0xC8 = 200)
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should have main block");
        // ifTrue:ifFalse: is inlined, so we should have conditional jumps
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "Should have conditional jumps for ifTrue:ifFalse:");
    }
    
    [Test]
    public void TestBlockWithMethodCall()
    {
        var source = WrapInTestClass(@"
testBlockWithSend
    ^[self size] value");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Block contains self send
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // size uses special bytecode 0xC2 (194)
        Assert.That(bytecodes.Select(t => (int)t).Contains(194), Is.True, "Should contain size send");
        // value uses special bytecode 0xC9 (201)
        Assert.That(bytecodes.Select(t => (int)t).Contains(201), Is.True, "Should contain value send");
    }
    
    [Test]
    public void TestBlockAssignment()
    {
        var source = WrapInTestClass(@"
testBlockAssignment
    | block1 block2 |
    block1 := [1].
    block2 := [2].
    ^(block1 value) + (block2 value)");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Multiple block assignments
        var bytecodes = (JArray)method["bytecodes"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 200);
        Assert.That(blockCount, Is.EqualTo(2), "Should have two blockCopy: sends");
    }
    
    [Test]
    public void TestBlockRecursion()
    {
        var source = WrapInTestClass(@"
testBlockRecursion
    | factorial |
    factorial := [:n |
        n <= 1
            ifTrue: [1]
            ifFalse: [n * (factorial value: n - 1)]].
    ^factorial value: 5");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Recursive block with self-reference
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");

        // ifTrue:ifFalse: is inlined, so we should have conditional jumps, not separate blocks
        var hasConditionalJump = bytecodes.Select(t => (int)t).Any(b =>
            (b >= 0x98 && b <= 0x9F) || (b >= 0xAC && b <= 0xAF));
        Assert.That(hasConditionalJump, Is.True, "Should have conditional jumps for ifTrue:ifFalse:");
    }
    
    [Test]
    public void TestBlockCopyingValue()
    {
        var source = WrapInTestClass(@"
testBlockCopyingValue
    | value block |
    value := 5.
    block := [value].
    value := 10.
    ^block value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Block captures variable reference, not value
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(200), Is.True, "Should contain blockCopy: send");
        
        // Should access temporary variable
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store in temp");
    }
}