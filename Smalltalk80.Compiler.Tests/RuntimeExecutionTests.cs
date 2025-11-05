using NUnit.Framework;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Runtime execution validation tests.
/// These tests compile Smalltalk code and execute it in the interpreter,
/// validating that the VM correctly accesses the expected temp/arg/ivar slots.
///
/// This is Phase 2 of comprehensive validation:
/// - Phase 1: Bytecode generation correctness (ComprehensiveContextAccessTests) ✅
/// - Phase 2: Runtime execution correctness (this file) 🔄
/// </summary>
[TestFixture]
public class RuntimeExecutionTests
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
    instanceVariableNames: 'ivar1 ivar2 ivar3'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'testing'!

{methodSource}
! !
";
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void L1_MethodAccessesArgAndTemp_Runtime()
    {
        var source = WrapInTestClass(@"
test: arg1
    | temp1 |
    temp1 := arg1 + 1.
    ^temp1");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute test:5
        // Expected: Returns 6 (arg1=5, temp1=6)
        // Validates:
        // - arg1 accessed from correct slot (5 in context)
        // - temp1 stored to correct slot (6 in context)
        // - temp1 read from correct slot (6 in context)
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void L2_BlockAccessesMethodTemp_Runtime()
    {
        var source = WrapInTestClass(@"
testBlock
    | outer |
    outer := 10.
    ^[outer + 5] value");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute testBlock
        // Expected: Returns 15
        // Validates:
        // - Block correctly accesses outer temp (slot 6 in parent context)
        // - VM resolves homeContext correctly
        // - VM subtracts blockLocalCount when accessing parent temp
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void L2_BlockModifiesMethodTemp_Runtime()
    {
        var source = WrapInTestClass(@"
testModify
    | counter |
    counter := 0.
    [counter := counter + 1] value.
    ^counter");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute testModify
        // Expected: Returns 1
        // Validates:
        // - Block reads counter from parent context (slot 6)
        // - Block writes counter back to parent context (slot 6)
        // - Modification visible to parent method after block execution
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void L3_NestedBlockAccessesGrandparentTemp_Runtime()
    {
        var source = WrapInTestClass(@"
testNested
    | outer |
    outer := 10.
    ^[[outer + 5] value] value");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute testNested
        // Expected: Returns 15
        // Validates:
        // - Inner block correctly accesses grandparent temp through homeContext chain
        // - VM correctly follows homeContext pointers through 2 levels
        // - Temp index calculation works across 2 context levels
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void RealWorld_atAllPut_Runtime()
    {
        var source = WrapInTestClass(@"
atAllPut: anObject
    | index size |
    index := 0.
    size := 16.
    [(index := index + 1) <= size]
        whileTrue: [Transcript show: index printString]");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute atAllPut: #test
        // Expected: Prints "123456789101112131415" (16 iterations)
        // Validates:
        // - Condition block reads index from parent (slot 6)
        // - Condition block modifies index in parent (slot 6)
        // - Body block reads index from parent (slot 6)
        // - Loop terminates after exactly 16 iterations
        // - NO infinite loop (currently failing)
    }

    [Test]
    [Ignore("Runtime execution not yet integrated with test harness")]
    public void L2_BlockAccessesMixedVariables_Runtime()
    {
        var source = WrapInTestClass(@"
test: arg1
    | temp1 |
    ivar1 := 1.
    temp1 := 10.
    ^[:x | ivar1 + arg1 + temp1 + x] value: 5");

        using var json = CompileToJson(source);

        // TODO: Load bundle into VM and execute test: 100
        // Expected: Returns 116 (ivar1=1, arg1=100, temp1=10, x=5)
        // Validates:
        // - Block accesses instance variable (slot 1 in receiver)
        // - Block accesses parent argument (slot 5 in parent context)
        // - Block accesses parent temp (slot 6 in parent context)
        // - Block accesses own argument (slot 5 in block context)
        // - All 4 variable types accessed correctly in single expression
    }
}
