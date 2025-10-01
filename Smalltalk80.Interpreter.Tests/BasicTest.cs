using Xunit;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Basic test to ensure the test project compiles and runs.
/// </summary>
public class BasicTest
{
    /// <summary>
    /// Simple test to verify the test framework is working.
    /// </summary>
    [Fact]
    public void TestFrameworkIsWorking()
    {
        // Arrange
        const int expected = 42;

        // Act
        var actual = 40 + 2;

        // Assert
        Assert.Equal(expected, actual);
    }
}