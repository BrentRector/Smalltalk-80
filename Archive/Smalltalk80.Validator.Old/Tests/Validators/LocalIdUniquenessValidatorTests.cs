using System.Text.Json;
using Smalltalk80.Validator.Core;
using Smalltalk80.Validator.Core.Validators;
using Smalltalk80.Validator.Core.Diagnostics;
using Xunit;

namespace Smalltalk80.Validator.Tests.Validators;

public class LocalIdUniquenessValidatorTests
{
    [Fact]
    public async Task UniqueLocalIds_NoDiagnostics()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/minimal-valid.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new LocalIdUniquenessValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == Severity.Error));
    }

    [Fact]
    public async Task DuplicateLocalIds_HasErrors()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/duplicate-localid.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new LocalIdUniquenessValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        var errors = diagnostics.Where(d => d.Severity == Severity.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == ErrorCodes.LocalIdDuplicate);
    }
}
