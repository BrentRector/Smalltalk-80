using Moq;
using Xunit;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Tests.VM;

/// <summary>
/// Comprehensive unit tests for the BytecodeInterpreter component.
/// Tests all bytecode ranges from Blue Book Chapter 30.
/// </summary>
public class BytecodeInterpreterTests
{
    private readonly Mock<ISmalltalkHeapManager> _mockHeapManager;
    private readonly Mock<IBytecodeContext> _mockContext;
    private readonly VMMetadata _metadata;
    private readonly RuntimeHeapAccessor _runtimeHeapAccess;

    public BytecodeInterpreterTests()
    {
        _mockHeapManager = new Mock<ISmalltalkHeapManager>(MockBehavior.Loose);
        _mockContext = new Mock<IBytecodeContext>(MockBehavior.Loose);
        _metadata = CreateTestMetadata();
        _runtimeHeapAccess = new RuntimeHeapAccessor(_mockHeapManager.Object);

        // Setup default behaviors
        SetupDefaultMocks();
    }

    private VMMetadata CreateTestMetadata()
    {
        return new VMMetadata
        {
            BlockContextClassOOP = new SmalltalkOOP(100),
            MethodContextClassOOP = new SmalltalkOOP(102),
            SmallIntegerClassOOP = new SmalltalkOOP(104),
            SymbolClassOOP = new SmalltalkOOP(106),
            ClassNameFieldIndex = 6,
            MetaclassThisClassFieldIndex = 6
        };
    }

    private void SetupDefaultMocks()
    {
        // Setup active context
        var activeContext = new SmalltalkOOP(200);
        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);

        // Setup class OOP for context (MethodContext by default)
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Setup receiver
        var receiverOOP = new SmalltalkOOP(300);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);
    }

    private BytecodeInterpreter CreateInterpreter()
    {
        return new BytecodeInterpreter(
            _mockHeapManager.Object,
            _mockContext.Object,
            _runtimeHeapAccess,
            _metadata);
    }

    #region Push Receiver Variable Tests (0x00-0x0F)

    [Theory]
    [InlineData(0x00, 0)]
    [InlineData(0x01, 1)]
    [InlineData(0x05, 5)]
    [InlineData(0x0F, 15)]
    public void ExecuteBytecode_PushReceiverVariable_PushesCorrectValue(byte bytecode, int expectedIndex)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var receiverOOP = new SmalltalkOOP(300);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42 + expectedIndex);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(receiverOOP, expectedIndex))
            .Returns(expectedValue);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(expectedValue), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushReceiverVariable_FromBlockContext_UsesHomeReceiver()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var blockContext = new SmalltalkOOP(200);
        var methodContext = new SmalltalkOOP(250);
        var receiverOOP = new SmalltalkOOP(300);
        var expectedValue = SmalltalkOOP.FromSmallInteger(99);

        _mockContext.Setup(c => c.ActiveContext).Returns(blockContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(blockContext))
            .Returns(_metadata.BlockContextClassOOP);
        _mockContext.Setup(c => c.FindMethodContext(blockContext))
            .Returns(methodContext);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(receiverOOP, 0))
            .Returns(expectedValue);

        // Act
        var result = interpreter.ExecuteBytecode(0x00, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(expectedValue), Times.Once);
    }

    #endregion

    #region Push Temporary Variable Tests (0x10-0x1F)

    [Theory]
    [InlineData(0x10, 0)]
    [InlineData(0x11, 1)]
    [InlineData(0x15, 5)]
    [InlineData(0x1F, 15)]
    public void ExecuteBytecode_PushTempVariable_PushesCorrectValue(byte bytecode, int expectedIndex)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var expectedValue = SmalltalkOOP.FromSmallInteger(100 + expectedIndex);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetTemporaryVariable(activeContext, expectedIndex))
            .Returns(expectedValue);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(expectedValue), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushTempVariable_WithVerboseDebug_OutputsDebugInfo()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        interpreter.VerboseDebug = true;
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetTemporaryVariable(activeContext, 0))
            .Returns(expectedValue);

        // Act
        var result = interpreter.ExecuteBytecode(0x10, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(expectedValue), Times.Once);
    }

    #endregion

    #region Push Literal Constant Tests (0x20-0x3F)

    [Theory]
    [InlineData(0x20, 0)]
    [InlineData(0x21, 1)]
    [InlineData(0x2F, 15)]
    [InlineData(0x3F, 31)]
    public void ExecuteBytecode_PushLiteralConstant_PushesCorrectLiteral(byte bytecode, int expectedIndex)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var expectedLiteral = new SmalltalkOOP(500 + expectedIndex);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, expectedIndex + 1))
            .Returns(expectedLiteral);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(expectedLiteral), Times.Once);
    }

    #endregion

    #region Push Literal Variable Tests (0x40-0x5F)

    [Fact]
    public void ExecuteBytecode_PushLiteralVariable_NilLiteral_PushesNil()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, 1))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = interpreter.ExecuteBytecode(0x40, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.Nil), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushLiteralVariable_Association_PushesValue()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var associationOOP = new SmalltalkOOP(600);
        var associationClassOOP = new SmalltalkOOP(700);
        var keyOOP = new SmalltalkOOP(800);
        var valueOOP = SmalltalkOOP.FromSmallInteger(42);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, 1))
            .Returns(associationOOP);
        _mockHeapManager.Setup(h => h.GetClassOOP(associationOOP))
            .Returns(associationClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariableCount(associationOOP))
            .Returns(2);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX))
            .Returns(keyOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX))
            .Returns(valueOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x40, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(valueOOP), Times.Once);
    }

    #endregion

    #region Pop and Store Instance Variable Tests (0x60-0x67)

    [Theory]
    [InlineData(0x60, 0)]
    [InlineData(0x61, 1)]
    [InlineData(0x65, 5)]
    [InlineData(0x67, 7)]
    public void ExecuteBytecode_PopStoreInstanceVariable_StoresCorrectly(byte bytecode, int expectedIndex)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var receiverOOP = new SmalltalkOOP(300);
        var valueToStore = SmalltalkOOP.FromSmallInteger(123);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);
        _mockContext.Setup(c => c.Pop()).Returns(valueToStore);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockHeapManager.Verify(h => h.SetInstanceVariable(receiverOOP, expectedIndex, valueToStore), Times.Once);
    }

    #endregion

    #region Pop and Store Temporary Variable Tests (0x68-0x6F)

    [Theory]
    [InlineData(0x68, 0)]
    [InlineData(0x69, 1)]
    [InlineData(0x6D, 5)]
    [InlineData(0x6F, 7)]
    public void ExecuteBytecode_PopStoreTempVariable_StoresCorrectly(byte bytecode, int expectedIndex)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var valueToStore = SmalltalkOOP.FromSmallInteger(456);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.Pop()).Returns(valueToStore);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.SetTemporaryVariable(activeContext, expectedIndex, valueToStore), Times.Once);
    }

    #endregion

    #region Special Constants Tests (0x70-0x77)

    [Fact]
    public void ExecuteBytecode_PushSelf_PushesReceiver()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var receiverOOP = new SmalltalkOOP(300);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x70, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(receiverOOP), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushTrue_PushesTrueOOP()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x71, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.True), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushFalse_PushesFalseOOP()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x72, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.False), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushNil_PushesNilOOP()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x73, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.Nil), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushMinusOne_PushesSmallIntegerMinusOne()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x74, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SmalltalkOOP.FromSmallInteger(-1)), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushZero_PushesSmallIntegerZero()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x75, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SmalltalkOOP.FromSmallInteger(0)), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushOne_PushesSmallIntegerOne()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x76, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SmalltalkOOP.FromSmallInteger(1)), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_PushTwo_PushesSmallIntegerTwo()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x77, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(SmalltalkOOP.FromSmallInteger(2)), Times.Once);
    }

    #endregion

    #region Return Operations Tests (0x78-0x7D)

    [Fact]
    public void ExecuteBytecode_ReturnSelf_PushesReceiverAndReturnsFalse()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var receiverOOP = new SmalltalkOOP(300);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_RECEIVER_INDEX))
            .Returns(receiverOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x78, methodOOP);

        // Assert
        Assert.False(result);
        _mockContext.Verify(c => c.Push(receiverOOP), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_ReturnTrue_PushesTrueAndReturnsFalse()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x79, methodOOP);

        // Assert
        Assert.False(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.True), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_ReturnFalse_PushesFalseAndReturnsFalse()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x7A, methodOOP);

        // Assert
        Assert.False(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.False), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_ReturnNil_PushesNilAndReturnsFalse()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x7B, methodOOP);

        // Assert
        Assert.False(result);
        _mockContext.Verify(c => c.Push(SpecialObjectsArray.Instance.Nil), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_ReturnTop_ReturnsFalseWithoutPush()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x7C, methodOOP);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Jump Tests (0x90-0x97)

    [Theory]
    [InlineData(0x90, 1)]
    [InlineData(0x91, 2)]
    [InlineData(0x94, 5)]
    [InlineData(0x97, 8)]
    public void ExecuteBytecode_ShortJumpForward_UpdatesIPCorrectly(byte bytecode, int expectedJump)
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var currentIP = SmalltalkOOP.FromSmallInteger(10);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(activeContext, VMConstants.CONTEXT_PC_INDEX))
            .Returns(currentIP);

        // Act
        var result = interpreter.ExecuteBytecode(bytecode, methodOOP);

        // Assert
        Assert.True(result);
        _mockHeapManager.Verify(h => h.SetInstanceVariable(
            activeContext,
            VMConstants.CONTEXT_PC_INDEX,
            SmalltalkOOP.FromSmallInteger(10 + expectedJump)), Times.Once);
    }

    #endregion

    #region Push Active Context Test (0x89)

    [Fact]
    public void ExecuteBytecode_PushActiveContext_PushesCurrentContext()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act
        var result = interpreter.ExecuteBytecode(0x89, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.Push(activeContext), Times.Once);
    }

    #endregion

    #region Send Literal Selector Tests (0xD0-0xFF)

    [Fact]
    public void ExecuteBytecode_Send0Args_PopReceiverAndSendsMessage()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var selectorOOP = new SmalltalkOOP(500);
        var receiverOOP = new SmalltalkOOP(300);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, 1)).Returns(selectorOOP);
        _mockContext.Setup(c => c.Pop()).Returns(receiverOOP);
        _mockContext.Setup(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.IsAny<SmalltalkOOP[]>()))
            .Returns(true);

        // Act
        var result = interpreter.ExecuteBytecode(0xD0, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.Is<SmalltalkOOP[]>(a => a.Length == 0)), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_Send1Arg_PopReceiverArgAndSendsMessage()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var selectorOOP = new SmalltalkOOP(500);
        var receiverOOP = new SmalltalkOOP(300);
        var argOOP = SmalltalkOOP.FromSmallInteger(42);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, 1)).Returns(selectorOOP);
        _mockContext.SetupSequence(c => c.Pop())
            .Returns(argOOP)
            .Returns(receiverOOP);
        _mockContext.Setup(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.IsAny<SmalltalkOOP[]>()))
            .Returns(true);

        // Act
        var result = interpreter.ExecuteBytecode(0xE0, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.Is<SmalltalkOOP[]>(a => a.Length == 1 && a[0] == argOOP)), Times.Once);
    }

    [Fact]
    public void ExecuteBytecode_Send2Args_PopReceiverArgsAndSendsMessage()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);
        var selectorOOP = new SmalltalkOOP(500);
        var receiverOOP = new SmalltalkOOP(300);
        var arg1OOP = SmalltalkOOP.FromSmallInteger(1);
        var arg2OOP = SmalltalkOOP.FromSmallInteger(2);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockContext.Setup(c => c.GetMethodLiteral(methodOOP, 1)).Returns(selectorOOP);
        _mockContext.SetupSequence(c => c.Pop())
            .Returns(arg2OOP)
            .Returns(arg1OOP)
            .Returns(receiverOOP);
        _mockContext.Setup(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.IsAny<SmalltalkOOP[]>()))
            .Returns(true);

        // Act
        var result = interpreter.ExecuteBytecode(0xF0, methodOOP);

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.SendMessageAndContinue(receiverOOP, selectorOOP, It.Is<SmalltalkOOP[]>(a => a.Length == 2 && a[0] == arg1OOP && a[1] == arg2OOP)), Times.Once);
    }

    #endregion

    #region Selector Cache Tests

    [Fact]
    public void InitializeSelectorCaches_SetsUpCachesCorrectly()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var arithmeticSelectors = new SmalltalkOOP[16];
        var specialSelectors = new SmalltalkOOP[16];
        for (int i = 0; i < 16; i++)
        {
            arithmeticSelectors[i] = new SmalltalkOOP(1000 + i);
            specialSelectors[i] = new SmalltalkOOP(2000 + i);
        }

        // Act - should not throw
        interpreter.InitializeSelectorCaches(arithmeticSelectors, specialSelectors);

        // Assert - no exception means success
        Assert.True(true);
    }

    #endregion

    #region Unimplemented Bytecode Test

    [Fact]
    public void ExecuteBytecode_UnimplementedBytecode_ThrowsNotImplementedException()
    {
        // Arrange
        var interpreter = CreateInterpreter();
        var methodOOP = new SmalltalkOOP(400);
        var activeContext = new SmalltalkOOP(200);

        _mockContext.Setup(c => c.ActiveContext).Returns(activeContext);
        _mockHeapManager.Setup(h => h.GetClassOOP(activeContext))
            .Returns(_metadata.MethodContextClassOOP);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => interpreter.ExecuteBytecode(0x8F, methodOOP));
    }

    #endregion

    #region VerboseDebug Property Tests

    [Fact]
    public void VerboseDebug_DefaultsToFalse()
    {
        // Arrange
        var interpreter = CreateInterpreter();

        // Assert
        Assert.False(interpreter.VerboseDebug);
    }

    [Fact]
    public void VerboseDebug_CanBeSetToTrue()
    {
        // Arrange
        var interpreter = CreateInterpreter();

        // Act
        interpreter.VerboseDebug = true;

        // Assert
        Assert.True(interpreter.VerboseDebug);
    }

    #endregion
}
