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
        
        // Block creation uses bytecode 143
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Should send value message
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Block should contain push 42 and block return (125)
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Should send value: message
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value:"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Should send value:value: message
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value:value:"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Block temporaries are handled within the block context
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
    }
    
    [Test]
    public void TestNestedBlocks()
    {
        var source = WrapInTestClass(@"
testNestedBlocks
    ^[[:x | x * 2] value: 3] value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have two block creations for nested blocks
        var bytecodes = (JArray)method["bytecode"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.EqualTo(2), "Should have two block creations");
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Block should access outer temporary variable
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value:value:"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "collect:"), Is.True);
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
        
        // Complex block with nested blocks for ifTrue:ifFalse:
        var bytecodes = (JArray)method["bytecode"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.GreaterThanOrEqualTo(3), "Should have main block and conditional blocks");
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        var literals = (JArray)method["literals"]!;
        Assert.That(literals.Any(l => l["value"]?.ToString() == "size"), Is.True);
        Assert.That(literals.Any(l => l["value"]?.ToString() == "value"), Is.True);
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
        var bytecodes = (JArray)method["bytecode"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.EqualTo(2), "Should have two block creations");
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Should have nested blocks for conditionals
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.GreaterThanOrEqualTo(3), "Should have main block and conditional blocks");
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
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(143), Is.True, "Should contain block creation");
        
        // Should access temporary variable
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store in temp");
    }
}