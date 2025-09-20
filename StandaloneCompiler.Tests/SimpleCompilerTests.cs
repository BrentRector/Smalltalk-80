namespace StandaloneCompiler.Tests;

/// <summary>
/// Simple placeholder tests for the StandaloneCompiler.
/// </summary>
public sealed class SimpleCompilerTests
{
    /// <summary>
    /// Tests that the compiler project builds successfully.
    /// </summary>
    [Fact]
    public void Compiler_ProjectBuilds_Successfully()
    {
        // Arrange & Act
        var compilerAssembly = typeof(StandaloneCompiler.Program).Assembly;
        
        // Assert
        compilerAssembly.Should().NotBeNull();
        compilerAssembly.GetName().Name.Should().Be("StandaloneCompiler");
    }
    
    /// <summary>
    /// Tests that ANTLR-generated parser classes exist.
    /// </summary>
    [Fact]
    public void Compiler_GeneratedClasses_Exist()
    {
        // Arrange & Act
        var lexerType = typeof(Smalltalk80Lexer);
        var parserType = typeof(Smalltalk80Parser);
        
        // Assert
        lexerType.Should().NotBeNull();
        parserType.Should().NotBeNull();
    }
}