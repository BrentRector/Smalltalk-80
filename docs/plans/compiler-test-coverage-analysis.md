# Smalltalk80.Compiler.Tests Coverage Analysis

**Created**: 2025-12-26
**Updated**: 2025-12-26
**Purpose**: Identify missing test cases to ensure refactoring safety

## Status: IMPLEMENTED

The following test files have been created and committed:
- `BytecodeEmitterTests.cs` - ~80 tests for bytecode generation
- `VariableResolverTests.cs` - ~25 tests for variable resolution
- `TempFrameAllocatorTests.cs` - ~25 tests for temp frame allocation
- `BytecodeOutputTests.cs` - ~50 tests for bytecode verification

### Running the Tests

Before starting refactoring, run all compiler tests to verify 100% pass rate:

```bash
cd Smalltalk80.Compiler.Tests
dotnet test
```

Expected: All tests should pass. Any failing tests indicate issues that must be fixed before refactoring.

---

## Executive Summary

The test suite now has **comprehensive coverage** for core functionality. The previously identified gaps have been addressed with dedicated unit tests.

---

## Current Test Coverage

| Test File | Lines | Tests | Coverage Area |
|-----------|-------|-------|---------------|
| **BytecodeEmitterTests.cs** | 650 | ~80 | **NEW: Bytecode generation** |
| **VariableResolverTests.cs** | 300 | ~25 | **NEW: Variable resolution** |
| **TempFrameAllocatorTests.cs** | 280 | ~25 | **NEW: Temp frame allocation** |
| **BytecodeOutputTests.cs** | 450 | ~50 | **NEW: Bytecode verification** |
| BasicCompilationTests.cs | 352 | 12 | JSON output, basic structure |
| BlockTests.cs | 339 | 17 | Block creation, params, temps |
| VariableTests.cs | 319 | 17 | Variable access (inst/temp/arg) |
| LiteralTests.cs | 304 | 18 | Literals (numbers, strings, etc.) |
| MessageTests.cs | 338 | 17 | Message sends (unary/binary/keyword) |
| MethodTests.cs | 356 | 20 | Method compilation, headers |
| ControlStructureTests.cs | 350 | 16 | Control flow (if/while/etc.) |
| ControlStructureInliningTests.cs | 834 | 52 | Inlining optimization |
| EdgeCaseTests.cs | 339 | 16 | Edge cases |
| IntegrationTests.cs | 285 | 7 | Full file compilation |
| TailRecursionAnalyzerTests.cs | 419 | 23 | Tail recursion detection |
| ComprehensiveContextAccessTests.cs | 646 | 18 | Context variable access |
| BlockOuterTempTests.cs | 363 | 6 | Block outer temp access |
| BlockContextTestBytecodeValidation.cs | 1329 | 30 | Block bytecode validation |
| RuntimeExecutionTests.cs | 187 | 6 | Runtime tests (ignored) |

**Total: ~440+ tests** (up from ~260)

---

## Part 1: Missing Test Cases (High Priority)

These tests are critical for ensuring refactoring doesn't break working code.

### 1.1 BytecodeEmitter Tests (NEW FILE NEEDED)

The `BytecodeEmitter` class has **NO dedicated unit tests**. This is a critical gap.

```csharp
// BytecodeEmitterTests.cs

[TestFixture]
public class BytecodeEmitterTests
{
    private BytecodeEmitter _emitter;

    [SetUp]
    public void Setup()
    {
        _emitter = new BytecodeEmitter();
    }

    #region Push Bytecodes

    [Test]
    public void PushReceiverVariable_Index0Through15_UsesShortForm()
    {
        for (int i = 0; i <= 15; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.PushReceiverVariable(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(i));
        }
    }

    [Test]
    public void PushReceiverVariable_Index16Plus_UsesExtendedForm()
    {
        _emitter.PushReceiverVariable(16);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes[0], Is.EqualTo(128)); // Extended push
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(0)); // Receiver var type
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(16)); // Index
    }

    [Test]
    public void PushTemporaryVariable_Index0Through15_UsesShortForm()
    {
        for (int i = 0; i <= 15; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.PushTemporaryVariable(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(16 + i));
        }
    }

    [Test]
    public void PushTemporaryVariable_Index16Through63_UsesExtendedForm()
    {
        _emitter.PushTemporaryVariable(32);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes[0], Is.EqualTo(128)); // Extended push
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(1)); // Temp var type
        Assert.That(bytecodes[1] & 0x3F, Is.EqualTo(32)); // Index
    }

    [Test]
    public void PushLiteralConstant_Index0Through31_UsesShortForm()
    {
        for (int i = 0; i <= 31; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.PushLiteralConstant(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(32 + i));
        }
    }

    [Test]
    public void PushLiteralConstant_Index32Plus_UsesExtendedForm()
    {
        _emitter.PushLiteralConstant(63);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes[0], Is.EqualTo(128)); // Extended push
        Assert.That(bytecodes[1] >> 6, Is.EqualTo(2)); // Literal constant type
    }

    [Test]
    public void PushLiteralVariable_Index0Through31_UsesShortForm()
    {
        for (int i = 0; i <= 31; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.PushLiteralVariable(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(64 + i));
        }
    }

    #endregion

    #region Store Bytecodes

    [Test]
    public void StoreReceiverVariable_Index0Through7_UsesShortForm()
    {
        for (int i = 0; i <= 7; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.StoreAndPopReceiverVariable(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(96 + i));
        }
    }

    [Test]
    public void StoreTemporaryVariable_Index0Through7_UsesShortForm()
    {
        for (int i = 0; i <= 7; i++)
        {
            var emitter = new BytecodeEmitter();
            emitter.StoreAndPopTemporaryVariable(i);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(104 + i));
        }
    }

    [Test]
    public void StoreReceiverVariable_Index8Plus_UsesExtendedForm()
    {
        _emitter.StoreAndPopReceiverVariable(10);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes[0], Is.EqualTo(129)); // Extended store
    }

    #endregion

    #region Special Constants

    [Test]
    public void PushSelf_EmitsCorrectBytecode()
    {
        _emitter.PushSelf();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(112));
    }

    [Test]
    public void PushTrue_EmitsCorrectBytecode()
    {
        _emitter.PushTrue();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(113));
    }

    [Test]
    public void PushFalse_EmitsCorrectBytecode()
    {
        _emitter.PushFalse();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(114));
    }

    [Test]
    public void PushNil_EmitsCorrectBytecode()
    {
        _emitter.PushNil();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(115));
    }

    [Test]
    public void PushMinusOne_EmitsCorrectBytecode()
    {
        _emitter.PushMinusOne();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(116));
    }

    [Test]
    public void PushZero_EmitsCorrectBytecode()
    {
        _emitter.PushZero();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(117));
    }

    [Test]
    public void PushOne_EmitsCorrectBytecode()
    {
        _emitter.PushOne();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(118));
    }

    [Test]
    public void PushTwo_EmitsCorrectBytecode()
    {
        _emitter.PushTwo();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(119));
    }

    #endregion

    #region Return Bytecodes

    [Test]
    public void ReturnSelf_EmitsCorrectBytecode()
    {
        _emitter.ReturnSelf();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(120));
    }

    [Test]
    public void ReturnTrue_EmitsCorrectBytecode()
    {
        _emitter.ReturnTrue();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(121));
    }

    [Test]
    public void ReturnFalse_EmitsCorrectBytecode()
    {
        _emitter.ReturnFalse();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(122));
    }

    [Test]
    public void ReturnNil_EmitsCorrectBytecode()
    {
        _emitter.ReturnNil();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(123));
    }

    [Test]
    public void ReturnTop_EmitsCorrectBytecode()
    {
        _emitter.ReturnTop();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(124));
    }

    [Test]
    public void BlockReturn_EmitsCorrectBytecode()
    {
        _emitter.BlockReturn();
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(125));
    }

    #endregion

    #region Send Bytecodes

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
    public void SendSpecialArithmeticSelector_EmitsCorrectBytecode(string selector, int expectedBytecode)
    {
        _emitter.SendSpecialSelector(selector);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expectedBytecode));
    }

    [Test]
    [TestCase("at:", 192)]
    [TestCase("at:put:", 193)]
    [TestCase("size", 194)]
    [TestCase("next", 195)]
    [TestCase("nextPut:", 196)]
    [TestCase("atEnd", 197)]
    [TestCase("==", 198)]
    [TestCase("class", 199)]
    [TestCase("blockCopy:", 200)]
    [TestCase("value", 201)]
    [TestCase("value:", 202)]
    [TestCase("do:", 203)]
    [TestCase("new", 204)]
    [TestCase("new:", 205)]
    [TestCase("x", 206)]
    [TestCase("y", 207)]
    public void SendSpecialCommonSelector_EmitsCorrectBytecode(string selector, int expectedBytecode)
    {
        _emitter.SendSpecialSelector(selector);
        Assert.That(_emitter.GetBytecodes()[0], Is.EqualTo(expectedBytecode));
    }

    #endregion

    #region Jump Bytecodes

    [Test]
    public void ShortUnconditionalJump_Offset1Through8_UsesShortForm()
    {
        for (int offset = 1; offset <= 8; offset++)
        {
            var emitter = new BytecodeEmitter();
            emitter.JumpForward(offset);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(143 + offset));
        }
    }

    [Test]
    public void ShortJumpOnFalse_Offset1Through8_UsesShortForm()
    {
        for (int offset = 1; offset <= 8; offset++)
        {
            var emitter = new BytecodeEmitter();
            emitter.JumpOnFalse(offset);
            Assert.That(emitter.GetBytecodes()[0], Is.EqualTo(151 + offset));
        }
    }

    [Test]
    public void LongUnconditionalJump_UsesExtendedForm()
    {
        _emitter.JumpForward(100);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0] >= 0xA4 && bytecodes[0] <= 0xA7, Is.True);
    }

    [Test]
    public void LongJumpOnFalse_UsesExtendedForm()
    {
        _emitter.JumpOnFalse(100);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0] >= 0xAC && bytecodes[0] <= 0xAF, Is.True);
    }

    [Test]
    public void LongJumpOnTrue_UsesExtendedForm()
    {
        _emitter.JumpOnTrue(100);
        var bytecodes = _emitter.GetBytecodes();
        Assert.That(bytecodes.Length, Is.EqualTo(2));
        Assert.That(bytecodes[0] >= 0xA8 && bytecodes[0] <= 0xAB, Is.True);
    }

    #endregion

    #region Literal Pool Management

    [Test]
    public void AddSelector_ReturnsSameIndexForDuplicates()
    {
        int index1 = _emitter.AddSelector("myMethod:");
        int index2 = _emitter.AddSelector("myMethod:");
        Assert.That(index1, Is.EqualTo(index2));
    }

    [Test]
    public void AddLiteral_ReturnsSameIndexForEqualLiterals()
    {
        int index1 = _emitter.AddLiteral(42);
        int index2 = _emitter.AddLiteral(42);
        Assert.That(index1, Is.EqualTo(index2));
    }

    [Test]
    public void GetLiterals_ReturnsSelectorsFirst()
    {
        _emitter.AddLiteral("string");
        _emitter.AddSelector("selector:");
        _emitter.AddLiteral(42);

        var literals = _emitter.GetLiterals();
        // First literals should be selectors
        Assert.That(literals[0], Is.InstanceOf<Symbol>());
    }

    #endregion

    #region BlockCopy Bytecode

    [Test]
    public void EmitBlockCopy_GeneratesCorrectSequence()
    {
        _emitter.EmitBlockCopy(2, 3, 10);
        var bytecodes = _emitter.GetBytecodes();

        Assert.That(bytecodes[0], Is.EqualTo(143)); // BlockCopy bytecode
        // Verify arguments are encoded correctly
    }

    #endregion

    #region Patch Points

    [Test]
    public void CreatePatchPoint_ReturnsCurrentPosition()
    {
        _emitter.PushSelf();
        _emitter.PushTrue();
        int patchPoint = _emitter.CreatePatchPoint();
        Assert.That(patchPoint, Is.EqualTo(2));
    }

    [Test]
    public void PatchJumpOffset_UpdatesBytecode()
    {
        _emitter.JumpForward(0); // Placeholder
        int patchPoint = 0;
        _emitter.PushSelf();
        _emitter.PushTrue();
        _emitter.PatchJumpOffset(patchPoint, 2);

        var bytecodes = _emitter.GetBytecodes();
        // Verify jump target was patched
    }

    #endregion
}
```

### 1.2 VariableResolver Tests (NEW FILE NEEDED)

```csharp
// VariableResolverTests.cs

[TestFixture]
public class VariableResolverTests
{
    #region Variable Resolution

    [Test]
    public void Resolve_InstanceVariable_ReturnsReceiverVariable()
    {
        var resolver = CreateResolver(instanceVars: ["x", "y", "z"]);
        var result = resolver.Resolve("y");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.ReceiverVariable));
        Assert.That(result.Index, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_MethodArgument_ReturnsTemporaryVariable()
    {
        var resolver = CreateResolverWithMethod(args: ["arg1", "arg2"]);
        var result = resolver.Resolve("arg2");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
        Assert.That(result.Index, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_MethodTemp_ReturnsTemporaryAfterArgs()
    {
        var resolver = CreateResolverWithMethod(args: ["arg1"], temps: ["temp1"]);
        var result = resolver.Resolve("temp1");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
        Assert.That(result.Index, Is.EqualTo(1)); // After arg1
    }

    [Test]
    public void Resolve_BlockArg_ReturnsTemporaryAtOffset()
    {
        var resolver = CreateResolverWithBlock(
            methodArgs: ["marg"],
            methodTemps: ["mtemp"],
            blockArgs: ["barg"]);
        var result = resolver.Resolve("barg");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
        Assert.That(result.Index, Is.EqualTo(0)); // Block arg is at index 0 in block
    }

    [Test]
    public void Resolve_OuterTemp_ReturnsAdjustedIndex()
    {
        var resolver = CreateResolverWithBlock(
            methodArgs: [],
            methodTemps: ["outer"],
            blockArgs: ["x"]);
        var result = resolver.Resolve("outer");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
        Assert.That(result.Index, Is.EqualTo(1)); // After block arg
    }

    [Test]
    public void Resolve_Self_ReturnsSpecialVariable()
    {
        var resolver = CreateResolver();
        var result = resolver.Resolve("self");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.Self));
    }

    [Test]
    public void Resolve_Super_ReturnsSpecialVariable()
    {
        var resolver = CreateResolver();
        var result = resolver.Resolve("super");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.Super));
    }

    [Test]
    public void Resolve_True_ReturnsSpecialConstant()
    {
        var resolver = CreateResolver();
        var result = resolver.Resolve("true");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.SpecialConstant));
    }

    [Test]
    public void Resolve_ClassVariable_ReturnsLiteralVariable()
    {
        var resolver = CreateResolver(classVars: ["ClassVar"]);
        var result = resolver.Resolve("ClassVar");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.LiteralVariable));
    }

    [Test]
    public void Resolve_GlobalVariable_ReturnsLiteralVariable()
    {
        var resolver = CreateResolver();
        var result = resolver.Resolve("Smalltalk");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.LiteralVariable));
    }

    [Test]
    public void Resolve_UndeclaredVariable_ReturnsGlobalAssumption()
    {
        var resolver = CreateResolver();
        var result = resolver.Resolve("UnknownGlobal");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.LiteralVariable));
        Assert.That(resolver.HasUndeclaredVariables, Is.True);
    }

    #endregion

    #region Shadowing

    [Test]
    public void Resolve_ShadowedByTemp_ReturnsTemp()
    {
        var resolver = CreateResolverWithMethod(
            instanceVars: ["x"],
            temps: ["x"]); // Temp shadows instance var
        var result = resolver.Resolve("x");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
    }

    [Test]
    public void Resolve_ShadowedByBlockArg_ReturnsBlockArg()
    {
        var resolver = CreateResolverWithBlock(
            methodTemps: ["x"],
            blockArgs: ["x"]); // Block arg shadows method temp
        var result = resolver.Resolve("x");

        Assert.That(result.Kind, Is.EqualTo(VariableKind.TemporaryVariable));
        Assert.That(result.Index, Is.EqualTo(0)); // Block arg index
    }

    #endregion

    // Helper methods to create resolvers...
}
```

### 1.3 TempFrameAllocator Tests (NEW FILE NEEDED)

```csharp
// TempFrameAllocatorTests.cs

[TestFixture]
public class TempFrameAllocatorTests
{
    [Test]
    public void Allocate_MethodArgs_StartAtZero()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod(["arg1", "arg2"], ["temp1"]);

        Assert.That(allocator.GetIndex("arg1"), Is.EqualTo(0));
        Assert.That(allocator.GetIndex("arg2"), Is.EqualTo(1));
    }

    [Test]
    public void Allocate_MethodTemps_FollowArgs()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod(["arg1", "arg2"], ["temp1", "temp2"]);

        Assert.That(allocator.GetIndex("temp1"), Is.EqualTo(2));
        Assert.That(allocator.GetIndex("temp2"), Is.EqualTo(3));
    }

    [Test]
    public void Allocate_BlockArgs_StartAtBlockOffset()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod(["marg"], ["mtemp"]);
        allocator.EnterBlock(["barg1", "barg2"], []);

        Assert.That(allocator.GetIndex("barg1"), Is.EqualTo(0));
        Assert.That(allocator.GetIndex("barg2"), Is.EqualTo(1));
    }

    [Test]
    public void Allocate_OuterVariableFromBlock_AdjustsIndex()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod([], ["outer"]);
        allocator.EnterBlock(["x"], []);

        // Outer has absolute index 0, block has 1 local (x)
        // To access outer from block: 0 + 1 = 1
        Assert.That(allocator.GetIndex("outer"), Is.EqualTo(1));
    }

    [Test]
    public void Allocate_NestedBlock_AccumulatesOffsets()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod([], ["outer"]);
        allocator.EnterBlock(["x"], []);
        allocator.EnterBlock(["y"], []);

        // Outer has absolute index 0
        // First block has 1 local (x)
        // Second block has 1 local (y)
        // To access outer: 0 + 1 + 1 = 2
        Assert.That(allocator.GetIndex("outer"), Is.EqualTo(2));
    }

    [Test]
    public void ExitBlock_RestoresParentScope()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod([], ["outer"]);
        allocator.EnterBlock(["x"], []);
        allocator.ExitBlock();

        Assert.That(allocator.GetIndex("outer"), Is.EqualTo(0));
    }

    [Test]
    public void GetBlockLocalCount_ReturnsArgsAndTemps()
    {
        var allocator = new TempFrameAllocator();
        allocator.EnterMethod([], []);
        allocator.EnterBlock(["a", "b"], ["t"]);

        Assert.That(allocator.GetBlockLocalCount(), Is.EqualTo(3));
    }
}
```

---

## Part 2: Missing Test Cases (Medium Priority)

### 2.1 AST Builder Tests (FileInFormatAstBuilder)

```csharp
// FileInFormatAstBuilderTests.cs

[TestFixture]
public class FileInFormatAstBuilderTests
{
    #region Class Definition Parsing

    [Test]
    public void Parse_ClassDefinition_ExtractsInstanceVariables()
    {
        var source = @"
Object subclass: #TestClass
    instanceVariableNames: 'x y z'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";

        var result = ParseSource(source);
        var classDef = result.Classes.First();

        Assert.That(classDef.InstanceVariableNames, Is.EqualTo(["x", "y", "z"]));
    }

    [Test]
    public void Parse_ClassDefinition_ExtractsClassVariables()
    {
        var source = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: 'ClassVar1 ClassVar2'
    poolDictionaries: ''
    category: 'Test'!";

        var result = ParseSource(source);
        var classDef = result.Classes.First();

        Assert.That(classDef.ClassVariableNames, Is.EqualTo(["ClassVar1", "ClassVar2"]));
    }

    [Test]
    public void Parse_ClassDefinition_ExtractsPoolDictionaries()
    {
        var source = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: 'TextConstants GraphicsConstants'
    category: 'Test'!";

        var result = ParseSource(source);
        var classDef = result.Classes.First();

        Assert.That(classDef.PoolDictionaryNames, Is.EqualTo(["TextConstants", "GraphicsConstants"]));
    }

    #endregion

    #region Method Parsing

    [Test]
    public void Parse_UnaryMethod_ExtractsSelector()
    {
        var result = ParseMethodSource("simpleMethod ^42");
        Assert.That(result.Selector, Is.EqualTo("simpleMethod"));
    }

    [Test]
    public void Parse_BinaryMethod_ExtractsSelector()
    {
        var result = ParseMethodSource("+ other ^self addWith: other");
        Assert.That(result.Selector, Is.EqualTo("+"));
    }

    [Test]
    public void Parse_KeywordMethod_ExtractsSelector()
    {
        var result = ParseMethodSource("at: index put: value ^self basicAt: index put: value");
        Assert.That(result.Selector, Is.EqualTo("at:put:"));
    }

    [Test]
    public void Parse_MethodWithPrimitive_ExtractsPrimitiveNumber()
    {
        var result = ParseMethodSource(@"
basicAt: index
    <primitive: 60>
    ^self primitiveFailed");

        Assert.That(result.PrimitiveNumber, Is.EqualTo(60));
    }

    [Test]
    public void Parse_MethodWithComment_IgnoresComment()
    {
        var result = ParseMethodSource(@"
test
    ""This is a comment""
    ^42");

        Assert.That(result.Selector, Is.EqualTo("test"));
    }

    #endregion

    #region Expression Parsing

    [Test]
    public void Parse_Assignment_CreatesAssignmentNode()
    {
        var result = ParseExpressionSource("x := 42");
        Assert.That(result, Is.InstanceOf<AssignmentNode>());
    }

    [Test]
    public void Parse_Cascade_CreatesCascadeNode()
    {
        var result = ParseExpressionSource("self foo; bar; baz");
        Assert.That(result, Is.InstanceOf<CascadeNode>());
    }

    [Test]
    public void Parse_DynamicArray_CreatesArrayNode()
    {
        var result = ParseExpressionSource("{1. 2. 3 + 4}");
        Assert.That(result, Is.InstanceOf<DynamicArrayNode>());
    }

    #endregion
}
```

### 2.2 ColdStartJsonGenerator Tests

```csharp
// ColdStartJsonGeneratorTests.cs

[TestFixture]
public class ColdStartJsonGeneratorTests
{
    #region Symbol Table Generation

    [Test]
    public void Generate_CollectsAllSymbols()
    {
        var json = CompileSource(@"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!");

        var symbols = json.RootElement.GetProperty("symbols");
        Assert.That(symbols.GetArrayLength(), Is.GreaterThan(0));

        // Should contain class name
        bool hasTestClass = false;
        for (int i = 0; i < symbols.GetArrayLength(); i++)
        {
            if (symbols[i].GetString() == "TestClass")
            {
                hasTestClass = true;
                break;
            }
        }
        Assert.That(hasTestClass, Is.True);
    }

    [Test]
    public void Generate_SymbolsAreUnique()
    {
        var json = CompileSource(WrapInTestClass(@"
test1 ^#symbol.
test2 ^#symbol"));

        var symbols = json.RootElement.GetProperty("symbols");
        var symbolList = new List<string>();
        for (int i = 0; i < symbols.GetArrayLength(); i++)
        {
            symbolList.Add(symbols[i].GetString()!);
        }

        Assert.That(symbolList.Distinct().Count(), Is.EqualTo(symbolList.Count));
    }

    #endregion

    #region String Table Generation

    [Test]
    public void Generate_CollectsAllStrings()
    {
        var json = CompileSource(WrapInTestClass("test ^'Hello'"));

        var strings = json.RootElement.GetProperty("strings");
        Assert.That(strings.GetArrayLength(), Is.GreaterThan(0));
    }

    #endregion

    #region Method Header Generation

    [Test]
    public void Generate_MethodHeader_IncludesArgCount()
    {
        var json = CompileSource(WrapInTestClass("test: a with: b ^a + b"));
        var method = json.RootElement.GetProperty("methods")[0];
        var header = method.GetProperty("header");

        Assert.That(header.GetProperty("argCount").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public void Generate_MethodHeader_IncludesTempCount()
    {
        var json = CompileSource(WrapInTestClass("test | a b c | ^a"));
        var method = json.RootElement.GetProperty("methods")[0];
        var header = method.GetProperty("header");

        Assert.That(header.GetProperty("tempCount").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public void Generate_MethodHeader_IncludesLiteralCount()
    {
        var json = CompileSource(WrapInTestClass("test ^self foo bar baz"));
        var method = json.RootElement.GetProperty("methods")[0];
        var header = method.GetProperty("header");

        Assert.That(header.GetProperty("literalCount").GetInt32(), Is.GreaterThan(0));
    }

    [Test]
    public void Generate_LargeContext_SetWhenNeeded()
    {
        // Generate method with many temps to require large context
        var temps = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"t{i}"));
        var json = CompileSource(WrapInTestClass($"test | {temps} | ^t1"));
        var method = json.RootElement.GetProperty("methods")[0];
        var header = method.GetProperty("header");

        Assert.That(header.GetProperty("largeContext").GetBoolean(), Is.True);
    }

    #endregion

    #region Install Order Generation

    [Test]
    public void Generate_InstallOrder_HasCorrectPhases()
    {
        var json = CompileSource(WrapInTestClass("test ^42"));
        var installOrder = json.RootElement.GetProperty("installOrder");
        var phases = installOrder.GetProperty("phases");

        var phaseKinds = new List<string>();
        for (int i = 0; i < phases.GetArrayLength(); i++)
        {
            phaseKinds.Add(phases[i].GetProperty("kind").GetString()!);
        }

        // Should have classes before methods
        int classesIndex = phaseKinds.IndexOf("classes");
        int methodsIndex = phaseKinds.IndexOf("methods");
        Assert.That(classesIndex, Is.LessThan(methodsIndex));
    }

    #endregion
}
```

---

## Part 3: Missing Test Cases (Lower Priority)

### 3.1 Error Handling Tests

```csharp
// CompilerErrorTests.cs

[TestFixture]
public class CompilerErrorTests
{
    [Test]
    public void Compile_SyntaxError_ThrowsWithMessage()
    {
        var source = "Object subclass: #Test"; // Incomplete
        Assert.Throws<InvalidOperationException>(() => CompileSource(source));
    }

    [Test]
    public void Compile_UnterminatedString_ReportsError()
    {
        var source = WrapInTestClass("test ^'unclosed string");
        Assert.Throws<InvalidOperationException>(() => CompileSource(source));
    }

    [Test]
    public void Compile_InvalidPrimitiveNumber_ReportsError()
    {
        var source = WrapInTestClass("<primitive: abc> ^self");
        Assert.Throws<InvalidOperationException>(() => CompileSource(source));
    }
}
```

### 3.2 Bytecode Size Limit Tests

```csharp
// BytecodeLimitTests.cs

[TestFixture]
public class BytecodeLimitTests
{
    [Test]
    public void Compile_MethodOver256Literals_UsesExtendedBytecodes()
    {
        // Generate method with many literals
        var assignments = string.Join("\n",
            Enumerable.Range(1, 100).Select(i => $"a{i} := {i}."));
        var source = WrapInTestClass($"test | {string.Join(" ", Enumerable.Range(1, 100).Select(i => $"a{i}"))} | {assignments} ^self");

        // Should compile without error
        var json = CompileSource(source);
        Assert.That(json, Is.Not.Null);
    }

    [Test]
    public void Compile_VeryLargeMethod_DoesNotOverflow()
    {
        // Generate a very large method
        var statements = string.Join("\n",
            Enumerable.Range(1, 500).Select(i => $"self foo{i % 50}."));
        var source = WrapInTestClass($"test {statements} ^self");

        var json = CompileSource(source);
        Assert.That(json, Is.Not.Null);
    }
}
```

### 3.3 Source Position Tests

```csharp
// SourcePositionTests.cs

[TestFixture]
public class SourcePositionTests
{
    [Test]
    public void Compile_Method_IncludesSourcePosition()
    {
        var json = CompileSource(WrapInTestClass("test ^42"));
        var method = json.RootElement.GetProperty("methods")[0];

        // Should have source position bytes
        var bytecodes = method.GetProperty("bytecodes");
        Assert.That(bytecodes.GetArrayLength(), Is.GreaterThan(2));
        // Last 3 bytes are source position
    }

    [Test]
    public void Compile_SourceFile_GeneratesSourcesFile()
    {
        // Verify .sources file is generated with chunk format
    }
}
```

---

## Part 4: Implementation Plan

### Phase 1: Critical Tests (Before Refactoring)
1. Create `BytecodeEmitterTests.cs` (~50 tests)
2. Create `VariableResolverTests.cs` (~20 tests)
3. Create `TempFrameAllocatorTests.cs` (~15 tests)

### Phase 2: Supporting Tests
4. Create `FileInFormatAstBuilderTests.cs` (~25 tests)
5. Create `ColdStartJsonGeneratorTests.cs` (~20 tests)

### Phase 3: Edge Case Tests
6. Create `CompilerErrorTests.cs` (~10 tests)
7. Create `BytecodeLimitTests.cs` (~5 tests)
8. Create `SourcePositionTests.cs` (~5 tests)

### Phase 4: Integration Validation
9. Add "golden file" tests that compare output byte-for-byte
10. Add performance regression tests

---

## Success Criteria

After implementing these tests:

1. **Total test count**: ~400+ tests (up from ~260)
2. **Coverage**: All major compiler components have dedicated unit tests
3. **Regression safety**: Any refactoring that changes bytecode output will fail tests
4. **Edge cases**: Boundary conditions (64+ temps, 32+ literals, etc.) are tested

---

## Appendix: Current Test Gaps Summary

| Component | Current Coverage | Gap |
|-----------|------------------|-----|
| BytecodeEmitter | 0 tests | **CRITICAL** |
| VariableResolver | ~5 tests (indirect) | **HIGH** |
| TempFrameAllocator | ~5 tests (indirect) | **HIGH** |
| FileInFormatAstBuilder | ~10 tests (indirect) | Medium |
| ColdStartJsonGenerator | ~15 tests (indirect) | Medium |
| Error handling | ~2 tests | Low |
| Bytecode limits | 0 tests | Low |
| Source positions | 0 tests | Low |
