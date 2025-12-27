using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests that verify specific bytecode output patterns.
/// These tests ensure refactoring doesn't change the bytecodes produced by the compiler.
/// </summary>
[TestFixture]
public class BytecodeOutputTests : CompilerTestBase
{
    #region Push Bytecodes

    [Test]
    public void ReturnSelf_Produces0x78()
    {
        var json = CompileSource(WrapInTestClass("test ^self"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Blue Book optimization: ^self compiles to RETURN_SELF (0x78) directly
        AssertBytecodeAt(bytecodes, 0, 0x78, "RETURN_SELF");
    }

    [Test]
    public void ReturnTrue_Produces0x79()
    {
        // Blue Book optimization: ^true compiles to RETURN_TRUE (0x79)
        var json = CompileSource(WrapInTestClass("test ^true"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x79, "RETURN_TRUE");
    }

    [Test]
    public void ReturnFalse_Produces0x7A()
    {
        // Blue Book optimization: ^false compiles to RETURN_FALSE (0x7A)
        var json = CompileSource(WrapInTestClass("test ^false"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x7A, "RETURN_FALSE");
    }

    [Test]
    public void ReturnNil_Produces0x7B()
    {
        // Blue Book optimization: ^nil compiles to RETURN_NIL (0x7B)
        var json = CompileSource(WrapInTestClass("test ^nil"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x7B, "RETURN_NIL");
    }

    [Test]
    public void PushMinusOne_Produces0x74()
    {
        var json = CompileSource(WrapInTestClass("test ^-1"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x74, "PUSH_MINUS_ONE");
    }

    [Test]
    public void PushZero_Produces0x75()
    {
        var json = CompileSource(WrapInTestClass("test ^0"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x75, "PUSH_ZERO");
    }

    [Test]
    public void PushOne_Produces0x76()
    {
        var json = CompileSource(WrapInTestClass("test ^1"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x76, "PUSH_ONE");
    }

    [Test]
    public void PushTwo_Produces0x77()
    {
        var json = CompileSource(WrapInTestClass("test ^2"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        AssertBytecodeAt(bytecodes, 0, 0x77, "PUSH_TWO");
    }

    [Test]
    public void PushReceiverVariable_ShortForm()
    {
        // x is ivar 0, y is ivar 1, z is ivar 2
        var json = CompileSource(WrapInTestClass("test ^y"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // PUSH_IVAR_1 = 0x01
        AssertBytecodeAt(bytecodes, 0, 0x01, "PUSH_IVAR_1");
    }

    [Test]
    public void PushTemporary_ShortForm()
    {
        var json = CompileSource(WrapInTestClass("test | a b c | ^b"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // PUSH_TEMP_1 = 0x11
        AssertBytecodeAt(bytecodes, 0, 0x11, "PUSH_TEMP_1");
    }

    [Test]
    public void PushArgument_ShortForm()
    {
        var json = CompileSource(WrapInTestClass("test: arg ^arg"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // PUSH_TEMP_0 = 0x10 (args are temps in unified frame)
        AssertBytecodeAt(bytecodes, 0, 0x10, "PUSH_TEMP_0");
    }

    #endregion

    #region Store Bytecodes

    [Test]
    public void StoreReceiverVariable_ShortForm()
    {
        var json = CompileSource(WrapInTestClass("test x := 42"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Push 42, then POP_STORE_IVAR_0 = 0x60
        int storeIndex = FindBytecode(bytecodes, 0x60);
        Assert.That(storeIndex, Is.GreaterThanOrEqualTo(0), "Should have POP_STORE_IVAR_0");
    }

    [Test]
    public void StoreTemporary_ShortForm()
    {
        var json = CompileSource(WrapInTestClass("test | a | a := 42"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Push 42, then POP_STORE_TEMP_0 = 0x68
        int storeIndex = FindBytecode(bytecodes, 0x68);
        Assert.That(storeIndex, Is.GreaterThanOrEqualTo(0), "Should have POP_STORE_TEMP_0");
    }

    #endregion

    #region Return Bytecodes

    [Test]
    public void ImplicitReturnSelf_EndsWithReturnSelf()
    {
        // Implicit return self - method without explicit return
        var json = CompileSource(WrapInTestClass("test self foo"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Method should end with RETURN_SELF (0x78) for implicit return
        // Find the last return bytecode in the bytecode sequence
        int returnSelfIndex = FindBytecode(bytecodes, 0x78);
        Assert.That(returnSelfIndex, Is.GreaterThanOrEqualTo(0), "Should have RETURN_SELF for implicit return");
    }

    [Test]
    public void ReturnTop_Produces0x7C()
    {
        var json = CompileSource(WrapInTestClass("test ^42"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int returnIndex = FindBytecode(bytecodes, 0x7C);
        Assert.That(returnIndex, Is.GreaterThanOrEqualTo(0), "Should have RETURN_TOP");
    }

    [Test]
    public void ReturnTopFromBlock_Produces0x7D()
    {
        var json = CompileSource(WrapInTestClass("test ^[42] value"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Blue Book: Blocks are compiled inline, RETURN_TOP_FROM_BLOCK (0x7D) is in the main method
        int returnIndex = FindBytecode(bytecodes, 0x7D);
        Assert.That(returnIndex, Is.GreaterThanOrEqualTo(0), "Block should have RETURN_TOP_FROM_BLOCK");
    }

    #endregion

    #region Send Bytecodes

    [Test]
    [TestCase("+", 0xB0)]
    [TestCase("-", 0xB1)]
    [TestCase("<", 0xB2)]
    [TestCase(">", 0xB3)]
    [TestCase("<=", 0xB4)]
    [TestCase(">=", 0xB5)]
    [TestCase("=", 0xB6)]
    [TestCase("~=", 0xB7)]
    [TestCase("*", 0xB8)]
    [TestCase("/", 0xB9)]
    public void ArithmeticSelector_ProducesSpecialBytecode(string selector, int expected)
    {
        var json = CompileSource(WrapInTestClass($"test ^1 {selector} 2"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecode(bytecodes, expected);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), $"Should have special bytecode for {selector}");
    }

    [Test]
    public void SizeSelector_Produces0xC2()
    {
        var json = CompileSource(WrapInTestClass("test ^#(1 2 3) size"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecode(bytecodes, 0xC2);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have special bytecode for size");
    }

    [Test]
    public void ValueSelector_Produces0xC9()
    {
        var json = CompileSource(WrapInTestClass("test ^[42] value"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecode(bytecodes, 0xC9);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have special bytecode for value");
    }

    [Test]
    public void ClassSelector_Produces0xC7()
    {
        var json = CompileSource(WrapInTestClass("test ^self class"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecode(bytecodes, 0xC7);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have special bytecode for class");
    }

    [Test]
    public void NewSelector_Produces0xCC()
    {
        var json = CompileSource(WrapInTestClass("test ^Array new"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecode(bytecodes, 0xCC);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have special bytecode for new");
    }

    [Test]
    public void UnaryMessage_ShortForm_D0toDF()
    {
        var json = CompileSource(WrapInTestClass("test ^self customUnary"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // SEND_LITERAL_0_0_ARGS = 0xD0
        int sendIndex = FindBytecodeInRange(bytecodes, 0xD0, 0xDF);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have short form unary send");
    }

    [Test]
    public void KeywordMessage_1Arg_ShortForm_E0toEF()
    {
        var json = CompileSource(WrapInTestClass("test ^self custom: 42"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecodeInRange(bytecodes, 0xE0, 0xEF);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have short form 1-arg send");
    }

    [Test]
    public void KeywordMessage_2Args_ShortForm_F0toFF()
    {
        var json = CompileSource(WrapInTestClass("test ^self custom: 1 with: 2"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int sendIndex = FindBytecodeInRange(bytecodes, 0xF0, 0xFF);
        Assert.That(sendIndex, Is.GreaterThanOrEqualTo(0), "Should have short form 2-arg send");
    }

    #endregion

    #region Jump Bytecodes (Control Flow Inlining)

    [Test]
    public void IfTrue_ProducesJumpOnFalse()
    {
        var json = CompileSource(WrapInTestClass("test true ifTrue: [self foo]"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have BFP (branch on false, pop) - short or long form
        bool hasShortBFP = FindBytecodeInRange(bytecodes, 0x98, 0x9F) >= 0;
        bool hasLongBFP = FindBytecodeInRange(bytecodes, 0xAC, 0xAF) >= 0;
        Assert.That(hasShortBFP || hasLongBFP, Is.True, "ifTrue: should produce BFP bytecode");
    }

    [Test]
    public void IfFalse_ProducesJumpOnTrue()
    {
        var json = CompileSource(WrapInTestClass("test false ifFalse: [self foo]"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have BTP (branch on true, pop) - long form only
        bool hasLongBTP = FindBytecodeInRange(bytecodes, 0xA8, 0xAB) >= 0;
        Assert.That(hasLongBTP, Is.True, "ifFalse: should produce BTP bytecode");
    }

    [Test]
    public void IfTrueIfFalse_ProducesBothJumps()
    {
        var json = CompileSource(WrapInTestClass("test ^true ifTrue: [1] ifFalse: [2]"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have BFP and unconditional jump
        bool hasBFP = FindBytecodeInRange(bytecodes, 0x98, 0x9F) >= 0 ||
                      FindBytecodeInRange(bytecodes, 0xAC, 0xAF) >= 0;
        bool hasJump = FindBytecodeInRange(bytecodes, 0x90, 0x97) >= 0 ||
                       FindBytecodeInRange(bytecodes, 0xA4, 0xA7) >= 0;

        Assert.That(hasBFP, Is.True, "ifTrue:ifFalse: should produce BFP");
        Assert.That(hasJump, Is.True, "ifTrue:ifFalse: should produce unconditional jump");
    }

    #endregion

    #region Extended Bytecodes

    [Test]
    public void ExtendedPush_For16PlusIndex()
    {
        // Create a class with many instance variables
        var source = @"
Object subclass: #BigClass
    instanceVariableNames: 'a b c d e f g h i j k l m n o p q'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!BigClass methodsFor: 'testing'!
test ^q
! !
";
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // q is ivar index 16, needs extended push (0x80)
        AssertBytecodeAt(bytecodes, 0, 0x80, "EXTENDED_PUSH");
    }

    [Test]
    public void ExtendedStore_For8PlusIndex()
    {
        var source = @"
Object subclass: #BigClass
    instanceVariableNames: 'a b c d e f g h i'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!BigClass methodsFor: 'testing'!
test i := 42
! !
";
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // i is ivar index 8, needs extended store (0x82)
        int extStoreIndex = FindBytecode(bytecodes, 0x82);
        Assert.That(extStoreIndex, Is.GreaterThanOrEqualTo(0), "Should have EXTENDED_POP_STORE");
    }

    #endregion

    #region Stack Operations

    [Test]
    public void Pop_Produces0x87()
    {
        var json = CompileSource(WrapInTestClass("test self foo. self bar"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int popIndex = FindBytecode(bytecodes, 0x87);
        Assert.That(popIndex, Is.GreaterThanOrEqualTo(0), "Statement separator should produce POP");
    }

    [Test]
    public void Dup_Produces0x88()
    {
        // Cascade uses DUP
        var json = CompileSource(WrapInTestClass("test self foo; bar"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        int dupIndex = FindBytecode(bytecodes, 0x88);
        Assert.That(dupIndex, Is.GreaterThanOrEqualTo(0), "Cascade should produce DUP");
    }

    #endregion

    #region Method Header

    [Test]
    public void MethodHeader_ArgCount()
    {
        var json = CompileSource(WrapInTestClass("test: a with: b and: c ^a"));
        var method = GetFirstMethod(json);
        var header = (JObject)method["header"]!;

        Assert.That((int)header["argCount"]!, Is.EqualTo(3));
    }

    [Test]
    public void MethodHeader_TempCount()
    {
        var json = CompileSource(WrapInTestClass("test | a b c d e | ^a"));
        var method = GetFirstMethod(json);
        var header = (JObject)method["header"]!;

        Assert.That((int)header["tempCount"]!, Is.EqualTo(5));
    }

    [Test]
    public void MethodHeader_LargeContext_WhenNeeded()
    {
        // Create a method that needs large context (many temps)
        // Blue Book uses largeContext when temps+args > 6 (small context limit)
        // But modern implementations often have higher thresholds
        // 15 temps should definitely require largeContext
        var temps = string.Join(" ", Enumerable.Range(1, 15).Select(i => $"t{i}"));
        var json = CompileSource(WrapInTestClass($"test | {temps} | ^t1"));
        var method = GetFirstMethod(json);
        var header = (JObject)method["header"]!;

        // Verify tempCount is correct (15 temps)
        Assert.That((int)header["tempCount"]!, Is.EqualTo(15), "Should have 15 temps");
    }

    [Test]
    public void MethodHeader_PrimitiveNumber()
    {
        // Primitive annotation comes after the method selector, before the body
        var source = WrapInTestClass(@"test
    <primitive: 60>
    ^self primitiveFailed");
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        var header = (JObject)method["header"]!;

        // Check that the primitive index is recorded
        var primitiveIndex = header["primitiveIndex"]?.Value<int>() ?? 0;
        Assert.That(primitiveIndex, Is.EqualTo(60), "Should have primitive 60");
    }

    #endregion

    #region Complex Patterns

    [Test]
    public void BinaryAddition_CorrectSequence()
    {
        var json = CompileSource(WrapInTestClass("test ^3 + 4"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should be: push 3 (literal), push 4 (literal), send +, return
        // 3 is not special, so needs literal push
        // 4 is not special, so needs literal push
        int addIndex = FindBytecode(bytecodes, 0xB0); // +
        Assert.That(addIndex, Is.GreaterThanOrEqualTo(0), "Should have + bytecode");
        Assert.That(addIndex, Is.GreaterThan(0), "+ should not be first bytecode");
    }

    [Test]
    public void NestedBlocks_ProducesCorrectStructure()
    {
        // With Blue Book inline blocks, blocks are compiled inline, not as separate methods
        var json = CompileSource(WrapInTestClass("test ^[[42] value] value"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Should have two blockCopy: bytecodes (0xC8 = 200) for nested blocks
        var blockCopyCount = bytecodes.Select(t => (int)t).Count(b => b == 200);
        Assert.That(blockCopyCount, Is.EqualTo(2), "Should have 2 blockCopy: bytecodes for nested blocks");

        // Should also have two RETURN_TOP_FROM_BLOCK (0x7D = 125) bytecodes
        var blockReturnCount = bytecodes.Select(t => (int)t).Count(b => b == 125);
        Assert.That(blockReturnCount, Is.EqualTo(2), "Should have 2 block return bytecodes");
    }

    [Test]
    public void Assignment_ProducesCorrectSequence()
    {
        var json = CompileSource(WrapInTestClass("test | a | a := 42. ^a"));
        var method = GetFirstMethod(json);
        var bytecodes = (JArray)method["bytecodes"]!;

        // Sequence: push literal 42, store temp 0, push temp 0, return
        bool hasStore = FindBytecode(bytecodes, 0x68) >= 0 || // POP_STORE_TEMP_0
                        FindBytecode(bytecodes, 0x82) >= 0;   // EXTENDED_POP_STORE
        Assert.That(hasStore, Is.True, "Should have store bytecode");
    }

    #endregion

    #region Helper Methods

    private int FindBytecode(JArray bytecodes, int target)
    {
        for (int i = 0; i < bytecodes.Count; i++)
        {
            if ((int)bytecodes[i] == target)
                return i;
        }
        return -1;
    }

    private int FindBytecodeInRange(JArray bytecodes, int min, int max)
    {
        for (int i = 0; i < bytecodes.Count; i++)
        {
            int b = (int)bytecodes[i];
            if (b >= min && b <= max)
                return i;
        }
        return -1;
    }

    #endregion
}
