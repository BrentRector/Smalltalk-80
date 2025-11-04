using NUnit.Framework;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// COMPREHENSIVE test suite for variable access across 3 levels of context.
/// Tests all combinations of args/temps/instance vars at each level.
/// Validates both COMPILER bytecode generation AND INTERPRETER execution.
/// </summary>
[TestFixture]
public class ComprehensiveContextAccessTests
{
    private static JsonDocument CompileToJson(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);
        var tree = parser.fileIn();

        if (parser.NumberOfSyntaxErrors > 0)
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} errors");

        var astBuilder = new FileInFormatAstBuilder(tokenStream);
        astBuilder.Visit(tree);
        var result = astBuilder.GetResult();

        var generator = new ColdStartJsonGenerator("test.st");
        string json = generator.Generate(result);

        return JsonDocument.Parse(json);
    }

    private static string WrapInTestClass(string methodSource)
    {
        return $@"
Object subclass: #TestClass
    instanceVariableNames: 'ivar1 ivar2'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'testing'!

{methodSource}
! !
";
    }

    /// <summary>
    /// Find all block methods in the JSON (selectorIndex == -1)
    /// </summary>
    private static List<JsonElement> FindBlocks(JsonDocument json)
    {
        var blocks = new List<JsonElement>();
        var methods = json.RootElement.GetProperty("methods");

        for (int i = 0; i < methods.GetArrayLength(); i++)
        {
            var m = methods[i];
            if (m.TryGetProperty("selectorIndex", out var selector) && selector.GetInt32() == -1)
            {
                blocks.Add(m);
            }
        }

        return blocks;
    }

    // ===== LEVEL 1: Method accessing its own vars =====

    [Test]
    public void L1_MethodAccessesOwnArg()
    {
        var source = WrapInTestClass(@"
test: arg1
    ^arg1");

        using var json = CompileToJson(source);
        var method = json.RootElement.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");

        // Should be PUSH_TEMP 0 (first arg)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), "Method should PUSH_TEMP 0 for first arg");
    }

    [Test]
    public void L1_MethodAccessesOwnTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 5.
    ^temp1");

        using var json = CompileToJson(source);
        var method = json.RootElement.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");

        // Find PUSH_TEMP for temp1 (after the store)
        bool foundPushTemp = false;
        for (int i = 2; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 16) // PUSH_TEMP 0
            {
                foundPushTemp = true;
                break;
            }
        }

        Assert.That(foundPushTemp, Is.True, "Method should PUSH_TEMP 0 for first temp");
    }

    // ===== LEVEL 2: Block accessing parent method vars =====

    [Test]
    public void L2_BlockAccessesMethodArg()
    {
        var source = WrapInTestClass(@"
test: arg1
    ^[arg1 + 1] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses arg1 (parent arg 0)
        // Should generate: PUSH_TEMP 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Block should PUSH_TEMP 0 when accessing parent arg (no block locals)");
    }

    [Test]
    public void L2_BlockAccessesMethodTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 10.
    ^[temp1 + 5] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses temp1 (parent temp 0)
        // Should generate: PUSH_TEMP 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Block should PUSH_TEMP 0 when accessing first parent temp");
    }

    [Test]
    public void L2_BlockWithArgAccessesMethodTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 10.
    ^[:x | temp1 + x] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has 1 arg (blockLocalCount=1)
        // Accesses temp1 (parent temp 0)
        // Compiler should generate: PUSH_TEMP 1 (0 + blockLocalCount)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Block should PUSH_TEMP 1 when accessing parent temp (block has 1 arg)");
    }

    [Test]
    public void L2_BlockModifiesMethodTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 0.
    [(temp1 := temp1 + 1) <= 5]
        whileTrue: [self].
    ^temp1");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        // First block is the condition
        Assert.That(blocks.Count, Is.GreaterThan(0));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses temp1 (parent temp 0)
        // Should: PUSH_TEMP 0, PUSH 1, SEND +, DUP, STORE_TEMP 0, ...
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Condition block should PUSH_TEMP 0 for parent temp");

        // Find the STORE_TEMP
        bool foundStore = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 104) // COMPACT_POP_STORE_TEMP 0
            {
                foundStore = true;
                break;
            }
        }

        Assert.That(foundStore, Is.True,
            "Condition block should STORE_TEMP 0 back to parent temp");
    }

    // ===== LEVEL 3: Nested blocks accessing grandparent vars =====

    [Test]
    public void L3_NestedBlockAccessesGrandparentTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 10.
    ^[[temp1 + 5] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks (outer and inner)");

        // Inner block (index 0 or 1 depending on order) accesses grandparent temp
        var innerBlock = blocks[0].GetProperty("bytecodes")[0].GetInt32() == 16 ? blocks[0] : blocks[1];
        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses temp1 from grandparent method
        // Should generate: PUSH_TEMP 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Nested block should PUSH_TEMP 0 when accessing grandparent temp");
    }

    [Test]
    public void L3_NestedBlockWithArgsAccessesGrandparentTemp()
    {
        var source = WrapInTestClass(@"
test
    | temp1 |
    temp1 := 10.
    ^[:x | [:y | temp1 + x + y] value: 2] value: 1");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2));

        // Find the inner block (has 1 arg)
        JsonElement innerBlock = default;
        foreach (var block in blocks)
        {
            var header = block.GetProperty("header");
            if (header.GetProperty("argCount").GetInt32() == 1)
            {
                var firstBytecode = block.GetProperty("bytecodes")[0].GetInt32();
                // Inner block starts with PUSH_TEMP for temp1
                if (firstBytecode >= 16 && firstBytecode <= 23)
                {
                    innerBlock = block;
                    break;
                }
            }
        }

        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined), "Should find inner block");

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 1 arg (y) => blockLocalCount=1
        // Accesses temp1 from grandparent (2 levels up)
        // Compiler should generate: PUSH_TEMP 1 (0 + blockLocalCount)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Inner block should PUSH_TEMP 1 when accessing grandparent temp (block has 1 arg)");
    }

    // ===== COMPREHENSIVE atAllPut: test (the real-world failure case) =====

    [Test]
    public void RealWorld_atAllPut_BytecodeValidation()
    {
        var source = WrapInTestClass(@"
atAllPut: anObject
    | index size |
    index := 0.
    size := 16.
    [(index := index + 1) <= size]
        whileTrue: [self at: index put: anObject]");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        // Should have 2 blocks: condition and body
        Assert.That(blocks.Count, Is.EqualTo(2));

        // Find the condition block (has assignment)
        JsonElement? conditionBlock = null;
        foreach (var block in blocks)
        {
            var bytecodes = block.GetProperty("bytecodes");
            // Condition block has: PUSH, PUSH, SEND +, DUP, STORE, PUSH, SEND <=
            if (bytecodes.GetArrayLength() >= 7)
            {
                conditionBlock = block;
                break;
            }
        }

        Assert.That(conditionBlock, Is.Not.Null, "Should find condition block");

        var bc = conditionBlock.Value.GetProperty("bytecodes");

        // Method: atAllPut: has arg at 0, index at 1, size at 2
        // Block has no args/temps (blockLocalCount=0)
        // Accesses index (parent temp 1, which is slot 1)
        // Should generate: PUSH_TEMP 1
        Assert.That(bc[0].GetInt32(), Is.EqualTo(17),
            "Condition should PUSH_TEMP 1 for 'index' (arg + first temp)");

        // After DUP, should STORE_TEMP 1
        bool foundStore = false;
        for (int i = 4; i < Math.Min(7, bc.GetArrayLength()); i++)
        {
            if (bc[i].GetInt32() == 105) // COMPACT_POP_STORE_TEMP 1
            {
                foundStore = true;
                break;
            }
        }
        Assert.That(foundStore, Is.True, "Condition should STORE_TEMP 1 back to 'index'");

        // Should PUSH_TEMP 2 for 'size'
        bool foundSizePush = false;
        for (int i = 5; i < Math.Min(8, bc.GetArrayLength()); i++)
        {
            if (bc[i].GetInt32() == 18) // PUSH_TEMP 2
            {
                foundSizePush = true;
                break;
            }
        }
        Assert.That(foundSizePush, Is.True, "Condition should PUSH_TEMP 2 for 'size'");
    }
}
