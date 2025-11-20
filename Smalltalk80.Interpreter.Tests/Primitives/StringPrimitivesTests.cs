using Moq;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;

namespace Smalltalk80.Interpreter.Tests.Primitives;

/// <summary>
/// Tests for String primitives.
/// </summary>
[Trait("Category", "Primitives")]
public class StringPrimitivesTests : PrimitiveTestBase
{
    #region Primitive 105: StringReplace

    [Fact]
    public void StringReplace_CopyFromSource_Succeeds()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, destOOP, 10);
        SetupIndexedSize(mockHeap, srcOOP, 5);

        // Setup source bytes
        for (int i = 0; i < 5; i++)
        {
            SetupIndexedByte(mockHeap, srcOOP, i, (byte)('a' + i));
        }

        var context = CreateContext(
            destOOP,
            [SmallInt(3), SmallInt(7), srcOOP, SmallInt(1)], // copy 5 bytes from src to dest[3..7]
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, destOOP);
        // Verify copies were made
        mockHeap.Verify(h => h.SetIndexedByte(destOOP, 2, (byte)'a'), Times.Once);
        mockHeap.Verify(h => h.SetIndexedByte(destOOP, 3, (byte)'b'), Times.Once);
        mockHeap.Verify(h => h.SetIndexedByte(destOOP, 4, (byte)'c'), Times.Once);
        mockHeap.Verify(h => h.SetIndexedByte(destOOP, 5, (byte)'d'), Times.Once);
        mockHeap.Verify(h => h.SetIndexedByte(destOOP, 6, (byte)'e'), Times.Once);
    }

    [Fact]
    public void StringReplace_StartIndexZero_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, destOOP, 10);
        SetupIndexedSize(mockHeap, srcOOP, 5);

        var context = CreateContext(
            destOOP,
            [SmallInt(0), SmallInt(4), srcOOP, SmallInt(1)], // Invalid start index
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_StopBeforeStart_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, destOOP, 10);
        SetupIndexedSize(mockHeap, srcOOP, 5);

        var context = CreateContext(
            destOOP,
            [SmallInt(5), SmallInt(3), srcOOP, SmallInt(1)], // stop < start
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_DestIndexOutOfBounds_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, destOOP, 5);
        SetupIndexedSize(mockHeap, srcOOP, 5);

        var context = CreateContext(
            destOOP,
            [SmallInt(3), SmallInt(7), srcOOP, SmallInt(1)], // stop index 7 > dest size 5
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_SourceIndexOutOfBounds_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, destOOP, 10);
        SetupIndexedSize(mockHeap, srcOOP, 3);

        var context = CreateContext(
            destOOP,
            [SmallInt(1), SmallInt(5), srcOOP, SmallInt(1)], // need 5 bytes but source has only 3
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_SmallIntegerReceiver_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();
        var srcOOP = ObjectOOP(200);

        SetupIndexedSize(mockHeap, srcOOP, 5);

        var context = CreateContext(
            SmallInt(42),
            [SmallInt(1), SmallInt(5), srcOOP, SmallInt(1)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_SmallIntegerSource_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();
        var destOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, destOOP, 10);

        var context = CreateContext(
            destOOP,
            [SmallInt(1), SmallInt(5), SmallInt(42), SmallInt(1)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_NonSmallIntegerIndices_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();

        var destOOP = ObjectOOP(100);
        var srcOOP = ObjectOOP(200);

        var context = CreateContext(
            destOOP,
            [ObjectOOP(300), SmallInt(5), srcOOP, SmallInt(1)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void StringReplace_WrongArgumentCount_Fails()
    {
        var primitive = new StringPrimitives.StringReplace();
        var mockHeap = CreateMockHeapManager();
        var destOOP = ObjectOOP(100);

        var context = CreateContext(
            destOOP,
            [SmallInt(1), SmallInt(5), ObjectOOP(200)], // Only 3 arguments
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion

    #region Primitive 106: AsSymbol

    [Fact]
    public void AsSymbol_ValidString_ReturnsSymbol()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);
        var symbolOOP = ObjectOOP(200);

        // Setup string "hello"
        byte[] helloBytes = System.Text.Encoding.Latin1.GetBytes("hello");
        SetupIndexedSize(mockHeap, stringOOP, helloBytes.Length);
        for (int i = 0; i < helloBytes.Length; i++)
        {
            SetupIndexedByte(mockHeap, stringOOP, i, helloBytes[i]);
        }

        var mockRuntime = new Mock<RuntimeHeapAccessor>(mockHeap.Object);
        mockRuntime.Setup(r => r.InternSymbol("hello")).Returns(symbolOOP);

        var context = CreateContext(
            stringOOP,
            [],
            mockHeap.Object,
            mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertSuccessWithOOP(result, symbolOOP);
    }

    [Fact]
    public void AsSymbol_EmptyString_Fails()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, stringOOP, 0);

        var context = CreateContext(
            stringOOP,
            [],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsSymbol_SmallIntegerReceiver_Fails()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var heapManager = CreateMockHeapManager().Object;

        var context = CreateContext(
            SmallInt(42),
            [],
            heapManager);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsSymbol_SymbolNotFound_Fails()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        byte[] bytes = System.Text.Encoding.Latin1.GetBytes("unknown");
        SetupIndexedSize(mockHeap, stringOOP, bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            SetupIndexedByte(mockHeap, stringOOP, i, bytes[i]);
        }

        var mockRuntime = new Mock<RuntimeHeapAccessor>(mockHeap.Object);
        mockRuntime.Setup(r => r.InternSymbol("unknown")).Returns(Nil);

        var context = CreateContext(
            stringOOP,
            [],
            mockHeap.Object,
            mockRuntime.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsSymbol_NoRuntimeHeapAccess_Fails()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        byte[] bytes = System.Text.Encoding.Latin1.GetBytes("test");
        SetupIndexedSize(mockHeap, stringOOP, bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            SetupIndexedByte(mockHeap, stringOOP, i, bytes[i]);
        }

        var context = CreateContext(
            stringOOP,
            [],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    [Fact]
    public void AsSymbol_WithArguments_Fails()
    {
        var primitive = new StringPrimitives.AsSymbol();
        var mockHeap = CreateMockHeapManager();
        var stringOOP = ObjectOOP(100);

        SetupIndexedSize(mockHeap, stringOOP, 5);

        var context = CreateContext(
            stringOOP,
            [SmallInt(1)],
            mockHeap.Object);

        var result = primitive.Execute(context);

        AssertFailure(result);
    }

    #endregion
}
