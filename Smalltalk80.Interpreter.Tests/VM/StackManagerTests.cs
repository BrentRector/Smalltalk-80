using Moq;
using Xunit;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Tests.VM;

/// <summary>
/// Comprehensive unit tests for the StackManager component.
/// Tests evaluation stack operations: Push, Pop, and Peek.
/// </summary>
public class StackManagerTests
{
    private readonly Mock<ISmalltalkHeapManager> _mockHeapManager;
    private readonly Mock<IObjectMetadataAccessor> _mockHelpers;
    private readonly VMMetadata _metadata;
    private SmalltalkOOP _activeContext;
    private bool _selectorTraceEnabled;
    private int _pushTraceCounter;

    public StackManagerTests()
    {
        _mockHeapManager = new Mock<ISmalltalkHeapManager>(MockBehavior.Loose);
        _mockHelpers = new Mock<IObjectMetadataAccessor>(MockBehavior.Loose);
        _metadata = CreateTestMetadata();
        _activeContext = new SmalltalkOOP(200);
        _selectorTraceEnabled = false;
        _pushTraceCounter = 0;
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

    private StackManager CreateStackManager()
    {
        return new StackManager(
            _mockHeapManager.Object,
            _metadata,
            _mockHelpers.Object,
            () => _activeContext,
            () => _selectorTraceEnabled,
            () => _pushTraceCounter,
            () => _pushTraceCounter++);
    }

    private void SetupMethodContext(int stackPointer, int stackSize, int numArgs = 0, int numTemps = 0)
    {
        _mockHeapManager.Setup(h => h.GetObjectFormat(_activeContext))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetClassOOP(_activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHelpers.Setup(h => h.GetClassName(_metadata.MethodContextClassOOP))
            .Returns("MethodContext");
        _mockHelpers.Setup(h => h.GetClass(_activeContext))
            .Returns(_metadata.MethodContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX))
            .Returns(SmalltalkOOP.FromSmallInteger(stackPointer));
        _mockHeapManager.Setup(h => h.GetIndexedSize(_activeContext))
            .Returns(stackSize);

        var methodOOP = new SmalltalkOOP(400);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.CONTEXT_METHOD_INDEX))
            .Returns(methodOOP);
        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(5, false, numTemps, 1, numArgs, 0, false, false, 0));
    }

    private void SetupBlockContext(int stackPointer, int stackSize, int nargs)
    {
        _mockHeapManager.Setup(h => h.GetObjectFormat(_activeContext))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetClassOOP(_activeContext))
            .Returns(_metadata.BlockContextClassOOP);
        _mockHelpers.Setup(h => h.GetClassName(_metadata.BlockContextClassOOP))
            .Returns("BlockContext");
        _mockHelpers.Setup(h => h.GetClass(_activeContext))
            .Returns(_metadata.BlockContextClassOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX))
            .Returns(SmalltalkOOP.FromSmallInteger(stackPointer));
        _mockHeapManager.Setup(h => h.GetIndexedSize(_activeContext))
            .Returns(stackSize);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.BLOCKCONTEXT_NARGS_INDEX))
            .Returns(SmalltalkOOP.FromSmallInteger(nargs));

        var methodContext = new SmalltalkOOP(500);
        var methodOOP = new SmalltalkOOP(400);
        _mockHelpers.Setup(h => h.FindMethodContext(_activeContext))
            .Returns(methodContext);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodContext, VMConstants.CONTEXT_METHOD_INDEX))
            .Returns(methodOOP);
    }

    #region Push Tests

    [Fact]
    public void Push_ValidValue_IncreasesStackPointer()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 5, stackSize: 20, numArgs: 2, numTemps: 3);
        var valueToPush = SmalltalkOOP.FromSmallInteger(42);

        // Act
        stackManager.Push(valueToPush);

        // Assert
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 5, valueToPush), Times.Once);
        _mockHeapManager.Verify(h => h.SetInstanceVariable(
            _activeContext,
            VMConstants.CONTEXT_STACKP_INDEX,
            SmalltalkOOP.FromSmallInteger(6)), Times.Once);
    }

    [Fact]
    public void Push_MultipleValues_IncreasesStackPointerCorrectly()
    {
        // Arrange
        var stackManager = CreateStackManager();
        int currentSP = 5;
        SetupMethodContext(stackPointer: currentSP, stackSize: 20, numArgs: 2, numTemps: 3);

        // Setup dynamic SP updates
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX))
            .Returns(() => SmalltalkOOP.FromSmallInteger(currentSP));
        _mockHeapManager.Setup(h => h.SetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX, It.IsAny<SmalltalkOOP>()))
            .Callback<SmalltalkOOP, int, SmalltalkOOP>((ctx, idx, val) => currentSP = val.AsSmallInteger());

        var value1 = SmalltalkOOP.FromSmallInteger(1);
        var value2 = SmalltalkOOP.FromSmallInteger(2);
        var value3 = SmalltalkOOP.FromSmallInteger(3);

        // Act
        stackManager.Push(value1);
        stackManager.Push(value2);
        stackManager.Push(value3);

        // Assert
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 5, value1), Times.Once);
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 6, value2), Times.Once);
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 7, value3), Times.Once);
        Assert.Equal(8, currentSP);
    }

    [Fact]
    public void Push_StackOverflow_ThrowsException()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 20, stackSize: 20, numArgs: 2, numTemps: 3);
        var valueToPush = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stackManager.Push(valueToPush));
    }

    [Fact]
    public void Push_InvalidContext_ThrowsException()
    {
        // Arrange
        var stackManager = CreateStackManager();
        _mockHeapManager.Setup(h => h.GetObjectFormat(_activeContext))
            .Returns(ObjectFormat.ByteIndexed);
        _mockHelpers.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>()))
            .Returns("ByteArray");
        _mockHelpers.Setup(h => h.GetClass(_activeContext))
            .Returns(new SmalltalkOOP(999));

        var valueToPush = SmalltalkOOP.FromSmallInteger(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stackManager.Push(valueToPush));
    }

    [Fact]
    public void Push_WithSelectorTraceEnabled_IncrementsCounter()
    {
        // Arrange
        _selectorTraceEnabled = true;
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 5, stackSize: 20, numArgs: 2, numTemps: 3);
        var valueToPush = SmalltalkOOP.FromSmallInteger(42);

        // Act
        stackManager.Push(valueToPush);

        // Assert
        Assert.Equal(1, _pushTraceCounter);
    }

    [Fact]
    public void Push_SmallInteger_PushesCorrectly()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 0, stackSize: 10, numArgs: 0, numTemps: 0);
        var intValue = SmalltalkOOP.FromSmallInteger(12345);

        // Act
        stackManager.Push(intValue);

        // Assert
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 0, intValue), Times.Once);
    }

    [Fact]
    public void Push_ObjectOOP_PushesCorrectly()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 0, stackSize: 10, numArgs: 0, numTemps: 0);
        var objectOOP = new SmalltalkOOP(600);

        // Act
        stackManager.Push(objectOOP);

        // Assert
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 0, objectOOP), Times.Once);
    }

    #endregion

    #region Pop Tests

    [Fact]
    public void Pop_ValidStack_ReturnsTopValue()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 6, stackSize: 20, numArgs: 2, numTemps: 3);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 5))
            .Returns(expectedValue);

        // Act
        var result = stackManager.Pop();

        // Assert
        Assert.Equal(expectedValue, result);
        _mockHeapManager.Verify(h => h.SetInstanceVariable(
            _activeContext,
            VMConstants.CONTEXT_STACKP_INDEX,
            SmalltalkOOP.FromSmallInteger(5)), Times.Once);
    }

    [Fact]
    public void Pop_MultipleValues_ReturnsInReverseOrder()
    {
        // Arrange
        var stackManager = CreateStackManager();
        int currentSP = 8;
        SetupMethodContext(stackPointer: currentSP, stackSize: 20, numArgs: 2, numTemps: 3);

        var value1 = SmalltalkOOP.FromSmallInteger(1);
        var value2 = SmalltalkOOP.FromSmallInteger(2);
        var value3 = SmalltalkOOP.FromSmallInteger(3);

        // Setup dynamic SP updates
        _mockHeapManager.Setup(h => h.GetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX))
            .Returns(() => SmalltalkOOP.FromSmallInteger(currentSP));
        _mockHeapManager.Setup(h => h.SetInstanceVariable(_activeContext, VMConstants.CONTEXT_STACKP_INDEX, It.IsAny<SmalltalkOOP>()))
            .Callback<SmalltalkOOP, int, SmalltalkOOP>((ctx, idx, val) => currentSP = val.AsSmallInteger());

        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 7)).Returns(value3);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 6)).Returns(value2);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 5)).Returns(value1);

        // Act
        var pop1 = stackManager.Pop();
        var pop2 = stackManager.Pop();
        var pop3 = stackManager.Pop();

        // Assert
        Assert.Equal(value3, pop1);
        Assert.Equal(value2, pop2);
        Assert.Equal(value1, pop3);
        Assert.Equal(5, currentSP);
    }

    [Fact]
    public void Pop_StackUnderflow_ThrowsException()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 5, stackSize: 20, numArgs: 2, numTemps: 3);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stackManager.Pop());
    }

    [Fact]
    public void Pop_BlockContext_UsesNargsAsInitialSP()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupBlockContext(stackPointer: 3, stackSize: 20, nargs: 2);
        var expectedValue = SmalltalkOOP.FromSmallInteger(99);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 2))
            .Returns(expectedValue);

        // Act
        var result = stackManager.Pop();

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void Pop_BlockContext_StackUnderflow_ThrowsException()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupBlockContext(stackPointer: 2, stackSize: 20, nargs: 2);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stackManager.Pop());
    }

    #endregion

    #region Peek Tests

    [Fact]
    public void Peek_ValidStack_ReturnsTopWithoutPopping()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 6, stackSize: 20, numArgs: 2, numTemps: 3);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 5))
            .Returns(expectedValue);

        // Act
        var result = stackManager.Peek();

        // Assert
        Assert.Equal(expectedValue, result);
        // Verify SP was NOT modified
        _mockHeapManager.Verify(h => h.SetInstanceVariable(
            _activeContext,
            VMConstants.CONTEXT_STACKP_INDEX,
            It.IsAny<SmalltalkOOP>()), Times.Never);
    }

    [Fact]
    public void Peek_EmptyStack_ThrowsException()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 0, stackSize: 20, numArgs: 0, numTemps: 0);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stackManager.Peek());
    }

    [Fact]
    public void Peek_MultipleCalls_ReturnsSameValue()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 6, stackSize: 20, numArgs: 2, numTemps: 3);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 5))
            .Returns(expectedValue);

        // Act
        var result1 = stackManager.Peek();
        var result2 = stackManager.Peek();
        var result3 = stackManager.Peek();

        // Assert
        Assert.Equal(expectedValue, result1);
        Assert.Equal(expectedValue, result2);
        Assert.Equal(expectedValue, result3);
    }

    #endregion

    #region Boundary Condition Tests

    [Fact]
    public void Push_AtBoundary_Succeeds()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 19, stackSize: 20, numArgs: 2, numTemps: 3);
        var valueToPush = SmalltalkOOP.FromSmallInteger(42);

        // Act
        stackManager.Push(valueToPush);

        // Assert
        _mockHeapManager.Verify(h => h.SetIndexedOOP(_activeContext, 19, valueToPush), Times.Once);
    }

    [Fact]
    public void Pop_AtMinimum_Succeeds()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 6, stackSize: 20, numArgs: 2, numTemps: 3);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 5))
            .Returns(expectedValue);

        // Act
        var result = stackManager.Pop();

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void Peek_AtMinimum_Succeeds()
    {
        // Arrange
        var stackManager = CreateStackManager();
        SetupMethodContext(stackPointer: 1, stackSize: 20, numArgs: 0, numTemps: 0);
        var expectedValue = SmalltalkOOP.FromSmallInteger(42);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(_activeContext, 0))
            .Returns(expectedValue);

        // Act
        var result = stackManager.Peek();

        // Assert
        Assert.Equal(expectedValue, result);
    }

    #endregion
}
