# Validator Migration Script
# This script creates stub migrated validators based on existing validators

$oldPath = "Smalltalk80.Validator\Validators"
$newPath = "Smalltalk80.Validator.Core\Validators"

$validators = @(
    @{Name="LocalIdUniquenessValidator"; Heavy=$false},
    @{Name="SymbolUtf8AndUniquenessValidator"; Heavy=$false},
    @{Name="MethodHeaderValidator"; Heavy=$false},
    @{Name="BytecodeValidator"; Heavy=$true},
    @{Name="LiteralFrameValidator"; Heavy=$false},
    @{Name="PatchValidator"; Heavy=$false},
    @{Name="PatchTargetExistenceValidator"; Heavy=$false},
    @{Name="InstallOrderValidator"; Heavy=$false},
    @{Name="InstallOrderCoverageValidator"; Heavy=$false},
    @{Name="ObjectFormatValidator"; Heavy=$false},
    @{Name="BlockContextClosureGraphValidator"; Heavy=$true},
    @{Name="ClassHierarchySanityValidator"; Heavy=$false},
    @{Name="MethodPrimitiveValidityValidator"; Heavy=$false},
    @{Name="MethodSelectorPresenceValidator"; Heavy=$false},
    @{Name="PoolDictionaryUsageValidator"; Heavy=$false},
    @{Name="DeadCodeDetectionValidator"; Heavy=$true}
)

foreach ($v in $validators) {
    $name = $v.Name
    $heavy = if ($v.Heavy) { "true" } else { "false" }

    $content = @"
using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Migrated from old validator - implementation pending
/// </summary>
public class $name : IValidator
{
    public string Id => "$name";
    public bool IsHeavy => $heavy;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // TODO: Migrate validation logic from old validator

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
"@

    $outputFile = Join-Path $newPath "$name.cs"
    Set-Content -Path $outputFile -Value $content -Encoding UTF8
    Write-Host "Created: $outputFile"
}

Write-Host "`nAll validator stubs created successfully!"
