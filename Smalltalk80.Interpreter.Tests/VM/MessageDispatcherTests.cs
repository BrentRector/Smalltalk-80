using Moq;
using Xunit;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.VM;

/// <summary>
/// Comprehensive unit tests for the MessageDispatcher component.
/// Tests method lookup in class hierarchy and primitive number extraction.
/// </summary>
[Trait("Category", "VM")]
public class MessageDispatcherTests
{
    private readonly Mock<ISmalltalkHeap> _mockHeapManager;
    private readonly Mock<IObjectMetadataAccessor> _mockHelpers;

    public MessageDispatcherTests()
    {
        _mockHeapManager = new Mock<ISmalltalkHeap>(MockBehavior.Loose);
        _mockHelpers = new Mock<IObjectMetadataAccessor>(MockBehavior.Loose);
        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        _mockHeapManager.Setup(h => h.GetObjectFormat(It.IsAny<SmalltalkOOP>()))
            .Returns(ObjectFormat.PointerIndexed);
    }

    private MessageDispatcher CreateDispatcher()
    {
        return new MessageDispatcher(_mockHeapManager.Object, _mockHelpers.Object);
    }

    #region LookupMethod Tests - Method Found in Receiver's Class

    [Fact]
    public void LookupMethod_MethodInReceiverClass_ReturnsMethod()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(1);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 0)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 0)).Returns(methodOOP);
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(4);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 0)).Returns((byte)'t');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 1)).Returns((byte)'e');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 2)).Returns((byte)'s');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 3)).Returns((byte)'t');

        // Act
        var result = dispatcher.LookupMethod(classOOP, "test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(methodOOP, result.MethodOOP);
    }

    [Fact]
    public void LookupMethod_MultipleMethods_FindsCorrectOne()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selector1OOP = new SmalltalkOOP(400);
        var selector2OOP = new SmalltalkOOP(401);
        var method1OOP = new SmalltalkOOP(500);
        var method2OOP = new SmalltalkOOP(501);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(2);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 0)).Returns(selector1OOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 1)).Returns(selector2OOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 0)).Returns(method1OOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 1)).Returns(method2OOP);

        // Setup selector1 as "foo"
        _mockHeapManager.Setup(h => h.GetIndexedSize(selector1OOP)).Returns(3);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector1OOP, 0)).Returns((byte)'f');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector1OOP, 1)).Returns((byte)'o');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector1OOP, 2)).Returns((byte)'o');

        // Setup selector2 as "bar"
        _mockHeapManager.Setup(h => h.GetIndexedSize(selector2OOP)).Returns(3);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector2OOP, 0)).Returns((byte)'b');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector2OOP, 1)).Returns((byte)'a');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selector2OOP, 2)).Returns((byte)'r');

        // Act
        var result = dispatcher.LookupMethod(classOOP, "bar");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(method2OOP, result.MethodOOP);
    }

    #endregion

    #region LookupMethod Tests - Method Found in Superclass

    [Fact]
    public void LookupMethod_MethodInSuperclass_ReturnsMethod()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var superclassOOP = new SmalltalkOOP(150);
        var methodDictOOP = new SmalltalkOOP(200);
        var superMethodDictOOP = new SmalltalkOOP(250);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);
        var methodOOP = new SmalltalkOOP(500);

        // Setup receiver class with empty method dict
        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("ChildClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(new SmalltalkOOP(301));
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(new SmalltalkOOP(301)))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(0);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(superclassOOP);

        // Setup superclass with the method
        _mockHelpers.Setup(h => h.GetClassName(superclassOOP)).Returns("ParentClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(superclassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(superMethodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(superMethodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(superMethodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(superMethodDictOOP)).Returns(1);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(superMethodDictOOP, 0)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 0)).Returns(methodOOP);

        // Setup selector as "inherited"
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(9);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, It.IsAny<int>()))
            .Returns<SmalltalkOOP, int>((_, i) => (byte)"inherited"[i]);

        // Act
        var result = dispatcher.LookupMethod(classOOP, "inherited");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(methodOOP, result.MethodOOP);
    }

    [Fact]
    public void LookupMethod_MethodInGrandparentClass_ReturnsMethod()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var childOOP = new SmalltalkOOP(100);
        var parentOOP = new SmalltalkOOP(150);
        var grandparentOOP = new SmalltalkOOP(175);
        var grandMethodDictOOP = new SmalltalkOOP(250);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);
        var methodOOP = new SmalltalkOOP(500);

        // Setup child with empty dict
        _mockHelpers.Setup(h => h.GetClassName(childOOP)).Returns("Child");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(childOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(childOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(parentOOP);

        // Setup parent with empty dict
        _mockHelpers.Setup(h => h.GetClassName(parentOOP)).Returns("Parent");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(parentOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(parentOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(grandparentOOP);

        // Setup grandparent with the method
        _mockHelpers.Setup(h => h.GetClassName(grandparentOOP)).Returns("Grandparent");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(grandparentOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(grandMethodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(grandMethodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(grandMethodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(grandMethodDictOOP)).Returns(1);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(grandMethodDictOOP, 0)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 0)).Returns(methodOOP);

        // Setup selector as "old"
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(3);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 0)).Returns((byte)'o');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 1)).Returns((byte)'l');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 2)).Returns((byte)'d');

        // Act
        var result = dispatcher.LookupMethod(childOOP, "old");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(methodOOP, result.MethodOOP);
    }

    #endregion

    #region LookupMethod Tests - Method Not Found

    [Fact]
    public void LookupMethod_MethodNotFound_ReturnsNull()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(0);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = dispatcher.LookupMethod(classOOP, "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupMethod_EmptyHierarchy_ReturnsNull()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = dispatcher.LookupMethod(classOOP, "test");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupMethod_MaxDepthExceeded_ReturnsNull()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classes = new SmalltalkOOP[25];
        for (int i = 0; i < 25; i++)
        {
            classes[i] = new SmalltalkOOP(100 + i);
        }

        // Setup circular-like reference that exceeds max depth
        for (int i = 0; i < 24; i++)
        {
            var classOOP = classes[i];
            _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns($"Class{i}");
            _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
                .Returns(SpecialObjectsArray.Instance.Nil);
            _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
                .Returns(classes[i + 1]);
        }

        // Last class returns nil
        _mockHelpers.Setup(h => h.GetClassName(classes[24])).Returns("Class24");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classes[24], VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classes[24], VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = dispatcher.LookupMethod(classes[0], "test");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region LookupMethod Tests - Nil Slots

    [Fact]
    public void LookupMethod_NilSelectorSlot_SkipsSlot()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(2);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 0))
            .Returns(SpecialObjectsArray.Instance.Nil); // Nil slot
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 1)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 1)).Returns(methodOOP);

        // Setup selector as "test"
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(4);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 0)).Returns((byte)'t');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 1)).Returns((byte)'e');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 2)).Returns((byte)'s');
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, 3)).Returns((byte)'t');

        // Act
        var result = dispatcher.LookupMethod(classOOP, "test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(methodOOP, result.MethodOOP);
    }

    [Fact]
    public void LookupMethod_NilValuesArray_TriesSuperclass()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var superclassOOP = new SmalltalkOOP(150);
        var methodDictOOP = new SmalltalkOOP(200);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(SpecialObjectsArray.Instance.Nil); // Nil values array
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(superclassOOP);
        _mockHelpers.Setup(h => h.GetClassName(superclassOOP)).Returns("SuperClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(superclassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(superclassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = dispatcher.LookupMethod(classOOP, "test");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region LookupMethod Tests - Invalid Format Errors

    [Fact]
    public void LookupMethod_InvalidMethodDictFormat_ThrowsException()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.ByteIndexed); // Invalid format
        _mockHelpers.Setup(h => h.GetClass(methodDictOOP)).Returns(new SmalltalkOOP(999));
        _mockHelpers.Setup(h => h.GetClassName(new SmalltalkOOP(999))).Returns("ByteArray");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => dispatcher.LookupMethod(classOOP, "test"));
    }

    [Fact]
    public void LookupMethod_InvalidValuesArrayFormat_ThrowsException()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.ByteIndexed); // Invalid format
        _mockHelpers.Setup(h => h.GetClass(valuesArrayOOP)).Returns(new SmalltalkOOP(999));
        _mockHelpers.Setup(h => h.GetClassName(new SmalltalkOOP(999))).Returns("ByteArray");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => dispatcher.LookupMethod(classOOP, "test"));
    }

    #endregion

    #region GetPrimitiveNumber Tests

    [Fact]
    public void GetPrimitiveNumber_MethodWithPrimitive_ReturnsPrimitiveNumber()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var methodOOP = new SmalltalkOOP(500);
        var expectedPrimitive = 42;

        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(10, false, 2, 7, 3, expectedPrimitive, false, false, 0));

        // Act
        var result = dispatcher.GetPrimitiveNumber(methodOOP);

        // Assert
        Assert.Equal(expectedPrimitive, result);
    }

    [Fact]
    public void GetPrimitiveNumber_MethodWithoutPrimitive_ReturnsZero()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(5, false, 1, 1, 1, 0, false, false, 0));

        // Act
        var result = dispatcher.GetPrimitiveNumber(methodOOP);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetPrimitiveNumber_QuickReturnSelfMethod_ReturnsZero()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(0, false, 0, 5, 0, 0, true, false, 0));

        // Act
        var result = dispatcher.GetPrimitiveNumber(methodOOP);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetPrimitiveNumber_QuickReturnFieldMethod_ReturnsZero()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(0, false, 3, 6, 0, 0, false, true, 3));

        // Act
        var result = dispatcher.GetPrimitiveNumber(methodOOP);

        // Assert
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(110)]
    [InlineData(255)]
    public void GetPrimitiveNumber_VariousPrimitiveNumbers_ReturnsCorrectValue(int primitiveNumber)
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var methodOOP = new SmalltalkOOP(500);

        _mockHelpers.Setup(h => h.ParseMethodHeader(methodOOP))
            .Returns(new MethodHeaderInfo(10, false, 2, 7, 2, primitiveNumber, false, false, 0));

        // Act
        var result = dispatcher.GetPrimitiveNumber(methodOOP);

        // Assert
        Assert.Equal(primitiveNumber, result);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void LookupMethod_EmptySelector_ReturnsNull()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(1);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 0)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(0); // Empty selector

        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX))
            .Returns(SpecialObjectsArray.Instance.Nil);

        // Act
        var result = dispatcher.LookupMethod(classOOP, "test");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupMethod_LongSelector_FindsCorrectly()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var classOOP = new SmalltalkOOP(100);
        var methodDictOOP = new SmalltalkOOP(200);
        var valuesArrayOOP = new SmalltalkOOP(300);
        var selectorOOP = new SmalltalkOOP(400);
        var methodOOP = new SmalltalkOOP(500);
        var longSelector = "thisIsAVeryLongSelectorWithManyColons:and:more:arguments:";

        _mockHelpers.Setup(h => h.GetClassName(classOOP)).Returns("TestClass");
        _mockHeapManager.Setup(h => h.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX))
            .Returns(methodDictOOP);
        _mockHeapManager.Setup(h => h.GetInstanceVariable(methodDictOOP, 0))
            .Returns(valuesArrayOOP);
        _mockHeapManager.Setup(h => h.GetObjectFormat(methodDictOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetObjectFormat(valuesArrayOOP))
            .Returns(ObjectFormat.PointerIndexed);
        _mockHeapManager.Setup(h => h.GetIndexedSize(methodDictOOP)).Returns(1);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(methodDictOOP, 0)).Returns(selectorOOP);
        _mockHeapManager.Setup(h => h.GetIndexedOOP(valuesArrayOOP, 0)).Returns(methodOOP);
        _mockHeapManager.Setup(h => h.GetIndexedSize(selectorOOP)).Returns(longSelector.Length);
        _mockHeapManager.Setup(h => h.GetIndexedByte(selectorOOP, It.IsAny<int>()))
            .Returns<SmalltalkOOP, int>((_, i) => (byte)longSelector[i]);

        // Act
        var result = dispatcher.LookupMethod(classOOP, longSelector);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(methodOOP, result.MethodOOP);
    }

    #endregion
}
