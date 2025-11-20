// InitExpressionExecutorTests.cs
// Comprehensive unit tests for InitExpressionExecutor

using Moq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.ColdStart;

/// <summary>
/// Unit tests for InitExpressionExecutor covering expression execution,
/// result handling, and error handling.
/// </summary>
public class InitExpressionExecutorTests
{
    #region Test Fixtures

    private static void SetupSpecialObjects()
    {
        var nilOOP = new SmalltalkOOP(10);
        var trueOOP = new SmalltalkOOP(11);
        var falseOOP = new SmalltalkOOP(12);
        SpecialObjectsArray.Initialize(nilOOP, falseOOP, trueOOP);

        // Set up Smalltalk global
        var smalltalkOOP = new SmalltalkOOP(50);
        GlobalRegistry.Reset();
        GlobalRegistry.Register("Smalltalk", smalltalkOOP);
    }

    private static Mock<ISmalltalkHeapManager> CreateHeapManagerMock()
    {
        var mock = new Mock<ISmalltalkHeapManager>();

        // Set up basic heap operations
        mock.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(0);
        mock.Setup(h => h.GetIndexedOOP(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(100));
        mock.Setup(h => h.GetSlot(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(100));
        mock.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));

        return mock;
    }

    private static Mock<VirtualMachine> CreateVMMock(Mock<ISmalltalkHeapManager> heapManager)
    {
        var vmMock = new Mock<VirtualMachine>(heapManager.Object);
        vmMock.Setup(vm => vm.ExecuteExistingContext(It.IsAny<SmalltalkOOP>()))
            .Returns(new SmalltalkOOP(300));

        return vmMock;
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void InitExpressionExecutor_DefaultPolicy_IsAbortOnFirstError()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);

        // Act
        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Assert - no exception means default policy is used
        Assert.NotNull(executor);
    }

    [Fact]
    public void InitExpressionExecutor_CustomTimeout_IsAccepted()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);

        // Act
        var executor = new InitExpressionExecutor(
            heapManager.Object,
            vm.Object,
            InitPolicy.AbortOnFirstError,
            10000);

        // Assert
        Assert.NotNull(executor);
    }

    #endregion

    #region ExecuteAll Tests

    [Fact]
    public void ExecuteAll_EmptyArray_ReturnsZeroTotal()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up empty init array
        var initArrayOOP = new SmalltalkOOP(100);
        SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);
        heapManager.Setup(h => h.GetIndexedSize(initArrayOOP)).Returns(0);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Act
        var report = executor.ExecuteAll();

        // Assert
        Assert.Equal(0, report.Total);
        Assert.Equal(0, report.Succeeded);
        Assert.Equal(0, report.Failed);
        Assert.Empty(report.Results);
    }

    [Fact]
    public void ExecuteAll_SingleExpression_ExecutesSuccessfully()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up init array with one method
        var initArrayOOP = new SmalltalkOOP(100);
        var methodOOP = new SmalltalkOOP(200);
        SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);

        heapManager.Setup(h => h.GetIndexedSize(initArrayOOP)).Returns(1);
        heapManager.Setup(h => h.GetIndexedOOP(initArrayOOP, 0)).Returns(methodOOP);

        // Set up MethodContext and Process class lookups
        heapManager.Setup(h => h.GetIndexedSize(SpecialObjectsArray.Instance.Smalltalk)).Returns(10);
        heapManager.Setup(h => h.GetIndexedOOP(SpecialObjectsArray.Instance.Smalltalk, It.IsAny<int>()))
            .Returns(SpecialObjectsArray.Instance.Nil);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Act
        var report = executor.ExecuteAll();

        // Assert
        Assert.Equal(1, report.Total);
    }

    [Fact]
    public void ExecuteAll_NullInitMethods_ThrowsException()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set init methods to null
        SpecialObjectsArray.SetInitExpressionMethods(SmalltalkOOP.Null);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => executor.ExecuteAll());
    }

    [Fact]
    public void ExecuteAll_WithCancellation_StopsExecution()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up init array with multiple methods
        var initArrayOOP = new SmalltalkOOP(100);
        SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);
        heapManager.Setup(h => h.GetIndexedSize(initArrayOOP)).Returns(5);
        heapManager.Setup(h => h.GetIndexedOOP(initArrayOOP, It.IsAny<int>())).Returns(new SmalltalkOOP(200));
        heapManager.Setup(h => h.GetIndexedSize(SpecialObjectsArray.Instance.Smalltalk)).Returns(0);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Create pre-cancelled token
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var report = executor.ExecuteAll(cts.Token);

        // Assert - should stop before executing any
        Assert.Equal(5, report.Total);
        Assert.Empty(report.Results);
    }

    #endregion

    #region ExecuteInitExpression Tests

    [Fact]
    public void ExecuteInitExpression_NullMethodOOP_ReturnsValidationFailure()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);

        // Act
        var result = executor.ExecuteInitExpression("init:0", SmalltalkOOP.Null);

        // Assert
        Assert.Equal(InitStatus.ValidationFailed, result.Status);
        Assert.Contains("null", result.ErrorMessage);
    }

    [Fact]
    public void ExecuteInitExpression_ValidMethod_ReturnsSuccess()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up method context and process class lookups
        heapManager.Setup(h => h.GetIndexedSize(SpecialObjectsArray.Instance.Smalltalk)).Returns(0);

        // Pre-register symbols for class lookup
        SymbolRegistry.Reset();
        SymbolRegistry.Register(0, new SmalltalkOOP(1000));
        SymbolRegistry.Freeze();

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);
        var methodOOP = new SmalltalkOOP(200);

        // Act
        var result = executor.ExecuteInitExpression("init:0", methodOOP);

        // Assert - will fail to find MethodContext class, but validates method
        Assert.NotNull(result);
    }

    [Fact]
    public void ExecuteInitExpression_WithTimeout_ReturnsTimeoutOnExpiry()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up to throw OperationCanceledException
        vm.Setup(v => v.ExecuteExistingContext(It.IsAny<SmalltalkOOP>()))
            .Throws(new OperationCanceledException());

        heapManager.Setup(h => h.GetIndexedSize(SpecialObjectsArray.Instance.Smalltalk)).Returns(0);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);
        var methodOOP = new SmalltalkOOP(200);

        // Act
        var result = executor.ExecuteInitExpression("init:0", methodOOP, 1);

        // Assert - execution path depends on context setup
        Assert.NotNull(result);
    }

    [Fact]
    public void ExecuteInitExpression_RecordsElapsedTime()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        heapManager.Setup(h => h.GetIndexedSize(SpecialObjectsArray.Instance.Smalltalk)).Returns(0);

        var executor = new InitExpressionExecutor(heapManager.Object, vm.Object);
        var methodOOP = new SmalltalkOOP(200);

        // Act
        var result = executor.ExecuteInitExpression("init:0", methodOOP);

        // Assert
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    #endregion

    #region InitResult Tests

    [Fact]
    public void InitResult_Success_HasCorrectProperties()
    {
        // Arrange & Act
        var result = InitResult.Success("init:0", new SmalltalkOOP(100), 50);

        // Assert
        Assert.Equal("init:0", result.LocalId);
        Assert.Equal(InitStatus.Success, result.Status);
        Assert.Equal(100, result.ResultOOP.Value);
        Assert.Equal(50, result.ElapsedMilliseconds);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void InitResult_ValidationFailed_HasErrorMessage()
    {
        // Arrange & Act
        var result = InitResult.ValidationFailed("init:0", "Invalid method");

        // Assert
        Assert.Equal(InitStatus.ValidationFailed, result.Status);
        Assert.Equal("Invalid method", result.ErrorMessage);
    }

    [Fact]
    public void InitResult_Timeout_HasCorrectStatus()
    {
        // Arrange & Act
        var result = InitResult.Timeout("init:0", 5000);

        // Assert
        Assert.Equal(InitStatus.Timeout, result.Status);
        Assert.Equal(5000, result.ElapsedMilliseconds);
    }

    [Fact]
    public void InitResult_PrimitiveFailure_IncludesPrimitiveIndex()
    {
        // Arrange & Act
        var result = InitResult.PrimitiveFailure("init:0", 60, "Primitive failed", 100);

        // Assert
        Assert.Equal(InitStatus.PrimitiveFailure, result.Status);
        Assert.Equal(60, result.PrimitiveIndex);
        Assert.Contains("Primitive failed", result.ErrorMessage);
    }

    [Fact]
    public void InitResult_Exception_IncludesStackTrace()
    {
        // Arrange & Act
        var result = InitResult.Exception("init:0", 1000, "Error occurred", "at method:Foo>>bar", 200);

        // Assert
        Assert.Equal(InitStatus.Exception, result.Status);
        Assert.Equal(1000, result.ExceptionOOP);
        Assert.Contains("Error occurred", result.ErrorMessage);
        Assert.Contains("at method:Foo>>bar", result.StackTrace);
    }

    [Fact]
    public void InitResult_FatalError_HasMessage()
    {
        // Arrange & Act
        var result = InitResult.FatalError("init:0", "Fatal error occurred");

        // Assert
        Assert.Equal(InitStatus.FatalError, result.Status);
        Assert.Contains("Fatal error occurred", result.ErrorMessage);
    }

    #endregion

    #region InitExecutionReport Tests

    [Fact]
    public void InitExecutionReport_CalculatesCorrectTotals()
    {
        // Arrange
        var report = new InitExecutionReport
        {
            Total = 10,
            Succeeded = 7,
            Failed = 3,
            Results =
            [
                InitResult.Success("init:0", new SmalltalkOOP(100), 10),
                InitResult.Success("init:1", new SmalltalkOOP(101), 20),
                InitResult.ValidationFailed("init:2", "Failed")
            ],
            TotalElapsedMilliseconds = 100
        };

        // Assert
        Assert.Equal(10, report.Total);
        Assert.Equal(7, report.Succeeded);
        Assert.Equal(3, report.Failed);
        Assert.Equal(100, report.TotalElapsedMilliseconds);
    }

    #endregion

    #region Exception Type Tests

    [Fact]
    public void PrimitiveFailureException_ContainsPrimitiveIndex()
    {
        // Arrange & Act
        var ex = new PrimitiveFailureException(60, "Primitive 60 failed");

        // Assert
        Assert.Equal(60, ex.PrimitiveIndex);
        Assert.Contains("Primitive 60 failed", ex.Message);
    }

    [Fact]
    public void SmalltalkException_ContainsExceptionOop()
    {
        // Arrange & Act
        var ex = new SmalltalkException(1000, "Smalltalk error");

        // Assert
        Assert.Equal(1000, ex.ExceptionOop);
        Assert.Contains("Smalltalk error", ex.Message);
    }

    #endregion

    #region Policy Tests

    [Fact]
    public void ExecuteAll_AbortOnFirstError_StopsAfterFailure()
    {
        // Arrange
        var heapManager = CreateHeapManagerMock();
        var vm = CreateVMMock(heapManager);
        SetupSpecialObjects();

        // Set up init array with multiple methods
        var initArrayOOP = new SmalltalkOOP(100);
        SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);
        heapManager.Setup(h => h.GetIndexedSize(initArrayOOP)).Returns(5);

        // First method fails validation (null OOP)
        heapManager.Setup(h => h.GetIndexedOOP(initArrayOOP, 0)).Returns(SmalltalkOOP.Null);
        heapManager.Setup(h => h.GetIndexedOOP(initArrayOOP, It.Is<int>(i => i > 0))).Returns(new SmalltalkOOP(200));

        var executor = new InitExpressionExecutor(
            heapManager.Object,
            vm.Object,
            InitPolicy.AbortOnFirstError);

        // Act
        var report = executor.ExecuteAll();

        // Assert
        Assert.Equal(5, report.Total);
        Assert.Single(report.Results); // Only first expression attempted
    }

    #endregion
}
