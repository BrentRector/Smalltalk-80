using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Comprehensive tests for the SCompiledMethod implementation.
/// </summary>
public class TestSCompiledMethod
{
    /// <summary>
    /// Tests basic compiled method creation.
    /// </summary>
    [Fact]
    public void TestCreation()
    {
        // Arrange
        var methodClass = new SClass("CompiledMethod", 0);

        // Act
        var method = new SCompiledMethod(methodClass);

        // Assert
        Assert.Equal(methodClass, method.Klass);
        Assert.NotNull(method.Literals);
        Assert.Empty(method.Literals);
        Assert.NotNull(method.Bytecode);
        Assert.Empty(method.Bytecode);
        Assert.Equal(0, method.Header);
        Assert.Equal(0, method.PrimitiveIndex);
        Assert.Null(method.Selector);
    }

    /// <summary>
    /// Tests header encoding and decoding for arguments.
    /// </summary>
    [Fact]
    public void TestHeaderNumArgs()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act & Assert
        for (int numArgs = 0; numArgs <= 31; numArgs++)
        {
            method.Header = SCompiledMethod.MakeHeader(numArgs, 0, 0, false, 0);
            Assert.Equal(numArgs, method.NumArgs);
        }
    }

    /// <summary>
    /// Tests header encoding and decoding for temporaries.
    /// </summary>
    [Fact]
    public void TestHeaderNumTemps()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act & Assert
        for (int numTemps = 0; numTemps <= 63; numTemps++)
        {
            method.Header = SCompiledMethod.MakeHeader(0, numTemps, 0, false, 0);
            Assert.Equal(numTemps, method.NumTemps);
        }
    }

    /// <summary>
    /// Tests header encoding and decoding for literals.
    /// </summary>
    [Fact]
    public void TestHeaderNumLiterals()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act & Assert
        for (int numLiterals = 0; numLiterals <= 63; numLiterals++)
        {
            method.Header = SCompiledMethod.MakeHeader(0, 0, numLiterals, false, 0);
            Assert.Equal(numLiterals, method.NumLiterals);
        }
    }

    /// <summary>
    /// Tests header encoding for primitive flag.
    /// </summary>
    [Fact]
    public void TestHeaderHasPrimitive()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act & Assert
        method.Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 0);
        Assert.False(method.HasPrimitive);

        method.Header = SCompiledMethod.MakeHeader(0, 0, 0, true, 0);
        Assert.True(method.HasPrimitive);
    }

    /// <summary>
    /// Tests header encoding and decoding for stack size.
    /// </summary>
    [Fact]
    public void TestHeaderStackSize()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act & Assert
        for (int stackSize = 0; stackSize <= 31; stackSize++)
        {
            method.Header = SCompiledMethod.MakeHeader(0, 0, 0, false, stackSize);
            Assert.Equal(stackSize, method.StackSize);
        }
    }

    /// <summary>
    /// Tests complex header with all fields set.
    /// </summary>
    [Fact]
    public void TestComplexHeader()
    {
        // Arrange
        var method = new SCompiledMethod(null);
        int numArgs = 3;
        int numTemps = 5;
        int numLiterals = 10;
        bool hasPrimitive = true;
        int stackSize = 8;

        // Act
        method.Header = SCompiledMethod.MakeHeader(numArgs, numTemps, numLiterals, hasPrimitive, stackSize);

        // Assert
        Assert.Equal(numArgs, method.NumArgs);
        Assert.Equal(numTemps, method.NumTemps);
        Assert.Equal(numLiterals, method.NumLiterals);
        Assert.Equal(hasPrimitive, method.HasPrimitive);
        Assert.Equal(stackSize, method.StackSize);
    }

    /// <summary>
    /// Tests MakeHeader validation for out-of-range arguments.
    /// </summary>
    [Fact]
    public void TestMakeHeaderValidation()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(-1, 0, 0, false, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(32, 0, 0, false, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, -1, 0, false, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, 64, 0, false, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, 0, -1, false, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, 0, 64, false, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, 0, 0, false, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SCompiledMethod.MakeHeader(0, 0, 0, false, 32));
    }

    /// <summary>
    /// Tests getting and setting literals.
    /// </summary>
    [Fact]
    public void TestLiterals()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Literals = new object[3]
        };
        var symbol = SSymbol.Intern("test");
        var number = 42;
        var str = "hello";

        // Act
        method.SetLiteral(0, symbol);
        method.SetLiteral(1, number);
        method.SetLiteral(2, str);

        // Assert
        Assert.Equal(symbol, method.GetLiteral(0));
        Assert.Equal(number, method.GetLiteral(1));
        Assert.Equal(str, method.GetLiteral(2));
    }

    /// <summary>
    /// Tests literal access with invalid indices.
    /// </summary>
    [Fact]
    public void TestLiteralBoundsChecking()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Literals = new object[2]
        };

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => method.GetLiteral(-1));
        Assert.Throws<IndexOutOfRangeException>(() => method.GetLiteral(2));
        Assert.Throws<IndexOutOfRangeException>(() => method.SetLiteral(-1, "test"));
        Assert.Throws<IndexOutOfRangeException>(() => method.SetLiteral(2, "test"));
    }

    /// <summary>
    /// Tests bytecode access.
    /// </summary>
    [Fact]
    public void TestBytecode()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Bytecode = new byte[] { 0x10, 0x20, 0x30, 0x40 }
        };

        // Act & Assert
        Assert.Equal(0x10, method.GetBytecode(0));
        Assert.Equal(0x20, method.GetBytecode(1));
        Assert.Equal(0x30, method.GetBytecode(2));
        Assert.Equal(0x40, method.GetBytecode(3));
    }

    /// <summary>
    /// Tests bytecode access with invalid PC.
    /// </summary>
    [Fact]
    public void TestBytecodeBoundsChecking()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Bytecode = new byte[] { 0x10, 0x20 }
        };

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => method.GetBytecode(-1));
        Assert.Throws<IndexOutOfRangeException>(() => method.GetBytecode(2));
    }

    /// <summary>
    /// Tests primitive index property.
    /// </summary>
    [Fact]
    public void TestPrimitiveIndex()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act
        method.PrimitiveIndex = 60; // SmallInteger addition primitive

        // Assert
        Assert.Equal(60, method.PrimitiveIndex);
    }

    /// <summary>
    /// Tests selector property.
    /// </summary>
    [Fact]
    public void TestSelector()
    {
        // Arrange
        var method = new SCompiledMethod(null);
        var selector = SSymbol.Intern("testMethod:with:");

        // Act
        method.Selector = selector;

        // Assert
        Assert.Equal(selector, method.Selector);
        Assert.Equal("testMethod:with:", method.Selector.Value);
    }

    /// <summary>
    /// Tests ToString representation.
    /// </summary>
    [Fact]
    public void TestToString()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Selector = SSymbol.Intern("myMethod:"),
            Header = SCompiledMethod.MakeHeader(1, 2, 3, false, 5),
            Bytecode = new byte[10]
        };

        // Act
        var str = method.ToString();

        // Assert
        Assert.Equal("CompiledMethod(myMethod:, args=1, temps=2, bytes=10)", str);
    }

    /// <summary>
    /// Tests ToString with unnamed method.
    /// </summary>
    [Fact]
    public void TestToStringUnnamed()
    {
        // Arrange
        var method = new SCompiledMethod(null)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 0),
            Bytecode = new byte[5]
        };

        // Act
        var str = method.ToString();

        // Assert
        Assert.Equal("CompiledMethod(unnamed, args=0, temps=0, bytes=5)", str);
    }

    /// <summary>
    /// Tests a realistic method example.
    /// </summary>
    [Fact]
    public void TestRealisticMethod()
    {
        // Arrange - Create a method for Point>>x
        var methodClass = new SClass("CompiledMethod", 0);
        var method = new SCompiledMethod(methodClass)
        {
            Selector = SSymbol.Intern("x"),
            Header = SCompiledMethod.MakeHeader(
                numArgs: 0,
                numTemps: 0,
                numLiterals: 0,
                hasPrimitive: false,
                stackSize: 1
            ),
            Bytecode = new byte[] { 0x00, 0x7C }, // Push receiver variable 0, return top
            Literals = [],
            PrimitiveIndex = 0
        };

        // Act & Assert
        Assert.Equal("x", method.Selector.Value);
        Assert.Equal(0, method.NumArgs);
        Assert.Equal(0, method.NumTemps);
        Assert.Equal(0, method.NumLiterals);
        Assert.False(method.HasPrimitive);
        Assert.Equal(1, method.StackSize);
        Assert.Equal(2, method.Bytecode.Length);
    }

    /// <summary>
    /// Tests a method with a primitive.
    /// </summary>
    [Fact]
    public void TestMethodWithPrimitive()
    {
        // Arrange - Create a method for SmallInteger>>+
        var method = new SCompiledMethod(null)
        {
            Selector = CommonSymbols.Plus,
            Header = SCompiledMethod.MakeHeader(
                numArgs: 1,
                numTemps: 0,
                numLiterals: 1,
                hasPrimitive: true,
                stackSize: 2
            ),
            PrimitiveIndex = 1, // Addition primitive
            Literals = [CommonSymbols.Plus],
            Bytecode = new byte[] { /* primitive failure code */ }
        };

        // Act & Assert
        Assert.Equal("+", method.Selector.Value);
        Assert.Equal(1, method.NumArgs);
        Assert.True(method.HasPrimitive);
        Assert.Equal(1, method.PrimitiveIndex);
        Assert.Contains(CommonSymbols.Plus, method.Literals);
    }

    /// <summary>
    /// Tests maximum values for header fields.
    /// </summary>
    [Fact]
    public void TestMaximumHeaderValues()
    {
        // Arrange
        var method = new SCompiledMethod(null);

        // Act
        method.Header = SCompiledMethod.MakeHeader(31, 63, 63, true, 31);

        // Assert
        Assert.Equal(31, method.NumArgs);
        Assert.Equal(63, method.NumTemps);
        Assert.Equal(63, method.NumLiterals);
        Assert.True(method.HasPrimitive);
        Assert.Equal(31, method.StackSize);
    }
}