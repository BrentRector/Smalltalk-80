using NUnit.Framework;
using Smalltalk80.Compiler;
using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Unit tests for the BytecodeEmitter class.
/// Tests all bytecode generation to ensure refactoring doesn't break bytecode output.
/// </summary>
[TestFixture]
public class BytecodeEmitterTests
{
    private BytecodeEmitter _emitter = null!;

    [SetUp]
    public void Setup()
    {
        _emitter = new BytecodeEmitter();
    }

    #region Push Receiver Variable (0x00-0x0F, Extended 0x80)

    [Test]
    [TestCase(0, 0x00)]
    [TestCase(1, 0x01)]
    [TestCase(7, 0x07)]
    [TestCase(15, 0x0F)]
    public void PushReceiverVariable_ShortForm_EmitsCorrectBytecode(int index, int expected)
    {
        _emitter.PushReceiverVariable(index);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(63)]
    public void PushReceiverVariable_ExtendedForm_EmitsCorrectBytecode(int index)
    {
        _emitter.PushReceiverVariable(index);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x80), "Should emit EXTENDED_PUSH");
        // Type 0 = receiver variable (bits 6-7), index in lower 6 bits
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(0), "Type should be 0 (receiver var)");
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(index), $"Index should be {index}");
    }

    #endregion

    #region Push Temporary Variable (0x10-0x1F, Extended 0x80)

    [Test]
    [TestCase(0, 0x10)]
    [TestCase(1, 0x11)]
    [TestCase(7, 0x17)]
    [TestCase(15, 0x1F)]
    public void PushTemporary_ShortForm_EmitsCorrectBytecode(int index, int expected)
    {
        _emitter.PushTemporary(index);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(63)]
    public void PushTemporary_ExtendedForm_EmitsCorrectBytecode(int index)
    {
        _emitter.PushTemporary(index);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x80), "Should emit EXTENDED_PUSH");
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(1), "Type should be 1 (temp var)");
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(index), $"Index should be {index}");
    }

    #endregion

    #region Push Literal Constant (0x20-0x3F, Extended 0x80)

    [Test]
    [TestCase(0, 0x20)]
    [TestCase(1, 0x21)]
    [TestCase(15, 0x2F)]
    [TestCase(31, 0x3F)]
    public void PushLiteralConstant_ShortForm_EmitsCorrectBytecode(int index, int expected)
    {
        // Add literals up to the desired index
        for (int i = 0; i <= index; i++)
        {
            _emitter.AddLiteral($"literal{i}");
        }

        var emitter2 = new BytecodeEmitter();
        for (int i = 0; i <= index; i++)
        {
            emitter2.AddLiteral($"literal{i}");
        }
        emitter2.PushLiteralConstant($"literal{index}");

        var bytecodes = emitter2.GetBytecodes();
        Assert.That(bytecodes[0], Is.EqualTo(expected));
        Assert.That(bytecodes.Length, Is.EqualTo(1));
    }

    [Test]
    public void PushLiteralConstant_ExtendedForm_For32PlusIndex()
    {
        // Add 33 literals to force extended form
        for (int i = 0; i < 33; i++)
        {
            _emitter.AddLiteral($"literal{i}");
        }
        _emitter.PushLiteralConstant("literal32");

        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x80), "Should emit EXTENDED_PUSH");
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(2), "Type should be 2 (literal constant)");
    }

    #endregion

    #region Push Literal Variable (0x40-0x5F, Extended 0x80)

    [Test]
    public void PushLiteralVariable_ShortForm_EmitsCorrectBytecode()
    {
        _emitter.PushLiteralVariable("Transcript");
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0x40));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0x5F));
    }

    [Test]
    public void PushLiteralVariable_WithPoolDictionary_EmitsCorrectBytecode()
    {
        _emitter.PushLiteralVariable("CR", "TextConstants");
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.GreaterThanOrEqualTo(1));
    }

    #endregion

    #region Push Special Constants (0x70-0x77)

    [Test]
    public void PushSelf_EmitsCorrectBytecode()
    {
        _emitter.PushSelf();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x70));
    }

    [Test]
    public void PushTrue_EmitsCorrectBytecode()
    {
        _emitter.PushTrue();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x71));
    }

    [Test]
    public void PushFalse_EmitsCorrectBytecode()
    {
        _emitter.PushFalse();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x72));
    }

    [Test]
    public void PushNil_EmitsCorrectBytecode()
    {
        _emitter.PushNil();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x73));
    }

    [Test]
    public void PushMinusOne_EmitsCorrectBytecode()
    {
        _emitter.PushMinusOne();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x74));
    }

    [Test]
    public void PushZero_EmitsCorrectBytecode()
    {
        _emitter.PushZero();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x75));
    }

    [Test]
    public void PushOne_EmitsCorrectBytecode()
    {
        _emitter.PushOne();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x76));
    }

    [Test]
    public void PushTwo_EmitsCorrectBytecode()
    {
        _emitter.PushTwo();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x77));
    }

    [Test]
    public void PushActiveContext_EmitsCorrectBytecode()
    {
        _emitter.PushActiveContext();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x89));
    }

    #endregion

    #region Push SmallInteger (uses special or literal)

    [Test]
    [TestCase(-1, 0x74)]
    [TestCase(0, 0x75)]
    [TestCase(1, 0x76)]
    [TestCase(2, 0x77)]
    public void PushSmallInteger_SpecialValues_UsesSpecialBytecode(int value, int expected)
    {
        _emitter.PushSmallInteger(value);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
    }

    [Test]
    [TestCase(3)]
    [TestCase(10)]
    [TestCase(100)]
    public void PushSmallInteger_OtherValues_UsesLiteralConstant(int value)
    {
        _emitter.PushSmallInteger(value);
        var bytecodes = _emitter.GetBytecodes();

        // Should use PUSH_LITERAL_CONST bytecode (0x20-0x3F or extended)
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0x20));
    }

    #endregion

    #region Store and Pop Receiver Variable (0x60-0x67, Extended 0x82)

    [Test]
    [TestCase(0, 0x60)]
    [TestCase(1, 0x61)]
    [TestCase(7, 0x67)]
    public void StoreReceiverVariable_ShortForm_EmitsCorrectBytecode(int index, int expected)
    {
        _emitter.StoreReceiverVariable(index);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    [TestCase(8)]
    [TestCase(15)]
    [TestCase(32)]
    public void StoreReceiverVariable_ExtendedForm_EmitsCorrectBytecode(int index)
    {
        _emitter.StoreReceiverVariable(index);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x82), "Should emit EXTENDED_POP_STORE");
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(0), "Type should be 0 (receiver var)");
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(index), $"Index should be {index}");
    }

    #endregion

    #region Store and Pop Temporary Variable (0x68-0x6F, Extended 0x82)

    [Test]
    [TestCase(0, 0x68)]
    [TestCase(1, 0x69)]
    [TestCase(7, 0x6F)]
    public void StoreTemporary_ShortForm_EmitsCorrectBytecode(int index, int expected)
    {
        _emitter.StoreTemporary(index);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    [TestCase(8)]
    [TestCase(15)]
    [TestCase(32)]
    public void StoreTemporary_ExtendedForm_EmitsCorrectBytecode(int index)
    {
        _emitter.StoreTemporary(index);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x82), "Should emit EXTENDED_POP_STORE");
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(1), "Type should be 1 (temp var)");
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(index), $"Index should be {index}");
    }

    #endregion

    #region Store Literal Variable (Extended 0x82)

    [Test]
    public void StoreLiteralVariable_EmitsExtendedStore()
    {
        _emitter.StoreLiteralVariable("Transcript");
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x82), "Should emit EXTENDED_POP_STORE");
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(3), "Type should be 3 (literal var)");
    }

    #endregion

    #region Stack Operations (0x87 Pop, 0x88 Dup)

    [Test]
    public void Pop_EmitsCorrectBytecode()
    {
        _emitter.Pop();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x87));
    }

    [Test]
    public void Dup_EmitsCorrectBytecode()
    {
        _emitter.Dup();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x88));
    }

    #endregion

    #region Return Bytecodes (0x78-0x7D)

    [Test]
    public void ReturnSelf_EmitsCorrectBytecode()
    {
        _emitter.ReturnSelf();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x78));
    }

    [Test]
    public void ReturnTrue_EmitsCorrectBytecode()
    {
        _emitter.ReturnTrue();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x79));
    }

    [Test]
    public void ReturnFalse_EmitsCorrectBytecode()
    {
        _emitter.ReturnFalse();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x7A));
    }

    [Test]
    public void ReturnNil_EmitsCorrectBytecode()
    {
        _emitter.ReturnNil();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x7B));
    }

    [Test]
    public void ReturnTop_EmitsCorrectBytecode()
    {
        _emitter.ReturnTop();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x7C));
    }

    [Test]
    public void ReturnTopFromBlock_EmitsCorrectBytecode()
    {
        _emitter.ReturnTopFromBlock();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(0x7D));
    }

    #endregion

    #region Send Arithmetic Selectors (0xB0-0xBF)

    [Test]
    [TestCase("+", 176)]
    [TestCase("-", 177)]
    [TestCase("<", 178)]
    [TestCase(">", 179)]
    [TestCase("<=", 180)]
    [TestCase(">=", 181)]
    [TestCase("=", 182)]
    [TestCase("~=", 183)]
    [TestCase("*", 184)]
    [TestCase("/", 185)]
    [TestCase("\\\\", 186)]
    [TestCase("@", 187)]
    [TestCase("bitShift:", 188)]
    [TestCase("//", 189)]
    [TestCase("bitAnd:", 190)]
    [TestCase("bitOr:", 191)]
    public void EmitSend_ArithmeticSelector_EmitsSpecialBytecode(string selector, int expected)
    {
        int argCount = selector.EndsWith(":") ? 1 : 1; // All arithmetic are binary
        _emitter.EmitSend(selector, argCount);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    #endregion

    #region Send Special Selectors (0xC0-0xCF)

    [Test]
    [TestCase("at:", 192, 1)]
    [TestCase("at:put:", 193, 2)]
    [TestCase("size", 194, 0)]
    [TestCase("next", 195, 0)]
    [TestCase("nextPut:", 196, 1)]
    [TestCase("atEnd", 197, 0)]
    [TestCase("==", 198, 1)]
    [TestCase("class", 199, 0)]
    [TestCase("blockCopy:", 200, 1)]
    [TestCase("value", 201, 0)]
    [TestCase("value:", 202, 1)]
    [TestCase("do:", 203, 1)]
    [TestCase("new", 204, 0)]
    [TestCase("new:", 205, 1)]
    [TestCase("x", 206, 0)]
    [TestCase("y", 207, 0)]
    public void EmitSend_SpecialSelector_EmitsSpecialBytecode(string selector, int expected, int argCount)
    {
        _emitter.EmitSend(selector, argCount);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expected));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    #endregion

    #region Send Regular Selectors (0xD0-0xFF, 0x83, 0x84)

    [Test]
    public void EmitSend_0Args_LiteralIndex0To15_UsesShortForm()
    {
        _emitter.EmitSend("customUnary", 0);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xD0));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xDF));
    }

    [Test]
    public void EmitSend_1Arg_LiteralIndex0To15_UsesShortForm()
    {
        _emitter.EmitSend("customKeyword:", 1);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xE0));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xEF));
    }

    [Test]
    public void EmitSend_2Args_LiteralIndex0To15_UsesShortForm()
    {
        _emitter.EmitSend("at:put:", 2); // Not special - uses literal form
        // at:put: is special (193), so use different selector
        var emitter2 = new BytecodeEmitter();
        emitter2.EmitSend("custom:with:", 2);
        var bytecodes = emitter2.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xF0));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xFF));
    }

    [Test]
    public void EmitSend_3Args_UsesSingleExtendedSend()
    {
        _emitter.EmitSend("a:b:c:", 3);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x83), "Should use SINGLE_EXTENDED_SEND");
    }

    [Test]
    public void EmitSend_ManyArgs_UsesDoubleExtendedSend()
    {
        _emitter.EmitSend("a:b:c:d:e:f:g:h:", 8);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(3));
        Assert.That(bytecodes[0], Is.EqualTo(0x84), "Should use DOUBLE_EXTENDED_SEND");
        Assert.That(bytecodes[1], Is.EqualTo(8), "Arg count should be 8");
    }

    #endregion

    #region Super Send (0x85, 0x86)

    [Test]
    public void EmitSuperSend_SmallArgsAndIndex_UsesSingleExtended()
    {
        _emitter.EmitSuperSend("initialize", 0);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0x85), "Should use SINGLE_EXTENDED_SUPER");
    }

    [Test]
    public void EmitSuperSend_LargeArgsOrIndex_UsesDoubleExtended()
    {
        // Add many literals to force large index
        for (int i = 0; i < 32; i++)
        {
            _emitter.AddLiteral(new Symbol($"selector{i}"));
        }
        _emitter.EmitSuperSend("superMethod", 0);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(3));
        Assert.That(bytecodes[0], Is.EqualTo(0x86), "Should use DOUBLE_EXTENDED_SUPER");
    }

    #endregion

    #region Short Unconditional Jump (0x90-0x97)

    [Test]
    [TestCase(1, 0x90)]
    [TestCase(2, 0x91)]
    [TestCase(8, 0x97)]
    public void EmitShortForwardJump_ValidOffset_EmitsCorrectBytecode(int offset, int expected)
    {
        _emitter.EmitShortForwardJump(offset);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.EqualTo(expected));
    }

    [Test]
    public void EmitShortForwardJump_InvalidOffset_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _emitter.EmitShortForwardJump(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _emitter.EmitShortForwardJump(9));
    }

    #endregion

    #region Short Jump on False (0x98-0x9F)

    [Test]
    [TestCase(1, 0x98)]
    [TestCase(2, 0x99)]
    [TestCase(8, 0x9F)]
    public void EmitShortJumpOnFalse_ValidOffset_EmitsCorrectBytecode(int offset, int expected)
    {
        _emitter.EmitShortJumpOnFalse(offset);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(1));
        Assert.That(bytecodes[0], Is.EqualTo(expected));
    }

    [Test]
    public void EmitShortJumpOnFalse_InvalidOffset_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _emitter.EmitShortJumpOnFalse(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _emitter.EmitShortJumpOnFalse(9));
    }

    #endregion

    #region Long Unconditional Jump (0xA0-0xA7)

    [Test]
    public void EmitForwardJump_ShortOffset_Uses1Byte()
    {
        _emitter.EmitForwardJump(5);
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    public void EmitForwardJump_LongOffset_Uses2Bytes()
    {
        _emitter.EmitForwardJump(100);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xA4));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xA7));
    }

    [Test]
    public void EmitForwardJump_MaxOffset_Works()
    {
        _emitter.EmitForwardJump(1023);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.EqualTo(0xA7)); // 1023 / 256 = 3, so 0xA4 + 3
    }

    [Test]
    public void EmitForwardJump_ExceedsMax_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _emitter.EmitForwardJump(1024));
    }

    #endregion

    #region Long Jump on False (0xAC-0xAF)

    [Test]
    public void EmitJumpOnFalse_ShortOffset_Uses1Byte()
    {
        _emitter.EmitJumpOnFalse(5);
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(1));
    }

    [Test]
    public void EmitJumpOnFalse_LongOffset_Uses2Bytes()
    {
        _emitter.EmitJumpOnFalse(100);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xAC));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xAF));
    }

    #endregion

    #region Long Jump on True (0xA8-0xAB)

    [Test]
    public void EmitJumpOnTrue_AlwaysUses2Bytes()
    {
        _emitter.EmitJumpOnTrue(5);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xA8));
        Assert.That(bytecodes[0], Is.LessThanOrEqualTo(0xAB));
    }

    #endregion

    #region Backward Jump (0xA0-0xA3)

    [Test]
    public void EmitJumpBackward_ValidOffset_EmitsCorrectBytecode()
    {
        // Emit some bytecodes first
        int start = _emitter.MarkMethodStart();
        _emitter.PushSelf();
        _emitter.PushSelf();
        _emitter.PushSelf();

        _emitter.EmitJumpBackward(start);
        var bytecodes = _emitter.GetBytecodes();

        // Last two bytes should be backward jump
        Assert.That(bytecodes[3], Is.GreaterThanOrEqualTo(0xA0));
        Assert.That(bytecodes[3], Is.LessThanOrEqualTo(0xA7));
    }

    #endregion

    #region Jump Placeholder and Patching

    [Test]
    public void EmitJumpOnFalsePlaceholder_ThenPatch_Works()
    {
        int placeholder = _emitter.EmitJumpOnFalsePlaceholder();
        _emitter.PushSelf();
        _emitter.PushSelf();
        _emitter.PushSelf();
        _emitter.PatchJumpOnFalse(placeholder);

        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(5));
        // First two bytes are the patched jump
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xAC));
    }

    [Test]
    public void EmitJumpOnTruePlaceholder_ThenPatch_Works()
    {
        int placeholder = _emitter.EmitJumpOnTruePlaceholder();
        _emitter.PushSelf();
        _emitter.PushSelf();
        _emitter.PatchJumpOnTrue(placeholder);

        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(4));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xA8));
    }

    [Test]
    public void EmitForwardJumpPlaceholder_ThenPatch_Works()
    {
        int placeholder = _emitter.EmitForwardJumpPlaceholder();
        _emitter.PushSelf();
        _emitter.PatchForwardJump(placeholder);

        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(3));
        Assert.That(bytecodes[0], Is.GreaterThanOrEqualTo(0xA4));
    }

    #endregion

    #region Literal Pool Management

    [Test]
    public void AddLiteral_ReturnsSameIndexForDuplicates()
    {
        int index1 = _emitter.AddLiteral(42);
        int index2 = _emitter.AddLiteral(42);
        Assert.That(index1, Is.EqualTo(index2));
    }

    [Test]
    public void AddLiteral_SymbolsGoToSelectorsPool()
    {
        int index = _emitter.AddLiteral(new Symbol("mySelector"));
        Assert.That(index, Is.EqualTo(0)); // First selector

        var literals = _emitter.GetLiterals();
        Assert.That(literals[0], Is.InstanceOf<Symbol>());
    }

    [Test]
    public void AddLiteral_StringsGoToOtherLiterals()
    {
        int index = _emitter.AddLiteral("myString");
        Assert.That(index, Is.EqualTo(0)); // First other literal (no selectors yet)

        var literals = _emitter.GetLiterals();
        Assert.That(literals[0], Is.EqualTo("myString"));
    }

    [Test]
    public void GetLiterals_SelectorsFirst_ThenOthers()
    {
        _emitter.AddLiteral("string1");
        _emitter.AddLiteral(new Symbol("selector1"));
        _emitter.AddLiteral("string2");
        _emitter.AddLiteral(new Symbol("selector2"));

        var literals = _emitter.GetLiterals();

        // Selectors should be first
        Assert.That(literals[0], Is.InstanceOf<Symbol>());
        Assert.That(literals[1], Is.InstanceOf<Symbol>());
        Assert.That(literals[2], Is.EqualTo("string1"));
        Assert.That(literals[3], Is.EqualTo("string2"));
    }

    [Test]
    public void AddLiteral_ArraysComparedElementwise()
    {
        var array1 = new object[] { 1, 2, 3 };
        var array2 = new object[] { 1, 2, 3 };

        int index1 = _emitter.AddLiteral(array1);
        int index2 = _emitter.AddLiteral(array2);

        Assert.That(index1, Is.EqualTo(index2));
    }

    #endregion

    #region Sizing Mode

    [Test]
    public void SizingMode_EmitCountsButDoesNotAdd()
    {
        _emitter.EnterSizingMode();
        _emitter.PushSelf();
        _emitter.PushSelf();
        _emitter.PushSelf();
        int count = _emitter.EndSizingMode();

        Assert.That(count, Is.EqualTo(3));
        Assert.That(_emitter.GetBytecodes().Length, Is.EqualTo(0), "Should not have emitted anything");
    }

    [Test]
    public void SizingMode_NestedWorks()
    {
        _emitter.EnterSizingMode();
        _emitter.PushSelf();

        _emitter.EnterSizingMode();
        _emitter.PushTrue();
        _emitter.PushFalse();
        int innerCount = _emitter.EndSizingMode();

        _emitter.PushNil();
        int outerCount = _emitter.EndSizingMode();

        Assert.That(innerCount, Is.EqualTo(2));
        Assert.That(outerCount, Is.EqualTo(2)); // PushSelf + PushNil (inner not counted in outer)
    }

    [Test]
    public void IsSizingMode_ReflectsState()
    {
        Assert.That(_emitter.IsSizingMode, Is.False);

        _emitter.EnterSizingMode();
        Assert.That(_emitter.IsSizingMode, Is.True);

        _emitter.EndSizingMode();
        Assert.That(_emitter.IsSizingMode, Is.False);
    }

    #endregion

    #region Static Size Calculation Methods

    [Test]
    public void SizeForwardJump_ShortOffset_Returns1()
    {
        for (int i = 1; i <= 8; i++)
        {
            Assert.That(BytecodeEmitter.SizeForwardJump(i), Is.EqualTo(1));
        }
    }

    [Test]
    public void SizeForwardJump_LongOffset_Returns2()
    {
        Assert.That(BytecodeEmitter.SizeForwardJump(9), Is.EqualTo(2));
        Assert.That(BytecodeEmitter.SizeForwardJump(100), Is.EqualTo(2));
    }

    [Test]
    public void SizeJumpOnFalse_ShortOffset_Returns1()
    {
        for (int i = 1; i <= 8; i++)
        {
            Assert.That(BytecodeEmitter.SizeJumpOnFalse(i), Is.EqualTo(1));
        }
    }

    [Test]
    public void SizeJumpOnTrue_AlwaysReturns2()
    {
        Assert.That(BytecodeEmitter.SizeJumpOnTrue(1), Is.EqualTo(2));
        Assert.That(BytecodeEmitter.SizeJumpOnTrue(8), Is.EqualTo(2));
        Assert.That(BytecodeEmitter.SizeJumpOnTrue(100), Is.EqualTo(2));
    }

    #endregion

    #region CurrentIP and CurrentOffset

    [Test]
    public void CurrentIP_TracksEmittedBytes()
    {
        Assert.That(_emitter.CurrentIP, Is.EqualTo(0));

        _emitter.PushSelf();
        Assert.That(_emitter.CurrentIP, Is.EqualTo(1));

        _emitter.EmitJumpOnFalse(100); // 2 bytes
        Assert.That(_emitter.CurrentIP, Is.EqualTo(3));
    }

    [Test]
    public void CurrentOffset_SameAsCurrentIP()
    {
        _emitter.PushSelf();
        _emitter.PushTrue();

        Assert.That(_emitter.CurrentOffset, Is.EqualTo(_emitter.CurrentIP));
    }

    #endregion

    #region CalculateMaxStackDepth

    [Test]
    public void CalculateMaxStackDepth_PushOnly()
    {
        _emitter.PushSelf();
        _emitter.PushTrue();
        _emitter.PushNil();

        Assert.That(_emitter.CalculateMaxStackDepth(), Is.EqualTo(3));
    }

    [Test]
    public void CalculateMaxStackDepth_PushAndPop()
    {
        _emitter.PushSelf();
        _emitter.PushTrue();
        _emitter.Pop();
        _emitter.PushNil();

        // Push 1, push 2, pop to 1, push to 2
        Assert.That(_emitter.CalculateMaxStackDepth(), Is.EqualTo(2));
    }

    [Test]
    public void CalculateMaxStackDepth_BinaryMessage()
    {
        _emitter.PushOne();
        _emitter.PushTwo();
        _emitter.EmitSend("+", 1);

        // Push 1, push 2 (max=2), send pops 2 pushes 1 (now 1)
        Assert.That(_emitter.CalculateMaxStackDepth(), Is.EqualTo(2));
    }

    #endregion
}
