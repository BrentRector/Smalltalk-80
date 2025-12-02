using Antlr4.Runtime;
using Smalltalk80.Compiler;
using Smalltalk80.Compiler.Generated;
using Xunit;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Comprehensive unit tests for Phase 3 Blue Book bytecode reconciliation:
/// Control structure optimization (ifTrue:/ifFalse:/and:/or: inlining).
///
/// These tests verify that the compiler generates conditional jump bytecodes
/// instead of message sends for inlineable control structures with zero-arg blocks.
///
/// Blue Book Bytecode Reference:
/// - 0xA0-0xA3: Long unconditional backward jump
/// - 0xA4-0xA7: Long unconditional forward jump
/// - 0xA8-0xAB: Long conditional jump on true
/// - 0xAC-0xAF: Long conditional jump on false
/// - 0x90-0x97: Short unconditional jump
/// - 0xF0-0xFF: Short conditional jump on false
/// </summary>
[Trait("Category", "Compiler")]
public class ControlStructureInliningTests
{
    #region Bytecode Constants

    // Long jump bytecode ranges
    private const byte LONG_UNCONDITIONAL_BACKWARD_BASE = 0xA0;
    private const byte LONG_UNCONDITIONAL_BACKWARD_MAX = 0xA3;
    private const byte LONG_UNCONDITIONAL_FORWARD_BASE = 0xA4;
    private const byte LONG_UNCONDITIONAL_FORWARD_MAX = 0xA7;
    private const byte LONG_JUMP_ON_TRUE_BASE = 0xA8;
    private const byte LONG_JUMP_ON_TRUE_MAX = 0xAB;
    private const byte LONG_JUMP_ON_FALSE_BASE = 0xAC;
    private const byte LONG_JUMP_ON_FALSE_MAX = 0xAF;

    // Short jump bytecode ranges
    private const byte SHORT_UNCONDITIONAL_JUMP_BASE = 0x90;
    private const byte SHORT_UNCONDITIONAL_JUMP_MAX = 0x97;
    private const byte SHORT_JUMP_ON_FALSE_BASE = 0xF0;
    private const byte SHORT_JUMP_ON_FALSE_MAX = 0xFF;

    // Push constants
    private const byte PUSH_TRUE = 0x78;
    private const byte PUSH_FALSE = 0x79;
    private const byte PUSH_NIL = 0x77;
    private const byte PUSH_SELF = 0x70;

    // Return bytecodes
    private const byte RETURN_TOP = 0x7C;
    private const byte RETURN_SELF = 0x78;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Compiles a Smalltalk method and returns the compiled method data.
    /// </summary>
    private static CompiledMethodData CompileMethod(string methodSource)
    {
        var inputStream = new AntlrInputStream(methodSource);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);

        var methodDef = parser.methodDef();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors");
        }

        var compiler = new BytecodeCompiler();
        return compiler.CompileMethodFromTree(methodDef, new List<string>());
    }

    /// <summary>
    /// Compiles a Smalltalk method with instance variables.
    /// </summary>
    private static CompiledMethodData CompileMethodWithInstanceVars(string methodSource, List<string> instanceVars)
    {
        var inputStream = new AntlrInputStream(methodSource);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);

        var methodDef = parser.methodDef();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors");
        }

        var compiler = new BytecodeCompiler();
        return compiler.CompileMethodFromTree(methodDef, instanceVars);
    }

    private static bool ContainsJumpOnFalse(byte[] bytecodes)
    {
        return bytecodes.Any(b =>
            (b >= LONG_JUMP_ON_FALSE_BASE && b <= LONG_JUMP_ON_FALSE_MAX) ||
            (b >= SHORT_JUMP_ON_FALSE_BASE && b <= SHORT_JUMP_ON_FALSE_MAX));
    }

    private static bool ContainsJumpOnTrue(byte[] bytecodes)
    {
        return bytecodes.Any(b => b >= LONG_JUMP_ON_TRUE_BASE && b <= LONG_JUMP_ON_TRUE_MAX);
    }

    private static bool ContainsUnconditionalForwardJump(byte[] bytecodes)
    {
        return bytecodes.Any(b =>
            (b >= LONG_UNCONDITIONAL_FORWARD_BASE && b <= LONG_UNCONDITIONAL_FORWARD_MAX) ||
            (b >= SHORT_UNCONDITIONAL_JUMP_BASE && b <= SHORT_UNCONDITIONAL_JUMP_MAX));
    }

    private static bool ContainsMessageSendFor(byte[] bytecodes, CompiledMethodData method, string selector)
    {
        for (int i = 0; i < method.Literals.Length; i++)
        {
            if (method.Literals[i] is Symbol s && s.Value == selector)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsPushNil(byte[] bytecodes)
    {
        return bytecodes.Any(b => b == PUSH_NIL);
    }

    private static bool ContainsPushTrue(byte[] bytecodes)
    {
        return bytecodes.Any(b => b == PUSH_TRUE);
    }

    private static bool ContainsPushFalse(byte[] bytecodes)
    {
        return bytecodes.Any(b => b == PUSH_FALSE);
    }

    private static int CountJumpOnFalse(byte[] bytecodes)
    {
        return bytecodes.Count(b =>
            (b >= LONG_JUMP_ON_FALSE_BASE && b <= LONG_JUMP_ON_FALSE_MAX) ||
            (b >= SHORT_JUMP_ON_FALSE_BASE && b <= SHORT_JUMP_ON_FALSE_MAX));
    }

    private static int CountJumpOnTrue(byte[] bytecodes)
    {
        return bytecodes.Count(b => b >= LONG_JUMP_ON_TRUE_BASE && b <= LONG_JUMP_ON_TRUE_MAX);
    }

    private static int CountUnconditionalJumps(byte[] bytecodes)
    {
        return bytecodes.Count(b =>
            (b >= LONG_UNCONDITIONAL_FORWARD_BASE && b <= LONG_UNCONDITIONAL_FORWARD_MAX) ||
            (b >= SHORT_UNCONDITIONAL_JUMP_BASE && b <= SHORT_UNCONDITIONAL_JUMP_MAX));
    }

    /// <summary>
    /// Returns bytecode disassembly for debugging test failures.
    /// </summary>
    private static string DisassembleBytecodes(byte[] bytecodes)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < bytecodes.Length; i++)
        {
            result.AppendLine($"  {i:D3}: 0x{bytecodes[i]:X2}");
        }
        return result.ToString();
    }

    #endregion

    #region ifTrue: Basic Tests

    [Fact]
    public void IfTrue_WithZeroArgBlock_GeneratesJumpOnFalse()
    {
        var source = "testMethod ^true ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            $"ifTrue: with zero-arg block should generate jump-on-false bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "ifTrue:"),
            "ifTrue: with zero-arg block should not generate ifTrue: message send");
    }

    [Fact]
    public void IfTrue_WithLiteralInBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [42]";
        var method = CompileMethod(source);

        Assert.Contains(method.Literals, lit => lit is int i && i == 42);
    }

    [Fact]
    public void IfTrue_WithStringLiteralInBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: ['hello']";
        var method = CompileMethod(source);

        Assert.Contains(method.Literals, lit => lit is string s && s == "hello");
    }

    [Fact]
    public void IfTrue_WithSymbolInBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [#mySymbol]";
        var method = CompileMethod(source);

        Assert.Contains(method.Literals, lit => lit is Symbol sym && sym.Value == "mySymbol");
    }

    [Fact]
    public void IfTrue_PushesNilForFalseBranch()
    {
        var source = "testMethod ^false ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsPushNil(bytecodes),
            $"ifTrue: should push nil for false branch\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Fact]
    public void IfTrue_WithSelfInBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [self]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with self in block should be inlined");
    }

    #endregion

    #region ifTrue: Block Argument Variations

    [Fact]
    public void IfTrue_WithOneArgBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: [:x | x + 1]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:"),
            "ifTrue: with one-arg block should generate ifTrue: message send");
    }

    [Fact]
    public void IfTrue_WithTwoArgBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: [:x :y | x + y]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:"),
            "ifTrue: with two-arg block should generate ifTrue: message send");
    }

    [Fact]
    public void IfTrue_WithNonBlockArgument_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: 42";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:"),
            "ifTrue: with non-block argument should generate message send");
    }

    [Fact]
    public void IfTrue_WithVariableAsArgument_FallsBackToMessageSend()
    {
        var source = "testMethod | block | block := [42]. ^true ifTrue: block";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:"),
            "ifTrue: with variable as argument should generate message send");
    }

    #endregion

    #region ifFalse: Basic Tests

    [Fact]
    public void IfFalse_WithZeroArgBlock_GeneratesJumpOnTrue()
    {
        var source = "testMethod ^false ifFalse: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            $"ifFalse: with zero-arg block should generate jump-on-true bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "ifFalse:"),
            "ifFalse: with zero-arg block should not generate ifFalse: message send");
    }

    [Fact]
    public void IfFalse_PushesNilForTrueBranch()
    {
        var source = "testMethod ^true ifFalse: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsPushNil(bytecodes),
            $"ifFalse: should push nil when condition is true\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Fact]
    public void IfFalse_WithOneArgBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^false ifFalse: [:x | x + 1]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifFalse:"),
            "ifFalse: with one-arg block should generate ifFalse: message send");
    }

    [Fact]
    public void IfFalse_WithNonBlockArgument_FallsBackToMessageSend()
    {
        var source = "testMethod ^false ifFalse: 42";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifFalse:"),
            "ifFalse: with non-block argument should generate message send");
    }

    #endregion

    #region ifTrue:ifFalse: Basic Tests

    [Fact]
    public void IfTrueIfFalse_WithZeroArgBlocks_GeneratesJumps()
    {
        var source = "testMethod ^true ifTrue: [1] ifFalse: [2]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            $"ifTrue:ifFalse: should generate jump-on-false bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.True(ContainsUnconditionalForwardJump(bytecodes),
            "ifTrue:ifFalse: should generate unconditional jump to skip false branch");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "ifTrue:ifFalse:"),
            "ifTrue:ifFalse: with zero-arg blocks should not generate message send");
    }

    [Fact]
    public void IfTrueIfFalse_ContainsBothLiterals()
    {
        var source = "testMethod ^true ifTrue: [1] ifFalse: [2]";
        var method = CompileMethod(source);

        Assert.Contains(method.Literals, lit => lit is int i && i == 1);
        Assert.Contains(method.Literals, lit => lit is int i && i == 2);
    }

    [Fact]
    public void IfTrueIfFalse_WithTrueArgNonBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: 1 ifFalse: [2]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:ifFalse:"),
            "ifTrue:ifFalse: with non-block first arg should generate message send");
    }

    [Fact]
    public void IfTrueIfFalse_WithFalseArgNonBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: [1] ifFalse: 2";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:ifFalse:"),
            "ifTrue:ifFalse: with non-block second arg should generate message send");
    }

    [Fact]
    public void IfTrueIfFalse_WithTrueBlockHavingArgs_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: [:x | x] ifFalse: [2]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:ifFalse:"),
            "ifTrue:ifFalse: with true block having args should generate message send");
    }

    [Fact]
    public void IfTrueIfFalse_WithFalseBlockHavingArgs_FallsBackToMessageSend()
    {
        var source = "testMethod ^true ifTrue: [1] ifFalse: [:x | x]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifTrue:ifFalse:"),
            "ifTrue:ifFalse: with false block having args should generate message send");
    }

    #endregion

    #region ifFalse:ifTrue: Basic Tests

    [Fact]
    public void IfFalseIfTrue_WithZeroArgBlocks_GeneratesJumps()
    {
        var source = "testMethod ^false ifFalse: [1] ifTrue: [2]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            $"ifFalse:ifTrue: should generate jump-on-true bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.True(ContainsUnconditionalForwardJump(bytecodes),
            "ifFalse:ifTrue: should generate unconditional jump");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "ifFalse:ifTrue:"),
            "ifFalse:ifTrue: with zero-arg blocks should not generate message send");
    }

    [Fact]
    public void IfFalseIfTrue_WithNonBlockArgs_FallsBackToMessageSend()
    {
        var source = "testMethod ^false ifFalse: 1 ifTrue: 2";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "ifFalse:ifTrue:"),
            "ifFalse:ifTrue: with non-block args should generate message send");
    }

    #endregion

    #region and: Tests

    [Fact]
    public void And_WithZeroArgBlock_GeneratesJumpOnFalse()
    {
        var source = "testMethod ^true and: [false]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            $"and: should generate jump-on-false for short-circuit evaluation\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "and:"),
            "and: with zero-arg block should not generate and: message send");
    }

    [Fact]
    public void And_WithBlockReturningTrue_CompilesCorrectly()
    {
        var source = "testMethod ^true and: [true]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "and: with [true] should be inlined");
        Assert.True(ContainsPushTrue(bytecodes),
            "and: with [true] should push true in block body");
    }

    [Fact]
    public void And_WithBlockReturningFalse_CompilesCorrectly()
    {
        var source = "testMethod ^true and: [false]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "and: with [false] should be inlined");
        Assert.True(ContainsPushFalse(bytecodes),
            "and: with [false] should push false in block body");
    }

    [Fact]
    public void And_WithNonBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true and: false";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "and:"),
            "and: with non-block argument should generate and: message send");
    }

    [Fact]
    public void And_WithOneArgBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^true and: [:x | x]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "and:"),
            "and: with one-arg block should generate and: message send");
    }

    [Fact]
    public void And_WithVariableBlock_FallsBackToMessageSend()
    {
        var source = "testMethod | blk | blk := [false]. ^true and: blk";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "and:"),
            "and: with variable block should generate and: message send");
    }

    #endregion

    #region or: Tests

    [Fact]
    public void Or_WithZeroArgBlock_GeneratesJumpOnTrue()
    {
        var source = "testMethod ^false or: [true]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            $"or: should generate jump-on-true for short-circuit evaluation\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "or:"),
            "or: with zero-arg block should not generate or: message send");
    }

    [Fact]
    public void Or_WithBlockReturningTrue_CompilesCorrectly()
    {
        var source = "testMethod ^false or: [true]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            "or: with [true] should be inlined");
    }

    [Fact]
    public void Or_WithBlockReturningFalse_CompilesCorrectly()
    {
        var source = "testMethod ^false or: [false]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            "or: with [false] should be inlined");
    }

    [Fact]
    public void Or_WithNonBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^false or: true";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "or:"),
            "or: with non-block argument should generate or: message send");
    }

    [Fact]
    public void Or_WithOneArgBlock_FallsBackToMessageSend()
    {
        var source = "testMethod ^false or: [:x | x]";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "or:"),
            "or: with one-arg block should generate or: message send");
    }

    #endregion

    #region Nested Control Structures

    [Fact]
    public void NestedIfTrue_GeneratesMultipleJumps()
    {
        var source = "testMethod ^true ifTrue: [false ifTrue: [42]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.True(jumpCount >= 2,
            $"Nested ifTrue: should generate at least 2 jump-on-false bytecodes, found {jumpCount}\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Fact]
    public void NestedIfFalse_GeneratesMultipleJumps()
    {
        var source = "testMethod ^false ifFalse: [true ifFalse: [42]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnTrue(bytecodes);
        Assert.True(jumpCount >= 2,
            $"Nested ifFalse: should generate at least 2 jump-on-true bytecodes, found {jumpCount}\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Fact]
    public void MixedNestedControlStructures_CompileCorrectly()
    {
        var source = "testMethod ^true ifTrue: [false ifFalse: [42]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "Outer ifTrue: should generate jump-on-false");
        Assert.True(ContainsJumpOnTrue(bytecodes),
            "Inner ifFalse: should generate jump-on-true");
    }

    [Fact]
    public void DeeplyNestedIfTrue_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [true ifTrue: [true ifTrue: [42]]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.True(jumpCount >= 3,
            $"Deeply nested ifTrue: should generate at least 3 jump-on-false bytecodes, found {jumpCount}");
    }

    [Fact]
    public void AndChain_CompilesCorrectly()
    {
        var source = "testMethod ^true and: [true and: [false]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.True(jumpCount >= 2,
            $"Chained and: should generate at least 2 jump-on-false bytecodes, found {jumpCount}");
    }

    [Fact]
    public void OrChain_CompilesCorrectly()
    {
        var source = "testMethod ^false or: [false or: [true]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnTrue(bytecodes);
        Assert.True(jumpCount >= 2,
            $"Chained or: should generate at least 2 jump-on-true bytecodes, found {jumpCount}");
    }

    [Fact]
    public void MixedAndOr_CompilesCorrectly()
    {
        var source = "testMethod ^true and: [false or: [true]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "and: should generate jump-on-false");
        Assert.True(ContainsJumpOnTrue(bytecodes),
            "or: should generate jump-on-true");
    }

    #endregion

    #region Block Temporaries

    [Fact]
    public void IfTrue_WithBlockTemporary_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [| temp | temp := 42. temp]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with block temp should be inlined");
        Assert.False(ContainsMessageSendFor(bytecodes, method, "ifTrue:"),
            "ifTrue: with zero-arg block (even with temps) should not generate message send");
    }

    [Fact]
    public void IfTrue_WithMultipleBlockTemporaries_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [| a b c | a := 1. b := 2. c := 3. a + b + c]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with multiple block temps should be inlined");
    }

    [Fact]
    public void IfTrueIfFalse_WithBlockTemporaries_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [| a | a := 1. a] ifFalse: [| b | b := 2. b]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue:ifFalse: with block temps should be inlined");
        Assert.True(ContainsUnconditionalForwardJump(bytecodes),
            "ifTrue:ifFalse: should have unconditional jump");
    }

    [Fact]
    public void NestedBlocksWithTemporaries_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [| a | a := true ifTrue: [| b | b := 42. b]. a]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.True(jumpCount >= 2,
            $"Nested blocks with temps should generate multiple jumps, found {jumpCount}");
    }

    #endregion

    #region Method Temporaries Interaction

    [Fact]
    public void IfTrue_WithMethodTemporary_CompilesCorrectly()
    {
        var source = "testMethod | a | a := 5. ^a > 3 ifTrue: [a * 2]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with method temp reference should be inlined");
    }

    [Fact]
    public void IfTrue_ModifyingMethodTemporary_CompilesCorrectly()
    {
        var source = "testMethod | a | a := 5. true ifTrue: [a := a + 1]. ^a";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: modifying method temp should be inlined");
    }

    [Fact]
    public void IfTrueIfFalse_ModifyingMethodTemporary_CompilesCorrectly()
    {
        var source = "testMethod | a | a := 0. true ifTrue: [a := 1] ifFalse: [a := 2]. ^a";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue:ifFalse: modifying method temp should be inlined");
    }

    #endregion

    #region Instance Variable Interaction

    [Fact]
    public void IfTrue_WithInstanceVariable_CompilesCorrectly()
    {
        var source = "testMethod ^value > 0 ifTrue: [value * 2]";
        var method = CompileMethodWithInstanceVars(source, new List<string> { "value" });
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with instance var reference should be inlined");
    }

    [Fact]
    public void IfTrue_ModifyingInstanceVariable_CompilesCorrectly()
    {
        var source = "testMethod value > 0 ifTrue: [value := value + 1]. ^value";
        var method = CompileMethodWithInstanceVars(source, new List<string> { "value" });
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: modifying instance var should be inlined");
    }

    #endregion

    #region Empty Blocks

    [Fact]
    public void IfTrue_WithEmptyBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: []";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with empty block should be inlined");
    }

    [Fact]
    public void IfFalse_WithEmptyBlock_CompilesCorrectly()
    {
        var source = "testMethod ^false ifFalse: []";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            "ifFalse: with empty block should be inlined");
    }

    [Fact]
    public void IfTrueIfFalse_WithEmptyBlocks_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [] ifFalse: []";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue:ifFalse: with empty blocks should be inlined");
    }

    [Fact]
    public void And_WithEmptyBlock_CompilesCorrectly()
    {
        var source = "testMethod ^true and: []";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "and: with empty block should be inlined");
    }

    [Fact]
    public void Or_WithEmptyBlock_CompilesCorrectly()
    {
        var source = "testMethod ^false or: []";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            "or: with empty block should be inlined");
    }

    #endregion

    #region Non-Local Returns in Blocks

    [Fact]
    public void IfTrue_WithNonLocalReturn_CompilesCorrectly()
    {
        var source = "testMethod true ifTrue: [^42]. ^0";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with non-local return should still be inlined");
    }

    [Fact]
    public void IfFalse_WithNonLocalReturn_CompilesCorrectly()
    {
        var source = "testMethod false ifFalse: [^42]. ^0";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnTrue(bytecodes),
            "ifFalse: with non-local return should still be inlined");
    }

    [Fact]
    public void IfTrueIfFalse_WithNonLocalReturns_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [^1] ifFalse: [^2]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue:ifFalse: with non-local returns should be inlined");
    }

    #endregion

    #region Complex Block Bodies

    [Fact]
    public void IfTrue_WithMultipleStatements_CompilesCorrectly()
    {
        var source = "testMethod | a | ^true ifTrue: [a := 1. a := a + 1. a := a * 2. a]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with multiple statements should be inlined");
    }

    [Fact]
    public void IfTrue_WithMessageSends_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [3 + 4 * 5]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with message sends in block should be inlined");
    }

    [Fact]
    public void IfTrue_WithCascade_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [self yourself; yourself]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with cascade in block should be inlined");
    }

    #endregion

    #region whileTrue:/whileFalse: (Not Yet Implemented)

    [Fact]
    public void WhileTrue_NotYetInlined_GeneratesMessageSend()
    {
        var source = "testMethod | i | i := 0. [i < 10] whileTrue: [i := i + 1]. ^i";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "whileTrue:"),
            "whileTrue: should generate message send (inlining not yet implemented)");
    }

    [Fact]
    public void WhileFalse_NotYetInlined_GeneratesMessageSend()
    {
        var source = "testMethod | i | i := 0. [i >= 10] whileFalse: [i := i + 1]. ^i";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "whileFalse:"),
            "whileFalse: should generate message send (inlining not yet implemented)");
    }

    [Fact]
    public void WhileTrue_NoArg_NotYetInlined_GeneratesMessageSend()
    {
        var source = "testMethod | done | done := false. [done not] whileTrue: [done := true]. ^done";
        var method = CompileMethod(source);

        Assert.True(ContainsMessageSendFor(method.Bytecodes, method, "whileTrue:"),
            "whileTrue: (no-arg form) should generate message send (inlining not yet implemented)");
    }

    #endregion

    #region Conditional Receiver Expressions

    [Fact]
    public void IfTrue_WithComparisonReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^(3 > 2) ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with comparison receiver should be inlined");
    }

    [Fact]
    public void IfTrue_WithEqualityReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^(3 = 3) ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with equality receiver should be inlined");
    }

    [Fact]
    public void IfTrue_WithMessageReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^self isEmpty ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with message send receiver should be inlined");
    }

    [Fact]
    public void IfTrue_WithAndReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^(true and: [true]) ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.True(jumpCount >= 2,
            $"ifTrue: with and: receiver should generate at least 2 jumps, found {jumpCount}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void IfTrue_WithNilReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^nil ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with nil receiver should be inlined (runtime will handle nil)");
    }

    [Fact]
    public void IfTrue_WithSelfReceiver_CompilesCorrectly()
    {
        var source = "testMethod ^self ifTrue: [42]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        Assert.True(ContainsJumpOnFalse(bytecodes),
            "ifTrue: with self receiver should be inlined (runtime will handle non-boolean)");
    }

    [Fact]
    public void IfTrueIfFalse_NestedInBothBranches_CompilesCorrectly()
    {
        var source = "testMethod ^true ifTrue: [false ifTrue: [1] ifFalse: [2]] ifFalse: [true ifTrue: [3] ifFalse: [4]]";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpOnFalseCount = CountJumpOnFalse(bytecodes);
        int unconditionalCount = CountUnconditionalJumps(bytecodes);

        Assert.True(jumpOnFalseCount >= 3,
            $"Complex nested ifTrue:ifFalse: should have at least 3 jump-on-false, found {jumpOnFalseCount}");
        Assert.True(unconditionalCount >= 3,
            $"Complex nested ifTrue:ifFalse: should have at least 3 unconditional jumps, found {unconditionalCount}");
    }

    [Fact]
    public void LongSequenceOfControlStructures_CompilesCorrectly()
    {
        var source = @"testMethod
            | a |
            a := 0.
            true ifTrue: [a := a + 1].
            false ifFalse: [a := a + 2].
            true ifTrue: [a := a + 3] ifFalse: [a := a + 4].
            ^a";
        var method = CompileMethod(source);
        var bytecodes = method.Bytecodes;

        int jumpOnFalseCount = CountJumpOnFalse(bytecodes);
        int jumpOnTrueCount = CountJumpOnTrue(bytecodes);

        Assert.True(jumpOnFalseCount >= 2,
            $"Sequence should have at least 2 jump-on-false, found {jumpOnFalseCount}");
        Assert.True(jumpOnTrueCount >= 1,
            $"Sequence should have at least 1 jump-on-true, found {jumpOnTrueCount}");
    }

    #endregion
}
