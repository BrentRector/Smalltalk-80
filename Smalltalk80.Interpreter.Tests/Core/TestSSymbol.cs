using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Comprehensive tests for the SSymbol implementation.
/// </summary>
public class TestSSymbol : IDisposable
{
    /// <summary>
    /// Clears the symbol pool after each test to ensure isolation.
    /// </summary>
    public void Dispose()
    {
        SSymbol.ClearPool();
    }

    /// <summary>
    /// Tests that interning returns the same instance for identical strings.
    /// </summary>
    [Fact]
    public void TestInterning()
    {
        // Arrange
        var value = "testSymbol";

        // Act
        var symbol1 = SSymbol.Intern(value);
        var symbol2 = SSymbol.Intern(value);

        // Assert
        Assert.Same(symbol1, symbol2);
        Assert.True(ReferenceEquals(symbol1, symbol2));
    }

    /// <summary>
    /// Tests that different strings produce different symbols.
    /// </summary>
    [Fact]
    public void TestDifferentStringsProduceDifferentSymbols()
    {
        // Arrange & Act
        var symbol1 = SSymbol.Intern("symbol1");
        var symbol2 = SSymbol.Intern("symbol2");

        // Assert
        Assert.NotSame(symbol1, symbol2);
        Assert.False(ReferenceEquals(symbol1, symbol2));
        Assert.NotEqual(symbol1.Value, symbol2.Value);
    }

    /// <summary>
    /// Tests the Value property.
    /// </summary>
    [Fact]
    public void TestValueProperty()
    {
        // Arrange
        var value = "mySymbol";

        // Act
        var symbol = SSymbol.Intern(value);

        // Assert
        Assert.Equal(value, symbol.Value);
    }

    /// <summary>
    /// Tests that symbols are compared by reference, not value.
    /// </summary>
    [Fact]
    public void TestReferenceEquality()
    {
        // Arrange
        var symbol1 = SSymbol.Intern("test");
        var symbol2 = SSymbol.Intern("test");
        var symbol3 = SSymbol.Intern("other");

        // Act & Assert
        Assert.True(symbol1 == symbol2);
        Assert.True(symbol1.Equals(symbol2));
        Assert.False(symbol1 != symbol2);
        
        Assert.False(symbol1 == symbol3);
        Assert.False(symbol1.Equals(symbol3));
        Assert.True(symbol1 != symbol3);
    }

    /// <summary>
    /// Tests thread-safe interning.
    /// </summary>
    [Fact]
    public void TestThreadSafeInterning()
    {
        // Arrange
        var symbolNames = Enumerable.Range(0, 100).Select(i => $"symbol{i}").ToArray();
        var results = new SSymbol[symbolNames.Length];
        var tasks = new List<Task>();

        // Act - Create symbols from multiple threads
        for (int i = 0; i < symbolNames.Length; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                results[index] = SSymbol.Intern(symbolNames[index]);
            }));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert - Verify each symbol is unique and properly interned
        for (int i = 0; i < symbolNames.Length; i++)
        {
            var verifySymbol = SSymbol.Intern(symbolNames[i]);
            Assert.Same(results[i], verifySymbol);
        }
    }

    /// <summary>
    /// Tests AllSymbols returns all interned symbols.
    /// </summary>
    [Fact]
    public void TestAllSymbols()
    {
        // Arrange
        var symbol1 = SSymbol.Intern("symbol1");
        var symbol2 = SSymbol.Intern("symbol2");
        var symbol3 = SSymbol.Intern("symbol3");

        // Act
        var allSymbols = SSymbol.AllSymbols().ToList();

        // Assert
        Assert.Equal(3, allSymbols.Count);
        Assert.Contains(symbol1, allSymbols);
        Assert.Contains(symbol2, allSymbols);
        Assert.Contains(symbol3, allSymbols);
    }

    /// <summary>
    /// Tests IsInterned method.
    /// </summary>
    [Fact]
    public void TestIsInterned()
    {
        // Arrange
        SSymbol.Intern("interned");

        // Act & Assert
        Assert.True(SSymbol.IsInterned("interned"));
        Assert.False(SSymbol.IsInterned("notInterned"));
    }

    /// <summary>
    /// Tests ClearPool functionality.
    /// </summary>
    [Fact]
    public void TestClearPool()
    {
        // Arrange
        var symbol1 = SSymbol.Intern("test");
        Assert.True(SSymbol.IsInterned("test"));

        // Act
        SSymbol.ClearPool();
        var symbol2 = SSymbol.Intern("test");

        // Assert
        Assert.False(ReferenceEquals(symbol1, symbol2));
        Assert.NotSame(symbol1, symbol2);
        Assert.Equal(symbol1.Value, symbol2.Value);
    }

    /// <summary>
    /// Tests null string validation.
    /// </summary>
    [Fact]
    public void TestNullValidation()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SSymbol.Intern(null));
    }

    /// <summary>
    /// Tests ToString representation.
    /// </summary>
    [Fact]
    public void TestToString()
    {
        // Arrange
        var symbol = SSymbol.Intern("mySymbol");

        // Act
        var str = symbol.ToString();

        // Assert
        Assert.Equal("#mySymbol", str);
    }

    /// <summary>
    /// Tests GetHashCode is based on the string value.
    /// </summary>
    [Fact]
    public void TestGetHashCode()
    {
        // Arrange
        var value = "testSymbol";
        var symbol = SSymbol.Intern(value);

        // Act
        var hashCode = symbol.GetHashCode();

        // Assert
        Assert.Equal(value.GetHashCode(), hashCode);
    }

    /// <summary>
    /// Tests Length property.
    /// </summary>
    [Fact]
    public void TestLength()
    {
        // Arrange
        var symbol = SSymbol.Intern("hello");

        // Act & Assert
        Assert.Equal(5, symbol.Length);
    }

    /// <summary>
    /// Tests indexer access.
    /// </summary>
    [Fact]
    public void TestIndexer()
    {
        // Arrange
        var symbol = SSymbol.Intern("hello");

        // Act & Assert
        Assert.Equal('h', symbol[0]);
        Assert.Equal('e', symbol[1]);
        Assert.Equal('l', symbol[2]);
        Assert.Equal('l', symbol[3]);
        Assert.Equal('o', symbol[4]);
    }

    /// <summary>
    /// Tests CommonSymbols are properly pre-interned.
    /// </summary>
    [Fact]
    public void TestCommonSymbols()
    {
        // Act & Assert - Arithmetic
        Assert.Equal("+", CommonSymbols.Plus.Value);
        Assert.Equal("-", CommonSymbols.Minus.Value);
        Assert.Equal("*", CommonSymbols.Times.Value);
        Assert.Equal("/", CommonSymbols.Divide.Value);
        Assert.Equal("\\\\", CommonSymbols.Modulo.Value);
        Assert.Equal("//", CommonSymbols.IntegerDivide.Value);

        // Comparison
        Assert.Equal("=", CommonSymbols.EqualsSelector.Value);
        Assert.Equal("~=", CommonSymbols.NotEquals.Value);
        Assert.Equal("<", CommonSymbols.LessThan.Value);
        Assert.Equal("<=", CommonSymbols.LessThanOrEqual.Value);
        Assert.Equal(">", CommonSymbols.GreaterThan.Value);
        Assert.Equal(">=", CommonSymbols.GreaterThanOrEqual.Value);
        Assert.Equal("==", CommonSymbols.IdentityEquals.Value);

        // Object protocol
        Assert.Equal("class", CommonSymbols.Class.Value);
        Assert.Equal("new", CommonSymbols.New.Value);
        Assert.Equal("basicNew", CommonSymbols.BasicNew.Value);
        Assert.Equal("basicNew:", CommonSymbols.BasicNewColon.Value);
        Assert.Equal("at:", CommonSymbols.At.Value);
        Assert.Equal("at:put:", CommonSymbols.AtPut.Value);
        Assert.Equal("size", CommonSymbols.Size.Value);

        // Block protocol
        Assert.Equal("value", CommonSymbols.Value.Value);
        Assert.Equal("value:", CommonSymbols.ValueColon.Value);
        Assert.Equal("value:value:", CommonSymbols.ValueValue.Value);

        // Boolean protocol
        Assert.Equal("ifTrue:", CommonSymbols.IfTrue.Value);
        Assert.Equal("ifFalse:", CommonSymbols.IfFalse.Value);
        Assert.Equal("ifTrue:ifFalse:", CommonSymbols.IfTrueIfFalse.Value);
        Assert.Equal("and:", CommonSymbols.And.Value);
        Assert.Equal("or:", CommonSymbols.Or.Value);
        Assert.Equal("not", CommonSymbols.Not.Value);

        // Collection protocol
        Assert.Equal("do:", CommonSymbols.Do.Value);
        Assert.Equal("select:", CommonSymbols.Select.Value);
        Assert.Equal("collect:", CommonSymbols.Collect.Value);
        Assert.Equal("add:", CommonSymbols.Add.Value);
        Assert.Equal("remove:", CommonSymbols.Remove.Value);

        // Error handling
        Assert.Equal("error:", CommonSymbols.Error.Value);
        Assert.Equal("doesNotUnderstand:", CommonSymbols.DoesNotUnderstand.Value);
        Assert.Equal("primitiveFailed", CommonSymbols.PrimitiveFailed.Value);
    }

    /// <summary>
    /// Tests that CommonSymbols are actually interned.
    /// </summary>
    [Fact]
    public void TestCommonSymbolsAreInterned()
    {
        // Act & Assert
        Assert.Same(CommonSymbols.Plus, SSymbol.Intern("+"));
        Assert.Same(CommonSymbols.Class, SSymbol.Intern("class"));
        Assert.Same(CommonSymbols.IfTrueIfFalse, SSymbol.Intern("ifTrue:ifFalse:"));
        Assert.Same(CommonSymbols.DoesNotUnderstand, SSymbol.Intern("doesNotUnderstand:"));
    }

    /// <summary>
    /// Tests symbols with empty string.
    /// </summary>
    [Fact]
    public void TestEmptyStringSymbol()
    {
        // Act
        var symbol = SSymbol.Intern("");

        // Assert
        Assert.Equal("", symbol.Value);
        Assert.Equal(0, symbol.Length);
        Assert.Equal("#", symbol.ToString());
    }

    /// <summary>
    /// Tests symbols with special characters.
    /// </summary>
    [Fact]
    public void TestSpecialCharacterSymbols()
    {
        // Arrange
        var specialSymbols = new[] { "at:put:", "==", "~=", ">>", "->", ":=", "[]" };

        // Act & Assert
        foreach (var special in specialSymbols)
        {
            var symbol = SSymbol.Intern(special);
            Assert.Equal(special, symbol.Value);
            Assert.Same(symbol, SSymbol.Intern(special));
        }
    }
}