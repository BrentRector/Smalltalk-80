using NUnit.Framework;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Comprehensive tests for blocks accessing outer method temporaries.
/// These tests validate the ACTUAL BYTECODE INDICES, not just structure.
/// </summary>
[TestFixture]
public class BlockOuterTempTests
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
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'testing'!

{methodSource}
! !
";
    }

    [Test]
    public void TestBlockAccessingSingleOuterTemp()
    {
        var source = WrapInTestClass(@"
testMethod
    | outer |
    outer := 10.
    ^[outer + 5] value");

        using var json = CompileToJson(source);
        var root = json.RootElement;

        // Find the BLOCK method (not the main method)
        // Blocks have selectorIndex = -1
        JsonElement? blockMethod = null;
        var methods = root.GetProperty("methods");
        for (int i = 0; i < methods.GetArrayLength(); i++)
        {
            var m = methods[i];
            if (m.TryGetProperty("selectorIndex", out var selector) && selector.GetInt32() == -1)
            {
                blockMethod = m;
                break;
            }
        }

        Assert.That(blockMethod, Is.Not.Null, "Should have a block method");

        var bytecodes = blockMethod.Value.GetProperty("bytecodes");

        // Block accesses 'outer' which is temp 0 in parent
        // Block has no args, no temps, so blockLocalCount = 0
        // Bytecode should be PUSH_TEMP 0 (first parent temp)
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16),
            "Block should PUSH_TEMP 0 when accessing first outer temp");
    }

    [Test]
    public void TestBlockAccessingSecondOuterTemp()
    {
        var source = WrapInTestClass(@"
testMethod
    | temp1 temp2 |
    temp1 := 10.
    temp2 := 20.
    ^[temp2 + 5] value");

        using var json = CompileToJson(source);
        var root = json.RootElement;

        // Find block method
        // Blocks have selectorIndex = -1
        JsonElement? blockMethod = null;
        var methods = root.GetProperty("methods");
        for (int i = 0; i < methods.GetArrayLength(); i++)
        {
            var m = methods[i];
            if (m.TryGetProperty("selectorIndex", out var selector) && selector.GetInt32() == -1)
            {
                blockMethod = m;
                break;
            }
        }

        Assert.That(blockMethod, Is.Not.Null);
        var bytecodes = blockMethod.Value.GetProperty("bytecodes");

        // Block accesses 'temp2' which is temp 1 in parent (second temp)
        // Should be PUSH_TEMP 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Block should PUSH_TEMP 1 when accessing second outer temp");
    }

    [Test]
    public void TestBlockAccessingOuterTempAfterArg()
    {
        var source = WrapInTestClass(@"
testMethod: arg
    | temp |
    temp := 10.
    ^[temp + arg] value");

        using var json = CompileToJson(source);
        var root = json.RootElement;

        // Find block method
        // Blocks have selectorIndex = -1
        JsonElement? blockMethod = null;
        var methods = root.GetProperty("methods");
        for (int i = 0; i < methods.GetArrayLength(); i++)
        {
            var m = methods[i];
            if (m.TryGetProperty("selectorIndex", out var selector) && selector.GetInt32() == -1)
            {
                blockMethod = m;
                break;
            }
        }

        Assert.That(blockMethod, Is.Not.Null);
        var bytecodes = blockMethod.Value.GetProperty("bytecodes");

        // Method has: arg at 0, temp at 1
        // Block accesses temp (index 1)
        // Should be PUSH_TEMP 1
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Block should PUSH_TEMP 1 when accessing outer temp after arg");
    }

    [Test]
    public void TestBlockModifyingOuterTemp_atAllPutCase()
    {
        var source = WrapInTestClass(@"
atAllPut: anObject
    | index size |
    index := 0.
    size := 16.
    [(index := index + 1) <= size]
        whileTrue: [self]");

        using var json = CompileToJson(source);
        var root = json.RootElement;

        // Find the CONDITION block (first block literal)
        JsonElement? conditionBlock = null;
        var methods = root.GetProperty("methods");

        // Get main method
        var mainMethod = methods[0];
        var literals = mainMethod.GetProperty("literals");

        // First literal should be the condition block
        if (literals.GetArrayLength() > 0)
        {
            var lit0 = literals[0];
            if (lit0.TryGetProperty("localId", out var blockId))
            {
                string blockLocalId = blockId.GetString()!;

                // Find this block's method
                for (int i = 0; i < methods.GetArrayLength(); i++)
                {
                    var m = methods[i];
                    if (m.TryGetProperty("localId", out var lid) &&
                        lid.GetString() == blockLocalId)
                    {
                        conditionBlock = m;
                        break;
                    }
                }
            }
        }

        Assert.That(conditionBlock, Is.Not.Null, "Should find condition block");

        var bytecodes = conditionBlock.Value.GetProperty("bytecodes");

        // atAllPut: has arg at 0, index at 1, size at 2
        // Condition block: [(index := index + 1) <= size]
        // Should be:
        //   PUSH_TEMP 1   (push 'index')
        //   PUSH 1
        //   SEND +
        //   DUP
        //   STORE_TEMP 1  (store back to 'index')
        //   PUSH_TEMP 2   (push 'size')
        //   SEND <=
        //   RETURN

        Console.WriteLine("Condition block bytecodes:");
        for (int i = 0; i < Math.Min(9, bytecodes.GetArrayLength()); i++)
        {
            Console.WriteLine($"  [{i}] = {bytecodes[i].GetInt32()}");
        }

        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(17),
            "Should PUSH_TEMP 1 for 'index' (1st temp)");
        Assert.That(bytecodes[4].GetInt32() == 105 || bytecodes[5].GetInt32() == 105, Is.True,
            "Should STORE_TEMP 1 for 'index'");
        Assert.That(bytecodes.GetArrayLength() >= 7, Is.True);

        // Find the PUSH for 'size'
        bool foundSizePush = false;
        for (int i = 5; i < Math.Min(8, bytecodes.GetArrayLength()); i++)
        {
            if (bytecodes[i].GetInt32() == 18)  // PUSH_TEMP 2
            {
                foundSizePush = true;
                break;
            }
        }
        Assert.That(foundSizePush, Is.True, "Should PUSH_TEMP 2 for 'size' (2nd temp)");
    }
}
