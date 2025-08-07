using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Smalltalk80.ByteCodes;
using Smalltalk80.Parser;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for advanced AST-to-bytecode compilation features including
/// cascade expressions, dynamic arrays, dynamic dictionaries, and enhanced returns.
/// </summary>
public class AdvancedAstCompilerTests
{
    private readonly ITestOutputHelper output;

    public AdvancedAstCompilerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void CompileCascadeExpression_GeneratesOptimizedBytecode()
    {
        // Test: receiver msg1; msg2; msg3
        var receiver = new VariableExpression("obj");
        var messages = new List<ExpressionNode>
        {
            new UnarySendExpression(new PseudoVariableExpression("self"), "method1"),
            new UnarySendExpression(new PseudoVariableExpression("self"), "method2"),
            new BinarySendExpression(new PseudoVariableExpression("self"), "+", new LiteralExpression(1))
        };

        var cascade = new CascadeExpression(receiver, messages);
        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(cascade) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);
        
        // Verify cascade pattern: push receiver, dup, send, pop, dup, send, pop, send
        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Cascade bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");
        
        // Should contain DuplicateTop bytecodes for receiver optimization
        Assert.Contains((byte)Bytecode.DuplicateTop, bytecodes);
        Assert.Contains((byte)Bytecode.PopStack, bytecodes);
    }    [Fact]
    public void CompileDynamicArray_GeneratesRuntimeArrayCreation()
    {
        // Test: { 1. 2. 'hello'. obj method }
        var elements = new List<ExpressionNode>
        {
            new LiteralExpression(1),
            new LiteralExpression(2),
            new LiteralExpression("hello"),
            new UnarySendExpression(new VariableExpression("obj"), "method")
        };

        var dynArray = new DynamicArrayExpression(elements);
        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(dynArray) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        // Verify array creation pattern: push elements, push count, push Array class, send newFrom:
        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Dynamic array bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should push the count (4) and contain Array class reference
        Assert.Contains((byte)Bytecode.PushFour, bytecodes);
        Assert.Contains("Array", compiled.Literals);
    }

    [Fact]
    public void CompileDynamicDictionary_GeneratesRuntimeDictionaryCreation()
    {
        // Test: { #key1 -> 'value1'. #key2 -> 42 }
        var elements = new List<ExpressionNode>
        {
            new LiteralExpression("key1"),
            new LiteralExpression("value1"),
            new LiteralExpression("key2"),
            new LiteralExpression(42)
        };

        var dynDict = new DynamicDictionaryExpression(elements);
        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(dynDict) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        // Verify dictionary creation pattern: push pairs, push count, push Dictionary class, send newFromPairs:
        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Dynamic dictionary bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should push the pair count (2) and contain Dictionary class reference
        Assert.Contains((byte)Bytecode.PushTwo, bytecodes);
        Assert.Contains("Dictionary", compiled.Literals);
    }

    [Fact]
    public void CompileReturnExpression_GeneratesOptimizedReturnBytecodes()
    {
        // Test various return forms: ^self, ^true, ^false, ^nil, ^expression
        var testCases = new[]
        {
            (new ReturnExpression(new PseudoVariableExpression("self")), Bytecode.ReturnSelf),
            (new ReturnExpression(new PseudoVariableExpression("true")), Bytecode.ReturnTrue),
            (new ReturnExpression(new PseudoVariableExpression("false")), Bytecode.ReturnFalse),
            (new ReturnExpression(new PseudoVariableExpression("nil")), Bytecode.ReturnNil)
        };

        foreach (var (returnExpr, expectedBytecode) in testCases)
        {
            var method = new MethodNode("testReturn", Array.Empty<string>(), new SequenceNode(
                Array.Empty<string>(),
                new[] { new AnswerStatement(returnExpr.Value) }
            ));

            var compiler = new AstMethodCompiler();
            var compiled = compiler.Compile(method);

            Assert.NotNull(compiled);
            Assert.Contains((byte)expectedBytecode, compiled.Bytecodes);

            output.WriteLine($"Return {returnExpr.Value} -> {expectedBytecode}");
        }
    }

    [Fact]
    public void CompileReturnExpression_ComplexExpression_GeneratesGenericReturn()
    {
        // Test: ^obj method + 1
        var complexExpr = new BinarySendExpression(
            new UnarySendExpression(new VariableExpression("obj"), "method"),
            "+",
            new LiteralExpression(1)
        );

        var returnExpr = new ReturnExpression(complexExpr);
        var method = new MethodNode("testComplexReturn", Array.Empty<string>(), new SequenceNode(
            Array.Empty<string>(),
            new[] { new AnswerStatement(returnExpr.Value) }
        ));

        var compiler = new AstMethodCompiler();
        var compiled = compiler.Compile(method);

        Assert.NotNull(compiled);
        Assert.Contains((byte)Bytecode.ReturnTopFromMethod, compiled.Bytecodes);

        output.WriteLine($"Complex return bytecodes: {string.Join(", ", compiled.Bytecodes.Select(b => (Bytecode)b))}");
    }

    [Fact]
    public void CompileNestedAdvancedFeatures_GeneratesCorrectBytecode()
    {
        // Test: obj method1; method2: { 1. obj value }; method3: { #key -> obj result }
        var receiver = new VariableExpression("obj");
        var dynamicArray = new DynamicArrayExpression(new List<ExpressionNode>
        {
            new LiteralExpression(1),
            new UnarySendExpression(new VariableExpression("obj"), "value")
        });
        var dynamicDict = new DynamicDictionaryExpression(new List<ExpressionNode>
        {
            new LiteralExpression("key"),
            new UnarySendExpression(new VariableExpression("obj"), "result")
        });

        var messages = new List<ExpressionNode>
        {
            new UnarySendExpression(new PseudoVariableExpression("self"), "method1"),
            new KeywordSendExpression(
                new PseudoVariableExpression("self"),
                new[] { "method2:" },
                new[] { dynamicArray }
            ),
            new KeywordSendExpression(
                new PseudoVariableExpression("self"),
                new[] { "method3:" },
                new[] { dynamicDict }
            )
        };

        var cascade = new CascadeExpression(receiver, messages);
        var method = new MethodNode("testNestedFeatures", Array.Empty<string>(), new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(cascade) }
        ));

        var compiler = new AstMethodCompiler();
        var compiled = compiler.Compile(method);

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        // Should contain references to both Array and Dictionary classes
        Assert.Contains("Array", compiled.Literals);
        Assert.Contains("Dictionary", compiled.Literals);

        output.WriteLine($"Nested features bytecodes: {string.Join(", ", compiled.Bytecodes.Select(b => (Bytecode)b))}");
        output.WriteLine($"Literals: {string.Join(", ", compiled.Literals)}");
    }

    [Fact]
    public void CompileEmptyDynamicArray_GeneratesCorrectBytecode()
    {
        // Test: { }
        var emptyArray = new DynamicArrayExpression(new List<ExpressionNode>());
        var method = new MethodNode("testEmptyArray", Array.Empty<string>(), new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(emptyArray) }
        ));

        var compiler = new AstMethodCompiler();
        var compiled = compiler.Compile(method);

        Assert.NotNull(compiled);
        Assert.Contains((byte)Bytecode.PushZero, compiled.Bytecodes); // Array size 0
        Assert.Contains("Array", compiled.Literals);
    }

    [Fact]
    public void CompileEmptyDynamicDictionary_GeneratesCorrectBytecode()
    {
        // Test: { }
        var emptyDict = new DynamicDictionaryExpression(new List<ExpressionNode>());
        var method = new MethodNode("testEmptyDict", Array.Empty<string>(), new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(emptyDict) }
        ));

        var compiler = new AstMethodCompiler();
        var compiled = compiler.Compile(method);

        Assert.NotNull(compiled);
        Assert.Contains((byte)Bytecode.PushZero, compiled.Bytecodes); // Pair count 0
        Assert.Contains("Dictionary", compiled.Literals);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void CompileCascadeWithDifferentMessageCounts_GeneratesCorrectPattern(int messageCount)
    {
        var receiver = new VariableExpression("obj");
        var messages = new List<ExpressionNode>();

        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(new UnarySendExpression(new PseudoVariableExpression("self"), $"method{i}"));
        }

        var cascade = new CascadeExpression(receiver, messages);
        var method = new MethodNode("testCascadeCount", Array.Empty<string>(), new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(cascade) }
        ));

        var compiler = new AstMethodCompiler();
        var compiled = compiler.Compile(method);

        Assert.NotNull(compiled);

        // Count DuplicateTop bytecodes (should be messageCount - 1)
        var duplicateCount = compiled.Bytecodes.Count(b => b == (byte)Bytecode.DuplicateTop);
        Assert.Equal(messageCount - 1, duplicateCount);

        output.WriteLine($"Cascade with {messageCount} messages has {duplicateCount} duplicate bytecodes");
    }
}
