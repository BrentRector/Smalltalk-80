using System.Text.Json;
using Smalltalk80.Validator.Core;
using Smalltalk80.Validator.Core.Validators;
using Xunit;

namespace Smalltalk80.Validator.Tests.Validators;

public class MetadataCountsValidatorTests
{
    [Fact]
    public async Task ValidMetadata_NoDiagnostics()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/minimal-valid.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new MetadataCountsValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        Assert.DoesNotContain(diagnostics, d => d.Severity == Severity.Error);
    }

    [Fact]
    public async Task InvalidMetadata_HasErrors()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/invalid-metadata.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new MetadataCountsValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        var errors = diagnostics.Where(d => d.Severity == Severity.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("symbolCount"));
    }
}
