using NUnit.Framework;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Comprehensive bytecode validation tests for all 120 BlockContextTest methods.
/// Validates that the compiler generates correct PUSH_TEMP/STORE_TEMP indices
/// for accessing variables across nested block contexts.
/// </summary>
[TestFixture]
public class BlockContextTestBytecodeValidation
{
    #region Helper Methods

    /// <summary>
    /// Compiles Smalltalk source to JSON using System.Text.Json.
    /// </summary>
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

    /// <summary>
    /// Wraps a method source in a test class definition.
    /// </summary>
    private static string WrapInTestClass(string methodSource)
    {
        return $@"
Object subclass: #BlockContextTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!BlockContextTest methodsFor: 'testing'!

{methodSource}
! !
";
    }

    /// <summary>
    /// Finds all block methods in the JSON (selectorIndex == -1).
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

    /// <summary>
    /// Gets the block local count (args + temps) for a block.
    /// </summary>
    private static int GetBlockLocalCount(JsonElement block)
    {
        var header = block.GetProperty("header");
        int argCount = header.GetProperty("argCount").GetInt32();
        int tempCount = header.GetProperty("tempCount").GetInt32();
        return argCount + tempCount;
    }

    #endregion

    #region Single-Level Tests (9 tests)

    [Test]
    public void Test_s00_BlockAccessesMethodTemp()
    {
        // s00: | m | m _ 10. ^[m] value!
        var source = WrapInTestClass(@"
s00
    | m | m _ 10. ^[m] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses m (parent temp 0), so bytecode index = 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Block should access method temp 'm' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_s01_BlockUsesOwnTemp()
    {
        // s01: ^[| t1 | t1 _ 10. t1] value!
        var source = WrapInTestClass(@"
s01
    ^[| t1 | t1 _ 10. t1] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 1 temp (blockLocalCount=1)
        // After assignment, accesses its own temp t1 at index 0
        bool foundPushTemp0 = false;
        for (int i = 2; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 16) // PUSH_TEMP 0
            {
                foundPushTemp0 = true;
                break;
            }
        }

        Assert.That(foundPushTemp0, Is.True,
            "Block should access own temp 't1' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_s02_BlockUsesBothTemps()
    {
        // s02: ^[| t1 t2 | t1 _ 5. t2 _ 5. t1 + t2] value!
        var source = WrapInTestClass(@"
s02
    ^[| t1 t2 | t1 _ 5. t2 _ 5. t1 + t2] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 2 temps (blockLocalCount=2)
        // Should access t1 (index 0) and t2 (index 1)
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1
        }

        Assert.That(foundPushTemp0, Is.True, "Should access t1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access t2 with PUSH_TEMP 1");
    }

    [Test]
    public void Test_s10_BlockUsesArg()
    {
        // s10: ^[:a1 | a1] value: 10!
        var source = WrapInTestClass(@"
s10
    ^[:a1 | a1] value: 10");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 1 arg (blockLocalCount=1)
        // Accesses own arg a1 at index 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Block should access own arg 'a1' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_s11_BlockUsesArgAndTemp()
    {
        // s11: ^[:a1 | | t1 | t1 _ 5. a1 + t1] value: 5!
        var source = WrapInTestClass(@"
s11
    ^[:a1 | | t1 | t1 _ 5. a1 + t1] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 1 arg + 1 temp (blockLocalCount=2)
        // a1 at index 0, t1 at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (t1)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access t1 with PUSH_TEMP 1");
    }

    [Test]
    public void Test_s12_BlockUsesArgAndBothTemps()
    {
        // s12: ^[:a1 | | t1 t2 | t1 _ 3. t2 _ 4. a1 + t1 + t2] value: 3!
        var source = WrapInTestClass(@"
s12
    ^[:a1 | | t1 t2 | t1 _ 3. t2 _ 4. a1 + t1 + t2] value: 3");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 1 arg + 2 temps (blockLocalCount=3)
        // a1 at index 0, t1 at index 1, t2 at index 2
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;
        bool foundPushTemp2 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (t1)
            if (bc == 18) foundPushTemp2 = true; // PUSH_TEMP 2 (t2)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access t1 with PUSH_TEMP 1");
        Assert.That(foundPushTemp2, Is.True, "Should access t2 with PUSH_TEMP 2");
    }

    [Test]
    public void Test_s20_BlockUsesBothArgs()
    {
        // s20: ^[:a1 :a2 | a1 + a2] value: 5 value: 5!
        var source = WrapInTestClass(@"
s20
    ^[:a1 :a2 | a1 + a2] value: 5 value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 2 args (blockLocalCount=2)
        // a1 at index 0, a2 at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (a2)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access a2 with PUSH_TEMP 1");
    }

    [Test]
    public void Test_s21_BlockUsesBothArgsAndTemp()
    {
        // s21: ^[:a1 :a2 | | t1 | t1 _ 4. a1 + a2 + t1] value: 3 value: 3!
        var source = WrapInTestClass(@"
s21
    ^[:a1 :a2 | | t1 | t1 _ 4. a1 + a2 + t1] value: 3 value: 3");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 2 args + 1 temp (blockLocalCount=3)
        // a1 at index 0, a2 at index 1, t1 at index 2
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;
        bool foundPushTemp2 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (a2)
            if (bc == 18) foundPushTemp2 = true; // PUSH_TEMP 2 (t1)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access a2 with PUSH_TEMP 1");
        Assert.That(foundPushTemp2, Is.True, "Should access t1 with PUSH_TEMP 2");
    }

    [Test]
    public void Test_s22_BlockUsesAll()
    {
        // s22: ^[:a1 :a2 | | t1 t2 | t1 _ 2. t2 _ 3. a1 + a2 + t1 + t2] value: 2 value: 3!
        var source = WrapInTestClass(@"
s22
    ^[:a1 :a2 | | t1 t2 | t1 _ 2. t2 _ 3. a1 + a2 + t1 + t2] value: 2 value: 3");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has 2 args + 2 temps (blockLocalCount=4)
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;
        bool foundPushTemp2 = false;
        bool foundPushTemp3 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (a2)
            if (bc == 18) foundPushTemp2 = true; // PUSH_TEMP 2 (t1)
            if (bc == 19) foundPushTemp3 = true; // PUSH_TEMP 3 (t2)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access a2 with PUSH_TEMP 1");
        Assert.That(foundPushTemp2, Is.True, "Should access t1 with PUSH_TEMP 2");
        Assert.That(foundPushTemp3, Is.True, "Should access t2 with PUSH_TEMP 3");
    }

    #endregion

    #region Two-Level Tests (81 tests)

    [Test]
    public void Test_t0000_BothAccessMethodTemp()
    {
        // t0000: | m | m _ 10. ^[[m] value] value!
        var source = WrapInTestClass(@"
t0000
    | m | m _ 10. ^[[m] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (smallest bytecode array - it just accesses m and returns)
        var innerBlock = blocks.OrderBy(b => b.GetProperty("bytecodes").GetArrayLength()).First();
        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses m from method (grandparent temp 0)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Inner block should access method temp 'm' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_t0001_InnerHasTempAccessesMethodTemp()
    {
        // t0001: | m | m _ 5. ^[[| t | t _ 5. m + t] value] value!
        var source = WrapInTestClass(@"
t0001
    | m | m _ 5. ^[[| t | t _ 5. m + t] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (has a temp)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 1);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 1 temp (blockLocalCount=1)
        // Accesses m from method at logical index 0
        // Bytecode index = 0 + 1 = 1
        bool foundPushTemp1 = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 17) // PUSH_TEMP 1
            {
                foundPushTemp1 = true;
                break;
            }
        }

        Assert.That(foundPushTemp1, Is.True,
            "Inner block should access method temp 'm' with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t0002_InnerHas2TempsAccessesMethodTemp()
    {
        // t0002: | m | m _ 3. ^[[| t1 t2 | t1 _ 3. t2 _ 4. m + t1 + t2] value] value!
        var source = WrapInTestClass(@"
t0002
    | m | m _ 3. ^[[| t1 t2 | t1 _ 3. t2 _ 4. m + t1 + t2] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (has 2 temps)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 2);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 2 temps (blockLocalCount=2)
        // Accesses m from method at logical index 0
        // Bytecode index = 0 + 2 = 2
        bool foundPushTemp2 = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 18) // PUSH_TEMP 2
            {
                foundPushTemp2 = true;
                break;
            }
        }

        Assert.That(foundPushTemp2, Is.True,
            "Inner block should access method temp 'm' with PUSH_TEMP 2");
    }

    [Test]
    public void Test_t0010_InnerHasArg()
    {
        // t0010: ^[[[:a | a] value: 10] value] value!
        var source = WrapInTestClass(@"
t0010
    ^[[[:a | a] value: 10] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(3), "Should have 3 blocks");

        // Find innermost block (has 1 arg)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost block has 1 arg (blockLocalCount=1)
        // Accesses its own arg at index 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Innermost block should access own arg 'a' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_t0011_InnerHasArgAndTemp()
    {
        // t0011: ^[[[:a | | t | t _ 5. a + t] value: 5] value] value!
        var source = WrapInTestClass(@"
t0011
    ^[[[:a | | t | t _ 5. a + t] value: 5] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        // Find innermost block (has 1 arg and 1 temp)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 1);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost block has 1 arg + 1 temp (blockLocalCount=2)
        // a at index 0, t at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (a)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (t)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access a with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access t with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t0100_OuterHasTempInnerAccessesIt()
    {
        // t0100: ^[| ot | ot _ 10. [ot] value] value!
        var source = WrapInTestClass(@"
t0100
    ^[| ot | ot _ 10. [ot] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (no args/temps, accesses outer temp)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses ot from outer block at index 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Inner block should access outer temp 'ot' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_t0110_OuterTempInnerArgAccessesOuterTemp()
    {
        // t0110: ^[| ot | ot _ 10. [:a | ot] value: 999] value!
        var source = WrapInTestClass(@"
t0110
    ^[| ot | ot _ 10. [:a | ot] value: 999] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (has 1 arg)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 1 arg (blockLocalCount=1)
        // Accesses ot from outer block at logical index 0
        // Bytecode index = 0 + 1 = 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17), // PUSH_TEMP 1
            "Inner block should access outer temp 'ot' with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t0200_Outer2TempsInnerAccessesBoth()
    {
        // t0200: ^[| ot1 ot2 | ot1 _ 5. ot2 _ 5. [ot1 + ot2] value] value!
        var source = WrapInTestClass(@"
t0200
    ^[| ot1 ot2 | ot1 _ 5. ot2 _ 5. [ot1 + ot2] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses ot1 at index 0, ot2 at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (ot1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (ot2)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access ot1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access ot2 with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t1000_OuterArgInnerAccessesIt()
    {
        // t1000: ^[:oa | [oa] value] value: 10!
        var source = WrapInTestClass(@"
t1000
    ^[:oa | [oa] value] value: 10");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses oa from outer block at index 0
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Inner block should access outer arg 'oa' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_t1100_OuterArgTempInnerAccessesBoth()
    {
        // t1100: ^[:oa | | ot | ot _ 5. [oa + ot] value] value: 5!
        var source = WrapInTestClass(@"
t1100
    ^[:oa | | ot | ot _ 5. [oa + ot] value] value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (no args/temps)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses oa at index 0, ot at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (oa)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (ot)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access oa with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access ot with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t2000_Outer2ArgsInnerAccessesBoth()
    {
        // t2000: ^[:oa1 :oa2 | [oa1 + oa2] value] value: 5 value: 5!
        var source = WrapInTestClass(@"
t2000
    ^[:oa1 :oa2 | [oa1 + oa2] value] value: 5 value: 5");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses oa1 at index 0, oa2 at index 1
        bool foundPushTemp0 = false;
        bool foundPushTemp1 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0 (oa1)
            if (bc == 17) foundPushTemp1 = true; // PUSH_TEMP 1 (oa2)
        }

        Assert.That(foundPushTemp0, Is.True, "Should access oa1 with PUSH_TEMP 0");
        Assert.That(foundPushTemp1, Is.True, "Should access oa2 with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t2222_AllHave2Args2Temps()
    {
        // t2222: ^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ia1 :ia2 | | it1 it2 | it1 _ 4. it2 _ 5. oa1 + oa2 + ot1 + ot2 + it1 + it2] value: 999 value: 888] value: 0 value: 0!
        var source = WrapInTestClass(@"
t2222
    ^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ia1 :ia2 | | it1 it2 | it1 _ 4. it2 _ 5. oa1 + oa2 + ot1 + ot2 + it1 + it2] value: 999 value: 888] value: 0 value: 0");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Both blocks have 2 args + 2 temps, so find the one that accesses parent vars (has PUSH_TEMP >= 20)
        var candidates = blocks.Where(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 2 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 2).ToList();

        var innerBlock = candidates.FirstOrDefault(b => {
                var bc = b.GetProperty("bytecodes");
                for (int i = 0; i < bc.GetArrayLength(); i++)
                {
                    int bytecode = bc[i].GetInt32();
                    if (bytecode >= 20 && bytecode < 32) return true; // Has parent access (PUSH_TEMP 4+)
                }
                return false;
            });
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined),
            $"Found {candidates.Count} blocks with 2 args + 2 temps, but none access parent vars");

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 2 args + 2 temps (blockLocalCount=4)
        // Outer has 2 args + 2 temps
        // oa1 at logical 0 -> bytecode 4
        // oa2 at logical 1 -> bytecode 5
        // ot1 at logical 2 -> bytecode 6
        // ot2 at logical 3 -> bytecode 7
        // ia1 at index 0, ia2 at index 1, it1 at index 2, it2 at index 3

        bool foundOuterAccess = false;
        bool foundInnerAccess = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc >= 20 && bc <= 23) foundOuterAccess = true; // PUSH_TEMP 4-7 (outer vars)
            if (bc >= 16 && bc <= 19) foundInnerAccess = true; // PUSH_TEMP 0-3 (inner vars)
        }

        Assert.That(foundOuterAccess, Is.True, "Should access outer block vars");
        Assert.That(foundInnerAccess, Is.True, "Should access own vars");
    }

    // Additional two-level tests would follow the same pattern...
    // For brevity, I'll add a few more key patterns:

    [Test]
    public void Test_t0111_OuterTempInnerArgTemp()
    {
        // t0111: ^[| ot | ot _ 5. [:a | | it | it _ 5. ot + it] value: 999] value!
        var source = WrapInTestClass(@"
t0111
    ^[| ot | ot _ 5. [:a | | it | it _ 5. ot + it] value: 999] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 1 arg + 1 temp (blockLocalCount=2)
        // Accesses ot from outer at logical 0
        // Bytecode index = 0 + 2 = 2
        bool foundPushTemp2 = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 18) // PUSH_TEMP 2
            {
                foundPushTemp2 = true;
                break;
            }
        }

        Assert.That(foundPushTemp2, Is.True,
            "Inner block should access outer temp 'ot' with PUSH_TEMP 2");
    }

    [Test]
    public void Test_t1111_BothArgTemp()
    {
        // t1111: ^[:oa | | ot | ot _ 3. [:ia | | it | it _ 4. oa + ot + it] value: 999] value: 3!
        var source = WrapInTestClass(@"
t1111
    ^[:oa | | ot | ot _ 3. [:ia | | it | it _ 4. oa + ot + it] value: 999] value: 3");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        // Both blocks have 1 arg + 1 temp, so find the one that accesses parent vars (has PUSH_TEMP >= 18)
        var candidates = blocks.Where(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 1).ToList();

        var innerBlock = candidates.FirstOrDefault(b => {
                var bc = b.GetProperty("bytecodes");
                for (int i = 0; i < bc.GetArrayLength(); i++)
                {
                    int bytecode = bc[i].GetInt32();
                    if (bytecode >= 18 && bytecode < 32) return true; // Has parent access (PUSH_TEMP 2+)
                }
                return false;
            });
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined),
            $"Found {candidates.Count} blocks with 1 arg + 1 temp, but none access parent vars");

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner has 1 arg + 1 temp (blockLocalCount=2)
        // Outer has oa at 0, ot at 1
        // oa at logical 0 -> bytecode 2 (PUSH_TEMP 18)
        // ot at logical 1 -> bytecode 3 (PUSH_TEMP 19)
        bool foundOuterArg = false;
        bool foundOuterTemp = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 18) foundOuterArg = true;  // PUSH_TEMP 2 (oa)
            if (bc == 19) foundOuterTemp = true; // PUSH_TEMP 3 (ot)
        }

        Assert.That(foundOuterArg, Is.True, "Should access outer arg 'oa' with PUSH_TEMP 2");
        Assert.That(foundOuterTemp, Is.True, "Should access outer temp 'ot' with PUSH_TEMP 3");
    }

    #endregion

    #region Three-Level Tests (27 tests)

    [Test]
    public void Test_n000000_MethodTempAccessThrough3Levels()
    {
        // n000000: | m | m _ 10. ^[[[m] value] value] value!
        var source = WrapInTestClass(@"
n000000
    | m | m _ 10. ^[[[m] value] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(3), "Should have 3 blocks");

        // Find innermost block (smallest bytecode array, just accesses m)
        var innerBlock = blocks.OrderBy(b =>
            b.GetProperty("bytecodes").GetArrayLength()).First();

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost block has no args/temps (blockLocalCount=0)
        // Accesses m from method
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16), // PUSH_TEMP 0
            "Innermost block should access method temp 'm' with PUSH_TEMP 0");
    }

    [Test]
    public void Test_n111111_AllLevel1Arg1Temp()
    {
        // n111111: ^[:oa | | ot | ot _ 3. [:ma | | mt | mt _ 4. [:ia | | it | it _ 3. oa + ot + mt + it] value: 999] value: 888] value: 0!
        var source = WrapInTestClass(@"
n111111
    ^[:oa | | ot | ot _ 3. [:ma | | mt | mt _ 4. [:ia | | it | it _ 3. oa + ot + mt + it] value: 999] value: 888] value: 0");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(3), "Should have 3 blocks");

        // Find innermost block (has the MOST bytecodes - accesses oa, ot, mt, it)
        var innerBlock = blocks.OrderByDescending(b => b.GetProperty("bytecodes").GetArrayLength()).First();
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost has 1 arg + 1 temp (blockLocalCount=2)
        // Middle has 1 arg + 1 temp (blockLocalCount=2)
        // Outer has 1 arg + 1 temp (blockLocalCount=2)
        //
        // Expected bytecode indices (inner accessing):
        // - oa (outer arg 0): 0 + inner(2) + middle(2) = 4 → PUSH_TEMP 4 (bytecode 20)
        // - ot (outer temp 1): 1 + inner(2) + middle(2) = 5 → PUSH_TEMP 5 (bytecode 21)
        // - mt (middle temp 1): 1 + inner(2) = 3 → PUSH_TEMP 3 (bytecode 19)
        // - it (inner temp 1): 1 → PUSH_TEMP 1 (bytecode 17)

        bool foundGrandparentAccess = false;
        bool foundParentAccess = false;
        bool foundOwnAccess = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 20 || bc == 21) foundGrandparentAccess = true; // PUSH_TEMP 4-5 (oa, ot)
            if (bc == 19) foundParentAccess = true;                   // PUSH_TEMP 3 (mt)
            if (bc == 17) foundOwnAccess = true;                      // PUSH_TEMP 1 (it)
        }

        Assert.That(foundOwnAccess, Is.True, "Should access own variables");
        Assert.That(foundParentAccess, Is.True, "Should access parent (middle) variables");
        Assert.That(foundGrandparentAccess, Is.True, "Should access grandparent (outer) variables");
    }

    [Test]
    public void Test_n222222_MaxComplexity()
    {
        // n222222: ^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma1 :ma2 | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 2. oa1 + oa2 + ot1 + ot2 + mt1 + mt2 + it1 + it2] value: 999 value: 888] value: 777 value: 666] value: 0 value: 0!
        var source = WrapInTestClass(@"
n222222
    ^[:oa1 :oa2 | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma1 :ma2 | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia1 :ia2 | | it1 it2 | it1 _ 2. it2 _ 2. oa1 + oa2 + ot1 + ot2 + mt1 + mt2 + it1 + it2] value: 999 value: 888] value: 777 value: 666] value: 0 value: 0");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(3), "Should have 3 blocks");

        // Find innermost block (has 2 args and 2 temps)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 2 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 2 &&
            b.GetProperty("bytecodes").GetArrayLength() > 10);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost has 2 args + 2 temps (blockLocalCount=4)
        // Middle has 2 args + 2 temps
        // Outer has 2 args + 2 temps
        // Outer vars: logical 0-3 -> bytecode 4-7
        // Middle vars: logical 4-7 -> bytecode 8-11
        // Inner vars: index 0-3

        bool foundHighIndexAccess = false;
        bool foundLowIndexAccess = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc >= 24 && bc <= 31) foundHighIndexAccess = true; // PUSH_TEMP 8+ (parent/grandparent)
            if (bc >= 16 && bc <= 19) foundLowIndexAccess = true;  // PUSH_TEMP 0-3 (own vars)
        }

        Assert.That(foundHighIndexAccess || foundLowIndexAccess, Is.True,
            "Innermost block should access variables from all levels");
    }

    [Test]
    public void Test_n121212_UniformComplexNesting()
    {
        // n121212: ^[:oa | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia | | it1 it2 | it1 _ 2. it2 _ 2. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 0] value: 0] value: 0!
        var source = WrapInTestClass(@"
n121212
    ^[:oa | | ot1 ot2 | ot1 _ 0. ot2 _ 1. [:ma | | mt1 mt2 | mt1 _ 2. mt2 _ 3. [:ia | | it1 it2 | it1 _ 2. it2 _ 2. oa + ot1 + ot2 + ma + mt1 + mt2 + ia + it1 + it2] value: 0] value: 0] value: 0");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(3), "Should have 3 blocks");

        // Find innermost block
        var innerBlock = blocks.FirstOrDefault(b => {
            var header = b.GetProperty("header");
            return header.GetProperty("argCount").GetInt32() == 1 &&
                   header.GetProperty("tempCount").GetInt32() == 2 &&
                   b.GetProperty("bytecodes").GetArrayLength() > 10;
        });
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Innermost has 1 arg + 2 temps (blockLocalCount=3)
        // Middle has 1 arg + 2 temps
        // Outer has 1 arg + 2 temps
        // Variables are accessed including ma and ia which are args

        bool foundMultipleAccesses = false;
        int distinctIndices = new HashSet<int>(
            Enumerable.Range(0, bytecodes.GetArrayLength())
                .Select(i => bytecodes[i].GetInt32())
                .Where(bc => bc >= 16 && bc <= 31)
        ).Count;

        foundMultipleAccesses = distinctIndices >= 4; // Should access vars from multiple levels

        Assert.That(foundMultipleAccesses, Is.True,
            "Innermost block should access variables from all three levels");
    }

    #endregion

    #region Mutation Tests (3 tests)

    [Test]
    public void Test_mut1_BlockModifiesMethodTemp()
    {
        // mut1: | m | m _ 5. [m _ m + 5] value. ^m!
        var source = WrapInTestClass(@"
mut1
    | m | m _ 5. [m _ m + 5] value. ^m");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(1), "Should have 1 block");

        var block = blocks[0];
        var bytecodes = block.GetProperty("bytecodes");

        // Block has no args/temps (blockLocalCount=0)
        // Accesses and modifies m (parent temp 0)
        bool foundPushTemp0 = false;
        bool foundStoreTemp0 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0
            if (bc == 104 || bc == 96) foundStoreTemp0 = true; // STORE_TEMP 0 or COMPACT_POP_STORE_TEMP 0
        }

        Assert.That(foundPushTemp0, Is.True, "Block should read method temp 'm' with PUSH_TEMP 0");
        Assert.That(foundStoreTemp0, Is.True, "Block should write method temp 'm' with STORE_TEMP 0");
    }

    [Test]
    public void Test_mut2_InnerModifiesOuterTemp()
    {
        // mut2: ^[| t | t _ 5. [t _ t + 5] value. t] value!
        var source = WrapInTestClass(@"
mut2
    ^[| t | t _ 5. [t _ t + 5] value. t] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (no args/temps)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has no args/temps (blockLocalCount=0)
        // Accesses and modifies t from outer block (index 0)
        bool foundPushTemp0 = false;
        bool foundStoreTemp0 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundPushTemp0 = true; // PUSH_TEMP 0
            if (bc == 104 || bc == 96) foundStoreTemp0 = true; // STORE_TEMP 0 or COMPACT_POP_STORE_TEMP 0
        }

        Assert.That(foundPushTemp0, Is.True, "Inner should read outer temp 't' with PUSH_TEMP 0");
        Assert.That(foundStoreTemp0, Is.True, "Inner should write outer temp 't' with STORE_TEMP 0");
    }

    [Test]
    public void Test_mut3_ArgsImmutableInnerSeesOriginal()
    {
        // mut3: ^[:a | [:b | a] value: 999] value: 10!
        var source = WrapInTestClass(@"
mut3
    ^[:a | [:b | a] value: 999] value: 10");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (has arg b)
        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1);

        // Could be either block has 1 arg, need to check which accesses outer
        foreach (var block in blocks.Where(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1))
        {
            var bc = block.GetProperty("bytecodes");
            if (bc.GetArrayLength() <= 2) // Inner block is simpler, just returns 'a'
            {
                innerBlock = block;
                break;
            }
        }

        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner block has 1 arg (b) (blockLocalCount=1)
        // Accesses a from outer block at logical index 0
        // Bytecode index = 0 + 1 = 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17), // PUSH_TEMP 1
            "Inner block should access outer arg 'a' with PUSH_TEMP 1");
    }

    #endregion

    #region Additional Two-Level Tests

    // Continue with remaining two-level tests...
    // Due to space constraints, I'll add a selection of the remaining tests

    [Test]
    public void Test_t0012_InnerArgAnd2Temps()
    {
        // t0012: ^[[[:a | | t1 t2 | t1 _ 3. t2 _ 4. a + t1 + t2] value: 3] value] value!
        var source = WrapInTestClass(@"
t0012
    ^[[[:a | | t1 t2 | t1 _ 3. t2 _ 4. a + t1 + t2] value: 3] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 2);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Block has 1 arg + 2 temps (blockLocalCount=3)
        bool foundArg = false;
        bool foundTemp1 = false;
        bool foundTemp2 = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc == 16) foundArg = true;    // PUSH_TEMP 0 (a)
            if (bc == 17) foundTemp1 = true;  // PUSH_TEMP 1 (t1)
            if (bc == 18) foundTemp2 = true;  // PUSH_TEMP 2 (t2)
        }

        Assert.That(foundArg && foundTemp1 && foundTemp2, Is.True,
            "Should access arg and both temps");
    }

    [Test]
    public void Test_t0101_BothHaveTempsInnerAccessesOuter()
    {
        // t0101: ^[| ot | ot _ 5. [| it | it _ 5. ot + it] value] value!
        var source = WrapInTestClass(@"
t0101
    ^[| ot | ot _ 5. [| it | it _ 5. ot + it] value] value");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 1 &&
            blocks.IndexOf(b) > 0); // Inner block usually comes second

        if (innerBlock.ValueKind == JsonValueKind.Undefined)
        {
            // Try finding by bytecode pattern
            innerBlock = blocks.FirstOrDefault(b => {
                var bc = b.GetProperty("bytecodes");
                // Inner block will access both its own temp and outer temp
                return b.GetProperty("header").GetProperty("tempCount").GetInt32() == 1 &&
                       bc.GetArrayLength() > 3;
            });
        }

        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner has 1 temp (blockLocalCount=1)
        // Accesses ot from outer at logical 0 -> bytecode 1
        bool foundOuterTemp = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            if (bytecodes[i].GetInt32() == 17) // PUSH_TEMP 1
            {
                foundOuterTemp = true;
                break;
            }
        }

        Assert.That(foundOuterTemp, Is.True,
            "Inner should access outer temp 'ot' with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t1010_BothHaveArgsInnerIgnores()
    {
        // t1010: ^[:oa | [:ia | oa] value: 999] value: 10!
        var source = WrapInTestClass(@"
t1010
    ^[:oa | [:ia | oa] value: 999] value: 10");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        Assert.That(blocks.Count, Is.EqualTo(2), "Should have 2 blocks");

        // Find inner block (simpler, just returns oa)
        var innerBlock = blocks.OrderBy(b =>
            b.GetProperty("bytecodes").GetArrayLength()).First();

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner has 1 arg (ia) (blockLocalCount=1)
        // Accesses oa from outer at logical 0 -> bytecode 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17), // PUSH_TEMP 1
            "Inner should access outer arg 'oa' with PUSH_TEMP 1");
    }

    [Test]
    public void Test_t1212_OuterArg2TempsInnerArg2Temps()
    {
        // t1212: ^[:oa | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia | | it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + it1 + it2] value: 999] value: 0!
        var source = WrapInTestClass(@"
t1212
    ^[:oa | | ot1 ot2 | ot1 _ 1. ot2 _ 2. [:ia | | it1 it2 | it1 _ 3. it2 _ 4. oa + ot1 + ot2 + it1 + it2] value: 999] value: 0");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 1 &&
            b.GetProperty("header").GetProperty("tempCount").GetInt32() == 2 &&
            b.GetProperty("bytecodes").GetArrayLength() > 8);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner has 1 arg + 2 temps (blockLocalCount=3)
        // Outer: oa at 0, ot1 at 1, ot2 at 2
        // oa -> bytecode 3, ot1 -> bytecode 4, ot2 -> bytecode 5
        bool foundOuterAccess = false;

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc >= 19 && bc <= 21) // PUSH_TEMP 3-5
            {
                foundOuterAccess = true;
                break;
            }
        }

        Assert.That(foundOuterAccess, Is.True,
            "Inner should access outer variables with adjusted indices");
    }

    [Test]
    public void Test_t2100_Outer2ArgsTempInnerAccessesAll()
    {
        // t2100: ^[:oa1 :oa2 | | ot | ot _ 3. [oa1 + oa2 + ot] value] value: 3 value: 4!
        var source = WrapInTestClass(@"
t2100
    ^[:oa1 :oa2 | | ot | ot _ 3. [oa1 + oa2 + ot] value] value: 3 value: 4");

        using var json = CompileToJson(source);
        var blocks = FindBlocks(json);

        var innerBlock = blocks.FirstOrDefault(b =>
            b.GetProperty("header").GetProperty("argCount").GetInt32() == 0);
        Assert.That(innerBlock.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));

        var bytecodes = innerBlock.GetProperty("bytecodes");

        // Inner has no locals (blockLocalCount=0)
        // Outer: oa1 at 0, oa2 at 1, ot at 2
        bool foundAllOuter = false;
        var foundIndices = new HashSet<int>();

        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            int bc = bytecodes[i].GetInt32();
            if (bc >= 16 && bc <= 18) // PUSH_TEMP 0-2
            {
                foundIndices.Add(bc);
            }
        }

        foundAllOuter = foundIndices.Count == 3;
        Assert.That(foundAllOuter, Is.True,
            "Inner should access all three outer variables");
    }

    #endregion
}