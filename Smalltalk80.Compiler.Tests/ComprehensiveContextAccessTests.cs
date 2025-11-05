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

    // ===== ARGUMENT ACCESS TESTS =====

    [Test]
    public void L2_BlockAccessesMethodArg_AfterArg()
    {
        var source = WrapInTestClass(@"
test: arg1 with: arg2
    ^[arg2 + 1] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses arg2 (parent arg 1)
        // Should generate: PUSH_TEMP 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Block should PUSH_TEMP 1 when accessing second parent arg");
    }

    [Test]
    public void L2_BlockWithArgAccessesMethodArg()
    {
        var source = WrapInTestClass(@"
test: arg1
    ^[:x | arg1 + x] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has 1 arg (blockLocalCount=1)
        // Accesses arg1 (parent arg 0)
        // Should generate: PUSH_TEMP 1 (0 + blockLocalCount)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Block should PUSH_TEMP 1 when accessing parent arg (block has 1 arg)");
    }

    [Test]
    public void L2_BlockAccessesOwnArg()
    {
        var source = WrapInTestClass(@"
test
    ^[:x | x + 1] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has 1 arg (x at index 0)
        // Should generate: PUSH_TEMP 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Block should PUSH_TEMP 0 when accessing own arg");
    }

    [Test]
    public void L2_BlockAccessesOwnTemp()
    {
        var source = WrapInTestClass(@"
test
    ^[| temp | temp := 10. temp + 1] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has 1 temp (temp at index 0)
        // After assignment, should PUSH_TEMP 0
        bool foundPushTemp = false;
        for (int i = 2; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 16)
            {
                foundPushTemp = true;
                break;
            }
        }

        Assert.That(foundPushTemp, Is.True,
            "Block should PUSH_TEMP 0 when accessing own temp");
    }

    [Test]
    public void L2_BlockWithArgAndTempAccessesBoth()
    {
        var source = WrapInTestClass(@"
test
    ^[:x | | temp | temp := x. temp + x] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block has 1 arg (x at 0), 1 temp (temp at 1)
        // First bytecode should PUSH_TEMP 0 (accessing x for assignment)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Block should PUSH_TEMP 0 for own arg");

        // After assignment, should access temp (index 1) and arg (index 0)
        bool foundTempPush = false;
        bool foundArgPush = false;
        for (int i = 2; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 17) foundTempPush = true;
            if (bytecodes[i].GetInt32() == 16) foundArgPush = true;
        }

        Assert.That(foundTempPush, Is.True, "Should PUSH_TEMP 1 for own temp");
        Assert.That(foundArgPush, Is.True, "Should PUSH_TEMP 0 for own arg");
    }

    // ===== INSTANCE VARIABLE ACCESS TESTS =====

    [Test]
    public void L1_MethodAccessesInstanceVar()
    {
        var source = WrapInTestClass(@"
test
    ^ivar1");

        using var json = CompileToJson(source);
        var method = json.RootElement.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");

        // Should be PUSH_IVAR 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(0),
            "Method should PUSH_IVAR 0 for first instance var");
    }

    [Test]
    public void L2_BlockAccessesMethodInstanceVar()
    {
        var source = WrapInTestClass(@"
test
    ^[ivar1 + 5] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block should access instance var from receiver (self)
        // Should generate: PUSH_IVAR 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(0),
            "Block should PUSH_IVAR 0 when accessing instance var");
    }

    [Test]
    public void L2_BlockModifiesMethodInstanceVar()
    {
        var source = WrapInTestClass(@"
test
    ^[ivar1 := 10. ivar1] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Should find STORE_IVAR 0 and PUSH_IVAR 0
        bool foundStore = false;
        bool foundPush = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 96) foundStore = true; // STORE_IVAR 0
            if (bc == 0) foundPush = true;   // PUSH_IVAR 0
        }

        Assert.That(foundStore, Is.True, "Block should STORE_IVAR 0");
        Assert.That(foundPush, Is.True, "Block should PUSH_IVAR 0");
    }

    // ===== MIXED ACCESS TESTS =====

    [Test]
    public void L2_BlockAccessesMixedVariables()
    {
        var source = WrapInTestClass(@"
test: arg1
    | temp1 |
    temp1 := 10.
    ^[:x | ivar1 + arg1 + temp1 + x] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1));
        var bytecodes = blocks[0].GetProperty("bytecodes");

        // Block accesses:
        // - ivar1 (PUSH_IVAR 0)
        // - arg1 (parent arg 0, block has 1 arg, so PUSH_TEMP 1)
        // - temp1 (parent temp 0, block has 1 arg, so PUSH_TEMP 2)
        // - x (own arg 0, PUSH_TEMP 0)

        bool foundIvar = false;
        bool foundParentArg = false;
        bool foundParentTemp = false;
        bool foundOwnArg = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 0) foundIvar = true;   // PUSH_IVAR 0
            if (bc == 17) foundParentArg = true; // PUSH_TEMP 1 (parent arg)
            if (bc == 18) foundParentTemp = true; // PUSH_TEMP 2 (parent temp)
            if (bc == 16) foundOwnArg = true; // PUSH_TEMP 0 (own arg)
        }

        Assert.That(foundIvar, Is.True, "Should access instance var");
        Assert.That(foundParentArg, Is.True, "Should access parent arg");
        Assert.That(foundParentTemp, Is.True, "Should access parent temp");
        Assert.That(foundOwnArg, Is.True, "Should access own arg");
    }

    [Test]
    public void L3_NestedBlockAccessesMixedVariables()
    {
        var source = WrapInTestClass(@"
test: arg1
    | temp1 |
    temp1 := 10.
    ^[:x | [:y | ivar1 + arg1 + temp1 + x + y] value: 2] value: 1");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2));

        // Find the inner block (has arg y, accesses all vars)
        JsonElement? innerBlock = null;
        foreach (var block in blocks)
        {
            var bc = block.GetProperty("bytecodes");
            // Inner block should have more variable accesses
            if (bc.GetArrayLength() > 5)
            {
                innerBlock = block;
                break;
            }
        }

        Assert.That(innerBlock, Is.Not.Null, "Should find inner block");

        var bytecodes = innerBlock.Value.GetProperty("bytecodes");

        // Inner block (has 1 arg: y) accesses:
        // - ivar1 (PUSH_IVAR 0)
        // - arg1 (grandparent arg, PUSH_TEMP 1)
        // - temp1 (grandparent temp, PUSH_TEMP 2)
        // - x (parent arg, PUSH_TEMP 1... wait, same as arg1!)
        // - y (own arg, PUSH_TEMP 0)

        bool foundIvar = false;
        bool foundGrandparentVar = false;
        bool foundOwnArg = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 0) foundIvar = true;
            if (bc >= 17 && bc <= 19) foundGrandparentVar = true;
            if (bc == 16) foundOwnArg = true;
        }

        Assert.That(foundIvar, Is.True, "Inner block should access instance var");
        Assert.That(foundGrandparentVar, Is.True, "Inner block should access grandparent vars");
        Assert.That(foundOwnArg, Is.True, "Inner block should access own arg");
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
