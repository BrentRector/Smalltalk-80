using Moq;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for Control primitives (80-89): blocks, perform, process management, cache.
/// This is the highest priority test file with 50+ tests.
/// </summary>
[Trait("Category", "Primitives")]
public class ControlPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 80: BlockCopy - 15 tests

    [Fact]
    public void BlockCopy_ZeroArguments_CreatesBlockContext()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        // Setup for block creation
        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10)); // Not BlockContext class
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(0)], // 0 args
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, newBlockContextOOP);
    }

    [Fact]
    public void BlockCopy_OneArgument_SetsNargsCorrectly()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(1)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(1)), Times.Once);
    }

    [Fact]
    public void BlockCopy_TwoArguments_SetsNargsCorrectly()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(2)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2)), Times.Once);
    }

    [Fact]
    public void BlockCopy_ThreeArguments_SetsNargsCorrectly()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(3)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(3)), Times.Once);
    }

    [Fact]
    public void BlockCopy_SetsStartPC()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(0)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // startpc = currentPC + 2 (skip Blue Book 2-byte LONG_JUMP)
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(102)), Times.Once);
    }

    [Fact]
    public void BlockCopy_SetsHomeContext()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(0)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, methodContextOOP), Times.Once);
    }

    // Note: BlockCopy_SetsArgStartIndex test removed - argStartIndex VM extension no longer used
    // Block arguments are now stored via STORE_TEMP bytecodes at block body start (Blue Book compliant)

    [Fact]
    public void BlockCopy_NestedBlock_UsesOuterBlocksHome()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var mockHeap = CreateMockHeapManager();
        var outerBlockContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var methodContextOOP = ObjectOOP(75); // The outer block's home
        var newBlockContextOOP = ObjectOOP(200);

        // Outer block is a BlockContext
        SetupClassOOP(mockHeap, outerBlockContextOOP, blockContextClassOOP);
        // Outer block's home is the MethodContext
        SetupInstanceVariable(mockHeap, outerBlockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, methodContextOOP);
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            outerBlockContextOOP,
            [SmallInt(0)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // Nested block should point to same home as outer block
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, methodContextOOP), Times.Once);
    }

    [Fact]
    public void BlockCopy_InitializesCallerToNil()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var heap = new SmalltalkHeapManager();
        var metadata = CreateTestMetadata(heap);
        var nilOOP = metadata.KnownObjects.Nil;

        var mockHeap = CreateMockHeapManager();
        var methodContextOOP = ObjectOOP(100);
        var blockContextClassOOP = ObjectOOP(50);
        var newBlockContextOOP = ObjectOOP(200);

        SetupClassOOP(mockHeap, methodContextOOP, ObjectOOP(10));
        mockHeap.Setup(h => h.AllocateInstanceOf(blockContextClassOOP, 32)).Returns(newBlockContextOOP);

        var mockRuntimeAccess = CreateMockRuntimeServices(mockHeap);
        mockRuntimeAccess.Setup(r => r.GetClass("BlockContext")).Returns(blockContextClassOOP);

        // Blue Book: blockCopy: takes 1 argument (nargs only)
        var context = CreateContext(
            methodContextOOP,
            [SmallInt(0)],
            mockHeap.Object,
            mockRuntimeAccess.Object,
            instructionPointer: 100,
            metadata: metadata);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(newBlockContextOOP, VMConstants.BLOCKCONTEXT_CALLER_INDEX, nilOOP), Times.Once);
    }

    [Fact]
    public void BlockCopy_WrongArgumentCount_Fails()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var heapManager = CreateMockHeapManager().Object;
        // Blue Book: blockCopy: takes exactly 1 argument (nargs only)
        var context = CreateContext(ObjectOOP(100), [SmallInt(0), SmallInt(5)], heapManager); // 2 args instead of 1

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void BlockCopy_NonSmallIntegerArgCount_Fails()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var heapManager = CreateMockHeapManager().Object;
        // Blue Book: blockCopy: takes 1 argument (nargs only), must be SmallInteger
        var context = CreateContext(ObjectOOP(100), [ObjectOOP(50)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    // Note: BlockCopy_NonSmallIntegerArgStartIndex_Fails test removed - argStartIndex parameter no longer exists

    [Fact]
    public void BlockCopy_NegativeArgCount_Fails()
    {
        var primitive = new ControlPrimitives.BlockCopy();
        var heapManager = CreateMockHeapManager().Object;
        // Blue Book: blockCopy: takes 1 argument (nargs only), must be non-negative
        var context = CreateContext(ObjectOOP(100), [SmallInt(-1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    // Note: BlockCopy_NegativeArgStartIndex_Fails test removed - argStartIndex parameter no longer exists

    #endregion

    #region Primitive 81: Value - 18 tests

    [Fact]
    public void Value_ZeroArguments_ActivatesBlock()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(0));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockContext.Verify(c => c.SetActiveContext(blockContextOOP), Times.Once);
    }

    [Fact]
    public void Value_OneArgument_PushesToBlockContextStack()
    {
        // Blue Book: Arguments are pushed to BlockContext stack, not stored directly in home
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);
        var argValue = SmallInt(42);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(1));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [argValue],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // BLUE BOOK: Argument pushed to BlockContext stack at index 0
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 0, argValue), Times.Once);
    }

    [Fact]
    public void Value_TwoArguments_PushesAllToBlockContextStack()
    {
        // Blue Book: Arguments are pushed to BlockContext stack, not stored directly in home
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);
        var arg1 = SmallInt(10);
        var arg2 = SmallInt(20);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [arg1, arg2],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // BLUE BOOK: Arguments pushed to BlockContext stack at indices 0 and 1
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 0, arg1), Times.Once);
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 1, arg2), Times.Once);
    }

    [Fact]
    public void Value_ThreeArguments_PushesAllToBlockContextStack()
    {
        // Blue Book: Arguments are pushed to BlockContext stack
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);
        var arg1 = SmallInt(10);
        var arg2 = SmallInt(20);
        var arg3 = SmallInt(30);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(3));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [arg1, arg2, arg3],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // BLUE BOOK: Arguments pushed to BlockContext stack at indices 0, 1, 2
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 0, arg1), Times.Once);
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 1, arg2), Times.Once);
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 2, arg3), Times.Once);
    }

    [Fact]
    public void Value_SetsPCToStartPC()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(0));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(75));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_PC_INDEX, SmallInt(75)), Times.Once);
    }

    [Fact]
    public void Value_SetsStackPointer()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [SmallInt(1), SmallInt(2)],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmallInt(2)), Times.Once);
    }

    [Fact]
    public void Value_SetsCaller()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(0));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        mockHeap.Verify(h => h.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_CALLER_INDEX, activeContextOOP), Times.Once);
    }

    [Fact]
    public void Value_ArgumentCountMismatch_TooFew_Fails()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2));

        var context = CreateContext(
            blockContextOOP,
            [SmallInt(1)], // Only 1 argument, block expects 2
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Value_ArgumentCountMismatch_TooMany_Fails()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(1));

        var context = CreateContext(
            blockContextOOP,
            [SmallInt(1), SmallInt(2)], // 2 arguments, block expects 1
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Value_SmallIntegerReceiver_Fails()
    {
        var primitive = new ControlPrimitives.Value();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Value_TooManyArguments_Fails()
    {
        var primitive = new ControlPrimitives.Value();
        var heapManager = CreateMockHeapManager().Object;
        var context = CreateContext(
            ObjectOOP(100),
            [SmallInt(1), SmallInt(2), SmallInt(3), SmallInt(4)], // 4 arguments
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Value_NonSmallIntegerNargs_Fails()
    {
        var primitive = new ControlPrimitives.Value();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, ObjectOOP(50));

        var context = CreateContext(
            blockContextOOP,
            [],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 82: ValueWithArguments - 6 tests

    [Fact]
    public void ValueWithArguments_ValidArray_PushesArgsToBlockContextStack()
    {
        // Blue Book: Arguments are pushed to BlockContext stack
        var primitive = new ControlPrimitives.ValueWithArguments();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);
        var argsArrayOOP = ObjectOOP(400);
        var arg1 = SmallInt(10);
        var arg2 = SmallInt(20);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        SetupClassName(mockHeap, argsArrayOOP, "Array");
        SetupIndexedSize(mockHeap, argsArrayOOP, 2);
        SetupIndexedOOP(mockHeap, argsArrayOOP, 0, arg1);
        SetupIndexedOOP(mockHeap, argsArrayOOP, 1, arg2);

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [argsArrayOOP],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
        // BLUE BOOK: Arguments pushed to BlockContext stack at indices 0, 1
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 0, arg1), Times.Once);
        mockHeap.Verify(h => h.SetIndexedOOP(blockContextOOP, 1, arg2), Times.Once);
    }

    [Fact]
    public void ValueWithArguments_EmptyArray_ZeroArgBlock_Succeeds()
    {
        var primitive = new ControlPrimitives.ValueWithArguments();
        var mockHeap = CreateMockHeapManager();
        var mockContext = new Mock<IContextManager>();

        var blockContextOOP = ObjectOOP(100);
        var homeContextOOP = ObjectOOP(200);
        var activeContextOOP = ObjectOOP(300);
        var argsArrayOOP = ObjectOOP(400);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(0));
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX, homeContextOOP);
        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmallInt(50));

        SetupClassName(mockHeap, argsArrayOOP, "Array");
        SetupIndexedSize(mockHeap, argsArrayOOP, 0);

        mockContext.Setup(c => c.ActiveContext).Returns(activeContextOOP);

        var context = CreateContext(
            blockContextOOP,
            [argsArrayOOP],
            mockHeap.Object,
            contextManager: mockContext.Object);

        var result = primitive.Execute(context);

        AssertSuccess(result);
    }

    [Fact]
    public void ValueWithArguments_ArraySizeMismatch_Fails()
    {
        var primitive = new ControlPrimitives.ValueWithArguments();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);
        var argsArrayOOP = ObjectOOP(400);

        SetupInstanceVariable(mockHeap, blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmallInt(2));
        SetupClassName(mockHeap, argsArrayOOP, "Array");
        SetupIndexedSize(mockHeap, argsArrayOOP, 3); // Array has 3, block expects 2

        var context = CreateContext(
            blockContextOOP,
            [argsArrayOOP],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void ValueWithArguments_NotAnArray_Fails()
    {
        var primitive = new ControlPrimitives.ValueWithArguments();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);
        var notArrayOOP = ObjectOOP(400);

        SetupClassName(mockHeap, notArrayOOP, "String");

        var context = CreateContext(
            blockContextOOP,
            [notArrayOOP],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void ValueWithArguments_SmallIntegerArgument_Fails()
    {
        var primitive = new ControlPrimitives.ValueWithArguments();
        var mockHeap = CreateMockHeapManager();

        var blockContextOOP = ObjectOOP(100);

        var context = CreateContext(
            blockContextOOP,
            [SmallInt(42)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void ValueWithArguments_WrongArgumentCount_Fails()
    {
        var primitive = new ControlPrimitives.ValueWithArguments();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [], // No argument
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 83: Perform - 5 tests

    [Fact]
    public void Perform_ValidSelector_SendsMessage()
    {
        var primitive = new ControlPrimitives.Perform();
        var mockHeap = CreateMockHeapManager();
        var mockSender = new Mock<IPrimitiveMessageSender>();

        var receiverOOP = ObjectOOP(100);
        var selectorOOP = ObjectOOP(200);
        var resultOOP = SmallInt(42);

        mockSender.Setup(s => s.SendMessage(receiverOOP, selectorOOP, Array.Empty<SmalltalkOOP>()))
            .Returns(resultOOP);

        var context = CreateContext(
            receiverOOP,
            [selectorOOP],
            mockHeap.Object,
            messageSender: mockSender.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultOOP);
    }

    [Fact]
    public void Perform_SmallIntegerSelector_Fails()
    {
        var primitive = new ControlPrimitives.Perform();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [SmallInt(42)],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Perform_NoMessageSender_Fails()
    {
        var primitive = new ControlPrimitives.Perform();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [ObjectOOP(200)],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Perform_WrongArgumentCount_Fails()
    {
        var primitive = new ControlPrimitives.Perform();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [], // No selector
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Perform_TooManyArguments_Fails()
    {
        var primitive = new ControlPrimitives.Perform();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [ObjectOOP(200), ObjectOOP(300)], // Two arguments
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 84: PerformWithArguments - 4 tests

    [Fact]
    public void PerformWithArguments_ValidSelectorAndArg_SendsMessage()
    {
        var primitive = new ControlPrimitives.PerformWithArguments();
        var mockHeap = CreateMockHeapManager();
        var mockSender = new Mock<IPrimitiveMessageSender>();

        var receiverOOP = ObjectOOP(100);
        var selectorOOP = ObjectOOP(200);
        var argOOP = SmallInt(5);
        var argsArrayOOP = ObjectOOP(300);  // Array containing the arguments
        var resultOOP = SmallInt(42);

        // Set up the Array to contain one element (argOOP)
        SetupIndexedSize(mockHeap, argsArrayOOP, 1);
        SetupIndexedOOP(mockHeap, argsArrayOOP, 0, argOOP);

        mockSender.Setup(s => s.SendMessage(receiverOOP, selectorOOP, new[] { argOOP }))
            .Returns(resultOOP);

        var context = CreateContext(
            receiverOOP,
            [selectorOOP, argsArrayOOP],  // Pass the Array, not the arg directly
            mockHeap.Object,
            messageSender: mockSender.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, resultOOP);
    }

    [Fact]
    public void PerformWithArguments_SmallIntegerSelector_Fails()
    {
        var primitive = new ControlPrimitives.PerformWithArguments();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [SmallInt(42), SmallInt(5)],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void PerformWithArguments_WrongArgumentCount_Fails()
    {
        var primitive = new ControlPrimitives.PerformWithArguments();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [ObjectOOP(200)], // Only selector, no argument
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void PerformWithArguments_NoMessageSender_Fails()
    {
        var primitive = new ControlPrimitives.PerformWithArguments();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [ObjectOOP(200), SmallInt(5)],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 89: FlushCache - 3 tests

    [Fact]
    public void FlushCache_ClearsAllCaches()
    {
        var primitive = new ControlPrimitives.FlushCache();
        var mockHeap = CreateMockHeapManager();
        var mockCache = new Mock<ICacheManager>();

        var receiverOOP = ObjectOOP(100);

        var context = CreateContext(
            receiverOOP,
            [],
            mockHeap.Object,
            cacheManager: mockCache.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, receiverOOP);
        mockCache.Verify(c => c.ClearMethodCache(), Times.Once);
        mockCache.Verify(c => c.InvalidateAllRuntimeCaches(), Times.Once);
    }

    [Fact]
    public void FlushCache_NoCacheManager_Fails()
    {
        var primitive = new ControlPrimitives.FlushCache();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            ObjectOOP(100),
            [],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void FlushCache_WithArguments_Fails()
    {
        var primitive = new ControlPrimitives.FlushCache();
        var mockHeap = CreateMockHeapManager();
        var mockCache = new Mock<ICacheManager>();

        var context = CreateContext(
            ObjectOOP(100),
            [SmallInt(1)],
            mockHeap.Object,
            cacheManager: mockCache.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 85: Signal - 4 tests

    [Fact]
    public void Signal_SmallIntegerReceiver_Fails()
    {
        var primitive = new ControlPrimitives.Signal();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Signal_WithArguments_Fails()
    {
        var primitive = new ControlPrimitives.Signal();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Signal_NullClassName_Fails()
    {
        var primitive = new ControlPrimitives.Signal();
        var mockHeap = CreateMockHeapManager();
        var semaphoreOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetClassName(semaphoreOOP)).Returns((string)null!);

        var mockRuntime = CreateMockRuntimeServices(mockHeap);
        var context = CreateContext(semaphoreOOP, [], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Signal_NonSmallIntegerExcessSignals_Fails()
    {
        var primitive = new ControlPrimitives.Signal();
        var mockHeap = CreateMockHeapManager();
        var metadata = CreateDefaultTestMetadata(mockHeap.Object);
        var semaphoreOOP = ObjectOOP(100);
        var semaphoreClassOOP = ObjectOOP(200);
        var nonSmallIntOOP = ObjectOOP(999);  // Non-SmallInteger OOP

        SetupClassName(mockHeap, semaphoreOOP, "Semaphore");
        mockHeap.Setup(h => h.GetClassOOP(semaphoreOOP)).Returns(semaphoreClassOOP);

        var mockRuntime = CreateMockRuntimeServices(mockHeap, metadata);
        // Setup field indices for Semaphore fields
        mockRuntime.Setup(r => r.GetFieldIndex(semaphoreClassOOP, "firstLink")).Returns(0);
        mockRuntime.Setup(r => r.GetFieldIndex(semaphoreClassOOP, "excessSignals")).Returns(1);

        // firstLink is nil (no processes waiting)
        var nilOOP = metadata.KnownObjects.Nil;  // Use actual nil from metadata
        mockHeap.Setup(h => h.GetInstanceVariable(semaphoreOOP, 0))
            .Returns(nilOOP);

        // excessSignals is not a SmallInteger (should cause failure)
        mockHeap.Setup(h => h.GetInstanceVariable(semaphoreOOP, 1))
            .Returns(nonSmallIntOOP);

        var context = CreateContext(semaphoreOOP, [], mockHeap.Object, mockRuntime.Object, metadata: metadata);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 86: Wait - 3 tests

    [Fact]
    public void Wait_SmallIntegerReceiver_Fails()
    {
        var primitive = new ControlPrimitives.Wait();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Wait_WithArguments_Fails()
    {
        var primitive = new ControlPrimitives.Wait();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Wait_NullClassName_Fails()
    {
        var primitive = new ControlPrimitives.Wait();
        var mockHeap = CreateMockHeapManager();
        var semaphoreOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetClassName(semaphoreOOP)).Returns((string)null!);

        var mockRuntime = CreateMockRuntimeServices(mockHeap);
        var context = CreateContext(semaphoreOOP, [], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 87: Resume - 3 tests

    [Fact]
    public void Resume_SmallIntegerReceiver_Fails()
    {
        var primitive = new ControlPrimitives.Resume();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Resume_WithArguments_Fails()
    {
        var primitive = new ControlPrimitives.Resume();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Resume_NullClassName_Fails()
    {
        var primitive = new ControlPrimitives.Resume();
        var mockHeap = CreateMockHeapManager();
        var processOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetClassName(processOOP)).Returns((string)null!);

        var mockRuntime = CreateMockRuntimeServices(mockHeap);
        var context = CreateContext(processOOP, [], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 88: Suspend - 3 tests

    [Fact]
    public void Suspend_SmallIntegerReceiver_Fails()
    {
        var primitive = new ControlPrimitives.Suspend();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(SmallInt(42), [], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Suspend_WithArguments_Fails()
    {
        var primitive = new ControlPrimitives.Suspend();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(ObjectOOP(100), [SmallInt(1)], heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void Suspend_NullClassName_Fails()
    {
        var primitive = new ControlPrimitives.Suspend();
        var mockHeap = CreateMockHeapManager();
        var processOOP = ObjectOOP(100);

        mockHeap.Setup(h => h.GetClassName(processOOP)).Returns((string)null!);

        var mockRuntime = CreateMockRuntimeServices(mockHeap);
        var context = CreateContext(processOOP, [], mockHeap.Object, mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
