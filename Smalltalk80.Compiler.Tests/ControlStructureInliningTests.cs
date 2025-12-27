using Newtonsoft.Json.Linq;
using NUnit.Framework;

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
[TestFixture]
[Category("Compiler")]
public class ControlStructureInliningTests : CompilerTestBase
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
    private const byte SHORT_JUMP_ON_FALSE_BASE = 0x98;  // Blue Book: 0x98-0x9F for BFP offsets 1-8
    private const byte SHORT_JUMP_ON_FALSE_MAX = 0x9F;

    // Push constants (Blue Book correct values)
    private const byte PUSH_SELF = 0x70;
    private const byte PUSH_TRUE = 0x71;
    private const byte PUSH_FALSE = 0x72;
    private const byte PUSH_NIL = 0x73;

    // Return bytecodes
    private const byte RETURN_SELF = 0x78;
    private const byte RETURN_TOP = 0x7C;

    #endregion

    #region Helper Methods

    private static bool ContainsJumpOnFalse(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b =>
            (b >= LONG_JUMP_ON_FALSE_BASE && b <= LONG_JUMP_ON_FALSE_MAX) ||
            (b >= SHORT_JUMP_ON_FALSE_BASE && b <= SHORT_JUMP_ON_FALSE_MAX));
    }

    private static bool ContainsJumpOnTrue(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b => b >= LONG_JUMP_ON_TRUE_BASE && b <= LONG_JUMP_ON_TRUE_MAX);
    }

    private static bool ContainsUnconditionalForwardJump(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b =>
            (b >= LONG_UNCONDITIONAL_FORWARD_BASE && b <= LONG_UNCONDITIONAL_FORWARD_MAX) ||
            (b >= SHORT_UNCONDITIONAL_JUMP_BASE && b <= SHORT_UNCONDITIONAL_JUMP_MAX));
    }

    private bool ContainsMessageSendFor(JObject json, JObject method, string selector)
    {
        return LiteralContainsSymbol(json, method, selector);
    }

    private static bool ContainsPushNil(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b => b == PUSH_NIL);
    }

    private static bool ContainsPushTrue(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b => b == PUSH_TRUE);
    }

    private static bool ContainsPushFalse(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Any(b => b == PUSH_FALSE);
    }

    private static int CountJumpOnFalse(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Count(b =>
            (b >= LONG_JUMP_ON_FALSE_BASE && b <= LONG_JUMP_ON_FALSE_MAX) ||
            (b >= SHORT_JUMP_ON_FALSE_BASE && b <= SHORT_JUMP_ON_FALSE_MAX));
    }

    private static int CountJumpOnTrue(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Count(b => b >= LONG_JUMP_ON_TRUE_BASE && b <= LONG_JUMP_ON_TRUE_MAX);
    }

    private static int CountUnconditionalJumps(JArray bytecodes)
    {
        return bytecodes.Select(t => (int)t).Count(b =>
            (b >= LONG_UNCONDITIONAL_FORWARD_BASE && b <= LONG_UNCONDITIONAL_FORWARD_MAX) ||
            (b >= SHORT_UNCONDITIONAL_JUMP_BASE && b <= SHORT_UNCONDITIONAL_JUMP_MAX));
    }

    private static string DisassembleBytecodes(JArray bytecodes)
    {
        var result = new System.Text.StringBuilder();
        int i = 0;
        foreach (var b in bytecodes)
        {
            result.AppendLine($"  {i:D3}: 0x{(int)b:X2}");
            i++;
        }
        return result.ToString();
    }

    private JObject CompileTestMethod(string methodBody)
    {
        var source = WrapInTestClass(methodBody);
        var json = CompileSource(source);
        return GetFirstMethod(json);
    }

    private (JObject json, JObject method) CompileTestMethodWithJson(string methodBody)
    {
        var source = WrapInTestClass(methodBody);
        var json = CompileSource(source);
        return (json, GetFirstMethod(json));
    }

    private JArray GetBytecodes(JObject method)
    {
        return (JArray)method["bytecodes"]!;
    }

    #endregion

    #region ifTrue: Basic Tests

    [Test]
    public void IfTrue_WithZeroArgBlock_GeneratesJumpOnFalse()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            $"ifTrue: with zero-arg block should generate jump-on-false bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:"), Is.False,
            "ifTrue: with zero-arg block should not generate ifTrue: message send");
    }

    [Test]
    public void IfTrue_PushesNilForFalseBranch()
    {
        var method = CompileTestMethod("testMethod ^false ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsPushNil(bytecodes), Is.True,
            $"ifTrue: should push nil for false branch\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Test]
    public void IfTrue_WithSelfInBlock_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [self]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with self in block should be inlined");
    }

    #endregion

    #region ifTrue: Block Argument Variations

    [Test]
    public void IfTrue_WithOneArgBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [:x | x + 1]");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:"), Is.True,
            "ifTrue: with one-arg block should generate ifTrue: message send");
    }

    [Test]
    public void IfTrue_WithNonBlockArgument_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: 42");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:"), Is.True,
            "ifTrue: with non-block argument should generate message send");
    }

    [Test]
    public void IfTrue_WithVariableAsArgument_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod | block | block := [42]. ^true ifTrue: block");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:"), Is.True,
            "ifTrue: with variable as argument should generate message send");
    }

    #endregion

    #region ifFalse: Basic Tests

    [Test]
    public void IfFalse_WithZeroArgBlock_GeneratesJumpOnTrue()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false ifFalse: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            $"ifFalse: with zero-arg block should generate jump-on-true bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsMessageSendFor(json, method, "ifFalse:"), Is.False,
            "ifFalse: with zero-arg block should not generate ifFalse: message send");
    }

    [Test]
    public void IfFalse_PushesNilForTrueBranch()
    {
        var method = CompileTestMethod("testMethod ^true ifFalse: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsPushNil(bytecodes), Is.True,
            $"ifFalse: should push nil when condition is true\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Test]
    public void IfFalse_WithOneArgBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false ifFalse: [:x | x + 1]");

        Assert.That(ContainsMessageSendFor(json, method, "ifFalse:"), Is.True,
            "ifFalse: with one-arg block should generate ifFalse: message send");
    }

    #endregion

    #region ifTrue:ifFalse: Basic Tests

    [Test]
    public void IfTrueIfFalse_WithZeroArgBlocks_GeneratesJumps()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [1] ifFalse: [2]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            $"ifTrue:ifFalse: should generate jump-on-false bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsUnconditionalForwardJump(bytecodes), Is.True,
            "ifTrue:ifFalse: should generate unconditional jump to skip false branch");
        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:ifFalse:"), Is.False,
            "ifTrue:ifFalse: with zero-arg blocks should not generate message send");
    }

    [Test]
    public void IfTrueIfFalse_WithTrueArgNonBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: 1 ifFalse: [2]");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:ifFalse:"), Is.True,
            "ifTrue:ifFalse: with non-block first arg should generate message send");
    }

    [Test]
    public void IfTrueIfFalse_WithFalseArgNonBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [1] ifFalse: 2");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:ifFalse:"), Is.True,
            "ifTrue:ifFalse: with non-block second arg should generate message send");
    }

    [Test]
    public void IfTrueIfFalse_WithTrueBlockHavingArgs_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [:x | x] ifFalse: [2]");

        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:ifFalse:"), Is.True,
            "ifTrue:ifFalse: with true block having args should generate message send");
    }

    #endregion

    #region ifFalse:ifTrue: Basic Tests

    [Test]
    public void IfFalseIfTrue_WithZeroArgBlocks_GeneratesJumps()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false ifFalse: [1] ifTrue: [2]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            $"ifFalse:ifTrue: should generate jump-on-true bytecode\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsUnconditionalForwardJump(bytecodes), Is.True,
            "ifFalse:ifTrue: should generate unconditional jump");
        Assert.That(ContainsMessageSendFor(json, method, "ifFalse:ifTrue:"), Is.False,
            "ifFalse:ifTrue: with zero-arg blocks should not generate message send");
    }

    [Test]
    public void IfFalseIfTrue_WithNonBlockArgs_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false ifFalse: 1 ifTrue: 2");

        Assert.That(ContainsMessageSendFor(json, method, "ifFalse:ifTrue:"), Is.True,
            "ifFalse:ifTrue: with non-block args should generate message send");
    }

    #endregion

    #region and: Tests

    [Test]
    public void And_WithZeroArgBlock_GeneratesJumpOnFalse()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true and: [false]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            $"and: should generate jump-on-false for short-circuit evaluation\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsMessageSendFor(json, method, "and:"), Is.False,
            "and: with zero-arg block should not generate and: message send");
    }

    [Test]
    public void And_WithBlockReturningTrue_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true and: [true]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "and: with [true] should be inlined");
        Assert.That(ContainsPushTrue(bytecodes), Is.True,
            "and: with [true] should push true in block body");
    }

    [Test]
    public void And_WithBlockReturningFalse_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true and: [false]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "and: with [false] should be inlined");
        Assert.That(ContainsPushFalse(bytecodes), Is.True,
            "and: with [false] should push false in block body");
    }

    [Test]
    public void And_WithNonBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true and: false");

        Assert.That(ContainsMessageSendFor(json, method, "and:"), Is.True,
            "and: with non-block argument should generate and: message send");
    }

    [Test]
    public void And_WithOneArgBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true and: [:x | x]");

        Assert.That(ContainsMessageSendFor(json, method, "and:"), Is.True,
            "and: with one-arg block should generate and: message send");
    }

    #endregion

    #region or: Tests

    [Test]
    public void Or_WithZeroArgBlock_GeneratesJumpOnTrue()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false or: [true]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            $"or: should generate jump-on-true for short-circuit evaluation\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
        Assert.That(ContainsMessageSendFor(json, method, "or:"), Is.False,
            "or: with zero-arg block should not generate or: message send");
    }

    [Test]
    public void Or_WithBlockReturningTrue_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^false or: [true]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "or: with [true] should be inlined");
    }

    [Test]
    public void Or_WithNonBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false or: true");

        Assert.That(ContainsMessageSendFor(json, method, "or:"), Is.True,
            "or: with non-block argument should generate or: message send");
    }

    [Test]
    public void Or_WithOneArgBlock_FallsBackToMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^false or: [:x | x]");

        Assert.That(ContainsMessageSendFor(json, method, "or:"), Is.True,
            "or: with one-arg block should generate or: message send");
    }

    #endregion

    #region Nested Control Structures

    [Test]
    public void NestedIfTrue_GeneratesMultipleJumps()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [false ifTrue: [42]]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"Nested ifTrue: should generate at least 2 jump-on-false bytecodes, found {jumpCount}\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Test]
    public void NestedIfFalse_GeneratesMultipleJumps()
    {
        var method = CompileTestMethod("testMethod ^false ifFalse: [true ifFalse: [42]]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnTrue(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"Nested ifFalse: should generate at least 2 jump-on-true bytecodes, found {jumpCount}\nBytecodes:\n{DisassembleBytecodes(bytecodes)}");
    }

    [Test]
    public void MixedNestedControlStructures_CompileCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [false ifFalse: [42]]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "Outer ifTrue: should generate jump-on-false");
        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "Inner ifFalse: should generate jump-on-true");
    }

    [Test]
    public void DeeplyNestedIfTrue_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [true ifTrue: [true ifTrue: [42]]]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(3),
            $"Deeply nested ifTrue: should generate at least 3 jump-on-false bytecodes, found {jumpCount}");
    }

    [Test]
    public void AndChain_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true and: [true and: [false]]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"Chained and: should generate at least 2 jump-on-false bytecodes, found {jumpCount}");
    }

    [Test]
    public void OrChain_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^false or: [false or: [true]]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnTrue(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"Chained or: should generate at least 2 jump-on-true bytecodes, found {jumpCount}");
    }

    [Test]
    public void MixedAndOr_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true and: [false or: [true]]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "and: should generate jump-on-false");
        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "or: should generate jump-on-true");
    }

    #endregion

    #region Block Temporaries

    [Test]
    public void IfTrue_WithBlockTemporary_CompilesCorrectly()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod ^true ifTrue: [| temp | temp := 42. temp]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with block temp should be inlined");
        Assert.That(ContainsMessageSendFor(json, method, "ifTrue:"), Is.False,
            "ifTrue: with zero-arg block (even with temps) should not generate message send");
    }

    [Test]
    public void IfTrue_WithMultipleBlockTemporaries_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [| a b c | a := 1. b := 2. c := 3. a + b + c]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with multiple block temps should be inlined");
    }

    [Test]
    public void IfTrueIfFalse_WithBlockTemporaries_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [| a | a := 1. a] ifFalse: [| b | b := 2. b]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue:ifFalse: with block temps should be inlined");
        Assert.That(ContainsUnconditionalForwardJump(bytecodes), Is.True,
            "ifTrue:ifFalse: should have unconditional jump");
    }

    [Test]
    public void NestedBlocksWithTemporaries_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [| a | a := true ifTrue: [| b | b := 42. b]. a]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"Nested blocks with temps should generate multiple jumps, found {jumpCount}");
    }

    #endregion

    #region Method Temporaries Interaction

    [Test]
    public void IfTrue_WithMethodTemporary_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod | a | a := 5. ^a > 3 ifTrue: [a * 2]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with method temp reference should be inlined");
    }

    [Test]
    public void IfTrue_ModifyingMethodTemporary_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod | a | a := 5. true ifTrue: [a := a + 1]. ^a");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: modifying method temp should be inlined");
    }

    [Test]
    public void IfTrueIfFalse_ModifyingMethodTemporary_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod | a | a := 0. true ifTrue: [a := 1] ifFalse: [a := 2]. ^a");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue:ifFalse: modifying method temp should be inlined");
    }

    #endregion

    #region Empty Blocks

    [Test]
    public void IfTrue_WithEmptyBlock_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: []");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with empty block should be inlined");
    }

    [Test]
    public void IfFalse_WithEmptyBlock_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^false ifFalse: []");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "ifFalse: with empty block should be inlined");
    }

    [Test]
    public void IfTrueIfFalse_WithEmptyBlocks_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [] ifFalse: []");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue:ifFalse: with empty blocks should be inlined");
    }

    [Test]
    public void And_WithEmptyBlock_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true and: []");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "and: with empty block should be inlined");
    }

    [Test]
    public void Or_WithEmptyBlock_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^false or: []");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "or: with empty block should be inlined");
    }

    #endregion

    #region Non-Local Returns in Blocks

    [Test]
    public void IfTrue_WithNonLocalReturn_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod true ifTrue: [^42]. ^0");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with non-local return should still be inlined");
    }

    [Test]
    public void IfFalse_WithNonLocalReturn_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod false ifFalse: [^42]. ^0");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnTrue(bytecodes), Is.True,
            "ifFalse: with non-local return should still be inlined");
    }

    [Test]
    public void IfTrueIfFalse_WithNonLocalReturns_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [^1] ifFalse: [^2]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue:ifFalse: with non-local returns should be inlined");
    }

    #endregion

    #region Complex Block Bodies

    [Test]
    public void IfTrue_WithMultipleStatements_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod | a | ^true ifTrue: [a := 1. a := a + 1. a := a * 2. a]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with multiple statements should be inlined");
    }

    [Test]
    public void IfTrue_WithMessageSends_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [3 + 4 * 5]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with message sends in block should be inlined");
    }

    [Test]
    public void IfTrue_WithCascade_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [self yourself; yourself]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with cascade in block should be inlined");
    }

    #endregion

    #region whileTrue:/whileFalse: (Not Yet Implemented)

    [Test]
    public void WhileTrue_NotYetInlined_GeneratesMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod | i | i := 0. [i < 10] whileTrue: [i := i + 1]. ^i");

        Assert.That(ContainsMessageSendFor(json, method, "whileTrue:"), Is.True,
            "whileTrue: should generate message send (inlining not yet implemented)");
    }

    [Test]
    public void WhileFalse_NotYetInlined_GeneratesMessageSend()
    {
        var (json, method) = CompileTestMethodWithJson("testMethod | i | i := 0. [i >= 10] whileFalse: [i := i + 1]. ^i");

        Assert.That(ContainsMessageSendFor(json, method, "whileFalse:"), Is.True,
            "whileFalse: should generate message send (inlining not yet implemented)");
    }

    #endregion

    #region Conditional Receiver Expressions

    [Test]
    public void IfTrue_WithComparisonReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^(3 > 2) ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with comparison receiver should be inlined");
    }

    [Test]
    public void IfTrue_WithEqualityReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^(3 = 3) ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with equality receiver should be inlined");
    }

    [Test]
    public void IfTrue_WithMessageReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^self isEmpty ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with message send receiver should be inlined");
    }

    [Test]
    public void IfTrue_WithAndReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^(true and: [true]) ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        int jumpCount = CountJumpOnFalse(bytecodes);
        Assert.That(jumpCount, Is.GreaterThanOrEqualTo(2),
            $"ifTrue: with and: receiver should generate at least 2 jumps, found {jumpCount}");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void IfTrue_WithNilReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^nil ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with nil receiver should be inlined (runtime will handle nil)");
    }

    [Test]
    public void IfTrue_WithSelfReceiver_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^self ifTrue: [42]");
        var bytecodes = GetBytecodes(method);

        Assert.That(ContainsJumpOnFalse(bytecodes), Is.True,
            "ifTrue: with self receiver should be inlined (runtime will handle non-boolean)");
    }

    [Test]
    public void IfTrueIfFalse_NestedInBothBranches_CompilesCorrectly()
    {
        var method = CompileTestMethod("testMethod ^true ifTrue: [false ifTrue: [1] ifFalse: [2]] ifFalse: [true ifTrue: [3] ifFalse: [4]]");
        var bytecodes = GetBytecodes(method);

        int jumpOnFalseCount = CountJumpOnFalse(bytecodes);
        int unconditionalCount = CountUnconditionalJumps(bytecodes);

        Assert.That(jumpOnFalseCount, Is.GreaterThanOrEqualTo(3),
            $"Complex nested ifTrue:ifFalse: should have at least 3 jump-on-false, found {jumpOnFalseCount}");
        Assert.That(unconditionalCount, Is.GreaterThanOrEqualTo(3),
            $"Complex nested ifTrue:ifFalse: should have at least 3 unconditional jumps, found {unconditionalCount}");
    }

    [Test]
    public void LongSequenceOfControlStructures_CompilesCorrectly()
    {
        var method = CompileTestMethod(@"testMethod
            | a |
            a := 0.
            true ifTrue: [a := a + 1].
            false ifFalse: [a := a + 2].
            true ifTrue: [a := a + 3] ifFalse: [a := a + 4].
            ^a");
        var bytecodes = GetBytecodes(method);

        int jumpOnFalseCount = CountJumpOnFalse(bytecodes);
        int jumpOnTrueCount = CountJumpOnTrue(bytecodes);

        Assert.That(jumpOnFalseCount, Is.GreaterThanOrEqualTo(2),
            $"Sequence should have at least 2 jump-on-false, found {jumpOnFalseCount}");
        Assert.That(jumpOnTrueCount, Is.GreaterThanOrEqualTo(1),
            $"Sequence should have at least 1 jump-on-true, found {jumpOnTrueCount}");
    }

    #endregion
}
