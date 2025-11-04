using System.Text.Json;
using Smalltalk80.Validator.Core;
using Smalltalk80.Validator.Core.Validators;
using Xunit;

namespace Smalltalk80.Validator.Tests.Validators;

public class BytecodeValidatorTests
{
    [Fact]
    public async Task ValidBytecode_NoDiagnostics()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/minimal-valid.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new BytecodeValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        Assert.DoesNotContain(diagnostics, d => d.Severity == Severity.Error);
    }

    [Fact]
    public void IsHeavy_ReturnsTrue()
    {
        // Arrange
        var validator = new BytecodeValidator();

        // Assert
        Assert.True(validator.IsHeavy);
    }
}
