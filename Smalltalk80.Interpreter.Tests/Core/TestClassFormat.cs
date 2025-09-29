using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Comprehensive tests for the ClassFormat enumeration and extensions.
/// </summary>
public class TestClassFormat
{
    /// <summary>
    /// Tests the IsVariable extension method.
    /// </summary>
    [Fact]
    public void TestIsVariable()
    {
        // Act & Assert
        Assert.False(ClassFormat.FixedSize.IsVariable());
        Assert.True(ClassFormat.VariableByteSubclass.IsVariable());
        Assert.True(ClassFormat.VariableWordSubclass.IsVariable());
        Assert.True(ClassFormat.VariableSubclass.IsVariable());
        Assert.False(ClassFormat.CompiledMethod.IsVariable());
        Assert.False(ClassFormat.Weak.IsVariable());
        Assert.False(ClassFormat.Ephemeron.IsVariable());
    }

    /// <summary>
    /// Tests the IsBytes extension method.
    /// </summary>
    [Fact]
    public void TestIsBytes()
    {
        // Act & Assert
        Assert.False(ClassFormat.FixedSize.IsBytes());
        Assert.True(ClassFormat.VariableByteSubclass.IsBytes());
        Assert.False(ClassFormat.VariableWordSubclass.IsBytes());
        Assert.False(ClassFormat.VariableSubclass.IsBytes());
        Assert.False(ClassFormat.CompiledMethod.IsBytes());
        Assert.False(ClassFormat.Weak.IsBytes());
        Assert.False(ClassFormat.Ephemeron.IsBytes());
    }

    /// <summary>
    /// Tests the IsWords extension method.
    /// </summary>
    [Fact]
    public void TestIsWords()
    {
        // Act & Assert
        Assert.False(ClassFormat.FixedSize.IsWords());
        Assert.False(ClassFormat.VariableByteSubclass.IsWords());
        Assert.True(ClassFormat.VariableWordSubclass.IsWords());
        Assert.True(ClassFormat.VariableSubclass.IsWords());
        Assert.False(ClassFormat.CompiledMethod.IsWords());
        Assert.False(ClassFormat.Weak.IsWords());
        Assert.False(ClassFormat.Ephemeron.IsWords());
    }

    /// <summary>
    /// Tests the HasBothNamedAndIndexed extension method.
    /// </summary>
    [Fact]
    public void TestHasBothNamedAndIndexed()
    {
        // Act & Assert
        Assert.False(ClassFormat.FixedSize.HasBothNamedAndIndexed());
        Assert.False(ClassFormat.VariableByteSubclass.HasBothNamedAndIndexed());
        Assert.False(ClassFormat.VariableWordSubclass.HasBothNamedAndIndexed());
        Assert.True(ClassFormat.VariableSubclass.HasBothNamedAndIndexed());
        Assert.False(ClassFormat.CompiledMethod.HasBothNamedAndIndexed());
        Assert.False(ClassFormat.Weak.HasBothNamedAndIndexed());
        Assert.False(ClassFormat.Ephemeron.HasBothNamedAndIndexed());
    }

    /// <summary>
    /// Tests the IsValidForInstSize extension method.
    /// </summary>
    [Fact]
    public void TestIsValidForInstSize()
    {
        // Test FixedSize - valid for any instance size
        Assert.True(ClassFormat.FixedSize.IsValidForInstSize(0));
        Assert.True(ClassFormat.FixedSize.IsValidForInstSize(1));
        Assert.True(ClassFormat.FixedSize.IsValidForInstSize(10));

        // Test VariableByteSubclass - only valid with 0 instance variables
        Assert.True(ClassFormat.VariableByteSubclass.IsValidForInstSize(0));
        Assert.False(ClassFormat.VariableByteSubclass.IsValidForInstSize(1));
        Assert.False(ClassFormat.VariableByteSubclass.IsValidForInstSize(10));

        // Test VariableWordSubclass - only valid with 0 instance variables
        Assert.True(ClassFormat.VariableWordSubclass.IsValidForInstSize(0));
        Assert.False(ClassFormat.VariableWordSubclass.IsValidForInstSize(1));
        Assert.False(ClassFormat.VariableWordSubclass.IsValidForInstSize(10));

        // Test VariableSubclass - valid for any instance size
        Assert.True(ClassFormat.VariableSubclass.IsValidForInstSize(0));
        Assert.True(ClassFormat.VariableSubclass.IsValidForInstSize(1));
        Assert.True(ClassFormat.VariableSubclass.IsValidForInstSize(10));

        // Test CompiledMethod - valid for any instance size
        Assert.True(ClassFormat.CompiledMethod.IsValidForInstSize(0));
        Assert.True(ClassFormat.CompiledMethod.IsValidForInstSize(1));
        Assert.True(ClassFormat.CompiledMethod.IsValidForInstSize(10));

        // Test Weak - valid for any instance size
        Assert.True(ClassFormat.Weak.IsValidForInstSize(0));
        Assert.True(ClassFormat.Weak.IsValidForInstSize(1));
        Assert.True(ClassFormat.Weak.IsValidForInstSize(10));

        // Test Ephemeron - valid for any instance size
        Assert.True(ClassFormat.Ephemeron.IsValidForInstSize(0));
        Assert.True(ClassFormat.Ephemeron.IsValidForInstSize(1));
        Assert.True(ClassFormat.Ephemeron.IsValidForInstSize(10));
    }

    /// <summary>
    /// Tests the enum values match expected constants.
    /// </summary>
    [Fact]
    public void TestEnumValues()
    {
        // Act & Assert
        Assert.Equal(0, (int)ClassFormat.FixedSize);
        Assert.Equal(1, (int)ClassFormat.VariableByteSubclass);
        Assert.Equal(2, (int)ClassFormat.VariableWordSubclass);
        Assert.Equal(3, (int)ClassFormat.VariableSubclass);
        Assert.Equal(4, (int)ClassFormat.CompiledMethod);
        Assert.Equal(5, (int)ClassFormat.Weak);
        Assert.Equal(6, (int)ClassFormat.Ephemeron);
    }

    /// <summary>
    /// Tests typical Smalltalk class format usage patterns.
    /// </summary>
    [Theory]
    [InlineData("Object", ClassFormat.FixedSize, 0, true)]           // Object class
    [InlineData("Point", ClassFormat.FixedSize, 2, true)]            // Point with x, y
    [InlineData("Array", ClassFormat.VariableWordSubclass, 0, true)] // Array (variable)
    [InlineData("ByteArray", ClassFormat.VariableByteSubclass, 0, true)] // ByteArray
    [InlineData("String", ClassFormat.VariableByteSubclass, 0, true)]    // String
    [InlineData("CompiledMethod", ClassFormat.CompiledMethod, 0, true)]  // CompiledMethod
    [InlineData("MethodDictionary", ClassFormat.VariableSubclass, 2, true)] // Has named and indexed
    // Invalid combinations
    [InlineData("InvalidArray", ClassFormat.VariableWordSubclass, 2, false)] // Arrays can't have inst vars
    [InlineData("InvalidByteArray", ClassFormat.VariableByteSubclass, 1, false)] // ByteArrays can't have inst vars
    public void TestTypicalClassFormats(string className, ClassFormat format, int instSize, bool expectedValid)
    {
        // Act
        var isValid = format.IsValidForInstSize(instSize);

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    /// <summary>
    /// Tests that format characteristics are mutually exclusive where appropriate.
    /// </summary>
    [Fact]
    public void TestFormatCharacteristicsMutuallyExclusive()
    {
        // A format cannot be both bytes and words
        foreach (ClassFormat format in Enum.GetValues(typeof(ClassFormat)))
        {
            if (format.IsBytes())
            {
                Assert.False(format.IsWords());
            }
        }
    }

    /// <summary>
    /// Tests combinations of format properties.
    /// </summary>
    [Fact]
    public void TestFormatPropertyCombinations()
    {
        // VariableSubclass should be both variable and words, and support both named and indexed
        var varSubclass = ClassFormat.VariableSubclass;
        Assert.True(varSubclass.IsVariable());
        Assert.True(varSubclass.IsWords());
        Assert.True(varSubclass.HasBothNamedAndIndexed());
        Assert.False(varSubclass.IsBytes());

        // VariableByteSubclass should be variable and bytes, but not words
        var byteSubclass = ClassFormat.VariableByteSubclass;
        Assert.True(byteSubclass.IsVariable());
        Assert.True(byteSubclass.IsBytes());
        Assert.False(byteSubclass.IsWords());
        Assert.False(byteSubclass.HasBothNamedAndIndexed());

        // VariableWordSubclass should be variable and words, but not bytes
        var wordSubclass = ClassFormat.VariableWordSubclass;
        Assert.True(wordSubclass.IsVariable());
        Assert.False(wordSubclass.IsBytes());
        Assert.True(wordSubclass.IsWords());
        Assert.False(wordSubclass.HasBothNamedAndIndexed());

        // FixedSize should not be variable, bytes, or words
        var fixedSize = ClassFormat.FixedSize;
        Assert.False(fixedSize.IsVariable());
        Assert.False(fixedSize.IsBytes());
        Assert.False(fixedSize.IsWords());
        Assert.False(fixedSize.HasBothNamedAndIndexed());
    }

    /// <summary>
    /// Tests that all enum values are covered by extension methods.
    /// </summary>
    [Fact]
    public void TestAllEnumValuesCovered()
    {
        foreach (ClassFormat format in Enum.GetValues(typeof(ClassFormat)))
        {
            // Every format should have a defined behavior for these methods
            _ = format.IsVariable();
            _ = format.IsBytes();
            _ = format.IsWords();
            _ = format.HasBothNamedAndIndexed();
            _ = format.IsValidForInstSize(0);
            _ = format.IsValidForInstSize(5);
        }
    }
}