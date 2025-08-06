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
            new[] { "obj" }, // Add 'obj' as a temporary variable
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
    }

    [Fact]
    public void CompileDynamicArrayExpression_GeneratesCorrectBytecode()
    {
        // Test: { expr1. expr2. expr3 }
        var elements = new List<ExpressionNode>
        {
            new LiteralExpression(1),
            new LiteralExpression("hello"),
            new UnarySendExpression(new VariableExpression("obj"), "method")
        };

        var dynArray = new DynamicArrayExpression(elements);
        var sequence = new SequenceNode(
            new[] { "obj" }, // Add 'obj' as a temporary variable
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

        // Should contain Array class reference and the literal count (3)
        Assert.Contains("Array", compiled.Literals);
        Assert.Contains(3, compiled.Literals); // The count 3 is pushed as a literal
    }

    [Fact]
    public void CompileDynamicDictionaryExpression_GeneratesCorrectBytecode()
    {
        // Test: #{ key1 -> value1. key2 -> value2 }
        var elements = new List<ExpressionNode>
        {
            new LiteralExpression("name"),
            new LiteralExpression("John"),
            new LiteralExpression("age"),
            new LiteralExpression(25)
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

        // Verify dictionary creation pattern similar to dynamic arrays
        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Dynamic dictionary bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should contain Dictionary class reference
        Assert.Contains("Dictionary", compiled.Literals);
    }

    [Fact]
    public void CompileReturnStatements_GeneratesCorrectBytecode()
    {
        // Test optimized return bytecodes for pseudo-variables
        var testCases = new[]
        {
            (Expression: (object)new PseudoVariableExpression("self"), OptimizedReturn: Bytecode.ReturnSelf),
            (Expression: (object)new PseudoVariableExpression("nil"), OptimizedReturn: Bytecode.ReturnNil),
            (Expression: (object)new PseudoVariableExpression("true"), OptimizedReturn: Bytecode.ReturnTrue),
            (Expression: (object)new PseudoVariableExpression("false"), OptimizedReturn: Bytecode.ReturnFalse)
        };

        foreach (var (returnExpr, expectedReturnBytecode) in testCases)
        {
            var sequence = new SequenceNode(
                Array.Empty<string>(),
                new[] { new AnswerStatement((ExpressionNode)returnExpr) }
            );

            var compiled = AstMethodCompiler.CompileMethod(
                sequence,
                Array.Empty<string>(),
                Array.Empty<string>()
            );

            Assert.NotNull(compiled);
            Assert.NotEmpty(compiled.Bytecodes);

            var bytecodes = compiled.Bytecodes;
            output.WriteLine($"Return {returnExpr} bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

            // Should generate single optimized return bytecode (no push needed)
            Assert.Single(bytecodes);
            Assert.Equal((byte)expectedReturnBytecode, bytecodes[0]);
        }
    }

    [Fact]
    public void CompileComplexReturnStatement_GeneratesReturnTop()
    {
        // Test: ^ expr (complex expression that can't be optimized)
        var returnExpr = new BinarySendExpression(
            new LiteralExpression(1),
            "+",
            new LiteralExpression(2)
        );

        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new AnswerStatement(returnExpr) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Complex return bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should contain ReturnTopFromMethod for non-optimizable returns
        Assert.Contains((byte)Bytecode.ReturnTopFromMethod, bytecodes);
    }

    [Fact]
    public void CompileNestedAdvancedFeatures_HandlesComplexExpressions()
    {
        // Test: cascade with dynamic array: obj msg1: { a. b }; msg2
        var innerArray = new DynamicArrayExpression(new List<ExpressionNode>
        {
            new VariableExpression("a"),
            new VariableExpression("b")
        });

        var receiver = new VariableExpression("obj");
        var messages = new List<ExpressionNode>
        {
            new KeywordSendExpression(receiver, new[] { "msg1:" }, new[] { innerArray }),
            new UnarySendExpression(receiver, "msg2")
        };

        var cascade = new CascadeExpression(receiver, messages);
        var sequence = new SequenceNode(
            new[] { "obj", "a", "b" }, // Add all variables as temporaries
            new[] { new ExpressionStatement(cascade) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Nested features bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should handle both cascade and dynamic array features
        Assert.Contains((byte)Bytecode.DuplicateTop, bytecodes); // From cascade
        Assert.Contains("Array", compiled.Literals); // From dynamic array
    }

    [Fact]
    public void CompileEmptyDynamicArray_GeneratesCorrectBytecode()
    {
        // Test: { } (empty dynamic array)
        var emptyArray = new DynamicArrayExpression(new List<ExpressionNode>());
        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(emptyArray) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Empty array bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should still push zero as count and create array
        Assert.Contains((byte)Bytecode.PushZero, bytecodes);
        Assert.Contains("Array", compiled.Literals);
    }

    [Fact]
    public void CompileEmptyDynamicDictionary_GeneratesCorrectBytecode()
    {
        // Test: #{ } (empty dynamic dictionary)
        var emptyDict = new DynamicDictionaryExpression(new List<ExpressionNode>());
        var sequence = new SequenceNode(
            Array.Empty<string>(),
            new[] { new ExpressionStatement(emptyDict) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Empty dictionary bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should create empty dictionary
        Assert.Contains("Dictionary", compiled.Literals);
    }

    [Fact]
    public void CompileCascadeWithManyMessages_OptimizesCorrectly()
    {
        // Test: receiver msg1; msg2; msg3; msg4; msg5 (many messages to verify optimization scales)
        var receiver = new VariableExpression("obj");
        var messages = Enumerable.Range(1, 5)
            .Select(i => new UnarySendExpression(receiver, $"msg{i}"))
            .Cast<ExpressionNode>()
            .ToList();

        var cascade = new CascadeExpression(receiver, messages);
        var sequence = new SequenceNode(
            new[] { "obj" }, // Add 'obj' as a temporary variable
            new[] { new ExpressionStatement(cascade) }
        );

        var compiled = AstMethodCompiler.CompileMethod(
            sequence,
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        Assert.NotNull(compiled);
        Assert.NotEmpty(compiled.Bytecodes);

        var bytecodes = compiled.Bytecodes;
        output.WriteLine($"Many messages cascade bytecodes: {string.Join(", ", bytecodes.Select(b => (Bytecode)b))}");

        // Should have 4 DuplicateTop (for first 4 messages, last one doesn't need it)
        var dupCount = bytecodes.Count(b => b == (byte)Bytecode.DuplicateTop);
        Assert.Equal(4, dupCount);

        // Should have 5 PopStack (4 for first 4 message results + 1 for cascade expression result)
        var popCount = bytecodes.Count(b => b == (byte)Bytecode.PopStack);
        Assert.Equal(5, popCount);

        // Should have implicit return self
        Assert.Contains((byte)Bytecode.ReturnSelf, bytecodes);
    }
}
